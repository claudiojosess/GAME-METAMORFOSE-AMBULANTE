using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

namespace AeroMorphET
{
    public sealed class AeroMorphMenuSystem : MonoBehaviour
    {
        private enum MenuPage { Main, Options, Help }

        [Header("Menu")]
        [SerializeField] private bool showMenuOnStart = true;
        [SerializeField] private bool pauseGameWhileMenuIsOpen = true;

        [Header("Game Over")]
        [SerializeField] private string gameOverPanelName = "GameOver";
        [SerializeField] private bool fixExistingRestartButton = true;

        private static AeroMorphMenuSystem instance;
        public static bool IsMenuVisible => instance != null && instance.menuVisible;
        private static bool startImmediatelyAfterReload;
        private static bool openingAfterReload;

        private MenuPage currentPage = MenuPage.Main;
        private bool menuVisible;
        private bool gameStarted;
        private bool gameOverDetected;
        private bool escapeHandledThisFrame;

        private GameObject distanceHUD;
        private GameObject stateHUD;
        private GameObject voiceHUD;
        private GameObject helpHUD;
        private GameObject micHUD;
        private GameObject gameOverPanel;

        private GUIStyle overlayStyle;
        private GUIStyle panelStyle;
        private GUIStyle titleStyle;
        private GUIStyle subtitleStyle;
        private GUIStyle infoStyle;
        private GUIStyle buttonStyle;
        private GUIStyle disabledButtonStyle;
        private GUIStyle sectionTitleStyle;
        private GUIStyle helpStyle;
        private GUIStyle gameOverButtonStyle;

        private Texture2D overlayTexture;
        private Texture2D panelTexture;
        private Texture2D buttonTexture;
        private Texture2D buttonHoverTexture;
        private Texture2D disabledTexture;
        private Texture2D restartTexture;

        private const string VolumePref = "MetamorfoseAmbulante.Volume";
        private const string FullscreenPref = "MetamorfoseAmbulante.Fullscreen";

        private float masterVolume = 0.85f;
        private bool fullscreen;

