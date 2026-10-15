using UnityEngine;

namespace LookImportant
{
    /// <summary>Momentary mushroom button. Lives on the plunger; held while the mouse is down.</summary>
    public class PushButton : Interactable
    {
        public Vector3 pressDirection = Vector3.down;
        public float travel = 0.008f;
        public AudioClip soundDown, soundUp;

        Vector3 rest;
        Spring depth;
        bool held;

        public bool IsHeld => held;
        public event System.Action<PushButton> Pressed;

        protected override void Awake()
        {
            base.Awake();
            rest = transform.localPosition;
        }

        public override void Press(in RaycastHit hit)
        {
            held = true;
            Sfx.Play(soundDown, transform.position);
            Pressed?.Invoke(this);
        }

        public override void Release()
        {
            if (!held) return;
            held = false;
            Sfx.Play(soundUp, transform.position, 0.8f);
        }

        void Update()
        {
            depth.Step(held ? travel : 0f, held ? 30f : 14f, held ? 0.9f : 0.35f, Time.deltaTime);
            transform.localPosition = rest + pressDirection * Mathf.Min(depth.value, travel * 1.05f);
        }
    }
}
