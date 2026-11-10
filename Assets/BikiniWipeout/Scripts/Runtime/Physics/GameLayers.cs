using UnityEngine;

namespace BikiniWipeout
{
    /// <summary>
    /// Single source of truth for physics layers and collision rules.
    /// Layer names live in ProjectSettings/TagManager.asset and must match the indices here;
    /// the collision matrix is re-applied at startup so the rules below always win.
    /// </summary>
    public static class GameLayers
    {
        public const int Default = 0;

        /// <summary>The character's CharacterController capsule.</summary>
        public const int Player = 6;

        /// <summary>Ragdoll bones. Never collides with Player or with itself.</summary>
        public const int Ragdoll = 7;

        /// <summary>Static walkable geometry.</summary>
        public const int Ground = 8;

        /// <summary>Sweeper arms, balls and their impact zones.</summary>
        public const int Obstacle = 9;

        /// <summary>Kinematic platforms that carry the character.</summary>
        public const int MovingPlatform = 10;

        /// <summary>Gameplay trigger volumes: water, checkpoints, finish line.</summary>
        public const int Trigger = 11;

        /// <summary>Invisible walls around the course.</summary>
        public const int Boundary = 12;

        public static readonly int[] GameplayLayers =
        {
            Player, Ragdoll, Ground, Obstacle, MovingPlatform, Trigger, Boundary
        };

        /// <summary>Surfaces the character can stand on.</summary>
        public static readonly int StandableMask = Mask(Default, Ground, MovingPlatform, Obstacle);

        /// <summary>Surfaces it is safe to recover or respawn on (no obstacles).</summary>
        public static readonly int SafeGroundMask = Mask(Default, Ground, MovingPlatform);

        /// <summary>Everything that blocks the character capsule.</summary>
        public static readonly int SolidMask = Mask(Default, Ground, MovingPlatform, Obstacle, Boundary);

        /// <summary>Geometry the camera should not clip through.</summary>
        public static readonly int CameraObstructionMask = Mask(Default, Ground);

        public static string NameOf(int layer)
        {
            switch (layer)
            {
                case Player: return "Player";
                case Ragdoll: return "Ragdoll";
                case Ground: return "Ground";
                case Obstacle: return "Obstacle";
                case MovingPlatform: return "MovingPlatform";
                case Trigger: return "Trigger";
                case Boundary: return "Boundary";
                default: return null;
            }
        }

        public static int Mask(params int[] layers)
        {
            int mask = 0;
            for (int i = 0; i < layers.Length; i++)
            {
                mask |= 1 << layers[i];
            }

            return mask;
        }

        public static bool IsGameplayLayer(int layer)
        {
            return layer >= Player && layer <= Boundary;
        }

        /// <summary>The collision rule for a pair of layers.</summary>
        public static bool Collides(int a, int b)
        {
            bool aGame = IsGameplayLayer(a);
            bool bGame = IsGameplayLayer(b);

            if (aGame && bGame)
            {
                return CollidesGameplay(a, b) || CollidesGameplay(b, a);
            }

            if (aGame || bGame)
            {
                // Built-in / unused layers behave like Default, except triggers only see characters.
                return a != Trigger && b != Trigger;
            }

            return true;
        }

        private static bool CollidesGameplay(int a, int b)
        {
            switch (a)
            {
                case Player:
                case Ragdoll:
                    return b == Ground || b == Obstacle || b == MovingPlatform || b == Trigger || b == Boundary;
                case Obstacle:
                    return b == Obstacle || b == Ground || b == MovingPlatform || b == Boundary;
                default:
                    return false;
            }
        }

        /// <summary>Writes the rules into the physics layer collision matrix.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void ApplyCollisionMatrix()
        {
            for (int i = 0; i < GameplayLayers.Length; i++)
            {
                int a = GameplayLayers[i];
                for (int b = 0; b < 32; b++)
                {
                    Physics.IgnoreLayerCollision(a, b, !Collides(a, b));
                }
            }
        }

        public static void SetLayerRecursively(GameObject root, int layer)
        {
            root.layer = layer;
            Transform t = root.transform;
            for (int i = 0; i < t.childCount; i++)
            {
                SetLayerRecursively(t.GetChild(i).gameObject, layer);
            }
        }
    }
}
