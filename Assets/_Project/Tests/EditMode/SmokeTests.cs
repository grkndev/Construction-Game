using System.Linq;
using NUnit.Framework;
using UnityEditor;
using Game.Core;
using Game.Editor;

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
    }
}
