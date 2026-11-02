using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Events;

namespace AeroMorphET
{
    /// <summary>
    /// Comando de voz local e de baixa latência (sólido / líquido / gasoso).
    ///
    /// Pipeline (tudo em quadros de ~20 ms, sem alocação de memória no loop):
    ///   microfone -> detecção de início/fim da fala -> 26 características (Goertzel) -> vizinho mais próximo -> SetForm()
    ///
    /// Por que é rápido:
    ///  - Lê só as amostras NOVAS do microfone, quadro a quadro (nada de reler/copiar janelas de 300 ms).
    ///  - Goertzel em 12 frequências por quadro (~4 mil multiplicações) no lugar da DFT O(N²) antiga.
    ///  - Decide quando a fala TERMINA (60 ms de silêncio) ou ao atingir maxUtteranceSeconds; não espera janela fixa.
    ///  - Chama o morph diretamente, sem passar por strings/eventos no caminho crítico.
    ///
    /// Não é reconhecimento de fala: é comparação com amostras gravadas por você (calibração).
    /// Grave as sílabas "lí", "só" e "ga" com a SUA voz (e de outros jogadores, se necessário).
    /// Calibração: ligue calibrationMode, F1 = sólido, F2 = líquido, F3 = gasoso, F4 = parar. Fale várias vezes.
    /// </summary>
    public sealed class VoiceInputManager : MonoBehaviour
    {
        public static VoiceInputManager Instance { get; private set; }

        public enum VoiceCommand { None = 0, Solid = 1, Liquid = 2, Gas = 3 }

        [Serializable] public class StringEvent : UnityEvent<string> { }
        public StringEvent onRecognized = new StringEvent();
        public UnityEvent onListeningStarted = new UnityEvent();
        public UnityEvent onListeningStopped = new UnityEvent();

        [Header("Inicialização")]
        [SerializeField] private bool startListeningAutomatically = false;
        [SerializeField] private string microphoneDevice = "";
        [SerializeField] private int requestedSampleRate = 16000;

        [Header("Detecção de fala (VAD)")]
        [Tooltip("Volume mínimo (RMS) para considerar que começou a fala.")]
        [SerializeField, Range(0.002f, 0.1f)] private float voiceThreshold = 0.015f;
        [Tooltip("O limiar real = max(voiceThreshold, ruído de fundo * este valor).")]
        [SerializeField, Range(1.5f, 8f)] private float noiseMultiplier = 3f;
        [Tooltip("Silêncio que marca o fim da palavra. Menor = responde mais rápido.")]
        [SerializeField, Range(0.03f, 0.2f)] private float endSilenceSeconds = 0.06f;
        [Tooltip("Tempo máximo de captura. Se a fala passar disso, decide mesmo assim.")]
        [SerializeField, Range(0.12f, 0.5f)] private float maxUtteranceSeconds = 0.30f;
        [Tooltip("Mínimo de quadros com voz para aceitar (filtra cliques/ruídos).")]
        [SerializeField, Range(2, 10)] private int minVoicedFrames = 3;
        [SerializeField, Range(0f, 0.5f)] private float cooldownSeconds = 0.08f;

        [Header("Classificador")]
        [Tooltip("Distância máxima aceita ao exemplo mais próximo (menor = mais rigoroso).")]
        [SerializeField, Range(0.3f, 3f)] private float maxDistance = 1.4f;
        [Tooltip("Diferença mínima entre 1ª e 2ª classe, em % (0 = sempre escolhe a mais próxima).")]
        [SerializeField, Range(0f, 0.5f)] private float minMargin = 0.08f;

        [Header("Calibração")]
        [SerializeField] private bool calibrationMode = false;
        [SerializeField, Range(1, 200)] private int samplesPerCommand = 30;
        [SerializeField] private bool saveCalibrationAutomatically = true;

        public bool IsListening { get; private set; }
        public string LastRecognized { get; private set; } = "—";
        public float LastConfidence { get; private set; }
        public string Status { get; private set; } = "Parado";

        // ---------- constantes ----------
        private const string CalibrationFile = "aeromorph_voice_templates.json";
        private const int Bands = 12;                       // frequências Goertzel
        private const int FeatureDim = Bands * 2 + 2 + 1;   // 2 metades * bandas + 2 ZCR + 1 razão de energia
        private const float MinFreq = 250f, MaxFreq = 6500f;
        private const float FrameSeconds = 0.02f;

        // ---------- microfone ----------
        private AudioClip clip;
        private int clipSamples;
        private int sampleRate;
        private int frameSize;
        private int readPos;
        private bool micReady;
        private float[] frameBuf;
        private float[] goertzelCoeff;

