> **Phase 2A update:** See [PHASE_2A.md](PHASE_2A.md) for the implemented changes and local
> upgrade/playtest procedure. The architecture map below describes the original foundation;
> Phase 2A adds explicit Settling, whole-body stability, safe recovery and continuous camera focus.

# Astra Integration Guide — Physics and Animation Refinement

Foundation 1 delivers working, deliberately simple versions of movement, knockdown, ragdoll,
animation state and camera. This document maps where each one lives, what it depends on, which
values are exposed for tuning, and where to extend it.

**Status caveat:** the systems below compile against Unity 6000.6.6f1 and their rule logic is unit
tested, but none of it has been run in the editor yet (see `TEST_RESULTS.md`). Treat all tuning
values as untested starting points.

## 1. Architecture at a glance

```
ImpactSource (any obstacle)                    PlayerInputReader : ICharacterInput
      │  ImpactData (point, normal, surface velocity, force multiplier)   │
      ▼                                                                    ▼
KnockdownController : IImpactReceiver ───────────────► CharacterMotor (CharacterController)
      │   uses Core.ImpactEvaluator + Core.KnockdownStateMachine      ▲        │
      │   suspends / teleports / locks the motor ─────────────────────┘        │ Velocity, IsGrounded,
      ▼                                                                         │ Jumped, Landed
RagdollController (Rigidbodies + CharacterJoints)                               ▼
      │  Activated / Deactivated / BoneCollided                    AnimationStateController ──► Animator params
      ▼                                                                         │
CharacterAudio ──► AudioManager                                                 ▼
                                                              PlaceholderProceduralAnimator (temporary)

PlayerCharacter (facade) ◄── ThirdPersonCamera, CourseManager, PlayerRespawner, UI
```

Rules that keep it modular:

- **Obstacles never reference the ragdoll.** They carry an `ImpactSource`, which reports a measured
  impact to whatever `IImpactReceiver` it touched. (`GeneratedContentTests.ObstaclePrefabs_DoNotReferenceTheRagdoll`.)
- **`RagdollController` knows nothing about movement or obstacles.** It switches bones between
  kinematic and simulated, launches them, and blends back.
- **`CharacterMotor` knows nothing about ragdolls, obstacles or the model.**
- **`KnockdownController` is the only class that links motor and ragdoll.**
- Rule/timing logic is engine-free in `Scripts/Core` and unit tested; MonoBehaviours apply it to the scene.

Assemblies: `BikiniWipeout.Core` (no engine refs) ← `BikiniWipeout.Runtime` ← `BikiniWipeout.Editor`.
Serialized fields are `internal` + `[SerializeField]`; the Editor and test assemblies see them via `InternalsVisibleTo`.

## 2. The systems you will modify

### 2.1 Player movement — `Runtime/Character/CharacterMotor.cs`

- `CharacterController`-based. Camera-relative walk/run, acceleration/deceleration, turn rate, jump
  with coyote time + input buffering + variable height, air control, gravity, terminal velocity.
- Follows whatever it stands on (position **and** yaw) by tracking its local point on the ground
  transform each frame, and inherits platform momentum when leaving it.
- Runs in `Update`. Input comes through `ICharacterInput` (`motor.InputSource`), so AI or tests can drive it.
- **Tuning:** `Settings/PlayerMovementConfig.asset` (speeds, accelerations, turn speeds, jump height,
  gravity multipliers, coyote/buffer times, ground probe, external-push damping). Read live every frame.
- **API for other systems:** `SetControlLock(reason, bool)` (steering off, physics on),
  `SetSuspended(bool)` (motor + controller off while the ragdoll owns the body), `Teleport`,
  `AddExternalVelocity` (shoves), `Velocity`, `IsGrounded`, `PlanarSpeed`, `VerticalSpeed`, events `Jumped`, `Landed`.
- **Extension points:** replace the body of `Update` with a different locomotion model while keeping
  the public API above and nothing else changes. A Rigidbody-based motor would need to keep
  `SetSuspended`/`Teleport` semantics for `KnockdownController` and `PlayerRespawner`.

