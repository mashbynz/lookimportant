using UnityEngine;

namespace LookImportant
{
    /// <summary>Jewel pilot lamp: an emissive lens (bloom does the glow), with a brief flicker on strike.</summary>
    public class IndicatorLamp : MonoBehaviour
    {
        public Renderer lens;
        [ColorUsage(false, true)] public Color litEmission = Color.white;

        bool lit;
        float level, flicker;

        public bool IsLit => lit;

        public void SetLit(bool value)
        {
            if (value == lit) return;
            lit = value;
            if (lit) flicker = 0.09f;
        }

        void Start() => Emission.Set(lens, Color.black);

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
            Emission.Set(lens, litEmission * level);
        }
    }
}
