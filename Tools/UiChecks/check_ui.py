#!/usr/bin/env python3
"""Static integration checks; does not compile or run Unity."""
from pathlib import Path

root = Path(__file__).resolve().parents[2]
def read(path): return (root / path).read_text(encoding='utf-8')
runtime = 'Assets/BikiniWipeout/Scripts/Runtime/'
editor = 'Assets/BikiniWipeout/Scripts/Editor/'
menu = read(runtime + 'UI/MainMenuController.cs')
pause = read(runtime + 'UI/PauseMenuController.cs')
course = read(runtime + 'Course/CourseManager.cs')
graphics = read(runtime + 'UI/GraphicsSettingsController.cs')
builder = read(editor + 'Foundation1Builder.UI.cs')
scenes = read(editor + 'Foundation1Builder.Scenes.cs')
gauntlet = read(editor + 'Foundation1Builder.Course1Scene.cs')
checks = {
    'course readiness and disabled placeholder': 'ready && !string.IsNullOrEmpty(sceneName) && Application.CanStreamedLevelBeLoaded(sceneName)' in menu and 'sceneName = "", ready = false' in menu,
    'paged course registry': 'coursePage * 2 + 1' in menu and '(coursePage + 1) * 2 < courses.Length' in menu,
    'character selection reads and saves identifier': 'PlayerPrefs.GetString(CharacterKey' in menu and 'PlayerPrefs.SetString(CharacterKey, characters[characterIndex].id)' in menu,
    'settings persisted and restored': all('PlayerPrefs.GetInt("Splashdown.' + x in graphics and 'PlayerPrefs.SetInt("Splashdown.' + x in graphics for x in ('Quality', 'Width', 'Height', 'Fullscreen')),
    'pause settings close before resume': 'pauseMenu.TryCloseSettings()' in course and 'settingsPanel.SetActive(false)' in pause and 'course.SetPaused(false)' in pause,
    'menu first, other scenes preserved': 'new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes)' in scenes and 'scenes.Insert(0, new EditorBuildSettingsScene(MainMenuScenePath, true))' in scenes,
    'course generator includes gameplay UI': 'BuildCourseUi(course)' in gauntlet and 'results.courseSelectButton' in builder,
    'navigation and scene transition guards': 'loading = true' in menu and 'sceneTransition = true' in course and 'CanStreamedLevelBeLoaded(SceneNames.MainMenu)' in course,
    'settings and controls wired in both contexts': builder.count('AddComponent<GraphicsSettingsController>()') == 2 and builder.count('BuildVolumeControls(') >= 3,
}
for name, ok in checks.items(): print(('PASS' if ok else 'FAIL') + ': ' + name)
if not all(checks.values()): raise SystemExit(1)
