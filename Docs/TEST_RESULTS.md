# Foundation 1 — Test Results

Date: 2026-11-10 · Target: Unity 6000.6.6f1 (revision d20c9ac777da)

## Summary

| Check | Result | How it was run |
| --- | --- | --- |
| Unity 6000.6.6f1 available | Not installed in the authoring environment | Linux editor build downloaded to scratch space for its assemblies only |
| C# compilation, all 6 assemblies | **PASS — 0 errors, 0 warnings** | Roslyn + engine assemblies shipped in 6000.6.6f1 (see below) |
| Core gameplay logic unit tests | **PASS — 82 / 82** | NUnit 3.14 under .NET 8, outside Unity |
| Project opens in the Unity editor | **NOT RUN** | Editor cannot start without a licence |
| Content generation (`Foundation1Builder`) | **NOT RUN** | Compiles; never executed |
| EditMode project / generated-content tests | **NOT RUN** | Written and compiled only |
| PlayMode end-to-end tests (13) | **NOT RUN** | Written and compiled only |
| Visual, physics and audio playtesting | **NOT DONE** | The game has never been executed or observed |

Nothing in the bottom half of this table should be read as passing. The first time the project is
opened in Unity is the first time the builder, the scenes, the physics and the audio actually run.

## What was verified

### 1. Compilation against the real engine

The authoring environment had no Unity install and no licence, and launching the downloaded editor
binary was not permitted, so Unity's own compile pipeline could not be used. Instead:

- The Linux build of Unity 6000.6.6f1 was downloaded and unpacked for its managed assemblies,
  bundled .NET SDK and built-in package sources.
- Reference assemblies for `UnityEngine.UI`, `UnityEditor.UI`, `Unity.InputSystem` (1.20.0) and
  `UnityEngine.TestRunner` were compiled from the package sources.
- Every project assembly was compiled with C# 9 against those and all `UnityEngine.*` /
  `UnityEditor.*` module assemblies:

```
== BikiniWipeout.Core:                     errors=0 warnings=0
== BikiniWipeout.Runtime:                  errors=0 warnings=0
== BikiniWipeout.Runtime (player defines): errors=0 warnings=0   (no UNITY_EDITOR, no UnityEditor references)
== BikiniWipeout.Editor:                   errors=0 warnings=0
== BikiniWipeout.Tests.EditMode:           errors=0 warnings=0
== BikiniWipeout.Tests.PlayMode:           errors=0 warnings=0
PROJECT COMPILE: OK
```

Reproduce with `Tools/offline_compile_check.py`. Obsolete-API warnings were left enabled; the one
this surfaced (`FindObjectsByType(FindObjectsSortMode)`, deprecated in 6000.6) was fixed.

Limits of this check: it proves the code compiles against this Unity version's API. It does not
prove Unity's own asmdef resolution, serialization, or any runtime behaviour.

### 2. Core logic unit tests (executed)

`Assets/BikiniWipeout/Scripts/Core` has no engine dependency, so its tests
(`Tests/EditMode/Core`) were compiled and run for real:

```
Test Run Summary
  Overall result: Passed
  Test Count: 82, Passed: 82, Failed: 0, Warnings: 0, Inconclusive: 0, Skipped: 0
```

| Suite | Covers |
| --- | --- |
| `CourseSessionTests` | Countdown ticks, timer start/overshoot, pause, finish, counters |
| `CheckpointTrackerTests` | Forward-only activation, bounds, reset |
| `KnockdownStateMachineTests` | Repeat-trigger protection, min/max ragdoll time, settle detection, recovery, immunity, deferral |
| `ImpactEvaluatorTests` | None / shove / knockdown thresholds, launch-speed floor and cap, force multiplier |
| `RespawnGuardTests` | Hazard cooldown, pending-respawn lockout, loop detection and checkpoint fallback |
| `PathTimelineTests` | Ping-pong and loop timelines, determinism, periodicity, easing |
| `VolumeSettingsTests`, `TimeFormatterTests`, `BestTimeRecordTests`, `MovementMathTests` | Clamping, persistence, formatting, records, helpers |

The same files run inside Unity as EditMode tests.

### 3. Static consistency checks

- `ProjectSettings/TagManager.asset` layer names and the `DynamicsManager.asset` collision matrix
  were generated from the same rule table as `GameLayers.cs` (and are re-applied at runtime).
- All asset GUIDs in the repository are unique; every `.cs` / `.asmdef` / folder has a `.meta`.
- Project settings and URP assets are the unmodified files from Unity's
  `com.unity.template.urp-blank` 17.2.6 (the 6000.6 template), with these edits: product/company
  name, 1920x1080 resizable window, run in background, the Mobile quality tier removed (PC only),
  URP asset set as the default pipeline, gameplay layers + collision matrix, solver iterations
  10 / 2, build-settings scene list emptied (the builder fills it), project-wide input actions
  pointed at `BikiniWipeoutControls`, and Enter Play Mode set to a full domain reload.
- Package versions in `Packages/manifest.json` match the versions built into / shipped for 6000.6.6f1
  (URP 17.6.0, uGUI 2.6.0, Test Framework 1.8.0, Input System 1.20.0).

## What still has to be run in Unity

In this order, on a machine with Unity 6000.6.6f1:

1. Open the project; confirm the Console shows `Foundation 1 content is ready` with no errors.
2. **Bikini Wipeout > Validate Project** — expect 0 errors.
3. Test Runner > EditMode (expect the 82 core tests plus project/content checks to pass).
4. Test Runner > PlayMode — 13 scripted end-to-end tests: scene wiring, audio init, countdown/timer,
   pause, walk/run/camera, jump, moving-platform carry, knockdown → settle → recover (with a physics
   sanity check on every bone each frame), sweeper hit, water respawn, checkpoint respawn, finish
   screen, restart.
5. Play the course by hand. Things only a person can judge and that are most likely to need tuning:
   ragdoll joint limits and how the knockdown reads, impact force feel, jump clearance over the
   sweeper, ball timing, platform spacing, camera feel, audio levels.

## Known risks (unverified behaviour)

- **Content builder** (`Foundation1Builder`): ~1,800 lines of editor code that has never executed.
  If a step throws, the error names the step; fix and rerun "Build ... (Missing Only)".
- **Ragdoll joint axes/limits** follow Unity's ragdoll-wizard conventions but were not observed;
  a limb bending the wrong way would be a sign-flip in `Foundation1Builder.Prefabs.cs` (`Joint(...)`).
- **Trigger-based impact detection** against a `CharacterController` relies on standard PhysX
  trigger behaviour; if hits are missed, check the layer matrix and the `ImpactZone` colliders first.
- **Moving-platform carry** uses the interpolated platform transform; small vertical jitter on
  vertically moving platforms is possible (none are used in this course).
