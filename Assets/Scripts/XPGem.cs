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

    [Header("Audio")]
    [SerializeField] private AudioClip gemPickupSFX; // Arrastrar GetXP.mp3 aqui

    private Transform playerTransform;
    private PlayerLevelSystem cachedPlayerLevel;
    private bool isMagnetized = false;

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
        }
    }

    public void SetXPValue(float amount)
    {
        xpAmount = amount;
    }

    // ======================================================================
    // RECICLAJE (OBJECT POOLING)
    // ======================================================================

    public void OnPoolSpawned()
    {
        // Estado limpio en cada reutilización desde el pool.
        isMagnetized = false;
        isDespawned = false;
    }

    public void OnPoolDespawned()
    {
        isMagnetized = false;
        isDespawned = true;
    }

    private void Update()
    {
        if (playerTransform == null || cachedPlayerLevel == null)
        {
            ResolvePlayer();
            return;
        }

        float distance = Vector3.Distance(transform.position, playerTransform.position);
        
        if (distance <= magnetRadius)
        {
            isMagnetized = true;
        }

        if (isMagnetized)
        {
            transform.position = Vector3.MoveTowards(transform.position, playerTransform.position + Vector3.up * 0.5f, moveSpeed * Time.deltaTime);

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