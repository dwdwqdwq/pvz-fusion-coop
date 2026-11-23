# PvZ Fusion Co-op — Multiplayer Mod

Play **PvZ Fusion 4.1** together: defend the same lawn and complete the normal campaign in co-op.

[Download the mod](https://github.com/dwdwqdwq/pvz-fusion-coop/releases/tag/coop-p0-2026-11-23) · [Українською](README.uk.md)

## Features

- Cooperative play on one shared battlefield.
- Shared sun balance and coordinated card selection, including Fusion's extra card copies.
- Teammate names, card previews with cooldowns and planting indicators.
- Host and join by IP address and TCP port.
- English and Ukrainian interface.

## Install or update

Requires **PvZ Fusion 4.1 for Windows**, a working **BepInEx IL2CPP** setup and the co-op dependencies, including `Steamworks.NET.dll`.

1. Download **PvZ-Fusion-Coop-Multiplayer-Mod-0.12.13.zip** from the release.
2. Close the game and back up `BepInEx/plugins/PvZCoop/PvZCoop.dll`.
3. Extract the ZIP into the game folder and replace the DLL.
4. Install the **same mod build on every player's PC**.

This download is a mod update, not the full game or a new version of Fusion. The loader and dependency setup are not bundled. Existing saves and language settings are preserved. Older co-op builds use a different protocol and cannot join this build.

## How to play

1. Connect to the same LAN. For remote play, join the same **Radmin VPN** or **ZeroTier One** virtual network.
2. Open **CO-OP**. The host creates a room and selects a TCP port (default `27650`).
3. Guests enter the host's LAN/VPN IPv4 address and the same port.
4. The host selects a normal level. Everyone chooses cards and presses **READY**.

Allow the game's connection on the appropriate private network. VPN software is installed separately; matchmaking and an internet relay are not included.

## Language

Select **LANGUAGE → English / Українська** in the main menu, then restart the game. Each player can use their own language.

## Credits

Co-op adaptation and Ukrainian localization developed with **OpenAI Codex**. This is an unofficial fan modification. PvZ, Fusion, original artwork, fonts and game data belong to their respective creators. Some co-op UI material comes from the supplied Chinese ecology mod; its original author/source has not been established. No blanket license is asserted over third-party code or game-derived assets. Dependencies retain their own licenses; see the [Steamworks.NET notice](docs/Steamworks.NET-LICENSE.txt).
