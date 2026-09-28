using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Hornea el NavMesh de la arena de pruebas y publica una señal de "listo".
///
/// Existe por un motivo concreto: <see cref="EnemySpawner"/> busca posiciones con
/// <see cref="NavMesh.SamplePosition"/> y <b>falla en silencio</b> si todavía no hay
/// triangulación. Sin este gate, un fallo de horneado se confunde con un bug de spawn.
/// El sandbox bloquea sus acciones de horda hasta que <see cref="IsReady"/> es true.
///
/// No toca ningún script de producción: es un componente exclusivo de Assets/_TEST.
/// </summary>
[DefaultExecutionOrder(-700)]
[DisallowMultipleComponent]
[AddComponentMenu("TEST/Test NavMesh Gate")]
public class TestNavMeshGate : MonoBehaviour
{
    [Header("Navegación")]
    [Tooltip("Superficie cuyo NavMesh se hornea al arrancar la arena de pruebas. Si se deja vacía se busca en la escena.")]
    [SerializeField] private NavMeshSurface surface;

    [Tooltip("Hornea en Awake. Desactívalo si el NavMesh ya viene horneado y guardado en la escena.")]
    [SerializeField] private bool bakeOnAwake = true;

    [Header("Depuración")]
    [SerializeField] private bool logBake = true;

    /// <summary>Instancia activa del sandbox (una sola por escena).</summary>
    public static TestNavMeshGate Instance { get; private set; }

    /// <summary>True cuando existe un NavMesh navegable y el spawn masivo puede buscar posiciones.</summary>
    public bool IsReady { get; private set; }

    /// <summary>Momento real en el que quedó listo. Sirve para medir el coste del horneado.</summary>
    public float ReadyAtRealtime { get; private set; }

    /// <summary>Se lanza una sola vez, en el frame en que el NavMesh queda disponible.</summary>
    public event System.Action OnNavMeshReady;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[TestNavMeshGate] Ya existe una instancia activa: se desactiva el duplicado.", this);
            enabled = false;
            return;
        }

        Instance = this;

        ResolveSurface();

        if (bakeOnAwake)
        {
            Bake();
        }

        VerifyReady();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>
    /// Hornea el NavMesh de forma síncrona. Es público para poder rehornear desde el
    /// panel de depuración tras mover paredes en Play Mode.
    /// </summary>
    [ContextMenu("Hornear NavMesh ahora")]
    public void Bake()
    {
        if (surface == null)
        {
            ResolveSurface();
        }

        if (surface == null)
        {
            Debug.LogError("[TestNavMeshGate] No hay NavMeshSurface en la escena: no se puede hornear.", this);
            return;
        }

        float start = Time.realtimeSinceStartup;
        surface.BuildNavMesh();
        float cost = Time.realtimeSinceStartup - start;

        if (logBake)
        {
            Debug.Log($"<color=cyan>[TestNavMeshGate] NavMesh horneado en {cost * 1000f:0.0} ms.</color>", this);
        }

        VerifyReady();
    }

    /// <summary>
    /// Comprueba la triangulación real en vez de confiar en el flag del horneado.
    /// Es la única comprobación fiable: SamplePosition devuelve false con un NavMesh vacío.
    /// </summary>
    private void VerifyReady()
    {
        bool ready = NavMesh.CalculateTriangulation().indices.Length > 0;

        if (ready == IsReady)
        {
            return;
        }

        IsReady = ready;
        ReadyAtRealtime = Time.realtimeSinceStartup;

        if (IsReady)
        {
            OnNavMeshReady?.Invoke();
            return;
        }

        Debug.LogError(
            "[TestNavMeshGate] El NavMesh sigue vacío tras hornear. Revisa que el suelo esté en la capa " +
            "'Terreno' y que el NavMeshSurface tenga esa capa en su Layer Mask.",
            this);
    }

    /// <summary>Busca la superficie en la escena si el Inspector la dejó vacía (patrón de auto-resolución del proyecto).</summary>
    private void ResolveSurface()
    {
        if (surface != null)
        {
            return;
        }

        surface = FindAnyObjectByType<NavMeshSurface>();

        if (surface == null)
        {
            Debug.LogError(
                "[TestNavMeshGate] No se encontró ningún NavMeshSurface en la escena. " +
                "Ejecuta Tools/40 Days of Light/Construir escena TEST para crear la arena.",
                this);
        }
    }
}
