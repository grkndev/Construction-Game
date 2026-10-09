using System;
using System.Linq;
using Game.Audio;
using Game.Core;
using Game.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Tests
{
    public class AudioSynthTests
    {
        static readonly SfxKind[] AllKinds = (SfxKind[])Enum.GetValues(typeof(SfxKind));

        static float Peak(float[] s) => s.Max(Math.Abs);
        static float Rms(float[] s) => (float)Math.Sqrt(s.Sum(x => (double)x * x) / s.Length);

        [Test]
        public void EveryEffect_IsAudible_NotClipping_AndShort()
        {
            foreach (var kind in AllKinds)
            {
                var samples = AudioBuilder.Make(kind);
                Assert.Greater(samples.Length, AudioSynth.Rate / 20, $"{kind} too short");   // > 50 ms
                Assert.Less(samples.Length, AudioSynth.Rate * 2, $"{kind} too long for an effect");
                Assert.LessOrEqual(Peak(samples), 1f, $"{kind} clips");
                Assert.Greater(Rms(samples), 0.01f, $"{kind} is nearly silent");
                Assert.IsFalse(samples.Any(float.IsNaN), $"{kind} has NaN");
            }
        }

        [Test]
        public void Synth_IsDeterministic()
        {
            CollectionAssert.AreEqual(AudioSynth.Hit(), AudioSynth.Hit());
            CollectionAssert.AreEqual(AudioSynth.DistrictMusic(), AudioSynth.DistrictMusic());
        }

        [TestCase(true)]
        [TestCase(false)]
        public void Music_IsExactlyEightBars_AndLoopsWithoutAClick(bool dashboard)
        {
            var bpm = dashboard ? AudioSynth.DashboardBpm : AudioSynth.DistrictBpm;
            var samples = dashboard ? AudioSynth.DashboardMusic() : AudioSynth.DistrictMusic();

            Assert.AreEqual(AudioSynth.MusicSamples(bpm), samples.Length);
            Assert.AreEqual(32 * 60.0 / bpm, samples.Length / (double)AudioSynth.Rate, 0.01);
            Assert.LessOrEqual(Peak(samples), 1f);
            Assert.Greater(Rms(samples), 0.03f);
            // The loop point must not jump: the last sample flows into the first like any two neighbours.
            Assert.Less(Math.Abs(samples[samples.Length - 1] - samples[0]), 0.2f);
        }

        [Test]
        public void Wav_HasAValidHeaderAndTheRightSize()
        {
            var samples = new float[100];
            var wav = AudioBuilder.ToWav(samples);
            Assert.AreEqual(44 + 200, wav.Length);
            Assert.AreEqual("RIFF", System.Text.Encoding.ASCII.GetString(wav, 0, 4));
            Assert.AreEqual("WAVE", System.Text.Encoding.ASCII.GetString(wav, 8, 4));
            Assert.AreEqual(AudioSynth.Rate, BitConverter.ToInt32(wav, 24));
        }

        [Test]
        public void EverySound_ExistsAsAnImportedClip()
        {
            foreach (var kind in AllKinds)
                Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<AudioClip>(AudioBuilder.SfxPath(kind)), kind.ToString());
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<AudioClip>(AudioBuilder.DashboardMusicPath));
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<AudioClip>(AudioBuilder.DistrictMusicPath));
        }

        [Test]
        public void MainScene_AudioManager_HasEveryClipAssigned()
        {
            var scene = EditorSceneManager.OpenScene(GameSceneBuilder.ScenePath, OpenSceneMode.Additive);
            try
            {
                var manager = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<AudioManager>(true)).Single();
                var so = new SerializedObject(manager);
                var clips = so.FindProperty("sfx");
                Assert.AreEqual(AllKinds.Length, clips.arraySize);
                for (var i = 0; i < clips.arraySize; i++)
                    Assert.IsNotNull(clips.GetArrayElementAtIndex(i).objectReferenceValue, AllKinds[i].ToString());
                Assert.IsNotNull(so.FindProperty("musicDashboard").objectReferenceValue);
                Assert.IsNotNull(so.FindProperty("musicDistrict").objectReferenceValue);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
