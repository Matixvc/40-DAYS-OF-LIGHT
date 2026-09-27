using UnityEngine;

/// <summary>
/// Sacudida de cámara ligera (juice) para impactos del jugador.
/// Vive en la Main Camera junto a <see cref="CameraFollow"/>: aplica un offset
/// ADITIVO en LateUpdate con orden de ejecución posterior (200), de modo que
/// CameraFollow recalcula la posición absoluta cada frame y no hay deriva.
/// Cero GC por frame: sin Instantiate, sin Find, sin corrutinas y sin asignaciones en LateUpdate.
/// </summary>
[DefaultExecutionOrder(200)]
[DisallowMultipleComponent]
public class CameraShake : MonoBehaviour
{
    public static CameraShake Instance { get; private set; }

    [Header("Depuración")]
    [Tooltip("Muestra en consola cada sacudida. Útil para comprobar el cableado del juice.")]
    [SerializeField] private bool logShakes = false;

    private float remaining;
    private float shakeDuration;
    private float shakeMagnitude;

    /// <summary>True mientras hay una sacudida en curso.</summary>
    public bool IsShaking => remaining > 0f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[CameraShake] Ya existe una instancia activa. Se desactiva el duplicado: mantén solo una, en la Main Camera.", this);
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
        // Al desactivarse no debe quedar ninguna sacudida "colgada" para el siguiente arranque.
        StopShake();
    }

    /// <summary>
    /// Activa una sacudida. Si hay una en curso, la nueva la refuerza (se queda el mayor tiempo/magnitud).
    /// </summary>
    public void Shake(float duration, float magnitude)
    {
        if (duration <= 0f || magnitude <= 0f) return;

        remaining = Mathf.Max(remaining, duration);
        shakeDuration = Mathf.Max(shakeDuration, duration);
        shakeMagnitude = Mathf.Max(shakeMagnitude, magnitude);

        if (logShakes)
        {
            Debug.Log($"[CameraShake] Sacudida: {duration:0.00}s | magnitud: {magnitude:0.00}u", this);
        }
    }

    /// <summary>Corta la sacudida de inmediato (game over, reinicio de partida, cambio de escena).</summary>
    public void StopShake()
    {
        remaining = 0f;
        shakeDuration = 0f;
        shakeMagnitude = 0f;
    }

    private void LateUpdate()
    {
        if (remaining <= 0f || shakeMagnitude <= 0f || shakeDuration <= 0f) return;

        // Decaimiento lineal: fuerza máxima al inicio y cero al terminar.
        float strength = shakeMagnitude * (remaining / shakeDuration);
        transform.position += Random.insideUnitSphere * strength;

        // La duración se descuenta aquí mismo: sin corrutina no hay basura ni estado colgado.
        remaining -= Time.deltaTime;

        if (remaining <= 0f)
        {
            StopShake();
        }
    }
}
