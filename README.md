# PvZ Fusion Co-op

Unofficial cooperative campaign mod for **PvZ Fusion 4.1 on Windows (IL2CPP)**. Defend the same lawn with friends. Developed with OpenAI Codex.

[Українською](README.uk.md) · [Download the tested P0 patch](https://github.com/dwdwqdwq/pvz-fusion-coop/releases/tag/coop-p0-2026-11-23)

## Current build: P0 review — 23 November 2026

This is a **co-op mod update**, not a new Fusion release. Internal mod version: `0.12.13`; protocol: `fusion41-coop-p0-atomic-v2`. The version number alone does not identify this candidate: compare the DLL hash below.

- Host-authoritative zombie deaths and protection against stale packets recreating dead entities.
- Planting and shared sun handled as one operation, with duplicate-request protection, expiry and explicit failure handling.
- Synchronization checks for glove moves, mine maturity, Chomper, explosions, HP/armor, status effects, pause/restart and campaign rewards.
- Teammate cards and cooldowns, placement previews, and English/Ukrainian interface selection.

## Install / update

1. Use Fusion 4.1 with a working BepInEx IL2CPP installation and the existing co-op dependencies, including `Steamworks.NET.dll`. This small patch is **not a full game or a complete first-time mod-loader installation**.
2. Close the game and back up `BepInEx/plugins/PvZCoop/PvZCoop.dll`.
3. Download `PvZ-Fusion-Coop-P0-review-2026-11-23.zip` from the release and extract it into the game directory, replacing that DLL. Your saves and language configuration are not included or replaced.
4. **All players must install the exact same DLL. Older releases are incompatible with this protocol.**

DLL SHA-256:
```text
c1d979503b688ae8ef5657dd7232f1d7aae503831eed54cad23c257c0acee68a
```

## Connect and play

1. Join the same LAN, or the same **Radmin VPN / ZeroTier One** virtual network for remote play. These services are external and are not bundled.
2. Open CO-OP. The host creates a room using a TCP port (default `27650`).
3. Guests enter the host's LAN/VPN IPv4 address and the same port. Allow the game's connection on the appropriate private network.
4. The host selects a normal level. Everyone selects cards and presses READY.

No internet matchmaking or relay is included. Recovery from a fully closed TCP connection is not validated by this release.

## Language

Choose **LANGUAGE → English / Українська** in the main menu, then restart. Alternatively set `[Language] Interface = en` or `uk` in `BepInEx/config/pvzfusion.coop.cfg` while the game is closed. Language settings are local.

## Verification and limits

**17 paired native game runs across 12 scenarios**, using two isolated copies on one PC, an ordinary lawn and difficulty 1. Controlled TCP tests used 200/500/800 ms delays, jitter, repeated/dropped application messages and short stream stalls. Real entity IDs, HP/armor, timers, positions and animation clips were compared; deliberately corrupted measurements were rejected.

This is **not** proof of a bug-free full campaign, all 633 plants, four-player gameplay, or two physical PCs over the internet. Four-player support is experimental and was not tested in this pass. Application-message loss is not the same as IP packet loss. See [verification](docs/VERIFICATION.md).

## Build

Use .NET SDK 8+ and your own compatible game assemblies (not included):
```powershell
./build.ps1 -GameDir 'D:/Games/Fusion-4.1'
dotnet run --project tests
dotnet run --project plant-operation-tests
dotnet run --project zombie-verification-tests
dotnet run --project glove-transaction-tests
```
Output: `src/bin/Release/net6.0/PvZCoop.dll`. Building does not install the DLL. Locally rebuilt binaries may have a different hash from the tested release.

## Credits and scope

Co-op adaptation and Ukrainian localization developed with **OpenAI Codex**. PvZ, Fusion and the original artwork, font and data belong to their respective creators. Some co-op UI material comes from the supplied Chinese ecology mod; its original source/author has not been established. This is an unofficial fan modification. No blanket license is asserted over third-party code or game-derived assets. BepInEx, Il2CppInterop and Steamworks.NET retain their own licenses. See [Steamworks.NET notice](docs/Steamworks.NET-LICENSE.txt).
