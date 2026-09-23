using UnityEngine;

[CreateAssetMenu(fileName = "NewEnemyData", menuName = "Stats/Enemy Data")]
public class EnemyDataSO : ScriptableObject
{
    [Header("Estadísticas Base")]
    public float maxHealth = 30f;
    public float moveSpeed = 3.5f;
    public float damage = 10f;
    public float xpReward = 15f;

    [Header("Ataque y Combate")]
    public float attackRange = 1.8f;      // Distancia a la que decide iniciar el ataque
    public float attackCooldown = 1.5f;   // Tiempo de espera entre ataques
    public float attackHitRadius = 1.2f;  // Radio del impacto en el momento exacto del golpe

    // --- BOTÓN MANUAL EN EL INSPECTOR ---
    [ContextMenu("Resetear a Valores Base")]
    public void ResetStats()
    {
        maxHealth = 30f;
        moveSpeed = 3.5f;
        damage = 10f;
        xpReward = 15f;
        attackRange = 1.8f;
        attackCooldown = 1.5f;
        attackHitRadius = 1.2f;
        Debug.Log($"<color=cyan>[SO] {name} reseteado a sus estadísticas base.</color>");
    }
}