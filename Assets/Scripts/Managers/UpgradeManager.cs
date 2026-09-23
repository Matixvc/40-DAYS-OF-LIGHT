using UnityEngine;

public class UpgradeManager : MonoBehaviour
{
    [Header("Referencias a Datos")]
    [SerializeField] private CharacterDataSO playerData;
    [SerializeField] private WeaponDataSO mainWeaponData;
    [SerializeField] private HealthComponent playerHealth;

    public void ApplyUpgrade(UpgradeDataSO upgrade)
    {
        if (upgrade == null) return;

        switch (upgrade.upgradeType)
        {
            case UpgradeType.IncreaseDamage:
                if (mainWeaponData != null)
                {
                    mainWeaponData.damage += mainWeaponData.damage * upgrade.value;
                    Debug.Log($"<color=cyan>Nuevo Daño del Arma: {mainWeaponData.damage}</color>");
                }
                break;

            case UpgradeType.IncreaseRange:
                if (mainWeaponData != null)
                {
                    mainWeaponData.attackRange += mainWeaponData.attackRange * upgrade.value;
                    Debug.Log($"<color=cyan>Nuevo Rango del Arma: {mainWeaponData.attackRange}</color>");
                }
                break;

            case UpgradeType.DecreaseAttackInterval:
                if (mainWeaponData != null)
                {
                    mainWeaponData.attackInterval -= mainWeaponData.attackInterval * upgrade.value;
                    mainWeaponData.attackInterval = Mathf.Max(0.1f, mainWeaponData.attackInterval); // Límite de cadencia
                }
                break;

            case UpgradeType.IncreaseMoveSpeed:
                if (playerData != null)
                {
                    playerData.moveSpeed += playerData.moveSpeed * upgrade.value;
                    Debug.Log($"<color=cyan>Nueva Velocidad del Jugador: {playerData.moveSpeed}</color>");
                }
                break;

            case UpgradeType.HealPlayer:
                if (playerHealth != null)
                {
                    playerHealth.Heal(upgrade.value);
                }
                break;
        }
    }
}