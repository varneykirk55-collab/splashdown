# Splashdown outdoor broadcast sets

Based on the integrated courses at `069a255`. Open any committed course scene directly. The scene builders also place the broadcast prefabs when regenerating a course.

| Course | Set dressing |
| --- | --- |
| Splashdown Gauntlet | A two-sided launch gantry, two camera jibs outside the course lanes, and a finish marquee beside the existing gate. Existing stands, floodlights, and video board frame the arena. |
| Chaos Cove | Beach spectator decks behind the launch, a gantry, camera pontoons on opposite flanks, and a finish marquee. The cliffs and waterfalls remain visible. |
| Foundation | A start gantry, side gallery, camera pontoon, and finish board dress the training course and its character variants. |

The reusable assets live in `Assets/BikiniWipeout/Art/Broadcast`. The set pieces use static batching flags, shared materials, and no colliders, shadow casters, cameras, or additional lights. The course grade uses neutral tonemapping and low-intensity bloom where the course camera has post-processing enabled. The Foundation camera retains its original settings.

The water shader draws a single opaque surface using animated slopes, a caustic texture, sunlight and the environment cubemap. Reflections approximate the sky. The water surface has no live view of the character or stands. The splash uses the existing particle bursts and cooldown. The upward-facing emitter and translucent drop sprite are separate from the confetti material.

## Audio integration points for the project manager

| Cue | Existing integration |
| --- | --- |
| Course music | `MusicTrackId.Course` in the existing audio library |
| Crowd and environmental beds | `ArenaAmbience` plays `ArenaAmbience` or `TropicalAmbience` by course |
| Competition start or future spoken introduction | Course countdown already uses `CountdownTick` and `CountdownGo`. A dedicated spoken introduction needs its own cue. |
| Water entry | `WaterHazard.Splash` requests `WaterSplash` |
| Finish and crowd reaction | Existing `FinishCelebration`, `CrowdCheer`, and `ConfettiPop` cues |
| Future elimination | Reserve a separate cue when elimination is implemented. Current water entry can lead to a checkpoint respawn. |

## Unity playtest

1. In Unity 6000.6.6f1, open a course from `Assets/BikiniWipeout/Scenes` and press Play. Check both competition courses and switch character variants. The committed layout is ready without running a builder.
2. At spawn, check that the next landing, hazard edges and checkpoint remain visible at your usual zoom. Run to the finish to check the sightlines in the reverse direction.
3. Fall into water. Look for an upward spray, a ring on the surface in Gauntlet and Cove, and the usual checkpoint respawn. Check the water at low camera angles for shimmer or shader errors.
4. Compare frame time on the laptop at the same resolution and quality as the base commit. Unity shader compilation, rendering, PlayMode behavior and frame times require local verification.
