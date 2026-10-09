using System;
using System.IO;
using Game.Core;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Writes the synthesised sounds and music (<see cref="AudioSynth"/>) as WAV files under Assets/_Project/Audio and sets
    /// their import settings. <c>Ensure</c> only writes files that are missing (binary files in git: regenerating for nothing
    /// would churn LFS); <c>Rebuild</c> rewrites them all, e.g. after changing a recipe.
    /// </summary>
    public static class AudioBuilder
    {
        const string Dir = "Assets/_Project/Audio";
        public const string DashboardMusicPath = Dir + "/Music_Dashboard.wav";
        public const string DistrictMusicPath = Dir + "/Music_District.wav";

        /// <summary>File used for each effect (worker hits reuse the player's hit sound; the manager plays it quieter).</summary>
        public static string SfxPath(SfxKind kind)
        {
            var name = kind == SfxKind.HitWorker ? SfxKind.HitPlayer : kind;
            return $"{Dir}/Sfx_{name}.wav";
        }

        public static float[] Make(SfxKind kind)
        {
            switch (kind)
            {
                case SfxKind.HitPlayer:
                case SfxKind.HitWorker: return AudioSynth.Hit();
                case SfxKind.RepairHit: return AudioSynth.RepairHit();
                case SfxKind.FloorBuilt: return AudioSynth.FloorBuilt();
                case SfxKind.BuildingDone: return AudioSynth.BuildingDone();
                case SfxKind.LevelUp: return AudioSynth.LevelUp();
                case SfxKind.Buy: return AudioSynth.Buy();
                case SfxKind.ColaPickup: return AudioSynth.ColaPickup();
                case SfxKind.ColaDrink: return AudioSynth.ColaDrink();
                case SfxKind.Boost: return AudioSynth.Boost();
                case SfxKind.RepairDone: return AudioSynth.RepairDone();
                case SfxKind.Problem: return AudioSynth.Problem();
                case SfxKind.Enter: return AudioSynth.Enter();
                case SfxKind.Exit: return AudioSynth.Exit();
                case SfxKind.Click: return AudioSynth.Click();
                default: throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
            }
        }

        [MenuItem("Tools/Project/Generate Audio (missing only)")]
        public static void Ensure() => Generate(force: false);

        [MenuItem("Tools/Project/Rebuild Audio")]
        public static void Rebuild() => Generate(force: true);

        static void Generate(bool force)
        {
            EditorWire.EnsureFolder(Dir);
            foreach (SfxKind kind in Enum.GetValues(typeof(SfxKind)))
                Write(SfxPath(kind), () => Make(kind), music: false, force);
            Write(DashboardMusicPath, AudioSynth.DashboardMusic, music: true, force);
            Write(DistrictMusicPath, AudioSynth.DistrictMusic, music: true, force);
            AssetDatabase.Refresh();
        }

        static void Write(string assetPath, Func<float[]> make, bool music, bool force)
        {
            if (force || !File.Exists(assetPath))
            {
                File.WriteAllBytes(assetPath, ToWav(make()));
                AssetDatabase.ImportAsset(assetPath);
            }

            // Checked on every run: short effects are decompressed on load (no latency); music stays compressed in memory.
            var importer = (AudioImporter)AssetImporter.GetAtPath(assetPath);
            var settings = importer.defaultSampleSettings;
            var loadType = music ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
            if (settings.loadType == loadType && settings.compressionFormat == AudioCompressionFormat.Vorbis &&
                Mathf.Approximately(settings.quality, music ? 0.55f : 0.7f) && importer.forceToMono)
                return;
            settings.loadType = loadType;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = music ? 0.55f : 0.7f;
            importer.defaultSampleSettings = settings;
            importer.forceToMono = true;
            importer.SaveAndReimport();
        }

        // 16-bit PCM mono WAV.
        public static byte[] ToWav(float[] samples)
        {
            using (var stream = new MemoryStream())
            using (var w = new BinaryWriter(stream))
            {
                var dataBytes = samples.Length * 2;
                w.Write(new[] { 'R', 'I', 'F', 'F' });
                w.Write(36 + dataBytes);
                w.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
                w.Write(16);
                w.Write((short)1);                       // PCM
                w.Write((short)1);                       // mono
                w.Write(AudioSynth.Rate);
                w.Write(AudioSynth.Rate * 2);            // byte rate
                w.Write((short)2);                       // block align
                w.Write((short)16);
                w.Write(new[] { 'd', 'a', 't', 'a' });
                w.Write(dataBytes);
                foreach (var s in samples) w.Write((short)Mathf.RoundToInt(Mathf.Clamp(s, -1f, 1f) * 32767f));
                return stream.ToArray();
            }
        }
    }
}