        private string endReason = "";
        private float endScore, endDistance, returnAt;
        private int endCorrectBarriers;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InstallSceneListener()
        {
            instance = null;
            startImmediatelyAfterReload = false;
            openingAfterReload = false;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => EnsureInstance();

        public static AeroMorphMenuSystem EnsureInstance()
        {
            if (instance != null && instance.isActiveAndEnabled) return instance;
            var existing = FindAnyObjectByType<AeroMorphMenuSystem>();
            if (existing != null) { existing.enabled = true; instance = existing; return existing; }
            return new GameObject("MenuSystem_AUTO").AddComponent<AeroMorphMenuSystem>();
        }

        public void NotifyGameOver(string reason, float score)
        {
            if (gameOverDetected) return;
            endReason = reason;
            endScore = score;
            endDistance = GameManager.Instance != null ? GameManager.Instance.DistanceTravelled : 0f;
            endCorrectBarriers = GameManager.Instance != null ? GameManager.Instance.CorrectBarriers : 0;
            gameOverDetected = true;
            gameStarted = true;
            menuVisible = false;
            returnAt = Time.unscaledTime + 3.5f;
            Time.timeScale = 0f;
        }

        public void ReturnToMainMenu()
        {
            Time.timeScale = 1f;
            startImmediatelyAfterReload = false;
            string target = Application.CanStreamedLevelBeLoaded("MenuPrincipal")
                ? "MenuPrincipal" : SceneManager.GetActiveScene().name;
            SceneManager.LoadScene(target);
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;

            masterVolume = PlayerPrefs.GetFloat(VolumePref, 0.85f);
            fullscreen = PlayerPrefs.GetInt(
                FullscreenPref,
                Screen.fullScreen ? 1 : 0) == 1;

            AudioListener.volume = masterVolume;
            Screen.fullScreen = fullscreen;
        }

        private void Start()
        {
            if (gameOverDetected) return;
            ResolveSceneObjects();

            if (fixExistingRestartButton)
                HookExistingRestartButton();

            if (startImmediatelyAfterReload)
            {
                startImmediatelyAfterReload = false;
                if (openingAfterReload)
                    StartCoroutine(PlayOpeningSequence());
                else
                    StartGameplay();
                openingAfterReload = false;
                return;
            }

            if (showMenuOnStart)
                OpenMainMenu();
            else
                StartGameplay();
        }

        private void Update()
        {
            if (!gameOverDetected && GameManager.Instance != null && GameManager.Instance.IsGameOver)
                NotifyGameOver("Forma incorreta para este obstáculo.", GameManager.Instance.Score);
            if (gameOverDetected && Time.unscaledTime >= returnAt)
                ReturnToMainMenu();
        }

        private void OnGUI()
        {
            EnsureStyles();

            float scaleX = Screen.width / 1920f;
            float scaleY = Screen.height / 1080f;

            Matrix4x4 oldMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(
                Vector3.zero,
                Quaternion.identity,
                new Vector3(scaleX, scaleY, 1f));

            HandleEscapeKey();

            if (menuVisible)
            {
                switch (currentPage)
                {
                    case MenuPage.Main:
                        DrawMainMenu();
                        break;
                    case MenuPage.Options:
                        DrawOptions();
                        break;
                    case MenuPage.Help:
                        DrawHelp();
                        break;
                }
            }

            if (gameOverDetected)
                DrawGameOverSafetyButtons();

            GUI.matrix = oldMatrix;
            escapeHandledThisFrame = false;
        }

        private void OnDestroy()
        {
            if (instance == this)
                instance = null;

            DestroyTexture(overlayTexture);
            DestroyTexture(panelTexture);
            DestroyTexture(buttonTexture);
            DestroyTexture(buttonHoverTexture);
            DestroyTexture(disabledTexture);
            DestroyTexture(restartTexture);
        }

        private void HandleEscapeKey()
        {
            Event current = Event.current;

            if (current == null ||
                current.type != EventType.KeyDown ||
                current.keyCode != KeyCode.Escape ||
                escapeHandledThisFrame)
                return;

            escapeHandledThisFrame = true;
            current.Use();

            if (menuVisible && currentPage != MenuPage.Main)
            {
                currentPage = MenuPage.Main;
                return;
            }

            if (!gameStarted || gameOverDetected)
                return;

            if (menuVisible)
                ContinueGame();
            else
                OpenMainMenu();
        }

        private void DrawMainMenu()
        {
            GUI.Box(new Rect(0f, 0f, 1920f, 1080f), GUIContent.none, overlayStyle);

            Rect panel = new Rect(125f, 95f, 650f, 890f);
            GUI.Box(panel, GUIContent.none, panelStyle);

            GUI.Label(
                new Rect(185f, 145f + Mathf.Sin(Time.unscaledTime * 2f) * 5f, 530f, 155f),
                gameStarted ? "PAUSADO" : "METAMORFOSE\nAMBULANTE",
                titleStyle);

            GUI.Label(
                new Rect(187f, 300f, 530f, 45f),
                "A CORRIDA DE ZYX",
                subtitleStyle);

            GUI.Label(
                new Rect(187f, 354f, 530f, 64f),
                gameStarted
                    ? "Sua corrida está pausada."
                    : "Transforme-se. Supere os obstáculos. Continue correndo.",
                infoStyle);

            float x = 185f;
            float y = 455f;
            float w = 530f;
            float h = 78f;
            float gap = 22f;

            if (GUI.Button(new Rect(x, y, w, h), "NOVO JOGO", buttonStyle))
                NewGame();

            y += h + gap;

            bool previousEnabled = GUI.enabled;
            GUI.enabled = gameStarted;

            if (GUI.Button(
                new Rect(x, y, w, h),
                "CONTINUAR",
                gameStarted ? buttonStyle : disabledButtonStyle))
            {
                ContinueGame();
            }

            GUI.enabled = previousEnabled;
            y += h + gap;

            if (GUI.Button(new Rect(x, y, w, h), "OPÇÕES", buttonStyle))
                currentPage = MenuPage.Options;

            y += h + gap;

            if (GUI.Button(new Rect(x, y, w, h), "AJUDA", buttonStyle))
                currentPage = MenuPage.Help;

            y += h + gap;

            if (GUI.Button(new Rect(x, y, w, h), "SAIR", buttonStyle))
                QuitGame();

            GUI.Label(
                new Rect(1000f, 270f, 720f, 260f),
                "ZYX\nPRECISA\nCONTINUAR",
                new GUIStyle(titleStyle)
                {
                    alignment = TextAnchor.MiddleRight,
                    fontSize = 76
                });

            GUI.Label(
                new Rect(970f, 815f, 750f, 95f),
                "A / D  •  MUDAR DE FAIXA\n" +
                "ESPAÇO  •  PULAR     1 / 2 / 3  •  TRANSFORMAR",
                new GUIStyle(infoStyle)
                {
                    alignment = TextAnchor.MiddleRight,
                    fontSize = 23
                });
        }

        private void DrawOptions()
        {
            GUI.Box(new Rect(0f, 0f, 1920f, 1080f), GUIContent.none, overlayStyle);

            Rect panel = new Rect(500f, 185f, 920f, 710f);
            GUI.Box(panel, GUIContent.none, panelStyle);

            GUI.Label(
                new Rect(590f, 240f, 740f, 75f),
                "OPÇÕES",
                sectionTitleStyle);

            GUI.Label(
                new Rect(610f, 365f, 300f, 45f),
                "VOLUME GERAL",
                subtitleStyle);

            float newVolume = GUI.HorizontalSlider(
                new Rect(610f, 430f, 700f, 36f),
                masterVolume,
                0f,
                1f);

            if (!Mathf.Approximately(newVolume, masterVolume))
            {
                masterVolume = newVolume;
                AudioListener.volume = masterVolume;
                PlayerPrefs.SetFloat(VolumePref, masterVolume);
            }

            GUI.Label(
                new Rect(610f, 485f, 700f, 42f),
                Mathf.RoundToInt(masterVolume * 100f) + "%",
                infoStyle);

            bool newFullscreen = GUI.Toggle(
                new Rect(610f, 570f, 700f, 55f),
                fullscreen,
                "   TELA CHEIA",
                helpStyle);

            if (newFullscreen != fullscreen)
            {
                fullscreen = newFullscreen;
                Screen.fullScreen = fullscreen;
                PlayerPrefs.SetInt(FullscreenPref, fullscreen ? 1 : 0);
            }

            if (GUI.Button(new Rect(610f, 645f, 700f, 60f),
                VoiceInputManager.Instance != null && VoiceInputManager.Instance.IsListening ? "DESATIVAR MICROFONE" : "ATIVAR MICROFONE", buttonStyle))
            {
                if (VoiceInputManager.Instance != null)
                {
                    if (VoiceInputManager.Instance.IsListening) VoiceInputManager.Instance.StopListening();
                    else VoiceInputManager.Instance.StartListening();
                }
            }
            if (GUI.Button(
                new Rect(760f, 735f, 400f, 78f),
                "VOLTAR",
                buttonStyle))
            {
                PlayerPrefs.Save();
                currentPage = MenuPage.Main;
            }
        }

        private void DrawHelp()
        {
            GUI.Box(new Rect(0f, 0f, 1920f, 1080f), GUIContent.none, overlayStyle);

            Rect panel = new Rect(410f, 105f, 1100f, 870f);
            GUI.Box(panel, GUIContent.none, panelStyle);

            GUI.Label(
                new Rect(520f, 160f, 880f, 70f),
                "COMO JOGAR",
                sectionTitleStyle);

            string help =
                "MOVIMENTO\n" +
                "A / D ou ← / →     Mudar de faixa\n" +
                "ESPAÇO ou ↑        Pular\n" +
                "S ou ↓             Abaixar\n\n" +
                "TRANSFORMAÇÕES\n" +
                "1                   SÓLIDO — romper barreiras de impacto\n" +
                "2                   LÍQUIDO — rastejar por espaços baixos\n" +
                "3                   GASOSO — atravessar grades e filtros\n\n" +
                "PONTUAÇÃO\n" +
                "Distância percorrida + " + (GameManager.Instance != null ? GameManager.Instance.PointsPerCorrectBarrier : 100) +
                " pontos por barreira superada com a forma correta.\n\n" +
                "COMANDO DE VOZ\n" +
                "Diga “SÓLIDO”, “LÍQUIDO” ou “GASOSO” para mudar de forma.\n\n" +
                "PAUSA\n" +
                "ESC                 Abrir ou fechar o menu durante a corrida";

            GUI.Label(
                new Rect(555f, 245f, 810f, 590f),
                help,
                helpStyle);

            if (GUI.Button(
                new Rect(760f, 835f, 400f, 78f),
                "VOLTAR",
                buttonStyle))
            {
                currentPage = MenuPage.Main;
            }
        }

        public void NewGame()
        {
            // Sempre começa uma corrida limpa na cena principal.
            LoadMainAndStart(true);
        }

        public void ContinueGame()
        {
            if (!gameStarted || gameOverDetected) return;
            StartGameplay();
        }

        private void StartGameplay()
        {
            var runner = FindFirstObjectByType<EndlessRunnerMovement>();
            if (runner == null)
            {
                Debug.LogError("Metamorfose: controlador do ET ausente na cena jogável.");
                return;
            }
            // Nada relacionado ao microfone pode impedir a liberação da corrida.
            Time.timeScale = 1f;
            runner.enabled = true;
            runner.SetMovementEnabled(true);
            gameStarted = true;
            gameOverDetected = false;
            menuVisible = false;
            currentPage = MenuPage.Main;
            if (gameOverPanel != null) gameOverPanel.SetActive(false);
            SetHUDVisible(true);
            Debug.Log("Metamorfose: corrida iniciada; movimento habilitado e Time.timeScale = 1.");
        }

        private IEnumerator PlayOpeningSequence()
        {
            var runner = FindFirstObjectByType<EndlessRunnerMovement>();
            Camera sceneCamera = Camera.main;
            CameraFollow cameraFollow = sceneCamera != null
                ? sceneCamera.GetComponent<CameraFollow>()
                : null;

            if (runner == null)
            {
                StartGameplay();
                yield break;
            }

            Time.timeScale = 1f;
            runner.enabled = false;
            runner.SetMovementEnabled(false);
            menuVisible = false;
            gameStarted = false;
            SetHUDVisible(false);

            Transform alien = runner.transform;
            if (cameraFollow != null)
                cameraFollow.enabled = false;

            // A câmera fica na posição normal do jogo; o ET olha para ela.
            Quaternion facingCamera = Quaternion.Euler(0f, 180f, 0f);
            Quaternion runningFacing = Quaternion.identity;
            alien.rotation = facingCamera;

            // Mantém o enquadramento aberto enquanto o ET encara a câmera parada.
            yield return new WaitForSecondsRealtime(1.8f);

            // O personagem vira de costas para a câmera e se alinha à direção da corrida.
            float elapsed = 0f;
            const float turnDuration = 1f;
            while (elapsed < turnDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / turnDuration));
                alien.rotation = Quaternion.Slerp(facingCamera, runningFacing, t);
                yield return null;
            }
            alien.rotation = runningFacing;

