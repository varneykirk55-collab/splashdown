using UnityEngine;

namespace BikiniWipeout
{
    public enum ImpactSourceKind
    {
        Generic,
        Sweeper,
        Ball,
        Platform
    }

    /// <summary>Everything a receiver needs to react to being hit. Obstacles fill it in; they never touch the ragdoll.</summary>
    public struct ImpactData
    {
        /// <summary>World-space point on the obstacle surface closest to the receiver.</summary>
        public Vector3 Point;

        /// <summary>Unit vector from the obstacle towards the receiver.</summary>
        public Vector3 Normal;

        /// <summary>Velocity of the obstacle surface at <see cref="Point"/> (m/s).</summary>
        public Vector3 SourceVelocity;

        /// <summary>Per-obstacle impact tuning. 1 = natural.</summary>
        public float ForceMultiplier;

        public ImpactSourceKind Kind;
        public GameObject Source;
    }

    /// <summary>Implemented by anything that reacts to obstacle hits (the player's <see cref="KnockdownController"/>).</summary>
    public interface IImpactReceiver
    {
        /// <summary>World-space point obstacles aim at (roughly the centre of mass).</summary>
        Vector3 ImpactCenter { get; }

        /// <summary>Returns true if the impact had an effect (shove or knockdown).</summary>
        bool ReceiveImpact(in ImpactData impact);
    }
}
