using TMPro;
using UnityEngine;

/// <summary>
/// Número de daño flotante en World Space (TextMeshPro).
/// Se recicla con <see cref="ObjectPoolManager"/>: cero GC durante el gameplay
/// (sin Instantiate/Destroy y sin crear strings: el texto se formatea con TMP_Text.SetText).
/// Uso: el pool lo activa (OnPoolSpawned resetea el estado) y luego el emisor llama a Show(cantidad).
/// La animación (ascenso + desvanecido 0.6s) corre en Update con tiempo escalado:
/// se congela en pausa/nivel, igual que el combate.
/// La devolución al pool está protegida contra devoluciones duplicadas con la bandera isDespawned.
/// </summary>
[DisallowMultipleComponent]
public class DamageNumber : MonoBehaviour, IPooledObject
{
    [Header("Referencias")]
    [Tooltip("TextMeshPro en World Space. Si se deja vacío se resuelve en los hijos.")]
    [SerializeField] private TextMeshPro textMesh;

    [Header("Animación")]
    [Tooltip("Duración del ascenso + desvanecido, en segundos.")]
    [SerializeField] private float duration = 0.6f;
    [Tooltip("Altura total que sube el número durante la animación.")]
    [SerializeField] private float riseHeight = 1.2f;
    [Tooltip("Escala inicial (pop) que se relaja a 1 en los primeros 0.15s.")]
    [SerializeField] private float popScale = 1.3f;

    [Header("Tipografía")]
    [Tooltip("Espaciado entre caracteres del TextMeshPro. Se aplica siempre (Awake, Show y Editor): " +
             "un valor ~0 mantiene juntas las cifras, mientras que un valor heredado del asset TMP " +
             "(a veces muy abierto) hace que números como '22' se lean como dos '2' separados.")]
    [SerializeField, Range(-2f, 6f)] private float characterSpacing = 0f;

    private float elapsed;
    private bool playing;

    /// <summary>
    /// True cuando esta instancia ya se devolvió al pool (o ya se desactivó).
    /// Evita la DOBLE DEVOLUCIÓN: si <see cref="ObjectPoolManager.ReleaseAll"/> (fin de partida,
    /// cambio de escena o destrucción del manager) devuelve el número a mitad de animación,
    /// su propio Update aún puede llamar a Finish() en ese mismo frame y el pool avisaría
    /// de una devolución duplicada por consola.
    /// </summary>
    private bool isDespawned;

    /// <summary>
    /// True desde que la aplicación empezó a cerrarse (OnApplicationQuit). A partir de ese momento
    /// NO se devuelve nada al pool: el manager se está destruyendo y esa devolución solo producía
    /// la traza 'ObjectPoolManager.OnDestroy() -> ReleaseAll() -> Despawn()' en consola.
    /// Es un campo de instancia (no static) para que no se quede "pegado" entre sesiones de Play.
    /// </summary>
    private bool isApplicationQuitting;

    private float riseSpeed;
    private Vector3 baseScale;
    private Transform mainCameraTransform;

    private const float PopDuration = 0.15f;

    private void Awake()
    {
        ResolveTextMesh();
        ApplyCharacterSpacing();
        baseScale = transform.localScale;
        riseSpeed = riseHeight / Mathf.Max(0.01f, duration);
        CacheMainCamera();
    }

    /// <summary>
    /// Impone al TMP el espaciado de caracteres configurado en el Inspector. Sin esta aplicación,
    /// el texto hereda el <c>characterSpacing</c> serializado del prefab/asset TMP, que puede ser
    /// tan amplio que cifras como "22" se perciben como dos números "2" separados.
    /// </summary>
    private void ApplyCharacterSpacing()
    {
        if (textMesh == null) return;

        textMesh.characterSpacing = characterSpacing;
    }

    private void OnValidate()
    {
        // Refleja el ajuste de espaciado en el Editor sin entrar en Play Mode.
        if (textMesh == null) ResolveTextMesh();

        ApplyCharacterSpacing();
    }

