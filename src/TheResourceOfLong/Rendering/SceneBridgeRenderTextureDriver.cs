using UnityEngine;

namespace TheResourceOfLong
{
    public sealed class SceneBridgeRenderTextureDriver : MonoBehaviour
    {
        public Camera TargetCamera;
        public float FramesPerSecond = 30f;

        private float _accumulator;

        private void OnEnable()
        {
            _accumulator = 0f;
            RenderNow();
        }

        private void LateUpdate()
        {
            if (TargetCamera == null) return;

            float frameDuration = FramesPerSecond > 0f ? 1f / Mathf.Max(1f, FramesPerSecond) : 0f;
            if (frameDuration <= 0f)
            {
                RenderNow();
                return;
            }

            _accumulator += Time.unscaledDeltaTime;
            if (_accumulator < frameDuration) return;

            _accumulator = 0f;
            RenderNow();
        }

        public void RenderNow()
        {
            if (TargetCamera == null) return;
            TargetCamera.Render();
        }
    }
}
