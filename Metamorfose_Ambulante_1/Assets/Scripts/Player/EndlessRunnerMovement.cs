using UnityEngine;

namespace AeroMorphET
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class EndlessRunnerMovement : MonoBehaviour
    {
        [SerializeField] private float initialSpeed = 9f;
        [SerializeField] private float speedIncreasePerMinute = 4f;
        [SerializeField] private float maxSpeed = 24f;
        [SerializeField] private float laneWidth = 3f;
        [SerializeField] private float laneChangeSpeed = 10f;
        [SerializeField] private float jumpForce = 8f;
        [SerializeField] private float gravity = 22f;
        [SerializeField] private float slideDuration = 0.8f;

        public float CurrentSpeed { get; private set; }
        public float DistanceTravelled { get; private set; }
        public bool MovementEnabled { get; private set; } = false;
        public bool IsSliding => slideTimer > 0f;

        private Rigidbody rb;
        private int lane;
        private float verticalVelocity;
        private bool grounded = true;
        private float slideTimer;
        private AlienMorphController morph;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            morph = GetComponent<AlienMorphController>();
            CurrentSpeed = initialSpeed;
        }

        private void Update()
        {
            if (!MovementEnabled || Time.timeScale <= 0f || GameManager.Instance?.IsGameOver == true) return;

            CurrentSpeed = Mathf.Min(maxSpeed, initialSpeed + (Time.timeSinceLevelLoad / 60f) * speedIncreasePerMinute);
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) lane = Mathf.Max(-1, lane - 1);
            if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) lane = Mathf.Min(1, lane + 1);
            if ((Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.UpArrow)) && grounded) { verticalVelocity = jumpForce; grounded = false; GameAudio.Play("Pular"); }
            if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) slideTimer = slideDuration;

            if (!grounded) verticalVelocity -= gravity * Time.deltaTime;
            if (slideTimer > 0) slideTimer -= Time.deltaTime;
        }

        private void FixedUpdate()
        {
            if (!MovementEnabled || Time.timeScale <= 0f || GameManager.Instance?.IsGameOver == true) return;
            Vector3 pos = rb.position;
            pos.z += CurrentSpeed * Time.fixedDeltaTime;
            pos.x = Mathf.Lerp(pos.x, lane * laneWidth, laneChangeSpeed * Time.fixedDeltaTime);
            pos.y += verticalVelocity * Time.fixedDeltaTime;
            if (pos.y <= 1.1f) { pos.y = 1.1f; verticalVelocity = 0; grounded = true; }
            rb.MovePosition(pos);
            DistanceTravelled += CurrentSpeed * Time.fixedDeltaTime;
        }

        public void SetMovementEnabled(bool enabled) => MovementEnabled = enabled;
    }
}
