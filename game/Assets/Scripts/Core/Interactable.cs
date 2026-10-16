using System.Collections.Generic;
using UnityEngine;

namespace LookImportant
{
    /// <summary>
    /// Anything on the bench the player can poke. Colliders on child meshes route
    /// clicks here via GetComponentInParent. Hovering makes the object's own
    /// renderers glow softly - the only affordance; there are no written hints.
    /// </summary>
    public abstract class Interactable : MonoBehaviour
    {
        static readonly Color HoverGlow = new(0.045f, 0.038f, 0.028f);
        static readonly HashSet<string> SelfLitMaterials = new() { "M_Bulb", "M_LampAmber", "M_LampGreen", "M_LampRed", "M_Fluoro" };

        Renderer[] glowRenderers;
        bool hovered;
        float glow;

        public bool IsHovered => hovered;

        protected virtual void Awake()
        {
            var list = new List<Renderer>();
            foreach (var r in GetComponentsInChildren<Renderer>())
            {
                var m = r.sharedMaterial;
                if (m == null || !SelfLitMaterials.Contains(m.name))
                    list.Add(r);
            }
            glowRenderers = list.ToArray();
        }

        public void SetHovered(bool value)
        {
            if (hovered == value) return;
            hovered = value;
            if (value) OnHoverEnter(); else OnHoverExit();
        }

        protected virtual void OnHoverEnter() { }
        protected virtual void OnHoverExit() { }

        /// <summary>Primary button pressed on this object.</summary>
        public virtual void Press(in RaycastHit hit) { }
        /// <summary>Primary button released after a Press on this object (wherever the cursor is).</summary>
        public virtual void Release() { }
        /// <summary>Secondary button pressed on this object.</summary>
        public virtual void AltPress(in RaycastHit hit) { }
        /// <summary>Scroll wheel while hovered: +1 away from the player, -1 towards.</summary>
        public virtual void Scroll(int direction) { }

        protected virtual void LateUpdate()
        {
            float target = hovered ? 1f : 0f;
            if (!hovered && glow <= 0f) return;
            glow = Mathf.MoveTowards(glow, target, Time.deltaTime * 6f);
            float pulse = hovered ? 0.8f + 0.2f * Mathf.Sin(Time.time * 4f) : 1f;
            var c = HoverGlow * (glow * pulse);
            foreach (var r in glowRenderers)
                Emission.Set(r, c);
        }
    }
}
