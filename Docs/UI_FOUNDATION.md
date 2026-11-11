# Splashdown UI foundation

## Setup and generation

Open the project in Unity 6000.6.6f1 with URP and let imports and the first-open generators finish. Foundation generation creates MainMenu and the original course; Course 1 generation creates `Course_SplashdownGauntlet`. The build scene list puts MainMenu first and retains unrelated entries. The generator opens MainMenu after creation. Course 1 is available only if explicitly marked ready and included in enabled build scenes.

For an existing generated project, back up scene edits before regeneration. **Bikini Wipeout > UI > Rebuild Main Menu Scene** replaces MainMenu only; it retains existing player and course assets. To install the updated pause and results interfaces into an existing Course 1, use **Bikini Wipeout > Course 1 > Rebuild Splashdown Gauntlet** after backing up the Course 1 scene and `Prefabs/Course1`: that command overwrites both. Missing-only generation never updates an existing scene. Do not use a rebuild to preserve hand-edited content. Inspect build settings after generation and confirm MainMenu is first and both required scenes are enabled.

## Architecture and configuration

MainMenuController owns screen navigation and a serialized course array. Course cards page two at a time. To add a future course, add an entry with its title and scene name, register the scene in Build Settings, and set `ready` only after integration and validation. Availability requires both `ready` and Unity reporting the scene loadable. Course 2 has no scene reference and remains disabled, independent of progression.

The character array currently exposes the existing course player only. The Course 1 builder reuses the existing course player prefab where available, otherwise its generated placeholder. The menu uses a text preview fallback because spawning that player would also activate gameplay, physics and input components. Selection is saved by stable identifier. Previous/next controls and the registry are in place, but new character IDs are deliberately rejected until a safe gameplay spawning integration and preview for them are implemented and validated; adding a registry entry alone will not swap the player. No extra character is claimed to be playable.

`GraphicsSettingsController` is shared by main-menu and pause settings; master, music and effects sliders use the existing `AudioManager` and `VolumeSliders`. Settings use PlayerPrefs. The existing input-system UI module supplies keyboard, mouse and controller navigation. Control text reflects the default player input map; a customized input action asset may differ. Paused settings close before pause input can resume play. Results reuse CourseManager completion and checkpoint data.

Buttons and panels use procedural rounded sprites and unscaled entrance/focus animation. The menu background has a slow decorative drift. AudioLibrary supplies existing menu music, hover and confirmation. `UiBack`, `UiTransition`, `UiCourseSelect`, `UiCharacterConfirm` and `UiCourseComplete` are separate event IDs: assign suitable clips in AudioLibrary. Unassigned events are safe silent hooks and report missing clips; no unrelated assets are substituted. Existing finish celebration remains in place.

## Validation and limitations

`python3 Tools/UiChecks/check_ui.py` checks source wiring for course guards, paging, persistence, pause, scene registration and generated UI. `git diff --check` checks whitespace. These are static checks, not C# compilation or behavioral tests. Unity Editor, its assemblies and .NET are unavailable in the Codex Cloud environment; Unity compilation, generated-scene missing-reference checks, EditMode/PlayMode tests and all interactive validation remain unrun. Character preview is text-only; additional character gameplay integration is not implemented. Audio clips for the new events are not supplied. Graphics changes and input navigation require Windows testing, including resolution and controller behavior.

## Manual Unity checklist

1. Back up edited generated assets; generate missing scenes or use the warned rebuild commands above. Check Console for errors and missing references. Verify MainMenu is first in Build Profiles/Build Settings.
2. Run **Bikini Wipeout > Validate Project**, then the complete EditMode and PlayMode suites. Resolve failures before continuing.
3. Start from MainMenu. Test mouse, keyboard and controller focus, back and transitions. Verify Course 1 is selectable and Course 2 says Coming Soon and cannot launch. Confirm the current player and enter Course 1.
4. Test movement, camera, ragdoll, checkpoints and audio. Pause with Escape and controller Start; open settings, change all controls, close settings, confirm gameplay remains paused, then resume. Test restart and return to menu.
5. Finish Course 1 and verify time, checkpoint count, Retry, Course Select and Main Menu. Check audio, focus and transitions.
6. Change volume, quality, resolution and fullscreen; restart a Windows build and verify persistence. Check readability and clipping at multiple aspect ratios, audio balance, performance and Console logs.