### 2.2 Knockdown and recovery — `Runtime/Character/KnockdownController.cs`

Flow: `ReceiveImpact` → closing speed along the impact normal → `ImpactEvaluator` → **None / Shove / Knockdown**.

- Knockdown: `KnockdownStateMachine.TryKnockdown()` (rejects while down or immune) → lock + suspend
  motor → `ragdoll.Activate(inheritedVelocity)` → `ragdoll.ApplyLaunch(launch, point)`.
- Each frame the state machine is ticked with "is the hips slower than `settleSpeed`". When it
  `WantsRecovery`, `TryRecover` looks for a **safe stand point** (ground raycast on `SafeGroundMask`,
  slope check, `CheckCapsule` clearance on `SolidMask`, then two rings of 8 offsets). Found → teleport
  motor there, `ragdoll.Deactivate(blend: true)`, Recovering phase, then control + immunity. Not found
  → retry after `recoveryRetryDelay`; after `maxRecoveryAttempts` it raises `RescueRequested` and the
  respawner returns the character to the last checkpoint.
- **Tuning:** `Settings/KnockdownConfig.asset` — `impact` thresholds (min shove speed, knockdown speed,
  velocity transfer, min/max launch speed), `upwardLaunchRatio`, `shoveScale`, `timings` (min/max
  ragdoll time, settle time, recovery duration, post-recovery immunity), `settleSpeed`,
  `respawnImmunity`, safe-reposition distances and retry limits.
- **Events:** `KnockedDown(ImpactData)`, `Shoved`, `RecoveryStarted`, `Recovered`, `RescueRequested`.
  `ForceKnockdown(launchVelocity, point)` triggers one from script.
- **Extension points:**
  - *Active ragdoll / get-up animations:* the Recovering phase is currently a timed pose blend.
    Replace what happens between `RecoveryStarted` and `Recovered` (choose face-up/face-down get-up
    from `ComputeGetUpFacing`'s hips orientation test, play it, end the phase from an animation event
    instead of `RecoveryDuration`).
  - *Partial reactions (stumbles):* add an `ImpactOutcome` between Shove and Knockdown in
    `Core/ImpactEvaluator.cs` and handle it in `ReceiveImpact`.
  - *Directional impulses per body part:* `ImpactData.Point` is already passed through to `ApplyLaunch`.

### 2.3 Ragdoll — `Runtime/Ragdoll/RagdollController.cs`

- Discovers bones as **every Rigidbody under `ragdollRoot`** (and their colliders). Nothing is
  hard-coded to the placeholder, so a rig set up with Unity's Ragdoll Wizard works as-is.
- Inactive: bodies kinematic, colliders off, discrete, no interpolation. Active: dynamic, config
  collision mode, interpolated, Animator and listed behaviours disabled.
- `ApplyLaunch` gives **every bone the same velocity change** (joints stay relaxed), a small extra
  to the nearest bone, and a tumble torque on the hips.
- `Deactivate(blend, relocate)` captures the world pose, lets the caller move the character root,
  restores the pose, then blends to the animated pose: `Update` resets bones to the rest pose (before
  the Animator runs), `LateUpdate` (execution order 100) lerps from the captured pose to whatever
  animation wrote this frame.
- **Tuning:** `Settings/RagdollConfig.asset` — damping, max linear/angular velocity,
  `maxDepenetrationVelocity`, solver iterations, collision detection mode, interpolation,
  `hitBoneExtraSpeed`, `tumbleSpin`, `poseBlendTime`, collision-sound thresholds.
- **Deliberately conservative choices to revisit:** ragdoll self-collision is **off** (layer matrix),
  joint projection on, preprocessing off, launch speed capped, depenetration velocity capped.
