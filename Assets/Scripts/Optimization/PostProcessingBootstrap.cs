using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Garantiza al arrancar que exista un Global Volume de URP con posprocesado ligero
/// para móvil (Bloom, Vignette, Color Adjustments y Tonemapping) y activa
/// 'Render Post Processing' en la cámara principal.
/// Si no se asigna un perfil en el Inspector se crea uno temporal con valores
/// pensados para 60 FPS en gama media/baja.
/// </summary>
[DefaultExecutionOrder(-850)]
[DisallowMultipleComponent]
public class PostProcessingBootstrap : MonoBehaviour
{
    [Header("Perfil de posprocesado")]
    [Tooltip("Perfil URP (p. ej. Assets/Extra/Settings/SampleSceneProfile). Si se deja vacío se crea uno temporal con los valores móviles de abajo.")]
    [SerializeField] private VolumeProfile profile;

    [Header("Global Volume")]
    [Tooltip("Crea un 'Global Volume' si la escena no tiene ninguno.")]
    [SerializeField] private bool createGlobalVolumeIfMissing = true;
    [Tooltip("Asigna el perfil al Volume existente solo si no tiene ninguno configurado.")]
    [SerializeField] private bool assignProfileIfEmpty = true;

    [Header("Cámara")]
    [Tooltip("Cámara que renderiza el juego. Si se deja vacía se usa Camera.main.")]
    [SerializeField] private Camera targetCamera;
    [Tooltip("Activa 'Render Post Processing' en la cámara al arrancar.")]
    [SerializeField] private bool enablePostProcessingOnCamera = true;

    [Header("Valores móviles (solo si NO hay perfil asignado)")]
    [SerializeField, Min(0f)] private float bloomThreshold = 0.85f;
    [SerializeField, Min(0f)] private float bloomIntensity = 0.4f;
    [SerializeField, Range(0f, 1f)] private float bloomScatter = 0.6f;
    [Tooltip("Dual: equilibrio entre calidad y coste en móviles.")]
    [SerializeField] private BloomFilterMode bloomFilterMode = BloomFilterMode.Dual;
    [Tooltip("Quarter: el Bloom empieza a 1/4 de resolución (gran ahorro en gama baja).")]
    [SerializeField] private BloomDownscaleMode bloomDownscale = BloomDownscaleMode.Quarter;
    [SerializeField, Range(0f, 1f)] private float vignetteIntensity = 0.28f;
    [SerializeField, Range(0.01f, 1f)] private float vignetteSmoothness = 0.35f;
    [SerializeField] private float postExposure = 0.15f;
    [SerializeField, Range(-100f, 100f)] private float contrast = 12f;
    [SerializeField, Range(-100f, 100f)] private float saturation = 10f;
    [Tooltip("Tono cálido: multiplicador del render en Color Adjustments.")]
    [SerializeField] private Color colorFilter = new Color(1f, 0.96f, 0.9f, 1f);
    [Tooltip("Neutral: re-mapeo de rango barato y buen punto de partida del grading.")]
    [SerializeField] private TonemappingMode tonemappingMode = TonemappingMode.Neutral;

    [Header("Depuración")]
    [SerializeField] private bool logSetup = true;

    private VolumeProfile runtimeProfile;

    private void Awake()
    {
        EnsureCameraPostProcessing();
        EnsureGlobalVolume();
    }

    /// <summary>Activa 'Render Post Processing' en la cámara objetivo (o en Camera.main).</summary>
    private void EnsureCameraPostProcessing()
    {
        if (!enablePostProcessingOnCamera) return;

        Camera cam = targetCamera != null ? targetCamera : Camera.main;

        if (cam == null)
        {
            if (logSetup)
            {
                Debug.LogWarning("[PostProcessingBootstrap] No se encontró cámara: no se pudo activar el posprocesado.", this);
            }

            return;
        }

        UniversalAdditionalCameraData cameraData = cam.GetUniversalAdditionalCameraData();

        if (cameraData == null) return;

        if (!cameraData.renderPostProcessing)
        {
            cameraData.renderPostProcessing = true;

            if (logSetup)
            {
                Debug.Log($"[PostProcessingBootstrap] 'Render Post Processing' activado en '{cam.name}'.", cam);
            }
        }
    }

    /// <summary>Busca un Volume global en escena o lo crea, y le asigna el perfil.</summary>
    private void EnsureGlobalVolume()
    {
        Volume volume = FindAnyObjectByType<Volume>();

        if (volume == null)
        {
            if (!createGlobalVolumeIfMissing)
            {
                if (logSetup)
                {
                    Debug.LogWarning("[PostProcessingBootstrap] No hay Volume en la escena y la creación está desactivada.", this);
                }

                return;
            }

            GameObject volumeObject = new GameObject("Global Volume");
            volume = volumeObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 0f;
            volume.weight = 1f;

            if (logSetup)
            {
                Debug.Log("[PostProcessingBootstrap] Se creó un 'Global Volume' en la escena.", volume);
            }
        }

        if (volume.sharedProfile == null && assignProfileIfEmpty)
        {
            VolumeProfile resolved = profile != null ? profile : GetOrCreateRuntimeProfile();

            if (resolved != null)
            {
                volume.sharedProfile = resolved;

                if (logSetup)
                {
                    Debug.Log(
                        $"[PostProcessingBootstrap] Perfil '{resolved.name}' asignado al Volume '{volume.name}'.",
                        volume);
                }
            }
        }
        else if (logSetup && volume.sharedProfile != null)
        {
            Debug.Log(
                $"[PostProcessingBootstrap] Volume '{volume.name}' ya usa el perfil '{volume.sharedProfile.name}': se respeta la configuración.",
                volume);
        }
    }

    /// <summary>Devuelve el perfil del Inspector o genera uno ligero en tiempo de ejecución.</summary>
    private VolumeProfile GetOrCreateRuntimeProfile()
    {
        if (profile != null) return profile;

        if (runtimeProfile == null)
        {
            runtimeProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            runtimeProfile.name = "RuntimeMobilePostProcess";

            Bloom bloom = runtimeProfile.Add<Bloom>(overrides: true);
            bloom.threshold.value = bloomThreshold;
            bloom.intensity.value = bloomIntensity;
            bloom.scatter.value = bloomScatter;
            bloom.highQualityFiltering.value = false;
            bloom.filter.value = bloomFilterMode;
            bloom.downscale.value = bloomDownscale;

            Vignette vignette = runtimeProfile.Add<Vignette>(overrides: true);
            vignette.color.value = new Color(0f, 0f, 0f, 1f);
            vignette.intensity.value = vignetteIntensity;
            vignette.smoothness.value = vignetteSmoothness;
            vignette.rounded.value = false;

            ColorAdjustments colorAdjustments = runtimeProfile.Add<ColorAdjustments>(overrides: true);
            colorAdjustments.postExposure.value = postExposure;
            colorAdjustments.contrast.value = contrast;
            colorAdjustments.saturation.value = saturation;
            colorAdjustments.colorFilter.value = colorFilter;
            colorAdjustments.hueShift.value = 0f;

            Tonemapping tonemapping = runtimeProfile.Add<Tonemapping>(overrides: true);
            tonemapping.mode.value = tonemappingMode;

            if (logSetup)
            {
                Debug.Log(
                    "[PostProcessingBootstrap] Perfil de runtime creado: Bloom (Fast/Low) + Vignette + Color Adjustments + Tonemapping.",
                    runtimeProfile);
            }
        }

        return runtimeProfile;
    }
}