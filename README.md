# Splashdown — Foundation 1 + Phase 2A

Phase 2A refines ragdoll recovery and camera tracking on a separate branch. See
[the Phase 2A handoff](Docs/PHASE_2A.md) and [current test results](Docs/TEST_RESULTS.md).
On an existing generated project, run **Bikini Wipeout > Apply Phase 2A Placeholder Physics**
to update only the placeholder prefab; no course rebuild is required.


Third-person, physics-based obstacle-course game-show prototype for Windows PC.
Unity **6000.6.6f1**, Universal Render Pipeline, Input System, C#.

Foundation 1 is the first playable slice: run a short course, get knocked down by obstacles,
ragdoll, recover, fall in the water, respawn at checkpoints, and finish with a recorded time.

> **Historical Foundation 1 authoring status.** This project was authored in a cloud environment
> with no Unity licence, so the editor was never launched. Every script was compiled against the
> real Unity 6000.6.6f1 assemblies (0 errors, 0 warnings) and the engine-free gameplay logic
> passes its unit tests, but **the game has not been run or playtested**. See
> [Docs/TEST_RESULTS.md](Docs/TEST_RESULTS.md) for exactly what was and was not verified.

## Open and play

1. Install **Unity 6000.6.6f1** (Unity Hub > Installs > Install Editor > Archive) with Windows build support.
2. In Unity Hub: **Add > Add project from disk** and select this folder (the one containing `Assets/`).
3. Open it. On the first open Unity imports packages and compiles; then the project **generates its
   prefabs and scenes automatically** (watch the Console for
   `[Bikini Wipeout] Foundation 1 content is ready`) and opens the course scene.
   - If that does not happen, run **Bikini Wipeout > Build Foundation 1 Content (Missing Only)** from the menu bar.
4. Press **Play**. A 3-2-1 countdown runs, then you have control.
5. To start from the menu instead, open `Assets/BikiniWipeout/Scenes/MainMenu.unity` and press Play.

Why generated? The repository stores the game as code (`Foundation1Builder`), so all content is
reproducible. Once generated, the scenes and prefabs are ordinary Unity assets — edit them freely
and commit them. "Missing Only" never overwrites your edits; "Rebuild (Overwrite Generated)" does.

### Controls

| Action | Keyboard / mouse | Gamepad |
| --- | --- | --- |
| Move | W A S D / arrow keys | Left stick |
| Look | Mouse | Right stick |
| Zoom in / out | Mouse wheel, or `=` / `-` (also numpad + / -) | D-pad up / down |
| Run (hold) | Left Shift | Left stick press or left trigger |
| Jump | Space | South button (A / Cross) |
| Pause / resume | Esc or P | Start |
| Menus | Mouse | D-pad / left stick to move, South button to select |

Controller notes: any gamepad the Input System supports works (Xbox, PlayStation, Switch Pro, generic).
The on-screen control legend switches between keyboard and gamepad wording depending on which you
touched last; the controller rumbles on wipeouts, shoves and hard landings (only while a gamepad is
the active device; switch it off with `Enable Rumble` on the player's `Gamepad Feedback`); and
the game pauses if the controller you are playing with is unplugged. Right-stick look uses a response
curve for fine aiming (`Gamepad Look Exponent` in `CameraConfig`).

Zoom range, wheel step, held-zoom speed and smoothing are in `Settings/CameraConfig.asset`
(`Zoom Min/Max Distance`, `Scroll Zoom Step`, `Held Zoom Speed`, `Zoom Smooth Time`). The zoom
you choose is kept across respawns.

Walking is the default; hold Run to run. To invert that, tick **Run By Default** on
`Assets/BikiniWipeout/Settings/PlayerMovementConfig.asset`.

### The course

Start platform → **rotating sweeper** (jump the arm) → checkpoint → **bouncing balls** (time your run)
→ checkpoint → **moving platforms** (jump between them) → finish line. Water lies under everything;
falling in returns you to the last checkpoint.

## Project layout

```
Assets/
  BikiniWipeout/
    Scripts/
      Core/      Engine-free gameplay rules (no UnityEngine reference) - fully unit tested
      Runtime/   MonoBehaviours: Character, Ragdoll, Impact, Obstacles, Water, Course, Camera, Audio, UI, Input
      Editor/    Foundation1Builder (content generator), ProjectValidator, first-open setup
    Tests/
      EditMode/  Core logic tests + project/generated-content checks
      PlayMode/  End-to-end tests that drive the generated course with scripted input
    Audio/Placeholder/   Synthesized temporary SFX and music (see Tools/generate_placeholder_audio.py)
    Settings/            Input actions; generated: config assets + AudioLibrary
    Scenes/ Prefabs/ Materials/ Animations/   Generated on first open
  Settings/      URP assets (from Unity's 6000.6 URP template)
Docs/            ASTRA_INTEGRATION.md, TEST_RESULTS.md
Tools/           Placeholder audio generator, offline compile check
```

## Validation and tests

In the editor:

- **Bikini Wipeout > Validate Project** — checks render pipeline, layers, collision matrix, input,
  configs, audio coverage, prefab wiring and build settings. Results go to the Console.
- **Window > General > Test Runner** — run the **EditMode** and **PlayMode** suites.
  Generate the content first; tests that need it are skipped (not failed) until it exists.

From the command line (Windows; adjust the Unity path):

```bat
set UNITY="C:\Program Files\Unity\Hub\Editor\6000.6.6f1\Editor\Unity.exe"

:: 1. Generate content (exit code 0 = success)
%UNITY% -batchmode -projectPath . -executeMethod BikiniWipeout.EditorTools.Foundation1Builder.BuildAllBatch -logFile Logs\build.log

:: 2. Validate (exit code 0 = no errors)
%UNITY% -batchmode -projectPath . -executeMethod BikiniWipeout.EditorTools.ProjectValidator.ValidateBatch -logFile Logs\validate.log

:: 3. Tests
%UNITY% -batchmode -projectPath . -runTests -testPlatform EditMode -testResults Logs\editmode.xml -logFile Logs\editmode.log
%UNITY% -batchmode -projectPath . -runTests -testPlatform PlayMode -testResults Logs\playmode.xml -logFile Logs\playmode.log
```

`BuildAllBatch` regenerates prefabs and scenes (it overwrites generated ones) — use the menu's
"Missing Only" variant if you have hand-edited them.

## Notes

- **Audio** is placeholder: every clip in `Audio/Placeholder` is synthesized by
  `Tools/generate_placeholder_audio.py` (original, no third-party licence). Swap clips in
  `Settings/AudioLibrary.asset`; no code changes needed.
- **Character** is a primitive mannequin with an 11-body ragdoll. The Animator has all eight states
  with **empty placeholder clips**; limb motion is procedural. See the Astra guide for the swap path.
- Project settings come from Unity's official URP template for 6000.6, with gameplay physics layers added.
- Documentation for the physics/animation refinement pass: [Docs/ASTRA_INTEGRATION.md](Docs/ASTRA_INTEGRATION.md).