            if (cameraFollow != null)
                cameraFollow.enabled = true;

            StartGameplay();
        }

        private void OpenMainMenu()
        {
            menuVisible = true;
            currentPage = MenuPage.Main;
            SetHUDVisible(false);

            var runner = FindFirstObjectByType<EndlessRunnerMovement>();
            if (runner != null) runner.SetMovementEnabled(false);
            if (pauseGameWhileMenuIsOpen) Time.timeScale = 0f;
        }

        public void RestartGame()
        {
            RestartSceneAndStart();
        }

        private void LoadMainAndStart()
        {
            LoadMainAndStart(false);
        }

        private void LoadMainAndStart(bool playOpening)
        {
            if (!Application.CanStreamedLevelBeLoaded("Main"))
            {
                Debug.LogError("Metamorfose Ambulante: adicione Assets/Scenes/Main.unity em File > Build Profiles > Scene List.");
                return;
            }
            startImmediatelyAfterReload = true;
            openingAfterReload = playOpening;
            Time.timeScale = 1f;
            SceneManager.LoadScene("Main");
        }

        private void RestartSceneAndStart() => LoadMainAndStart();

        public void QuitGame()
        {
            Time.timeScale = 1f;

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void HookExistingRestartButton()
        {
            // A UI do fim de jogo é IMGUI; dispensa Unity UI e EventSystem.
        }

        private void DrawGameOverSafetyButtons()
        {
            GUI.depth = -100;
            GUI.Box(new Rect(0, 0, 1920, 1080), GUIContent.none, overlayStyle);
            GUI.Box(new Rect(460, 170, 1000, 720), GUIContent.none, panelStyle);
            var heading = new GUIStyle(titleStyle) { alignment = TextAnchor.MiddleCenter, fontSize = 76 };
            var details = new GUIStyle(infoStyle) { alignment = TextAnchor.MiddleCenter, wordWrap = true, fontSize = 32 };
            GUI.Label(new Rect(500, 215, 920, 120), "GAME OVER", heading);
            GUI.Label(new Rect(530, 335, 860, 205), endReason + "\nDistância: " + endDistance.ToString("0") + " m" +
                "  •  Barreiras corretas: " + endCorrectBarriers + "\nPontuação total: " + endScore.ToString("0"), details);
            GUI.Label(new Rect(530, 550, 860, 60), "Voltando ao menu principal...", details);
            if (GUI.Button(new Rect(650, 640, 620, 80), "MENU PRINCIPAL", buttonStyle)) ReturnToMainMenu();
            if (GUI.Button(new Rect(650, 740, 620, 75), "REINICIAR AGORA", gameOverButtonStyle)) RestartGame();
        }

        private void ResolveSceneObjects()
        {
            distanceHUD = FindSceneObject("Distância");
            stateHUD = FindSceneObject("Estado");
            voiceHUD = FindSceneObject("Voz");
            helpHUD = FindSceneObject("Ajuda");
            micHUD = FindSceneObject("Mic");
            gameOverPanel = FindSceneObject(gameOverPanelName);

            if (gameOverPanel != null)
                gameOverPanel.SetActive(false);
        }

        private void SetHUDVisible(bool visible)
        {
            SetObjectActive(distanceHUD, visible);
            SetObjectActive(stateHUD, visible);
            SetObjectActive(voiceHUD, visible);
            SetObjectActive(helpHUD, visible);
            SetObjectActive(micHUD, visible);
        }

        private static void SetObjectActive(GameObject obj, bool state)
        {
            if (obj != null && obj.activeSelf != state)
                obj.SetActive(state);
        }

        private static GameObject FindSceneObject(string objectName)
        {
            if (string.IsNullOrWhiteSpace(objectName))
                return null;

            GameObject active = GameObject.Find(objectName);

            if (active != null)
                return active;

            Transform[] all = Resources.FindObjectsOfTypeAll<Transform>();

            foreach (Transform transformInScene in all)
            {
                if (transformInScene == null)
                    continue;

                GameObject obj = transformInScene.gameObject;

                if (!obj.scene.IsValid())
                    continue;

                if (obj.name == objectName)
                    return obj;
            }

            return null;
        }

        private void EnsureStyles()
        {
            if (overlayStyle != null)
                return;

            overlayTexture = CreateSolidTexture(
                new Color(0.008f, 0.018f, 0.030f, 0.89f));

            panelTexture = CreateSolidTexture(
                new Color(0.018f, 0.045f, 0.070f, 0.98f));

            buttonTexture = CreateSolidTexture(
                new Color(0.035f, 0.32f, 0.47f, 1f));

            buttonHoverTexture = CreateSolidTexture(
                new Color(0.04f, 0.60f, 0.82f, 1f));

            disabledTexture = CreateSolidTexture(
                new Color(0.10f, 0.15f, 0.18f, 0.90f));

            restartTexture = CreateSolidTexture(
                new Color(0.04f, 0.62f, 0.30f, 1f));

            overlayStyle = new GUIStyle(GUI.skin.box);
            overlayStyle.normal.background = overlayTexture;

            panelStyle = new GUIStyle(GUI.skin.box);
            panelStyle.normal.background = panelTexture;

            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 59,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                wordWrap = true
            };
            titleStyle.normal.textColor =
                new Color(0.88f, 0.97f, 1f, 1f);

            subtitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 24,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            subtitleStyle.normal.textColor =
                new Color(0.16f, 0.75f, 0.96f, 1f);

            infoStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 21,
                alignment = TextAnchor.MiddleLeft,
                wordWrap = true
            };
            infoStyle.normal.textColor =
                new Color(0.72f, 0.82f, 0.87f, 1f);

