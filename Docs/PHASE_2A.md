# Splashdown Phase 2A — ragdoll, camera and recovery

Branch: `astra/phase-2a-ragdoll-camera`
Base: `85c5c3655f62dc1dbb84c52da8ced610e518590a` (`claude/bikini-wipeout-foundation-1-tcqjy6`)
Date: 2026-11-10. No merge, course expansion or environment redesign.

## DONE

Inspected README, ASTRA_INTEGRATION, TEST_RESULTS, controller, animation, physics,
obstacle impacts, checkpoint/respawn and camera code before editing.

Concrete defects and changes:

- Camera collision distance was clamped **up** to 1.2 m even if an obstruction was closer.
  Collision now wins over the preferred minimum. A disabled query sphere resolves initial
  overlap at the pivot and camera endpoint; it never participates in simulation.
- Camera read the visible pose before the ragdoll's late pose blend and switched to the
  upright root when recovery began. It now runs after that blend, follows the visible hips
  throughout blending, retains wipeout framing during recovery and caps follow lag at 0.8 m.
- Water entry reset ragdoll bones under the unmoved character root, taking camera focus back
  to the takeoff position during the respawn delay. Focus is captured before resetting bones
  and held until respawn completes. Respawn still intentionally snaps behind the checkpoint.
- Settling considered only hip linear speed, ignoring spinning or moving limbs. Every bone's
  linear and angular velocity must now be below thresholds, with nearby safe ground.
- Recovery could be started before the timing rules allowed it. `BeginRecovery` now requires
  `WantsRecovery`. Explicit phases are Normal → Knockdown → Ragdoll → Settling → Recovering → Normal.
  Knockdown is an atomic notification during the physics handoff. Renewed motion returns
  Settling to Ragdoll without restarting the overall timeout. Timeout remains an escape from
  never-settling physics, but never bypasses runtime ground/clearance checks.
- Stand-point search could choose ground above the hips and under-tested capsule clearance.
  It now rejects elevated surfaces and tests the full capsule plus skin at its actual lifted
  stand pose. Nearby support is required before recovery, including at timeout. Existing bounded
  retries and checkpoint rescue remain. Some safe lateral repositioning (up to 1.6 m) is retained.
- Falling-ball normals were flattened and converted into an upward launch. Knockdowns now
  retain their vertical impact direction; artificial lift scales with the horizontal component.
  Whole-body velocity transfer remains, with bounded extra force applied near the contacted bone.
- Simulated velocities are cleared before switching bones to kinematic; current transforms are
  synchronized before activation. Recovery control lock lasts at least as long as pose blending.
- Placeholder knee limits allowed 80° but its crouch used 120°; hip/shoulder limits also excluded
  extremes of procedural poses. New generation and the targeted upgrade set knees to -130…0°,
  hip high twist to 90°, shoulder high twist to 130° (existing local-X joint convention).
- Placeholder total mass remains 61 kg: hips 16, chest 18, head 5, upper arms 2.5 each,
  forearms 1.5 each, thighs 5 each, shins 2 each. This shifts mass from distal legs into the torso;
  these are starting values requiring a physics playtest, not measured improvements.
- Procedural walking stride fades with speed instead of immediately using full walk amplitude;
  walk/run state selection has hysteresis. Landing events during knockdown/recovery no longer
  queue a spurious landing pose. Existing jumping/falling poses and movement controller remain.
- Non-finite impact inputs are rejected. Empty or suspended ragdoll setups cannot start knockdown.

Existing audio hooks, input, course, checkpoint progression and respawn-loop guards are preserved.
No active-ragdoll system was added.

## Animation and character assets

There are still **no genuine animation clips**. Recovery remains a timed ragdoll-to-procedural
pose blend, followed by a procedural rise. This is a temporary solution, not a real get-up animation.
`RecoveryFaceUp` is available on KnockdownController and written to an Animator bool of the same
name if present. `RecoveryStarted` now fires after the Animator is re-enabled. When clips arrive,
use this bool to select face-up/face-down clips and replace the timed completion appropriately.
The test assumes the existing hips +Z points out of the front of the body; validate that convention
on the approved AccuRig character. Follow the import/swap steps in ASTRA_INTEGRATION section 2.4.
Do not run the placeholder migration on a replacement character.

## TESTED / UNVERIFIED

See TEST_RESULTS.md for the exact execution record. NUnit tests run outside Unity establish rule
behavior only. Offline compilation establishes API compatibility only. Neither is a physics or
visual playtest. Added PlayMode tests need the generated course and a licensed Unity editor.
The natural recovery test now requires a `Recovered` event so checkpoint rescue cannot mask failure.

## NEXT — exact local playtest

Close Unity, then in the existing Splashdown repository:

```powershell
git fetch origin
git switch --track origin/astra/phase-2a-ragdoll-camera
```

