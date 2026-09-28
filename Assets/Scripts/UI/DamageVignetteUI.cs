using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Viñeta de daño: un flash rojo en los bordes de la pantalla cada vez que el jugador
/// recibe daño, más un pulso tenue y persistente cuando su vida baja del umbral.
///
/// POR QUÉ <see cref="Time.unscaledDeltaTime"/> Y NO <see cref="Time.deltaTime"/>:
/// este feedback tiene que verse DURANTE la micro-pausa de impacto (<c>timeScale ≈ 0.05</c>),
/// con el panel de nivel abierto (<c>timeScale = 0</c>) y en el game over. Con tiempo escalado
/// el flash se congelaría a media imagen o duraría veinte veces más de lo previsto: justo los
/// tres momentos en los que el jugador más lo necesita.
///
/// CERO GC Y SIN CORRUTINAS: un temporizador y una interpolación en Update, igual que
/// <see cref="CameraShake"/> y <see cref="DamageNumber"/>. Una corrutina por impacto generaría
/// basura en el ciclo de vida del componente, que es justo lo que este proyecto evita.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Image))]
[AddComponentMenu("UI/Damage Vignette")]
public class DamageVignetteUI : MonoBehaviour
{
    [Header("Objetivo")]
    [Tooltip("HealthComponent del jugador. Si se deja vacío se busca al arrancar.")]
    [SerializeField] private HealthComponent targetHealth;

    [Header("Referencias UI")]
    [Tooltip("Imagen a pantalla completa con el sprite de viñeta radial. Si se vacía se usa la de este objeto.")]
    [SerializeField] private Image vignetteImage;

    [Header("Flash de daño")]
    [Tooltip("Alpha máximo del flash al recibir un golpe. 0.55 es intenso sin tapar el centro de la pantalla.")]
    [Range(0f, 1f)][SerializeField] private float flashAlpha = 0.55f;
    [Tooltip("Segundos REALES que tarda el flash en desaparecer.")]
    [SerializeField] private float flashDuration = 0.35f;
    [SerializeField] private Color flashColor = new Color(0.85f, 0.05f, 0.05f, 1f);

    [Header("Aviso de vida baja")]
    [Tooltip("Fracción de vida máxima por debajo de la cual se muestra el aviso persistente.")]
    [Range(0f, 1f)][SerializeField] private float lowHealthThreshold = 0.35f;
    [Tooltip("Alpha del aviso persistente con vida baja. Muy tenue: acompaña, no distrae.")]
    [Range(0f, 1f)][SerializeField] private float lowHealthAlpha = 0.22f;
    [Tooltip("Segundos de un ciclo completo de respiración del aviso.")]
    [SerializeField] private float lowHealthPulsePeriod = 1.4f;
    [SerializeField] private Color lowHealthColor = new Color(0.6f, 0.02f, 0.02f, 1f);

    [Header("Depuración")]
    [SerializeField] private bool logMissingReferences = true;

    private Image image;

    /// <summary>Segundos reales restantes de flash.</summary>
    private float flashRemaining;

    /// <summary>
    /// Alpha objetivo del flash. Campo aparte para que dos golpes seguidos mantengan la
    /// intensidad del primero en lugar de reiniciarla y verse como un parpadeo seco.
    /// </summary>
    private float flashPeakAlpha;

    /// <summary>
    /// Vida en el frame anterior. Sin esta comparación, cada curación (regeneración, mejora de
    /// vida máxima) emitiría OnHealthChanged y provocaría un flash rojo con la vida SUBIENDO.
    /// </summary>
    private float previousHealth;

    private bool hasPreviousHealth;
    private bool missingTargetWarned;

    private void Awake()
    {
        if (vignetteImage == null)
        {
            vignetteImage = GetComponent<Image>();
        }

        image = vignetteImage;

        if (image != null)
        {
            // La viñeta es decorativa: no debe recibir el ratón ni bloquear los botones de la UI.
            image.raycastTarget = false;
            image.color = Color.clear;
        }
    }

    private void OnEnable()
    {
        ResolveTarget();

        if (targetHealth == null)
        {
            return;
        }

        targetHealth.OnHealthChanged += HandleHealthChanged;

        // Arranca sincronizado con la vida real para que el primer frame no dispare un flash.
        previousHealth = targetHealth.CurrentHealth;
        hasPreviousHealth = true;
        flashRemaining = 0f;
    }

