using System;
using System.Collections.Generic;

namespace BikiniWipeout.Core
{
    public enum ObstacleKind
    {
        /// <summary>01 - two padded arms on a hub. A = speed (deg/s), B = start angle.</summary>
        Sweeper,

        /// <summary>08 - four low arms that fill a bridge; they must be jumped. A = speed, B = start angle.</summary>
        SpinningArms,

        /// <summary>08 (large) - four long low arms. A = speed, B = start angle.</summary>
        GiantArms,

        /// <summary>01 (large) - low arms to jump and high arms that punish a badly timed jump. A = speed, B = start angle.</summary>
        DoubleSweeper,

        /// <summary>10 - pendulum swinging across the path. A = period (s), B = start phase 0..1, C = amplitude (deg).</summary>
        SwingingBall,

        /// <summary>05 - vertical water jet that throws the character upwards. A = cycle (s), B = cycle offset (s).</summary>
        Geyser,

        /// <summary>05 - horizontal water jet that pushes across the path. A = cycle, B = offset, C = heading relative to the leg (deg).</summary>
        SideJet,

        /// <summary>06 - bucket that fills, wobbles and dumps. A = cycle, B = offset, C = knockback heading relative to the leg (deg).</summary>
        TippingBucket,

        /// <summary>Patrolling physical ball (from Course 1). A = patrol half-length along the leg, B = start phase, C = speed.</summary>
        BouncingBall
    }

    /// <summary>A hazard placed on the course. Coordinates are in the leg's local frame; Y is the height of the deck it stands on.</summary>
    public sealed class CourseObstacle
    {
        public string Name;
        public ObstacleKind Kind;
        public int Leg;
        public float X, Z, Y;
        public float A, B, C;
    }

    public sealed class CourseCheckpoint
    {
        public string Name;

        /// <summary>The static surface the character respawns on.</summary>
        public string Surface;

        public int Leg;
        public float X, Z, Y;

        /// <summary>Facing relative to the leg, in degrees. Corner checkpoints face the next leg.</summary>
        public float Yaw;

        public float TriggerWidth, TriggerDepth;
    }

    /// <summary>Where a section's name arch stands.</summary>
    public readonly struct CourseGate
    {
        public CourseGate(int leg, float x, float z, float y)
        {
            Leg = leg;
            X = x;
            Z = z;
            Y = y;
        }

        public int Leg { get; }
        public float X { get; }
        public float Z { get; }
        public float Y { get; }
    }

    /// <summary>
    /// Course 2 - Chaos Cove. Single source of truth for the walkable surfaces, hazards, checkpoints
    /// and section gates: the scene builder constructs the course from this data, the map tool draws
    /// it and the tests check every jump against the character's jump envelope.
    ///
    /// The course is a loop of four legs: leg 0 runs +Z (sections A, B), leg 1 runs +X (C),
    /// leg 2 runs -Z (D, E) and leg 3 runs -X (F), finishing beside the start.
    /// Positions were generated with a running cursor (gap + length), so consecutive surfaces line up.
    /// </summary>
    public static class Course2Layout
    {
        public const float WaterLevel = -2.2f;

        /// <summary>Local Z of the finish gate on leg 3.</summary>
        public const float FinishGateZ = 68.5f;

        public const int BigBallCount = 4;

        public static readonly CourseLeg[] Legs =
        {
            new CourseLeg(0f, 0f, 0),
            new CourseLeg(6f, 147.5f, 90),
            new CourseLeg(99.8f, 141.5f, 180),
            new CourseLeg(93.8f, 14.1f, 270)
        };

        public static readonly string[] SectionNames =
        {
            "SPLASH ENTRY", "TIMING TERRACE", "WOBBLE WHARF", "BOUNCE AND SLIDE", "THE FLOATING ARENA", "THE BIG BALLS"
        };

