# Stars & Stripes (charD) integration handoff

Status: source assets, controller, materials, import settings, builder, and tests prepared.
**Unity prefab/scene generation and in-engine validation are pending license activation.**
Unity 6000.6.6f1 exits with code 198: `No valid Unity Editor license found. Please activate your license.`
See `Logs/charD-integration.log`. Git read-only access is not a development blocker; no Git writes were attempted.

## Files added

All asset paths below are relative to `Assets/BikiniWipeout/` and include accompanying `.meta` files.

- `Characters/StarsStripes/charD_starsstripes.fbx`: original supplied rigged FBX, unmodified; Humanoid import settings and explicit material remaps.
- `Characters/StarsStripes/Textures/`: body BaseColor, Normal and MetallicSmoothness; hands BaseColor and MetallicSmoothness.
- `Characters/StarsStripes/Materials/`: separate body and hands URP Lit materials. Normal/data textures are linear; albedo is sRGB. No nonexistent hand normal map is assigned.
- `Animations/StarsStripes/StarsStripes.controller`: separate copy of the cowgirl controller, retaining shared animation references and all state transitions, locomotion weights, jump-root corrections and recovery speeds.
- `Settings/KnockdownConfig_StarsStripes.asset`: separate knockdown configuration with 2.1-second recovery.
- `Scripts/Editor/StarsStripesIntegration.cs`: explicit integration command. Builds charD from the placeholder gameplay root, adds its own model/avatar, configures its 11-body/10-joint ragdoll, and creates a dedicated validation scene.
- `Tests/EditMode/Project/StarsStripesContentTests.cs`: checks avatar ownership, material/texture wiring, missing scripts, ragdoll structure, controller ownership and timing.
- `Tests/PlayMode/StarsStripesPlayTests.cs`: adapts the existing cowgirl behavior/quality tests for charD: standing, walk/run/sprint, feet and knee alignment, pelvis motion, jump displacement, fall facing, landing transitions, and face-up/face-down ragdoll recovery.

Cowgirl and shared gameplay source files are unchanged. Animation clips under `Animations/Cowgirl` remain read-only dependencies. The copied controller retains cowgirl cadence/blend thresholds; charD-specific foot sliding and pose quality require the pending PlayMode measurements.

## Finish generation after activating Unity

Run `Bikini Wipeout > Characters > Integrate Stars & Stripes`, or:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.6.6f1\Editor\Unity.exe' -batchmode -nographics -projectPath C:\GameDev\Splashdown-Characters -executeMethod BikiniWipeout.EditorTools.StarsStripesIntegration.Integrate -quit -logFile C:\GameDev\Splashdown-Characters\Logs\charD-integration.log
```

The builder creates these currently pending assets:

- `Prefabs/Characters/Player_StarsStripes.prefab`
- `Scenes/Course_Foundation1_StarsStripes.unity`

The scene starts from the existing foundation course, preserves its player placement/checkpoints, and remaps camera/course/UI references to the new player. It does not change build settings or existing scenes. Regeneration preserves destination GUIDs.

Run the Unity EditMode suite and PlayMode filter `BikiniWipeout.Tests.StarsStripesPlayTests`. Missing charD scene is a test failure, not a skip. Tests do not replace visual review in the Game view.

## Terra registry contract

- Stable ID: `charD`
- Display name: `Stars & Stripes`
- Prefab path after generation: `Assets/BikiniWipeout/Prefabs/Characters/Player_StarsStripes.prefab`
- Root API: existing `PlayerCharacter`, `CharacterMotor`, `KnockdownController`, `RagdollController` and `PlayerRespawner`.
- Capsule height: 1.65 m; camera focus height: 1.0 m; movement configuration shared with cowgirl; root motion disabled.
- Do not register as ready until the prefab has been generated and Unity tests pass. No registry schema exists in this branch, so no competing registry implementation was introduced.

## Validation and known defects

- Offline Roslyn compilation against the installed Unity 6000.6.6f1 assemblies and exact Input System 1.20.0: all project assemblies pass, including editor/player runtime and both test assemblies, with zero warnings/errors.
- Existing core suite: **122 passed, 0 failed, 0 skipped**, using Unity's bundled .NET 8.0.21 runtime. Results: `Logs/charD-core-tests/core.trx`. The initial system .NET invocation could not run the net8 test host; the bundled runtime resolved that.
- Unity import, Avatar validity, generated prefab/scene, rendering and PlayMode behavior: **not run**, blocked by the Editor license.
- Source FBX and texture integrity and preservation of existing project files: see `Logs/charD-source-validation.json`.
- Read-only Git comparison against HEAD reports no modified tracked files. All six copied source files match their originals by SHA-256. This integration adds 30 asset/metadata files plus this handoff; no existing character or gameplay assets were edited.
- Source delivery notes report reduced but remaining eye smearing/jagged left-eye UVs, a slightly lighter/pinker hand tone, no hand tattoo/nail detail, no dedicated neck texture blend pass, and hair shear on large head turns. Fist webbing and deep squats also stretch. These are source-reported defects, not newly observed Unity defects.
- Final visual review must check material orientation, foot contact/sliding, knee/finger axes, hair deformation, jump/landing continuity and both recovery poses before declaring integration complete.
