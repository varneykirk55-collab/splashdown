using System;
using System.Collections.Generic;
using System.Linq;
using BikiniWipeout.Core;
using NUnit.Framework;

namespace BikiniWipeout.Tests
{
    /// <summary>
    /// Checks that Course 2 (Chaos Cove) is physically completable with the character's real movement
    /// values, is the intended size, contains every approved obstacle type, and restarts fairly.
    /// </summary>
    public class Course2LayoutTests
    {
        private const float RunMargin = 0.75f;
        private const float WalkMargin = 0.9f;

        private static readonly JumpEnvelope Jump = JumpEnvelope.Default;
        private static readonly CourseLeg[] Legs = Course2Layout.Legs;
        private static IReadOnlyList<CourseSurface> Surfaces => Course2Layout.Surfaces;

        private static IEnumerable<(CourseSurface from, CourseSurface to)> Steps()
        {
            for (int i = 0; i + 1 < Surfaces.Count; i++)
            {
                yield return (Surfaces[i], Surfaces[i + 1]);
            }
        }

        private static bool IsIncline(CourseSurface surface)
        {
            return surface.Kind == SurfaceKind.SlideRamp || surface.Kind == SurfaceKind.Ramp;
        }

        /// <summary>Height the player has to gain (positive) or loses (negative) on this step, in the worst case.</summary>
        private static float Rise(CourseSurface from, CourseSurface to)
        {
            return to.Height - from.ExitHeight;
        }

        /// <summary>Furthest the player can travel on this step: a running jump, or the flight off a bounce pad.</summary>
        private static float Reach(CourseSurface from, CourseSurface to, JumpEnvelope jump)
        {
            float rise = Rise(from, to);
            if (from.LaunchSpeed > 0f)
            {
                return jump.RunSpeed * SlipMath.LaunchAirTime(from.LaunchSpeed, rise, jump.Gravity, jump.FallGravityMultiplier);
            }

            return jump.RunReach(rise);
        }

        [Test]
        public void Course_HasSixSections_InOrder_EachEndingOnACheckpoint()
        {
            string order = new string(Surfaces.Select(s => s.Section).Distinct().ToArray());
            Assert.AreEqual("ABCDEF", order);
            Assert.AreEqual(6, Course2Layout.SectionNames.Length);
            Assert.AreEqual(6, Course2Layout.Gates.Count, "One name gate per section.");

            foreach (char section in "ABCDEF")
            {
                Assert.GreaterOrEqual(Surfaces.Count(s => s.Section == section), 4, "Section " + section);
            }

            foreach (char section in "ABCDE")
            {
                Assert.AreEqual(SurfaceKind.Checkpoint, Surfaces.Last(s => s.Section == section).Kind,
                    "Section " + section + " should end on a checkpoint platform.");
            }

            Assert.AreEqual(SurfaceKind.Start, Surfaces[0].Kind);
            Assert.AreEqual(SurfaceKind.Finish, Surfaces[Surfaces.Count - 1].Kind);
            Assert.AreEqual(1, Surfaces.Count(s => s.Kind == SurfaceKind.Finish));
            Assert.AreEqual(Surfaces.Count, Surfaces.Select(s => s.Name).Distinct().Count(), "Surface names must be unique.");
        }

        [Test]
        public void Course_IsAboutOneAndAHalfTimesAsLongAsCourse1()
        {
            float course1 = CourseLayoutMath.RouteLength(Course1Layout.Legs, Course1Layout.Surfaces);
            float course2 = CourseLayoutMath.RouteLength(Legs, Surfaces);
            float ratio = course2 / course1;
            Assert.GreaterOrEqual(ratio, 1.4f, $"Course 2 is {course2:0} m, Course 1 is {course1:0} m.");
            Assert.LessOrEqual(ratio, 1.7f, $"Course 2 is {course2:0} m, Course 1 is {course1:0} m.");
        }

        [Test]
        public void EveryStep_IsARealGap_OrAnInclineYouWalkOnto()
        {
            foreach ((CourseSurface from, CourseSurface to) in Steps())
            {
                float gap = CourseLayoutMath.Gap(Legs, from, to);
                if (IsIncline(from) || IsIncline(to))
                {
                    Assert.LessOrEqual(gap, 0.01f, $"{from.Name} -> {to.Name}: ramps and slides must join their neighbours.");
                }
                else
                {
                    Assert.Greater(gap, 0.6f, $"{from.Name} -> {to.Name} should be separated by water.");
                }
            }
        }