        public static readonly IReadOnlyList<CourseSurface> Surfaces = new[]
        {
            // ---- Section A: Splash Entry (introduction to moving obstacles; every gap is clearable at walking speed)
            Box("StartPlatform", 'A', 0, SurfaceKind.Start, 0f, 6f, 12f, 12f),
            Moving("A_Mover_1", 'A', 0, -3f, 15.2f, 4f, 4f, 6f, 0f),
            Moving("A_Mover_2", 'A', 0, 3f, 20.6f, 4f, 4f, -6f, 0f),
            Box("A_SweeperIsland", 'A', 0, SurfaceKind.Island, 0f, 31f, 10f, 14f),
            Moving("A_Mover_3", 'A', 0, -1.5f, 41.4f, 4f, 4f, 3f, 5f),
            Box("A_CheckpointPlatform", 'A', 0, SurfaceKind.Checkpoint, 0f, 53f, 10f, 6f),

            // ---- Section B: Timing Terrace (timing and precision)
            Box("B_ArmsBridge", 'B', 0, SurfaceKind.Bridge, 0f, 66.6f, 4.5f, 18f),
            Box("B_Rest", 'B', 0, SurfaceKind.Rest, 0f, 79.2f, 6f, 4f),
            Box("B_SwingBridge", 'B', 0, SurfaceKind.Bridge, 0f, 92f, 3f, 18f),
            Box("B_CheckpointPlatform", 'B', 0, SurfaceKind.Checkpoint, 0f, 105.6f, 10f, 6f),
            Box("B_JetWalk", 'B', 0, SurfaceKind.Bridge, 0f, 118.2f, 4f, 16f),
            Disc("B_Pad_1", 'B', 0, SurfaceKind.Pad, -1.2f, 129.2f, 1.5f, 0f),
            Disc("B_Pad_2", 'B', 0, SurfaceKind.Pad, 1.2f, 133.7f, 1.5f, 0f),
            Disc("B_Pad_3", 'B', 0, SurfaceKind.Pad, -1.2f, 138.2f, 1.5f, 0f),
            Box("Corner1Platform", 'B', 0, SurfaceKind.Checkpoint, 0f, 147.5f, 12f, 12f),

            // ---- Section C: Wobble Wharf (unstable platforms and water hazards)
            Box("C_Seesaw_1", 'C', 1, SurfaceKind.Seesaw, 0f, 6.3f, 4f, 9f, 0f, 0f, 0.7f),
            Box("C_Rest_1", 'C', 1, SurfaceKind.Rest, 0f, 14.4f, 5f, 4f),
            Box("C_Seesaw_2", 'C', 1, SurfaceKind.Seesaw, 0f, 22.5f, 3.4f, 9f, 0f, 0f, 0.7f),
            Box("C_Rest_2", 'C', 1, SurfaceKind.Rest, 0f, 30.6f, 5f, 4f),
            Box("C_BucketBridge", 'C', 1, SurfaceKind.Bridge, 0f, 42.2f, 3.6f, 16f),
            Box("C_CheckpointPlatform", 'C', 1, SurfaceKind.Checkpoint, 0f, 54.8f, 10f, 6f),
            Box("C_CollapseBridge", 'C', 1, SurfaceKind.CollapsingBridge, 0f, 67.1f, 3f, 15.4f),
            Moving("C_Mover", 'C', 1, 0f, 78.2f, 4f, 4f, 0f, 6f),
            Box("Corner2Platform", 'C', 1, SurfaceKind.Checkpoint, 0f, 93.8f, 12f, 12f),

            // ---- Section D: Bounce and Slide (large physics-based wipeouts)
            Disc("D_Bounce_1", 'D', 2, SurfaceKind.BouncePad, 0f, 3.1f, 1.4f, 0f, 10.3f, 0f),
            Disc("D_Bounce_2", 'D', 2, SurfaceKind.BouncePad, 0.7f, 6.5f, 1.4f, 1f, 10.3f, 0f),
            Disc("D_Bounce_3", 'D', 2, SurfaceKind.BouncePad, -0.7f, 9.9f, 1.4f, 2f, 10.3f, 0f),
            Disc("D_Bounce_4", 'D', 2, SurfaceKind.BouncePad, 0f, 13.3f, 1.4f, 3f, 10.3f, 0f),
            Box("D_SlideTop", 'D', 2, SurfaceKind.Rest, 0f, 18.2f, 6f, 5f, 4f),
            Box("D_Slide", 'D', 2, SurfaceKind.SlideRamp, 0f, 27.2f, 5f, 13f, 4f, 4f, 0f),
            Disc("D_SplashIsland", 'D', 2, SurfaceKind.Island, 0f, 41.7f, 8.5f, 0f),
            Box("D_CheckpointPlatform", 'D', 2, SurfaceKind.Checkpoint, 0f, 54.8f, 10f, 6f),

            // ---- Section E: The Floating Arena (combined obstacle sequences)
            Box("E_ComboBridge", 'E', 2, SurfaceKind.Bridge, 0f, 67.4f, 3.4f, 16f),
            Box("E_CheckpointPlatform", 'E', 2, SurfaceKind.Checkpoint, 0f, 80f, 10f, 6f),
            Disc("E_Arena", 'E', 2, SurfaceKind.FloatingArena, 0f, 93.8f, 9f, 0f, 0f, 0f, 0.65f),
            Moving("E_Mover", 'E', 2, -3f, 106.3f, 4f, 4f, 6f, 0f),
            Box("E_ExitBridge", 'E', 2, SurfaceKind.Bridge, 0f, 114.8f, 3f, 10f),
            Box("Corner3Platform", 'E', 2, SurfaceKind.Checkpoint, 0f, 127.4f, 12f, 12f),

            // ---- Section F: The Big Balls (the final challenge)
            Moving("F_Mover", 'F', 3, 0f, 3.4f, 4f, 4f, 0f, 5f),
            Box("F_SpinDeck", 'F', 3, SurfaceKind.Island, 0f, 17.9f, 8f, 12f),
            Box("F_Ramp", 'F', 3, SurfaceKind.Ramp, 0f, 27.9f, 4.5f, 8f, 0f, -2f, 0f),
            Box("F_BallDeck", 'F', 3, SurfaceKind.Checkpoint, 0f, 34.9f, 7f, 6f, 2f),
            Disc("BigBall_1", 'F', 3, SurfaceKind.BigBall, 0f, 40.9f, 0.85f, 2.6f, 0f, 2f),
            Disc("BigBall_2", 'F', 3, SurfaceKind.BigBall, 0f, 45.6f, 0.85f, 2.6f, 0f, 2f),
            Disc("BigBall_3", 'F', 3, SurfaceKind.BigBall, 0f, 50.3f, 0.85f, 2.6f, 0f, 2f),
            Disc("BigBall_4", 'F', 3, SurfaceKind.BigBall, 0f, 55f, 0.85f, 2.6f, 0f, 2f),
            Box("F_Landing", 'F', 3, SurfaceKind.Rest, 0f, 61f, 8f, 6f, 1.4f),
            Box("FinishPlatform", 'F', 3, SurfaceKind.Finish, 0f, 71.6f, 14f, 12f, 1.4f),
        };

