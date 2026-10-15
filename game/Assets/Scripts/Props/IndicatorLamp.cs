using UnityEngine;

namespace LookImportant
{
    /// <summary>Jewel pilot lamp: emissive lens plus a tiny point light, with a brief flicker on strike.</summary>
    public class IndicatorLamp : MonoBehaviour
    {
        public Renderer lens;
        public Light glow;
        [ColorUsage(false, true)] public Color litEmission = Color.white;
        public float glowIntensity = 0.35f;

        bool lit;
        float level, flicker;

        public bool IsLit => lit;

        public void SetLit(bool value)
        {
            if (value == lit) return;
            lit = value;
            if (lit) flicker = 0.09f;
        }

        void Start() => Apply();

        void Update()
        {
            float target = lit ? 1f : 0f;
            if (flicker > 0f)
            {
                flicker -= Time.deltaTime;
                target *= Random.value < 0.5f ? 0.25f : 1f;
            }
            if (Mathf.Approximately(level, target) && flicker <= 0f) return;
            level = Mathf.MoveTowards(level, target, Time.deltaTime * (lit ? 30f : 10f));
            Apply();
        }

        void Apply()
        {
            Emission.Set(lens, litEmission * level);
            if (glow != null)
            {
                glow.intensity = glowIntensity * level;
                glow.enabled = level > 0.01f;
            }
        }
    }
}