        [Test]
        public void EveryGap_IsWithinReach_WithMargin()
        {
            foreach ((CourseSurface from, CourseSurface to) in Steps())
            {
                float gap = CourseLayoutMath.Gap(Legs, from, to);
                if (gap <= 0f)
                {
                    continue;
                }

                float reach = Reach(from, to, Jump);
                Assert.Greater(reach, 0f, $"{from.Name} -> {to.Name}: the next surface is too high to reach at all.");
                Assert.LessOrEqual(gap, reach * RunMargin,
                    $"{from.Name} -> {to.Name}: gap {gap:0.00} m exceeds {RunMargin:P0} of the reach ({reach:0.00} m) for a rise of {Rise(from, to):0.00} m.");
            }
        }

        [Test]
        public void SectionA_CanBeCompletedAtWalkingSpeed()
        {
            foreach ((CourseSurface from, CourseSurface to) in Steps().Where(s => s.from.Section == 'A' && s.to.Section == 'A'))
            {
                float gap = CourseLayoutMath.Gap(Legs, from, to);
                Assert.LessOrEqual(gap, Jump.WalkReach(Rise(from, to)) * WalkMargin, $"{from.Name} -> {to.Name}: gap {gap:0.00} m.");
            }
        }

        [Test]
        public void NoStepUp_IsTallerThanTheCharacterCanClimb()
        {
            foreach ((CourseSurface from, CourseSurface to) in Steps())
            {
                if (CourseLayoutMath.Gap(Legs, from, to) <= 0f)
                {
                    continue; // walked, not jumped
                }

                float limit = from.LaunchSpeed > 0f
                    ? SlipMath.LaunchApex(from.LaunchSpeed, Jump.Gravity) * 0.6f
                    : Jump.JumpHeight * 0.6f;
                Assert.LessOrEqual(Rise(from, to), limit, $"{from.Name} -> {to.Name}");
            }
        }

        [Test]
        public void BouncePads_CanBeRiddenWithoutSprinting()
        {
            // A character that simply walks forward off each pad must still reach the next one.
            foreach ((CourseSurface from, CourseSurface to) in Steps().Where(s => s.from.Kind == SurfaceKind.BouncePad))
            {
                float gap = CourseLayoutMath.Gap(Legs, from, to);
                float air = SlipMath.LaunchAirTime(from.LaunchSpeed, Rise(from, to), Jump.Gravity, Jump.FallGravityMultiplier);
                Assert.Greater(air, 0f, from.Name);
                Assert.GreaterOrEqual(Jump.WalkSpeed * air + from.Radius, gap + from.Radius + 0.2f,
                    $"{from.Name} -> {to.Name}: gap {gap:0.00} m, walking flight {Jump.WalkSpeed * air:0.00} m.");
                Assert.Greater(SlipMath.LaunchApex(from.LaunchSpeed, Jump.Gravity), Jump.JumpHeight,
                    "A bounce pad should throw the character higher than a jump.");
            }

            Assert.GreaterOrEqual(Surfaces.Count(s => s.Kind == SurfaceKind.BouncePad), 3);
        }

        [Test]
        public void NonAdjacentSurfaces_DoNotOverlap()
        {
            for (int i = 0; i < Surfaces.Count; i++)
            {
                for (int j = i + 2; j < Surfaces.Count; j++)
                {
                    Assert.Greater(CourseLayoutMath.Gap(Legs, Surfaces[i], Surfaces[j]), 0.5f,
                        $"{Surfaces[i].Name} and {Surfaces[j].Name} overlap or nearly touch.");
                }
            }
        }

        [Test]
        public void AllThirteenApprovedObstacles_ArePresent()
        {
            int Hazards(params ObstacleKind[] kinds) => Course2Layout.Obstacles.Count(o => kinds.Contains(o.Kind));
            int Kind(SurfaceKind kind) => Surfaces.Count(s => s.Kind == kind);

            Assert.GreaterOrEqual(Hazards(ObstacleKind.Sweeper, ObstacleKind.DoubleSweeper), 2, "01 rotating sweepers");
            Assert.GreaterOrEqual(Kind(SurfaceKind.MovingPlatform), 5, "02 moving platforms");
            Assert.GreaterOrEqual(Kind(SurfaceKind.Seesaw), 2, "03 seesaw platforms");
            Assert.GreaterOrEqual(Kind(SurfaceKind.BouncePad), 4, "04 bounce pads");
            Assert.GreaterOrEqual(Hazards(ObstacleKind.Geyser, ObstacleKind.SideJet), 6, "05 water jets");
            Assert.GreaterOrEqual(Hazards(ObstacleKind.TippingBucket), 3, "06 tipping buckets");
            Assert.GreaterOrEqual(Kind(SurfaceKind.SlideRamp), 1, "07 water slide ramp");
            Assert.GreaterOrEqual(Hazards(ObstacleKind.SpinningArms, ObstacleKind.GiantArms), 4, "08 spinning arms");
            Assert.GreaterOrEqual(Kind(SurfaceKind.CollapsingBridge), 1, "09 collapsing bridge");
            Assert.GreaterOrEqual(Hazards(ObstacleKind.SwingingBall), 6, "10 swinging balls");
            Assert.AreEqual(1, Kind(SurfaceKind.FloatingArena), "11 floating arena");
            Assert.GreaterOrEqual(Course2Layout.Checkpoints.Count, 8, "12 checkpoints");
            Assert.AreEqual(1, Kind(SurfaceKind.Finish), "12 finish gate");
            Assert.AreEqual(Course2Layout.BigBallCount, Kind(SurfaceKind.BigBall), "13 big balls");
            Assert.AreEqual(4, Course2Layout.BigBallCount, "The brief asks for exactly four big balls.");
        }

