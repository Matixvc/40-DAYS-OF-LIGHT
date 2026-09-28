using UnityEngine;

/// <summary>
/// Base común de los comportamientos de arma: resuelve referencias, cachea buffers y
/// centraliza el daño/cadencia efectivos para que ningún arquetipo los recalcule a su manera.
///
/// Decisión de diseño: la CADENCIA vive en cada comportamiento, no en el
/// <see cref="WeaponController"/>. Un pulso y un proyectil disparan "de golpe", pero una
/// órbita y un aura golpean de forma continua y se autolimitan con su propio intervalo por
/// objetivo. Si el controlador impusiera una cadencia única habría que añadir excepciones
/// por arquetipo justo ahí donde el patrón se rompe.
///
/// Los buffers son campos de INSTANCIA preasignados: cero asignaciones por frame.
/// </summary>
public abstract class WeaponBehaviourBase : MonoBehaviour, IWeaponBehaviour
{
    /// <summary>Nivel del arma: 1 es el de partida; cada nivel extra multiplica el daño.</summary>
    protected int Level { get; private set; } = 1;

    /// <summary>Controlador propietario (niveles, estadísticas de partida y capa de enemigos).</summary>
    protected WeaponController Owner { get; private set; }

    /// <summary>Configuración del arma. Es un asset inmutable: nunca se escribe en él.</summary>
    protected WeaponDataSO Data { get; private set; }

    /// <summary>Estadísticas de partida (puede ser null en una escena sin RunStats).</summary>
    protected RunStats Stats => Owner != null ? Owner.Stats : null;

    /// <summary>Máscara de capa de enemigos, tomada del controlador.</summary>
    protected LayerMask EnemyLayer => Owner != null ? Owner.EnemyLayer : default;

    // Buffers reutilizados en cada consulta de física (Zero-GC).
    protected readonly Collider[] colliderBuffer = new Collider[WeaponTargeting.DefaultBufferSize];
    protected readonly HealthComponent[] targetBuffer = new HealthComponent[WeaponTargeting.MaxDistinctTargets];
    protected readonly float[] distanceBuffer = new float[WeaponTargeting.MaxDistinctTargets];

    // Un único MaterialPropertyBlock por comportamiento: tintar con él no instancia el material.
    //
    // INICIALIZACIÓN PEREZOSA Y OBLIGATORIA: MaterialPropertyBlock deriva de UnityEngine.Object,
    // y crearlo en un inicializador de campo lo construye DENTRO del constructor del
    // MonoBehaviour. En ese punto el objeto nativo aún no existe y Unity lanza:
    //   "CreateImpl is not allowed to be called from a MonoBehaviour constructor"
    //
    // Además el bloque se comparte entre todos los renderers del arma, así que no puede
    // liberarse entre usos: se crea una sola vez, la primera vez que hace falta, y se
    // reutiliza de ahí en adelante (cero GC en el bucle de combate).
    private MaterialPropertyBlock propertyBlock;

    /// <summary>
    /// Acceso perezoso al bloque de propiedades. Garantiza que nunca sea null en
    /// <see cref="TintVisual"/> aunque el arma se equipe antes de que se haya tintado nada.
    /// </summary>
    protected MaterialPropertyBlock PropBlock => propertyBlock ?? (propertyBlock = new MaterialPropertyBlock());

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private readonly System.Collections.Generic.List<GameObject> visuals =
        new System.Collections.Generic.List<GameObject>(8);

    /// <summary>Daño final por golpe: el del SO o RunStats, escalado por el nivel del arma.</summary>
    protected float CurrentDamage => Data != null && Owner != null
        ? Data.GetDamage(Stats) * Owner.GetWeaponDamageMultiplier(Level)
        : 0f;

    /// <summary>Segundos entre ataques/ticks: del SO o RunStats, escalados por el nivel del arma.</summary>
    protected float CurrentCooldown => Data != null && Owner != null
        ? Data.GetCooldown(Stats) * Owner.GetWeaponCooldownMultiplier(Level)
        : 1f;

    /// <summary>Radio base del arquetipo, ya multiplicado por el área de las mejoras.</summary>
    protected float ScaleArea(float baseRadius)
    {
        RunStats stats = Stats;
        return stats != null ? baseRadius * stats.AreaSizeMultiplier : baseRadius;
    }

    public virtual void Initialize(WeaponController owner, WeaponDataSO data, int level)
    {
        Owner = owner;
        Data = data;
        Level = Mathf.Max(1, level);

        BuildVisuals();
    }

    public virtual void SetLevel(int level)
    {
        Level = Mathf.Max(1, level);
    }

    /// <summary>Latido por frame. Cada arquetipo decide su propia frecuencia.</summary>
    public abstract void Tick(float deltaTime);

