namespace BikiniWipeout
{
    /// <summary>
    /// Every one-shot sound the game can request. Gameplay code only ever names an event;
    /// which clip plays (if any) is decided by the <see cref="AudioLibrary"/> asset.
    /// Values are explicit because they are serialized in the library asset: never renumber.
    /// </summary>
    public enum AudioEventId
    {
        None = 0,
        Footstep = 1,
        Jump = 2,
        Land = 3,
        ObstacleImpact = 4,
        RagdollCollision = 5,
        WaterSplash = 6,
        CheckpointActivated = 7,
        FinishCelebration = 8,
        UiClick = 9,
        UiHover = 10,
        CountdownTick = 11,
        CountdownGo = 12,

        // Phase 3 (arena and Course 1)
        BallBounce = 13,
        MechanismWhoosh = 14,
        CrowdCheer = 15,

        /// <summary>Looping crowd bed. Played by <see cref="ArenaAmbience"/>, not as a one-shot.</summary>
        ArenaAmbience = 16,
        ConfettiPop = 17,

        // Course 2 (Chaos Cove)
        SpringBounce = 18,
        WaterJet = 19,
        BucketWarning = 20,
        BucketDump = 21,
        SlideWhoosh = 22,
        BridgeCrack = 23,
        BridgeCollapse = 24,
        InflatableBoing = 25,

        /// <summary>Looping water-park bed for Course 2. Played by <see cref="ArenaAmbience"/>, not as a one-shot.</summary>
        TropicalAmbience = 26
    }

    public enum MusicTrackId
    {
        None = 0,
        MainMenu = 1,
        Course = 2
    }
}
