using UnityEngine;
using UnityEngine.Rendering;

namespace BikiniWipeout.EditorTools
{
    /// <summary>
    /// Stadium dressing for the Splashdown arena. Everything here is visual only (no colliders) apart
    /// from the invisible boundary walls and the start-platform rails, so it costs no physics time.
    /// </summary>
    public static partial class Foundation1Builder
    {
        private static void BuildStadium(Transform parent, ContentAssets assets, ArenaAssets arena)
        {
            Transform root = Empty("Stadium", parent, Vector3.zero, GameLayers.Default).transform;
            float centerX = (ArenaMinX + ArenaMaxX) * 0.5f;
            float centerZ = (ArenaMinZ + ArenaMaxZ) * 0.5f;
            float lengthX = ArenaMaxX - ArenaMinX;
            float lengthZ = ArenaMaxZ - ArenaMinZ;

            // ---- Invisible walls keep the character, ragdoll and balls inside the arena.
            Transform bounds = Empty("Boundaries", root, Vector3.zero, GameLayers.Boundary).transform;
            Wall("Wall_West", bounds, new Vector3(ArenaMinX, 5f, centerZ), new Vector3(1f, 40f, lengthZ));
            Wall("Wall_East", bounds, new Vector3(ArenaMaxX, 5f, centerZ), new Vector3(1f, 40f, lengthZ));
            Wall("Wall_South", bounds, new Vector3(centerX, 5f, ArenaMinZ), new Vector3(lengthX, 40f, 1f));
            Wall("Wall_North", bounds, new Vector3(centerX, 5f, ArenaMaxZ), new Vector3(lengthX, 40f, 1f));

            // ---- Spectator stands on all four sides. yaw turns the stand to face the arena.
            Stand(root, "Stand_South", new Vector3(centerX, 0f, ArenaMinZ - 2f), 180f, lengthX + 30f, assets, arena);
            Stand(root, "Stand_North", new Vector3(centerX, 0f, ArenaMaxZ + 2f), 0f, lengthX + 30f, assets, arena);
            Stand(root, "Stand_West", new Vector3(ArenaMinX - 2f, 0f, centerZ), 270f, lengthZ + 30f, assets, arena);
            Stand(root, "Stand_East", new Vector3(ArenaMaxX + 2f, 0f, centerZ), 90f, lengthZ + 30f, assets, arena);

            // ---- Floodlight towers at the corners.
            Transform lights = Empty("Floodlights", root, Vector3.zero, GameLayers.Default).transform;
            Vector3 middle = new Vector3(centerX, 0f, centerZ);
            foreach (Vector3 corner in new[]
                     {
                         new Vector3(ArenaMinX - 4f, 0f, ArenaMinZ - 4f), new Vector3(ArenaMaxX + 4f, 0f, ArenaMinZ - 4f),
                         new Vector3(ArenaMinX - 4f, 0f, ArenaMaxZ + 4f), new Vector3(ArenaMaxX + 4f, 0f, ArenaMaxZ + 4f)
                     })
            {
                Floodlight(lights, corner, middle, assets, arena);
            }

            Jumbotron(root, new Vector3(38f, 0f, 52f), assets, arena);
            Scenery(root, middle, arena);
            Buoys(root, assets);
            StartDressing(root, assets, arena);
        }

        /// <summary>Three stepped tiers of abstract crowd with a back wall, a striped rail and flags along the top.</summary>
        private static void Stand(Transform parent, string name, Vector3 position, float yaw, float length, ContentAssets assets, ArenaAssets arena)
        {
            var stand = new GameObject(name);
            stand.transform.SetParent(parent, false);
            stand.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            Transform root = stand.transform;
            float bottom = Course1Layout_WaterBottom;

            // Local +Z points away from the arena.
            Primitive(PrimitiveType.Cube, "Base", root, new Vector3(0f, (bottom + 1.2f) * 0.5f, 1.5f),
                new Vector3(length, 1.2f - bottom, 3f), arena.Concrete, GameLayers.Default, false);
            Primitive(PrimitiveType.Cube, "BaseStripe", root, new Vector3(0f, 1.3f, 0.05f), new Vector3(length, 0.6f, 0.2f),
                arena.StripesBlue, GameLayers.Default, false);

            for (int tier = 0; tier < 3; tier++)
            {
                float top = 3.4f + tier * 3.2f;
                float depth = 5f;
                float z = 3f + tier * depth + depth * 0.5f;
                GameObject block = Primitive(PrimitiveType.Cube, "Tier" + tier, root, new Vector3(0f, (bottom + top) * 0.5f, z),
                    new Vector3(length, top - bottom, depth), arena.Crowd, GameLayers.Default, false);
                block.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            }

            float backZ = 3f + 3 * 5f + 0.6f;
            Primitive(PrimitiveType.Cube, "BackWall", root, new Vector3(0f, 7.5f, backZ), new Vector3(length, 15f, 1.2f),
                arena.PanelsBlue, GameLayers.Default, false);
            Primitive(PrimitiveType.Cube, "TopRail", root, new Vector3(0f, 15.3f, backZ), new Vector3(length, 0.7f, 1.5f),
                arena.StripesRed, GameLayers.Default, false);

            Material[] colours = { assets.HotPink, assets.Yellow, assets.Turquoise, assets.Orange, assets.Purple };
            int count = Mathf.Max(2, Mathf.RoundToInt(length / 13f));
            for (int i = 0; i < count; i++)
            {
                float x = -length * 0.5f + (i + 0.5f) * (length / count);
                Primitive(PrimitiveType.Cylinder, "FlagPole", root, new Vector3(x, 18.6f, backZ), new Vector3(0.14f, 3f, 0.14f),
                    assets.White, GameLayers.Default, false);
                GameObject flag = Primitive(PrimitiveType.Cube, "Flag", root, new Vector3(x + 1.1f, 20.6f, backZ), new Vector3(2.2f, 1.3f, 0.06f),
                    colours[i % colours.Length], GameLayers.Default, false);
                if (i % 3 == 0)
                {
                    var sway = flag.AddComponent<Bobbing>();
                    sway.height = 0f;
                    sway.swayDegrees = 7f;
                    sway.period = 2.2f + (i % 5) * 0.3f;
                    sway.phase = i * 0.37f;
                }
            }
        }

