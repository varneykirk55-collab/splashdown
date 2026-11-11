using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace BikiniWipeout.EditorTools
{
    public static partial class Foundation1Builder
    {
        /// <summary>Height of a swinging ball's pivot above the deck it guards (rod + ball radius + clearance).</summary>
        internal const float SwingPivotHeight = SwingRodLength + SwingBallRadius + 0.15f;

        private const float SwingRodLength = 6f;
        private const float SwingBallRadius = 1.1f;
        private const float BigBallRadius = 2f;
        private const float CollapseSegmentLength = 2.2f;

        /// <summary>Reusable Course 2 prefabs, one per obstacle type on the approved sheet.</summary>
        internal sealed class Course2Prefabs
        {
            public GameObject Mover, Sweeper, SpinningArms, GiantArms, DoubleSweeper, SwingingBall, Geyser, SideJet, Bucket;
            public GameObject SeesawWide, SeesawNarrow, BouncePad, CollapseSegment, Arena, BigBall, CheckpointGate, Palm;
        }

        private static Course2Prefabs BuildCourse2Prefabs(ContentAssets assets, ArenaAssets arena, CoveAssets cove)
        {
            string folder = Course2PrefabFolder + "/";
            var set = new Course2Prefabs();

            // 02
            set.Mover = GetOrBuildPrefab(folder + "CoveMovingPlatform.prefab", () => CreateCoveMover(assets, cove));

            // 01 and 08: name, reach, low bars, high bars, bar material, hub material, speed (deg/s), impact force
            set.Sweeper = GetOrBuildPrefab(folder + "CoveSweeper.prefab",
                () => CreateSweeperVariant("CoveSweeper", 3.8f, 2, false, cove.StripesPink, assets.Yellow, 58f, 1.4f));
            set.DoubleSweeper = GetOrBuildPrefab(folder + "CoveDoubleSweeper.prefab",
                () => CreateSweeperVariant("CoveDoubleSweeper", 7.2f, 2, true, cove.StripesPink, cove.PillarBlue, 80f, 1.7f));
            set.SpinningArms = GetOrBuildPrefab(folder + "SpinningArms.prefab",
                () => CreateSweeperVariant("SpinningArms", 2.9f, 4, false, cove.StripesPink, cove.PillarBlue, 80f, 1.5f));
            set.GiantArms = GetOrBuildPrefab(folder + "GiantArms.prefab",
                () => CreateSweeperVariant("GiantArms", 5.5f, 4, false, cove.StripesPink, assets.Yellow, 60f, 1.6f));

            set.SwingingBall = GetOrBuildPrefab(folder + "SwingingBall.prefab", () => CreateSwingingBall(assets, arena, cove));   // 10
            set.Geyser = GetOrBuildPrefab(folder + "WaterGeyser.prefab", () => CreateGeyser(assets, cove));                       // 05
            set.SideJet = GetOrBuildPrefab(folder + "WaterSideJet.prefab", () => CreateSideJet(assets, cove));                    // 05
            set.Bucket = GetOrBuildPrefab(folder + "TippingBucket.prefab", () => CreateTippingBucket(assets, arena, cove));       // 06
            set.SeesawWide = GetOrBuildPrefab(folder + "SeesawWide.prefab", () => CreateSeesaw("SeesawWide", 4f, 9f, assets, cove)); // 03
            set.SeesawNarrow = GetOrBuildPrefab(folder + "SeesawNarrow.prefab", () => CreateSeesaw("SeesawNarrow", 3.4f, 9f, assets, cove));
            set.BouncePad = GetOrBuildPrefab(folder + "BouncePad.prefab", () => CreateBouncePad(assets, cove));                   // 04
            set.CollapseSegment = GetOrBuildPrefab(folder + "CollapsingBridgeSegment.prefab", () => CreateCollapseSegment(assets)); // 09
            set.Arena = GetOrBuildPrefab(folder + "FloatingArena.prefab", () => CreateFloatingArena(assets, arena, cove));        // 11
            set.CheckpointGate = GetOrBuildPrefab(folder + "CheckpointGate.prefab", () => CreateCheckpointGate(assets, arena));   // 12
            set.BigBall = GetOrBuildPrefab(folder + "BigBall.prefab", () => CreateBigBall(assets, arena, cove));                  // 13
            set.Palm = GetOrBuildPrefab(folder + "PalmTree.prefab", () => CreatePalm(cove));
            return set;
        }

        // ------------------------------------------------------------------ helpers

        private static Rigidbody KinematicBody(GameObject go)
        {
            var body = go.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            return body;
        }

        /// <summary>Cylinder primitive with a true (convex mesh) collider instead of the default capsule.</summary>
        private static GameObject SolidCylinder(string name, Transform parent, Vector3 center, float radius, float height,
            Material material, int layer)
        {
            GameObject cylinder = Primitive(PrimitiveType.Cylinder, name, parent, center,
                new Vector3(radius * 2f, height * 0.5f, radius * 2f), material, layer, false);
            var collider = cylinder.AddComponent<MeshCollider>();
            collider.sharedMesh = cylinder.GetComponent<MeshFilter>().sharedMesh;
            collider.convex = true;
            return cylinder;
        }

        /// <summary>
        /// Looping water spray. The cone emits along the object's local +Z. Particle counts are capped
        /// low on purpose: there are many jets in the course and only a few fire at once.
        /// </summary>
        private static ParticleSystem WaterSpray(string name, Transform parent, Vector3 localPosition, Quaternion localRotation,
            float rate, float speedMin, float speedMax, float lifeMin, float lifeMax, float sizeMin, float sizeMax,
            float coneAngle, float coneRadius, float gravity, int maxParticles, bool loop, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = localRotation;

            var particles = go.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = particles.main;
            main.duration = loop ? 1f : 0.8f;
            main.loop = loop;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifeMin, lifeMax);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speedMin, speedMax);
            main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
            main.startColor = new ParticleSystem.MinMaxGradient(Color.white, new Color(0.62f, 0.88f, 1f, 1f));
            main.gravityModifier = gravity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = maxParticles;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = rate;

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = coneAngle;
            shape.radius = coneRadius;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return particles;
        }

        private static readonly Quaternion SprayUp = Quaternion.Euler(-90f, 0f, 0f);
        private static readonly Quaternion SprayDown = Quaternion.Euler(90f, 0f, 0f);

        /// <summary>Hides a group of small props beyond a distance, so they cost nothing when far away.</summary>
        private static void CullWhenSmall(GameObject root, float screenHeight)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                return;
            }

            var group = root.AddComponent<LODGroup>();
            group.SetLODs(new[] { new LOD(screenHeight, renderers) });
            group.RecalculateBounds();
        }

        // ------------------------------------------------------------------ 02 moving platform

        /// <summary>Course 2 skin of the moving platform (same MovingPlatform component as Foundation 1). 4 x 4 m.</summary>
        private static GameObject CreateCoveMover(ContentAssets assets, CoveAssets cove)
        {
            var root = new GameObject("CoveMovingPlatform") { layer = GameLayers.MovingPlatform };
            Primitive(PrimitiveType.Cube, "Deck", root.transform, new Vector3(0f, -0.25f, 0f), new Vector3(4f, 0.5f, 4f),
                assets.HotPink, GameLayers.MovingPlatform, true);
            Primitive(PrimitiveType.Cube, "Trim", root.transform, new Vector3(0f, -0.3f, 0f), new Vector3(4.3f, 0.3f, 4.3f),
                assets.Yellow, GameLayers.MovingPlatform, false);
            Primitive(PrimitiveType.Cube, "Stripe", root.transform, new Vector3(0f, 0.006f, 0f), new Vector3(3.4f, 0.012f, 1.2f),
                assets.Yellow, GameLayers.MovingPlatform, false);
            for (int side = -1; side <= 1; side += 2)
            {
                // Pontoons, so the platform reads as floating on rails.
                GameObject pontoon = CapsuleBar(side < 0 ? "PontoonLeft" : "PontoonRight", root.transform,
                    new Vector3(side * 1.5f, -0.85f, 0f), 4f, 0.8f, cove.PillarBlue, GameLayers.MovingPlatform, false);
                pontoon.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }

            KinematicBody(root);
            root.AddComponent<MovingPlatform>();
            return root;
        }

        // ------------------------------------------------------------------ 10 swinging ball

        /// <summary>Large pendulum on a gantry, built on the Course 1 <see cref="Pendulum"/> component. The prefab origin is the pivot.</summary>
        private static GameObject CreateSwingingBall(ContentAssets assets, ArenaAssets arena, CoveAssets cove)
        {
            const float postSpan = 5.4f;
            const float postLength = 11f;

            var root = new GameObject("SwingingBall") { layer = GameLayers.Obstacle };
            for (int side = -1; side <= 1; side += 2)
            {
                Primitive(PrimitiveType.Cylinder, side < 0 ? "PostLeft" : "PostRight", root.transform,
                    new Vector3(side * postSpan, -postLength * 0.5f + 0.4f, 0f), new Vector3(0.55f, postLength * 0.5f, 0.55f),
                    cove.PillarBlue, GameLayers.Default, false);
                Primitive(PrimitiveType.Sphere, side < 0 ? "CapLeft" : "CapRight", root.transform,
                    new Vector3(side * postSpan, 0.5f, 0f), Vector3.one * 0.9f, assets.Yellow, GameLayers.Default, false);
            }

            CapsuleBar("Crossbar", root.transform, new Vector3(0f, 0.4f, 0f), postSpan * 2f + 0.6f, 0.6f, cove.StripesPink,
                GameLayers.Default, false);

            GameObject arm = Empty("Arm", root.transform, Vector3.zero, GameLayers.Obstacle);
            Rigidbody body = KinematicBody(arm);
            Primitive(PrimitiveType.Cylinder, "Rod", arm.transform, new Vector3(0f, -SwingRodLength * 0.5f, 0f),
                new Vector3(0.18f, SwingRodLength * 0.5f, 0.18f), assets.Orange, GameLayers.Obstacle, false);
            GameObject bob = Primitive(PrimitiveType.Sphere, "Ball", arm.transform, new Vector3(0f, -SwingRodLength, 0f),
                Vector3.one * (SwingBallRadius * 2f), assets.HotPink, GameLayers.Obstacle, true);
            Primitive(PrimitiveType.Cylinder, "Band", bob.transform, Vector3.zero, new Vector3(1.01f, 0.1f, 1.01f), assets.Yellow,
                GameLayers.Obstacle, false);

            GameObject zone = Empty("ImpactZone", arm.transform, new Vector3(0f, -SwingRodLength, 0f), GameLayers.Obstacle);
            var sphere = zone.AddComponent<SphereCollider>();
            sphere.isTrigger = true;
            sphere.radius = SwingBallRadius + 0.1f;

            var impact = arm.AddComponent<ImpactSource>();
            impact.kind = ImpactSourceKind.Generic;
            impact.body = body;

            var whoosh = arm.AddComponent<ProximityWhoosh>();
            whoosh.tip = bob.transform;
            whoosh.radius = 5f;

            var pendulum = root.AddComponent<Pendulum>();
            pendulum.armBody = body;
            pendulum.impactSource = impact;
            pendulum.amplitude = 40f;
            pendulum.impactForce = 1.5f;
            impact.forceMultiplier = pendulum.impactForce;
            return root;
        }

        // ------------------------------------------------------------------ 05 water jets

        /// <summary>Vertical jet set into the deck. The prefab origin is the nozzle, at deck level.</summary>
        private static GameObject CreateGeyser(ContentAssets assets, CoveAssets cove)
        {
            var root = new GameObject("WaterGeyser");
            Primitive(PrimitiveType.Cylinder, "Ring", root.transform, new Vector3(0f, 0.005f, 0f), new Vector3(2f, 0.012f, 2f),
                assets.HotPink, GameLayers.Default, false);
            Primitive(PrimitiveType.Cylinder, "Nozzle", root.transform, new Vector3(0f, 0.012f, 0f), new Vector3(1.1f, 0.016f, 1.1f),
                cove.PillarBlue, GameLayers.Default, false);

            var jet = root.AddComponent<WaterJet>();
            jet.mode = WaterJetMode.Geyser;
            jet.radius = 1f;
            jet.range = 4.5f;
            jet.warningEffect = WaterSpray("Warning", root.transform, new Vector3(0f, 0.05f, 0f), SprayUp,
                26f, 1.2f, 2.4f, 0.25f, 0.45f, 0.12f, 0.3f, 18f, 0.4f, 1.2f, 24, true, assets.Splash);
            jet.activeEffect = WaterSpray("Jet", root.transform, new Vector3(0f, 0.05f, 0f), SprayUp,
                80f, 10f, 13f, 0.5f, 0.75f, 0.22f, 0.5f, 5f, 0.35f, 1.5f, 90, true, assets.Splash);
            return root;
        }

        /// <summary>Horizontal jet on a post standing in the water beside the path. The prefab origin is the nozzle; it sprays along +Z.</summary>
        private static GameObject CreateSideJet(ContentAssets assets, CoveAssets cove)
        {
            var root = new GameObject("WaterSideJet");
            Primitive(PrimitiveType.Cylinder, "Post", root.transform, new Vector3(0f, -2.4f, -0.35f), new Vector3(0.6f, 2.6f, 0.6f),
                cove.PillarBlue, GameLayers.Default, false);
            Primitive(PrimitiveType.Sphere, "Head", root.transform, new Vector3(0f, 0f, -0.35f), Vector3.one * 1.1f, assets.Yellow,
                GameLayers.Default, false);
            GameObject barrel = Primitive(PrimitiveType.Cylinder, "Barrel", root.transform, new Vector3(0f, 0f, 0.1f),
                new Vector3(0.5f, 0.45f, 0.5f), assets.HotPink, GameLayers.Default, false);
            barrel.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            var jet = root.AddComponent<WaterJet>();
            jet.mode = WaterJetMode.Push;
            jet.radius = 0.95f;
            jet.range = 6.5f;
            jet.period = 4f;
            jet.activeTime = 1.3f;
            jet.warningTime = 0.8f;
            jet.warningEffect = WaterSpray("Warning", root.transform, new Vector3(0f, 0f, 0.6f), Quaternion.identity,
                22f, 1f, 2f, 0.25f, 0.4f, 0.1f, 0.25f, 14f, 0.15f, 1.5f, 20, true, assets.Splash);
            jet.activeEffect = WaterSpray("Jet", root.transform, new Vector3(0f, 0f, 0.6f), Quaternion.identity,
                80f, 11f, 14f, 0.42f, 0.55f, 0.25f, 0.55f, 4f, 0.2f, 0.25f, 90, true, assets.Splash);
            return root;
        }

        // ------------------------------------------------------------------ 06 tipping bucket

        /// <summary>Bucket on a gantry across the path. The prefab origin is on the deck under the bucket; local +Z is the path.</summary>
        private static GameObject CreateTippingBucket(ContentAssets assets, ArenaAssets arena, CoveAssets cove)
        {
            const float span = 3.1f;
            const float beamHeight = 4.7f;

            var root = new GameObject("TippingBucket");
            for (int side = -1; side <= 1; side += 2)
            {
                Primitive(PrimitiveType.Cylinder, side < 0 ? "PostLeft" : "PostRight", root.transform,
                    new Vector3(side * span, beamHeight * 0.5f - 3f, 0f), new Vector3(0.5f, beamHeight * 0.5f + 3f, 0.5f),
                    side < 0 ? assets.HotPink : cove.PillarBlue, GameLayers.Default, false);
            }

            CapsuleBar("Beam", root.transform, new Vector3(0f, beamHeight, 0f), span * 2f + 0.5f, 0.5f, cove.StripesPink,
                GameLayers.Default, false);

            // The bucket hangs from its pivot, so it tips around its rim like the real thing.
            Transform pivot = Empty("Bucket", root.transform, new Vector3(0f, beamHeight - 0.45f, 0f), GameLayers.Default).transform;
            Primitive(PrimitiveType.Cylinder, "Body", pivot, new Vector3(0f, -0.6f, 0f), new Vector3(2f, 0.6f, 2f), cove.BucketYellow,
                GameLayers.Default, false);
            Primitive(PrimitiveType.Cylinder, "Rim", pivot, new Vector3(0f, -0.04f, 0f), new Vector3(2.2f, 0.07f, 2.2f), assets.HotPink,
                GameLayers.Default, false);
            Primitive(PrimitiveType.Cylinder, "Water", pivot, new Vector3(0f, 0.02f, 0f), new Vector3(1.85f, 0.02f, 1.85f), assets.Turquoise,
                GameLayers.Default, false);
            Primitive(PrimitiveType.Cylinder, "Band", pivot, new Vector3(0f, -0.75f, 0f), new Vector3(2.03f, 0.12f, 2.03f), cove.PillarBlue,
                GameLayers.Default, false);

            var bucket = root.AddComponent<TippingBucket>();
            bucket.bucket = pivot;
            bucket.waterEffect = WaterSpray("Water", root.transform, new Vector3(0f, beamHeight - 1.2f, 0f), SprayDown,
                130f, 6f, 9f, 0.45f, 0.65f, 0.3f, 0.65f, 16f, 0.7f, 1.6f, 110, false, assets.Splash);
            return root;
        }

        // ------------------------------------------------------------------ 03 seesaw

        /// <summary>Seesaw plank on a fulcrum. The prefab origin is the centre of the plank's top face (the pivot).</summary>
        private static GameObject CreateSeesaw(string name, float width, float length, ContentAssets assets, CoveAssets cove)
        {
            var root = new GameObject(name) { layer = GameLayers.MovingPlatform };
            GameObject axle = Primitive(PrimitiveType.Cylinder, "Fulcrum", root.transform, new Vector3(0f, -0.95f, 0f),
                new Vector3(0.9f, width * 0.5f + 0.3f, 0.9f), assets.HotPink, GameLayers.Default, false);
            axle.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            Primitive(PrimitiveType.Cylinder, "Base", root.transform, new Vector3(0f, -1.55f, 0f), new Vector3(3.2f, 0.25f, 3.2f),
                assets.HotPink, GameLayers.Default, false);
            Primitive(PrimitiveType.Cylinder, "Support", root.transform, new Vector3(0f, -6f, 0f), new Vector3(1.6f, 4.3f, 1.6f),
                cove.PillarBlue, GameLayers.Default, false);

            // The Rigidbody object is unscaled so riders are not distorted; only the plank mesh is scaled.
            GameObject deck = Empty("Deck", root.transform, Vector3.zero, GameLayers.MovingPlatform);
            Rigidbody body = KinematicBody(deck);
            Primitive(PrimitiveType.Cube, "Plank", deck.transform, new Vector3(0f, -0.25f, 0f), new Vector3(width, 0.5f, length),
                cove.DeckBlue, GameLayers.MovingPlatform, true);
            Primitive(PrimitiveType.Cube, "Trim", deck.transform, new Vector3(0f, -0.3f, 0f), new Vector3(width + 0.3f, 0.3f, length + 0.3f),
                assets.Yellow, GameLayers.MovingPlatform, false);
            for (int end = -1; end <= 1; end += 2)
            {
                Primitive(PrimitiveType.Cube, end < 0 ? "MarkNear" : "MarkFar", deck.transform,
                    new Vector3(0f, 0.006f, end * (length * 0.5f - 0.7f)), new Vector3(width - 0.4f, 0.012f, 0.9f), assets.HotPink,
                    GameLayers.MovingPlatform, false);
            }

            var tilt = root.AddComponent<TiltingPlatform>();
            tilt.body = body;
            tilt.mode = TiltMode.Seesaw;
            tilt.maxTilt = 9f;
            tilt.halfLength = length * 0.5f;
            return root;
        }

        // ------------------------------------------------------------------ 04 bounce pad

        /// <summary>Spring pad. The prefab origin is the centre of the pad's top face. Radius 1.4 m.</summary>
        private static GameObject CreateBouncePad(ContentAssets assets, CoveAssets cove)
        {
            const float radius = 1.4f;
            var root = new GameObject("BouncePad") { layer = GameLayers.Ground };

            // Invisible solid top: it never moves, so standing and landing on it are reliable.
            GameObject solid = SolidCylinder("Solid", root.transform, new Vector3(0f, -0.15f, 0f), radius, 0.3f, null, GameLayers.Ground);
            Object.DestroyImmediate(solid.GetComponent<MeshRenderer>());
            Object.DestroyImmediate(solid.GetComponent<MeshFilter>());

            // The visible pad squashes around its base when it fires.
            Transform visual = Empty("Visual", root.transform, new Vector3(0f, -0.32f, 0f), GameLayers.Default).transform;
            Primitive(PrimitiveType.Cylinder, "Top", visual, new Vector3(0f, 0.16f, 0f), new Vector3(radius * 2f, 0.16f, radius * 2f),
                assets.HotPink, GameLayers.Default, false);
            Primitive(PrimitiveType.Cylinder, "Rim", visual, new Vector3(0f, 0.1f, 0f), new Vector3(radius * 2f + 0.3f, 0.09f, radius * 2f + 0.3f),
                assets.Yellow, GameLayers.Default, false);
            Primitive(PrimitiveType.Cylinder, "Target", visual, new Vector3(0f, 0.325f, 0f), new Vector3(radius * 0.9f, 0.006f, radius * 0.9f),
                assets.Yellow, GameLayers.Default, false);

            for (int coil = 0; coil < 4; coil++)
            {
                Primitive(PrimitiveType.Cylinder, "Coil" + coil, root.transform, new Vector3(0f, -0.48f - coil * 0.26f, 0f),
                    new Vector3(1.3f - (coil % 2) * 0.25f, 0.09f, 1.3f - (coil % 2) * 0.25f), cove.Spring, GameLayers.Default, false);
            }

            Primitive(PrimitiveType.Cylinder, "Post", root.transform, new Vector3(0f, -8.4f, 0f), new Vector3(0.8f, 7f, 0.8f),
                cove.PillarBlue, GameLayers.Default, false);

            var pad = root.AddComponent<BouncePad>();
            pad.radius = radius;
            pad.visual = visual;
            return root;
        }

        // ------------------------------------------------------------------ 09 collapsing bridge

        /// <summary>One plank of a collapsing bridge: 3 m wide, 2.2 m long. The prefab origin is the centre of its top face.</summary>
        private static GameObject CreateCollapseSegment(ContentAssets assets)
        {
            var root = new GameObject("CollapsingBridgeSegment") { layer = GameLayers.MovingPlatform };
            KinematicBody(root);
            var solid = root.AddComponent<BoxCollider>();
            solid.center = new Vector3(0f, -0.25f, 0f);
            solid.size = new Vector3(3f, 0.5f, CollapseSegmentLength - 0.04f);

            Transform visual = Empty("Visual", root.transform, Vector3.zero, GameLayers.MovingPlatform).transform;
            Primitive(PrimitiveType.Cube, "Plank", visual, new Vector3(0f, -0.25f, 0f), new Vector3(3f, 0.5f, CollapseSegmentLength - 0.1f),
                assets.HotPink, GameLayers.MovingPlatform, false);
            Primitive(PrimitiveType.Cube, "Edge", visual, new Vector3(0f, -0.3f, 0f), new Vector3(3.2f, 0.25f, CollapseSegmentLength - 0.3f),
                assets.White, GameLayers.MovingPlatform, false);

            var segment = root.AddComponent<CollapsingPlatform>();
            segment.shakeVisual = visual;
            return root;
        }

        // ------------------------------------------------------------------ 11 floating arena

        /// <summary>
        /// Round floating raft, 9 m radius, that leans towards wherever the character stands.
        /// Hazards are added on top of it in the scene (see the layout), parented to "Disc" so they tilt with it.
        /// </summary>
        private static GameObject CreateFloatingArena(ContentAssets assets, ArenaAssets arena, CoveAssets cove)
        {
            const float radius = 9f;
            var root = new GameObject("FloatingArena") { layer = GameLayers.MovingPlatform };
            Primitive(PrimitiveType.Cylinder, "Anchor", root.transform, new Vector3(0f, -6f, 0f), new Vector3(2.4f, 4.6f, 2.4f),
                cove.PillarBlue, GameLayers.Default, false);

            GameObject disc = Empty("Disc", root.transform, Vector3.zero, GameLayers.MovingPlatform);
            Rigidbody body = KinematicBody(disc);
            SolidCylinder("Deck", disc.transform, new Vector3(0f, -0.3f, 0f), radius, 0.6f, cove.DeckBlue, GameLayers.MovingPlatform);
            Primitive(PrimitiveType.Cylinder, "Float", disc.transform, new Vector3(0f, -1.1f, 0f), new Vector3(radius * 1.7f, 0.55f, radius * 1.7f),
                assets.Yellow, GameLayers.MovingPlatform, false);
            Primitive(PrimitiveType.Cylinder, "CentreMark", disc.transform, new Vector3(0f, 0.006f, 0f), new Vector3(radius * 1.25f, 0.006f, radius * 1.25f),
                arena.Target, GameLayers.MovingPlatform, false);

            // Inflatable rim (visual only, below deck level so it never trips the character).
            const int segments = 18;
            float step = 360f / segments;
            float length = radius * step * Mathf.Deg2Rad * 1.08f;
            for (int i = 0; i < segments; i++)
            {
                float angle = i * step;
                Transform holder = Empty("Rim" + i, disc.transform, Vector3.zero, GameLayers.MovingPlatform).transform;
                holder.localRotation = Quaternion.Euler(0f, angle, 0f);
                CapsuleBar("Tube", holder, new Vector3(0f, -0.28f, radius + 0.05f), length + 0.75f, 0.75f,
                    i % 2 == 0 ? assets.Yellow : cove.PillarBlue, GameLayers.MovingPlatform, false);
            }

            for (int i = 0; i < 4; i++)
            {
                Transform holder = Empty("Flag" + i, disc.transform, Vector3.zero, GameLayers.MovingPlatform).transform;
                holder.localRotation = Quaternion.Euler(0f, 45f + i * 90f, 0f);
                Primitive(PrimitiveType.Cylinder, "Pole", holder, new Vector3(0f, 1.3f, radius + 0.1f), new Vector3(0.14f, 1.6f, 0.14f),
                    assets.Yellow, GameLayers.MovingPlatform, false);
                Primitive(PrimitiveType.Cube, "Pennant", holder, new Vector3(0.75f, 2.5f, radius + 0.1f), new Vector3(1.5f, 0.8f, 0.05f),
                    assets.HotPink, GameLayers.MovingPlatform, false);
            }

            var tilt = root.AddComponent<TiltingPlatform>();
            tilt.body = body;
            tilt.mode = TiltMode.Raft;
            tilt.maxTilt = 4f;
            tilt.halfLength = radius;
            tilt.stiffness = 9f;
            tilt.damping = 4.5f;
            tilt.maxTiltSpeed = 12f;
            tilt.idleSway = 0.5f;
            return root;
        }

        // ------------------------------------------------------------------ 12 checkpoint

        /// <summary>The Foundation 1 checkpoint (same component and beacons) under a small inflatable arch with pennants.</summary>
        private static GameObject CreateCheckpointGate(ContentAssets assets, ArenaAssets arena)
        {
            const float radius = 3.3f;
            GameObject root = CreateCheckpoint(assets);
            root.name = "CheckpointGate";
            Arch(root.transform, radius, 0.45f, assets.HotPink, assets.Yellow, 10);
            for (int side = -1; side <= 1; side += 2)
            {
                Primitive(PrimitiveType.Cylinder, side < 0 ? "FlagPoleLeft" : "FlagPoleRight", root.transform,
                    new Vector3(side * radius, 2.2f, 0f), new Vector3(0.1f, 2.2f, 0.1f), assets.White, GameLayers.Default, false);
                Primitive(PrimitiveType.Cube, side < 0 ? "FlagLeft" : "FlagRight", root.transform,
                    new Vector3(side * (radius + 0.55f), 4f, 0f), new Vector3(1.1f, 0.6f, 0.05f), assets.HotPink, GameLayers.Default, false);
            }

            Primitive(PrimitiveType.Cube, "Line", root.transform, new Vector3(0f, 0.008f, 0f), new Vector3(radius * 2f - 0.6f, 0.016f, 0.5f),
                arena.Checker, GameLayers.Default, false);
            return root;
        }

        // ------------------------------------------------------------------ 13 big ball

        /// <summary>
        /// A Big Ball: an enormous, slick red sphere on a fixed post. There is no Rigidbody and no script
        /// that moves it, so it cannot wobble or turn. The prefab origin is the very top of the ball.
        /// </summary>
        private static GameObject CreateBigBall(ContentAssets assets, ArenaAssets arena, CoveAssets cove)
        {
            var root = new GameObject("BigBall") { layer = GameLayers.Ground };
            // Ground layer: the camera treats the ball as solid (no clipping through it) and the character can stand on it.
            GameObject ball = Primitive(PrimitiveType.Sphere, "Ball", root.transform, new Vector3(0f, -BigBallRadius, 0f),
                Vector3.one * (BigBallRadius * 2f), arena.BallRed, GameLayers.Ground, true);

            var slick = ball.AddComponent<SlipperySurface>();
            slick.shape = SlipShape.Sphere;
            slick.surface = ball.GetComponent<SphereCollider>();
            slick.traction = 0.12f;
            slick.slideGravity = 14f;
            slick.maxSlideSpeed = 8f;
            slick.contactDistance = 0.12f;
            slick.landingBounce = 0.6f;
            slick.landingHop = 0.3f;
            slick.bounceMinAngle = 16f;
            slick.landEvent = AudioEventId.InflatableBoing;
            slick.slideEvent = AudioEventId.None;

            Primitive(PrimitiveType.Cylinder, "Collar", root.transform, new Vector3(0f, -BigBallRadius * 2f + 0.1f, 0f),
                new Vector3(1.9f, 0.14f, 1.9f), assets.Yellow, GameLayers.Default, false);
            Primitive(PrimitiveType.Cylinder, "Support", root.transform, new Vector3(0f, -BigBallRadius * 2f - 3.9f, 0f),
                new Vector3(1.2f, 4f, 1.2f), cove.PillarBlue, GameLayers.Default, false);
            return root;
        }

        // ------------------------------------------------------------------ scenery

        private static GameObject CreatePalm(CoveAssets cove)
        {
            var root = new GameObject("PalmTree");
            Vector3 point = Vector3.zero;
            float lean = 0f;
            for (int i = 0; i < 3; i++)
            {
                lean += 6f;
                Quaternion rotation = Quaternion.Euler(lean, 0f, 0f);
                const float segment = 2.3f;
                GameObject trunk = Primitive(PrimitiveType.Cylinder, "Trunk" + i, root.transform,
                    point + rotation * Vector3.up * (segment * 0.5f), new Vector3(0.5f - i * 0.07f, segment * 0.52f, 0.5f - i * 0.07f),
                    cove.PalmTrunk, GameLayers.Default, false);
                trunk.transform.localRotation = rotation;
                point += rotation * Vector3.up * segment;
            }

            for (int i = 0; i < 6; i++)
            {
                Transform holder = Empty("Frond" + i, root.transform, point, GameLayers.Default).transform;
                holder.localRotation = Quaternion.Euler(0f, i * 60f + 15f, 0f) * Quaternion.Euler(24f + (i % 2) * 10f, 0f, 0f);
                GameObject leaf = Primitive(PrimitiveType.Sphere, "Leaf", holder, new Vector3(0f, 0f, 1.7f), new Vector3(0.95f, 0.1f, 3.6f),
                    cove.PalmLeaf, GameLayers.Default, false);
                leaf.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            }

            Primitive(PrimitiveType.Sphere, "Crown", root.transform, point, Vector3.one * 0.8f, cove.PalmTrunk, GameLayers.Default, false);
            CullWhenSmall(root, 0.012f);
            return root;
        }
    }
}
