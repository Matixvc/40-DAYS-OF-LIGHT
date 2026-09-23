using UnityEngine;

public enum UpgradeType
{
    IncreaseDamage,
    IncreaseRange,
    DecreaseAttackInterval,
    IncreaseMoveSpeed,
    HealPlayer
}

[CreateAssetMenu(fileName = "NewUpgrade", menuName = "Stats/Upgrade Data")]
public class UpgradeDataSO : ScriptableObject
{
    [Header("Información de la Mejora")]
    public string upgradeName = "Más Daño";
    [TextArea] public string description = "+20% de daño al Destello de Luz";
    public Sprite icon;

    [Header("Efecto")]
    public UpgradeType upgradeType;
    public float value = 0.2f; // <-- Nombre exacto 'value' para corregir los errores
}