        // ---------- estado da captura ----------
        private bool capturing;
        private bool needSilence;
        private int frameCount;
        private int silentRun;
        private int voicedFrames;
        private int maxFrames;
        private int endSilenceFrames;
        private float nextAllowedTime;
        private float noiseFloor;

        private float[] capBands;   // [maxFrames * Bands]
        private float[] capZcr;     // [maxFrames]
        private float[] capRms;     // [maxFrames]

        private readonly float[] curBands = new float[Bands];
        private float curZcr, curRms;

        private readonly float[] prevBands = new float[Bands];
        private float prevZcr, prevRms;
        private bool prevValid;

        private readonly float[] feature = new float[FeatureDim];

        // ---------- modelos ----------
        private readonly List<float[]>[] templates =
        {
            new List<float[]>(), new List<float[]>(), new List<float[]>(), new List<float[]>()
        };
        private int calibrationTarget;
        private int calibrationCount;

        private AlienMorphController morphController;

        [Serializable] private class Vec { public float[] v; }
        [Serializable] private class TemplateEntry { public int command; public List<Vec> vectors = new List<Vec>(); }
        [Serializable] private class TemplateSet { public List<TemplateEntry> entries = new List<TemplateEntry>(); }

        // =====================================================================
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            morphController = FindAnyObjectByType<AlienMorphController>();
            LoadTemplates();
        }

        private void Start()
        {
            if (startListeningAutomatically) StartListening();
        }

        public void StartListening()
        {
            if (IsListening) return;
#if UNITY_WEBGL
            SetStatus("Microfone local indisponível no WebGL");
            return;
#else
            try
            {
                string[] devices = Microphone.devices;
                if (devices == null || devices.Length == 0)
                {
                    SetStatus("Nenhum microfone encontrado");
                    UIManager.Instance?.SetVoiceFeedback("Nenhum microfone encontrado", false);
                    return;
                }

                if (string.IsNullOrWhiteSpace(microphoneDevice) || Array.IndexOf(devices, microphoneDevice) < 0)
                    microphoneDevice = devices[0];

                clip = Microphone.Start(microphoneDevice, true, 1, requestedSampleRate);
                if (clip == null) { SetStatus("Falha ao abrir microfone"); return; }

                sampleRate = clip.frequency;
                clipSamples = clip.samples;
                frameSize = ChooseFrameSize(clipSamples, sampleRate);
                frameBuf = new float[frameSize];

                float frameDur = (float)frameSize / sampleRate;
                maxFrames = Mathf.Clamp(Mathf.CeilToInt(maxUtteranceSeconds / frameDur) + 1, 6, 40);
                endSilenceFrames = Mathf.Max(1, Mathf.RoundToInt(endSilenceSeconds / frameDur));
                capBands = new float[maxFrames * Bands];
                capZcr = new float[maxFrames];
                capRms = new float[maxFrames];

                BuildGoertzel();

                micReady = false;
                capturing = false;
                needSilence = false;
                prevValid = false;
                noiseFloor = 0f;
                IsListening = true;
                SetStatus(calibrationMode ? "Calibração: F1 sólido / F2 líquido / F3 gasoso / F4 parar" : "Aguardando voz...");
                UpdateUI();
                onListeningStarted?.Invoke();
            }
            catch (Exception ex)
            {
                IsListening = false;
                SetStatus("Erro no microfone: " + ex.Message);
                Debug.LogException(ex);
            }
#endif
        }

        public void StopListening()
        {
            if (!IsListening && clip == null) return;
            try
            {
#if !UNITY_WEBGL
                if (!string.IsNullOrEmpty(microphoneDevice) && Microphone.IsRecording(microphoneDevice))
                    Microphone.End(microphoneDevice);
#endif
            }
            catch (Exception ex) { Debug.LogWarning("Erro ao parar microfone: " + ex.Message); }

            clip = null;
            IsListening = false;
            capturing = false;
            micReady = false;
            SetStatus("Parado");
            UpdateUI();
            onListeningStopped?.Invoke();
        }

