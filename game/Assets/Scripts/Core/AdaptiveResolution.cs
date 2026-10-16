using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace LookImportant
{
    /// <summary>
    /// The game has no settings, so it looks after its own frame rate: if frames
    /// keep missing the display's refresh, render resolution steps down (never
    /// below minScale). It does not step back up within a session - with vsync
    /// on there is no reliable way to see headroom - so a bad patch (e.g. a
    /// shader warm-up stall) is guarded against by needing a sustained miss.
    /// </summary>
    public class AdaptiveResolution : MonoBehaviour
    {
        public float minScale = 0.7f;
        public float step = 0.1f;
        public float window = 3f;            // seconds of frames per decision
        public float missFraction = 0.25f;   // share of slow frames that triggers a step

        UniversalRenderPipelineAsset urp;
        float budgetMs, elapsed, settle = 4f;
        int frames, slow;

        void Start()
        {
            urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urp == null || Application.isEditor ||
                System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-capture") >= 0)
            {
                enabled = false;   // the editor would save the change into the asset; benchmarks measure the configured scale
                return;
            }
            double hz = Screen.currentResolution.refreshRateRatio.value;
            budgetMs = 1000f / (float)(hz > 1 ? hz : 60.0);
        }

        void Update()
        {
            if (settle > 0f) { settle -= Time.unscaledDeltaTime; return; }   // ignore start-up hitches
            float ms = Time.unscaledDeltaTime * 1000f;
            elapsed += Time.unscaledDeltaTime;
            frames++;
            if (ms > budgetMs * 1.3f) slow++;
            if (elapsed < window) return;

            if (slow > frames * missFraction && urp.renderScale > minScale + 0.01f)
            {
                urp.renderScale = Mathf.Max(minScale, urp.renderScale - step);
                Debug.Log($"[AdaptiveResolution] {slow}/{frames} frames over {budgetMs * 1.3f:F1} ms; render scale -> {urp.renderScale:F2}");
                settle = 1f;
            }
            elapsed = 0f;
            frames = slow = 0;
        }
    }
}
