using System.Collections;
using UnityEngine;

namespace LookImportant
{
    /// <summary>
    /// Test hook for headless verification; inert unless the player is launched
    /// with -capture. Example:
    ///   LookImportant.x86_64 -capture out.png -capture-state live -capture-look 0,10
    /// States: rest (as the game starts), live (mains on, toggles 1 and 3 on,
    /// selector at 4, button held), dark (desk lamp off).
    /// Also logs the average frame time so builds can be compared.
    /// </summary>
    public class AutoCapture : MonoBehaviour
    {
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

            float elapsed = 0f;
            int frames = 0;
            while (elapsed < 3f)
            {
                yield return null;
                elapsed += Time.unscaledDeltaTime;
                frames++;
            }
            Debug.Log($"[AutoCapture] state={state} avg frame {elapsed / frames * 1000f:F1} ms over {frames} frames " +
                      $"({Screen.width}x{Screen.height}, {SystemInfo.graphicsDeviceName}, {SystemInfo.graphicsDeviceType})");
            ScreenCapture.CaptureScreenshot(path);
            yield return null;
            yield return null;
            yield return new WaitForSecondsRealtime(0.5f);
            Application.Quit();
        }

        static string Arg(string name)
        {
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == name) return args[i + 1];
            return null;
        }
    }
}
