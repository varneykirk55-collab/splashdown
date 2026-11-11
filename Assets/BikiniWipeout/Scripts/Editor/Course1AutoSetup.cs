using UnityEditor;
using UnityEngine;

namespace BikiniWipeout.EditorTools
{
    /// <summary>
    /// Generates Course 1 once, the first time the project is opened after this content arrives.
    /// It only ever creates what is missing; existing scenes, prefabs and tuned assets are left alone.
    /// </summary>
    [InitializeOnLoad]
    internal static class Course1AutoSetup
    {
        private const string AttemptedKey = "Splashdown.Course1AutoSetup.Attempted";

        static Course1AutoSetup()
        {
            if (Application.isBatchMode || SessionState.GetBool(AttemptedKey, false))
            {
                return;
            }

            EditorApplication.delayCall += TryRun;
        }

        private static void TryRun()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += TryRun;
                return;
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            SessionState.SetBool(AttemptedKey, true);
            if (Foundation1Builder.IsCourse1Built())
            {
                return;
            }

            // Never discard unsaved work in the scene that happens to be open.
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            {
                if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)
                {
                    Debug.Log("[Splashdown] Course 1 has not been generated yet. Save your scene, then run: " +
                              "Bikini Wipeout > Course 1 > Build Splashdown Gauntlet (Missing Only)");
                    return;
                }
            }

            Debug.Log("[Splashdown] Generating Course 1 - The Splashdown Gauntlet...");
            if (!Foundation1Builder.BuildCourse1(false))
            {
                Debug.LogError("[Splashdown] Automatic Course 1 generation failed (see the error above). After fixing the cause, run: " +
                               "Bikini Wipeout > Course 1 > Build Splashdown Gauntlet (Missing Only)");
            }
        }
    }
}
