using UnityEngine;

namespace Game.Core
{
    /// <summary>The pitch of the "floor built" note: every floor of a building is one step up a pentatonic scale, so a tall building sounds like climbing stairs.</summary>
    public static class FloorPitch
    {
        static readonly int[] Steps = { 0, 2, 4, 7, 9 };   // major pentatonic, in semitones
        const int MaxSemitones = 24;

        /// <summary>Playback pitch for floor <paramref name="floor"/> (1 = the first floor = normal pitch).</summary>
        public static float Multiplier(int floor)
        {
            var index = Mathf.Max(0, floor - 1);
            var semitones = Mathf.Min(MaxSemitones, Steps[index % Steps.Length] + 12 * (index / Steps.Length));
            return Mathf.Pow(2f, semitones / 12f);
        }
    }
}