        public static readonly IReadOnlyList<CourseObstacle> Obstacles = new[]
        {
            // ---- A
            Ob("A_Sweeper", ObstacleKind.Sweeper, 0, 0f, 31f, 0f, 58f, 0f, 0f),

            // ---- B
            Ob("B_Arms_1", ObstacleKind.SpinningArms, 0, 0f, 62.6f, 0f, 80f, 0f, 0f),
            Ob("B_Arms_2", ObstacleKind.SpinningArms, 0, 0f, 70.6f, 0f, -95f, 45f, 0f),
            Ob("B_Swing_1", ObstacleKind.SwingingBall, 0, 0f, 85.5f, 0f, 3.2f, 0f, 40f),
            Ob("B_Swing_2", ObstacleKind.SwingingBall, 0, 0f, 88.75f, 0f, 2.8f, 0.3f, 40f),
            Ob("B_Swing_3", ObstacleKind.SwingingBall, 0, 0f, 92f, 0f, 3.4f, 0.55f, 40f),
            Ob("B_Swing_4", ObstacleKind.SwingingBall, 0, 0f, 95.25f, 0f, 2.7f, 0.15f, 40f),
            Ob("B_Swing_5", ObstacleKind.SwingingBall, 0, 0f, 98.5f, 0f, 3f, 0.7f, 40f),
            Ob("B_Geyser_1", ObstacleKind.Geyser, 0, -0.9f, 112.7f, 0f, 3.2f, 0f, 0f),
            Ob("B_Geyser_2", ObstacleKind.Geyser, 0, 0.9f, 115.7f, 0f, 3.2f, 0.8f, 0f),
            Ob("B_Geyser_3", ObstacleKind.Geyser, 0, -0.9f, 118.7f, 0f, 3.2f, 1.6f, 0f),
            Ob("B_Geyser_4", ObstacleKind.Geyser, 0, 0.9f, 121.2f, 0f, 3.2f, 2.4f, 0f),
            Ob("B_Geyser_5", ObstacleKind.Geyser, 0, 0f, 123.7f, 0f, 3.2f, 0.4f, 0f),
            Ob("B_SideJet", ObstacleKind.SideJet, 0, 5.4f, 133.7f, 0f, 4f, 0f, -90f),

            // ---- C
            Ob("C_Bucket_1", ObstacleKind.TippingBucket, 1, 0f, 37.2f, 0f, 4.5f, 0f, 90f),
            Ob("C_Bucket_2", ObstacleKind.TippingBucket, 1, 0f, 42.2f, 0f, 4.5f, 1.5f, -90f),
            Ob("C_Bucket_3", ObstacleKind.TippingBucket, 1, 0f, 47.2f, 0f, 4.5f, 3f, 90f),

            // ---- D
            Ob("D_DoubleSweeper", ObstacleKind.DoubleSweeper, 2, 0f, 41.7f, 0f, 80f, 0f, 0f),

            // ---- E
            Ob("E_Arms", ObstacleKind.SpinningArms, 2, 0f, 63.4f, 0f, 85f, 0f, 0f),
            Ob("E_Geyser", ObstacleKind.Geyser, 2, 0f, 67.9f, 0f, 3f, 0f, 0f),
            Ob("E_Swing_1", ObstacleKind.SwingingBall, 2, 0f, 70.4f, 0f, 3f, 0.2f, 40f),
            Ob("E_Swing_2", ObstacleKind.SwingingBall, 2, 0f, 73.2f, 0f, 2.6f, 0.7f, 40f),
            Ob("E_ArenaArms", ObstacleKind.GiantArms, 2, 0f, 93.8f, 0f, 60f, 0f, 0f),
            Ob("E_ArenaGeyser_1", ObstacleKind.Geyser, 2, -7.2f, 90.8f, 0f, 3.5f, 0f, 0f),
            Ob("E_ArenaGeyser_2", ObstacleKind.Geyser, 2, -7.2f, 96.8f, 0f, 3.5f, 1.75f, 0f),
            Ob("E_ArenaBall_1", ObstacleKind.BouncingBall, 2, 7.2f, 91.6f, 0f, 2.6f, -0.6f, 2.4f),
            Ob("E_ArenaBall_2", ObstacleKind.BouncingBall, 2, 7.2f, 96f, 0f, 2.6f, 0.7f, 2.9f),
            Ob("E_Swing_3", ObstacleKind.SwingingBall, 2, 0f, 112.8f, 0f, 2.9f, 0f, 40f),
            Ob("E_Swing_4", ObstacleKind.SwingingBall, 2, 0f, 116.8f, 0f, 3.3f, 0.5f, 40f),

            // ---- F
            Ob("F_GiantArms", ObstacleKind.GiantArms, 3, 0f, 17.9f, 0f, -70f, 0f, 0f),
            Ob("F_Bucket", ObstacleKind.TippingBucket, 3, 0f, 27.9f, 1f, 4f, 0f, 180f),
        };

