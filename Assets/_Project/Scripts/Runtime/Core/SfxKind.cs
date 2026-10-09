namespace Game.Core
{
    /// <summary>Every sound effect the game can ask for (raised by <see cref="GameController"/>, played by the audio manager).</summary>
    public enum SfxKind
    {
        HitPlayer,
        HitWorker,
        RepairHit,
        FloorBuilt,
        BuildingDone,
        LevelUp,
        Buy,
        ColaPickup,
        ColaDrink,
        Boost,
        RepairDone,
        Problem,
        Enter,
        Exit,
        Click,
    }
}
