# Validation record

## Web release — September 28, 2026

The WebGL/IL2CPP release compiled in Unity 6000.6.0f1 with exit code 0, zero errors, and one shader deprecation warning. The browser ZIP passed CRC and itch.io package-limit checks.

On the itch.io-hosted version, the following were observed: opening/Play, ammunition switch, teleport beacon firing with its animation, teleporting to the beacon, relic pickup with score increment, and fullscreen. Pause was verified in the local browser build. The game rendered correctly; a nonfatal warning reported an unsupported FSR upscaling shader.

## Prior editor regression checks

Controlled Editor Play checks verified forward and return travel between the three levels; restoration of relics, score, elapsed time, and health; safe arrival locations; rejection of ordinary bullets and teleport beacons by cross-level gates; relic requirements at forward/final gates; and the difference between current-level Restart and final-victory Restart.

These were targeted regression checks, with test positioning and some isolated enemy conditions. They were not a new uninterrupted, fully manual three-level completion run. The Web release received a first-level smoke test rather than a repeat of every puzzle and movement route.

## Known behavior

- Progress persists only within the current running game. Reloading/closing the browser resets it.
- Enemies and local switches reload when changing scenes.
- Keyboard gameplay is intended for desktop/laptop browsers.
- This source repository requires the separately obtained 2D Game Kit dependency; see SETUP.md.

## Source handoff checks

The dependency setup script was run against a fresh isolated project copy using the verified 2D Game Kit 5.0 archive. It restored 4,244 kit files and applied four patches. A second run and the read-only `--check` both passed without changes. All 166 restored kit C# files matched the working game after newline normalization, and restored asset metadata GUIDs matched. A fresh Unity import/build of that isolated setup was not repeated.

All three submitted game scene files are byte-identical to the working project. The repository's only code difference from that project is the Web export helper's portable output path; that helper was separately compile-checked against Unity 6000.6.0f1. The design PDF was rendered and visually checked as exactly two pages.
