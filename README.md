# Relic Run

A three-level 2D platform adventure made in **Unity 6000.6.0f1** using Unity Technologies' **2D Game Kit**. Explore ancient ruins, collect relics, fight enemies, and choose when to teleport to a flying beacon. The final level combines body portals, beacon-only portals, and a route-selection puzzle.

## Assignment submission links

1. **Playable WebGL game:** [Play Relic Run on itch.io](https://im760.itch.io/relic-run)
2. **GitHub source repository:** [im7-60/Relic-Run](https://github.com/im7-60/Relic-Run)
3. **Two-page game design document:** [Mini_GDD.pdf](docs/Mini_GDD.pdf) ([readable Markdown version](docs/Mini_GDD.md))

Click **Run game**, then **Play**. A desktop/laptop keyboard is required. Click the game to focus keyboard input; use its bottom-right fullscreen button for a larger view. Refreshing or closing the game clears the current expedition.

## Assignment requirements

| Requirement | Implementation |
|---|---|
| Player movement and jumping | Ellen moves with A/D, jumps with Space, and collides with solid platforms. |
| A level with platforms | Three custom levels with platforms, gaps, and a moving bridge. |
| Collectible and score counter | Relics award 100 points each; the HUD displays relic count and score. |
| Hazard or enemy | Enemies, spikes, and fall zones; health is shown in the HUD. |
| Win and lose conditions | Collect the required relics and enter the final exit to win; zero health or falling ends the attempt. |
| Start and end screens | Opening screen with Play; victory/failure screens with Restart; pause menu with Resume/Restart. |
| At least two extra features | Two additional levels, player-timed teleport ammunition, portal-routing puzzles, animation/sound feedback, and return travel between levels. |

## Controls

| Key | Action |
|---|---|
| A / D | Move left / right |
| Space | Jump |
| W / S | Look up / crouch |
| K | Melee attack |
| B | Switch normal / teleport ammunition |
| O | Fire the selected ammunition |
| L | Teleport to the active cyan beacon |
| E | Change the route near a level 3 console |
| Esc | Pause / resume |

Only one beacon can be active. It disappears after 18 units, 1.5 seconds, or a solid collision, whichever happens first. A new shot replaces it. The destination must have room for Ellen's body; teleporting does not create a floor.

## Levels and progression

- **The Verdant Ruins:** movement, combat, a moving bridge, and a player-only pressure-pad gate. Collect at least **6 of 8** relics to enter level 2.
- **The Shattered Causeway:** raised platforms and gaps that reward jumping and carefully timed teleport shots. Collect at least **6 of 8** relics to enter level 3. Its starting return gate leads to level 1's end.
- **The Folded Archive:** collect **all 4** relics using BODY portals, ORB ONLY portals, and a console selecting amber, cobalt, or exit routes. Its starting return gate leads to level 2's end.

Walk into a cross-level gate or the final exit: bullets cannot activate these transitions. Return travel preserves each level's relics, score, time, and health within the current session. Enemies and local switches reload. Pause/failure Restart resets the current level; victory Restart clears the expedition and returns to level 1.

The final exit awards 500 points plus 50 per remaining health point. The result screen shows the final level's score, not a campaign total.

## Open the source project

This repository includes the project's **Assets**, **Packages**, and **ProjectSettings** folders. Unity-generated caches and exported player builds are excluded.

**Import the free 2D Game Kit dependency before opening the project.** The imported Asset Store kit is not redistributed in this public repository. [SETUP.md](docs/SETUP.md) explains how to use your own downloaded kit and apply the included compatibility changes. This preserves the asset GUIDs referenced by the scenes.

After setup, open the repository folder in Unity Hub using **6000.6.0f1**, allow packages/imports to finish, then open:

`Assets/CrystalRuins/Scenes/RelicRun.unity`

Press the editor's Play button, then the game's **Play** button. The build scene order is `RelicRun`, `RelicRun2`, `RelicRun3`.

To export WebGL, install Unity's **Web Build Support**, save all scenes, select Web as the active build platform, and use **Relic Run > Build itch.io Web player**. The default output is `Build/RelicRun-Web/Web` inside this project. Zip that folder's contents so `index.html` is at the ZIP root. The included template uses gzip with decompression fallback and does not require SharedArrayBuffer. Command-line builds can override the output with `-relicWebOutput`.

## Repository guide

- `Assets/CrystalRuins/Runtime/`: relics, scoring/UI, teleport shots, portals, and campaign state.
- `Assets/CrystalRuins/Scenes/`: the three authored levels.
- `Assets/CrystalRuins/Editor/`: level-authoring tools and Web export helper. Do not regenerate scenes over hand-edited layouts.
- `Assets/WebGLTemplates/RelicRun/`: browser loading screen and responsive canvas.
- `docs/`: the two-page design document, dependency setup, and validation notes.

## Credits

Unity Technologies' **2D Game Kit** supplies Ellen, base movement/combat, enemies, environment art, animations, audio, and interaction components. Project-specific additions include level layouts, relic scoring/HUD, menus, teleport beacons, portal routing, puzzles, and travel records. Custom code and game text were developed with AI assistance; the kit's code and assets are not claimed as original student work.

Reference: [Unity Learn — 2D Game Kit Walkthrough](https://learn.unity.com/course/2d-game-kit/tutorial/2d-game-kit-walkthrough?version=2017.3).

The imported kit remains subject to its own license. This repository does not grant a blanket open-source license over Unity or third-party assets.
