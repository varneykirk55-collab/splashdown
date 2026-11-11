using System;
using System.Collections.Generic;

namespace BikiniWipeout.Core
{
    public enum SurfaceShape
    {
        Box,
        Disc
    }

    /// <summary>What a surface is, so the scene builder knows how to dress it.</summary>
    public enum SurfaceKind
    {
        Start,
        Pad,
        MovingPlatform,
        SpinDisc,
        Island,
        Bridge,
        Rest,
        Checkpoint,
        Finish
    }

    /// <summary>One straight run of the course. Surfaces are positioned in the leg's local frame (x = sideways, z = forward).</summary>
    public readonly struct CourseLeg
    {
        public CourseLeg(float originX, float originZ, int yawDegrees)
        {
            OriginX = originX;
            OriginZ = originZ;
            YawDegrees = yawDegrees;
        }

        public float OriginX { get; }
        public float OriginZ { get; }

        /// <summary>Heading about the vertical axis. Multiples of 90 only.</summary>
        public int YawDegrees { get; }

        public void ToWorld(float localX, float localZ, out float worldX, out float worldZ)
        {
            double rad = YawDegrees * Math.PI / 180.0;
            double cos = Math.Round(Math.Cos(rad));
            double sin = Math.Round(Math.Sin(rad));
            worldX = (float)(OriginX + localX * cos + localZ * sin);
            worldZ = (float)(OriginZ - localX * sin + localZ * cos);
        }
    }

    /// <summary>A walkable surface of the course, in order of travel.</summary>
    public sealed class CourseSurface
    {
        public string Name;

        /// <summary>'A'..'E'.</summary>
        public char Section;

        public int Leg;
        public SurfaceKind Kind;
        public SurfaceShape Shape;

        /// <summary>Centre in the leg's local frame.</summary>
        public float X, Z;

        /// <summary>Box size: Width is sideways (x), Length is along the direction of travel (z).</summary>
        public float Width, Length;

        public float Radius;

        /// <summary>Height of the top face.</summary>
        public float Height;

        /// <summary>Moving platforms travel from the centre to centre + (MoveX, MoveZ) and back.</summary>
        public float MoveX, MoveZ;

        public bool IsMoving => MoveX != 0f || MoveZ != 0f;
    }

    /// <summary>
    /// Course 1 — The Splashdown Gauntlet. This is the single source of truth for where the walkable
    /// surfaces are: the scene builder constructs the course from it and the tests check every gap
    /// against the character's jump envelope, so the layout cannot silently become impossible.
    ///
    /// The course is a U: leg 0 runs +Z (sections A, B), leg 1 runs +X (section C), leg 2 runs -Z (D, E).
    /// </summary>
    public static class Course1Layout
    {
        public const float WaterLevel = -2.2f;

        public static readonly CourseLeg[] Legs =
        {
            new CourseLeg(0f, 0f, 0),
            new CourseLeg(6f, 110.3f, 90),
            new CourseLeg(76.5f, 104.3f, 180)
        };

        public static readonly string[] SectionNames =
        {
            "THE LAUNCH PAD", "THE BIG BOUNCE", "SPIN CITY", "THE CHAOS CROSSING", "THE FINAL SPLASH"
        };

