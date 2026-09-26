using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Iluminación ambiental día/noche conectada a <see cref="RunDirector"/>:
/// alterna el skybox, la niebla lineal de RenderSettings, la luz direccional
/// y (opcionalmente) el tinte de Color Adjustments del posprocesado.
/// La transición es una mezcla lineal sin asignaciones por frame.
/// </summary>
[DefaultExecutionOrder(-350)]
[DisallowMultipleComponent]
public class EnvironmentLightingController : MonoBehaviour
{
    public static EnvironmentLightingController Instance { get; private set; }

    [Header("Skybox")]
    [Tooltip("Cielo de las rondas de día.")]
    [SerializeField] private Material daySkyboxMaterial;
    [Tooltip("Cielo de las rondas de noche (jefe).")]
    [SerializeField] private Material nightSkyboxMaterial;
    [Tooltip("Refresca la sonda de ambiente al cambiar de skybox (una llamada por transición).")]
    [SerializeField] private bool refreshAmbientProbe = true;

    [Header("Luz direccional")]
    [Tooltip("Si se deja vacío se usa RenderSettings.sun o la primera luz de la escena.")]
    [SerializeField] private Light directionalLight;

    [Header("Ambiente de Día")]
    [SerializeField] private Color dayLightColor = new Color(1f, 0.95f, 0.85f);
    [SerializeField, Min(0f)] private float dayLightIntensity = 1.2f;
    [SerializeField] private Color dayFogColor = new Color(0.78f, 0.68f, 0.52f);
    [SerializeField, Min(0f)] private float dayFogStart = 40f;
    [SerializeField, Min(0f)] private float dayFogEnd = 250f;

    [Header("Ambiente de Noche")]
    [SerializeField] private Color nightLightColor = new Color(0.55f, 0.65f, 1f);
    [SerializeField, Min(0f)] private float nightLightIntensity = 0.35f;
    [SerializeField] private Color nightFogColor = new Color(0.05f, 0.07f, 0.15f);
    [SerializeField, Min(0f)] private float nightFogStart = 25f;
    [SerializeField, Min(0f)] private float nightFogEnd = 160f;

    [Header("Niebla")]
    [Tooltip("Configura RenderSettings.fog en modo Lineal (profundidad barata en GPU). Si está desactivado no se toca la niebla de la escena.")]
    [SerializeField] private bool driveFog = true;

    [Header("Transición")]
    [Tooltip("Duración de la mezcla día↔noche en segundos (0 = instantáneo).")]
    [SerializeField, Min(0f)] private float transitionDuration = 3f;

    [Header("Tinte de posprocesado (opcional)")]
    [Tooltip("Volume URP. Si se deja vacío se busca en la escena al arrancar.")]
    [SerializeField] private Volume postProcessVolume;
    [Tooltip("Viraje cálido de día y frío/sombrío de noche en Color Adjustments.")]
    [SerializeField] private bool tintColorGrade = true;
    [SerializeField] private Color dayColorFilter = new Color(1f, 0.97f, 0.9f, 1f);
    [SerializeField, Range(-100f, 100f)] private float dayContrast = 12f;
    [SerializeField, Range(-100f, 100f)] private float daySaturation = 10f;
    [SerializeField] private Color nightColorFilter = new Color(0.78f, 0.83f, 1f, 1f);
    [SerializeField, Range(-100f, 100f)] private float nightContrast = 4f;
    [SerializeField, Range(-100f, 100f)] private float nightSaturation = -6f;

    [Header("Depuración")]
    [SerializeField] private bool logTransitions = true;

    private RunDirector runDirector;
    private ColorAdjustments colorAdjustments;
    private float currentBlend; // 0 = día · 1 = noche
    private float targetBlend;
    private bool isTransitioning;

    /// <summary>True si el ambiente objetivo es de noche.</summary>
    public bool IsNight => targetBlend > 0.5f;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        ResolveDirectionalLight();
        ResolveColorAdjustments();

