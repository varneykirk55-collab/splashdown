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
        CountdownGo = 12
    }

    public enum MusicTrackId
    {
        None = 0,
        MainMenu = 1,
        Course = 2
    }
}
