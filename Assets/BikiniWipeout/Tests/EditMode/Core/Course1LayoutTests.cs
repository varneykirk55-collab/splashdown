using System.Collections.Generic;
using System.Linq;
using BikiniWipeout.Core;
using NUnit.Framework;

namespace BikiniWipeout.Tests
{
    /// <summary>
    /// Checks that Course 1 is physically completable with the character's real movement values:
    /// every gap between consecutive surfaces must be inside the jump envelope with margin to spare.
    /// </summary>
    public class Course1LayoutTests
    {
        /// <summary>Fraction of the theoretical jump distance a gap may use. Leaves room for imperfect timing.</summary>
        private const float RunMargin = 0.75f;
        private const float WalkMargin = 0.9f;

        private static readonly JumpEnvelope Jump = JumpEnvelope.Default;

        private static IEnumerable<(CourseSurface from, CourseSurface to)> Steps()
        {
            IReadOnlyList<CourseSurface> surfaces = Course1Layout.Surfaces;
            for (int i = 0; i + 1 < surfaces.Count; i++)
            {
                yield return (surfaces[i], surfaces[i + 1]);
            }
        }

        [Test]
        public void JumpEnvelope_MatchesHandCalculation()
        {
            // v0 = sqrt(2 * 24 * 1.4) = 8.198 m/s; up 0.3416 s; down sqrt(2 * 1.4 / 32.4) = 0.2940 s.
            Assert.AreEqual(0.6356f, Jump.AirTime(0f), 0.001f);
            Assert.AreEqual(4.13f, Jump.RunReach(0f), 0.02f);
            Assert.AreEqual(2.03f, Jump.WalkReach(0f), 0.02f);
            Assert.Less(Jump.RunReach(0.8f), Jump.RunReach(0f), "Jumping up to a higher surface covers less ground.");
            Assert.Greater(Jump.RunReach(-1f), Jump.RunReach(0f), "Dropping down covers more ground.");
            Assert.AreEqual(0f, Jump.RunReach(1.4f), "A surface at or above the jump apex is unreachable.");
        }

        [Test]
        public void Course_HasFiveSections_InOrder()
        {
            string order = new string(Course1Layout.Surfaces.Select(s => s.Section).Distinct().ToArray());
            Assert.AreEqual("ABCDE", order);
            Assert.AreEqual(5, Course1Layout.SectionNames.Length);
            foreach (char section in "ABCDE")
            {
                Assert.GreaterOrEqual(Course1Layout.Surfaces.Count(s => s.Section == section), 4, "Section " + section);
            }
        }

        [Test]
        public void Course_StartsAndEndsCorrectly_WithCheckpointsBetweenSections()
        {
            IReadOnlyList<CourseSurface> surfaces = Course1Layout.Surfaces;
            Assert.AreEqual(SurfaceKind.Start, surfaces[0].Kind);
            Assert.AreEqual(SurfaceKind.Finish, surfaces[surfaces.Count - 1].Kind);
            Assert.AreEqual(1, surfaces.Count(s => s.Kind == SurfaceKind.Finish));

            // Sections A to D each end on a checkpoint platform, so every section restarts from its own entrance.
            foreach (char section in "ABCD")
            {
                CourseSurface last = surfaces.Last(s => s.Section == section);
                Assert.AreEqual(SurfaceKind.Checkpoint, last.Kind, "Section " + section + " should end on a checkpoint platform.");
            }

            Assert.AreEqual(surfaces.Count, surfaces.Select(s => s.Name).Distinct().Count(), "Surface names must be unique.");
        }

        [Test]
        public void EveryGap_IsARealGap_NotAnOverlap()
        {
            foreach ((CourseSurface from, CourseSurface to) in Steps())
            {
                Assert.Greater(Course1Layout.Gap(from, to), 0.5f, $"{from.Name} -> {to.Name} overlap or nearly touch.");
            }
        }

