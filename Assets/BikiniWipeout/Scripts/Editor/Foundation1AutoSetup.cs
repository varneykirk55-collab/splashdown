using UnityEditor;
using UnityEngine;

namespace BikiniWipeout.EditorTools
{
    /// <summary>
    /// The repository stores the game as code. The first time the project is opened (after scripts
    /// compile and assets import), this generates the missing prefabs and scenes once so that
    /// "open the project, press Play" works. It never overwrites existing content.
    /// </summary>
    [InitializeOnLoad]
    internal static class Foundation1AutoSetup
    {
        private const string AttemptedKey = "BikiniWipeout.Foundation1AutoSetup.Attempted";

        static Foundation1AutoSetup()
        {
            // Command-line runs call the builder explicitly (see README).
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

            // One attempt per editor session: a failing build must not retry on every script reload.
            SessionState.SetBool(AttemptedKey, true);
            if (Foundation1Builder.IsContentBuilt())
            {
                return;
            }

            Debug.Log("[Bikini Wipeout] First open: generating Foundation 1 prefabs and scenes...");
            if (!Foundation1Builder.BuildAll(false))
            {
                Debug.LogError("[Bikini Wipeout] Automatic content generation failed (see the error above). " +
                               "After fixing the cause, run: Bikini Wipeout > Build Foundation 1 Content (Missing Only)");
            }
        }
    }
}
