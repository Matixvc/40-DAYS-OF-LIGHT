using System;
using UnityEngine;

/// <summary>
/// Micro-pausa de impacto ("hit stop"): el tiempo se ralentiza una fracción de segundo
/// en el frame en que un golpe conecta, para que el impacto se LEA en lugar de solo verse.
///
/// REGLA DE ARQUITECTURA: este componente NO escribe <see cref="Time.timeScale"/>. Solo posee
/// el estado de la pausa (tiempo restante, escala objetivo) y <see cref="GameStateController"/>
/// sigue siendo el único punto del proyecto que escribe la escala de tiempo. Así el hit-stop
/// es un factor más que se multiplica, no una excepción al invariante.
///
/// POR QUÉ <see cref="Time.unscaledDeltaTime"/> Y NO <see cref="Time.deltaTime"/>:
/// durante la micro-pausa la escala baja a ~0.05, así que un temporizador escalado avanzaría
/// 20x más lento en tiempo real: los "0.04 s" nominales durarían 0.8 s reales y el juego se
/// sentiría LAGGY en lugar de impactante. El temporizador va en tiempo real, siempre.
///
/// POR QUÉ <c>Mathf.Max</c> Y NO SUMAR: un pulso de área puede golpear a 20 enemigos en el
/// mismo frame. Sumar 20 paradas de 0.04 s congelaría el juego durante casi un segundo.
/// Se aplica la más larga, nunca la suma.
///
/// Además hay un anti-spam: un aura que aplica daño por ticks no puede pedir una pausa en
/// cada frame, o la partida se quedaría congelada de forma permanente.
/// </summary>
[DefaultExecutionOrder(-950)]
[DisallowMultipleComponent]
[AddComponentMenu("Game Feel/Hit Stop Manager")]
public class HitStopManager : MonoBehaviour
{
    public static HitStopManager Instance { get; private set; }

    [Header("Duración")]
    [Tooltip("Duración por defecto de la micro-pausa, en SEGUNDOS REALES. 0.04 es el valor estándar del género.")]
    [SerializeField, Min(0f)] private float defaultDuration = 0.04f;

    [Tooltip("Techo absoluto: una duración mayor se recorta aquí. Evita que un jefe deje el juego congelado un segundo entero.")]
    [SerializeField, Min(0.01f)] private float maxDuration = 0.15f;

    [Header("Intensidad")]
    [Range(0.01f, 1f)]
    [Tooltip("Escala de tiempo durante la micro-pausa. 0.05 = el juego avanza al 5%: un latigazo perceptible, nunca un tirón.")]
    [SerializeField] private float hitStopTimeScale = 0.05f;

    [Header("Anti-spam")]
    [Tooltip("Segundos reales mínimos entre dos micro-pausas. Sin esto, un arma de daño por ticks (Aura) " +
             "pediría una pausa por frame y la partida quedaría permanentemente ralentizada.")]
    [SerializeField, Min(0f)] private float minIntervalBetweenStops = 0.06f;

    [Header("Depuración")]
    [SerializeField] private bool logHitStops = false;

    /// <summary>Segundos reales que quedan de pausa (0 = inactiva).</summary>
    private float remaining;

    /// <summary>Reloj real (no escalado) hasta el que no se admite otra petición.</summary>
    private float nextAllowedTime;

    /// <summary>Escala que <see cref="GameStateController"/> debe aplicar mientras dura la pausa.</summary>
    public float ScaleWhileStopped => hitStopTimeScale;

    public float Remaining => remaining;
    public bool IsActive => remaining > 0f;

    /// <summary>Se lanza una vez, cuando la micro-pausa termina: quien escuche restaura la escala.</summary>
    public event Action OnHitStopEnded;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning(
                "[HitStopManager] Ya existe una instancia activa. Se desactiva el duplicado: mantén solo una, " +
                "junto al GameStateController.",
                this);
            enabled = false;
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void OnDisable()
    {
        // Nunca dejar una pausa colgada: si el objeto se desactiva (reinicio de partida, cambio de
        // escena) el tiempo debe volver a la normalidad aunque nadie esté escuchando ya.
        if (remaining > 0f)
        {
            Clear();
        }
    }

    /// <summary>
    /// Pide una micro-pausa de impacto.
    /// </summary>
    /// <param name="duration">Segundos reales. Si es 0 o menor se usa <c>defaultDuration</c>.</param>
    /// <returns>
    /// Segundos reales que se aplicarán realmente (0 si la petición se rechazó por el anti-spam).
    /// El llamante solo necesita comprobar que sea &gt; 0.
    /// </returns>
    public float Request(float duration = 0f)
    {
        float requested = duration > 0f ? duration : defaultDuration;
        requested = Mathf.Clamp(requested, 0f, maxDuration);

        if (requested <= 0f)
        {
            return 0f;
        }

        // Anti-spam con reloj NO escalado: la pausa no puede(chain)se a sí misma.
        float now = Time.unscaledTime;

        if (now < nextAllowedTime)
        {
            return 0f;
        }

        nextAllowedTime = now + Mathf.Max(0f, minIntervalBetweenStops);

        // Mathf.Max, NUNCA suma: 20 impactos en el mismo frame dan 0.04s, no 0.8s.
        remaining = Mathf.Max(remaining, requested);

        if (logHitStops)
        {
            Debug.Log(
                $"[HitStop] Micro-pausa de {remaining:0.000}s a escala {hitStopTimeScale:0.##}.",
                this);
        }

        return remaining;
    }

    private void Update()
    {
        if (remaining <= 0f) return;

        // Tiempo REAL: ver la nota de cabecera de la clase.
        remaining -= Time.unscaledDeltaTime;

        if (remaining > 0f) return;

        Clear();
    }

    /// <summary>Termina la pausa de inmediato y avisa a los suscriptores.</summary>
    private void Clear()
    {
        remaining = 0f;
        OnHitStopEnded?.Invoke();
    }
}