        private const float Course1Layout_WaterBottom = -4f;

        private static void Floodlight(Transform parent, Vector3 position, Vector3 lookAt, ContentAssets assets, ArenaAssets arena)
        {
            var tower = new GameObject("FloodlightTower");
            tower.transform.SetParent(parent, false);
            Vector3 flat = lookAt - position;
            flat.y = 0f;
            tower.transform.SetPositionAndRotation(position, Quaternion.LookRotation(flat.normalized, Vector3.up));

            Primitive(PrimitiveType.Cylinder, "Mast", tower.transform, new Vector3(0f, 12f, 0f), new Vector3(0.9f, 16f, 0.9f),
                assets.Dark, GameLayers.Default, false);
            GameObject lamp = Primitive(PrimitiveType.Cube, "Lamps", tower.transform, new Vector3(0f, 28.5f, 0.6f), new Vector3(7f, 4.5f, 0.6f),
                arena.Floodlight, GameLayers.Default, false);
            lamp.transform.localRotation = Quaternion.Euler(18f, 0f, 0f);
            lamp.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            Primitive(PrimitiveType.Cube, "LampBack", tower.transform, new Vector3(0f, 28.5f, 0.15f), new Vector3(7.4f, 4.9f, 0.5f),
                assets.Dark, GameLayers.Default, false).transform.localRotation = Quaternion.Euler(18f, 0f, 0f);
        }

        /// <summary>Big screen on a pontoon in the middle of the arena, with the game logo on both faces.</summary>
        private static void Jumbotron(Transform parent, Vector3 position, ContentAssets assets, ArenaAssets arena)
        {
            var screen = new GameObject("Jumbotron");
            screen.transform.SetParent(parent, false);
            screen.transform.position = position;
            Transform root = screen.transform;

            Primitive(PrimitiveType.Cylinder, "Pontoon", root, new Vector3(0f, Course1Layout_WaterBottom * 0.5f - 0.6f, 0f),
                new Vector3(15f, 1.6f, 15f), arena.PanelsRainbow, GameLayers.Default, false);
            FoamRing(root, position + Vector3.up * (-2.2f + 0.05f), 22f, arena);
            for (int side = -1; side <= 1; side += 2)
            {
                Primitive(PrimitiveType.Cylinder, "Post", root, new Vector3(side * 6.5f, 2.5f, 0f), new Vector3(0.9f, 4.5f, 0.9f),
                    side < 0 ? arena.PanelsPink : arena.PanelsYellow, GameLayers.Default, false);
            }

            Primitive(PrimitiveType.Cube, "Frame", root, new Vector3(0f, 10.5f, 0f), new Vector3(20f, 8.6f, 0.9f), assets.Dark,
                GameLayers.Default, false);
            Primitive(PrimitiveType.Cube, "Screen", root, new Vector3(0f, 10.5f, 0f), new Vector3(19f, 7.6f, 1.0f), arena.Screen,
                GameLayers.Default, false);
            Primitive(PrimitiveType.Cube, "StripeTop", root, new Vector3(0f, 15.1f, 0f), new Vector3(20.4f, 0.7f, 1.1f), arena.StripesRed,
                GameLayers.Default, false);

            // Readable from the start/finish end (looking +Z) and from the far end (looking -Z).
            WorldLabel(root, "LogoSouth", new Vector3(0f, 10.5f, -0.52f), "SPLASHDOWN", 250, new Color(1f, 0.86f, 0.15f), 18.5f, 7f);
            WorldLabel(root, "LogoNorth", new Vector3(0f, 10.5f, 0.52f), "SPLASHDOWN", 250, new Color(1f, 0.86f, 0.15f), 18.5f, 7f)
                .transform.parent.localRotation = Quaternion.Euler(0f, 180f, 0f);
        }

