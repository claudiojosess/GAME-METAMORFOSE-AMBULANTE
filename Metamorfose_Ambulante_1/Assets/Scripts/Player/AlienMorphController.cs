using System;
using System.Collections.Generic;
using UnityEngine;

namespace AeroMorphET
{
    public sealed class AlienMorphController : MonoBehaviour
    {
        public enum MorphState { Solid, Liquid, Gas }

        public MorphState CurrentState { get; private set; } = MorphState.Solid;
        public event Action<MorphState> StateChanged;

        [Header("Visuais das formas")]
        [SerializeField] private GameObject solidVisualRoot;
        [SerializeField] private GameObject liquidVisualRoot;
        [SerializeField] private GameObject gasVisualRoot;

        [Header("Colisores por forma")]
        [SerializeField] private BoxCollider solidCollider;
        [SerializeField] private CapsuleCollider liquidCollider;
        [SerializeField] private SphereCollider gasCollider;

        [Header("Transição visual")]
        [SerializeField] private float transitionSpeed = 10f;
        [SerializeField] private float liquidCrawlSpeed = 7f;
        [SerializeField] private float gasDriftSpeed = 4f;

        [Header("Animação do ET sólido")]
        [SerializeField] private float armSwingSpeed = 6.2f;
        [SerializeField] private float armSwingAngle = 24f;
        [SerializeField] private float headBobSpeed = 2.4f;
        [SerializeField] private float headBobAmount = 0.035f;
        [SerializeField] private float headTiltAngle = 4.5f;
        [SerializeField] private float blinkInterval = 3.2f;
        [SerializeField] private float blinkDuration = 0.14f;

        private GameObject activeVisual;

        // Rig simples da forma sólida.
        private Transform solidHeadRoot;
        private Transform solidLeftArmRoot;
        private Transform solidRightArmRoot;
        private Transform solidLeftEye;
        private Transform solidRightEye;

        private Vector3 headBasePosition;
        private Quaternion headBaseRotation;
        private Quaternion leftArmBaseRotation;
        private Quaternion rightArmBaseRotation;
        private Vector3 leftEyeBaseScale;
        private Vector3 rightEyeBaseScale;
        private float nextBlinkTime;
        private float blinkEndTime;
        private bool blinking;

        private readonly List<Transform> liquidBlobs = new();
        private readonly List<Vector3> liquidBasePositions = new();
        private readonly List<Vector3> liquidBaseScales = new();
        private readonly List<Transform> gasMolecules = new();
        private readonly List<Vector3> gasBasePositions = new();
        private readonly List<Vector3> gasBaseScales = new();

        private Transform leftRunLeg, rightRunLeg;
        private Quaternion leftLegRest, rightLegRest;

        private void Awake()
        {
            ResolveVisualRoots();
            AddRoadRunnerDetails();
            CacheSolidRig();
            if (solidVisualRoot != null)
            {
                leftRunLeg = solidVisualRoot.transform.Find("Coxa_E");
                rightRunLeg = solidVisualRoot.transform.Find("Coxa_D");
                if (leftRunLeg != null) leftLegRest = leftRunLeg.localRotation;
                if (rightRunLeg != null) rightLegRest = rightRunLeg.localRotation;
            }
            CacheAnimatedPieces();
            ScheduleNextBlink();
            SetForm(CurrentState, true);
        }

        private void Update()
        {
            if (Time.timeScale <= 0f || GameManager.Instance?.IsGameOver == true) return;
            if (Input.GetKeyDown(KeyCode.Alpha1)) SetForm(MorphState.Solid);
            if (Input.GetKeyDown(KeyCode.Alpha2)) SetForm(MorphState.Liquid);
            if (Input.GetKeyDown(KeyCode.Alpha3)) SetForm(MorphState.Gas);

            if (activeVisual != null)
            {
                activeVisual.transform.localScale = Vector3.Lerp(
                    activeVisual.transform.localScale,
                    CurrentState == MorphState.Solid && GetComponent<EndlessRunnerMovement>()?.IsSliding == true
                        ? new Vector3(1f, .58f, 1f) : Vector3.one,
                    Time.deltaTime * transitionSpeed);
            }

            switch (CurrentState)
            {
                case MorphState.Solid:
                    AnimateSolid();
                    break;
                case MorphState.Liquid:
                    AnimateLiquid();
                    break;
                case MorphState.Gas:
                    AnimateGas();
                    break;
            }
        }

        public void SetForm(MorphState state) => SetForm(state, false);

