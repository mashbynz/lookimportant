using System.Collections;
using System.Globalization;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace LookImportant
{
    /// <summary>
    /// Test hook for headless verification; inert unless the player is launched
    /// with -capture. Example:
    ///   LookImportant.x86_64 -capture out.png -capture-state live -capture-look 0,10
    /// States: rest (as the game starts), live (mains on, toggles 1 and 3 on,
    /// selector at 4, button held), dark (desk lamp off), hover (hover glow on
    /// the knife switch, selector and gauge). -capture-seconds sets how long to
    /// run (default 3). Vsync is switched off and frame times are logged, so the
    /// same hook doubles as a benchmark (tools/perf.sh).
    /// </summary>
    public class AutoCapture : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void KeepRunningUnfocused()
        {
            // Headless captures have no window focus; the game otherwise pauses when unfocused.
            if (Arg("-capture") != null) Application.runInBackground = true;
        }

        IEnumerator Start()
        {
            string path = Arg("-capture");
            if (path == null) yield break;
            string state = Arg("-capture-state") ?? "rest";
            string look = Arg("-capture-look");

            var fade = FindAnyObjectByType<StartupFade>();
            if (fade != null) fade.Skip();
            if (look != null)
            {
                var p = look.Split(',');
                GetComponent<DeskCamera>()?.OverrideLook(new Vector2(float.Parse(p[0]), float.Parse(p[1])));
            }
            else
            {
                GetComponent<DeskCamera>()?.OverrideLook(Vector2.zero);
            }

            if (state == "live")
            {
                FindAnyObjectByType<KnifeSwitch>().Set(true, instant: true);
                foreach (var t in FindObjectsByType<ToggleSwitch>(FindObjectsSortMode.None))
                    t.Set(t.name != "Toggle_2_Pivot", instant: true);
                FindAnyObjectByType<RotaryKnob>().SetIndex(3, instant: true);
                var button = FindAnyObjectByType<PushButton>();
                button.Press(default);
            }
            else if (state == "dark")
            {
                FindAnyObjectByType<DeskLamp>().Set(false, instant: true);
            }
            else if (state == "hover")
            {
                FindAnyObjectByType<KnifeSwitch>().SetHovered(true);
                FindAnyObjectByType<RotaryKnob>().SetHovered(true);
                FindAnyObjectByType<Gauge>().SetHovered(true);
            }

            ApplyPerfOverrides();

            // Measure unthrottled so a GPU run shows real headroom, not the vsync cap.
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            float seconds = float.TryParse(Arg("-capture-seconds"), out var s) ? s : 3f;
            var times = new System.Collections.Generic.List<float>();
            yield return null;   // the first frame includes scene start-up
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                yield return null;
                elapsed += Time.unscaledDeltaTime;
                times.Add(Time.unscaledDeltaTime * 1000f);
            }
            times.Sort();
            float p95 = times[Mathf.Min(times.Count - 1, (int)(times.Count * 0.95f))];
            Debug.Log($"[AutoCapture] state={state} avg frame {elapsed / times.Count * 1000f:F2} ms, p95 {p95:F2} ms over " +
                      $"{times.Count} frames ({Screen.width}x{Screen.height}, {SystemInfo.graphicsDeviceName}, " +
                      $"{SystemInfo.graphicsDeviceType}, quality {QualitySettings.names[QualitySettings.GetQualityLevel()]})");
            ScreenCapture.CaptureScreenshot(path);
            yield return null;
            yield return null;
            yield return new WaitForSecondsRealtime(0.5f);
            Application.Quit();
        }

        /// <summary>Benchmark-only switches for finding what costs frame time.</summary>
        static void ApplyPerfOverrides()
        {
            var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urp == null) return;
            if (Arg("-perf-msaa") is string msaa) urp.msaaSampleCount = int.Parse(msaa);
            if (Arg("-perf-scale") is string scale) urp.renderScale = float.Parse(scale, CultureInfo.InvariantCulture);
            if (Has("-perf-nossao"))
                foreach (var data in urp.rendererDataList)
                    if (data is UniversalRendererData ur)
                        foreach (var f in ur.rendererFeatures) f.SetActive(false);
            if (Has("-perf-noshadows") || Has("-perf-hardshadows"))
                foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                    if (l.shadows != LightShadows.None)
                        l.shadows = Has("-perf-noshadows") ? LightShadows.None : LightShadows.Hard;
            if (Has("-perf-nopost"))
                Camera.main.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            if (Has("-perf-nohdr"))
                urp.supportsHDR = false;
            if (Has("-perf-lampshadowsonly"))
                foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                    if (l.name == "CeilingLight") l.shadows = LightShadows.None;
            if (Has("-perf-bloomquarter") && FindAnyObjectByType<Volume>() is Volume v && v.profile.TryGet(out Bloom bloom))
            {
                bloom.downscale.Override(BloomDownscaleMode.Quarter);
                bloom.maxIterations.Override(4);
            }
            if (Arg("-perf-aa") is string aa)
                Camera.main.GetUniversalAdditionalCameraData().antialiasing = aa switch
                {
                    "fxaa" => AntialiasingMode.FastApproximateAntialiasing,
                    "smaa" => AntialiasingMode.SubpixelMorphologicalAntiAliasing,
                    _ => AntialiasingMode.None,
                };
        }

        static bool Has(string name) => System.Array.IndexOf(System.Environment.GetCommandLineArgs(), name) >= 0;

        static string Arg(string name)
        {
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == name) return args[i + 1];
            return null;
        }
    }
}
