using UnityEngine;

namespace LookImportant
{
    /// <summary>
    /// Mains knife switch on the pegboard. Lives on the blade hinge. Power flows
    /// only once the blades are actually seated in the jaws, so lamps come on at
    /// the moment of contact rather than on click.
    /// </summary>
    public class KnifeSwitch : Interactable
    {
        public Vector3 axis = Vector3.right;
        public float openAngle = 105f;
        public float contactAngle = 6f;
        public bool closed;
        public AudioClip soundClose, soundOpen;

        Quaternion rest;
        Spring angle;
        bool contact;

        public bool IsLive => contact;
        public event System.Action<KnifeSwitch> ContactChanged;

        protected override void Awake()
        {
            base.Awake();
            rest = transform.localRotation;
            angle.Snap(closed ? 0f : openAngle);
            contact = closed;
            Apply();
        }

        public override void Press(in RaycastHit hit) => Set(!closed);

        public void Set(bool close, bool instant = false)
        {
            closed = close;
            if (instant)
            {
                angle.Snap(closed ? 0f : openAngle);
                UpdateContact(false);
                Apply();
            }
            else if (!closed)
            {
                Sfx.Play(soundOpen, transform.position);
            }
        }

        void Update()
        {
            float target = closed ? 0f : openAngle;
            angle.Step(target, closed ? 3.2f : 1.6f, closed ? 0.85f : 0.3f, Time.deltaTime);
            if (angle.value < 0f)
            {
                angle.value = 0f;   // the jaws are a hard stop
                angle.velocity = 0f;
            }
            if (angle.value > openAngle + 12f)
            {
                angle.value = openAngle + 12f;
                angle.velocity = -angle.velocity * 0.3f;
            }
            UpdateContact(true);
            Apply();
        }

        void UpdateContact(bool withSound)
        {
            bool now = angle.value < contactAngle;
            if (now == contact) return;
            contact = now;
            if (contact && withSound) Sfx.Play(soundClose, transform.position);
            ContactChanged?.Invoke(this);
        }

        void Apply() => transform.localRotation = rest * Quaternion.AngleAxis(angle.value, axis);
    }
}