    /// <summary>Muestra esta instancia con la cantidad indicada y reinicia la animación.</summary>
    public void Show(float amount)
    {
        ResolveTextMesh();

        if (textMesh != null)
        {
            // Cifra entera y limpia: "22", nunca "22,0" ni "21,99". El redondeo es EXPLÍCITO
            // (Mathf.RoundToInt) y SetText(string, int) escribe en el buffer interno de TMP sin
            // crear strings (cero GC), igual que el patrón "{0:0}" anterior (a diferencia de
            // ToString(), que sí generaría basura por impacto).
            textMesh.SetText("{0}", Mathf.RoundToInt(amount));
            textMesh.alpha = 1f;
            textMesh.characterSpacing = characterSpacing;
        }

        transform.localScale = baseScale * Mathf.Max(0.01f, popScale);
        elapsed = 0f;
        playing = true;

        // Cubre también el caso sin pool (Instantiate directo), donde OnPoolSpawned() nunca llega:
        // en cuanto se vuelve a mostrar, la instancia vuelve a ser devolvible.
        isDespawned = false;

        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }
    }

    public void OnPoolSpawned()
    {
        // El pool activa el objeto antes de que el emisor llame a Show():
        // dejar estado limpio y en pausa hasta que llegue el valor.
        elapsed = 0f;
        playing = false;
        isDespawned = false; // Instancia reciclada: vuelve a estar lista para devolverse.
        transform.localScale = baseScale;

        if (textMesh != null)
        {
            textMesh.alpha = 1f;
        }
    }

    public void OnPoolDespawned()
    {
        playing = false;

        // El pool ya se hizo cargo de la instancia (p. ej. ReleaseAll): cualquier devolución
        // posterior desde Finish() debe ignorarse para no duplicar la entrada en 'available'.
        isDespawned = true;
    }

    /// <summary>
    /// Un objeto inactivo nunca es "devolvible": o lo recogió el pool (OnPoolDespawned) o la escena
    /// se está descargando. Por eso aquí solo se corta la animación y se marca la instancia:
    /// NO se llama a Finish() ni a Despawn() desde OnDisable/OnDestroy.
    /// </summary>
    private void OnDisable()
    {
        playing = false;
        isDespawned = true;
    }

    private void OnDestroy()
    {
        // El GameObject se está destruyendo (descarga de escena o cierre de la app): el pool no
        // debe recibirlo. Cualquier devolución posterior queda bloqueada por isDespawned.
        playing = false;
        isDespawned = true;
    }

    private void OnApplicationQuit()
    {
        // Cierre del juego: nada de tocar el pool a partir de aquí.
        isApplicationQuitting = true;
        playing = false;
        isDespawned = true;
    }

    private void Update()
    {
        if (isDespawned || !playing) return;

        if (mainCameraTransform == null)
        {
            CacheMainCamera();
        }

        // Billboard: siempre mirando a la cámara (sin asignaciones).
        if (mainCameraTransform != null)
        {
            transform.rotation = mainCameraTransform.rotation;
        }

        float deltaTime = Time.deltaTime;
        elapsed += deltaTime;

        transform.position += Vector3.up * (riseSpeed * deltaTime);

        float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, duration));

        if (textMesh != null)
        {
            textMesh.alpha = 1f - t;
        }

        float popT = Mathf.Clamp01(elapsed / PopDuration);
        transform.localScale = Vector3.Lerp(baseScale * Mathf.Max(0.01f, popScale), baseScale, popT);

        if (elapsed >= duration)
        {
            Finish();
        }
    }

    private void Finish()
    {
        playing = false;

        // Antes de invocar la devolución al pool se comprueba que la instancia siga siendo
        // "devolvible". Casos cubiertos por este único guard:
        //  - isDespawned: ReleaseAll() (fin de partida / cambio de escena / OnDestroy del manager)
        //    ya la devolvió en este mismo frame; el pool avisaría de una doble devolución.
        //  - isApplicationQuitting: la app se está cerrando, el pool ya no existe útilmente.
        //  - !activeSelf / !activeInHierarchy: la instancia ya venía desactivada (el propio pool,
        //    o su padre PooledObjects al descargar la escena).
        if (isDespawned || isApplicationQuitting || !gameObject.activeSelf || !gameObject.activeInHierarchy)
        {
            return;
        }

        ObjectPoolManager pool = ObjectPoolManager.Instance;

        if (pool != null && pool.IsPooled(gameObject))
        {
            // La marca se pone justo ANTES de invocar la devolución: ninguna reentrada
            // (Update, otro Finish o ReleaseAll) puede volver a entrar en el pool.
            isDespawned = true;
            pool.Despawn(gameObject);
            return;
        }

        // Sin pool (Instantiate directo): basta con desactivar la instancia.
        isDespawned = true;
        gameObject.SetActive(false);
    }

    private void ResolveTextMesh()
    {
        if (textMesh != null) return;

        textMesh = GetComponentInChildren<TextMeshPro>(true);

        if (textMesh == null)
        {
            textMesh = GetComponent<TextMeshPro>();
        }
    }

    private void CacheMainCamera()
    {
        Camera mainCamera = Camera.main;

        if (mainCamera != null)
        {
            mainCameraTransform = mainCamera.transform;
        }
    }
}
