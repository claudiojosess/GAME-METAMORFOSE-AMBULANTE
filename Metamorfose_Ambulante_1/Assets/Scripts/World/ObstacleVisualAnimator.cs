using UnityEngine;

namespace AeroMorphET
{
    public sealed class ObstacleVisualAnimator : MonoBehaviour
    {
        public enum MotionType { SpinZ, Bob, PressDown, Pulse }

        [SerializeField] private MotionType motion = MotionType.Pulse;
        [SerializeField] private float speed = 2f;
        [SerializeField] private float amplitude = .2f;
        [SerializeField] private float phase;

        private Vector3 startPosition;
        private Vector3 startScale;

        private void Awake()
        {
            startPosition = transform.localPosition;
            startScale = transform.localScale;
        }

        public void Configure(MotionType type, float animationSpeed, float animationAmplitude, float animationPhase = 0f)
        {
            motion = type;
            speed = animationSpeed;
            amplitude = animationAmplitude;
            phase = animationPhase;
            startPosition = transform.localPosition;
            startScale = transform.localScale;
        }

        private void Update()
        {
            float wave = Mathf.Sin(Time.time * speed + phase);

            switch (motion)
            {
                case MotionType.SpinZ:
                    transform.Rotate(0f, 0f, speed * 55f * Time.deltaTime, Space.Self);
                    break;

                case MotionType.Bob:
                    transform.localPosition = startPosition + Vector3.up * (wave * amplitude);
                    break;

                case MotionType.PressDown:
                    transform.localPosition = startPosition + Vector3.down * ((wave + 1f) * .5f * amplitude);
                    break;

                case MotionType.Pulse:
                    transform.localScale = startScale * (1f + wave * amplitude);
                    break;
            }
        }
    }
}
