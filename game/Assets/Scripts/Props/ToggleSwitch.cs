using UnityEngine;

namespace LookImportant
{
    /// <summary>Bat-handle toggle. Lives on the lever pivot; click flips it.</summary>
    public class ToggleSwitch : Interactable
    {
        public Vector3 axis = Vector3.right;
        public float onAngle = -24f;
        public float offAngle = 24f;
        public bool isOn;
        public AudioClip soundOn, soundOff;

        Quaternion rest;
        Spring angle;

        public bool IsOn => isOn;
        public event System.Action<ToggleSwitch> Changed;

        protected override void Awake()
        {
            base.Awake();
            rest = transform.localRotation;
            angle.Snap(isOn ? onAngle : offAngle);
            Apply();
        }

        public override void Press(in RaycastHit hit) => Set(!isOn);

        public void Set(bool on, bool instant = false)
        {
            if (on == isOn && !instant) return;
            isOn = on;
            if (instant) angle.Snap(isOn ? onAngle : offAngle);
            else Sfx.Play(isOn ? soundOn : soundOff, transform.position);
            Changed?.Invoke(this);
        }

        void Update()
        {
            angle.Step(isOn ? onAngle : offAngle, 16f, 0.5f, Time.deltaTime);
            Apply();
        }

        void Apply() => transform.localRotation = rest * Quaternion.AngleAxis(angle.value, axis);
    }
}
