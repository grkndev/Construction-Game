using System;

namespace Game.Editor
{
    /// <summary>
    /// Builds the game's sounds and music from maths: small deterministic synth voices (bells, plucks, sweeps, filtered noise)
    /// mixed into float buffers. <see cref="AudioBuilder"/> writes them out as WAV assets, so nothing is licensed or downloaded.
    /// Same inputs always give the same samples (noise comes from a fixed-seed generator), so regenerating never changes a file.
    /// </summary>
    public static class AudioSynth
    {
        public const int Rate = 22050;
        const double Tau = Math.PI * 2.0;

        // ---------------------------------------------------------------- music

        public const double DashboardBpm = 84.0;
        public const double DistrictBpm = 122.0;
        public const int MusicBars = 8;

        /// <summary>Samples in one loop of a track: exactly <see cref="MusicBars"/> bars of four beats.</summary>
        public static int MusicSamples(double bpm) => (int)Math.Round(MusicBars * 4 * 60.0 / bpm * Rate);

        static readonly int[][] Chords =
        {
            new[] { 60, 64, 67 },   // C
            new[] { 55, 59, 62 },   // G
            new[] { 57, 60, 64 },   // Am
            new[] { 53, 57, 60 },   // F
        };

        // Melody notes per bar: (beat, midi note, length in beats). Everything is C major pentatonic plus the chord tones.
        static readonly (double beat, int note, double len)[][] DashboardMelody =
        {
            new[] { (0.0, 76, 1.5), (2.0, 79, 1.0), (3.0, 76, 1.0) },
            new[] { (0.0, 81, 2.0), (2.0, 79, 1.0), (3.0, 76, 1.0) },
            new[] { (0.0, 74, 1.5), (2.0, 76, 1.0), (3.0, 72, 1.0) },
            new[] { (0.0, 74, 2.0), (2.0, 76, 1.0), (3.0, 79, 1.0) },
            new[] { (0.0, 79, 1.5), (2.0, 81, 1.0), (3.0, 79, 1.0) },
            new[] { (0.0, 76, 2.0), (2.0, 72, 1.0), (3.0, 76, 1.0) },
            new[] { (0.0, 77, 1.5), (2.0, 76, 1.0), (3.0, 74, 1.0) },
            new[] { (0.0, 74, 1.0), (1.0, 76, 1.0), (2.0, 72, 2.0) },
        };

        static readonly (double beat, int note, double len)[][] DistrictMelody =
        {
            new[] { (0.0, 79, 0.5), (0.5, 76, 0.5), (1.0, 72, 0.5), (1.5, 76, 0.5), (2.0, 79, 1.0), (3.0, 81, 1.0) },
            new[] { (0.0, 79, 0.5), (0.5, 74, 0.5), (1.0, 79, 0.5), (1.5, 81, 0.5), (2.0, 79, 2.0) },
            new[] { (0.0, 76, 0.5), (0.5, 81, 0.5), (1.0, 79, 0.5), (1.5, 76, 0.5), (2.0, 72, 1.0), (3.0, 76, 1.0) },
            new[] { (0.0, 77, 0.5), (0.5, 76, 0.5), (1.0, 74, 0.5), (1.5, 72, 0.5), (2.0, 74, 2.0) },
        };

        /// <summary>Calm, plucked-guitar-like loop for the map screen.</summary>
        public static float[] DashboardMusic()
        {
            var length = MusicSamples(DashboardBpm);
            var mix = new float[length];
            var beat = 60.0 / DashboardBpm;

            for (var bar = 0; bar < MusicBars; bar++)
            {
                var chord = Chords[bar % 4];
                var barStart = bar * 4 * beat;

                // Rolling arpeggio, eighth notes.
                var pattern = new[] { 0, 1, 2, 1, 0, 1, 2, 1 };
                for (var i = 0; i < 8; i++)
                    Mix(mix, barStart + i * beat * 0.5, Pluck(Hz(chord[pattern[i]] + 12), beat * 1.6, 4.5), 0.34f, wrap: true);

                // Soft bass on beats 1 and 3.
                Mix(mix, barStart, Pluck(Hz(chord[0] - 24), beat * 2.0, 2.2, sine: true), 0.55f, wrap: true);
                Mix(mix, barStart + 2 * beat, Pluck(Hz(chord[0] - 17), beat * 2.0, 2.2, sine: true), 0.4f, wrap: true);

                foreach (var (b, note, len) in DashboardMelody[bar])
                    Mix(mix, barStart + b * beat, Bell(Hz(note), len * beat + 0.6, 3.2), 0.30f, wrap: true);
            }
            return Normalize(mix, 0.8f);
        }