        private void SetForm(MorphState state, bool immediate)
        {
            if (!immediate && CurrentState != state) GameAudio.Play("Transformar");
            CurrentState = state;

            SetActive(solidVisualRoot, state == MorphState.Solid);
            SetActive(liquidVisualRoot, state == MorphState.Liquid);
            SetActive(gasVisualRoot, state == MorphState.Gas);

            if (solidCollider != null) solidCollider.enabled = state == MorphState.Solid;
            if (liquidCollider != null) liquidCollider.enabled = state == MorphState.Liquid;
            if (gasCollider != null) gasCollider.enabled = state == MorphState.Gas;

            activeVisual = state switch
            {
                MorphState.Solid => solidVisualRoot,
                MorphState.Liquid => liquidVisualRoot,
                _ => gasVisualRoot
            };

            ResetAnimatedPieces();

            if (activeVisual != null)
            {
                activeVisual.transform.localScale = Vector3.one;
            }

            StateChanged?.Invoke(state);
            UIManager.Instance?.SetMorphState(state);
        }

        private void AddRoadRunnerDetails()
        {
            if (solidVisualRoot == null || solidVisualRoot.transform.Find("Marca_Dorsal") != null) return;
            var head = solidVisualRoot.transform.Find("CabecaRoot");
            var torso = solidVisualRoot.transform.Find("Torso");
            if (head == null || torso == null) return;
            var bodyRenderer = torso.GetComponent<Renderer>();
            var antenna = head.Find("Antena_E");
            Material bodyMat = bodyRenderer != null ? bodyRenderer.sharedMaterial : null;
            Material glowMat = antenna != null ? antenna.GetComponent<Renderer>().sharedMaterial : bodyMat;
            AddDetail(solidVisualRoot.transform, "Marca_Dorsal", PrimitiveType.Capsule,
                new Vector3(0f, -.16f, -.43f), new Vector3(.17f, .37f, .09f), glowMat);
            AddDetail(solidVisualRoot.transform, "Cinto_Bioluminescente", PrimitiveType.Sphere,
                new Vector3(0f, -.61f, -.29f), new Vector3(.52f, .12f, .13f), glowMat);
            AddDetail(head, "Marca_Craniana_Traseira", PrimitiveType.Sphere,
                new Vector3(0f, 1.03f, -.47f), new Vector3(.27f, .36f, .085f), glowMat);
            foreach (int side in new[] { -1, 1 })
            {
                AddDetail(solidVisualRoot.transform, side < 0 ? "Luz_Ombro_E" : "Luz_Ombro_D", PrimitiveType.Sphere,
                    new Vector3(side * .43f, -.02f, -.31f), new Vector3(.16f, .12f, .10f), glowMat);
                AddDetail(solidVisualRoot.transform, side < 0 ? "Bota_E" : "Bota_D", PrimitiveType.Sphere,
                    new Vector3(side * .18f, -1.23f, -.05f), new Vector3(.31f, .12f, .32f), bodyMat);
            }
        }

        private static void AddDetail(Transform parent, string name, PrimitiveType type,
            Vector3 position, Vector3 scale, Material material)
        {
            var obj = GameObject.CreatePrimitive(type);
            obj.name = name;
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = position;
            obj.transform.localScale = scale;
            obj.GetComponent<Renderer>().sharedMaterial = material;
            var collider = obj.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
        }

