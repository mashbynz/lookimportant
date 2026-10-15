using UnityEngine;
using UnityEngine.InputSystem;

namespace LookImportant
{
    /// <summary>
    /// Mouse picking. Static props have colliders too, so they block clicks to
    /// anything hidden behind them. While a button is held the pressed object
    /// keeps focus even if the cursor drifts off it.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class InteractionController : MonoBehaviour
    {
        public float reach = 4f;

        Camera cam;
        Interactable hovered;
        Interactable pressed;

        void Awake() => cam = GetComponent<Camera>();

        void Update()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;

            Interactable hit = null;
            RaycastHit info = default;
            if (Application.isFocused &&
                Physics.Raycast(cam.ScreenPointToRay(mouse.position.ReadValue()), out info, reach))
                hit = info.collider.GetComponentInParent<Interactable>();

            if (pressed == null && hit != hovered)
            {
                if (hovered != null) hovered.SetHovered(false);
                hovered = hit;
                if (hovered != null) hovered.SetHovered(true);
            }

            if (mouse.leftButton.wasPressedThisFrame && hovered != null)
            {
                pressed = hovered;
                pressed.Press(info);
            }
            if (mouse.leftButton.wasReleasedThisFrame && pressed != null)
            {
                pressed.Release();
                pressed = null;
            }
            if (mouse.rightButton.wasPressedThisFrame && hovered != null)
                hovered.AltPress(info);

            float scroll = mouse.scroll.ReadValue().y;
            if (hovered != null && Mathf.Abs(scroll) > 0.01f)
                hovered.Scroll(scroll > 0 ? 1 : -1);

#if !UNITY_WEBGL
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame)
                Application.Quit();
#endif
        }
    }
}
