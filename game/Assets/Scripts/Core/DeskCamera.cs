using UnityEngine;
using UnityEngine.InputSystem;

namespace LookImportant
{
    /// <summary>
    /// Seated point of view. The cursor stays free for clicking; its distance
    /// from screen centre turns the head (gently near the middle, more at the
    /// edges), so the player discovers they can look past the resting frame.
    /// The head also shifts sideways a little for parallax and breathes.
    /// </summary>
    public class DeskCamera : MonoBehaviour
    {
        public float maxYaw = 34f;
        public float maxPitchUp = 24f;
        public float maxPitchDown = 16f;
        public float response = 0.18f;
        public float headShift = 0.0025f;   // metres of sideways head movement per degree of yaw

        Quaternion restRotation;
        Vector3 restPosition;
        Vector2 look, lookVelocity;
        Vector2? overrideLook;

        void Start()
        {
            restRotation = transform.rotation;
            restPosition = transform.position;
        }

        /// <summary>Pin the view (degrees yaw, pitch) - used by automated captures.</summary>
        public void OverrideLook(Vector2 yawPitch) => overrideLook = yawPitch;

        static float Curve(float v) => Mathf.Sign(v) * Mathf.Pow(Mathf.Abs(v), 1.8f);

        void LateUpdate()
        {
            Vector2 target = Vector2.zero;
            if (overrideLook.HasValue)
            {
                target = overrideLook.Value;
            }
            else if (Mouse.current != null && Application.isFocused)
            {
                Vector2 p = Mouse.current.position.ReadValue();
                float nx = Mathf.Clamp(p.x / Mathf.Max(1, Screen.width) * 2f - 1f, -1f, 1f);
                float ny = Mathf.Clamp(p.y / Mathf.Max(1, Screen.height) * 2f - 1f, -1f, 1f);
                target = new Vector2(Curve(nx) * maxYaw, Curve(ny) * (ny > 0 ? maxPitchUp : maxPitchDown));
            }
            look = Vector2.SmoothDamp(look, target, ref lookVelocity, response);

            float breath = Mathf.Sin(Time.time * 1.25f) * 0.12f;
            transform.rotation = Quaternion.AngleAxis(look.x, Vector3.up) * restRotation *
                                 Quaternion.AngleAxis(-(look.y + breath), Vector3.right);
            transform.position = restPosition + transform.right * (look.x * headShift) +
                                 Vector3.up * (breath * 0.002f);
        }
    }
}
