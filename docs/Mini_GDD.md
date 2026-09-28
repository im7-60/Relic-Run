# Relic Run

**Mini Game Design Document | Unity 2D platform adventure | September 28, 2026**

[Play in a browser](https://im760.itch.io/relic-run) - Desktop or laptop keyboard required.

## Concept and inspiration

Ellen explores three ancient ruins, collecting relics and finding a safe route forward. The central idea is to turn a projectile into a destination: the player fires a cyan beacon, watches its flight, and chooses when to teleport to it. Later puzzles combine that timing with portals and selectable routes.

The project builds on Unity Learn's **2D Game Kit Walkthrough**. Familiar movement, combat, art, animation, and audio support a progression from platform action to spatial puzzles. The design goal is to make the same small set of controls useful in increasingly interesting situations.

## Core mechanics and controls

| Controls | Action |
|---|---|
| A / D; Space | Move left / right; jump |
| W / S; K | Look up / crouch; melee attack |
| B; O; L | Switch ammo; fire; teleport to the active beacon |
| E; Esc | Change route near an archive console; pause / resume |

Movement uses gravity and solid platforms. Normal shots and melee attacks deal with enemies; spikes, enemies, and falls create risk. Health pickups support recovery. The HUD displays five health slots, relics, score, elapsed time, and the selected ammunition. The opening screen includes Play; both results screens include Restart.

Teleport ammunition has one active beacon. Another shot replaces it. It moves horizontally at 14 units per second and disappears after 18 units, 1.5 seconds, or a solid collision, whichever happens first. Jumping changes the shot's height. Pressing L requires enough room for Ellen's body at the destination; teleporting does not create a floor. Both ammo types use the same gun animation.

## Goals, scoring, and restart

- **Progress:** Levels 1 and 2 each contain eight relics; six unlock the forward gate. Level 3 requires all four relics. Only Ellen's body can activate a cross-level gate or final exit, so firing at a door cannot finish a level.
- **Win:** Collect the four archive relics and enter the final exit. Each relic awards 100 points. The final exit adds 500 plus 50 per remaining health point. The maximum final-level score is 1,150; the results display this level's score, not a campaign total.
- **Lose:** Zero health or falling into a fall zone ends the attempt. Restart from failure or pause resets the current level. Restart after the final victory clears the expedition and returns to level 1.
- **Revisit:** Purple gates connect level 2's start to level 1's end and level 3's start to level 2's end. In the same game session, each level retains its relics, score, elapsed time, and health. Time stops accumulating while away. Enemies and local switches reload. Refreshing or closing the browser clears progress.

## Three-level experience

1. **The Verdant Ruins:** Establish movement, combat, relic collection, spikes, a moving bridge, and a pressure-pad gate. Ellen can walk onto the pad; enemies cannot open it. Optional relics reward exploration after the six-relic requirement is met.
2. **The Shattered Causeway:** Raised platforms and gaps emphasize beacon height, travel distance, and safe landings. The player combines jumping and teleporting while collecting relics between crossings.
3. **The Folded Archive:** Four required relics connect a sequence of portal puzzles. Walk through a BODY portal to reach a balcony, then fire a beacon across a wide gap. Next, send a beacon through an ORB ONLY portal to get past a wall. At the hub, press E to select the amber, cobalt, or exit route. Collect both branch relics, use BODY return doors, and choose the exit route to finish. BACKTRACK doors allow missed relics to be recovered.

## Creative additions and why they matter

**Multiple levels:** Three environments develop the same mechanics in stages: basic action, precise teleporting, then combined portal puzzles. Return gates let players revisit earlier areas without losing their session's collection progress.

**Player-timed teleport shots:** The player chooses both a direction and an arrival moment. Limited range, lifetime, and body clearance make positioning and safe landing decisions part of the challenge.

**Portals and route selection:** BODY portals carry Ellen; ORB ONLY portals carry cyan beacons. Normal bullets cannot use them. Beacon portals preserve remaining time and range, and a remote camera view helps judge when to press L. The route console adds planning around a reusable hub.

**Animation and sound feedback:** Kit character animations and combat sounds, plus pickup and result sounds, make actions and outcomes easier to read. Color-coded portals, route labels, and brief hints explain the added mechanics during play.

## Credits and implementation scope

**Unity Technologies - 2D Game Kit:** Ellen, the base movement and combat systems, enemies, environment art, animations, audio, and interaction components are supplied by the kit.

**Project-specific additions:** Level layouts, relic scoring and HUD, menus, body-triggered progression, teleport ammunition, portal routing, archive puzzles, and session travel records. Custom code and game text were developed with AI assistance. Kit assets and code are not claimed as original student work.

**Reference:** [Unity Learn - 2D Game Kit Walkthrough](https://learn.unity.com/course/2d-game-kit/tutorial/2d-game-kit-walkthrough?version=2017.3). Created in Unity 6000.6.0f1 and published as a WebGL game. This document describes the implemented design; it is not a claim of exhaustive play-test coverage.