        runDirector = RunDirector.Instance != null
            ? RunDirector.Instance
            : FindAnyObjectByType<RunDirector>();

        if (runDirector != null)
        {
            runDirector.OnRoundStarted += HandleRoundStarted;
            runDirector.OnRunEnded += HandleRunEnded;
        }
        else if (logTransitions)
        {
            Debug.LogWarning(
                "[EnvironmentLightingController] No se encontró RunDirector: el ambiente solo se podrá cambiar con ApplyDay()/ApplyNight().",
                this);
        }

        // Estado inicial coherente con la partida (día si aún no hay run activo).
        bool startNight = runDirector != null && runDirector.IsRunActive && runDirector.IsNightRound;
        SetTarget(startNight ? 1f : 0f, immediate: true);
    }

    private void OnDestroy()
    {
        if (runDirector != null)
        {
            runDirector.OnRoundStarted -= HandleRoundStarted;
            runDirector.OnRunEnded -= HandleRunEnded;
        }

        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>Cambia el ambiente a día (mezcla suave según 'Transition Duration').</summary>
    public void ApplyDay() => SetTarget(0f, immediate: false);

    /// <summary>Cambia el ambiente a noche (mezcla suave según 'Transition Duration').</summary>
    public void ApplyNight() => SetTarget(1f, immediate: false);

    private void HandleRoundStarted(int round, bool isNight)
    {
        SetTarget(isNight ? 1f : 0f, immediate: false);
    }

    private void HandleRunEnded(bool victory)
    {
        // Al terminar la partida se vuelve al ambiente por defecto (día).
        SetTarget(0f, immediate: false);
    }

    private void SetTarget(float blend, bool immediate)
    {
        targetBlend = blend;

        if (immediate || transitionDuration <= 0f)
        {
            currentBlend = targetBlend;
            isTransitioning = false;
            ApplyBlend(currentBlend);

            if (logTransitions)
            {
                Debug.Log(
                    $"[EnvironmentLightingController] Ambiente aplicado al instante: {(targetBlend > 0.5f ? "NOCHE" : "DÍA")}.",
                    this);
            }

            return;
        }

        // Nada que mezclar si ya estamos en el objetivo (p. ej. día → día).
        if (Mathf.Approximately(currentBlend, targetBlend)) return;

        if (!isTransitioning)
        {
            isTransitioning = true;

            if (logTransitions)
            {
                Debug.Log(
                    $"[EnvironmentLightingController] Transiciendo hacia {(targetBlend > 0.5f ? "NOCHE" : "DÍA")} ({transitionDuration:0.0}s)...",
                    this);
            }
        }
    }

    private void Update()
    {
        if (!isTransitioning) return;

        float next = Mathf.MoveTowards(currentBlend, targetBlend, Time.unscaledDeltaTime / transitionDuration);

        if (Mathf.Approximately(next, currentBlend)) return;

        currentBlend = next;
        ApplyBlend(currentBlend);

        if (Mathf.Approximately(currentBlend, targetBlend))
        {
            isTransitioning = false;

            if (logTransitions)
            {
                Debug.Log(
                    $"[EnvironmentLightingController] Transición completada: {(currentBlend > 0.5f ? "NOCHE" : "DÍA")}.",
                    this);
            }
        }
    }

    /// <summary>Aplica un punto concreto de la mezcla 0 (día) → 1 (noche). Cero asignaciones.</summary>
    private void ApplyBlend(float t)
    {
        // --- Luz direccional ---
        if (directionalLight != null)
        {
            directionalLight.color = Color.Lerp(dayLightColor, nightLightColor, t);
            directionalLight.intensity = Mathf.Lerp(dayLightIntensity, nightLightIntensity, t);
        }

        // --- Niebla lineal ---
        if (driveFog)
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = Color.Lerp(dayFogColor, nightFogColor, t);
            RenderSettings.fogStartDistance = Mathf.Lerp(dayFogStart, nightFogStart, t);
            RenderSettings.fogEndDistance = Mathf.Lerp(dayFogEnd, nightFogEnd, t);
        }

        // --- Skybox (cambio en el punto medio: los materiales no se interpolan) ---
        bool wantNight = t >= 0.5f;
        Material skybox = wantNight ? nightSkyboxMaterial : daySkyboxMaterial;

        if (skybox != null && RenderSettings.skybox != skybox)
        {
            RenderSettings.skybox = skybox;

            if (refreshAmbientProbe)
            {
                DynamicGI.UpdateEnvironment();
            }
        }

        // --- Color grading del posprocesado (cálido de día, sombrío de noche) ---
        if (tintColorGrade && colorAdjustments != null)
        {
            colorAdjustments.colorFilter.value = Color.Lerp(dayColorFilter, nightColorFilter, t);
            colorAdjustments.contrast.value = Mathf.Lerp(dayContrast, nightContrast, t);
            colorAdjustments.saturation.value = Mathf.Lerp(daySaturation, nightSaturation, t);
        }
    }

    private void ResolveDirectionalLight()
    {
        if (directionalLight != null) return;

        if (RenderSettings.sun != null)
        {
            directionalLight = RenderSettings.sun;
            return;
        }

        Light[] lights = FindObjectsByType<Light>(FindObjectsInactive.Exclude);

        for (int i = 0; i < lights.Length; i++)
        {
            if (lights[i].type == LightType.Directional)
            {
                directionalLight = lights[i];
                return;
            }
        }

        Debug.LogWarning(
            "[EnvironmentLightingController] No se encontró ninguna luz direccional: el color/intensidad de día-noche no se aplicará.",
            this);
    }

    private void ResolveColorAdjustments()
    {
        if (!tintColorGrade) return;

        if (postProcessVolume == null)
        {
            postProcessVolume = FindAnyObjectByType<Volume>();
        }

        if (postProcessVolume == null || postProcessVolume.sharedProfile == null)
        {
            if (logTransitions)
            {
                Debug.Log(
                    "[EnvironmentLightingController] Sin Volume/Perfil de posprocesado: el tinte de color día-noche queda desactivado.",
                    this);
            }

            return;
        }

        // 'profile' instancia una copia privada del perfil: los cambios de grading
        // se aplican en tiempo de ejecución sin ensuciar el asset en el proyecto.
        VolumeProfile volumeProfile = postProcessVolume.profile;

        if (volumeProfile == null || !volumeProfile.TryGet(out colorAdjustments) || colorAdjustments == null)
        {
            colorAdjustments = null;

            if (logTransitions)
            {
                Debug.Log(
                    "[EnvironmentLightingController] El perfil no contiene 'Color Adjustments': sin tinte de color día-noche.",
                    this);
            }
        }
    }

    [ContextMenu("Aplicar día")]
    private void DebugApplyDay() => SetTarget(0f, immediate: true);

    [ContextMenu("Aplicar noche")]
    private void DebugApplyNight() => SetTarget(1f, immediate: true);

    [ContextMenu("Registrar estado")]
    private void DebugState()
    {
        Debug.Log(
            $"[EnvironmentLightingController:'{name}'] Luz: {(directionalLight != null ? directionalLight.name : "NO ASIGNADA")} | " +
            $"Skybox día: {(daySkyboxMaterial != null ? daySkyboxMaterial.name : "NO ASIGNADO")} | " +
            $"Skybox noche: {(nightSkyboxMaterial != null ? nightSkyboxMaterial.name : "NO ASIGNADO")} | " +
            $"Blend: {currentBlend:0.00} → {targetBlend:0.00} | Fog: {RenderSettings.fog} ({RenderSettings.fogMode}) | " +
            $"ColorAdjustments: {(colorAdjustments != null ? "sí" : "no")}",
            this);
    }
}