        public static readonly IReadOnlyList<CourseCheckpoint> Checkpoints = new[]
        {
            Cp("Checkpoint_0_Start", "StartPlatform", 0, 0f, 3f, 0f, 0f, 12f, 6f),
            Cp("Checkpoint_1_TimingTerrace", "A_CheckpointPlatform", 0, 0f, 53f, 0f, 0f, 10f, 6f),
            Cp("Checkpoint_2_JetWalk", "B_CheckpointPlatform", 0, 0f, 105.6f, 0f, 0f, 10f, 6f),
            Cp("Checkpoint_3_WobbleWharf", "Corner1Platform", 0, 0f, 147.5f, 0f, 90f, 12f, 12f),
            Cp("Checkpoint_4_CollapseBridge", "C_CheckpointPlatform", 1, 0f, 54.8f, 0f, 0f, 10f, 6f),
            Cp("Checkpoint_5_BounceAndSlide", "Corner2Platform", 1, 0f, 93.8f, 0f, 90f, 12f, 12f),
            Cp("Checkpoint_6_ChaosRun", "D_CheckpointPlatform", 2, 0f, 54.8f, 0f, 0f, 10f, 6f),
            Cp("Checkpoint_7_FloatingArena", "E_CheckpointPlatform", 2, 0f, 80f, 0f, 0f, 10f, 6f),
            Cp("Checkpoint_8_TheBigBalls", "Corner3Platform", 2, 0f, 127.4f, 0f, 90f, 12f, 12f),
            Cp("Checkpoint_9_BallDeck", "F_BallDeck", 3, 0f, 34.4f, 2f, 0f, 7f, 5f),
        };