        [Test]
        public void EveryGap_IsWithinARunningJump_WithMargin()
        {
            foreach ((CourseSurface from, CourseSurface to) in Steps())
            {
                float rise = to.Height - from.Height;
                float gap = Course1Layout.Gap(from, to);
                float reach = Jump.RunReach(rise) * RunMargin;
                Assert.Less(rise, Jump.JumpHeight * 0.6f, $"{from.Name} -> {to.Name}: step up of {rise:0.00} m is too high.");
                Assert.LessOrEqual(gap, reach,
                    $"{from.Name} -> {to.Name}: gap {gap:0.00} m exceeds {RunMargin:P0} of the running jump ({reach:0.00} m).");
            }
        }

        [Test]
        public void LaunchPad_IsBeginnerFriendly_EveryGapClearableAtWalkingSpeed()
        {
            foreach ((CourseSurface from, CourseSurface to) in Steps().Where(s => s.from.Section == 'A'))
            {
                float gap = Course1Layout.Gap(from, to);
                float reach = Jump.WalkReach(to.Height - from.Height) * WalkMargin;
                Assert.LessOrEqual(gap, reach, $"{from.Name} -> {to.Name}: gap {gap:0.00} m needs more than a walking jump ({reach:0.00} m).");
            }
        }

        [Test]
        public void Difficulty_Increases_FinalSectionHasTheLongestJumps()
        {
            float Longest(char section) => Steps().Where(s => s.to.Section == section).Max(s => Course1Layout.Gap(s.from, s.to));
            Assert.Less(Longest('A'), Longest('B'));
            Assert.Greater(Longest('E'), Longest('A'));
            Assert.GreaterOrEqual(Longest('E'), Longest('D'));
        }

        [Test]
        public void MovingPlatforms_RequireTiming_ButAlwaysComeBackIntoReach()
        {
            foreach ((CourseSurface from, CourseSurface to) in Steps().Where(s => s.from.IsMoving || s.to.IsMoving))
            {
                float best = Course1Layout.Gap(from, to);
                float worst = Course1Layout.WorstGap(from, to);
                Assert.GreaterOrEqual(worst, best);
                Assert.LessOrEqual(best, Jump.RunReach(0f) * RunMargin, $"{from.Name} -> {to.Name}");
            }
        }

        [Test]
        public void Surfaces_AreWideEnoughToStandOn()
        {
            foreach (CourseSurface surface in Course1Layout.Surfaces)
            {
                float smallest = surface.Shape == SurfaceShape.Disc ? surface.Radius * 2f : System.Math.Min(surface.Width, surface.Length);
                // The character capsule is 0.6 m wide; even the narrow bridges leave room either side.
                Assert.GreaterOrEqual(smallest, 2.2f, surface.Name);
                Assert.Greater(surface.Height, Course1Layout.WaterLevel + 1.5f, surface.Name + " must sit clearly above the water.");
            }
        }

        [Test]
        public void Legs_FormAConnectedU()
        {
            // Leg 0 heads +Z, leg 1 heads +X, leg 2 heads -Z.
            Course1Layout.Legs[0].ToWorld(0f, 10f, out float x0, out float z0);
            Course1Layout.Legs[1].ToWorld(0f, 10f, out float x1, out float z1);
            Course1Layout.Legs[2].ToWorld(0f, 10f, out float x2, out float z2);
            Assert.AreEqual(10f, z0 - Course1Layout.Legs[0].OriginZ, 0.001f);
            Assert.AreEqual(10f, x1 - Course1Layout.Legs[1].OriginX, 0.001f);
            Assert.AreEqual(-10f, z2 - Course1Layout.Legs[2].OriginZ, 0.001f);

            // Sideways (+x local) on the middle leg points towards world -Z.
            Course1Layout.Legs[1].ToWorld(3f, 0f, out float sx, out float sz);
            Assert.AreEqual(-3f, sz - Course1Layout.Legs[1].OriginZ, 0.001f);
            Assert.AreEqual(0f, sx - Course1Layout.Legs[1].OriginX, 0.001f);

            // The finish ends up back near the start line, on the far side of the arena.
            CourseSurface finish = Course1Layout.Surfaces[Course1Layout.Surfaces.Count - 1];
            Course1Layout.WorldCenter(finish, 0f, out float fx, out float fz);
            Assert.Greater(fx, 60f);
            Assert.That(fz, Is.InRange(-15f, 15f));
        }
    }
}