        // =====================================================================
        private void Update()
        {
#if !UNITY_WEBGL
            if (IsListening && clip != null) PumpMicrophone();
#endif
            if (calibrationMode)
            {
                if (Input.GetKeyDown(KeyCode.F1)) BeginCalibration(VoiceCommand.Solid);
                else if (Input.GetKeyDown(KeyCode.F2)) BeginCalibration(VoiceCommand.Liquid);
                else if (Input.GetKeyDown(KeyCode.F3)) BeginCalibration(VoiceCommand.Gas);
                else if (Input.GetKeyDown(KeyCode.F4)) { calibrationTarget = 0; SetStatus("Calibração pausada"); }
            }
        }

#if !UNITY_WEBGL
        private void PumpMicrophone()
        {
            int pos = Microphone.GetPosition(microphoneDevice);
            if (pos < 0) return;

            if (!micReady)
            {
                if (pos <= 0) return;                       // microfone ainda iniciando
                readPos = (pos / frameSize) * frameSize;    // começa alinhado em um quadro
                micReady = true;
                return;
            }

            int available = pos - readPos;
            if (available < 0) available += clipSamples;

            // Se houve travada longa e estamos quase estourando o buffer circular, ressincroniza.
            if (available > clipSamples - frameSize * 2)
            {
                readPos = (pos / frameSize) * frameSize;
                available = 0;
                capturing = false;
            }

            while (available >= frameSize)
            {
                clip.GetData(frameBuf, readPos); // nunca atravessa o fim do buffer (frameSize divide clipSamples)
                readPos += frameSize;
                if (readPos >= clipSamples) readPos = 0;
                available -= frameSize;
                ProcessFrame();
            }
        }
#endif

        // =====================================================================
        private void ProcessFrame()
        {
            ComputeFrame();

            float threshold = Mathf.Max(voiceThreshold, noiseFloor * noiseMultiplier);

            if (!capturing)
            {
                // Aprende o ruído de fundo só quando está quieto.
                if (curRms < threshold)
                    noiseFloor = noiseFloor <= 0f ? curRms : Mathf.Lerp(noiseFloor, curRms, 0.03f);

                if (needSilence)
                {
                    if (curRms < threshold * 0.7f) needSilence = false;
                    StorePrev();
                    return;
                }

                if (curRms >= threshold && Time.unscaledTime >= nextAllowedTime)
                {
                    capturing = true;
                    frameCount = 0;
                    silentRun = 0;
                    voicedFrames = 0;
                    if (prevValid) AppendFrame(prevBands, prevZcr, prevRms); // 20 ms de pré-roll p/ consoantes suaves (l, g)
                    AppendFrame(curBands, curZcr, curRms);
                    voicedFrames++;
                    return;
                }

                StorePrev();
                return;
            }

            // --- capturando ---
            AppendFrame(curBands, curZcr, curRms);
            if (curRms >= threshold * 0.6f) { silentRun = 0; voicedFrames++; }
            else silentRun++;

            bool ended = silentRun >= endSilenceFrames;
            bool full = frameCount >= maxFrames;
            if (!ended && !full) return;

            capturing = false;
            needSilence = full && !ended;  // se cortou no limite, só rearma depois de silêncio
            nextAllowedTime = Time.unscaledTime + cooldownSeconds;
            prevValid = false;

            int usable = frameCount - silentRun;
            if (voicedFrames < minVoicedFrames || usable < 2) return;

            BuildFeature(usable);
            ClassifyOrCalibrate();
        }

        private void StorePrev()
        {
            Array.Copy(curBands, prevBands, Bands);
            prevZcr = curZcr;
            prevRms = curRms;
            prevValid = true;
        }

        private void AppendFrame(float[] bands, float zcr, float rms)
        {
            if (frameCount >= maxFrames) return;
            Array.Copy(bands, 0, capBands, frameCount * Bands, Bands);
            capZcr[frameCount] = zcr;
            capRms[frameCount] = rms;
            frameCount++;
        }

        // ---------- características do quadro ----------
        private void ComputeFrame()
        {
            float[] x = frameBuf;
            int n = frameSize;

            float sum = 0f;
            int zc = 0;
            float prev = x[0];
            for (int i = 0; i < n; i++)
            {
                float s = x[i];
                sum += s * s;
                if ((s >= 0f) != (prev >= 0f)) zc++;
                prev = s;
            }
            curRms = Mathf.Sqrt(sum / n);
            curZcr = (float)zc / n;

            float norm = 1f / ((float)n * n);
            for (int b = 0; b < Bands; b++)
            {
                float coeff = goertzelCoeff[b];
                float s1 = 0f, s2 = 0f;
                for (int i = 0; i < n; i++)
                {
                    float s0 = x[i] + coeff * s1 - s2;
                    s2 = s1;
                    s1 = s0;
                }
                float power = (s1 * s1 + s2 * s2 - coeff * s1 * s2) * norm;
                curBands[b] = Mathf.Log(power + 1e-10f);
            }
        }