        /// <summary>Bouncy loop for working inside a district: kick, hats, bass, offbeat stabs and a pentatonic tune.</summary>
        public static float[] DistrictMusic()
        {
            var length = MusicSamples(DistrictBpm);
            var mix = new float[length];
            var beat = 60.0 / DistrictBpm;
            var hat = HighNoise(0.05, 70.0, seed: 11);
            var hatSoft = HighNoise(0.04, 90.0, seed: 17);

            for (var bar = 0; bar < MusicBars; bar++)
            {
                var chord = Chords[bar % 4];
                var barStart = bar * 4 * beat;

                // Four on the floor kick would be too heavy: beats 1 and 3, like a march.
                Mix(mix, barStart, Kick(), 0.75f, wrap: true);
                Mix(mix, barStart + 2 * beat, Kick(), 0.65f, wrap: true);

                for (var i = 0; i < 4; i++)
                {
                    var note = i == 2 ? chord[0] - 17 : chord[0] - 24;   // root, root, fifth, root
                    Mix(mix, barStart + i * beat, Pluck(Hz(note), beat * 0.9, 7.0), 0.5f, wrap: true);
                    Mix(mix, barStart + (i + 0.5) * beat, hat, 0.22f, wrap: true);
                    Mix(mix, barStart + i * beat, hatSoft, 0.1f, wrap: true);
                }

                // Syncopated chord stabs on the "and" of 2 and 4.
                foreach (var at in new[] { 1.5, 3.5 })
                    foreach (var n in chord)
                        Mix(mix, barStart + at * beat, Pluck(Hz(n + 12), beat * 0.5, 13.0), 0.16f, wrap: true);

                foreach (var (b, note, len) in DistrictMelody[bar % 4])
                    Mix(mix, barStart + b * beat, Pluck(Hz(note), len * beat + 0.25, 6.5), 0.36f, wrap: true);
            }
            return Normalize(mix, 0.8f);
        }

        // ---------------------------------------------------------------- sound effects

        /// <summary>Hammer on wood and nails: a low thump, a short click and a quick metallic ring.</summary>
        public static float[] Hit()
        {
            var mix = new float[Samples(0.28)];
            Mix(mix, 0, Sweep(210, 65, 0.16, 18.0), 0.9f);
            Mix(mix, 0, LowNoise(0.04, 1800.0, 90.0, seed: 3), 0.55f);
            Mix(mix, 0, Bell(1180, 0.2, 26.0), 0.16f);
            Mix(mix, 0, Bell(1770, 0.14, 34.0), 0.1f);
            return Normalize(mix, 0.85f);
        }

        /// <summary>A lighter, higher "tink" so repairing sounds different from building.</summary>
        public static float[] RepairHit()
        {
            var mix = new float[Samples(0.22)];
            Mix(mix, 0, Bell(1560, 0.2, 24.0), 0.55f);
            Mix(mix, 0, Bell(2340, 0.15, 30.0), 0.3f);
            Mix(mix, 0, LowNoise(0.02, 4000.0, 140.0, seed: 5), 0.35f);
            return Normalize(mix, 0.8f);
        }

        public static float[] FloorBuilt()
        {
            var mix = new float[Samples(0.55)];
            Mix(mix, 0.00, Bell(Hz(72), 0.5, 7.0), 0.6f);
            Mix(mix, 0.09, Bell(Hz(76), 0.45, 7.0), 0.55f);
            return Normalize(mix, 0.8f);
        }

        public static float[] BuildingDone()
        {
            var mix = new float[Samples(1.5)];
            var notes = new[] { 72, 76, 79, 84 };
            for (var i = 0; i < notes.Length; i++) Mix(mix, i * 0.11, Bell(Hz(notes[i]), 0.9, 4.0), 0.5f);
            foreach (var n in new[] { 72, 76, 79 }) Mix(mix, 0.45, Pluck(Hz(n), 1.0, 2.6), 0.22f);
            Mix(mix, 0.45, HighNoise(0.5, 6.0, seed: 21), 0.06f);   // a little glitter
            return Normalize(mix, 0.85f);
        }

        public static float[] LevelUp()
        {
            var mix = new float[Samples(1.3)];
            var notes = new[] { 72, 74, 76, 79, 84 };
            for (var i = 0; i < notes.Length; i++) Mix(mix, i * 0.09, Bell(Hz(notes[i]), 0.5, 6.0), 0.45f);
            foreach (var n in new[] { 72, 76, 79, 84 }) Mix(mix, 0.5, Bell(Hz(n), 0.8, 3.5), 0.3f);
            return Normalize(mix, 0.85f);
        }

