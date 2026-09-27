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
    [Tooltip("Límite de fotogramas por segundo para evitar sobrecarga y GPU Timeout (TDR).")]
    [SerializeField] private int targetFrameRate = 120;
    [Tooltip("Desactiva VSync para que targetFrameRate se respete rigurosamente tanto en móvil como en escritorio.")]
    [SerializeField] private bool disableVSync = true;
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

        // Limita la tasa de fotogramas objetivo a 120 para evitar renderizado descontrolado y colapsos por GPU TDR
        Application.targetFrameRate = targetFrameRate;

        if (disableVSync || isMobile)
        {
            QualitySettings.vSyncCount = 0;
        }

        if (isMobile)
        {
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