        private void AnimateSolid()
        {
            if (solidVisualRoot == null) return;

            // Respiração muito sutil para o corpo não parecer estático.
            float breath = 1f + Mathf.Sin(Time.time * 3.2f) * .014f;
            solidVisualRoot.transform.localScale = Vector3.Lerp(
                solidVisualRoot.transform.localScale,
                new Vector3(1f, breath, 1f),
                Time.deltaTime * transitionSpeed);

            // Braços se movimentam em sentidos opostos como numa corrida leve.
            float swing = Mathf.Sin(Time.time * armSwingSpeed) * armSwingAngle;

            if (solidLeftArmRoot != null)
            {
                Quaternion target = leftArmBaseRotation * Quaternion.Euler(swing, 0f, 0f);
                solidLeftArmRoot.localRotation = Quaternion.Slerp(
                    solidLeftArmRoot.localRotation,
                    target,
                    Time.deltaTime * 12f);
            }

            if (solidRightArmRoot != null)
            {
                Quaternion target = rightArmBaseRotation * Quaternion.Euler(-swing, 0f, 0f);
                solidRightArmRoot.localRotation = Quaternion.Slerp(
                    solidRightArmRoot.localRotation,
                    target,
                    Time.deltaTime * 12f);
            }

            // A cabeça flutua/balança suavemente, reforçando o ar simpático do ET.
            if (solidHeadRoot != null)
            {
                float t = Time.time * headBobSpeed;
                Vector3 targetPos = headBasePosition + new Vector3(
                    Mathf.Sin(t * .65f) * .012f,
                    Mathf.Sin(t) * headBobAmount,
                    0f);

                Quaternion targetRot = headBaseRotation * Quaternion.Euler(
                    Mathf.Sin(t * .72f) * 1.8f,
                    Mathf.Sin(t * .43f) * 2.2f,
                    Mathf.Sin(t * .58f) * headTiltAngle);

                solidHeadRoot.localPosition = Vector3.Lerp(
                    solidHeadRoot.localPosition,
                    targetPos,
                    Time.deltaTime * 8f);

                solidHeadRoot.localRotation = Quaternion.Slerp(
                    solidHeadRoot.localRotation,
                    targetRot,
                    Time.deltaTime * 8f);
            }

            // Passos alternados são visíveis pela câmera atrás do corredor.
            var runner = GetComponent<EndlessRunnerMovement>();
            float runFactor = runner != null && runner.MovementEnabled ? 1f : 0f;
            float stride = Mathf.Sin(Time.time * 11f) * 24f * runFactor;
            if (leftRunLeg != null) leftRunLeg.localRotation = leftLegRest * Quaternion.Euler(stride, 0f, 0f);
            if (rightRunLeg != null) rightRunLeg.localRotation = rightLegRest * Quaternion.Euler(-stride, 0f, 0f);
            AnimateBlink();
        }

        private void AnimateBlink()
        {
            if (solidLeftEye == null || solidRightEye == null) return;

            if (!blinking && Time.time >= nextBlinkTime)
            {
                blinking = true;
                blinkEndTime = Time.time + blinkDuration;
            }

            float eyeYFactor = 1f;

            if (blinking)
            {
                float remaining = Mathf.Max(0f, blinkEndTime - Time.time);
                float half = Mathf.Max(.01f, blinkDuration * .5f);
                float progress = 1f - remaining / Mathf.Max(.01f, blinkDuration);

                // Fecha na primeira metade e abre na segunda.
                eyeYFactor = progress < .5f
                    ? Mathf.Lerp(1f, .08f, progress / .5f)
                    : Mathf.Lerp(.08f, 1f, (progress - .5f) / .5f);

                if (Time.time >= blinkEndTime)
                {
                    blinking = false;
                    eyeYFactor = 1f;
                    ScheduleNextBlink();
                }
            }

            Vector3 leftScale = leftEyeBaseScale;
            leftScale.y *= eyeYFactor;
            solidLeftEye.localScale = leftScale;

            Vector3 rightScale = rightEyeBaseScale;
            rightScale.y *= eyeYFactor;
            solidRightEye.localScale = rightScale;
        }

        private void ScheduleNextBlink()
        {
            // Pequena variação evita uma piscada robótica com intervalo sempre idêntico.
            nextBlinkTime = Time.time + blinkInterval + UnityEngine.Random.Range(-.55f, .85f);
        }

        private void AnimateLiquid()
        {
            if (liquidVisualRoot == null) return;

            // A poça "rasteja": cada gota avança e recua em ondas diferentes.
            for (int i = 0; i < liquidBlobs.Count; i++)
            {
                float phase = i * .85f;
                float wave = Mathf.Sin(Time.time * liquidCrawlSpeed + phase);
                float sideWave = Mathf.Cos(Time.time * liquidCrawlSpeed * .55f + phase) * .04f;

                Vector3 basePos = liquidBasePositions[i];
                liquidBlobs[i].localPosition = basePos + new Vector3(
                    sideWave,
                    Mathf.Abs(wave) * .025f,
                    wave * .11f);

                Vector3 baseScale = liquidBaseScales[i];
                liquidBlobs[i].localScale = new Vector3(
                    baseScale.x * (1f + wave * .08f),
                    baseScale.y * (1f - Mathf.Abs(wave) * .08f),
                    baseScale.z * (1f - wave * .06f));
            }
        }

        private void AnimateGas()
        {
            if (gasVisualRoot == null) return;

            // As moléculas se afastam e flutuam independentemente, simulando um gás.
            for (int i = 0; i < gasMolecules.Count; i++)
            {
                float phase = i * 1.37f;
                float t = Time.time * gasDriftSpeed;
                Vector3 basePos = gasBasePositions[i];

                Vector3 drift = new(
                    Mathf.Sin(t * .72f + phase) * .13f,
                    Mathf.Cos(t * .93f + phase * .7f) * .16f,
                    Mathf.Sin(t * .61f + phase * 1.15f) * .12f);

                gasMolecules[i].localPosition = basePos + drift;

                float pulse = 1f + Mathf.Sin(t * 1.4f + phase) * .12f;
                gasMolecules[i].localScale = gasBaseScales[i] * pulse;
            }

            gasVisualRoot.transform.localRotation = Quaternion.Euler(
                0f,
                Mathf.Sin(Time.time * 1.2f) * 7f,
                Mathf.Cos(Time.time * .8f) * 3f);
        }

