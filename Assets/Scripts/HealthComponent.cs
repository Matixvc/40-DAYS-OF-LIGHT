using UnityEngine;
using System;

public class HealthComponent : MonoBehaviour
{
    [Header("Fuente de Datos (Opcional)")]
    [SerializeField] private CharacterDataSO playerData;
    [SerializeField] private EnemyDataSO enemyData;

    [Header("Estado en Tiempo de Ejecución")]
    [SerializeField] private float maxHealth = 100f;
    private float currentHealth;
    private bool isDead = false;

    [Header("Recompensas (Drop de XP)")]
    [SerializeField] private GameObject xpGemPrefab;
    private float xpReward = 15f;

    private Animator animator;

    public event Action<float, float> OnHealthChanged;
    public event Action OnDeath;

    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public bool IsDead => isDead;

    private static readonly int HitHash = Animator.StringToHash("Hit");

    private void Awake()
    {
        animator = GetComponentInChildren<Animator>();

        if (playerData != null)
        {
            maxHealth = playerData.maxHealth;
        }
        else if (enemyData != null)
        {
            maxHealth = enemyData.maxHealth;
            xpReward = enemyData.xpReward;
        }

        currentHealth = maxHealth;
    }
    
    public void TakeDamage(float amount)
    {
        if (isDead) return;

        currentHealth -= amount;
        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);

        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        // --- DISPARAR ANIMACIÓN DE IMPACTO ---
        if (animator != null && currentHealth > 0f)
        {
            animator.SetTrigger(HitHash);
        }

        if (currentHealth <= 0f)
        {
            Die();
        }
    }

    public void Heal(float amount)
    {
        if (isDead) return;

        currentHealth += amount;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);

        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    private void Die()
    {
        if (isDead) return;
        isDead = true;

        if (xpGemPrefab != null)
        {
            Vector3 spawnPos = new Vector3(transform.position.x, 0.5f, transform.position.z);
            GameObject gemObj = Instantiate(xpGemPrefab, spawnPos, Quaternion.identity);
            
            XPGem gemScript = gemObj.GetComponent<XPGem>();
            if (gemScript != null)
            {
                gemScript.SetXPValue(xpReward);
            }
        }

        OnDeath?.Invoke();

        if (gameObject.CompareTag("Player"))
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.TriggerGameOver();
            }
        }
        else
        {
            Destroy(gameObject, 0.05f);
        }
    }
}