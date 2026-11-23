"""Prove the managed transaction tests reject deliberately broken copies.

All generated sources, build output and logs stay under ignored integration/.
Does not edit the real implementation or launch the game.
"""
from pathlib import Path
import os
import subprocess
import uuid


root = Path(__file__).resolve().parents[1]
run = root / "integration" / ("plant-journal-mutants-" + uuid.uuid4().hex[:8])
run.mkdir(parents=True)
dotnet = root.parent / "coop-dev/tools/dotnet/dotnet.exe"
env = dict(os.environ, DOTNET_CLI_HOME=str(root.parent / "coop-dev/tools/cli"))
source = (root / "src/PvZCoop/PlantOperationJournal.cs").read_text(encoding="utf-8-sig")
mutations = {
    "baseline": None,
    "allow-reentrant-placement": ("entry.Phase!=State.Granted||", "entry.Phase==State.Waiting||"),
    "late-cancel-overwrites-success": ("if(entry.Phase==State.Complete)return true;", "// broken: overwrite terminal result"),
    "allow-expired-placement": ("entry.Phase!=State.Granted||now>=entry.Expires||", "entry.Phase!=State.Granted||false||"),
    "wrong-player-can-spend": ("(player,request.OperationId)", "(2,request.OperationId)"),
    "allow-intent-substitution": ("if(entry.Request!=null)return entry.Request==request;", "if(entry.Request!=null)return true;"),
}
for name, mutation in mutations.items():
    case = run / name
    case.mkdir()
    candidate = source
    if mutation:
        before, after = mutation
        if source.count(before) != 1:
            raise RuntimeError("Mutation no longer targets exactly one site: " + name)
        candidate = source.replace(before, after)
    (case / "PlantOperationJournal.cs").write_text(candidate, encoding="utf-8")
    for file in ("PlantTransaction.cs", "PlantSpawnPacket.cs", "SunLeaseQueue.cs"):
        (case / file).write_bytes((root / "src/PvZCoop" / file).read_bytes())
    (case / "Program.cs").write_bytes((root / "plant-operation-tests/Program.cs").read_bytes())
    (case / "Mutant.csproj").write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net8.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings></PropertyGroup></Project>', encoding="utf-8")
    result = subprocess.run([str(dotnet), "run", "--project", str(case / "Mutant.csproj"), "-c", "Release"],
                            cwd=root, env=env, capture_output=True, text=True)
    log = result.stdout + result.stderr
    (case / "result.txt").write_text(log, encoding="utf-8")
    # Build failures are not valid killed mutants: require a failed assertion.
    if name == "baseline":
        if result.returncode or "COMPLETE 42 plant operation assertions" not in log:
            raise RuntimeError("Baseline did not pass; see " + str(case))
    elif result.returncode == 0 or "Unhandled exception. System.Exception:" not in log:
        raise RuntimeError("Mutant not killed by an assertion: " + name + "; see " + str(case))
    print("PASS " + ("baseline" if mutation is None else "killed " + name), flush=True)
print("COMPLETE baseline + 5 killed transaction mutants; " + str(run))
