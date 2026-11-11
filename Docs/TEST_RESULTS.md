# Course 2 (Chaos Cove) — current verification record

Date: 2026-11-11. Branch: `claude/splashdown-course-2-asv7gw`. See [PHASE_3_COURSE2.md](PHASE_3_COURSE2.md#tests).

| Check | Actual result |
| --- | --- |
| Offline C# compilation against Unity 6000.6.6f1 | **PASS, 0 errors and 0 warnings**, all six project assembly variants |
| Engine-free NUnit tests | **150 passed, 0 failed, 0 skipped** (.NET SDK 8.0.318, NUnit 3.14): 122 existing + 28 new |
| Trial merge with `astra/phase-3b-integration` and `codex/terra-splashdown-ui-foundation` | **No conflicts**; merged tree compiles with 0 errors; 150 / 150 engine-free tests; `Tools/UiChecks/check_ui.py` passes |
| Unity editor: generate Course 2, Validate Project | **NOT RUN** (no licensed editor in the authoring environment) |
| Unity EditMode `Course2ContentTests` (3) and PlayMode `Course2PlayTests` (12) | **NOT RUN**; compiled only |
| Course 1 inside Unity after these changes | **NOT RUN**; engine-free Course 1 tests still pass |
| Interactive playtest, visuals, physics, audio, performance | **NOT PERFORMED** |

```sh
dotnet test Tools/CoreTests/CoreTests.csproj
python3 Tools/offline_compile_check.py --unity <Editor/Data> --inputsystem <package/InputSystem>
git diff --check
```

---

# Phase 3 — current verification record

See [PHASE_3.md](PHASE_3.md#tests). Summary: all six assemblies compile against Unity 6000.6.6f1 with
0 errors and 0 warnings; engine-free tests pass 122 / 122 (including 10 course-layout completability
tests). Nothing was run inside the Unity editor: the Course 1 generator, the 2 new EditMode tests and
the 9 new PlayMode tests are compiled only, and no visual, physics, audio or performance playtest exists.

# Phase 2A — current verification record

Date: 2026-11-10. Branch: `astra/phase-2a-ragdoll-camera`.

| Check | Actual result |
| --- | --- |
| Engine-free NUnit tests | **93 passed, 0 failed, 0 skipped**, .NET SDK 8.0.408 / NUnit 3.14 |
| Offline C# compilation against Unity 6000.6.6f1 | **PASS, 0 errors and 0 warnings**, all six project assembly variants |
| Unity batch startup | **FAILED, exit 1**; licensing IPC code `0x80000000`, “Licensing initialization failed” |
| Unity EditMode / generated-content tests | **NOT RUN**; added placeholder mass/joint coverage, compiled only |
| Unity PlayMode tests | **NOT RUN**; 17 tests compiled, including four new Phase 2A tests |
| Visual, physics, audio and local playtest | **NOT PERFORMED by this pass** |
| Whitespace patch check | `git diff --check` passed |

The user reports Foundation 1 was playtested locally. Historical authoring claims below refer to
Claude's earlier cloud pass, not the user's local session or this branch.

The editor archive was selectively extracted for managed references, packages, binary and shared
libraries. This is not a complete licensed editor installation. The batch probe also reported
missing compiler-prewarm files; its licensing client could not initialize. No scene/test run was
reached. This does not establish whether a fully provisioned licensed cloud editor would work.

Executed commands (tool paths depend on installation):

```sh
dotnet test Tools/CoreTests/CoreTests.csproj --logger 'trx;LogFileName=phase2a.trx'
python3 Tools/offline_compile_check.py --unity <Editor/Data> --inputsystem <package/InputSystem>
Unity -batchmode -nographics -quit -projectPath <Splashdown> -logFile <startup.log>
git diff --check
```

New engine-free regressions cover premature recovery rejection, continuous settling and timeout
preservation, resetting from settling/recovery, non-finite impacts and exact collision thresholds.
Unity regressions cover all-bone linear/angular settling, camera focus through blend/water delay,
close-wall camera collision, checkpoint orientation/control restoration from ragdoll, and placeholder
joint limits. Existing natural-recovery coverage now requires `Recovered`, so rescue cannot count
as successful recovery. Generated-content tests skip when scenes/prefabs have not been built.

See [PHASE_2A.md](PHASE_2A.md) for defects, settings, assets still missing and exact local instructions.

---

# Foundation 1 — Test Results

Date: 2026-11-10 · Target: Unity 6000.6.6f1 (revision d20c9ac777da)

## Summary

| Check | Result | How it was run |
| --- | --- | --- |
| Unity 6000.6.6f1 available | Not installed in the authoring environment | Linux editor build downloaded to scratch space for its assemblies only |
| C# compilation, all 6 assemblies | **PASS — 0 errors, 0 warnings** | Roslyn + engine assemblies shipped in 6000.6.6f1 (see below) |
| Core gameplay logic unit tests | **PASS — 101 / 101** | NUnit 3.14 under .NET 8, outside Unity |
| Project opens in the Unity editor | **NOT RUN** | Editor cannot start without a licence |
| Content generation (`Foundation1Builder`) | **NOT RUN** | Compiles; never executed |
| EditMode project / generated-content tests | **NOT RUN** | Written and compiled only |
| PlayMode end-to-end tests (14) | **NOT RUN** | Written and compiled only |
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
  Test Count: 101, Passed: 101, Failed: 0, Warnings: 0, Inconclusive: 0, Skipped: 0
```

| Suite | Covers |
| --- | --- |
| `CourseSessionTests` | Countdown ticks, timer start/overshoot, pause, finish, counters |
| `CheckpointTrackerTests` | Forward-only activation, bounds, reset |
| `KnockdownStateMachineTests` | Repeat-trigger protection, min/max ragdoll time, settle detection, recovery, immunity, deferral |
| `ImpactEvaluatorTests` | None / shove / knockdown thresholds, launch-speed floor and cap, force multiplier |
| `RespawnGuardTests` | Hazard cooldown, pending-respawn lockout, loop detection and checkpoint fallback |
| `PathTimelineTests` | Ping-pong and loop timelines, determinism, periodicity, easing |
| `ZoomControllerTests` | Wheel/held zoom, limits, smoothing without overshoot, scroll normalization (raw ±120 and normalized ±1), live re-clamping |
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
3. Test Runner > EditMode (expect the 101 core tests plus project/content checks to pass).
4. Test Runner > PlayMode — 14 scripted end-to-end tests: scene wiring, audio init, countdown/timer,
   pause, walk/run/camera, zoom (wheel, held, limits, ignored while paused), jump, moving-platform carry, knockdown → settle → recover (with a physics
   sanity check on every bone each frame), sweeper hit, water respawn, checkpoint respawn, finish
   screen, restart.
5. Play the course by hand. Things only a person can judge and that are most likely to need tuning:
   ragdoll joint limits and how the knockdown reads, impact force feel, jump clearance over the
   sweeper, ball timing, platform spacing, camera feel, audio levels.

## Known risks (unverified behaviour)

- **Gamepad and zoom (added after the first report):** no controller was ever connected and nothing
  was run. Mouse-wheel units differ by platform; `ZoomController.NormalizeScroll` accepts both raw
  (±120) and normalized (±1) values and is unit tested, but real wheel and touchpad feel is untested.
  Rumble, auto-pause on disconnect and the keyboard/gamepad hint switching depend on live
  device events and have only been compiled.

- **Content builder** (`Foundation1Builder`): ~1,800 lines of editor code that has never executed.
  If a step throws, the error names the step; fix and rerun "Build ... (Missing Only)".
- **Ragdoll joint axes/limits** follow Unity's ragdoll-wizard conventions but were not observed;
  a limb bending the wrong way would be a sign-flip in `Foundation1Builder.Prefabs.cs` (`Joint(...)`).
- **Trigger-based impact detection** against a `CharacterController` relies on standard PhysX
  trigger behaviour; if hits are missed, check the layer matrix and the `ImpactZone` colliders first.
- **Moving-platform carry** uses the interpolated platform transform; small vertical jitter on
  vertically moving platforms is possible (none are used in this course).
