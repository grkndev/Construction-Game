using System;

namespace Game.Core
{
    public enum EventKind { Chest, Birds, Pet }

    /// <summary>Which small event comes next and when (chest, birds, a stray cat or dog). At most one of each kind at a time. Plain logic.</summary>
    public static class EventPicker
    {
        public const float FirstMin = 25f, FirstMax = 40f, LaterMin = 50f, LaterMax = 80f;

        // Birds are the most common (cheap and cheerful), chests next, pets are the rare treat.
        const int ChestWeight = 3, BirdsWeight = 4, PetWeight = 2;

        /// <summary>The next kind to start, or null when all three are already around.</summary>
        public static EventKind? Pick(Random random, bool chestAlive, bool birdsAlive, bool petAlive)
        {
            var chest = chestAlive ? 0 : ChestWeight;
            var birds = birdsAlive ? 0 : BirdsWeight;
            var pet = petAlive ? 0 : PetWeight;
            var total = chest + birds + pet;
            if (total == 0) return null;
            var roll = random.Next(total);
            if (roll < chest) return EventKind.Chest;
            if (roll < chest + birds) return EventKind.Birds;
            return EventKind.Pet;
        }

        /// <summary>Seconds until the next event; the first one after entering a district comes sooner.</summary>
        public static float Delay(Random random, bool first)
        {
            var min = first ? FirstMin : LaterMin;
            var max = first ? FirstMax : LaterMax;
            return min + (float)random.NextDouble() * (max - min);
        }
    }
}
