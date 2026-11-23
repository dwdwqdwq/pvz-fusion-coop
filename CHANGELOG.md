# Changelog / Історія змін

# P0 review — 2026-11-23

## Українською

P0 review-патч для **PvZ Fusion4.1**, а не нова версія Fusion. Внутрішній номер кооперативу0.12.13; протокол atomic-v2. Оновлено захист смертей від запізнілих пакетів та операцію посадки/спільного сонця. Перевірено рукавичку, Чомпера, мінні таймери, вибухи, HP/броню, ефекти, паузу/рестарт і нагороди у визначених сценаріях.

17 парних прогонів у12 сценаріях, дві копії на одному ПК; затримки200–800мс і коливання мережі. Повне проходження, всі рослини,4гравці та два фізичні ПК через інтернет не перевірені.

**Завантажуйте ZIPпатча в Assets.** Це не повна гра: потрібні сумісна Fusion4.1, BepInEx IL2CPP та наявні залежності кооперативу. Закрийте гру, збережіть стару DLL і розпакуйте патч у папку гри. У всіх гравців має бути однакова DLL. Старі релізи несумісні. Мову English/Українська можна обрати в меню LANGUAGE; після зміни перезапустіть гру. Для віддаленої гри можна використовувати Radmin VPN або ZeroTier One.

## English

P0 review **co-op patch for PvZ Fusion4.1**, not a new Fusion release. Internal co-op version0.12.13; atomic-v2 protocol. Updates host-authoritative death/stale-packet protection and atomic planting/shared-sun handling. Bounded regression scenarios cover glove moves, Chomper, mine timers, explosions, HP/armor, effects, pause/restart and rewards.

17 paired native runs across12 scenarios, two isolated copies on one PC; controlled200–800ms delays and jitter. Full campaign completion, all plants,4players and two physical internet PCs remain untested.

**Download the patch ZIP from Assets.** Not a full game: requires compatible Fusion4.1, BepInEx IL2CPP and existing co-op dependencies. Close the game, back up the previous DLL and extract into the game directory. All players need the exact same DLL; older releases are incompatible. Choose English/Українська under LANGUAGE and restart. Radmin VPN or ZeroTier One can provide a virtual network for remote play.

## SHA-256

DLL: `c1d979503b688ae8ef5657dd7232f1d7aae503831eed54cad23c257c0acee68a`

Patch ZIP: `3ae545548f6bab248ca9607e63f26ee7596fe3f414d2ce82e1f5609bd0fa061b`

Developed with OpenAI Codex / Розроблено за допомогою OpenAI Codex.


## Historical0.11.0

Initial public two-player build. Its protocol is incompatible with the current candidate. Historical release downloads are not the current patch.
