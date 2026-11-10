# Verification

The public release was tested in an isolated Fusion 4.1 installation.

Checks to run for a language release:

- Clean build against compatible Fusion 4.1 BepInEx interop files.
- Transport tests: bidirectional TCP, 100 ordered messages, large Unicode frame, fragmentation, disconnect and oversize rejection.
- Fresh English session: original artwork/text, English co-op IP hint, working language panel, persisted Ukrainian selection and restart notice.
- Fresh Ukrainian session: translated artwork/text, Ukrainian co-op hints, special glyphs, persisted English selection and restart notice.
- Fresh English session again: restart persistence; rejected unsupported selection cannot corrupt the saved preference.
- Original personal DLL, friend DLL and full-game archive retain their recorded SHA-256 fingerprints.

Completed: build with 0 warnings and 0 errors; all language/transport tests passed; 39 runtime assertions passed across three fresh game sessions. See runtime-verification.txt and screenshots/. No test plugin is included in the player release.
