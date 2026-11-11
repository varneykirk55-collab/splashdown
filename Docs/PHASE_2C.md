# Splashdown Phase 2C — Jump teleport fix & running refinement (cowgirl)

Branch: `astra/phase-2c-jump-run` (from `astra/phase-2b-cowgirl-locomotion` @ `e535cf2`). Date: 2026-11-10.
The walk, movement speeds, the controller code, the ragdoll and all Phase 2A systems are unchanged.

## 1. Jump teleport

**What moved: only the mesh.** A per-frame in-game trace showed the player root following the motor's velocity
exactly on every frame (0 deviations over 1 cm). The body, measured relative to the root, was what jumped:

| Moment | Before | Cause |
|---|---|---|
| Landing | body **2.77 m ahead** of the capsule for ~0.2 s, then snapping back | `Cowgirl_Land` is cut from Running Jump frames 19–28 with XZ based on "Original". That keeps the authored offset from the file origin, and by f19 the jump has travelled ~2.4 m (2.8 m on her). |
| Takeoff | body dropped 8–13 cm against the rising capsule in ~50 ms | `Cowgirl_Jump` played the whole take from f1 (run-up stride) with height unbaked, so it held a low constant body height. |
| Touchdown | 14 cm drop in ~70 ms | Land height "from feet" started her below the extended-leg touchdown pose. |

Fixes (`CowgirlIntegration.cs`, clip import settings only):

- New `ClipDef.XZFromBody` (Based Upon = centre of mass) for Jump and Land, so the body stays centred on the capsule.
- `Cowgirl_Jump` = Running Jump **f2–11**, from push-off to apex (0.30 s; the motor's rise is ~0.34 s). Source
  frames: run-up f1–2, push-off f2–3, rise f3–11, fall f11–18, touchdown f19, settle f20–22.
- `Cowgirl_Land` height is now "Original" (authored hip height; same floor as the run).

After: hips stay within 11 cm horizontally of the capsule through the whole jump, and the largest body shift
within any 80 ms is 8–9 cm (the authored landing knee-bend), against 282 cm before. Takeoff hip height now matches
walking and running (0.85 m).

## 2. Running

Comparison of the raw Mixamo clips against their playback on her (frame-matched) showed both run clips retarget
faithfully: joint motion matches the source within ~1°, and knee bend matches exactly. The problem was which clip
was used for the sprint:

- The sprint (6.5 m/s) was **Slow Run time-scaled ×2.17**. Slow Run is a 3.0 m/s shuffle-jog (1.10 m steps,
  almost no flight phase), so the sprint ran at **5.9 steps/s** with short steps, a frantic shuffle.
- Fix: the Mixamo **Fast Run** (from Downloads, imported Humanoid with the same T-pose reference correction) is
  added to the Locomotion blend: 5.48 m/s native on her (1.46 m steps, 3.75 steps/s, real flight phase),
  time-scaled ×1.19 at 6.5 m/s, giving **4.45 steps/s**. Slow Run stays as the 3.0 m/s jog child, so the blend
  goes walk → jog → sprint. Walk children and all thresholds below 3.0 m/s are unchanged.
- The Fast Run's own rest pose in the file is not a T-pose (knees bent), so it uses the same synthesized
  T-pose reference as the other clips; its authored rest would add 29° of knee error.

Sprint in game: Fast Run weight 100%, knee bow-out max 4.4%, bent-knee direction 12°, hips facing travel 0.96
(the clip's pelvis is authored rotated ~12°, with counter-rotated shoulders), stance foot slide 22%.

Note: `Animations/Cowgirl/Fast Run.fbx` is the "With Skin" download (24 MB, includes a Mixamo mesh that is not
used). A "Without Skin" re-download can replace it with no other change.

## Tests (one Unity instance)

- EditMode 106/106.
- PlayMode 29 run, 26 passed. The 3 failures are the pre-existing ones (2 `WaitForEndOfFrame` batch-mode tests,
  `Player_Jumps_AndLands`).
- CowgirlPlayTests, all 12 passed:
  - New `Cowgirl_Jump_MeshStaysOnCapsule_NoSnap` (standing / walking / sprinting): horizontal offset under 20 cm,
    no body shift over 12 cm within 80 ms.
  - New `Cowgirl_Sprint_PlaysFastRun_LegsAligned`.
  - Walk values are identical to Phase 2B (100% walk clip, 5.5 cm sway, knee bow-out 3.9%).
  - Both get-ups pass.
- In-game renders (sprint front/side/rear, side-view running jump) checked: real running stride with flight phase,
  knees forward, body centred on the capsule through the jump.

## Remaining / unverified

- Mid-air, Falling Idle (knees tucked, one arm raised) changes from the leap pose abruptly. This is pre-existing
  and was not changed here.
- Sprint feet land close to the midline (ankle gap 0.05 of leg, a hip-proportion effect as with the walk). They do
  not cross badly in the renders; judge in play.
- Jog/sprint blend between 3.0 and 5.5 m/s is only seen while accelerating.
