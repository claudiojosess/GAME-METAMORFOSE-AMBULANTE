using UnityEngine;

namespace AeroMorphET
{
    // HUD IMGUI integrado: funciona sem o pacote Unity UI.
    public sealed class UIManager : MonoBehaviour
    {
        public static UIManager Instance { get; private set; }
        private string form = "SÓLIDO", feedback = "1: SÓLIDO   2: LÍQUIDO   3: GASOSO";
        private string gameOverReason;
        private float gameOverScore;
        private bool gameOver;
        private GUIStyle hud, center;
        private void Awake() { if (Instance != null && Instance != this) { Destroy(gameObject); return; } Instance = this; }
        private void OnDestroy() { if (Instance == this) Instance = null; }
        private void OnGUI()
        {
            if (AeroMorphMenuSystem.IsMenuVisible) return;
            if (hud == null)
            {
                hud = new GUIStyle(GUI.skin.box) { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                hud.normal.textColor = Color.white;
                center = new GUIStyle(hud) { fontSize = 28, wordWrap = true };
            }
            float scale = Mathf.Max(.6f, Mathf.Min(Screen.width / 960f, Screen.height / 540f));
            Matrix4x4 old = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            float w = Screen.width / scale, h = Screen.height / scale;
            if (gameOver)
            {
                var manager = GameManager.Instance;
                GUI.Box(new Rect((w - 560) / 2f, (h - 240) / 2f, 560, 210),
                    "FIM DA CORRIDA\n" + gameOverReason +
                    "\nDistância: " + (manager != null ? manager.DistanceTravelled.ToString("0") : "0") + " m" +
                    "  •  Barreiras: " + (manager != null ? manager.CorrectBarriers : 0) +
                    "\nPontuação: " + gameOverScore.ToString("0"), center);
            }
            else
            {
                var manager = GameManager.Instance;
                GUI.Box(new Rect(12, 12, 530, 68), "DISTÂNCIA " + (manager != null ? manager.DistanceTravelled.ToString("0") : "0") +
                    " m   |   BARREIRAS " + (manager != null ? manager.CorrectBarriers : 0) +
                    "   |   PONTOS " + (manager != null ? manager.Score.ToString("0") : "0") +
                    "\nFORMA: " + form, hud);
                GUI.Box(new Rect(w - 390, 12, 378, 50), feedback, hud);
                GUI.Box(new Rect(12, h - 49, w - 24, 38), "A/D: pistas  •  ESPAÇO: pular  •  1/2/3: formas", hud);
            }
            GUI.matrix = old;
        }
        public void SetMorphState(AlienMorphController.MorphState state) { form = ObstacleInteraction.StateName(state); }
        public void SetMicrophoneState(bool active) { }
        public void SetVoiceFeedback(string message, bool ok) { feedback = message; }
        public void ShowGameOver(string reason, float score) { gameOver = true; gameOverReason = reason; gameOverScore = score; }
    }
}
