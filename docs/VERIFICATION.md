# P0 verification / Перевірка P0

Build: `coop-p0-2026-11-23`, internal version0.12.13, protocol `fusion41-coop-p0-atomic-v2`.
DLL SHA-256: `c1d979503b688ae8ef5657dd7232f1d7aae503831eed54cad23c257c0acee68a`.

17 paired native runs /17 парних прогонів;12 scenarios /12 сценаріїв:

- death-verification; atomic-planting; atomic-fault; chomper-full; glove; family; family-motion; connected-campaign; session-controls; death-matrix; fast-mine-identity; sun.
- Two isolated Fusion4.1 copies, Windows, Unity2022.3.62f1c1, BepInEx6.0.0-be.793, .NET6.0.7. Ordinary lawn, difficulty1.
- 200/500/800ms transport delay, jitter, duplicate/dropped application messages,3s socket-write stalls; independently replayed pinned chaos-model verdicts. This is not IP packet-loss emulation.
- Paid native planting tested with one debit/one identity, duplicate retry/result loss, expiry and injected failure after consumption. The ambiguous failure pauses both copies and requires restart; no fabricated refund.
- Glove identity/maturity, Chomper digestion, five explosive plant types, HP/armor and limb loss, local effect expiry, sun visuals, pause/resume/restart and controlled native campaign victory/reward fixture.
- Full seven-family native audit: Peashooter, SunFlower, PotatoMine, Chomper, Squash, WallNut, Pumpkin.
- Final moving ConeZombie/Peashooter run through800±150ms each way:443/428 complete observer samples;262 compared states;0 sustained mismatches; position p95=.091 world units, animation-group mismatch3.05%, detected glitches0. Comparison explicitly accounts for known network delay (900ms+150ms allowance; state matching1000ms). No measured bones, facing or pose. Negative position and stale-HP controls rejected.

## Failures kept visible / Невдалі спроби

An earlier hybrid-mine test failed its8.5s wall-clock deadline; a separate native measurement showed5s simulation maturity, and a real6s pause reproduced why wall time is invalid. The corrected game-clock test passed; a deliberately wrong wall-clock control failed as expected. Earlier observer code missed a real animation on layer1; all layers were then measured. A restricted File.Replace failure affected isolated test-save restart; rerun with permitted access to those same test saves passed. No production save bypass was added. A campaign HP test incorrectly bypassed the full native hit path; rerun using the real TakeDamage path passed without manually refreshing HP text. These QA corrections are not additional gameplay fixes.

## Limits / Межі

No complete campaign playthrough, all633 plants,4-player run or two physical internet PCs. Native victory audio function invoked once per copy, not verified by listening. Short stream stalls do not prove reconnect after socket closure. Passing bounded scenarios is not a bug-free guarantee. Private raw logs, machine paths, saves and test helpers are not published.