        /// <summary>"Ka-ching": two bright pings and a shimmer.</summary>
        public static float[] Buy()
        {
            var mix = new float[Samples(0.6)];
            Mix(mix, 0.00, Bell(Hz(88), 0.4, 9.0), 0.5f);
            Mix(mix, 0.07, Bell(Hz(95), 0.5, 7.0), 0.55f);
            Mix(mix, 0.07, HighNoise(0.3, 12.0, seed: 31), 0.1f);
            return Normalize(mix, 0.8f);
        }

        public static float[] ColaPickup()
        {
            var mix = new float[Samples(0.25)];
            Mix(mix, 0, Sweep(480, 1150, 0.14, 9.0), 0.7f);
            Mix(mix, 0.1, Bell(1500, 0.14, 20.0), 0.3f);
            return Normalize(mix, 0.8f);
        }

        /// <summary>A can being opened (fizz) followed by two gulps.</summary>
        public static float[] ColaDrink()
        {
            var mix = new float[Samples(0.8)];
            Mix(mix, 0, HighNoise(0.4, 4.5, seed: 41), 0.4f);
            Mix(mix, 0.32, Sweep(230, 130, 0.12, 10.0), 0.7f);
            Mix(mix, 0.46, Sweep(210, 110, 0.14, 9.0), 0.7f);
            return Normalize(mix, 0.8f);
        }

        /// <summary>Coşku: a rising whoosh that ends in sparkles.</summary>
        public static float[] Boost()
        {
            var mix = new float[Samples(0.9)];
            Mix(mix, 0, Sweep(260, 1250, 0.5, 1.5), 0.45f);
            Mix(mix, 0, HighNoise(0.5, 3.0, seed: 51), 0.12f);
            var notes = new[] { 84, 88, 91, 96 };
            for (var i = 0; i < notes.Length; i++) Mix(mix, 0.42 + i * 0.07, Bell(Hz(notes[i]), 0.4, 8.0), 0.35f);
            return Normalize(mix, 0.85f);
        }

        public static float[] RepairDone()
        {
            var mix = new float[Samples(0.9)];
            var notes = new[] { 79, 84, 88 };
            for (var i = 0; i < notes.Length; i++) Mix(mix, i * 0.1, Bell(Hz(notes[i]), 0.6, 5.5), 0.5f);
            return Normalize(mix, 0.85f);
        }

        /// <summary>Two soft falling notes: something needs attention, nothing is wrong with the player.</summary>
        public static float[] Problem()
        {
            var mix = new float[Samples(0.6)];
            Mix(mix, 0.0, Pluck(Hz(76), 0.3, 6.0), 0.5f);
            Mix(mix, 0.2, Pluck(Hz(72), 0.4, 5.0), 0.5f);
            return Normalize(mix, 0.7f);
        }

        public static float[] Enter()
        {
            var mix = new float[Samples(0.45)];
            Mix(mix, 0, Sweep(180, 620, 0.32, 3.0), 0.4f);
            Mix(mix, 0, LowNoise(0.35, 2500.0, 4.0, seed: 61), 0.35f);
            return Normalize(mix, 0.75f);
        }

        public static float[] Exit()
        {
            var mix = new float[Samples(0.45)];
            Mix(mix, 0, Sweep(620, 180, 0.32, 3.0), 0.4f);
            Mix(mix, 0, LowNoise(0.35, 2500.0, 4.0, seed: 71), 0.35f);
            return Normalize(mix, 0.75f);
        }

        public static float[] Click()
        {
            var mix = new float[Samples(0.08)];
            Mix(mix, 0, Sweep(1100, 700, 0.05, 55.0), 0.8f);
            Mix(mix, 0, LowNoise(0.01, 3000.0, 200.0, seed: 81), 0.3f);
            return Normalize(mix, 0.6f);
        }

        // ---------------------------------------------------------------- voices

        static int Samples(double seconds) => (int)Math.Ceiling(seconds * Rate);

        public static double Hz(int midiNote) => 440.0 * Math.Pow(2.0, (midiNote - 69) / 12.0);

        // Mallet-like bell: a sine with two quickly dying overtones.
        static float[] Bell(double freq, double seconds, double decay)
        {
            var n = Samples(seconds);
            var v = new float[n];
            for (var i = 0; i < n; i++)
            {
                var t = (double)i / Rate;
                var s = Math.Sin(Tau * freq * t)
                        + 0.35 * Math.Sin(Tau * freq * 2.01 * t) * Math.Exp(-t * decay * 1.5)
                        + 0.15 * Math.Sin(Tau * freq * 3.99 * t) * Math.Exp(-t * decay * 2.5);
                v[i] = (float)(s * Attack(t) * Math.Exp(-t * decay));
            }
            return v;
        }

