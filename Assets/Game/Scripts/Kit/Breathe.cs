using UnityEngine;

namespace GameScaffold
{
    /// <summary>A slow scale breath (a title logo, a PLAY button). Unscaled time, so it breathes while the game is paused.</summary>
    public class Breathe : MonoBehaviour
    {
        public float amount = 0.025f;
        public float period = 3.2f;
        private Vector3 baseScale;

        private void Awake() { baseScale = transform.localScale; }

        private void Update()
        {
            float s = 1f + amount * Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f / period);
            transform.localScale = baseScale * s;
        }
    }
}
