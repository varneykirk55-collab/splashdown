# Splashdown Phase 3B — Local integration of Phase 3 (Course 1) with the cowgirl character

Date: 2026-11-11 · Unity 6000.6.6f1 URP · Branch `astra/phase-3b-integration`

## What was merged

- Base: `astra/phase-2c-jump-run` @ `2ec8f77` (cowgirl character, corrected locomotion, walk 1.5 m/s,
  jump-teleport fix, Fast Run sprint). That branch is left unchanged.
- Merged: `claude/bikini-wipeout-foundation-1-tcqjy6` @ `fc576df` (Phase 3 arena + Course 1, camera zoom,
  gamepad support). Merge was automatic: no conflicts. `CoursePlayTests.cs` auto-merged (zoom input
  fields plus the zoom test, next to the existing config-relative walk check).
- Backup taken before any change (not in git): `C:\Users\aresg\Backups\Splashdown-Astra-pre-phase3b-20261111`
  (full working tree including `.git` and all uncommitted/untracked files; `Library` excluded).

## Integration fix

`Foundation1Builder.ResolvePlayerPrefab` (Course 1 generator) took the player prefab from
`Course_Foundation1.unity`, which still holds the placeholder mannequin. It now checks
`Course_Foundation1_Cowgirl.unity` (`CowgirlIntegration.ScenePath`) first, then the Foundation scene,
then falls back to the placeholder. The generated `Course_SplashdownGauntlet.unity` contains
`Player_Cowgirl` (Cowgirl.controller, charA_cowgirlAvatar) and no placeholder.

No movement, animation, ragdoll, knockdown, camera, character prefab or config asset was edited.

## Generated content now committed

Previously, generated content (placeholder prefab, Foundation scenes, materials, settings assets,
audio `.meta` files) was untracked. So that this branch opens ready to play, it now commits the
generated Course 1 scene, `Prefabs/Course1`, arena materials, and the Foundation-generated assets they
reference. Committing the `.wav.meta` files keeps the audio GUIDs stable.

## Results (batch mode, this laptop)

| Check | Result |
| --- | --- |
| Compile (batch editor) | PASS, no compiler errors |
| Course 1 generation (`BuildCourse1Batch`) | PASS: "Course 1 is ready" |
| EditMode tests | **138 / 138 PASS** (includes `Course_IsCompletable_WithTheProjectsMovementConfig` on the real config) |
| PlayMode tests | **36 / 39 PASS**. 3 known batch-mode failures that predate Phase 3: `Camera_CloseWallDoesNotForceMinimumDistanceThroughWall` and `RagdollRespawn_RestoresCheckpointOrientationAndMouseLook` (WaitForEndOfFrame does not run in batch mode), `Player_Jumps_AndLands` (lands 0.15 m high) |
| Course1PlayTests (9) with the cowgirl | 9 / 9 PASS: sections wired, spawn and countdown, solid ground under every surface, obstacles move, every checkpoint respawns, pendulum knockdown and recovery, spin disc carries the player, finish plus results plus celebration, section announcement |
| Project validator | 78 passed, 1 warning (placeholder clips on the mannequin, expected), 0 errors |
| Rendered smoke run (temporary probe, not committed) | Cowgirl spawns on the start pad, countdown then running, walks at 1.5 m/s with animation, all 5 sections render |

Performance probe (one camera render per frame, 1280×720, batch editor, CPU wall time): average 8.9 ms,
worst 38.5 ms (a single first-frame spike) over 229 frames. 594 renderers (496 shadow casters),
35 rigidbodies, 142 colliders, 1 realtime light, 14 audio sources. Managed heap about 400–440 MB.
No changes were needed. If frame rate drops on the user's display, lower the URP shadow distance first.

## Known issues / for the playtest

- **Section A and the new walk speed.** Phase 3 sized Section A gaps to be clearable with a *walking*
  jump at the old 3.2 m/s walk (2.03 m reach). With the approved 1.5 m/s walk, a walking jump reaches
  about 0.95 m, so Section A gaps need **Sprint** held. The whole course is still completable with the
  running jump; the config-relative test confirms this. Changing it means retuning `Course1Layout` or
  the walk speed, so that decision is left to the user.
- Phase 3 risks remain untested by a human: piston push feel, spin-disc edges, giant-ball difficulty,
  0.5 m safe rims on sweeper islands, signage size.
- The cowgirl still lacks Landing (stand-in) and Swimming clips (from Phase 2B).

## Launch

1. Open `C:\Users\aresg\OneDrive\Documents\Splashdown-Astra` in Unity 6000.6.6f1 (branch `astra/phase-3b-integration`).
2. Open `Assets/BikiniWipeout/Scenes/Course_SplashdownGauntlet.unity` and press Play (or start from
   `MainMenu.unity`; PLAY loads Course 1).
3. Controls: WASD/arrows or left stick to move, Left Shift or L3 to sprint, Space or A/Cross to jump, mouse wheel, +/- or d-pad up/down to zoom.
