using System;
using UnityEngine;

namespace GameScaffold
{
    /// <summary>
    /// The pointer (finger or mouse) as press / drag / release, read through <see cref="GameInput.Press"/> and
    /// <see cref="GameInput.Point"/>. Taps and swipes are measured from it: store <see cref="ScreenPosition"/> on
    /// <see cref="InteractionStartedThisFrame"/>, compare on <see cref="InteractionEndedThisFrame"/>. One per scene
    /// (the scene builder adds it); <see cref="FunTestKit.PointerOverButton"/> tells a press on a button from one on
    /// the game.
    /// </summary>
    public class TouchInputAdapter : MonoBehaviour
    {
        public static TouchInputAdapter Instance { get; private set; }

        public bool IsInteracting { get; private set; }
        public bool InteractionStartedThisFrame { get; private set; }
        public bool InteractionEndedThisFrame { get; private set; }
        public Vector2 ScreenPosition { get; private set; }
        public Vector2 DeltaPosition { get; private set; }

        public event Action<Vector2> OnInteractionStarted;
        public event Action<Vector2> OnInteractionEnded;

        private Vector2 lastScreenPos;

        // Dev hook: when SimActive is true, the simulated pointer replaces real touch / mouse, so an Editor bot can play
        // the game through the same input path as a player (see InputBot).
        public static bool SimActive;
        public static bool SimDown;
        public static Vector2 SimPosition;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSim() { SimActive = false; SimDown = false; }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Update()
        {
            InteractionStartedThisFrame = false;
            InteractionEndedThisFrame = false;

            bool isCurrentlyInteracting = false;
            Vector2 currentPos = ScreenPosition;

            if (SimActive)
            {
                isCurrentlyInteracting = SimDown;
                if (SimDown) currentPos = SimPosition;
            }
            else if (DevMenu.IsOpen) isCurrentlyInteracting = false; // the dev panel has the finger
            else if (GameInput.Press.IsPressed())
            {
                isCurrentlyInteracting = true;
                currentPos = GameInput.Point.ReadValue<Vector2>();
            }
            else if (IsInteracting)
            {
                // The release frame: read where the finger lifted, so a flick that moves and lifts within one frame
                // still counts
                currentPos = GameInput.Point.ReadValue<Vector2>();
            }

            if (isCurrentlyInteracting && !IsInteracting)
            {
                InteractionStartedThisFrame = true;
                DeltaPosition = Vector2.zero;
                lastScreenPos = currentPos;
                OnInteractionStarted?.Invoke(currentPos);
            }
            else if (!isCurrentlyInteracting && IsInteracting)
            {
                InteractionEndedThisFrame = true;
                DeltaPosition = Vector2.zero;
                OnInteractionEnded?.Invoke(ScreenPosition);
            }
            else if (isCurrentlyInteracting)
            {
                DeltaPosition = currentPos - lastScreenPos;
                lastScreenPos = currentPos;
            }

            IsInteracting = isCurrentlyInteracting;
            ScreenPosition = currentPos;
        }

        /// <summary>Projects the pointer onto a horizontal plane at <paramref name="planeHeight"/> (3D games).</summary>
        public bool TryGetWorldPointOnPlane(Camera cam, float planeHeight, out Vector3 worldPoint)
        {
            if (cam == null) cam = Camera.main;
            if (cam == null) { worldPoint = Vector3.zero; return false; }
            Ray ray = cam.ScreenPointToRay(ScreenPosition);
            var plane = new Plane(Vector3.up, new Vector3(0f, planeHeight, 0f));
            if (plane.Raycast(ray, out float enter)) { worldPoint = ray.GetPoint(enter); return true; }
            worldPoint = Vector3.zero;
            return false;
        }

        /// <summary>The pointer in world space on the camera's near plane (2D games with an orthographic camera).</summary>
        public Vector2 WorldPoint2D(Camera cam = null)
        {
            if (cam == null) cam = Camera.main;
            if (cam == null) return Vector2.zero;
            return cam.ScreenToWorldPoint(new Vector3(ScreenPosition.x, ScreenPosition.y, cam.nearClipPlane));
        }
    }
}
