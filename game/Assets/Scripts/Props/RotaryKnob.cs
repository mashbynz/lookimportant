using UnityEngine;

namespace LookImportant
{
    /// <summary>
    /// Detented selector (chicken-head knob). Click steps clockwise and bounces
    /// back off the end stop; right-click steps back; the wheel turns either way.
    /// Angles are degrees clockwise as seen by the player.
    /// </summary>
    public class RotaryKnob : Interactable
    {
        public Vector3 axis = Vector3.up;
        public float[] detents = { -60f, -30f, 0f, 30f, 60f };
        public int index;
        public AudioClip soundDetent;

        Quaternion rest;
        Spring angle;
        int direction = 1;

        public int Index => index;
        public int Count => detents.Length;
        public event System.Action<RotaryKnob> Changed;

        protected override void Awake()
        {
            base.Awake();
            rest = transform.localRotation;
            angle.Snap(detents[index]);
            Apply();
        }

        public override void Press(in RaycastHit hit)
        {
            if (index + direction < 0 || index + direction >= detents.Length) direction = -direction;
            Step(direction);
        }

        public override void AltPress(in RaycastHit hit) => Step(-1);
        public override void Scroll(int dir) => Step(dir);

        public void SetIndex(int i, bool instant = false)
        {
            index = Mathf.Clamp(i, 0, detents.Length - 1);
            if (instant) angle.Snap(detents[index]);
            Changed?.Invoke(this);
        }

        void Step(int dir)
        {
            int target = Mathf.Clamp(index + dir, 0, detents.Length - 1);
            if (target == index)
            {
                angle.velocity += dir * 120f;   // nudge against the stop
                return;
            }
            index = target;
            Sfx.Play(soundDetent, transform.position, 1f, 0.9f + 0.06f * index);
            Changed?.Invoke(this);
        }

        void Update()
        {
            angle.Step(detents[index], 12f, 0.55f, Time.deltaTime);
            Apply();
        }

        void Apply() => transform.localRotation = rest * Quaternion.AngleAxis(angle.value, axis);
    }
}