        /// <summary>Distant hills and clouds so the arena sits in a world instead of floating in a void.</summary>
        private static void Scenery(Transform parent, Vector3 middle, ArenaAssets arena)
        {
            Transform root = Empty("Scenery", parent, Vector3.zero, GameLayers.Default).transform;
            var random = new System.Random(7);

            for (int i = 0; i < 9; i++)
            {
                float angle = i * 40f + (float)random.NextDouble() * 18f;
                float distance = 250f + (float)random.NextDouble() * 60f;
                Vector3 position = middle + Quaternion.Euler(0f, angle, 0f) * Vector3.forward * distance;
                float width = 130f + (float)random.NextDouble() * 80f;
                GameObject hill = Primitive(PrimitiveType.Sphere, "Hill", root, new Vector3(position.x, -14f, position.z),
                    new Vector3(width, 55f + (float)random.NextDouble() * 35f, width), arena.Hill, GameLayers.Default, false);
                NoShadows(hill);
            }

            for (int i = 0; i < 14; i++)
            {
                float angle = (float)random.NextDouble() * 360f;
                float distance = 140f + (float)random.NextDouble() * 220f;
                Vector3 position = middle + Quaternion.Euler(0f, angle, 0f) * Vector3.forward * distance;
                position.y = 75f + (float)random.NextDouble() * 55f;
                Transform cloud = Empty("Cloud", root, position, GameLayers.Default).transform;
                float size = 22f + (float)random.NextDouble() * 20f;
                NoShadows(Primitive(PrimitiveType.Sphere, "Puff", cloud, Vector3.zero, new Vector3(size * 1.6f, size * 0.55f, size), arena.Cloud,
                    GameLayers.Default, false));
                NoShadows(Primitive(PrimitiveType.Sphere, "Puff", cloud, new Vector3(size * 0.7f, -size * 0.08f, size * 0.1f),
                    new Vector3(size * 1.1f, size * 0.42f, size * 0.8f), arena.Cloud, GameLayers.Default, false));
                NoShadows(Primitive(PrimitiveType.Sphere, "Puff", cloud, new Vector3(-size * 0.65f, -size * 0.1f, -size * 0.1f),
                    new Vector3(size * 1.0f, size * 0.38f, size * 0.75f), arena.Cloud, GameLayers.Default, false));
            }
        }

        private static void NoShadows(GameObject go)
        {
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        /// <summary>Bobbing buoys marking the open water inside the U and along the outer lanes.</summary>
        private static void Buoys(Transform parent, ContentAssets assets)
        {
            Transform root = Empty("Buoys", parent, Vector3.zero, GameLayers.Default).transform;
            int index = 0;
            foreach (float x in new[] { -14f, 14f, 62f, 91f })
            {
                for (float z = -10f; z <= 120f; z += 13f)
                {
                    GameObject buoy = Primitive(PrimitiveType.Sphere, "Buoy", root, new Vector3(x, -2.2f + 0.2f, z), Vector3.one * 1.3f,
                        index % 2 == 0 ? assets.Orange : assets.White, GameLayers.Default, false);
                    var bob = buoy.AddComponent<Bobbing>();
                    bob.phase = index * 0.21f;
                    bob.period = 2.6f + (index % 4) * 0.35f;
                    index++;
                }
            }
        }

        /// <summary>Inflatable rails behind and beside the start platform, plus two entrance towers.</summary>
        private static void StartDressing(Transform parent, ContentAssets assets, ArenaAssets arena)
        {
            Transform root = Empty("StartDressing", parent, Vector3.zero, GameLayers.Ground).transform;

            // Solid, so nobody falls off backwards during the countdown.
            CapsuleBar("Rail_Back", root, new Vector3(0f, 0.45f, 0.35f), 12f, 0.7f, arena.PanelsYellow, GameLayers.Ground, true);
            GameObject left = CapsuleBar("Rail_Left", root, new Vector3(-5.65f, 0.45f, 4.2f), 7.5f, 0.7f, arena.PanelsBlue, GameLayers.Ground, true);
            left.transform.localRotation = Quaternion.Euler(0f, 90f, 90f);
            GameObject right = CapsuleBar("Rail_Right", root, new Vector3(5.65f, 0.45f, 4.2f), 7.5f, 0.7f, arena.PanelsBlue, GameLayers.Ground, true);
            right.transform.localRotation = Quaternion.Euler(0f, 90f, 90f);

            for (int side = -1; side <= 1; side += 2)
            {
                Primitive(PrimitiveType.Cylinder, "Tower", root, new Vector3(side * 8.5f, 1.5f, 1.5f), new Vector3(2.2f, 5f, 2.2f),
                    arena.PanelsRainbow, GameLayers.Default, false);
                Primitive(PrimitiveType.Sphere, "TowerBall", root, new Vector3(side * 8.5f, 7.6f, 1.5f), Vector3.one * 3f, arena.BallRed,
                    GameLayers.Default, false);
            }
        }
    }
}
