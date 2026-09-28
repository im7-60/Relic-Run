# Set up the Unity source project

## Required versions

- **Unity Editor 6000.6.0f1** (Unity 6.6), installed through Unity Hub.
- **2D Game Kit 5.0** by Unity Technologies, Asset Store product **107098**, upload **885608**.
- **Python 3.9 or newer** for the included setup script. It uses only the Python standard library.
- Unity's **Web Build Support** module if you want to export a browser build.

The game's custom scenes and scripts are in this repository. The raw imported kit is an external dependency and is excluded from Git. Acquire it from its [official Unity Asset Store listing](https://marketplace.unity.com/packages/templates/packs/2d-game-kit-2d-sample-project-107098). The listing and its included third-party notice specify the [Unity Asset Store EULA](https://unity.com/legal/as-terms). No payment is required for this kit.

## Restore the dependency before opening this project

1. Clone or download this repository.
2. Add the kit to your own Unity account. In a separate temporary Unity project, open Package Manager, choose **My Assets**, select **2D Game Kit**, and **Download** version 5.0. You do not need to import it into that temporary project.
3. Locate your downloaded `.unitypackage` file. On Windows the default cache is under `%APPDATA%\Unity\Asset Store-5.x\Unity Technologies\Complete ProjectsPacks\`. Its filename is `2D Game Kit 2D Sample Project.unitypackage`. A custom Asset Store cache location can be viewed in Unity's preferences.
4. Close Unity for the Relic Run project. From the repository folder run:

   ```powershell
   py -3 tools/setup_kit.py --package "C:\path\to\2D Game Kit 2D Sample Project.unitypackage"
   ```

   On macOS or Linux, use `python3` in place of `py -3` and supply your downloaded package's path.

5. The script verifies the package, restores only `Assets/2DGamekit` and its metadata, and applies four small project-specific changes. It does **not** import the kit's `Packages` or `ProjectSettings`, so this project's settings and three build scenes remain intact.
6. Open this repository's folder through Unity Hub with **6000.6.0f1**. Allow Unity to resolve packages and import assets. Open `Assets/CrystalRuins/Scenes/RelicRun.unity`, then press Play in the editor and Play in the game.

The package verified for this release is **301,187,093 bytes**, SHA-256:

```text
9fb3f15d73cf3035928a7f379aabd273b0e56b26e86ddd2e8d540f9d4b7c10bb
```

The script refuses a different archive or unexpected contents in a patched file. If Unity supplies another package version, obtain version 5.0 or review the differences before updating the patch data; do not bypass the check blindly. Do not commit the downloaded package or restored kit assets.

## What the patches change

The compact `tools/kit-patches.json` contains changes relative to the licensed kit; it does not contain complete imported scripts or raw art/audio.

| Kit file | Project change |
|---|---|
| `InputComponent.cs` | Creates input actions lazily after Input System startup and guards action shutdown. |
| `EnemyBehaviour.cs` | Adds an optional forward obstacle probe limit used by the first level's Chompers. |
| `PlayerCharacter.cs` | Exposes the existing ranged attack animation for teleport shots. |
| `Bullet.mat` | Uses transparent render queue 3000 for the normal projectile material. |

The pressure pad's player-only filter is saved in the authored scene; the kit's `PressurePad.cs` is unchanged. Unity may update sprite atlases, importer metadata, and rendering settings during its normal first import.

The patch command is safe to repeat: already-patched files are checked and skipped. After the dependency exists, these commands are also available:

```powershell
py -3 tools/setup_kit.py
py -3 tools/setup_kit.py --check
```

The second command checks patch hashes without writing. Keep Unity closed while applying patches, then reopen it or refresh assets.

## Editing and building

- Keep all `.meta` files with their assets. The scenes reference kit assets by their original GUIDs.
- The build scene order is `RelicRun`, `RelicRun2`, `RelicRun3`; do not replace the checked-in `ProjectSettings` with the kit's example settings.
- Edit terrain and object positions in the existing scenes. Level-generation menu commands can replace those authored layouts, so use them only when intentionally rebuilding a level.
- To export WebGL, install Web Build Support, select Web as the active platform, save your scenes, then use **Relic Run > Build itch.io Web player**. The default output is `Build/RelicRun-Web/Web` inside the project. Zip that folder's contents, with `index.html` at the ZIP root.

You can play the submitted game immediately at [itch.io](https://im760.itch.io/relic-run) without setting up Unity.
