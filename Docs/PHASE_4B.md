# Phase 4B · Outdoor course art

The approved image puts a glossy red ball against a cobalt pedestal, with turquoise water around its base. Large white chevrons, a splash-backed wordmark and a red padded arch make the event visible from a distance. This pass applies those features to the existing routes.

| Area | Implemented change |
| --- | --- |
| Branding and scenery | The launch has angled padded shoulders, warm four-lamp banks and splash pennants. The two-sided jumbotrons use the splash-backed wordmark. Spectator decks have bright rails, pennants and larger crowd silhouettes. Each sign face explicitly uses its printed material. The first pass had assigned the water material to the set pieces. |
| Ball and platform surfaces | Scarlet shells have a narrow equatorial seam and a high-gloss URP finish. A shared 300-triangle mesh rounds the edges of rectangular pads. The landing surfaces retain their existing colliders. Foundation decks, Gauntlet vinyl and Cove deck panels share the cobalt palette. |
| Water | Broken turquoise crests catch the sunlight. Each course has a painted mask derived from its fixed layout for red and blue reflection streaks near the set. Moving characters and equipment do not generate live reflections. |
| Setting | A painted sky wraps distant trees, hills and clouds around all seven scenes. Neutral-warm sunlight keeps white graphics readable. The existing tropical cliffs, palms and waterfalls remain in Cove. |

## Local review

Open the committed scenes directly from `Assets/BikiniWipeout/Scenes` in Unity 6000.6.6f1.

1. Open `Course_ChaosCove` first. At the Big Balls, compare highlights, the blue pedestals and the water around them with the approved image.
2. Open `Course_SplashdownGauntlet`. Check the launch arch, directional markings, both sides of the in-world scoreboard and a normal water-entry respawn.
3. Open `Course_Foundation1` to check the training pads. The Stars & Stripes variants and the Foundation Cowgirl variant share the art.
4. Run `BroadcastArtTests` in the Unity EditMode Test Runner for the imported mesh and sign materials. Compare frame time with the previous branch revision at the same camera position and resolution.

Cloud checks compared serialized components against `ca819ef` across all seven scenes, verified the renderer material assignments and water maps, and decoded the index and vertex buffers to check bounds, normals and triangle winding. Unity shader compilation, rendering, Unity tests, play feel and laptop performance await local review. The reference's camera composition and obstacle spacing are illustrative. The game uses its original camera and course dimensions.

## Changed files

The table lists every changed source and content file. Each new Unity asset also includes its importer metadata beside it.

| Directory | Files |
| --- | --- |
| `Assets/BikiniWipeout/Art/Broadcast` | `BroadcastJib.prefab`, `CompetitionWater.shader`, `Course1SetColors.png`, `Course2SetColors.png`, `CourseHorizon.png`, `CourseSky.mat`, `CourseSky.shader`, `CoveWater.mat`, `EventWater.mat`, `FinishBoard.png`, `FinishMarquee.prefab`, `FoundationSetColors.png`, `GalleryBoard.png`, `GauntletWater.mat`, `JumbotronFaces.prefab`, `LaunchGantry.prefab`, `PaddedBlock.asset`, `RippleField.png`, `SpectatorDeck.prefab`, `SplashPennant.mat`, `SplashPennant.png`, `StartBoard.png` |
| `Assets/BikiniWipeout/Materials` | `FinishRed.mat` |
| `Assets/BikiniWipeout/Materials/Arena` | `BallRed.mat`, `PanelsBlueTeal.mat`, `PanelsPinkPurple.mat`, `PanelsRainbow.mat` |
| `Assets/BikiniWipeout/Materials/Course2` | `CoveDeckBlue.mat`, `CovePillarBlue.mat` |
| `Assets/BikiniWipeout/Prefabs/Course1` | `ArenaWater.prefab`, `BouncingBallArena.prefab` |
| `Assets/BikiniWipeout/Prefabs/Obstacles` | `BouncingBall.prefab` |
| `Assets/BikiniWipeout/Scenes` | `Course_ChaosCove.unity`, `Course_ChaosCove_StarsStripes.unity`, `Course_Foundation1.unity`, `Course_Foundation1_Cowgirl.unity`, `Course_Foundation1_StarsStripes.unity`, `Course_SplashdownGauntlet.unity`, `Course_SplashdownGauntlet_StarsStripes.unity` |
| `Assets/BikiniWipeout/Scripts/Editor` | `Foundation1Builder.Assets.cs`, `Foundation1Builder.Broadcast.cs`, `Foundation1Builder.Course1Assets.cs`, `Foundation1Builder.Course1Prefabs.cs`, `Foundation1Builder.Course1Scene.cs`, `Foundation1Builder.Course2Assets.cs`, `Foundation1Builder.Course2Scene.cs`, `Foundation1Builder.Prefabs.cs`, `Foundation1Builder.Scenes.cs` |
| `Assets/BikiniWipeout/Tests/EditMode/Project` | `BroadcastArtTests.cs` |
| `Assets/BikiniWipeout/Textures/Arena` | `Ball_Shell.png`, `Chevron_BlueYellow.png`, `Crowd.png`, `Floodlight.png`, `Panels_BlueTeal.png`, `Panels_PinkPurple.png`, `Panels_Rainbow.png`, `Panels_YellowOrange.png` |
| `Assets/BikiniWipeout/Textures/Cove` | `Deck_Blue.png` |
| `Tools` | `generate_arena_textures.py`, `generate_cove_textures.py` |