        public static readonly IReadOnlyList<CourseSurface> Surfaces = new[]
        {
            // ---- Section A: The Launch Pad (beginner: every gap is clearable at walking speed)
            Box("StartPlatform", 'A', 0, SurfaceKind.Start, 0f, 6f, 12f, 12f),
            Box("LaunchPad_1", 'A', 0, SurfaceKind.Pad, 0f, 14.7f, 4f, 3f),
            Box("LaunchPad_2", 'A', 0, SurfaceKind.Pad, 1.5f, 19.2f, 4f, 3f),
            Moving("LaunchMover_1", 'A', 0, -2.5f, 24.2f, 4f, 4f, 5f, 0f),
            Moving("LaunchMover_2", 'A', 0, 0f, 29.7f, 4f, 4f, 0f, 4.5f),
            Box("SpinnerIsland", 'A', 0, SurfaceKind.Island, 0f, 47.7f, 11f, 20f),
            Box("Checkpoint1Platform", 'A', 0, SurfaceKind.Checkpoint, 0f, 62.2f, 10f, 6f),

            // ---- Section B: The Big Bounce
            Box("BounceAlley_Wide", 'B', 0, SurfaceKind.Island, 0f, 74.7f, 9f, 16f),
            Box("BounceAlley_Narrow", 'B', 0, SurfaceKind.Bridge, 0f, 90.5f, 5.5f, 12f),
            Disc("BouncePad", 'B', 0, SurfaceKind.Pad, 0f, 100.3f, 1.8f, 0f),
            Box("Corner1Platform", 'B', 0, SurfaceKind.Checkpoint, 0f, 110.3f, 12f, 12f),

            // ---- Section C: Spin City
            Disc("SpinDisc_1", 'C', 1, SurfaceKind.SpinDisc, 0f, 5.5f, 4f, 0f),
            Disc("SpinDisc_2", 'C', 1, SurfaceKind.SpinDisc, 0f, 15f, 4f, 0f),
            Box("SpinRest_1", 'C', 1, SurfaceKind.Rest, 0f, 23f, 6f, 5f),
            Disc("SweeperIsland", 'C', 1, SurfaceKind.Island, 0f, 34.5f, 7.5f, 0f),
            Box("SpinRest_2", 'C', 1, SurfaceKind.Rest, 0f, 45.5f, 6f, 4f),
            Box("BeamBridge", 'C', 1, SurfaceKind.Bridge, 0f, 56f, 3.6f, 14f),
            Box("Corner2Platform", 'C', 1, SurfaceKind.Checkpoint, 0f, 70.5f, 12f, 12f),

            // ---- Section D: The Chaos Crossing
            Box("PendulumBridge", 'D', 2, SurfaceKind.Bridge, 0f, 8.5f, 2.4f, 14f),
            Box("ChaosRest", 'D', 2, SurfaceKind.Rest, 0f, 19f, 6f, 4f),
            Box("PistonDeck", 'D', 2, SurfaceKind.Island, 0f, 29.5f, 7f, 14f),
            Box("ComboBridge", 'D', 2, SurfaceKind.Bridge, 0f, 45.1f, 2.6f, 14f),
            Box("Checkpoint4Platform", 'D', 2, SurfaceKind.Checkpoint, 0f, 57.6f, 10f, 8f),

            // ---- Section E: The Final Splash (rising pads up to the finish; running jumps required)
            Disc("FinalIsland", 'E', 2, SurfaceKind.Island, 0f, 71.1f, 8f, 0f),
            Disc("FinalPad_1", 'E', 2, SurfaceKind.Pad, 0f, 82.4f, 1.5f, 0.4f),
            Disc("FinalPad_2", 'E', 2, SurfaceKind.Pad, 1.0f, 87.4f, 1.5f, 0.8f),
            Disc("FinalPad_3", 'E', 2, SurfaceKind.Pad, -0.8f, 92.6f, 1.5f, 1.2f),
            Disc("FinalPad_4", 'E', 2, SurfaceKind.Pad, 0.4f, 97.9f, 1.5f, 1.6f),
            new CourseSurface
            {
                Name = "FinishPlatform", Section = 'E', Leg = 2, Kind = SurfaceKind.Finish, Shape = SurfaceShape.Box,
                X = 0f, Z = 108.4f, Width = 14f, Length = 14f, Height = 1.6f
            }
        };

        private static CourseSurface Box(string name, char section, int leg, SurfaceKind kind, float x, float z, float width, float length)
        {
            return new CourseSurface
            {
                Name = name, Section = section, Leg = leg, Kind = kind, Shape = SurfaceShape.Box,
                X = x, Z = z, Width = width, Length = length
            };
        }

        private static CourseSurface Moving(string name, char section, int leg, float x, float z, float width, float length,
            float moveX, float moveZ)
        {
            CourseSurface surface = Box(name, section, leg, SurfaceKind.MovingPlatform, x, z, width, length);
            surface.MoveX = moveX;
            surface.MoveZ = moveZ;
            return surface;
        }

        private static CourseSurface Disc(string name, char section, int leg, SurfaceKind kind, float x, float z, float radius, float height)
        {
            return new CourseSurface
            {
                Name = name, Section = section, Leg = leg, Kind = kind, Shape = SurfaceShape.Disc,
                X = x, Z = z, Radius = radius, Height = height
            };
        }

