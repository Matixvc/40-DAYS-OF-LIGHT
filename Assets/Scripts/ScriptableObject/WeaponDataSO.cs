using UnityEngine;

[CreateAssetMenu(fileName = "NewWeaponData", menuName = "Stats/Weapon Data")]
public class WeaponDataSO : ScriptableObject
{
    [Header("Información del Arma")]
    public string weaponName = "Destello de Luz";

    [Header("Estadísticas del Arma")]
    public float damage = 35f;
    public float attackRange = 4.5f;
    public float attackInterval = 0.8f;

    [Header("Visuales")]
    public Color flashColor = new Color(1f, 0.9f, 0.3f, 0.5f);

    // --- BOTÓN MANUAL EN EL INSPECTOR ---
    [ContextMenu("Resetear a Valores Base")]
    public void ResetStats()
    {
        damage = 35f;
        attackRange = 4.5f;
        attackInterval = 0.8f;
        Debug.Log($"<color=cyan>[SO] Arma {weaponName} reseteada a sus valores base.</color>");
    }
}