using UnityEditor;
using UnityEngine;

namespace BikiniWipeout.EditorTools
{
    /// <summary>
    /// Generates Course 2 once, the first time the project is opened after this content arrives.
    /// It only ever creates what is missing; existing scenes, prefabs and tuned assets are left alone.
    /// </summary>
    [InitializeOnLoad]
    internal static class Course2AutoSetup
    {
        private const string AttemptedKey = "Splashdown.Course2AutoSetup.Attempted";
        private static int waits;

        static Course2AutoSetup()
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

            // Let the Foundation and Course 1 first-open generators finish before this one starts.
            if ((!Foundation1Builder.IsContentBuilt() || !Foundation1Builder.IsCourse1Built()) && waits < 3)
            {
                waits++;
                EditorApplication.delayCall += TryRun;
                return;
            }

            SessionState.SetBool(AttemptedKey, true);
            if (Foundation1Builder.IsCourse2Built())
            {
                return;
            }

            // Never discard unsaved work in the scene that happens to be open.
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            {
                if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)
                {
                    Debug.Log("[Splashdown] Course 2 has not been generated yet. Save your scene, then run: " +
                              "Bikini Wipeout > Course 2 > Build Chaos Cove (Missing Only)");
                    return;
                }
            }

            Debug.Log("[Splashdown] Generating Course 2 - Chaos Cove...");
            if (!Foundation1Builder.BuildCourse2(false))
            {
                Debug.LogError("[Splashdown] Automatic Course 2 generation failed (see the error above). After fixing the cause, run: " +
                               "Bikini Wipeout > Course 2 > Build Chaos Cove (Missing Only)");
            }
        }
    }
}