        [Test]
        public void TheFloatingArena_CombinesSeveralHazards()
        {
            CourseSurface arena = Surfaces.Single(s => s.Kind == SurfaceKind.FloatingArena);
            List<CourseObstacle> onArena = Course2Layout.Obstacles
                .Where(o => o.Leg == arena.Leg && CourseLayoutMath.Contains(arena, o.X, o.Z)).ToList();
            Assert.GreaterOrEqual(onArena.Select(o => o.Kind).Distinct().Count(), 3, "The arena should mix at least three hazard types.");
            Assert.Greater(arena.Sag, 0f, "The arena deck is unstable footing.");

            // A learnable path: the rim lane outside the arms' reach must be wide enough to run along.
            const float giantArmsReach = 5.5f;
            Assert.GreaterOrEqual(arena.Radius - giantArmsReach, 3f);
        }

        [Test]
        public void Obstacles_StandOnTheirSurfaces_AndHaveUniqueNames()
        {
            Assert.AreEqual(Course2Layout.Obstacles.Count, Course2Layout.Obstacles.Select(o => o.Name).Distinct().Count());
            foreach (CourseObstacle obstacle in Course2Layout.Obstacles)
            {
                // A side jet stands in the water beside the surface it sprays across.
                float margin = obstacle.Kind == ObstacleKind.SideJet ? 6f : 0.3f;
                bool supported = Surfaces.Any(s => s.Leg == obstacle.Leg && CourseLayoutMath.Contains(s, obstacle.X, obstacle.Z, margin));
                Assert.IsTrue(supported, obstacle.Name + " is not on (or beside) any surface of its leg.");

                if (obstacle.Kind == ObstacleKind.Geyser || obstacle.Kind == ObstacleKind.SideJet || obstacle.Kind == ObstacleKind.TippingBucket)
                {
                    Assert.Greater(obstacle.A, 1.5f, obstacle.Name + " needs a cycle long enough to read and pass.");
                }
            }
        }

        [Test]
        public void Checkpoints_AreOnSafeStaticGround_InRouteOrder_AndNeverFarApart()
        {
            IReadOnlyList<CourseCheckpoint> checkpoints = Course2Layout.Checkpoints;
            Assert.AreEqual(checkpoints.Count, checkpoints.Select(c => c.Name).Distinct().Count());
            Assert.AreEqual("StartPlatform", checkpoints[0].Surface);

            var names = Surfaces.Select(s => s.Name).ToList();
            float[] distanceAt = new float[Surfaces.Count];
            for (int i = 1; i < Surfaces.Count; i++)
            {
                distanceAt[i] = distanceAt[i - 1] + CourseLayoutMath.RouteLength(Legs, new[] { Surfaces[i - 1], Surfaces[i] });
            }

            int previousIndex = -1;
            foreach (CourseCheckpoint checkpoint in checkpoints)
            {
                CourseSurface surface = Course2Layout.Find(checkpoint.Surface);
                Assert.IsNotNull(surface, checkpoint.Name + " refers to a surface that does not exist.");
                Assert.IsTrue(surface.Kind == SurfaceKind.Start || surface.Kind == SurfaceKind.Checkpoint,
                    checkpoint.Name + " must be on a plain, static platform.");
                Assert.AreEqual(surface.Leg, checkpoint.Leg, checkpoint.Name);
                Assert.IsTrue(CourseLayoutMath.Contains(surface, checkpoint.X, checkpoint.Z, -0.8f), checkpoint.Name + " spawn point is too near an edge.");
                Assert.AreEqual(surface.Height, checkpoint.Y, 0.001f, checkpoint.Name);
                Assert.IsFalse(Course2Layout.Obstacles.Any(o => o.Leg == surface.Leg && CourseLayoutMath.Contains(surface, o.X, o.Z)),
                    checkpoint.Name + " shares its platform with a hazard.");

                int index = names.IndexOf(surface.Name);
                Assert.Greater(index, previousIndex, checkpoint.Name + " is out of route order.");
                if (previousIndex >= 0)
                {
                    Assert.LessOrEqual(distanceAt[index] - distanceAt[previousIndex], 70f,
                        checkpoint.Name + " is too far from the checkpoint before it.");
                }

                previousIndex = index;
            }

            Assert.LessOrEqual(distanceAt[Surfaces.Count - 1] - distanceAt[previousIndex], 70f, "The finish is too far from the last checkpoint.");
        }