        /// <summary>One gate per section, in order (A..F).</summary>
        public static readonly IReadOnlyList<CourseGate> Gates = new[]
        {
            new CourseGate(0, 0f, 11f, 0f),
            new CourseGate(0, 0f, 55.2f, 0f),
            new CourseGate(1, 0f, -1.2f, 0f),
            new CourseGate(2, 0f, -1.2f, 0f),
            new CourseGate(2, 0f, 57f, 0f),
            new CourseGate(3, 0f, -1.2f, 0f),
        };

        public static CourseSurface Find(string name)
        {
            for (int i = 0; i < Surfaces.Count; i++)
            {
                if (Surfaces[i].Name == name)
                {
                    return Surfaces[i];
                }
            }

            return null;
        }

        private static CourseSurface Box(string name, char section, int leg, SurfaceKind kind, float x, float z, float width,
            float length, float height = 0f, float drop = 0f, float sag = 0f)
        {
            return new CourseSurface
            {
                Name = name, Section = section, Leg = leg, Kind = kind, Shape = SurfaceShape.Box,
                X = x, Z = z, Width = width, Length = length, Height = height, Drop = drop, Sag = sag
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

        private static CourseSurface Disc(string name, char section, int leg, SurfaceKind kind, float x, float z, float radius,
            float height, float launchSpeed = 0f, float bodyRadius = 0f, float sag = 0f)
        {
            return new CourseSurface
            {
                Name = name, Section = section, Leg = leg, Kind = kind, Shape = SurfaceShape.Disc,
                X = x, Z = z, Radius = radius, Height = height, LaunchSpeed = launchSpeed, BodyRadius = bodyRadius, Sag = sag
            };
        }

        private static CourseObstacle Ob(string name, ObstacleKind kind, int leg, float x, float z, float y, float a, float b, float c)
        {
            return new CourseObstacle { Name = name, Kind = kind, Leg = leg, X = x, Z = z, Y = y, A = a, B = b, C = c };
        }

        private static CourseCheckpoint Cp(string name, string surface, int leg, float x, float z, float y, float yaw,
            float triggerWidth, float triggerDepth)
        {
            return new CourseCheckpoint
            {
                Name = name, Surface = surface, Leg = leg, X = x, Z = z, Y = y, Yaw = yaw,
                TriggerWidth = triggerWidth, TriggerDepth = triggerDepth
            };
        }
    }
}
