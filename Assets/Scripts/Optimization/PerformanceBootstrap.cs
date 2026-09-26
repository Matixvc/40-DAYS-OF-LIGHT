using UnityEngine;

/// <summary>
/// Ajustes de rendimiento al arrancar la escena, pensados para Android de gama media/baja.
/// En escritorio mantiene la configuración de calidad del proyecto.
/// </summary>
[DefaultExecutionOrder(-900)]
[DisallowMultipleComponent]
public class PerformanceBootstrap : MonoBehaviour
{
    [Header("Objetivo de FPS")]
    [SerializeField] private int targetFrameRate = 60;
    [Tooltip("Desactiva VSync en móvil (necesario para que targetFrameRate se aplique de verdad).")]
    [SerializeField] private bool disableVSyncOnMobile = true;

    [Header("Solo móvil")]
    [Tooltip("Nivel de calidad a aplicar en móvil (-1 = no cambiar).")]
    [SerializeField] private int mobileQualityLevel = -1;
    [Tooltip("Desactiva las sombras en tiempo real en móvil (gran ahorro en gama baja).")]
    [SerializeField] private bool disableRealtimeShadowsOnMobile = false;

    [Header("Depuración")]
    [SerializeField] private bool logSettings = true;

    private void Awake()
    {
        bool isMobile = Application.isMobilePlatform;

        if (Application.targetFrameRate != targetFrameRate)
        {
            Application.targetFrameRate = targetFrameRate;
        }

        if (isMobile)
        {
            if (disableVSyncOnMobile)
            {
                QualitySettings.vSyncCount = 0;
            }

            if (mobileQualityLevel >= 0 && mobileQualityLevel < QualitySettings.names.Length)
            {
                QualitySettings.SetQualityLevel(mobileQualityLevel, true);
            }

            if (disableRealtimeShadowsOnMobile)
            {
                QualitySettings.shadows = ShadowQuality.Disable;
            }
        }

        if (logSettings)
        {
            Debug.Log(
                $"[PerformanceBootstrap] Móvil: {isMobile} | targetFrameRate: {Application.targetFrameRate} | " +
                $"vSync: {QualitySettings.vSyncCount} | Nivel de calidad: {QualitySettings.GetQualityLevel()} | " +
                $"Sombras: {QualitySettings.shadows}",
                this);
        }
    }
}