        // ---------------------------------------------------------------- the Big Balls

        private static List<CourseSurface> Balls => Surfaces.Where(s => s.Kind == SurfaceKind.BigBall).ToList();

        [Test]
        public void BigBalls_AreFourStationaryBalls_InARow_AboveTheWater()
        {
            List<CourseSurface> balls = Balls;
            Assert.AreEqual(4, balls.Count);
            for (int i = 0; i < balls.Count; i++)
            {
                CourseSurface ball = balls[i];
                Assert.IsFalse(ball.IsMoving, ball.Name + " must not move.");
                Assert.AreEqual(0f, ball.Sag, ball.Name + " must not wobble.");
                Assert.AreEqual(0f, ball.LaunchSpeed, ball.Name);
                Assert.GreaterOrEqual(ball.BodyRadius, 1.8f, ball.Name + " should be enormous.");
                Assert.AreEqual(balls[0].Height, ball.Height, 0.001f);
                Assert.AreEqual(balls[0].X, ball.X, 0.001f);
                Assert.Greater(ball.Height - 2f * ball.BodyRadius, Course2Layout.WaterLevel + 0.3f, ball.Name + " should sit clear of the water.");
                Assert.AreEqual(names(i), ball.Name);
            }

            // Consecutive in the route, with a checkpoint directly before them.
            int first = Surfaces.ToList().IndexOf(balls[0]);
            Assert.AreEqual(SurfaceKind.Checkpoint, Surfaces[first - 1].Kind, "The run-up to the balls should be a checkpoint.");
            for (int i = 0; i < balls.Count; i++)
            {
                Assert.AreSame(balls[i], Surfaces[first + i]);
            }

            string names(int i) => "BigBall_" + (i + 1);
        }

        [Test]
        public void BigBalls_NeedACommittedRunningJump_ThatTheCharacterCanMake()
        {
            List<CourseSurface> balls = Balls;
            float runReach = Jump.RunReach(0f);
            float walkReach = Jump.WalkReach(0f);
            for (int i = 0; i + 1 < balls.Count; i++)
            {
                float spacing = balls[i + 1].Z - balls[i].Z;
                Assert.Greater(spacing, 2f * balls[i].BodyRadius + 0.3f, "The balls must not touch.");

                // Crown to crown: possible at a run with margin...
                float capGap = spacing - balls[i].Radius - balls[i + 1].Radius;
                Assert.LessOrEqual(capGap, runReach * RunMargin, $"Ball {i + 1} -> {i + 2}: crown gap {capGap:0.00} m.");

                // ...a full-speed jump from the middle of one crown comes down on the next crown...
                Assert.LessOrEqual(Math.Abs(runReach - spacing), balls[i + 1].Radius,
                    $"A running jump ({runReach:0.00} m) from the top of ball {i + 1} should land on the crown of ball {i + 2} ({spacing:0.00} m away).");

                // ...and a walking jump does not get there. The challenge is the geometry, not the controls.
                Assert.Greater(spacing - balls[i + 1].Radius, walkReach, "A walking hop should not be enough.");
            }
        }

        [Test]
        public void BigBalls_Crown_IsTheOnlyPlaceWithGrip()
        {
            // Default ball traction: the surface holds slopes up to atan(traction).
            const float traction = 0.12f;
            float slipAngle = SlipMath.SlipAngle(traction);
            Assert.Less(slipAngle, 10f, "Very little traction.");

            foreach (CourseSurface ball in Balls)
            {
                // The landing cap used by the reach checks must still be a gentle part of the ball...
                double capAngle = Math.Asin(ball.Radius / ball.BodyRadius) * 180.0 / Math.PI;
                Assert.Less(capAngle, 30.0, ball.Name);

                // ...and at its edge the ball already slides, but slowly enough to correct.
                float atCapEdge = SlipMath.SlideAcceleration((float)Math.Cos(capAngle * Math.PI / 180.0), 14f, traction);
                Assert.Greater(atCapEdge, 0f);
                Assert.Less(atCapEdge, 34f * 0.25f, "The slide at the crown's edge should be weaker than the character's own acceleration.");
            }
        }
    }
}
