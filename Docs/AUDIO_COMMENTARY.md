# Audio: Commentary Director and Music

Branch `astra/audio-commentary`, based on `astra/phase-3b-integration` @ 1b49689 (Course 1 + cowgirl). Not pushed, not merged.

## How it works

- `CommentaryScheduler` (Core, engine-free, unit tested) decides what plays: priorities, cooldowns, sequencing, interruption.
- `CommentaryDirector` (Runtime/Audio, on the persistent AudioManager prefab) listens to existing gameplay events and plays the clips on two voices (John, Alex). It re-binds itself to whichever course scene is loaded.
- `CommentaryLibrary` (`Settings/CommentaryLibrary.asset`) holds the 10 recorded exchanges, their transcripts and topic tags. Add recordings there; no code change.
- Re-run the wiring any time: **Bikini Wipeout > Audio > Integrate Commentary and Music** (idempotent), or batch `-executeMethod BikiniWipeout.EditorTools.CommentaryIntegration.IntegrateBatch`.

## Gameplay events used (all existed already)

| Event | Source | Topic | Priority |
|---|---|---|---|
| Knockdown by ball | `KnockdownController.KnockedDown`, `ImpactData.Kind = Ball` | BallHit | Major |
| Knockdown by sweeper | same, `Kind = Sweeper` | SweeperHit | Major |
| Knockdown by a fast obstacle (obstacle speed x force >= 6 m/s) | same | Launch | Major |
| Knockdown by platform / scripted | same, other kinds | HardImpact | Notable |
| Fall into water | `PlayerRespawner.FellInHazard` | WaterFall | Notable |
| 3 failures within 90 s, no checkpoint between | counted by the director | RepeatedFailure | Major |
| Back on her feet / respawned | `KnockdownController.Recovered`, `PlayerRespawner.Respawned` | Recovery | Routine |
| Checkpoint reached | `CheckpointManager.CheckpointActivated` | Checkpoint | Routine |
| Course complete | `CourseManager.Finished` | Finish | Major |
| 35 s of quiet running | timer in the scheduler | Background | Background |

Not implemented because the game has no such event: swimming (no swim mechanic), player elimination (no lives or elimination), near misses (`ProximityWhoosh` tracks the camera, not the contestant).

## Rules

- One exchange = setup line, then (0.25-0.5 s later, configurable) the response. The response is never cut.
- Nothing interrupts a running exchange. A Notable/Major event waits in a single pending slot (highest priority wins; stale after 5 s). Routine and Background events are dropped when busy.
- Only a Background chat can be cancelled, and only before its response line; the cut line fades out in 0.12 s.
- Same topic: 6-30 s cooldown (per topic). Same exchange: 90 s. If every fitting exchange is cooling down, a Major event reuses the least recently played one; lower priorities stay silent.
- 1.2 s of silence between exchanges.
- Music ducks to 35 % while an exchange is active (0.15 s attack, 0.9 s release).

## Recording inventory and mapping

Transcribed locally with whisper.cpp (small.en) and paired by file number. Speakers: john = British, alex = American.

| Exchange | Setup | Response | Used for |
|---|---|---|---|
| 01_trolley | John: "She's airborne with the grace of a shopping trolley pushed off a cliff." | Alex: "And the trolley had a better landing." | Launch, BallHit, SweeperHit, HardImpact |
| 02_crowd_on_fire | **Alex**: "The crowd is on its feet!" | **John**: "Mostly because the seats are on fire." | Checkpoint, Finish, Background |
| 03_netting_fish | John: "She's tangled in the netting like a very enthusiastic fish." | Alex: "Except the fish isn't crying." | **Disabled** (no nets in the game) |
| 04_medical | John: "Medical attention is on the way." | Alex: "So is the coroner, just in case." | BallHit, SweeperHit, HardImpact, RepeatedFailure |
| 05_athleticism | John: "That was a spectacular display of athleticism!" | Alex: "Athleticism's a strong word for what just happened." | Launch, BallHit, SweeperHit |
| 06_judges | John: "The judges are deliberating." | Alex: "They're deciding if that counts as a wipeout or a cry for help." | RepeatedFailure, Finish, Background |
| 07_waving | John: "She's waving to the audience!" | Alex: "That's not waving. That's a distress signal with better lighting." | Recovery |
| 08_water_lovely | John: "The water's lovely this time of year!" | Alex: "She didn't ask for a weather report." | WaterFall |
| 09_replay | John: "The replay shows it all in glorious detail." | Alex: "\"Glorious\" isn't the word her lawyer's using." | Launch, BallHit, SweeperHit, RepeatedFailure |
| 10_sign_off | John: "And that's Splashdown for today." | Alex: "Tune in next week when someone actually survives!" | Finish (weighted x3) |

Originals are untouched on the Desktop; copies are in `Assets/BikiniWipeout/Audio/Commentary/` (mono, decompress-on-load).

## Music

Originals (WAV, ~35 MB each, 2:55-3:01) are untouched on the Desktop. Project copies are Ogg Vorbis q6 in `Assets/BikiniWipeout/Audio/Music/` (~3.8 MB each), imported as streaming Vorbis.

| Slot | Track | Why |
|---|---|---|
| Main menu (`MusicTrackId.MainMenu`) | Tide of Ku | Steadiest dynamics (LRA 2.3 LU), quietest opening |
| Course (`MusicTrackId.Course`) | Obsidian Surge (Gaming Edition) | Steady dynamics (LRA 2.7 LU), "Gaming Edition", sits well under commentary |
| Results (`MusicTrackId.Results`, new; starts at the finish line) | Ancestral Fury | Wide dynamics, strong ending |
| Unassigned | Obsidian Edge (New Chorus Hook), BASALT TEETH (Battle Chant Edition) | Imported and ready as alternates |

These choices came from titles and loudness measurements, **not from listening**. Swap them in `Settings/AudioLibrary.asset` (Music list). All loop; changing track crossfades over 1.5 s. The old placeholder music files are kept but unreferenced.

## Volume controls

Master / Music / Sound Effects / **Announcers** (new fourth slider, added to the menu, pause and results panels). Saved in PlayerPrefs (`bw.audio.announcer` beside the existing keys). Crowd ambience still follows the SFX slider and is not ducked.

## Tests

- EditMode: 157/157 (17 new: scheduler priorities, sequencing, cooldowns, interruption, no-overlap storm; announcer volume persistence).
- PlayMode: 8 new tests against the real Gauntlet scene, all pass. Full PlayMode: 44/47. The 3 failures are the known batch-mode ones that already failed before this work (`Camera_CloseWallDoesNotForceMinimumDistanceThroughWall`, `Player_Jumps_AndLands`, `RagdollRespawn_RestoresCheckpointOrientationAndMouseLook`).
- Tests check that clips are chosen, sequenced, non-overlapping, ducked and routed. They cannot judge how it sounds: a human listen is still needed.

## Known gaps

- Only 10 exchanges for the whole game, so wipeout-heavy runs will repeat lines after the 90 s cooldown, and some events go silent when everything fitting is cooling down.
- No recordings for: near misses, swimming, elimination, a dedicated "ball" or "sweeper" line, run start, or checkpoints that are really about progress (checkpoint reuses the crowd line).
- Pair 9 mentions a replay; the game has none.
- Pair 3 (netting) is disabled; set `enabled` on it in the library to use it for water falls.
