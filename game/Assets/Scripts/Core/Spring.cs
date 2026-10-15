using UnityEngine;

namespace LookImportant
{
    /// <summary>
    /// Damped harmonic oscillator used for every moving part, so things snap,
    /// overshoot and settle instead of tweening. frequency in Hz, damping is the
    /// damping ratio (1 = critical, lower = bouncier). Sub-stepped for stability.
    /// </summary>
    [System.Serializable]
    public struct Spring
    {
        public float value;
        public float velocity;

        public void Step(float target, float frequency, float damping, float dt)
        {
            float w = 2f * Mathf.PI * frequency;
            int steps = Mathf.Max(1, Mathf.CeilToInt(dt * 240f));
            float h = dt / steps;
            for (int i = 0; i < steps; i++)
            {
                float accel = w * w * (target - value) - 2f * damping * w * velocity;
                velocity += accel * h;
                value += velocity * h;
            }
        }

        public void Snap(float target)
        {
            value = target;
            velocity = 0f;
        }
    }
}