If the local branch already exists, use `git switch astra/phase-2a-ragdoll-camera` then
`git pull --ff-only`. Keep local edits; do not use a hard reset. A downloaded ZIP is not a git
checkout: instead download this branch's ZIP into a separate folder and open that folder.

1. Open the project in Unity **6000.6.6f1**, URP. Wait for compilation.
2. Existing generated project: run **Bikini Wipeout → Apply Phase 2A Placeholder Physics** once,
   outside Play mode. This updates only the placeholder prefab's masses/joint limits and keeps
   the course and existing config values. Scene instances inherit prefab changes unless overridden.
   For a fresh checkout, first-open generation already applies these settings. If needed use
   **Build Foundation 1 Content (Missing Only)**. Do not use the overwrite/rebuild command.
3. Run **Bikini Wipeout → Validate Project**, then **Window → General → Test Runner**:
   run EditMode and PlayMode. Treat ignored tests as not verified.
4. Open `Assets/BikiniWipeout/Scenes/Course_Foundation1.unity`, press Play, wait for countdown.
   Walk, hold Shift to run, stop, jump, and walk off an edge. Confirm smooth state changes and mouse look.
5. Take sweeper hits while standing, running and jumping. Let the bouncing balls strike from
   the side and above. Watch the limbs and pelvis react, with no persistent stretched joints.
6. On a platform, let the body settle and recover. Hold movement throughout: movement must remain
   locked until recovery ends. Repeat impacts during the ragdoll and immunity window; verify no loop.
7. Orbit the mouse near the sweeper hub, platform edges and ball obstacles. Verify no clipping,
   abrupt camera jump at recovery, or loss of the contestant during a fall. Extremely tight geometry
   may require collision-radius/offset tuning; arbitrary imported concave meshes are unverified.
8. Fall into water while upright and ragdolled. During the hidden delay the view must stay at the
   fall site, then return behind the checkpoint. Repeat after each checkpoint; verify facing, mouse
   control, movement and no immediate respawn loop.
9. Complete and restart the course. Check Console for physics/Animator warnings and exceptions.

## Settings to inspect (preserved assets are not overwritten)

| Asset | Field | Starting value / behavior |
| --- | --- | --- |
| KnockdownConfig | settleSpeed | 0.7 m/s, now applies to all bones |
| KnockdownConfig | settleAngularSpeed | 1.5 rad/s, new |
| KnockdownConfig | recoverySupportDistance | 0.8 m below hips, new; adjust for replacement rig |
| KnockdownConfig | timings | Existing values retained; defaults 1.1 s min, 4.5 s max, 0.35 s settle, 0.9 s recovery, 1 s immunity |
| KnockdownConfig | safeSearchRadius | Existing 1.6 m default; smaller reduces lateral repositioning but can increase rescue use |
| CameraConfig | maxFocusLag | 0.8 m, new |
| CameraConfig | minDistance | Preferred distance only; close obstruction can pull camera closer |
| RagdollConfig | poseBlendTime | Existing 0.35 s default; recovery lock extends if increased past recovery duration |

A downloaded repository does not include generated scenes/prefabs; settings on a previously
playtested local copy may differ. Check new fields in the Inspector after import. No global
replacement-character mass/joint assumptions are imposed by runtime code.

## Changed files

```text
.gitignore
Assets/BikiniWipeout/Scripts/Core/ImpactEvaluator.cs
Assets/BikiniWipeout/Scripts/Core/KnockdownStateMachine.cs
Assets/BikiniWipeout/Scripts/Editor/Foundation1Builder.Prefabs.cs
Assets/BikiniWipeout/Scripts/Runtime/Camera/ThirdPersonCamera.cs
Assets/BikiniWipeout/Scripts/Runtime/Character/AnimationStateController.cs
Assets/BikiniWipeout/Scripts/Runtime/Character/KnockdownController.cs
Assets/BikiniWipeout/Scripts/Runtime/Character/PlaceholderProceduralAnimator.cs
Assets/BikiniWipeout/Scripts/Runtime/Character/PlayerCharacter.cs
Assets/BikiniWipeout/Scripts/Runtime/Character/PlayerRespawner.cs
Assets/BikiniWipeout/Scripts/Runtime/Config/CameraConfig.cs
Assets/BikiniWipeout/Scripts/Runtime/Config/KnockdownConfig.cs
Assets/BikiniWipeout/Scripts/Runtime/Ragdoll/RagdollController.cs
Assets/BikiniWipeout/Tests/EditMode/Core/ImpactEvaluatorTests.cs
Assets/BikiniWipeout/Tests/EditMode/Core/KnockdownStateMachineTests.cs
Assets/BikiniWipeout/Tests/EditMode/Project/GeneratedContentTests.cs
Assets/BikiniWipeout/Tests/PlayMode/CoursePlayTests.cs
Docs/ASTRA_INTEGRATION.md
Docs/PHASE_2A.md
Docs/TEST_RESULTS.md
README.md
Tools/CoreTests/CoreTests.csproj
```
