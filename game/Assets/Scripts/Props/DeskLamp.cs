using UnityEngine;

namespace LookImportant
{
    /// <summary>Anglepoise-style lamp. Click anywhere on it to switch it.</summary>
    public class DeskLamp : Interactable
    {
        public Light lamp;
        public Renderer bulb;
        [ColorUsage(false, true)] public Color bulbEmission = Color.white;
        public bool on = true;
        public AudioClip soundSwitch;

        float intensity;
        float level;

        public bool IsOn => on;

        protected override void Awake()
        {
            base.Awake();
            intensity = lamp.intensity;
            level = on ? 1f : 0f;
            Apply();
        }

        public override void Press(in RaycastHit hit) => Set(!on);

        public void Set(bool value, bool instant = false)
        {
            if (value == on) return;
            on = value;
            if (instant) level = on ? 1f : 0f;
            else Sfx.Play(soundSwitch, transform.position);
            Apply();
            Ambience.RefreshReflectionsSoon();
        }

        void Update()
        {
            float target = on ? 1f : 0f;
            if (Mathf.Approximately(level, target)) return;
            // incandescent filament: quick to strike, slower to fade
            level = Mathf.MoveTowards(level, target, Time.deltaTime * (on ? 12f : 5f));
            Apply();
        }

        void Apply()
        {
            lamp.intensity = intensity * level;
            lamp.enabled = level > 0.001f;
            Emission.Set(bulb, bulbEmission * (level * level));
        }
    }
}