        // Plucked string-ish voice: triangle (or sine) plus a faint octave, exponential decay.
        static float[] Pluck(double freq, double seconds, double decay, bool sine = false)
        {
            var n = Samples(seconds);
            var v = new float[n];
            for (var i = 0; i < n; i++)
            {
                var t = (double)i / Rate;
                var p = freq * t;
                var wave = sine ? Math.Sin(Tau * p) : Triangle(p) + 0.25 * Triangle(p * 2.0);
                v[i] = (float)(wave * Attack(t) * Math.Exp(-t * decay));
            }
            return v;
        }

        // Sine gliding from f0 to f1 over `glide` seconds, then holding f1; exponential amplitude decay.
        static float[] Sweep(double f0, double f1, double seconds, double decay)
        {
            var n = Samples(seconds);
            var v = new float[n];
            var phase = 0.0;
            for (var i = 0; i < n; i++)
            {
                var t = (double)i / Rate;
                var f = f0 + (f1 - f0) * Math.Min(1.0, t / seconds);
                phase += f / Rate;
                v[i] = (float)(Math.Sin(Tau * phase) * Attack(t) * Math.Exp(-t * decay));
            }
            return v;
        }

        static float[] Kick()
        {
            var n = Samples(0.22);
            var v = new float[n];
            var phase = 0.0;
            for (var i = 0; i < n; i++)
            {
                var t = (double)i / Rate;
                phase += (46.0 + 90.0 * Math.Exp(-t * 28.0)) / Rate;
                v[i] = (float)(Math.Sin(Tau * phase) * Math.Exp(-t * 16.0));
            }
            return v;
        }

        // White noise through a one-pole low-pass, decaying.
        static float[] LowNoise(double seconds, double cutoff, double decay, int seed)
        {
            var n = Samples(seconds);
            var v = new float[n];
            var rng = new Lcg(seed);
            var a = Math.Exp(-Tau * cutoff / Rate);
            var y = 0.0;
            for (var i = 0; i < n; i++)
            {
                var t = (double)i / Rate;
                y = (1.0 - a) * rng.Next() + a * y;
                v[i] = (float)(y * 3.0 * Math.Exp(-t * decay));
            }
            return v;
        }

        // High-passed noise (noise minus its low-passed copy), decaying: hats, fizz, glitter.
        static float[] HighNoise(double seconds, double decay, int seed)
        {
            var n = Samples(seconds);
            var v = new float[n];
            var rng = new Lcg(seed);
            var a = Math.Exp(-Tau * 5200.0 / Rate);
            var low = 0.0;
            for (var i = 0; i < n; i++)
            {
                var t = (double)i / Rate;
                var x = rng.Next();
                low = (1.0 - a) * x + a * low;
                v[i] = (float)((x - low) * Math.Exp(-t * decay));
            }
            return v;
        }

        static double Triangle(double cycles) => 4.0 * Math.Abs(cycles - Math.Floor(cycles + 0.5)) - 1.0;

        // 3 ms fade-in so notes never start with a click.
        static double Attack(double t) => Math.Min(1.0, t / 0.003);

        // Adds `voice` into `mix` starting at `startSeconds`. With wrap the tail continues at the start (seamless music loops).
        static void Mix(float[] mix, double startSeconds, float[] voice, float gain, bool wrap = false)
        {
            var start = (int)Math.Round(startSeconds * Rate);
            for (var i = 0; i < voice.Length; i++)
            {
                var index = start + i;
                if (index >= mix.Length)
                {
                    if (!wrap) break;
                    index %= mix.Length;
                }
                mix[index] += voice[i] * gain;
            }
        }

        static float[] Normalize(float[] samples, float peak)
        {
            var max = 0f;
            foreach (var s in samples) max = Math.Max(max, Math.Abs(s));
            if (max < 1e-6f) return samples;
            var k = peak / max;
            for (var i = 0; i < samples.Length; i++) samples[i] *= k;
            return samples;
        }

        // Small deterministic noise source (white, -1..1).
        sealed class Lcg
        {
            uint state;
            public Lcg(int seed) => state = (uint)seed * 2654435761u + 12345u;
            public double Next()
            {
                state = state * 1664525u + 1013904223u;
                return (state >> 8) / (double)(1 << 23) - 1.0;
            }
        }
    }
}