        private void ResolveVisualRoots()
        {
            if (solidVisualRoot == null)
            {
                Transform t = transform.Find("Forma_Solida_ET");
                if (t != null) solidVisualRoot = t.gameObject;
            }

            if (liquidVisualRoot == null)
            {
                Transform t = transform.Find("Forma_Liquida_Rastejante");
                if (t != null) liquidVisualRoot = t.gameObject;
            }

            if (gasVisualRoot == null)
            {
                Transform t = transform.Find("Forma_Gasosa_Molecular");
                if (t != null) gasVisualRoot = t.gameObject;
            }
        }

        private void CacheSolidRig()
        {
            if (solidVisualRoot == null) return;

            solidHeadRoot = solidVisualRoot.transform.Find("CabecaRoot");
            solidLeftArmRoot = solidVisualRoot.transform.Find("BracoRoot_E");
            solidRightArmRoot = solidVisualRoot.transform.Find("BracoRoot_D");

            if (solidHeadRoot != null)
            {
                headBasePosition = solidHeadRoot.localPosition;
                headBaseRotation = solidHeadRoot.localRotation;
                solidLeftEye = solidHeadRoot.Find("Olho_E");
                solidRightEye = solidHeadRoot.Find("Olho_D");
            }

            if (solidLeftArmRoot != null)
                leftArmBaseRotation = solidLeftArmRoot.localRotation;

            if (solidRightArmRoot != null)
                rightArmBaseRotation = solidRightArmRoot.localRotation;

            if (solidLeftEye != null)
                leftEyeBaseScale = solidLeftEye.localScale;

            if (solidRightEye != null)
                rightEyeBaseScale = solidRightEye.localScale;
        }

        private void CacheAnimatedPieces()
        {
            liquidBlobs.Clear();
            liquidBasePositions.Clear();
            liquidBaseScales.Clear();
            gasMolecules.Clear();
            gasBasePositions.Clear();
            gasBaseScales.Clear();

            if (liquidVisualRoot != null)
            {
                foreach (Transform child in liquidVisualRoot.transform)
                {
                    if (!child.name.StartsWith("Gota_")) continue;
                    liquidBlobs.Add(child);
                    liquidBasePositions.Add(child.localPosition);
                    liquidBaseScales.Add(child.localScale);
                }
            }

            if (gasVisualRoot != null)
            {
                foreach (Transform child in gasVisualRoot.transform)
                {
                    if (!child.name.StartsWith("Molecula_")) continue;
                    gasMolecules.Add(child);
                    gasBasePositions.Add(child.localPosition);
                    gasBaseScales.Add(child.localScale);
                }
            }
        }

        private void ResetAnimatedPieces()
        {
            ResetSolidPose();

            for (int i = 0; i < liquidBlobs.Count; i++)
            {
                liquidBlobs[i].localPosition = liquidBasePositions[i];
                liquidBlobs[i].localScale = liquidBaseScales[i];
            }

            for (int i = 0; i < gasMolecules.Count; i++)
            {
                gasMolecules[i].localPosition = gasBasePositions[i];
                gasMolecules[i].localScale = gasBaseScales[i];
            }

            if (gasVisualRoot != null)
                gasVisualRoot.transform.localRotation = Quaternion.identity;
        }

        private void ResetSolidPose()
        {
            if (solidHeadRoot != null)
            {
                solidHeadRoot.localPosition = headBasePosition;
                solidHeadRoot.localRotation = headBaseRotation;
            }

            if (solidLeftArmRoot != null)
                solidLeftArmRoot.localRotation = leftArmBaseRotation;

            if (solidRightArmRoot != null)
                solidRightArmRoot.localRotation = rightArmBaseRotation;

            if (solidLeftEye != null)
                solidLeftEye.localScale = leftEyeBaseScale;

            if (solidRightEye != null)
                solidRightEye.localScale = rightEyeBaseScale;

            blinking = false;
        }

        private static void SetActive(GameObject obj, bool value)
        {
            if (obj != null && obj.activeSelf != value)
                obj.SetActive(value);
        }
    }
}