            sectionTitleStyle = new GUIStyle(titleStyle)
            {
                fontSize = 46,
                alignment = TextAnchor.MiddleCenter
            };

            helpStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 25,
                alignment = TextAnchor.UpperLeft,
                wordWrap = true
            };
            helpStyle.normal.textColor =
                new Color(0.83f, 0.91f, 0.95f, 1f);

            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 26,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            buttonStyle.normal.background = buttonTexture;
            buttonStyle.hover.background = buttonHoverTexture;
            buttonStyle.active.background = buttonHoverTexture;
            buttonStyle.normal.textColor = Color.white;
            buttonStyle.hover.textColor = Color.white;
            buttonStyle.active.textColor = Color.white;

            disabledButtonStyle = new GUIStyle(buttonStyle);
            disabledButtonStyle.normal.background = disabledTexture;
            disabledButtonStyle.normal.textColor =
                new Color(0.55f, 0.60f, 0.63f, 1f);

            gameOverButtonStyle = new GUIStyle(buttonStyle);
            gameOverButtonStyle.normal.background = restartTexture;
        }

        private static Texture2D CreateSolidTexture(Color color)
        {
            Texture2D texture = new Texture2D(
                1,
                1,
                TextureFormat.RGBA32,
                false);

            texture.SetPixel(0, 0, color);
            texture.Apply();

            return texture;
        }

        private static void DestroyTexture(Texture2D texture)
        {
            if (texture == null)
                return;

            if (Application.isPlaying)
                Destroy(texture);
            else
                DestroyImmediate(texture);
        }
    }
}
