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
    private bool isMagnetized = false;

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
    }

    public void OnPoolDespawned()
    {
        isMagnetized = false;
    }

    private void Update()
    {
        if (playerTransform == null)
        {
            PlayerLevelSystem player = FindAnyObjectByType<PlayerLevelSystem>();
            if (player != null)
            {
                playerTransform = player.transform;
            }
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
                Collect(playerTransform.GetComponent<PlayerLevelSystem>());
            }
        }
    }

    private void Collect(PlayerLevelSystem playerLevel)
    {
        if (playerLevel != null)
        {
            playerLevel.AddXP(xpAmount);

            if (AudioManager.Instance != null && gemPickupSFX != null)
            {
                AudioManager.Instance.PlaySFX(gemPickupSFX, 0.8f, 0.12f);
            }
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
}