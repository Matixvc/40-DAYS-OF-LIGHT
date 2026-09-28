using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

/// <summary>
/// Ajustes del generador de la escena de pruebas. Se guarda como asset para que las
/// referencias que el usuario ajuste a mano (prefab del Player, prefabs de la arena,
/// perfil de postprocesado) sobrevivan a las siguientes ejecuciones del builder.
///
/// Todo es opcional: si un campo queda vacío, el builder lo autodescubre con AssetDatabase.
/// Existe porque la resolución del Player es una cascada de tres vías y la vía manual
/// (un prefab real) siempre es la más fiable cuando esté disponible.
/// </summary>
[CreateAssetMenu(fileName = "TestBuildSettings", menuName = "40 Days of Light/TEST Build Settings")]
public class TestBuildSettings : ScriptableObject
{
    public const string AssetPath = "Assets/_TEST/Editor/TestBuildSettings.asset";

    [Header("Jugador")]
    [Tooltip("Prefab del Player. Si está vacío el builder lo busca por nombre y, si no lo " +
             "encuentra, copia la jerarquía del Player desde Prototype.unity.")]
    public GameObject playerPrefab;

    [Header("Prefabs de la arena")]
    public GameObject enemyPrefab;
    public GameObject xpPrefab;
    public GameObject damageNumberPrefab;

    [Header("Postprocesado (opcional)")]
    [Tooltip("Perfil URP. Si se deja vacío, PostProcessingBootstrap genera uno temporal.")]
    public VolumeProfile postProcessProfile;

    [Header("Input (opcional)")]
    [Tooltip("Asset de acciones. Si se deja vacío, PlayerInputReader crea las suyas por código.")]
    public InputActionAsset inputActions;

    [Header("Mejoras de prueba")]
    [Tooltip("Se inyectan en LevelUpUI y en el panel del sandbox. Si está vacío se buscan " +
             "todas las UpgradeDataSO del proyecto.")]
    public UpgradeDataSO[] testUpgrades;

    [Header("Armas de prueba (Fase 3)")]
    [Tooltip("Catálogo que recorre F11 en el sandbox. Si está vacío el builder crea cuatro " +
             "WeaponDataSO de ejemplo (Pulse/Orbit/Projectile/Aura) en Assets/_TEST/Weapons.")]
    public WeaponDataSO[] testWeapons;

    [Tooltip("Prefab con WeaponProjectile que usan las armas de arquetipo Projectile. Si está " +
             "vacío el builder crea uno procedural en Assets/_TEST/Weapons (con collider de trigger).")]
    public GameObject weaponProjectilePrefab;

    /// <summary>Carga los ajustes, creándolos la primera vez.</summary>
    public static TestBuildSettings GetOrCreate()
    {
        TestBuildSettings settings = AssetDatabase.LoadAssetAtPath<TestBuildSettings>(AssetPath);

        if (settings != null)
        {
            return settings;
        }

        settings = CreateInstance<TestBuildSettings>();

        if (!AssetDatabase.IsValidFolder("Assets/_TEST/Editor"))
        {
            AssetDatabase.CreateFolder("Assets/_TEST", "Editor");
        }

        AssetDatabase.CreateAsset(settings, AssetPath);
        AssetDatabase.SaveAssets();

        Debug.Log($"[TestSceneBuilder] Ajustes creados en {AssetPath}.", settings);
        return settings;
    }
}
