using UnityEngine;

namespace LookImportant
{
    /// <summary>
    /// Springy reaction for things that just want poking: tools swinging on
    /// their hooks, the padlock rattling, the mug rocking. Lives on the pivot.
    /// The swing direction follows which side of the pivot was clicked.
    /// </summary>
    public class Wobble : Interactable
    {
        public Vector3 primaryAxis = Vector3.forward;
        public Vector3 secondaryAxis = Vector3.zero;
        public float frequency = 1.4f;
        [Range(0, 1)] public float damping = 0.05f;
        public float kick = 160f;            // degrees per second added per click
        public float maxAngle = 25f;
        public AudioClip sound;
        public float volume = 1f;

        Quaternion rest;
        Spring a, b;

        protected override void Awake()
        {
            base.Awake();
            rest = transform.localRotation;
        }

        public override void Press(in RaycastHit hit)
        {
            // Push away from the click: which way gravity-side torque points decides the swing direction.
            Vector3 axis = transform.TransformDirection(primaryAxis);
            float side = Mathf.Sign(Vector3.Dot(Vector3.Cross(axis, hit.point - transform.position), Vector3.down) + 1e-4f);
            a.velocity += side * kick * Random.Range(0.8f, 1.2f);
            if (secondaryAxis != Vector3.zero)
                b.velocity += kick * Random.Range(-0.6f, 0.6f);
            Sfx.Play(sound, hit.point, volume);
        }

        void Update()
        {
            a.Step(0f, frequency, damping, Time.deltaTime);
            b.Step(0f, frequency * 1.3f, damping * 1.5f, Time.deltaTime);
            a.value = Mathf.Clamp(a.value, -maxAngle, maxAngle);
            b.value = Mathf.Clamp(b.value, -maxAngle, maxAngle);
            var r = Quaternion.AngleAxis(a.value, primaryAxis);
            if (secondaryAxis != Vector3.zero) r *= Quaternion.AngleAxis(b.value, secondaryAxis);
            transform.localRotation = rest * r;
        }
    }
}