- Joint setup lives in `Editor/Foundation1Builder.Prefabs.cs` (`Joint(...)` calls): all joints use
  local X as the twist axis and Z as the swing axis; limits follow the Ragdoll Wizard convention.
  These were not observed in motion — check knees and elbows first.

### 2.4 Animation state — `Runtime/Character/AnimationStateController.cs`

- Computes a logical `CharacterAnimState` (Idle, Walk, Run, Jump, Fall, Land, Knockdown, Recover)
  from the motor and knockdown phase, raises `StateChanged`, and writes Animator parameters:
  `Speed`, `MotionBlend` (0 idle / 0.5 walk / 1 run), `VerticalSpeed`, `Grounded`, `KnockedDown`,
  `Recovering`, triggers `Jump` and `Land`. It only writes parameters that exist on the controller.
- `Animations/PlaceholderCharacter.controller` (generated) has all eight states with transitions and
  an **empty placeholder clip per state** (`Animations/Placeholder/Placeholder_<State>.anim`).
  There is no real animation coverage yet.
- `PlaceholderProceduralAnimator` swings the mannequin's limbs in code so states are readable. It is
  temporary and self-contained.

**Bringing in the real character (Hunyuan 3D model, AccuRig, humanoid FBX clips):**

1. Import the rigged FBX as **Humanoid**; import clips as Humanoid too.
2. In `Prefabs/Characters/Player_Placeholder.prefab` (or a variant), replace the children of `Model`
   with the rigged character. Put its `Animator` on `Model` (or re-point the references below) and
   assign the Avatar.
3. Run **GameObject > 3D Object > Ragdoll…** on the new skeleton. Put the bone objects on the
   `Ragdoll` layer. Set `RagdollController.ragdollRoot` to `Model` and `rootBody` to the pelvis.
4. Assign clips to the eight states (or create an Animator Override Controller over the placeholder
   clips). `MotionBlend` is ready to drive a 1D locomotion blend tree if you collapse Idle/Walk/Run.
5. Delete `PlaceholderProceduralAnimator` and remove it from `RagdollController.disableWhileSimulated`.
6. For footsteps from animation events, call `CharacterAudio.PlayFootstep` and untick `distanceFootsteps`.
7. Adjust the `CharacterController` height/radius and `PlayerCharacter.focusHeight` to the new proportions.

No gameplay script needs to change for this swap.

### 2.5 Obstacle impact forces — `Runtime/Impact/ImpactSource.cs`

- One component for every obstacle. On trigger or collision contact it finds the closest point on
  its solid colliders to the receiver, measures its **own surface velocity at that point**
  (physics velocity for dynamic bodies; pose delta per fixed step for kinematic or static ones), and
  sends `ImpactData`.
- **Tuning per obstacle:** `forceMultiplier` (exposed as `impactForce` on `RotatingSweeper` and
  `BouncingBall`), `rehitInterval`, `kind`. Global response curve: `KnockdownConfig.impact`.
- New obstacle = any moving collider + `ImpactSource`. Optionally implement `IResettable`.

Obstacle components: `RotatingSweeper` (speed, axis, start angle, impact force, Reset/Restart/Stop),
`BouncingBall` (bounce height, bounciness, patrol offset/speed, start phase, impact force, auto-reset),
`MovingPlatform` (waypoints, speed, dwell, loop mode, start offset, easing; position is a pure
function of time via `Core.PathTimeline`).

### 2.6 Camera during wipeouts — `Runtime/Camera/ThirdPersonCamera.cs`

- Orbit camera following `PlayerCharacter.FocusPosition` (chest height when standing, **hips while
  ragdolled**), sphere-cast collision, snap-behind on respawn.
- While `Knockdown.IsRagdolled` it blends to a wider, lazier framing.
- **Tuning:** `Settings/CameraConfig.asset` — distance, pivot offset, FOV, sensitivities, pitch limits,
  follow smoothing, collision radius, and the wipeout block (`wipeoutDistanceMultiplier`,
  `wipeoutFovBoost`, `wipeoutFollowSmoothTime`, `wipeoutBlendSpeed`).
