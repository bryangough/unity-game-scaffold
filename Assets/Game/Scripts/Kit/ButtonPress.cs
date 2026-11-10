using UnityEngine;
using UnityEngine.EventSystems;

namespace GameScaffold
{
    /// <summary>
    /// A button's face sinks onto its shadow while it is held, so a tap feels pressed, not only tinted. Give it the
    /// face (a child image inset from the button's dark backing) and how far to drop.
    /// </summary>
    public class ButtonPress : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        private RectTransform face;
        private float depth;

        public void Init(RectTransform faceRect, float drop) { face = faceRect; depth = drop; }

        public void OnPointerDown(PointerEventData e) => Set(true);
        public void OnPointerUp(PointerEventData e) => Set(false);
        public void OnPointerExit(PointerEventData e) => Set(false);
        private void OnDisable() => Set(false);

        private void Set(bool down)
        {
            if (face == null) return;
            Vector2 o = down ? new Vector2(depth * 0.7f, -depth * 0.7f) : Vector2.zero;
            face.offsetMin = o;
            face.offsetMax = o;
        }
    }
}
