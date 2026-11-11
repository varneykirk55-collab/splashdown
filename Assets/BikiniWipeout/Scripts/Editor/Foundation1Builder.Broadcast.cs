using UnityEditor;
using UnityEngine;

namespace BikiniWipeout.EditorTools
{
    public static partial class Foundation1Builder
    {
        private const string BroadcastFolder = "Assets/BikiniWipeout/Art/Broadcast/";

        private static void PlaceBroadcastProp(string name, float x, float z, float yaw, float y = 0f)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BroadcastFolder + name + ".prefab");
            InstantiatePrefab(prefab, null, new Vector3(x, y, z), Quaternion.Euler(0f, yaw, 0f));
        }

        private static void FinishCourseArt()
        {
            var pad = AssetDatabase.LoadAssetAtPath<Mesh>(BroadcastFolder + "PaddedBlock.asset");
            var blue = AssetDatabase.LoadAssetAtPath<Material>(ArenaMaterialFolder + "/PanelsBlueTeal.mat");
            RenderSettings.skybox = AssetDatabase.LoadAssetAtPath<Material>(BroadcastFolder + "CourseSky.mat");
            foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            {
                foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (PrefabUtility.IsPartOfPrefabInstance(filter) || filter.sharedMesh == null || filter.sharedMesh.name != "Cube")
                        continue;

                    switch (filter.gameObject.name)
                    {
                        case "StartPlatform":
                        case "FinishPlatform":
                        case "CheckpointPlatform1":
                        case "CheckpointPlatform2":
                        case "BallAlley":
                            filter.GetComponent<MeshRenderer>().sharedMaterial = blue;
                            filter.sharedMesh = pad;
                            break;
                        case "Deck":
                        case "Trim":
                            filter.sharedMesh = pad;
                            break;
                    }
                }
            }
        }

        private static void DressFoundation1()
        {
            FinishCourseArt();
            PlaceBroadcastProp("LaunchGantry", 0f, 4f, 0f);
            PlaceBroadcastProp("FinishMarquee", 10f, 86f, -18f);
            PlaceBroadcastProp("BroadcastJib", -17f, 28f, 68f);
            PlaceBroadcastProp("SpectatorDeck", 19f, 52f, 90f);
        }

        private static void DressSplashdownGauntlet()
        {
            FinishCourseArt();
            PlaceBroadcastProp("JumbotronFaces", 38f, 52f, 0f, 10.5f);
            PlaceBroadcastProp("LaunchGantry", 0f, 8f, 0f);
            PlaceBroadcastProp("FinishMarquee", 88f, -3f, 172f);
            PlaceBroadcastProp("BroadcastJib", -17f, 44f, 82f);
            PlaceBroadcastProp("BroadcastJib", 93f, 89f, -72f);
        }

        private static void DressChaosCove()
        {
            FinishCourseArt();
            PlaceBroadcastProp("JumbotronFaces", 50f, 46f, 0f, 10.5f);
            PlaceBroadcastProp("LaunchGantry", 0f, 8f, 0f);
            PlaceBroadcastProp("FinishMarquee", 25f, 3f, 90f);
            PlaceBroadcastProp("BroadcastJib", -19f, 76f, 77f);
            PlaceBroadcastProp("BroadcastJib", 114f, 54f, -76f);
            PlaceBroadcastProp("SpectatorDeck", 10f, -18f, 180f);
            PlaceBroadcastProp("SpectatorDeck", 45f, -18f, 180f);
        }
    }
}
