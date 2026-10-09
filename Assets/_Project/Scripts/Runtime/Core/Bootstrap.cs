using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Core
{
    /// <summary>
    /// Single entry point of the game. Services (pause, speed modifiers, task director...) will be
    /// constructed here in later milestones instead of being scattered across singletons.
    /// Scene index 0 in Build Settings, so a standalone build always starts here.
    /// </summary>
    public sealed class Bootstrap : MonoBehaviour
    {
        public const string DefaultFirstScene = "Playground";

        [SerializeField] string firstScene = DefaultFirstScene;

        void Start()
        {
            // TODO(M1): build the service container here before loading the first scene.
            if (!Application.CanStreamedLevelBeLoaded(firstScene))
            {
                Debug.LogWarning($"[Bootstrap] Scene '{firstScene}' is not in Build Settings.");
                return;
            }
            SceneManager.LoadScene(firstScene);
        }
    }
}
