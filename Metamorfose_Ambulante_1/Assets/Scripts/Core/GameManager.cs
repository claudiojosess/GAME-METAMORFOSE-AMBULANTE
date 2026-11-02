using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AeroMorphET
{
    public sealed class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }
        public bool IsGameOver { get; private set; }
        public float Score { get; private set; }
        public int CorrectBarriers { get; private set; }
        public float DistanceTravelled => runner != null ? runner.DistanceTravelled : 0f;
        public int PointsPerCorrectBarrier => pointsPerCorrectBarrier;
        public event Action<float> ScoreChanged;
        public event Action GameOverTriggered;

        [SerializeField] private int wrongFormPenalty = 250;
        [SerializeField] private int pointsPerCorrectBarrier = 100;
        private EndlessRunnerMovement runner;
        private int totalPenalties;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            runner = FindAnyObjectByType<EndlessRunnerMovement>();
        }

        private void Update()
        {
            if (IsGameOver) return;
            if (runner == null) runner = FindAnyObjectByType<EndlessRunnerMovement>();
            if (runner == null) return;
            UpdateScore();
        }

        public void RegisterCorrectBarrier()
        {
            if (IsGameOver) return;
            CorrectBarriers++;
            UpdateScore();
        }

        public void ApplyPenalty()
        {
            if (IsGameOver) return;
            totalPenalties += wrongFormPenalty;
            UpdateScore();
        }

        private void UpdateScore()
        {
            Score = Mathf.Max(0f, DistanceTravelled + CorrectBarriers * pointsPerCorrectBarrier - totalPenalties);
            ScoreChanged?.Invoke(Score);
        }

        public void GameOver(string reason)
        {
            if (IsGameOver) return;
            IsGameOver = true;
            GameAudio.Play("GameOver");
            if (runner == null) runner = FindAnyObjectByType<EndlessRunnerMovement>();
            UpdateScore();
            if (runner != null) runner.SetMovementEnabled(false);
            AeroMorphMenuSystem.EnsureInstance().NotifyGameOver(reason, Score);
            GameOverTriggered?.Invoke();
        }

        public void RestartGame() => AeroMorphMenuSystem.EnsureInstance().RestartGame();
        private void OnDestroy() { if (Instance == this) Instance = null; }
    }
}
