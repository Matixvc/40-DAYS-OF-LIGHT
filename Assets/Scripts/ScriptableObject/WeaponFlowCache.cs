using UnityEngine;

/// <summary>
/// Caché de los ScriptableObjects que el flujo de armas necesita enlazar en la escena.
///
/// POR QUÉ EXISTE: <see cref="RunWeaponFlow"/> necesita dos listas de <see cref="UpgradeDataSO"/>
/// (armas iniciales y recompensas de jefe). Meterlas en el componente de la escena obligaría
/// a que la herramienta de editor los buscara por nombre de archivo, que se rompe en cuanto
/// alguien renombra un asset. Al guardarlas aquí, el enlace es por referencia directa.
///
/// Asset sin comportamiento: solo datos que la herramienta de editor escribe y el
/// componente de la escena lee. Nunca se escribe en runtime.
/// </summary>
[CreateAssetMenu(fileName = "SO_WeaponFlowCache", menuName = "Stats/Weapon Flow Cache")]
public class WeaponFlowCache : ScriptableObject
{
    [Tooltip("Armas iniciales ofrecidas al empezar la partida (Día 1).")]
    public UpgradeDataSO[] startingWeapons = new UpgradeDataSO[0];

    [Tooltip("Cartas de recompensa ofrecidas al derrotar a un jefe.")]
    public UpgradeDataSO[] bossRewards = new UpgradeDataSO[0];
}
