using UnityEditor;
using UnityEngine;

namespace BikiniWipeout.EditorTools
{
    public static partial class Foundation1Builder
    {
        private const string BroadcastFolder = "Assets/BikiniWipeout/Art/Broadcast/";

        private static void PlaceBroadcastProp(string name, float x, float z, float yaw)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BroadcastFolder + name + ".prefab");
            InstantiatePrefab(prefab, null, new Vector3(x, 0f, z), Quaternion.Euler(0f, yaw, 0f));
        }

        private static void DressFoundation1()
        {
            PlaceBroadcastProp("LaunchGantry", 0f, 4f, 0f);
            PlaceBroadcastProp("FinishMarquee", 10f, 86f, -18f);
            PlaceBroadcastProp("BroadcastJib", -17f, 28f, 68f);
            PlaceBroadcastProp("SpectatorDeck", 19f, 52f, 90f);
        }

        private static void DressSplashdownGauntlet()
        {
            PlaceBroadcastProp("LaunchGantry", 0f, 8f, 0f);
            PlaceBroadcastProp("FinishMarquee", 88f, -3f, 172f);
            PlaceBroadcastProp("BroadcastJib", -17f, 44f, 82f);
            PlaceBroadcastProp("BroadcastJib", 93f, 89f, -72f);
        }

        private static void DressChaosCove()
        {
            PlaceBroadcastProp("LaunchGantry", 0f, 8f, 0f);
            PlaceBroadcastProp("FinishMarquee", 25f, 3f, 90f);
            PlaceBroadcastProp("BroadcastJib", -19f, 76f, 77f);
            PlaceBroadcastProp("BroadcastJib", 114f, 54f, -76f);
            PlaceBroadcastProp("SpectatorDeck", 10f, -18f, 180f);
            PlaceBroadcastProp("SpectatorDeck", 45f, -18f, 180f);
        }
    }
}
