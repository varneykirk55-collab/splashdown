# Course 1 visual integration — playtest handoff

Branch: `astra/pads-obstacles-integration` · Unity 6000.6.6f1 (URP) · No merge to main, no pull request.

## Open and play

1. Open the project folder `C:\Users\aresg\OneDrive\Documents\Splashdown-Astra` in Unity Hub with editor **6000.6.6f1**. Wait for import and compilation (first open also imports the 13 new FBX files).
2. Open the scene `Assets/BikiniWipeout/Scenes/Course_SplashdownGauntlet.unity`.
3. Press **Play**. The scene contains the Course 1 player and the integrated visuals. (The main menu's PLAY button also starts this course.)
4. Do **not** run *Bikini Wipeout > Course 1 > Rebuild Splashdown Gauntlet*: it regenerates the scene and would bring back the old jump pads.

## What is integrated (all visual-only)

| Item | Where |
| --- | --- |
| Small and large sweepers, double sweeper, rotating beam | prefabs `SweeperSmall`, `SweeperLarge`, `SweeperDouble`, `RotatingBeam` (`Assets/BikiniWipeout/Prefabs/Course1/`) |
| Spin platform, pendulum, piston wall, bouncing-ball arena | prefabs `SpinPlatform`, `Pendulum`, `PistonWall`, `BouncingBallArena` |
| Seven jump pads | scene objects `BouncePad`, `FinalPad_1..4`, `LaunchPad_1..2` (child `SD_Visual_PadHead`) |
| Models, materials | `Assets/BikiniWipeout/Art/SD/Models` (13 FBX), `Art/SD/Materials` (SD_Red, SD_Blue, SD_White, SD_Chrome; URP Lit) |
| Blender sources, scripts, previews | `ArtStaging/Blender/` (outside `Assets/`) |

The old primitive renderers are **disabled, not deleted**; all colliders, rigidbodies, scripts, layers and pivots are as they were.
The dark support columns under the pads and the spin platform, foam rings, water, gates and other platforms are unchanged.

## Verification (see `ArtStaging/Integration/`)

- `handoff_verification.txt`: read-only Unity check of real references: all 8 prefabs and 27 scene instances plus 7 pads point at valid SD meshes and materials; no colliders on visuals; no old primitive renderer enabled; no missing references or scripts; ALL CHECKS PASSED.
- Gameplay dumps before/after each stage (`gameplay_*.txt`): identical, including a whole-scene dump for the final stage.
- Cumulative change versus the Phase 3B base: 8 prefabs, 1 scene, `Art.meta`, new `Art/SD` folder. No scripts, settings or packages changed.
- Tests, last run on the final integrated state (EditMode 157/157, `Course1PlayTests` 9/9): `editmode_results.xml`, `playmode_course1_results.xml`. Only Course 1 PlayMode tests were run; the rest of the PlayMode suite was not. After those runs the only addition was this read-only editor check.
- Before/after screenshots: `before_*.png` / `after_*.png` (edit-mode renders, no post-processing).

## Known visual and gameplay issues (not fixed in this handoff)

- **Chrome reads dark grey** (pendulum rods, pad neck rings, hubs): little for it to reflect. Needs your call (reflectivity or reflection probe).
- **Dark support columns** under pads differ from the reference's chrome pillars.
- Pendulum gantry is about 0.5 m wider across the post caps than the old one (decorative only).
- Rotating-beam base is 0.3 m wider than its hub on each side; feet may clip into it visually (no collider, 0.28 m high).
- Screenshots do not include the game camera's bloom/vignette; real in-game colour may differ slightly.
- **Existing, not from this work:** Section A gaps were sized for the old 3.2 m/s walk and need Sprint at the approved 1.5 m/s walk speed (from the Phase 3B notes). Known batch-test failures from earlier phases (Camera_CloseWall, Player_Jumps_AndLands, RagdollRespawn, Course 2 CollapsingBridge, UI screenshots) were not re-run here.
- **Not playtested by me:** feel, timing, camera and performance; only automated checks and edit-mode screenshots.
- No Course 2 work was performed.