        private void BuildGoertzel()
        {
            goertzelCoeff = new float[Bands];
            float maxF = Mathf.Min(MaxFreq, sampleRate * 0.45f);
            float ratio = Mathf.Pow(maxF / MinFreq, 1f / (Bands - 1));
            for (int b = 0; b < Bands; b++)
            {
                float f = MinFreq * Mathf.Pow(ratio, b);
                goertzelCoeff[b] = 2f * Mathf.Cos(2f * Mathf.PI * f / sampleRate);
            }
        }

        private static int ChooseFrameSize(int clipSamples, int rate)
        {
            int target = Mathf.Max(128, Mathf.RoundToInt(rate * FrameSeconds));
            for (int f = target; f >= target - 80 && f > 64; f--)
                if (clipSamples % f == 0) return f;
            for (int f = target + 1; f <= target + 80; f++)
                if (clipSamples % f == 0) return f;
            return target;
        }

        // ---------- vetor final da palavra ----------
        // 1ª metade (ataque: "l", "s", "g") + 2ª metade (vogal) + ZCR + contorno de energia.
        private void BuildFeature(int count)
        {
            int half = Mathf.Max(1, count / 2);
            int aStart = 0, aEnd = half;
            int bStart = count > 1 ? half : 0, bEnd = count;

            float zA, zB, eA, eB;
            HalfMean(aStart, aEnd, 0, out zA, out eA);
            HalfMean(bStart, bEnd, Bands, out zB, out eB);

            feature[Bands * 2] = zA * 4f;
            feature[Bands * 2 + 1] = zB * 4f;
            feature[Bands * 2 + 2] = Mathf.Clamp(Mathf.Log((eB + 1e-5f) / (eA + 1e-5f)), -2f, 2f) * 0.5f;
        }

        private void HalfMean(int start, int end, int offset, out float zcr, out float rms)
        {
            int n = Mathf.Max(1, end - start);
            for (int b = 0; b < Bands; b++) feature[offset + b] = 0f;
            float z = 0f, r = 0f;

            for (int f = start; f < end; f++)
            {
                int baseIdx = f * Bands;
                for (int b = 0; b < Bands; b++) feature[offset + b] += capBands[baseIdx + b];
                z += capZcr[f];
                r += capRms[f];
            }

            float mean = 0f;
            for (int b = 0; b < Bands; b++)
            {
                feature[offset + b] /= n;
                mean += feature[offset + b];
            }
            mean /= Bands;
            // Subtrair a média = invariância ao volume da voz.
            for (int b = 0; b < Bands; b++) feature[offset + b] = (feature[offset + b] - mean) * 0.25f;

            zcr = z / n;
            rms = r / n;
        }

        // ---------- classificação / calibração ----------
        private void ClassifyOrCalibrate()
        {
            if (calibrationMode && calibrationTarget != 0)
            {
                templates[calibrationTarget].Add((float[])feature.Clone());
                calibrationCount++;
                if (saveCalibrationAutomatically) SaveTemplates();

                string name = CommandName((VoiceCommand)calibrationTarget);
                SetStatus(calibrationCount >= samplesPerCommand
                    ? $"{name}: {calibrationCount} amostras (meta atingida). F1/F2/F3 troca de comando."
                    : $"Amostra {calibrationCount}/{samplesPerCommand}: {name}");
                return;
            }

            bool any = templates[1].Count > 0 || templates[2].Count > 0 || templates[3].Count > 0;
            if (!any)
            {
                SetStatus("Sem amostras: ative calibração e grave F1/F2/F3");
                UIManager.Instance?.SetVoiceFeedback("Calibre os comandos F1/F2/F3", false);
                return;
            }

            VoiceCommand cmd;
            float conf;
            if (!TryClassify(out cmd, out conf))
            {
                LastConfidence = conf;
                SetStatus("Não reconhecido");
                return;
            }

            LastConfidence = conf;
            ProcessRecognized(cmd);
        }

        private bool TryClassify(out VoiceCommand command, out float confidence)
        {
            command = VoiceCommand.None;
            confidence = 0f;
            float best = float.MaxValue, second = float.MaxValue;
            int bestClass = 0;

            for (int c = 1; c <= 3; c++)
            {
                List<float[]> list = templates[c];
                if (list.Count == 0) continue;

                float classBest = float.MaxValue;
                for (int i = 0; i < list.Count; i++)
                {
                    float d = Distance(feature, list[i]);
                    if (d < classBest) classBest = d;
                }

                if (classBest < best) { second = best; best = classBest; bestClass = c; }
                else if (classBest < second) second = classBest;
            }

            if (bestClass == 0) return false;

            float margin = second < float.MaxValue ? (second - best) / Mathf.Max(1e-4f, second) : 1f;
            float absolute = Mathf.Clamp01(1f - best / maxDistance);
            confidence = 0.6f * absolute + 0.4f * Mathf.Clamp01(margin * 4f);

            if (best > maxDistance || margin < minMargin) return false;
            command = (VoiceCommand)bestClass;
            return true;
        }

