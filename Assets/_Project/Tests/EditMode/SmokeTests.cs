using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using Game.World;
using Game.Core;
using Game.Editor;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Tests
{
    /// <summary>
    /// Smoke tests for the M0 setup: if any of these fail, the project is not in the state the rest
    /// of the roadmap assumes (asmdef wiring, serialization, input backend, scene order).
    /// </summary>
    public class SmokeTests
    {
        [Test]
        public void RuntimeAssembly_IsWiredIntoTestAssembly()
        {
            Assert.AreEqual("Game.Runtime", typeof(Economy).Assembly.GetName().Name);
        }

        [Test]
        public void Serialization_IsForceText()
        {
            Assert.AreEqual(SerializationMode.ForceText, EditorSettings.serializationMode);
        }

        [Test]
        public void MetaFiles_AreVisible()
        {
            Assert.AreEqual(ProjectSettingsApplier.VisibleMetaFiles, VersionControlSettings.mode);
        }

        [Test]
        public void InputHandling_IsInputSystemOnly()
        {
            Assert.AreEqual(ProjectSettingsApplier.InputSystemOnly, ProjectSettingsApplier.GetActiveInputHandler());
        }

        [Test]
        public void BuildSettings_ContainOnlyTheMainScene()
        {
            var paths = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            CollectionAssert.AreEqual(new[] { GameSceneBuilder.ScenePath }, paths);
        }

        [Test]
        public void MainScene_Exists()
        {
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<SceneAsset>(GameSceneBuilder.ScenePath));
        }

        [Test]
        public void MainScene_HasOneDistrictPerBalanceEntry_WithTheirPlots()
        {
            var scene = EditorSceneManager.OpenScene(GameSceneBuilder.ScenePath, OpenSceneMode.Additive);
            try
            {
                var districts = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<District>(true)).ToArray();
                Assert.AreEqual(Balance.Districts.Length, districts.Length);
                foreach (var district in districts) Assert.AreEqual(Balance.Districts[System.Array.IndexOf(districts, district)].Plans.Length, district.Sites.Length, district.name);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void SettingsWindow_DimLayerIsNotAParentOfThePanel()
        {
            // Clicks on a non-interactive child bubble up to the nearest parent handler: if the "tap outside to close" button were
            // a parent of the panel, every tap inside the panel (or on a slider's track) would close the window.
            var scene = EditorSceneManager.OpenScene(GameSceneBuilder.ScenePath, OpenSceneMode.Additive);
            try
            {
                var panel = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Game.UI.SettingsPanel>(true)).Single();
                var dim = panel.GetComponentsInChildren<Button>(true).Single(b => b.name == "Dim");
                foreach (var graphic in panel.GetComponentsInChildren<Graphic>(true))
                {
                    if (graphic.gameObject == dim.gameObject) continue;
                    Assert.IsFalse(graphic.transform.IsChildOf(dim.transform), $"{graphic.name} sits under the dim layer");
                }
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void MainScene_HasAScreenFade_ThatNeverBlocksInput()
        {
            var scene = EditorSceneManager.OpenScene(GameSceneBuilder.ScenePath, OpenSceneMode.Additive);
            try
            {
                var fade = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Game.UI.ScreenFade>(true)).Single();
                var image = fade.GetComponent<Image>();
                Assert.IsFalse(image.raycastTarget, "the fade must not swallow taps");
                var game = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<GameController>(true)).Single();
                Assert.AreSame(fade, new SerializedObject(game).FindProperty("fade").objectReferenceValue);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