        /// <summary>
        /// Smallest horizontal gap (edge to edge, metres) between two surfaces. For moving platforms this
        /// is the gap at their best alignment, i.e. what a player who waits for the right moment faces.
        /// Zero or negative means they touch or overlap.
        /// </summary>
        public static float Gap(CourseSurface a, CourseSurface b)
        {
            const int samples = 21;
            float best = float.MaxValue;
            int stepsA = a.IsMoving ? samples : 1;
            int stepsB = b.IsMoving ? samples : 1;
            for (int i = 0; i < stepsA; i++)
            {
                for (int j = 0; j < stepsB; j++)
                {
                    float ta = stepsA == 1 ? 0f : i / (float)(stepsA - 1);
                    float tb = stepsB == 1 ? 0f : j / (float)(stepsB - 1);
                    best = Math.Min(best, GapAt(a, ta, b, tb));
                }
            }

            return best;
        }

        /// <summary>Largest gap over the movement cycle (the worst moment to jump).</summary>
        public static float WorstGap(CourseSurface a, CourseSurface b)
        {
            const int samples = 21;
            float worst = float.MinValue;
            int stepsA = a.IsMoving ? samples : 1;
            int stepsB = b.IsMoving ? samples : 1;
            for (int i = 0; i < stepsA; i++)
            {
                for (int j = 0; j < stepsB; j++)
                {
                    float ta = stepsA == 1 ? 0f : i / (float)(stepsA - 1);
                    float tb = stepsB == 1 ? 0f : j / (float)(stepsB - 1);
                    worst = Math.Max(worst, GapAt(a, ta, b, tb));
                }
            }

            return worst;
        }

        /// <summary>World-space centre of a surface. <paramref name="travel"/> is 0..1 along a moving platform's path.</summary>
        public static void WorldCenter(CourseSurface surface, float travel, out float x, out float z)
        {
            Legs[surface.Leg].ToWorld(surface.X + surface.MoveX * travel, surface.Z + surface.MoveZ * travel, out x, out z);
        }

        /// <summary>World-space half extents of a box surface (width and length swap on legs turned 90 degrees).</summary>
        public static void WorldHalfExtents(CourseSurface surface, out float halfX, out float halfZ)
        {
            bool turned = Math.Abs(Legs[surface.Leg].YawDegrees) % 180 == 90;
            halfX = (turned ? surface.Length : surface.Width) * 0.5f;
            halfZ = (turned ? surface.Width : surface.Length) * 0.5f;
        }

        private static float GapAt(CourseSurface a, float ta, CourseSurface b, float tb)
        {
            WorldCenter(a, ta, out float ax, out float az);
            WorldCenter(b, tb, out float bx, out float bz);

            if (a.Shape == SurfaceShape.Disc && b.Shape == SurfaceShape.Disc)
            {
                return Distance(ax - bx, az - bz) - a.Radius - b.Radius;
            }

            if (a.Shape == SurfaceShape.Box && b.Shape == SurfaceShape.Box)
            {
                WorldHalfExtents(a, out float ahx, out float ahz);
                WorldHalfExtents(b, out float bhx, out float bhz);
                float dx = Math.Max(0f, Math.Abs(ax - bx) - ahx - bhx);
                float dz = Math.Max(0f, Math.Abs(az - bz) - ahz - bhz);
                return Distance(dx, dz);
            }

            // Box against disc: distance from the disc centre to the box, minus the radius.
            CourseSurface box = a.Shape == SurfaceShape.Box ? a : b;
            float boxX = a.Shape == SurfaceShape.Box ? ax : bx;
            float boxZ = a.Shape == SurfaceShape.Box ? az : bz;
            float discX = a.Shape == SurfaceShape.Box ? bx : ax;
            float discZ = a.Shape == SurfaceShape.Box ? bz : az;
            float radius = a.Shape == SurfaceShape.Box ? b.Radius : a.Radius;
            WorldHalfExtents(box, out float hx, out float hz);
            float ex = Math.Max(0f, Math.Abs(discX - boxX) - hx);
            float ez = Math.Max(0f, Math.Abs(discZ - boxZ) - hz);
            return Distance(ex, ez) - radius;
        }

        private static float Distance(float x, float z)
        {
            return (float)Math.Sqrt(x * x + z * z);
        }
    }
}
