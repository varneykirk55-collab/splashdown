using BikiniWipeout.Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace BikiniWipeout.EditorTools
{
    /// <summary>
    /// The tropical water park around Course 2: rocky cliffs with waterfalls on three sides, a beach
    /// with pavilions behind the start, a rock island in the middle of the loop, palms, islets and buoys.
    /// Everything is visual only (no colliders) apart from the invisible boundary walls and the start
    /// rails, is marked for static batching, and casts no shadows where it is far from the course.
    /// </summary>
    public static partial class Foundation1Builder
    {
        private const float CoveWater = Course2Layout.WaterLevel;

        private static void BuildCoveEnvironment(Transform parent, ContentAssets assets, ArenaAssets arena, CoveAssets cove, Course2Prefabs c2)
        {
            Transform root = Empty("Environment", parent, Vector3.zero, GameLayers.Default).transform;
            float centerX = (CoveMinX + CoveMaxX) * 0.5f;
            float centerZ = (CoveMinZ + CoveMaxZ) * 0.5f;
            float lengthX = CoveMaxX - CoveMinX;
            float lengthZ = CoveMaxZ - CoveMinZ;
            var random = new System.Random(11);

            // ---- Invisible walls keep the character, ragdoll and balls inside the cove.
            Transform bounds = Empty("Boundaries", root, Vector3.zero, GameLayers.Boundary).transform;
            Wall("Wall_West", bounds, new Vector3(CoveMinX, 5f, centerZ), new Vector3(1f, 50f, lengthZ));
            Wall("Wall_East", bounds, new Vector3(CoveMaxX, 5f, centerZ), new Vector3(1f, 50f, lengthZ));
            Wall("Wall_South", bounds, new Vector3(centerX, 5f, CoveMinZ), new Vector3(lengthX, 50f, 1f));
            Wall("Wall_North", bounds, new Vector3(centerX, 5f, CoveMaxZ), new Vector3(lengthX, 50f, 1f));

            // ---- Cliffs on the west, north and east; the south is left open to the beach and the sky.
            Transform cliffs = Empty("Cliffs", root, Vector3.zero, GameLayers.Default).transform;
            CliffWall(cliffs, new Vector3(CoveMinX - 4f, 0f, CoveMinZ + 10f), new Vector3(CoveMinX - 4f, 0f, CoveMaxZ + 4f), Vector3.right, 15f, 26f, cove, c2, random);
            CliffWall(cliffs, new Vector3(CoveMinX - 4f, 0f, CoveMaxZ + 4f), new Vector3(CoveMaxX + 4f, 0f, CoveMaxZ + 4f), Vector3.back, 18f, 30f, cove, c2, random);
            CliffWall(cliffs, new Vector3(CoveMaxX + 4f, 0f, CoveMaxZ + 4f), new Vector3(CoveMaxX + 4f, 0f, CoveMinZ + 10f), Vector3.left, 12f, 22f, cove, c2, random);

            Transform falls = Empty("Waterfalls", root, Vector3.zero, GameLayers.Default).transform;
            Waterfall(falls, new Vector3(22f, 0f, CoveMaxZ + 2.5f), Vector3.back, 10f, 22f, arena, cove);
            Waterfall(falls, new Vector3(84f, 0f, CoveMaxZ + 2.5f), Vector3.back, 13f, 26f, arena, cove);
            Waterfall(falls, new Vector3(CoveMinX - 2.5f, 0f, 62f), Vector3.right, 9f, 18f, arena, cove);
            Waterfall(falls, new Vector3(CoveMinX - 2.5f, 0f, 128f), Vector3.right, 11f, 21f, arena, cove);
            Waterfall(falls, new Vector3(CoveMaxX + 2.5f, 0f, 96f), Vector3.left, 9f, 15f, arena, cove);

            // ---- Beach behind the start, with pavilions and palms.
            Transform beach = Empty("Beach", root, Vector3.zero, GameLayers.Default).transform;
            NoShadows(Primitive(PrimitiveType.Cube, "Sand", beach, new Vector3(centerX, CoveWater - 0.55f, -46f), new Vector3(lengthX + 60f, 1.5f, 48f),
                cove.Sand, GameLayers.Default, false));
            Pavilion(beach, new Vector3(-14f, CoveWater + 0.2f, -30f), assets, cove);
            Pavilion(beach, new Vector3(34f, CoveWater + 0.2f, -33f), assets, cove);
            Pavilion(beach, new Vector3(82f, CoveWater + 0.2f, -30f), assets, cove);
            for (int i = 0; i < 9; i++)
            {
                float x = CoveMinX + 10f + i * (lengthX - 20f) / 8f + (float)random.NextDouble() * 6f;
                Palm(beach, c2, new Vector3(x, CoveWater + 0.2f, -27f - (float)random.NextDouble() * 10f), random);
            }

            // ---- The rock island in the middle of the loop: a spire with a waterfall facing the start, and the big screen.
            Transform island = Empty("CentreIsland", root, Vector3.zero, GameLayers.Default).transform;
            var middle = new Vector3(50f, 0f, 84f);
            Rock(island, middle + new Vector3(0f, -3.5f, 0f), new Vector3(34f, 9f, 28f), 0f, cove.Rock);
            Rock(island, middle + new Vector3(-9f, -2f, 8f), new Vector3(18f, 10f, 16f), 30f, cove.RockDark);
            Rock(island, middle + new Vector3(10f, -2.5f, -7f), new Vector3(16f, 8f, 18f), -20f, cove.RockDark);
            Primitive(PrimitiveType.Cube, "Spire", island, middle + new Vector3(2f, 6f, 4f), new Vector3(13f, 22f, 11f), cove.Rock,
                GameLayers.Default, false).transform.localRotation = Quaternion.Euler(0f, 12f, 3f);
            NoShadows(Primitive(PrimitiveType.Sphere, "SpireTop", island, middle + new Vector3(2f, 17f, 4f), new Vector3(16f, 5f, 14f), cove.Grass,
                GameLayers.Default, false));
            NoShadows(Primitive(PrimitiveType.Sphere, "Lawn", island, middle + new Vector3(0f, 0.6f, -4f), new Vector3(26f, 2.4f, 18f), cove.Grass,
                GameLayers.Default, false));
            Waterfall(island, middle + new Vector3(1f, 0f, -2.4f), Vector3.back, 7f, 17f, arena, cove);
            for (int i = 0; i < 6; i++)
            {
                Vector3 offset = Quaternion.Euler(0f, i * 60f + 20f, 0f) * Vector3.forward * (9f + (i % 2) * 3f);
                Palm(island, c2, middle + new Vector3(offset.x, 1.2f, offset.z * 0.7f - 3f), random);
            }

            Jumbotron(root, new Vector3(50f, 0f, 46f), assets, arena);

            // ---- Islets with palms and flowers wherever there is open water.
            Transform islets = Empty("Islets", root, Vector3.zero, GameLayers.Default).transform;
            foreach (Vector2 spot in new[]
                     {
                         new Vector2(-22f, 36f), new Vector2(-24f, 96f), new Vector2(-20f, 158f), new Vector2(26f, 172f), new Vector2(66f, 174f),
                         new Vector2(118f, 160f), new Vector2(122f, 70f), new Vector2(120f, 28f), new Vector2(110f, -10f), new Vector2(58f, -8f),
                         new Vector2(24f, 58f), new Vector2(76f, 118f), new Vector2(26f, 122f), new Vector2(76f, 44f), new Vector2(20f, 36f),
                         new Vector2(-18f, -12f), new Vector2(46f, 126f), new Vector2(80f, 76f)
                     })
            {
                if (!NearCourse(spot.x, spot.y, 9f))
                {
                    Islet(islets, new Vector3(spot.x, 0f, spot.y), arena, cove, c2, random);
                }
            }

            // ---- Buoys in the open water.
            Transform buoys = Empty("Buoys", root, Vector3.zero, GameLayers.Default).transform;
            int buoyIndex = 0;
            for (float x = CoveMinX + 12f; x < CoveMaxX - 8f; x += 19f)
            {
                for (float z = CoveMinZ + 14f; z < CoveMaxZ - 8f; z += 23f)
                {
                    float bx = x + (buoyIndex % 3) * 3f;
                    if (NearCourse(bx, z, 5f) || (new Vector2(bx, z) - new Vector2(middle.x, middle.z)).magnitude < 24f)
                    {
                        continue;
                    }

                    GameObject buoy = Primitive(PrimitiveType.Sphere, "Buoy", buoys, new Vector3(bx, CoveWater + 0.2f, z), Vector3.one * 1.3f,
                        buoyIndex % 2 == 0 ? assets.Orange : assets.White, GameLayers.Default, false);
                    NoShadows(buoy);
                    var bob = buoy.AddComponent<Bobbing>();
                    bob.phase = buoyIndex * 0.21f;
                    bob.period = 2.6f + (buoyIndex % 4) * 0.35f;
                    buoyIndex++;
                }
            }

            SlideTower(root, assets, cove);
            FinishDressing(root, assets, arena, cove);
            Scenery(root, new Vector3(centerX, 0f, centerZ), arena);
            StartDressing(root, assets, arena);
            MarkStatic(root);
        }

        /// <summary>True if a world point is within <paramref name="margin"/> metres of any course surface (anywhere along a mover's path).</summary>
        private static bool NearCourse(float x, float z, float margin)
        {
            foreach (CourseSurface surface in Course2Layout.Surfaces)
            {
                for (int step = 0; step <= 1; step++)
                {
                    CourseLayoutMath.WorldCenter(Course2Layout.Legs, surface, step, out float cx, out float cz);
                    float halfX, halfZ;
                    if (surface.Shape == SurfaceShape.Disc)
                    {
                        halfX = halfZ = Mathf.Max(surface.Radius, surface.BodyRadius);
                    }
                    else
                    {
                        CourseLayoutMath.WorldHalfExtents(Course2Layout.Legs, surface, out halfX, out halfZ);
                    }

                    if (Mathf.Abs(x - cx) < halfX + margin && Mathf.Abs(z - cz) < halfZ + margin)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static GameObject Rock(Transform parent, Vector3 position, Vector3 scale, float yaw, Material material)
        {
            GameObject rock = Primitive(PrimitiveType.Sphere, "Rock", parent, position, scale, material, GameLayers.Default, false);
            rock.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            return rock;
        }

        /// <summary>A run of blocky cliff from <paramref name="from"/> to <paramref name="to"/>, facing <paramref name="inward"/>.</summary>
        private static void CliffWall(Transform parent, Vector3 from, Vector3 to, Vector3 inward, float minHeight, float maxHeight,
            CoveAssets cove, Course2Prefabs c2, System.Random random)
        {
            const float spacing = 15f;
            const float bottom = -6f;
            float length = Vector3.Distance(from, to);
            int count = Mathf.Max(1, Mathf.CeilToInt(length / spacing));
            Vector3 along = (to - from).normalized;
            float baseYaw = Quaternion.LookRotation(inward, Vector3.up).eulerAngles.y;

            for (int i = 0; i <= count; i++)
            {
                float height = Mathf.Lerp(minHeight, maxHeight, (float)random.NextDouble());
                float depth = 13f + (float)random.NextDouble() * 6f;
                Vector3 foot = from + along * (i * length / count) - inward * (depth * 0.5f - 1.5f + (float)random.NextDouble() * 2f);
                Quaternion rotation = Quaternion.Euler((float)random.NextDouble() * 4f - 2f, baseYaw + (float)random.NextDouble() * 16f - 8f, 0f);

                GameObject block = Primitive(PrimitiveType.Cube, "Cliff", parent, new Vector3(foot.x, bottom + height * 0.5f, foot.z),
                    new Vector3(spacing + 4f, height, depth), i % 2 == 0 ? cove.Rock : cove.RockDark, GameLayers.Default, false);
                block.transform.localRotation = rotation;
                block.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;

                NoShadows(Primitive(PrimitiveType.Sphere, "Top", parent, new Vector3(foot.x, bottom + height, foot.z),
                    new Vector3(spacing + 7f, 4f, depth + 3f), cove.Grass, GameLayers.Default, false));

                Vector3 boulder = foot + inward * (depth * 0.5f + 1f + (float)random.NextDouble() * 3f) + along * ((float)random.NextDouble() * 8f - 4f);
                float size = 4f + (float)random.NextDouble() * 5f;
                NoShadows(Rock(parent, new Vector3(boulder.x, CoveWater, boulder.z), new Vector3(size * 1.3f, size, size),
                    (float)random.NextDouble() * 180f, i % 2 == 0 ? cove.RockDark : cove.Rock));

                if (i % 3 == 1)
                {
                    Palm(parent, c2, new Vector3(foot.x + inward.x * 3f, bottom + height + 1.2f, foot.z + inward.z * 3f), random);
                }
            }
        }

        /// <summary>A sheet of falling water on a rock face, with foam and a little mist where it lands.</summary>
        private static void Waterfall(Transform parent, Vector3 foot, Vector3 inward, float width, float height, ArenaAssets arena, CoveAssets cove)
        {
            var fall = new GameObject("Waterfall");
            fall.transform.SetParent(parent, false);
            fall.transform.SetPositionAndRotation(new Vector3(foot.x, CoveWater, foot.z) + inward * 1.2f, Quaternion.LookRotation(inward, Vector3.up));
            Transform root = fall.transform;

            NoShadows(Primitive(PrimitiveType.Cube, "Ledge", root, new Vector3(0f, height + 0.6f, -1.6f), new Vector3(width + 5f, 2.6f, 5f),
                cove.RockDark, GameLayers.Default, false));
            GameObject sheet = Primitive(PrimitiveType.Cube, "Water", root, new Vector3(0f, height * 0.5f, 0f), new Vector3(width, height, 0.6f),
                cove.Waterfall, GameLayers.Default, false);
            NoShadows(sheet);
            var flow = sheet.AddComponent<ScrollingSurface>();
            flow.tiling = new Vector2(Mathf.Max(1f, width / 5f), Mathf.Max(1f, height / 7f));
            flow.scrollSpeed = new Vector2(0f, 0.65f);
            flow.wobble = 0.004f;

            FoamRing(root, root.TransformPoint(new Vector3(0f, 0.05f, 2f)), width * 1.6f, arena);

            // Soft, slow mist: a couple of dozen large particles per waterfall at most.
            ParticleSystem mist = WaterSpray("Mist", root, new Vector3(0f, 0.4f, 1.2f), SprayUp, 9f, 0.8f, 2.4f, 1.3f, 2f, 1.6f, 3.2f,
                38f, width * 0.4f, 0.12f, 26, true, arena.Star);
            ParticleSystem.MainModule main = mist.main;
            main.playOnAwake = true;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 1f, 1f, 0.5f), new Color(0.8f, 0.95f, 1f, 0.25f));
        }

        private static void Palm(Transform parent, Course2Prefabs c2, Vector3 position, System.Random random)
        {
            GameObject palm = InstantiatePrefab(c2.Palm, parent, position, Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f));
            palm.transform.localScale = Vector3.one * (0.9f + (float)random.NextDouble() * 0.5f);
            RecordOverrides(palm.transform);
        }

        /// <summary>A little rock island with a palm and a few flowers.</summary>
        private static void Islet(Transform parent, Vector3 position, ArenaAssets arena, CoveAssets cove, Course2Prefabs c2, System.Random random)
        {
            var islet = new GameObject("Islet");
            islet.transform.SetParent(parent, false);
            islet.transform.position = position;
            Transform root = islet.transform;

            float size = 6f + (float)random.NextDouble() * 4f;
            NoShadows(Rock(root, new Vector3(0f, CoveWater - 0.3f, 0f), new Vector3(size * 1.3f, 3.4f, size), (float)random.NextDouble() * 180f, cove.Rock));
            NoShadows(Rock(root, new Vector3(size * 0.35f, CoveWater + 0.2f, -size * 0.2f), new Vector3(size * 0.5f, 2.6f, size * 0.45f),
                (float)random.NextDouble() * 180f, cove.RockDark));
            NoShadows(Primitive(PrimitiveType.Sphere, "Sand", root, new Vector3(-0.5f, CoveWater + 0.75f, 0.4f), new Vector3(size * 0.8f, 1.2f, size * 0.65f),
                cove.Sand, GameLayers.Default, false));
            FoamRing(root, position + Vector3.up * (CoveWater + 0.04f), size * 1.9f, arena);
            Palm(root, c2, position + new Vector3(-0.6f, CoveWater + 1.1f, 0.5f), random);

            var flowers = new GameObject("Flowers");
            flowers.transform.SetParent(root, false);
            for (int i = 0; i < 4; i++)
            {
                Vector3 offset = Quaternion.Euler(0f, i * 90f + 30f, 0f) * Vector3.forward * (size * 0.28f);
                NoShadows(Primitive(PrimitiveType.Sphere, "Flower", flowers.transform, new Vector3(offset.x, CoveWater + 1.35f, offset.z),
                    new Vector3(0.7f, 0.3f, 0.7f), cove.Flower, GameLayers.Default, false));
            }

            CullWhenSmall(flowers, 0.03f);
        }

        /// <summary>Open-sided beach hut with a striped roof.</summary>
        private static void Pavilion(Transform parent, Vector3 position, ContentAssets assets, CoveAssets cove)
        {
            var hut = new GameObject("Pavilion");
            hut.transform.SetParent(parent, false);
            hut.transform.position = position;
            Transform root = hut.transform;

            Primitive(PrimitiveType.Cylinder, "Deck", root, new Vector3(0f, 0.2f, 0f), new Vector3(9f, 0.2f, 9f), cove.DeckBlue, GameLayers.Default, false);
            for (int i = 0; i < 4; i++)
            {
                Vector3 corner = Quaternion.Euler(0f, 45f + i * 90f, 0f) * Vector3.forward * 3.6f;
                Primitive(PrimitiveType.Cylinder, "Post" + i, root, new Vector3(corner.x, 2.6f, corner.z), new Vector3(0.35f, 2.4f, 0.35f),
                    assets.Yellow, GameLayers.Default, false);
            }

            Primitive(PrimitiveType.Sphere, "Roof", root, new Vector3(0f, 5.2f, 0f), new Vector3(10.5f, 3.6f, 10.5f), cove.RoofStripes,
                GameLayers.Default, false);
            Primitive(PrimitiveType.Sphere, "Finial", root, new Vector3(0f, 7.2f, 0f), Vector3.one * 0.9f, assets.HotPink, GameLayers.Default, false);
        }

        /// <summary>Striped canopy over the top of the water slide, so the tower is a landmark from across the course.</summary>
        private static void SlideTower(Transform parent, ContentAssets assets, CoveAssets cove)
        {
            CourseSurface top = Course2Layout.Find("D_SlideTop");
            if (top == null)
            {
                return;
            }

            LegFrame frame = Frame2(top.Leg);
            var tower = new GameObject("SlideTower");
            tower.transform.SetParent(parent, false);
            tower.transform.SetPositionAndRotation(frame.Point(top.X, top.Height, top.Z), frame.Rotation);
            Transform root = tower.transform;

            for (int i = 0; i < 4; i++)
            {
                float x = (i % 2 == 0 ? -1f : 1f) * (top.Width * 0.5f - 0.25f);
                float z = (i < 2 ? -1f : 1f) * (top.Length * 0.5f - 0.25f);
                Primitive(PrimitiveType.Cylinder, "Post" + i, root, new Vector3(x, 1.9f, z), new Vector3(0.3f, 1.9f, 0.3f), assets.Yellow,
                    GameLayers.Default, false);
            }

            Primitive(PrimitiveType.Sphere, "Canopy", root, new Vector3(0f, 4.4f, 0f), new Vector3(top.Width + 2.4f, 3f, top.Length + 2.4f),
                cove.RoofStripes, GameLayers.Default, false);
            Primitive(PrimitiveType.Cylinder, "FlagPole", root, new Vector3(0f, 6.9f, 0f), new Vector3(0.12f, 1.4f, 0.12f), assets.White,
                GameLayers.Default, false);
            GameObject flag = Primitive(PrimitiveType.Cube, "Flag", root, new Vector3(0.8f, 7.8f, 0f), new Vector3(1.6f, 0.9f, 0.05f), assets.HotPink,
                GameLayers.Default, false);
            var sway = flag.AddComponent<Bobbing>();
            sway.height = 0f;
            sway.swayDegrees = 7f;
            sway.period = 2.4f;
        }

        /// <summary>Inflatable towers and pennants around the finish platform, so the end of the course is visible from the Big Balls.</summary>
        private static void FinishDressing(Transform parent, ContentAssets assets, ArenaAssets arena, CoveAssets cove)
        {
            CourseSurface finish = Course2Layout.Find("FinishPlatform");
            if (finish == null)
            {
                return;
            }

            LegFrame frame = Frame2(finish.Leg);
            var dressing = new GameObject("FinishDressing");
            dressing.transform.SetParent(parent, false);
            dressing.transform.SetPositionAndRotation(frame.Point(finish.X, finish.Height, finish.Z), frame.Rotation);
            Transform root = dressing.transform;

            float farEdge = finish.Length * 0.5f;
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * (finish.Width * 0.5f + 2.2f);
                Primitive(PrimitiveType.Cylinder, "Tower", root, new Vector3(x, 1.5f, farEdge - 1f), new Vector3(2.2f, 6f, 2.2f),
                    cove.StripesPink, GameLayers.Default, false);
                Primitive(PrimitiveType.Sphere, "TowerBall", root, new Vector3(x, 8.6f, farEdge - 1f), Vector3.one * 3f, arena.BallRed,
                    GameLayers.Default, false);

                for (int i = 0; i < 3; i++)
                {
                    float z = -farEdge + 2f + i * 4f;
                    Primitive(PrimitiveType.Cylinder, "Pole", root, new Vector3(x - side * 1.6f, 2f, z), new Vector3(0.12f, 2f, 0.12f),
                        assets.White, GameLayers.Default, false);
                    GameObject pennant = Primitive(PrimitiveType.Cube, "Pennant", root, new Vector3(x - side * 1.6f, 3.6f, z + 0.6f),
                        new Vector3(0.05f, 0.7f, 1.2f), i % 2 == 0 ? assets.HotPink : assets.Yellow, GameLayers.Default, false);
                    var sway = pennant.AddComponent<Bobbing>();
                    sway.height = 0f;
                    sway.swayDegrees = 6f;
                    sway.period = 2f + i * 0.3f;
                    sway.phase = i * 0.3f + (side > 0 ? 0.5f : 0f);
                }
            }

            // A back rail (solid) so a celebrating player does not run straight off the end.
            CapsuleBar("Rail_End", root, new Vector3(0f, 0.45f, farEdge - 0.35f), finish.Width, 0.7f, arena.PanelsYellow,
                GameLayers.Ground, true);
        }

        /// <summary>Flags plain scenery for static batching. Anything animated by a script, a particle system or an LOD group is left alone.</summary>
        private static void MarkStatic(Transform root)
        {
            foreach (MeshRenderer renderer in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                GameObject go = renderer.gameObject;
                if (go.GetComponentInParent<MonoBehaviour>() != null || go.GetComponentInParent<LODGroup>() != null
                    || go.GetComponentInParent<ParticleSystem>() != null || PrefabUtility.IsPartOfPrefabInstance(go))
                {
                    continue;
                }

                GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            }
        }
    }
}
