using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace BikiniWipeout.EditorTools
{
    public static partial class Foundation1Builder
    {
        /// <summary>Reusable Course 1 prefabs. Obstacle variants are built on the existing obstacle components.</summary>
        internal sealed class Course1Prefabs
        {
            public GameObject Water, Splash, ImpactBurst, Ball, SweeperSmall, SweeperLarge, SweeperDouble, Beam, SpinPlatform;
            public GameObject Pendulum, Piston, SectionGate, FinishGate;
        }

        private static Course1Prefabs BuildCourse1Prefabs(ContentAssets assets, ArenaAssets arena)
        {
            string folder = Course1PrefabFolder + "/";
            var set = new Course1Prefabs();
            set.Splash = GetOrBuildPrefab(folder + "SplashEffectArena.prefab", () => CreateArenaSplash(assets, arena));
            set.ImpactBurst = GetOrBuildPrefab(folder + "ImpactBurst.prefab", () => CreateImpactBurst(arena));
            set.Water = GetOrBuildPrefab(folder + "ArenaWater.prefab", () => CreateArenaWater(arena, set.Splash));
            set.Ball = GetOrBuildPrefab(folder + "BouncingBallArena.prefab", () => CreateArenaBall(assets, arena));

            // name, reach, low-bar count, high bars, bar material, speed (deg/s), impact force
            set.SweeperSmall = GetOrBuildPrefab(folder + "SweeperSmall.prefab",
                () => CreateSweeperVariant("SweeperSmall", 3.6f, 2, false, arena.StripesRed, assets.Yellow, 55f, 1.4f));
            set.SweeperLarge = GetOrBuildPrefab(folder + "SweeperLarge.prefab",
                () => CreateSweeperVariant("SweeperLarge", 6.6f, 2, false, arena.StripesRed, arena.PanelsBlue, 75f, 1.6f));
            set.SweeperDouble = GetOrBuildPrefab(folder + "SweeperDouble.prefab",
                () => CreateSweeperVariant("SweeperDouble", 7.2f, 2, true, arena.StripesRed, arena.PanelsYellow, 85f, 1.7f));
            set.Beam = GetOrBuildPrefab(folder + "RotatingBeam.prefab",
                () => CreateSweeperVariant("RotatingBeam", 2.8f, 2, false, arena.StripesBlue, assets.HotPink, 95f, 1.5f));

            set.SpinPlatform = GetOrBuildPrefab(folder + "SpinPlatform.prefab", () => CreateSpinPlatform(assets, arena));
            set.Pendulum = GetOrBuildPrefab(folder + "Pendulum.prefab", () => CreatePendulum(assets, arena));
            set.Piston = GetOrBuildPrefab(folder + "PistonWall.prefab", () => CreatePiston(assets, arena));
            set.SectionGate = GetOrBuildPrefab(folder + "SectionGate.prefab", () => CreateSectionGate(assets, arena));
            set.FinishGate = GetOrBuildPrefab(folder + "FinishGate.prefab", () => CreateFinishGate(assets, arena));
            return set;
        }

        // ------------------------------------------------------------------ shape helpers

        /// <summary>A rounded (inflatable-looking) bar lying along the parent's local X axis.</summary>
        private static GameObject CapsuleBar(string name, Transform parent, Vector3 center, float length, float thickness,
            Material material, int layer, bool keepCollider)
        {
            // Capsule primitive: 2 units tall along Y, 1 unit wide.
            GameObject bar = Primitive(PrimitiveType.Capsule, name, parent, center,
                new Vector3(thickness, length * 0.5f, thickness), material, layer, keepCollider);
            bar.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            return bar;
        }

        /// <summary>Inflatable arch in the parent's local XY plane: feet at x = ±radius, y = 0; top at y = radius.</summary>
        private static void Arch(Transform parent, float radius, float tube, Material a, Material b, int segments = 12)
        {
            Transform root = Empty("Arch", parent, Vector3.zero, GameLayers.Default).transform;
            float step = 180f / segments;
            float length = radius * step * Mathf.Deg2Rad * 1.25f;
            for (int i = 0; i < segments; i++)
            {
                float angle = (i + 0.5f) * step;
                float rad = angle * Mathf.Deg2Rad;
                GameObject piece = Primitive(PrimitiveType.Capsule, "Segment" + i, root,
                    new Vector3(Mathf.Cos(rad) * radius, Mathf.Sin(rad) * radius, 0f),
                    new Vector3(tube, length * 0.5f + tube * 0.25f, tube), i % 2 == 0 ? a : b, GameLayers.Default, false);
                // A capsule's long axis is local Y; rotating by the angle about Z lays it along the arch tangent.
                piece.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
            }
        }

        /// <summary>World-space text (signage). Readable when looking along the label's local +Z.</summary>
        private static Text WorldLabel(Transform parent, string name, Vector3 localPosition, string content, int fontSize,
            Color color, float widthMeters, float heightMeters)
        {
            var go = new GameObject(name, typeof(RectTransform)) { layer = UiLayer };
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.sizeDelta = new Vector2(widthMeters * 100f, heightMeters * 100f);
            rect.localScale = Vector3.one * 0.01f;
            rect.localPosition = localPosition;

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            Text text = UiText("Text", rect, content, fontSize, TextAnchor.MiddleCenter, color, Center, Vector2.zero, rect.sizeDelta);
            text.resizeTextForBestFit = false;
            return text;
        }

        private static Gradient FadeOut(Color color)
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.5f), new GradientAlphaKey(0f, 1f) });
            return gradient;
        }

        // ------------------------------------------------------------------ water and effects

        private static GameObject CreateArenaWater(ArenaAssets arena, GameObject splashPrefab)
        {
            const float size = 420f;
            const float depth = 30f;
            const float triggerInset = 0.45f;

            var root = new GameObject("ArenaWater") { layer = GameLayers.Trigger };
            var trigger = root.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(size, depth, size);
            trigger.center = new Vector3(0f, -triggerInset - depth * 0.5f, 0f);

            // Opaque, textured and gently animated: reads as bright stylized water at almost no cost.
            GameObject surface = Primitive(PrimitiveType.Plane, "Surface", root.transform, Vector3.zero,
                new Vector3(size / 10f, 1f, size / 10f), AssetDatabase.LoadAssetAtPath<Material>(BroadcastFolder + "EventWater.mat"), 4 /* built-in Water layer */, false);
            var renderer = surface.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var scroll = surface.AddComponent<ScrollingSurface>();
            scroll.tiling = new Vector2(size / 9f, size / 9f);

            var hazard = root.AddComponent<WaterHazard>();
            hazard.splashPrefab = splashPrefab;
            return root;
        }

        /// <summary>Droplet burst plus an expanding foam ring on the surface.</summary>
        private static GameObject CreateArenaSplash(ContentAssets assets, ArenaAssets arena)
        {
            GameObject root = CreateSplash(assets);
            root.name = "SplashEffectArena";
            ParticleSystem.MainModule dropletMain = root.GetComponent<ParticleSystem>().main;
            dropletMain.maxParticles = 160;
            ParticleSystem.EmissionModule dropletEmission = root.GetComponent<ParticleSystem>().emission;
            dropletEmission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)70) });

            var ringObject = new GameObject("FoamRing");
            ringObject.transform.SetParent(root.transform, false);
            ringObject.transform.localPosition = Vector3.up * 0.055f;

            var ring = ringObject.AddComponent<ParticleSystem>();
            ring.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = ring.main;
            main.duration = 1f;
            main.loop = false;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.1f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(2.4f, 3.4f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 4;

            ParticleSystem.EmissionModule emission = ring.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)2) });

            ParticleSystem.ShapeModule shape = ring.shape;
            shape.enabled = false;

            ParticleSystem.SizeOverLifetimeModule size = ring.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.25f, 1f, 1f));

            ParticleSystem.ColorOverLifetimeModule colour = ring.colorOverLifetime;
            colour.enabled = true;
            colour.color = new ParticleSystem.MinMaxGradient(FadeOut(Color.white));

            var ringRenderer = ringObject.GetComponent<ParticleSystemRenderer>();
            ringRenderer.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
            ringRenderer.sharedMaterial = arena.Ring;
            ringRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return root;
        }

        private static GameObject CreateImpactBurst(ArenaAssets arena)
        {
            var root = new GameObject("ImpactBurst");
            var particles = root.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = particles.main;
            main.duration = 0.5f;
            main.loop = false;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.7f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.95f, 0.4f), Color.white);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 32;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)16) });

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.15f;

            ParticleSystem.ColorOverLifetimeModule colour = particles.colorOverLifetime;
            colour.enabled = true;
            colour.color = new ParticleSystem.MinMaxGradient(FadeOut(Color.white));

            var renderer = root.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = arena.Star;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            root.AddComponent<SplashEffect>().lifetime = 1.2f;
            return root;
        }

        private static ParticleSystem CreateConfettiCannon(Transform parent, Vector3 localPosition, ArenaAssets arena)
        {
            var go = new GameObject("ConfettiCannon");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f); // cone points up

            var particles = go.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = particles.main;
            main.duration = 2.5f;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.5f, 4.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(8f, 15f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.24f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.gravityModifier = 0.55f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 400;

            var colours = new Gradient();
            colours.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(1f, 0.25f, 0.6f), 0f), new GradientColorKey(new Color(1f, 0.85f, 0.15f), 0.33f),
                    new GradientColorKey(new Color(0.25f, 0.55f, 1f), 0.66f), new GradientColorKey(new Color(1f, 0.55f, 0.1f), 1f)
                },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            main.startColor = new ParticleSystem.MinMaxGradient(colours) { mode = ParticleSystemGradientMode.RandomColor };

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 60f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)140) });

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 24f;
            shape.radius = 0.3f;

            ParticleSystem.RotationOverLifetimeModule rotation = particles.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-6f, 6f);

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = arena.Confetti;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return particles;
        }

        // ------------------------------------------------------------------ obstacles

        /// <summary>The Foundation 1 ball with arena colours and a bounce sound.</summary>
        private static GameObject CreateArenaBall(ContentAssets assets, ArenaAssets arena)
        {
            GameObject root = CreateBall(assets);
            root.name = "BouncingBallArena";
            root.GetComponent<MeshRenderer>().sharedMaterial = arena.BallRed;
            root.AddComponent<CollisionSound>();
            return root;
        }

        /// <summary>
        /// Rotating hazard built on <see cref="RotatingSweeper"/>: low bars to jump over and, optionally,
        /// high bars (above head height) that punish jumping at the wrong moment.
        /// </summary>
        private static GameObject CreateSweeperVariant(string name, float reach, int lowBars, bool highBars, Material barMaterial,
            Material hubMaterial, float speed, float force)
        {
            const float armHeight = 0.5f;
            const float thickness = 0.42f;
            const float highBarHeight = 2.35f; // bottom at ~2.14 m: clears a standing 1.7 m character, hits a jumping one
            float hubRadius = Mathf.Clamp(reach * 0.12f, 0.45f, 0.85f);
            float hubHeight = highBars ? highBarHeight + 0.5f : 1.35f;

            var root = new GameObject(name) { layer = GameLayers.Obstacle };
            Primitive(PrimitiveType.Cylinder, "Hub", root.transform, new Vector3(0f, hubHeight * 0.5f, 0f),
                new Vector3(hubRadius * 2f, hubHeight * 0.5f, hubRadius * 2f), hubMaterial, GameLayers.Obstacle, true);
            Primitive(PrimitiveType.Sphere, "HubCap", root.transform, new Vector3(0f, hubHeight, 0f),
                Vector3.one * (hubRadius * 2.1f), barMaterial, GameLayers.Obstacle, false);

            GameObject arm = Empty("Arm", root.transform, new Vector3(0f, armHeight, 0f), GameLayers.Obstacle);
            var body = arm.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;

            float length = reach - hubRadius;
            Transform firstTip = null;
            int total = lowBars + (highBars ? 2 : 0);
            for (int i = 0; i < total; i++)
            {
                bool high = i >= lowBars;
                float angle = high ? 90f + (i - lowBars) * 180f : i * 360f / lowBars;
                Transform holder = Empty((high ? "HighBar" : "Bar") + i, arm.transform,
                    new Vector3(0f, high ? highBarHeight - armHeight : 0f, 0f), GameLayers.Obstacle).transform;
                holder.localRotation = Quaternion.Euler(0f, angle, 0f);

                CapsuleBar("Tube", holder, new Vector3(hubRadius + length * 0.5f, 0f, 0f), length + thickness, thickness,
                    barMaterial, GameLayers.Obstacle, true);
                GameObject pad = Primitive(PrimitiveType.Sphere, "Pad", holder, new Vector3(reach, 0f, 0f),
                    Vector3.one * (thickness * 1.6f), hubMaterial, GameLayers.Obstacle, true);
                if (firstTip == null)
                {
                    firstTip = pad.transform;
                }

                // Trigger slightly larger than the tube: characters are detected just before solid contact.
                GameObject zone = Empty("ImpactZone", holder, new Vector3(hubRadius + length * 0.5f, 0f, 0f), GameLayers.Obstacle);
                var box = zone.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.size = new Vector3(length + thickness * 1.8f, thickness + 0.2f, thickness + 0.2f);
            }

            var impact = arm.AddComponent<ImpactSource>();
            impact.kind = ImpactSourceKind.Sweeper;
            impact.body = body;
            impact.forceMultiplier = force;

            var whoosh = arm.AddComponent<ProximityWhoosh>();
            whoosh.tip = firstTip;
            whoosh.radius = Mathf.Max(4f, reach * 0.9f);

            var sweeper = root.AddComponent<RotatingSweeper>();
            sweeper.armBody = body;
            sweeper.impactSource = impact;
            sweeper.rotationSpeed = speed;
            sweeper.impactForce = force;
            return root;
        }

        /// <summary>Large rotating disc the character rides. Reuses RotatingSweeper to drive a kinematic deck (no impact source).</summary>
        private static GameObject CreateSpinPlatform(ContentAssets assets, ArenaAssets arena)
        {
            const float radius = 4f;
            var root = new GameObject("SpinPlatform") { layer = GameLayers.MovingPlatform };
            Primitive(PrimitiveType.Cylinder, "Support", root.transform, new Vector3(0f, -4.5f, 0f),
                new Vector3(radius * 0.7f, 4f, radius * 0.7f), assets.Dark, GameLayers.Default, false);

            GameObject disc = Empty("Disc", root.transform, Vector3.zero, GameLayers.MovingPlatform);
            var body = disc.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;

            // Cylinder primitive: 2 units tall, 1 wide. Replace its capsule collider with the true shape.
            GameObject deck = Primitive(PrimitiveType.Cylinder, "Deck", disc.transform, new Vector3(0f, -0.25f, 0f),
                new Vector3(radius * 2f, 0.25f, radius * 2f), arena.Target, GameLayers.MovingPlatform, false);
            var collider = deck.AddComponent<MeshCollider>();
            collider.sharedMesh = deck.GetComponent<MeshFilter>().sharedMesh;
            collider.convex = true;

            Primitive(PrimitiveType.Cylinder, "Rim", disc.transform, new Vector3(0f, -0.3f, 0f),
                new Vector3(radius * 2f + 0.5f, 0.2f, radius * 2f + 0.5f), assets.Yellow, GameLayers.MovingPlatform, false);

            // The ring texture is rotationally symmetric, so spokes make the spin readable.
            for (int i = 0; i < 4; i++)
            {
                Transform spoke = Empty("Spoke" + i, disc.transform, Vector3.zero, GameLayers.MovingPlatform).transform;
                spoke.localRotation = Quaternion.Euler(0f, i * 90f, 0f);
                Primitive(PrimitiveType.Cube, "Mark", spoke, new Vector3(0f, 0.012f, radius * 0.62f),
                    new Vector3(0.7f, 0.02f, radius * 0.6f), i % 2 == 0 ? assets.Yellow : assets.Orange, GameLayers.MovingPlatform, false);
            }

            var spinner = root.AddComponent<RotatingSweeper>();
            spinner.armBody = body;
            spinner.rotationSpeed = 30f;
            spinner.impactForce = 0f;
            return root;
        }

        /// <summary>Swinging weight on a gantry. The prefab origin is the pivot; place it above the path.</summary>
        private static GameObject CreatePendulum(ContentAssets assets, ArenaAssets arena)
        {
            const float rodLength = 5.5f;
            const float bobRadius = 0.9f;
            const float postSpan = 4.6f;
            const float postLength = 10f;

            var root = new GameObject("Pendulum") { layer = GameLayers.Obstacle };

            // Gantry (visual only; it stands in the water either side of the path).
            for (int side = -1; side <= 1; side += 2)
            {
                Primitive(PrimitiveType.Cylinder, side < 0 ? "PostLeft" : "PostRight", root.transform,
                    new Vector3(side * postSpan, -postLength * 0.5f + 0.4f, 0f), new Vector3(0.5f, postLength * 0.5f, 0.5f),
                    side < 0 ? arena.PanelsBlue : arena.PanelsYellow, GameLayers.Default, false);
            }

            CapsuleBar("Crossbar", root.transform, new Vector3(0f, 0.4f, 0f), postSpan * 2f + 0.6f, 0.55f, arena.PanelsPink,
                GameLayers.Default, false);

            GameObject arm = Empty("Arm", root.transform, Vector3.zero, GameLayers.Obstacle);
            var body = arm.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;

            Primitive(PrimitiveType.Cylinder, "Rod", arm.transform, new Vector3(0f, -rodLength * 0.5f, 0f),
                new Vector3(0.16f, rodLength * 0.5f, 0.16f), assets.Dark, GameLayers.Obstacle, false);
            GameObject bob = Primitive(PrimitiveType.Sphere, "Weight", arm.transform, new Vector3(0f, -rodLength, 0f),
                Vector3.one * (bobRadius * 2f), arena.BallRed, GameLayers.Obstacle, true);
            Primitive(PrimitiveType.Cylinder, "Band", bob.transform, Vector3.zero, new Vector3(1.01f, 0.09f, 1.01f), assets.White,
                GameLayers.Obstacle, false);

            GameObject zone = Empty("ImpactZone", arm.transform, new Vector3(0f, -rodLength, 0f), GameLayers.Obstacle);
            var sphere = zone.AddComponent<SphereCollider>();
            sphere.isTrigger = true;
            sphere.radius = bobRadius + 0.1f;

            var impact = arm.AddComponent<ImpactSource>();
            impact.kind = ImpactSourceKind.Generic;
            impact.body = body;

            var whoosh = arm.AddComponent<ProximityWhoosh>();
            whoosh.tip = bob.transform;
            whoosh.radius = 4.5f;

            var pendulum = root.AddComponent<Pendulum>();
            pendulum.armBody = body;
            pendulum.impactSource = impact;
            impact.forceMultiplier = pendulum.impactForce;
            return root;
        }

        /// <summary>Moving wall / piston: a kinematic block driven by MovingPlatform that shoves characters sideways.</summary>
        private static GameObject CreatePiston(ContentAssets assets, ArenaAssets arena)
        {
            var root = new GameObject("PistonWall") { layer = GameLayers.Obstacle };
            var body = root.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;

            // 4 m long (along X, the push direction), 1.7 m tall, 1.3 m deep.
            Primitive(PrimitiveType.Cube, "Block", root.transform, new Vector3(0f, 0.95f, 0f), new Vector3(4f, 1.7f, 1.3f),
                arena.StripesBlue, GameLayers.Obstacle, true);
            Primitive(PrimitiveType.Cube, "FaceLeft", root.transform, new Vector3(-2.05f, 0.95f, 0f), new Vector3(0.12f, 1.8f, 1.4f),
                assets.Yellow, GameLayers.Obstacle, false);
            Primitive(PrimitiveType.Cube, "FaceRight", root.transform, new Vector3(2.05f, 0.95f, 0f), new Vector3(0.12f, 1.8f, 1.4f),
                assets.Yellow, GameLayers.Obstacle, false);

            GameObject zone = Empty("ImpactZone", root.transform, new Vector3(0f, 0.95f, 0f), GameLayers.Obstacle);
            var box = zone.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(4.25f, 1.7f, 1.5f);

            var impact = root.AddComponent<ImpactSource>();
            impact.kind = ImpactSourceKind.Platform;
            impact.body = body;
            impact.forceMultiplier = 1.2f;

            var mover = root.AddComponent<MovingPlatform>();
            mover.speed = 3.2f;
            mover.dwellTime = 1.1f;
            return root;
        }

        // ------------------------------------------------------------------ gates

        /// <summary>Inflatable arch with a name banner; announces the section on the HUD when the player passes under it.</summary>
        private static GameObject CreateSectionGate(ContentAssets assets, ArenaAssets arena)
        {
            const float radius = 4.6f;
            var root = new GameObject("SectionGate") { layer = GameLayers.Trigger };
            var trigger = root.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, 2f, 0f);
            trigger.size = new Vector3(radius * 2f, 4f, 1.5f);

            Arch(root.transform, radius, 0.6f, arena.PanelsPink, arena.PanelsYellow);
            Primitive(PrimitiveType.Cube, "Banner", root.transform, new Vector3(0f, radius + 1.0f, 0f), new Vector3(7.4f, 1.3f, 0.35f),
                arena.Screen, GameLayers.Default, false);
            WorldLabel(root.transform, "Label", new Vector3(0f, radius + 1.0f, -0.19f), "SECTION", 90, Color.white, 7.2f, 1.2f);

            root.AddComponent<SectionAnnouncer>();
            return root;
        }

        private static GameObject CreateFinishGate(ContentAssets assets, ArenaAssets arena)
        {
            const float radius = 6.4f;
            var root = new GameObject("FinishGate") { layer = GameLayers.Trigger };
            var trigger = root.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, 2.5f, 0f);
            trigger.size = new Vector3(14f, 5f, 1.2f);

            Arch(root.transform, radius, 0.9f, arena.PanelsYellow, arena.PanelsBlue, 14);
            Primitive(PrimitiveType.Cube, "CheckerStrip", root.transform, new Vector3(0f, 0.012f, 0f), new Vector3(12.8f, 0.02f, 1.6f),
                arena.Checker, GameLayers.Default, false);
            Primitive(PrimitiveType.Cube, "Banner", root.transform, new Vector3(0f, radius + 1.5f, 0f), new Vector3(8.4f, 2.2f, 0.5f),
                assets.HotPink, GameLayers.Default, false);
            Primitive(PrimitiveType.Cube, "BannerChecker", root.transform, new Vector3(0f, radius + 0.2f, 0f), new Vector3(8.6f, 0.45f, 0.54f),
                arena.Checker, GameLayers.Default, false);
            WorldLabel(root.transform, "Label", new Vector3(0f, radius + 1.5f, -0.27f), "FINISH", 170, Color.white, 8.2f, 2.1f);

            root.AddComponent<FinishLine>();

            Transform celebration = Empty("Celebration", root.transform, Vector3.zero, GameLayers.Default).transform;
            CreateConfettiCannon(celebration, new Vector3(-5.2f, 0.4f, 1.5f), arena);
            CreateConfettiCannon(celebration, new Vector3(5.2f, 0.4f, 1.5f), arena);
            celebration.gameObject.AddComponent<FinishCelebration>();
            return root;
        }
    }
}
