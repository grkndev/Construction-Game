using Unity.Profiling;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.UI
{
    /// <summary>
    /// A tiny FPS / frame-time / batch readout for testing on real devices. Off by default; on with the <c>-fps</c> flag, <c>?fps</c> in a web
    /// page's address, the F3 key, or three quick taps in the top-left corner (phones). Made at startup so no scene wiring is needed.
    /// </summary>
    public sealed class PerfOverlay : MonoBehaviour
    {
        const float Window = 0.5f;
        const float TapCorner = 140f;

        bool visible;
        float windowTime;
        int windowFrames;
        float worst;
        string text = "";
        ProfilerRecorder batches;
        int taps;
        float lastTap = -10f;
        GUIStyle style;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Create()
        {
            var go = new GameObject("PerfOverlay");
            DontDestroyOnLoad(go);
            go.AddComponent<PerfOverlay>();
        }

        void Awake()
        {
            visible = Core.LaunchArgs.Has("-fps") || Application.absoluteURL.Contains("fps");
            batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");
        }

        void OnDestroy() => batches.Dispose();

        void Update()
        {
            if (Keyboard.current != null && Keyboard.current.f3Key.wasPressedThisFrame) visible = !visible;
            CheckCornerTaps();
            if (!visible) return;

            var dt = Time.unscaledDeltaTime;
            windowTime += dt;
            windowFrames++;
            worst = Mathf.Max(worst, dt);
            if (windowTime < Window) return;

            var ms = windowTime / windowFrames * 1000f;
            text = $"{1000f / ms:0} fps  {ms:0.0} ms  (worst {worst * 1000f:0})" + (batches.Valid ? $"  batches {batches.LastValue}" : "");
            windowTime = 0f;
            windowFrames = 0;
            worst = 0f;
        }

        void CheckCornerTaps()
        {
            var pointer = Pointer.current;
            if (pointer == null || !pointer.press.wasPressedThisFrame) return;
            var at = pointer.position.ReadValue();
            if (at.x > TapCorner || at.y < Screen.height - TapCorner) { taps = 0; return; }
            if (Time.unscaledTime - lastTap > 1f) taps = 0;
            lastTap = Time.unscaledTime;
            if (++taps >= 3) { taps = 0; visible = !visible; }
        }

        void OnGUI()
        {
            if (!visible) return;
            style ??= new GUIStyle(GUI.skin.label) { fontSize = Mathf.Max(14, Screen.height / 40), fontStyle = FontStyle.Bold, wordWrap = false };
            // Below the camera cut-out / notch: the safe area's top edge (y counts from the bottom of the screen in Screen.safeArea).
            var top = Screen.height - Screen.safeArea.yMax;
            var rect = new Rect(Screen.safeArea.center.x - 450f, top + 8f, 900f, style.fontSize * 1.8f);
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            style.alignment = TextAnchor.MiddleCenter;
            GUI.Label(rect, text, style);
        }
    }
}
