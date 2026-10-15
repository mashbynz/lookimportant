using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace LookImportant
{
    /// <summary>Eyes adjusting: exposure rises from black over the first seconds.</summary>
    public class StartupFade : MonoBehaviour
    {
        public Volume volume;
        public float duration = 2.5f;
        public float startExposure = -7f;

        ColorAdjustments adjust;
        float targetExposure;
        float t;

        void Start()
        {
            if (volume == null || !volume.profile.TryGet(out adjust))
            {
                enabled = false;
                return;
            }
            targetExposure = adjust.postExposure.value;
            adjust.postExposure.value = startExposure;
        }

        void Update()
        {
            t += Time.deltaTime / duration;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
            adjust.postExposure.value = Mathf.Lerp(startExposure, targetExposure, k);
            if (t >= 1f) enabled = false;
        }

        public void Skip()
        {
            t = 1f;
            if (adjust != null) adjust.postExposure.value = targetExposure;
        }
    }
}
