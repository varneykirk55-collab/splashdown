# Sweeper mesh integration (branch astra/sweeper-mesh-integration)

Replaces the *look* of `SweeperSmall` and `SweeperLarge` (Assets/BikiniWipeout/Prefabs/Course1) with the refined SD_Sweeper meshes.
Done by `Assets/BikiniWipeout/Art/SD/Editor/SDSweeperIntegration.cs` (`Apply` is idempotent).

- Added `SD_Visual_Base` (child of the prefab root) and `SD_Visual_Arm` (child of `Arm`), identity local pose, Default layer, **no colliders**.
- The six primitive MeshRenderers per prefab (Hub, HubCap, 2x Tube, 2x Pad) are **disabled, not deleted**; their colliders are untouched.
- `gameplay_before.txt` vs `gameplay_after.txt`: dump of every GameObject (layer, active, local pose) and every non-renderer component's serialized fields, excluding the new `SD_Visual_*` objects. The two files are identical.
- Screenshots: `before_*.png`, `after_*.png` (A_Sweeper_1 and C_Sweeper in Course_SplashdownGauntlet, edit-mode render, scene never saved).
- Tests: `editmode_results.xml` (157/157 passed), `playmode_course1_results.xml` (Course1PlayTests 9/9 passed).
- To revert: `git checkout <parent> -- Assets/BikiniWipeout/Prefabs/Course1/SweeperSmall.prefab Assets/BikiniWipeout/Prefabs/Course1/SweeperLarge.prefab`.