        private static float Distance(float[] a, float[] b)
        {
            int n = Mathf.Min(a.Length, b.Length);
            float sum = 0f;
            for (int i = 0; i < n; i++)
            {
                float d = a[i] - b[i];
                sum += d * d;
            }
            return Mathf.Sqrt(sum / n);
        }

        // ---------- ação ----------
        private void ProcessRecognized(VoiceCommand command)
        {
            if (Time.timeScale <= 0f || GameManager.Instance?.IsGameOver == true) return;

            // Caminho crítico: aplica a forma imediatamente.
            if (morphController == null) morphController = FindAnyObjectByType<AlienMorphController>();
            if (morphController != null)
            {
                switch (command)
                {
                    case VoiceCommand.Solid: morphController.SetForm(AlienMorphController.MorphState.Solid); break;
                    case VoiceCommand.Liquid: morphController.SetForm(AlienMorphController.MorphState.Liquid); break;
                    case VoiceCommand.Gas: morphController.SetForm(AlienMorphController.MorphState.Gas); break;
                }
            }

            string name = CommandName(command);
            LastRecognized = name;
            onRecognized?.Invoke(name);
            UIManager.Instance?.SetVoiceFeedback("Comando: " + name, true);
            SetStatus("Comando executado: " + name);
        }

        /// <summary>Simula um comando (teclado/botão/teste). Aceita "solido", "liquido", "gas" (com ou sem acento).</summary>
        public void SimulateKeyword(string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword)) return;
            string k = keyword.Trim().ToLowerInvariant();
            if (k.StartsWith("s")) ProcessRecognized(VoiceCommand.Solid);
            else if (k.StartsWith("l")) ProcessRecognized(VoiceCommand.Liquid);
            else if (k.StartsWith("g")) ProcessRecognized(VoiceCommand.Gas);
        }

        // ---------- calibração ----------
        private void BeginCalibration(VoiceCommand command)
        {
            calibrationTarget = (int)command;
            calibrationCount = templates[calibrationTarget].Count;
            string name = CommandName(command);
            SetStatus($"Fale '{name}' agora. Repita para adicionar amostras.");
            UIManager.Instance?.SetVoiceFeedback("Grave: " + name, true);
        }

        public void ClearCalibration()
        {
            for (int i = 1; i <= 3; i++) templates[i].Clear();
            calibrationCount = 0;
            SaveTemplates();
            SetStatus("Calibração apagada");
        }

        private void SaveTemplates()
        {
            try
            {
                var data = new TemplateSet();
                for (int c = 1; c <= 3; c++)
                {
                    var entry = new TemplateEntry { command = c };
                    foreach (var v in templates[c]) entry.vectors.Add(new Vec { v = v });
                    data.entries.Add(entry);
                }
                File.WriteAllText(Path.Combine(Application.persistentDataPath, CalibrationFile),
                                  JsonUtility.ToJson(data), Encoding.UTF8);
            }
            catch (Exception ex) { Debug.LogWarning("Falha ao salvar calibração: " + ex.Message); }
        }

        private void LoadTemplates()
        {
            try
            {
                string path = Path.Combine(Application.persistentDataPath, CalibrationFile);
                if (!File.Exists(path)) return;
                var data = JsonUtility.FromJson<TemplateSet>(File.ReadAllText(path, Encoding.UTF8));
                if (data?.entries == null) return;

                for (int i = 1; i <= 3; i++) templates[i].Clear();
                foreach (var entry in data.entries)
                {
                    if (entry.command < 1 || entry.command > 3) continue;
                    foreach (var vec in entry.vectors)
                        if (vec?.v != null && vec.v.Length == FeatureDim)   // descarta arquivos do formato antigo
                            templates[entry.command].Add(vec.v);
                }
            }
            catch (Exception ex) { Debug.LogWarning("Falha ao carregar calibração: " + ex.Message); }
        }

        // ---------- utilidades ----------
        private static string CommandName(VoiceCommand command)
        {
            switch (command)
            {
                case VoiceCommand.Solid: return "sólido";
                case VoiceCommand.Liquid: return "líquido";
                case VoiceCommand.Gas: return "gasoso";
                default: return "—";
            }
        }

        private void SetStatus(string status) { Status = status; }
        private void UpdateUI() => UIManager.Instance?.SetMicrophoneState(IsListening);

        private void OnDestroy()
        {
            StopListening();
            if (Instance == this) Instance = null;
        }
    }
}
