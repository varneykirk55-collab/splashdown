# Section A vs Section C sweeper colour (investigated 2026-11-12)

**Cause: a screenshot-capture artifact, not the scene, the materials or the prefabs.**
The first camera render(s) in a Unity batch session draw with stale material/texture data: the whole frame comes out as one flat colour
(yellow, red, slate or pink depending on the run; textures and even the SD_Blue parts collapse to a single hue; water disappears).
`Shoot()` always rendered `A_Sweeper_1` first, so every Section A screenshot was a broken first frame and Section C (second) looked right.
Evidence: `shadowtest_default.png` (first render) vs `shadowtest_noShadows.png` (same camera after warm-up).

What was compared (`lighting_probe.txt`), and found identical or irrelevant:
- Lighting: one global directional "Sun" (colour 1,0.96,0.84, intensity 1.45, soft shadows). No other lights, no local volumes, no reflection probes.
- Ambient: global trilight (sky 0.78/0.90/1.0, equator 0.86/0.84/0.80, ground 0.30/0.62/0.66); one procedural skybox for reflections.
- Materials on both sweepers: the same SD_Red / SD_Blue / SD_Chrome assets, URP Lit, metallic 0 / 0 / 1, smoothness 0.74 / 0.74 / 0.80, reflections on.
- Post-processing: none in the scene; the game's camera component adds bloom/vignette at runtime, which the capture camera does not use.
- Camera angle does change the look slightly (back-lit vs sun-side) in both sections equally.

Fix: warm-up renders before every capture in `SDSweeperIntegration.Shoot` (and four views, each sweeper from a back-lit and a sun-side angle).
No change to prefabs, materials, scene, colliders, pivots or scripts was needed.