    private void OnDisable()
    {
        if (targetHealth != null)
        {
            targetHealth.OnHealthChanged -= HandleHealthChanged;
        }

        flashRemaining = 0f;
    }

    private void Start()
    {
        // En Start todos los Awake han terminado: momento seguro para resolver el objetivo.
        ResolveTarget();
    }

    /// <summary>
    /// Busca el HealthComponent del jugador si el campo sigue vacío. Nunca se llama desde Update.
    /// </summary>
    private void ResolveTarget()
    {
        if (targetHealth != null) return;

        PlayerController player = FindAnyObjectByType<PlayerController>();

        if (player != null)
        {
            targetHealth = player.GetComponent<HealthComponent>();
        }

        if (targetHealth == null && logMissingReferences && !missingTargetWarned)
        {
            missingTargetWarned = true;
            Debug.LogWarning(
                "[DamageVignetteUI] No se encontró el HealthComponent del jugador: el flash de daño no se mostrará. " +
                "Asigna el campo 'Target Health' o revisa que el Player siga teniendo su HealthComponent.",
                this);
        }
    }

    /// <summary>
    /// Reacción a <see cref="HealthComponent.OnHealthChanged"/>. Solo parpadea cuando la vida BAJA:
    /// así una curación, una mejora de vida máxima o el relleno automático no ensucian la pantalla.
    /// </summary>
    private void HandleHealthChanged(float currentHealth, float maxHealth)
    {
        // El primer evento es la inicialización de la vida, no un golpe.
        if (!hasPreviousHealth)
        {
            hasPreviousHealth = true;
            previousHealth = currentHealth;
            return;
        }

        bool tookDamage = currentHealth < previousHealth - 0.001f;
        previousHealth = currentHealth;

        if (!tookDamage) return;

        flashPeakAlpha = Mathf.Max(flashPeakAlpha, flashAlpha);
        flashRemaining = flashDuration;
    }

    private void Update()
    {
        if (image == null) return;

        float targetAlpha = EvaluateLowHealthAlpha();

        if (flashRemaining > 0f)
        {
            // Tiempo REAL: ver la nota de cabecera de la clase.
            flashRemaining -= Time.unscaledDeltaTime;

            if (flashRemaining > 0f)
            {
                float fade = Mathf.Clamp01(flashRemaining / Mathf.Max(0.01f, flashDuration));

                // Suavizado hacia 0 (smoothstep): el flash se desvanece de golpe al principio
                // y larga al final, que es como se lee un golpe real y no una linterna.
                float eased = fade * fade * (3f - 2f * fade);
                targetAlpha = Mathf.Max(targetAlpha, flashPeakAlpha * eased);
            }
            else
            {
                // Flash agotado: limpiar el peak para que el siguiente golpe no lo herede.
                flashRemaining = 0f;
                flashPeakAlpha = 0f;
            }
        }

        // Interpola el color del aviso de vida baja al rojo del flash: un solo cambio de tinte
        // en vez de dos imágenes superpuestas.
        Color color = Color.Lerp(lowHealthColor, flashColor, Mathf.Clamp01(targetAlpha / Mathf.Max(0.01f, flashAlpha)));
        image.color = new Color(color.r, color.g, color.b, targetAlpha);
    }

    /// <summary>
    /// Alpha del aviso de vida baja: 0 con la vida llena, y una respiración suave por debajo del umbral.
    /// Bajo el umbral la rampa es continua, no un escalón, para que la transición sea imperceptible.
    /// </summary>
    private float EvaluateLowHealthAlpha()
    {
        if (targetHealth == null || targetHealth.IsDead) return 0f;

        float maxHealth = targetHealth.MaxHealth;

        if (maxHealth <= 0f) return 0f;

        float healthRatio = Mathf.Clamp01(targetHealth.CurrentHealth / maxHealth);

        if (healthRatio >= lowHealthThreshold) return 0f;

        // Rampa: 0 justo en el umbral, 1 con la vida casi a cero.
        float severity = 1f - (healthRatio / Mathf.Max(0.01f, lowHealthThreshold));

        float pulse = 1f;

        if (lowHealthPulsePeriod > 0.01f)
        {
            float phase = Time.unscaledTime * (2f * Mathf.PI / lowHealthPulsePeriod);
            pulse = (Mathf.Sin(phase) * 0.5f) + 0.5f; // 0..1
        }

        return lowHealthAlpha * severity * Mathf.Lerp(0.7f, 1f, pulse);
    }
}
