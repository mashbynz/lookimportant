using UnityEngine;

namespace LookImportant
{
    /// <summary>
    /// Moving-coil meter. The needle chases `target` (0..1) on an under-damped
    /// spring and bounces off the end stops. Tapping the glass knocks it.
    /// </summary>
    public class Gauge : Interactable
    {
        public Transform needle;
        public Vector3 axis = Vector3.forward;
        public float minAngle = -135f;   // clockwise from 12 o'clock, as printed on the dial
        public float maxAngle = 135f;
        [Range(0, 1)] public float target;
        public bool live;
        public AudioClip soundTap;

        Quaternion rest;
        Spring value;

        public float Value => value.value;

        protected override void Awake()
        {
            base.Awake();
            rest = needle.localRotation;
            value.Snap(target);
            Apply();
        }

        public override void Press(in RaycastHit hit)
        {
            value.velocity += Random.Range(0.6f, 1.0f) * (Random.value < 0.5f ? -1f : 1f);
            Sfx.Play(soundTap, transform.position, 0.8f);
        }

        void Update()
        {
            float t = target;
            if (live) t += (Mathf.PerlinNoise(Time.time * 2.3f, 0.37f) - 0.5f) * 0.012f;
            value.Step(t, 1.4f, 0.22f, Time.deltaTime);
            if (value.value < -0.015f) { value.value = -0.015f; value.velocity = -value.velocity * 0.35f; }
            if (value.value > 1.015f) { value.value = 1.015f; value.velocity = -value.velocity * 0.35f; }
            Apply();
        }

        void Apply()
        {
            float a = Mathf.LerpUnclamped(minAngle, maxAngle, value.value);
            needle.localRotation = rest * Quaternion.AngleAxis(a, axis);
        }
    }
}
