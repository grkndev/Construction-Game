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
        DistrictDone,   // fanfare when the last building of a district is finished
        Stamp,          // the "stamp" thud of the district banner
        CoinLand,       // a flying coin reaches the purse (tiny, throttled)
        Chirp,          // startled birds
    }
}