- **Extension points:** impact shake / slow-motion can subscribe to `KnockdownController.KnockedDown`;
  a Cinemachine rig can replace this class if it keeps `LookEnabled` and `SnapBehindTarget` (used by
  `CourseManager` and the respawner).

## 3. Physics layers and collision rules

Defined once in `Runtime/Physics/GameLayers.cs`, mirrored in `ProjectSettings`, re-applied at startup.

| Layer | # | Collides with |
| --- | --- | --- |
| Player | 6 | Ground, Obstacle, MovingPlatform, Trigger, Boundary |
| Ragdoll | 7 | Ground, Obstacle, MovingPlatform, Trigger, Boundary (not Player, not itself) |
| Ground | 8 | Player, Ragdoll, Obstacle |
| Obstacle | 9 | Player, Ragdoll, Ground, MovingPlatform, Boundary, Obstacle |
| MovingPlatform | 10 | Player, Ragdoll, Obstacle |
| Trigger | 11 | Player, Ragdoll only (water, checkpoints, finish) |
| Boundary | 12 | Player, Ragdoll, Obstacle |

To enable ragdoll self-collision, change `CollidesGameplay` for `Ragdoll` and rerun the validator.

## 4. Generated assets

Created by `Editor/Foundation1Builder*.cs` on first open or via the **Bikini Wipeout** menu.

| Asset | Path |
| --- | --- |
| Player | `Prefabs/Characters/Player_Placeholder.prefab` |
| Obstacles | `Prefabs/Obstacles/RotatingSweeper`, `BouncingBall`, `MovingPlatform` |
| Environment | `Prefabs/Environment/Checkpoint`, `FinishLine`, `WaterHazard`, `SplashEffect` |
| Systems | `Prefabs/Systems/AudioManager` |
| Configs | `Settings/PlayerMovementConfig`, `KnockdownConfig`, `RagdollConfig`, `CameraConfig`, `AudioLibrary` |
| Animation | `Animations/PlaceholderCharacter.controller`, `Animations/Placeholder/*.anim` |
| Scenes | `Scenes/MainMenu.unity`, `Scenes/Course_Foundation1.unity` |

Config assets, materials, the audio library and the Animator controller are **never overwritten** if
they exist, so tuning survives a rebuild. Prefabs and scenes are overwritten only by
"Rebuild (Overwrite Generated)". If you change prefab structure by hand and want it to survive a
rebuild, either stop using the rebuild command or port the change into the builder.

## 5. Other systems (for reference)

- **Course flow:** `CourseManager` + `Core.CourseSession` — countdown, timer, pause (time scale 0),
  counters, finish, restart (scene reload), main menu, best time (`PlayerPrefs`).
- **Checkpoints / respawn:** `CheckpointManager`, `Checkpoint`, `PlayerRespawner` + `Core.RespawnGuard`
  (hazard cooldown, loop detection with fallback to the previous checkpoint, kill height).
- **Water:** `WaterHazard` trigger + `SplashEffect` placeholder. No swimming.
- **Audio:** static `AudioManager.Play(AudioEventId, position)` / `PlayMusic`; clips mapped in
  `AudioLibrary`; master via `AudioListener.volume`, music/SFX gains in `Core.VolumeSettings`.
  No `AudioMixer` yet — `VolumeSettings.LinearToDecibels` is there for when one is added.
- **Input:** `Settings/Input/BikiniWipeoutControls.inputactions` (also the project-wide actions asset).

## 6. Suggested first Astra tasks

1. Open, generate, run the PlayMode suite, and playtest; fix anything the first real run exposes.
2. Verify ragdoll joint limits and tune `RagdollConfig` / `KnockdownConfig` until knockdowns read well.
3. Import the approved character and clips; follow the swap steps in 2.4.
4. Replace the timed recovery with face-up / face-down get-up animations.
5. Evaluate ragdoll self-collision and partial (stumble) reactions.
6. Add camera impact feedback.
