using UnityEngine;

public class XPGem : MonoBehaviour
{
    [Header("Configuración de XP")]
    [SerializeField] private float xpAmount = 15f;
    [SerializeField] private float moveSpeed = 12f;

    [Header("Audio")]
    [SerializeField] private AudioClip gemPickupSFX; // Arrastrar GetXP.mp3 aqui

    private Transform playerTransform;
    private bool isMagnetized = false;

    public void SetXPValue(float amount)
    {
        xpAmount = amount;
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
        
        if (distance <= 3.5f)
        {
            isMagnetized = true;
        }

        if (isMagnetized)
        {
            transform.position = Vector3.MoveTowards(transform.position, playerTransform.position + Vector3.up * 0.5f, moveSpeed * Time.deltaTime);

            if (distance <= 0.8f)
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
        Destroy(gameObject);
    } 
}