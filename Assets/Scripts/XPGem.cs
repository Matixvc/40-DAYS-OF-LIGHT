using UnityEngine;

public class XPGem : MonoBehaviour, IPooledObject
{
    [Header("Configuración de XP")]
    [SerializeField] private float xpAmount = 15f;
    [SerializeField] private float moveSpeed = 12f;
    [Tooltip("Radio en el que la gema empieza a ser atraída por el jugador.")]
    [SerializeField] private float magnetRadius = 3.5f;
    [Tooltip("Distancia a la que se recoge la gema.")]
    [SerializeField] private float collectRadius = 0.8f;
    [Tooltip("Altura sobre el jugador hacia la que vuela la gema (centro del pecho).")]
    [SerializeField] private float hoverHeight = 0.5f;

    [Header("Atracción (pulido de game feel)")]
    [Tooltip("Rampa de velocidad al entrar en el imán. 0 = velocidad constante (comportamiento antiguo). " +
             "Con valor > 0 la gema arranca suave y acelera: se lee como atracción magnética, no como teletransporte.")]
    [SerializeField, Min(0f)] private float attractAcceleration = 28f;
    [Tooltip("Velocidad mínima al empezar la atracción. Baja de golpe y luego acelera.")]
    [SerializeField, Min(0f)] private float attractStartSpeed = 2.5f;
    [Tooltip("Giro en Y por segundo. Da vida a la gema mientras vuela hacia el jugador.")]
    [SerializeField, Min(0f)] private float spinSpeed = 180f;

    [Header("Radio conectado a las mejoras")]
    [Tooltip("Si está activo, el radio de imán sale de RunStats.PickupRadius (por defecto el del " +
             "CharacterDataSO). Es lo que hace que la mejora 'Radio de recogida' (StatType.IncreasePickupRadius) " +
             "afecte de verdad a las gemas: sin esto esa estadística no tenía ningún consumidor.")]
    [SerializeField] private bool usePickupRadiusStat = true;

    [Header("Audio")]
    [SerializeField] private AudioClip gemPickupSFX; // Arrastrar GetXP.mp3 aqui

    private Transform playerTransform;
    private PlayerLevelSystem cachedPlayerLevel;
    private RunStats cachedPlayerStats;
    private bool isMagnetized = false;

    /// <summary>
    /// Velocidad de atracción actual. Se interpola hacia <see cref="moveSpeed"/> con
    /// <see cref="attractAcceleration"/>: la gema no vuela a velocidad constante desde el primer frame.
    /// </summary>
    private float currentSpeed;

    /// <summary>
    /// True cuando esta gema ya se recogió (o se recicló): evita sumar XP o repetir el SFX dos veces
    /// si el Update llegara a ejecutarse otra vez antes de que el pool la desactive.
    /// </summary>
    private bool isDespawned;

    private void Awake()
    {
        ResolvePlayer();
    }

    private void Start()
    {
        ResolvePlayer();
    }

    /// <summary>Cachea el Player una sola vez: nunca se busca dentro de Update.</summary>
    private void ResolvePlayer()
    {
        if (cachedPlayerLevel != null && playerTransform != null) return;

        PlayerLevelSystem player = cachedPlayerLevel != null ? cachedPlayerLevel : FindAnyObjectByType<PlayerLevelSystem>();

        if (player != null)
        {
            cachedPlayerLevel = player;
            playerTransform = player.transform;

            // RunStats vive en el MISMO GameObject que el Player: se resuelve una vez aquí y
            // nunca dentro de Update. Puede ser null si el Player aún no lo tiene.
            cachedPlayerStats = player.GetComponent<RunStats>() ?? RunStats.Active;
        }
    }

    public void SetXPValue(float amount)
    {
        xpAmount = amount;
    }

    /// <summary>
    /// Radio de imán efectivo. Sin RunStats, o con la opción desactivada, se usa el valor
    /// serializado de este componente (comportamiento heredado intacto).
    /// </summary>
    private float EffectiveMagnetRadius =>
        usePickupRadiusStat && cachedPlayerStats != null
            ? Mathf.Max(0.1f, cachedPlayerStats.PickupRadius)
            : magnetRadius;

    // ======================================================================
    // RECICLAJE (OBJECT POOLING)
    // ======================================================================

    public void OnPoolSpawned()
    {
        // Estado limpio en cada reutilización desde el pool.
        isMagnetized = false;
        isDespawned = false;

        // La rampa de velocidad arranca de cero en cada reutilización: si conservara la
        // velocidad final del vuelo anterior, la gema siguiente saldría disparada.
        currentSpeed = 0f;
    }

    public void OnPoolDespawned()
    {
        isMagnetized = false;
        isDespawned = true;
        currentSpeed = 0f;
    }

    private void Update()
    {
        if (playerTransform == null || cachedPlayerLevel == null)
        {
            ResolvePlayer();
            return;
        }

        // UN SOLO punto de verdad para la distancia: se mide contra el MISMO punto al que
        // vuela la gema. Antes se medía contra los pies del jugador pero se movía hacia el
        // pecho (+0.5), así que la recogida ocurría con un desfase constante.
        Vector3 target = playerTransform.position + (Vector3.up * hoverHeight);
        float distance = Vector3.Distance(transform.position, target);

        if (!isMagnetized && distance <= EffectiveMagnetRadius)
        {
            isMagnetized = true;

            // Arranca cerca de cero y sube hasta moveSpeed: la aceleración es lo que convierte
            // un "MoveTowards" rígido en una atracción que se siente magnética.
            currentSpeed = Mathf.Min(attractStartSpeed, moveSpeed);
        }

        if (isMagnetized)
        {
            // Rampa de velocidad. Con attractAcceleration = 0 se queda en moveSpeed constante
            // y el comportamiento es idéntico al anterior.
            currentSpeed = Mathf.MoveTowards(
                currentSpeed,
                moveSpeed,
                attractAcceleration * Time.deltaTime);

            transform.position = Vector3.MoveTowards(transform.position, target, currentSpeed * Time.deltaTime);

            if (spinSpeed > 0f)
            {
                transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);
            }

            if (distance <= collectRadius)
            {
                Collect(cachedPlayerLevel);
            }
        }
    }

    private void Collect(PlayerLevelSystem playerLevel)
    {
        // Una gema solo se recoge una vez: sin esta guarda, dos recogidas en el mismo frame
        // sumarían la XP (y el sonido) por duplicado.
        if (isDespawned) return;

        isDespawned = true;

        if (playerLevel != null)
        {
            playerLevel.AddXP(xpAmount);
            PlayPickupSfx();
        }
        // Reciclar la gema en lugar de destruirla: cero GC durante el gameplay.
        ObjectPoolManager pool = ObjectPoolManager.Instance;

        if (pool != null && pool.IsPooled(gameObject))
        {
            pool.Despawn(gameObject);
            return;
        }

        Destroy(gameObject);
    }

    /// <summary>
    /// SFX de recogida (SFX_XPOp): usa el clip propio del prefab y, si no hay, el del catálogo del
    /// AudioManager. El portero anti-saturación del manager evita el muro de sonido al recoger
    /// muchas gemas de golpe.
    /// </summary>
    private void PlayPickupSfx()
    {
        AudioManager audio = AudioManager.Instance;

        if (audio == null) return;

        audio.PlayXpPickupSFX(gemPickupSFX);
    } 
}