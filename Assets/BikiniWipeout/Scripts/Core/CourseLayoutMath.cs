using System;
using System.Collections.Generic;

namespace BikiniWipeout.Core
{
    /// <summary>
    /// Geometry shared by course layouts: gaps between surfaces, world positions and route length.
    /// Works for any set of legs, so future courses can reuse it (Course 1 keeps its own copy untouched).
    /// </summary>
    public static class CourseLayoutMath
    {
        private const int Samples = 21;

        /// <summary>World-space centre of a surface. <paramref name="travel"/> is 0..1 along a moving platform's path.</summary>
        public static void WorldCenter(CourseLeg[] legs, CourseSurface surface, float travel, out float x, out float z)
        {
            legs[surface.Leg].ToWorld(surface.X + surface.MoveX * travel, surface.Z + surface.MoveZ * travel, out x, out z);
        }

        /// <summary>World-space half extents of a box surface (width and length swap on legs turned 90 degrees).</summary>
        public static void WorldHalfExtents(CourseLeg[] legs, CourseSurface surface, out float halfX, out float halfZ)
        {
            bool turned = Math.Abs(legs[surface.Leg].YawDegrees) % 180 == 90;
            halfX = (turned ? surface.Length : surface.Width) * 0.5f;
            halfZ = (turned ? surface.Width : surface.Length) * 0.5f;
        }

        /// <summary>Smallest edge-to-edge gap over the movement cycle: what a player who waits for the right moment faces.</summary>
        public static float Gap(CourseLeg[] legs, CourseSurface a, CourseSurface b)
        {
            return Extreme(legs, a, b, false);
        }

        /// <summary>Largest gap over the movement cycle (the worst moment to jump).</summary>
        public static float WorstGap(CourseLeg[] legs, CourseSurface a, CourseSurface b)
        {
            return Extreme(legs, a, b, true);
        }

        /// <summary>Length of the route through the centres of the surfaces, in metres.</summary>
        public static float RouteLength(CourseLeg[] legs, IReadOnlyList<CourseSurface> surfaces)
        {
            float total = 0f;
            for (int i = 1; i < surfaces.Count; i++)
            {
                WorldCenter(legs, surfaces[i - 1], 0f, out float ax, out float az);
                WorldCenter(legs, surfaces[i], 0f, out float bx, out float bz);
                float dy = surfaces[i].Height - surfaces[i - 1].Height;
                total += (float)Math.Sqrt((bx - ax) * (bx - ax) + (bz - az) * (bz - az) + dy * dy);
            }

            return total;
        }

        /// <summary>True if the leg-local point lies on the surface's footprint (at the start of its movement).</summary>
        public static bool Contains(CourseSurface surface, float localX, float localZ, float margin = 0f)
        {
            float dx = localX - surface.X;
            float dz = localZ - surface.Z;
            if (surface.Shape == SurfaceShape.Disc)
            {
                float radius = Math.Max(surface.Radius, surface.BodyRadius) + margin;
                return dx * dx + dz * dz <= radius * radius;
            }

            return Math.Abs(dx) <= surface.Width * 0.5f + margin && Math.Abs(dz) <= surface.Length * 0.5f + margin;
        }

        private static float Extreme(CourseLeg[] legs, CourseSurface a, CourseSurface b, bool worst)
        {
            float result = worst ? float.MinValue : float.MaxValue;
            int stepsA = a.IsMoving ? Samples : 1;
            int stepsB = b.IsMoving ? Samples : 1;
            for (int i = 0; i < stepsA; i++)
            {
                for (int j = 0; j < stepsB; j++)
                {
                    float ta = stepsA == 1 ? 0f : i / (float)(stepsA - 1);
                    float tb = stepsB == 1 ? 0f : j / (float)(stepsB - 1);
                    float gap = GapAt(legs, a, ta, b, tb);
                    result = worst ? Math.Max(result, gap) : Math.Min(result, gap);
                }
            }

            return result;
        }

        private static float GapAt(CourseLeg[] legs, CourseSurface a, float ta, CourseSurface b, float tb)
        {
            WorldCenter(legs, a, ta, out float ax, out float az);
            WorldCenter(legs, b, tb, out float bx, out float bz);

            if (a.Shape == SurfaceShape.Disc && b.Shape == SurfaceShape.Disc)
            {
                return Distance(ax - bx, az - bz) - a.Radius - b.Radius;
            }

            if (a.Shape == SurfaceShape.Box && b.Shape == SurfaceShape.Box)
            {
                WorldHalfExtents(legs, a, out float ahx, out float ahz);
                WorldHalfExtents(legs, b, out float bhx, out float bhz);
                float dx = Math.Max(0f, Math.Abs(ax - bx) - ahx - bhx);
                float dz = Math.Max(0f, Math.Abs(az - bz) - ahz - bhz);
                return Distance(dx, dz);
            }

            bool aIsBox = a.Shape == SurfaceShape.Box;
            CourseSurface box = aIsBox ? a : b;
            float boxX = aIsBox ? ax : bx;
            float boxZ = aIsBox ? az : bz;
            float discX = aIsBox ? bx : ax;
            float discZ = aIsBox ? bz : az;
            float radius = aIsBox ? b.Radius : a.Radius;
            WorldHalfExtents(legs, box, out float hx, out float hz);
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