    public virtual void Dispose()
    {
        // Si el objeto se está destruyendo, Unity ya se lleva por delante a los hijos: llamar
        // a Destroy aquí solo generaría avisos de "Destroy no permitido en OnDestroy".
        if (!isShuttingDown)
        {
            DestroyVisuals();
        }

        Owner = null;
        Data = null;
    }

    /// <summary>Marca de apagado ordenado, consultada por <see cref="Dispose"/>.</summary>
    private bool isShuttingDown;

    /// <summary>
    /// El controlador puede llamar a <see cref="Dispose"/> justo antes de destruir el host;
    /// esta bandera evita que la destrucción de hijos se solape con la del padre.
    /// </summary>
    private void OnDestroy()
    {
        isShuttingDown = true;
    }

    /// <summary>
    /// Crea la representación visual del arma en coordenadas locales del host. Si el SO no
    /// trae prefab se genera un primitivo (UNA vez al equipar, no por disparo) para que un
    /// arma sin visual no falle en silencio: se ve, aunque sea fea.
    /// </summary>
    protected GameObject CreateVisual(GameObject prefab, Vector3 localPosition, float uniformScale)
    {
        GameObject visual = prefab != null ? Instantiate(prefab, transform) : CreatePrimitiveFallback();

        if (visual == null)
        {
            return null;
        }

        visual.transform.SetParent(transform, false);
        visual.transform.localPosition = localPosition;
        visual.transform.localScale = Vector3.one * Mathf.Max(0.01f, uniformScale);

        TintVisual(visual, Data != null ? Data.flashColor : Color.white);
        visuals.Add(visual);

        return visual;
    }

    private GameObject CreatePrimitiveFallback()
    {
        GameObject primitive = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        primitive.name = "VisualProcedural";

        // Sin collider: el daño se resuelve con OverlapSphereNonAlloc, no con triggers. Dejar
        // el collider provocaría choques físicos del arma contra el propio jugador.
        Collider primitiveCollider = primitive.GetComponent<Collider>();

        if (primitiveCollider != null)
        {
            Destroy(primitiveCollider);
        }

        return primitive;
    }

    /// <summary>
    /// Tiñe el visual sin instanciar el material (SetPropertyBlock). Se escriben los tres
    /// nombres habituales de color para no depender del shader que acabe usando el prefab.
    /// </summary>
    protected void TintVisual(GameObject visual, Color color)
    {
        if (visual == null)
        {
            return;
        }

        Color opaque = new Color(color.r, color.g, color.b, 1f);

        Renderer[] renderers = visual.GetComponentsInChildren<Renderer>(true);

        if (renderers == null || renderers.Length == 0)
        {
            return;
        }

        // Se resuelve UNA vez por llamada, ya creado: la inicialización perezosa ocurre aquí,
        // nunca en el constructor, que es justo lo que Unity prohíbe.
        MaterialPropertyBlock block = PropBlock;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];

            if (renderer == null)
            {
                continue;
            }

            // GetPropertyBlock sobreescribe el bloque con el del renderer, así que se limpia
            // antes de escribir: de lo contrario, propiedades de un mesh se filtrarían a otro.
            block.Clear();

            renderer.GetPropertyBlock(block);
            block.SetColor(BaseColorId, opaque);
            block.SetColor(ColorId, opaque);
            block.SetColor(EmissionColorId, opaque * 0.6f);
            renderer.SetPropertyBlock(block);
        }
    }

    /// <summary>Destruye los visuales generados.</summary>
    protected void DestroyVisuals()
    {
        for (int i = visuals.Count - 1; i >= 0; i--)
        {
            if (visuals[i] != null)
            {
                Destroy(visuals[i]);
            }
        }

        visuals.Clear();
    }

    /// <summary>
    /// Oculta o muestra los visuales del arma. La usa el interruptor "armas ON/OFF" del
    /// sandbox: apagar el arma no debe dejar orbes flotando alrededor del jugador.
    ///
    /// <see cref="VisualsEnabled"/> queda expuesto para que los arquetipos con destellos
    /// temporales (Pulse) no vuelvan a encender su visual mientras el interruptor está apagado.
    /// </summary>
    public void SetVisualsActive(bool value)
    {
        VisualsEnabled = value;

        for (int i = 0; i < visuals.Count; i++)
        {
            if (visuals[i] != null)
            {
                visuals[i].SetActive(value);
            }
        }
    }

    /// <summary>Estado del interruptor de visuales del arma (true por defecto).</summary>
    public bool VisualsEnabled { get; private set; } = true;

    /// <summary>Crea el host del arma (hijo del Player) y lo deja listo para construir sus visuales.</summary>
    protected abstract void BuildVisuals();
}
