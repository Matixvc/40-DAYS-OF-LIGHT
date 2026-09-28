using UnityEditor;
using UnityEngine;

public static partial class TestSceneBuilder
{
    // Los assets de la Fase 3 viven en _TEST a propósito: son material de pruebas, no
    // contenido de producción. Si un día se promueven, se mueven los archivos y ya está
    // (las referencias las conserva Unity por GUID).
    private const string TestWeaponsFolder = "Assets/_TEST/Weapons";
    private const string TestModifiersFolder = "Assets/_TEST/Modifiers";
    private const string TestUpgradesFolder = "Assets/_TEST/Upgrades";
    private const string TestVisualsFolder = "Assets/_TEST/Weapons/Visuals";

    /// <summary>
    /// Crea (o recupera) los assets de la Fase 3: cuatro <see cref="WeaponDataSO"/> (un
    /// arquetipo cada uno), los <see cref="StatModifierSO"/> reutilizables y tres
    /// <see cref="UpgradeDataSO"/> v2 que ejemplifican rareza, modificadores y arma otorgada.
    ///
    /// Se ejecuta ANTES de <see cref="ResolveAssets"/>: así los upgrades creados aquí entran
    /// en el mismo barrido de <c>AssetDatabase.FindAssets</c> y aparecen en el panel de nivel
    /// sin tener que tocar TestBuildSettings a mano.
    ///
    /// Es idempotente: si un asset ya existe NO se reescribe, para que un diseñador pueda
    /// reequilibrarlos a mano sin que la siguiente construcción los pise.
    /// </summary>
    internal static void EnsureWeaponContent(BuildContext context)
    {
        EnsureTestFolder(TestWeaponsFolder);
        EnsureTestFolder(TestVisualsFolder);
        EnsureTestFolder(TestModifiersFolder);
        EnsureTestFolder(TestUpgradesFolder);

        GameObject projectilePrefab = EnsureProjectilePrefab(context);
        GameObject pulseVisual = EnsureVisualPrefab("WeaponPulseRing", new Color(1f, 0.90f, 0.35f, 0.45f), 1f);
        GameObject orbitVisual = EnsureVisualPrefab("WeaponOrb", new Color(0.55f, 0.85f, 1f, 1f), 0.45f);
        GameObject auraVisual = EnsureVisualPrefab("WeaponAuraRing", new Color(0.95f, 0.75f, 0.35f, 0.35f), 1f);

        WeaponDataSO pulse = EnsureWeaponData("SO_WeaponTest_Pulse", data =>
        {
            data.weaponName = "Destello de Luz";
            data.archetype = WeaponArchetype.Pulse;
            data.damage = 34f;
            data.attackRange = 4.6f;
            data.attackInterval = 0.9f;
            data.visualPrefab = pulseVisual;
            data.visualScale = 1f;
        });

        WeaponDataSO orbit = EnsureWeaponData("SO_WeaponTest_Orbit", data =>
        {
            data.weaponName = "Corona de Orbes";
            data.archetype = WeaponArchetype.Orbit;
            data.damage = 22f;
            data.attackInterval = 1f;             // sólo la usa el fallback de daño del arma
            data.orbitCount = 3;
            data.orbitRadius = 2.3f;
            data.orbitDegreesPerSecond = 135f;
            data.orbitHitInterval = 0.45f;
            data.orbitOrbRadius = 0.45f;
            data.visualPrefab = orbitVisual;
            data.visualScale = 1f;
        });

        WeaponDataSO projectile = EnsureWeaponData("SO_WeaponTest_Projectile", data =>
        {
            data.weaponName = "Dardos de Luz";
            data.archetype = WeaponArchetype.Projectile;
            data.damage = 26f;
            data.attackInterval = 1.1f;
            data.projectileCount = 1;
            data.projectileSpeed = 17f;
            data.projectileLifetime = 1.6f;
            data.projectileHitRadius = 0.6f;
            data.projectilePierce = 2;
            data.projectileSpreadDegrees = 8f;
            data.projectilePrefab = projectilePrefab;
        });

        WeaponDataSO aura = EnsureWeaponData("SO_WeaponTest_Aura", data =>
        {
            data.weaponName = "Manto Sagrado";
            data.archetype = WeaponArchetype.Aura;
            data.damage = 15f;
            data.attackInterval = 1f;
            data.auraRadius = 3.4f;
            data.auraTickInterval = 0.5f;
            data.visualPrefab = auraVisual;
            data.visualScale = 1f;
        });

        EnsureTestModifiers();
        EnsureTestV2Upgrades(pulse);

        context.TestWeapons = new[] { pulse, orbit, projectile, aura };
        context.WeaponProjectilePrefab = projectilePrefab;

        Debug.Log(
            $"<color=green>[TestSceneBuilder] Contenido de armas listo: {context.TestWeapons.Length} WeaponDataSO, " +
            $"proyectil {(projectilePrefab != null ? projectilePrefab.name : "NO")}.</color>",
            null);
    }

    /// <summary>Recupera el WeaponDataSO indicado o lo crea con la configuración dada.</summary>
    private static WeaponDataSO EnsureWeaponData(string fileName, System.Action<WeaponDataSO> configure)
    {
        string path = $"{TestWeaponsFolder}/{fileName}.asset";
        WeaponDataSO data = AssetDatabase.LoadAssetAtPath<WeaponDataSO>(path);

        if (data != null)
        {
            return data;
        }

        data = ScriptableObject.CreateInstance<WeaponDataSO>();
        data.name = fileName;
        configure(data);

        AssetDatabase.CreateAsset(data, path);
        return data;
    }

    /// <summary>Crea la carpeta indicada (y sus padres) si todavía no existe.</summary>
    private static void EnsureTestFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
        {
            return;
        }

        int separator = path.LastIndexOf('/');
        string parent = path.Substring(0, separator);
        string name = path.Substring(separator + 1);

        if (!AssetDatabase.IsValidFolder(parent))
        {
            EnsureTestFolder(parent);
        }

        AssetDatabase.CreateFolder(parent, name);
    }

    /// <summary>
    /// Visual genérico de un arma: esfera con material emisivo y SIN collider. Los
    /// arquetipos sólo necesitan un transform que escalar/rotar, así que una primitiva bien
    /// materializada basta para ver el arma funcionando sin depender de arte pendiente.
    /// </summary>
    private static GameObject EnsureVisualPrefab(string fileName, Color color, float scale)
    {
        string path = $"{TestVisualsFolder}/{fileName}.prefab";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

        if (prefab != null)
        {
            return prefab;
        }

        GameObject root = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        root.name = fileName;

        // El daño NO depende de coliders: WeaponProjectile y los behaviours usan
        // OverlapSphereNonAlloc. Dejar un collider aquí haría doble trabajo en cada impacto.
        Collider bodyCollider = root.GetComponent<Collider>();

        if (bodyCollider != null)
        {
            Object.DestroyImmediate(bodyCollider);
        }

        Material material = EnsureEmissiveMaterial($"{fileName}Mat", color);

        if (root.TryGetComponent(out MeshRenderer renderer))
        {
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        root.transform.localScale = Vector3.one * scale;

        prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefab;
    }

    /// <summary>Material URP emisivo (color + glow) para los visuales de las armas.</summary>
    private static Material EnsureEmissiveMaterial(string fileName, Color color)
    {
        string path = $"{TestVisualsFolder}/{fileName}.mat";
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);

        if (existing != null)
        {
            return existing;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

        if (shader == null)
        {
            Debug.LogWarning($"[TestSceneBuilder] Sin shader URP/Standard para el material {fileName}.");
            return null;
        }

        Material material = new Material(shader) { name = fileName };

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        material.color = color;

        // La emisividad hace que las armas se lean sobre el suelo oscuro de la arena sin
        // necesidad de más luces en la escena.
        material.EnableKeyword("_EMISSION");

        if (material.HasProperty("_EmissionColor"))
        {
            material.SetColor("_EmissionColor", color * 1.6f);
        }

        if (material.HasProperty("_Smoothness"))
        {
            material.SetFloat("_Smoothness", 0.35f);
        }

        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    /// <summary>
    /// Prefab del proyectil del arquetipo Projectile. No lleva collider ni Rigidbody a
    /// propósito: <see cref="WeaponProjectile"/> resuelve el impacto con un barrido de
    /// <c>OverlapSphereNonAlloc</c> sobre el segmento recorrido, que no tiene tunneling y no
    /// depende de la matriz de colisión del proyecto.
    /// </summary>
    private static GameObject EnsureProjectilePrefab(BuildContext context)
    {
        // Vía manual primero: si el diseñador ya tiene un prefab, suyo manda.
        if (context.Settings.weaponProjectilePrefab != null)
        {
            return context.Settings.weaponProjectilePrefab;
        }

        string path = $"{TestWeaponsFolder}/WeaponProjectile.prefab";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

        if (prefab != null)
        {
            return prefab;
        }

        GameObject root = new GameObject("WeaponProjectile");
        root.AddComponent<WeaponProjectile>();

        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        visual.name = "Mesh";
        visual.transform.SetParent(root.transform, false);
        visual.transform.localScale = Vector3.one * 0.35f;

        Collider bodyCollider = visual.GetComponent<Collider>();

        if (bodyCollider != null)
        {
            Object.DestroyImmediate(bodyCollider);
        }

        if (visual.TryGetComponent(out MeshRenderer renderer))
        {
            renderer.sharedMaterial =
                EnsureEmissiveMaterial("WeaponProjectileMat", new Color(1f, 0.85f, 0.4f, 1f));
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefab;
    }

    /// <summary>
    /// Catálogo de <see cref="StatModifierSO"/> del proyecto de pruebas. Se crean aunque hoy
    /// sólo los usen las mejoras v2: un diseñador puede referenciarlos desde cualquier
    /// UpgradeDataSO sin tener que rehacer el +20% de daño por enésima vez.
    /// </summary>
    private static void EnsureTestModifiers()
    {
        EnsureModifier("SO_Mod_Damage20", StatType.IncreaseDamage, StatOperation.PercentIncrease, 0.20f);
        EnsureModifier("SO_Mod_MoveSpeed12", StatType.IncreaseMoveSpeed, StatOperation.PercentIncrease, 0.12f);
        EnsureModifier("SO_Mod_Area15", StatType.IncreaseAreaSize, StatOperation.PercentIncrease, 0.15f);
        EnsureModifier("SO_Mod_Duration10", StatType.IncreaseDuration, StatOperation.PercentIncrease, 0.10f);
        EnsureModifier("SO_Mod_MaxHealth25", StatType.IncreaseMaxHealth, StatOperation.Add, 25f);
        EnsureModifier("SO_Mod_Crit10", StatType.IncreaseCritChance, StatOperation.Add, 0.10f);
        EnsureModifier("SO_Mod_Armor3", StatType.IncreaseArmor, StatOperation.Add, 3f);
        EnsureModifier("SO_Mod_Regen1", StatType.HealthRegen, StatOperation.Add, 1f);
        EnsureModifier("SO_Mod_Projectile1", StatType.IncreaseProjectileCount, StatOperation.Add, 1f);
        EnsureModifier("SO_Mod_Cooldown10", StatType.DecreaseAttackInterval, StatOperation.PercentIncrease, 0.10f);
    }

    private static StatModifierSO EnsureModifier(
        string fileName, StatType stat, StatOperation operation, float value)
    {
        string path = $"{TestModifiersFolder}/{fileName}.asset";
        StatModifierSO modifier = AssetDatabase.LoadAssetAtPath<StatModifierSO>(path);

        if (modifier != null)
        {
            return modifier;
        }

        modifier = ScriptableObject.CreateInstance<StatModifierSO>();
        modifier.name = fileName;
        modifier.stat = stat;
        modifier.operation = operation;
        modifier.value = value;

        AssetDatabase.CreateAsset(modifier, path);
        return modifier;
    }

    /// <summary>
    /// Tres mejoras v2 de ejemplo, una por rareza alta. Existen para que la Fase 2 y la 3 se
    /// puedan verificar en la escena TEST sin crear assets a mano: modificadores varios,
    /// una mejora apilable y una que otorga un arma.
    /// </summary>
    private static void EnsureTestV2Upgrades(WeaponDataSO grantedWeapon)
    {
        StatModifierSO damage20 = EnsureModifier(
            "SO_Mod_Damage20", StatType.IncreaseDamage, StatOperation.PercentIncrease, 0.20f);
        StatModifierSO area15 = EnsureModifier(
            "SO_Mod_Area15", StatType.IncreaseAreaSize, StatOperation.PercentIncrease, 0.15f);
        StatModifierSO duration10 = EnsureModifier(
            "SO_Mod_Duration10", StatType.IncreaseDuration, StatOperation.PercentIncrease, 0.10f);
        StatModifierSO maxHealth25 = EnsureModifier(
            "SO_Mod_MaxHealth25", StatType.IncreaseMaxHealth, StatOperation.Add, 25f);

        EnsureUpgradeData("SO_UpgradeTest_Fuerza", upgrade =>
        {
            upgrade.upgradeName = "Fuerza Radiante";
            upgrade.description = "+20% de daño en todas las armas.";
            upgrade.modifiers = new[] { damage20 };
            upgrade.rarity = UpgradeRarity.Rare;
            upgrade.maxStacks = 3;
            upgrade.weight = 90;
        });

        EnsureUpgradeData("SO_UpgradeTest_Torre", upgrade =>
        {
            upgrade.upgradeName = "Torre de Presencia";
            upgrade.description = "+15% de área y +10% de duración de efectos.";
            upgrade.modifiers = new[] { area15, duration10 };
            upgrade.rarity = UpgradeRarity.Epic;
            upgrade.maxStacks = 2;
            upgrade.weight = 60;
        });

        EnsureUpgradeData("SO_UpgradeTest_Destello", upgrade =>
        {
            upgrade.upgradeName = "Destello Sellado";
            upgrade.description = "+25 de vida máxima y equipa (o mejora) el Destello de Luz.";
            upgrade.modifiers = new[] { maxHealth25 };
            upgrade.grantsWeapon = grantedWeapon;
            upgrade.rarity = UpgradeRarity.Legendary;
            upgrade.maxStacks = 1;
            upgrade.weight = 30;
        });
    }

    private static void EnsureUpgradeData(string fileName, System.Action<UpgradeDataSO> configure)
    {
        string path = $"{TestUpgradesFolder}/{fileName}.asset";
        UpgradeDataSO upgrade = AssetDatabase.LoadAssetAtPath<UpgradeDataSO>(path);

        if (upgrade != null)
        {
            return;
        }

        upgrade = ScriptableObject.CreateInstance<UpgradeDataSO>();
        upgrade.name = fileName;
        configure(upgrade);

        AssetDatabase.CreateAsset(upgrade, path);
    }

    /// <summary>
    /// Añade el <see cref="WeaponController"/> al Player. Se hace aquí y no en el código de
    /// producción porque el Player real aún no lo necesita: en la escena TEST es el sistema
    /// que queremos ejercitar, y añadirlo desde el builder deja el juego intacto.
    /// </summary>
    internal static void BuildWeapons(BuildContext context)
    {
        if (context.Player == null)
        {
            Debug.LogError("[TestSceneBuilder] No hay Player: se omite la construcción del sistema de armas.");
            return;
        }

        WeaponController controller = context.Player.GetComponent<WeaponController>();

        if (controller == null)
        {
            controller = context.Player.AddComponent<WeaponController>();
        }

        context.WeaponController = controller;

        // La regeneración de vida vive en su propio componente para no meter coste de Update
        // en HealthComponent, que ya lo usan todos los enemigos.
        if (context.Player.GetComponent<PlayerRegeneration>() == null)
        {
            context.Player.AddComponent<PlayerRegeneration>();
        }

        // Sin pulso de arranque: el WeaponController desactiva PlayerAttack cuando el sistema
        // nuevo está activo, y tener el pulso en startingWeapons no aporta nada (es el
        // comportamiento de Pulse, ahora gestionado por PulseWeaponBehaviour). El catálogo
        // inicial se asigna más abajo con SetObjectArray (SetValue no admite arrays).
        TestSceneBuilderUtil.SetValue(controller, "legacyPulse", context.Player.GetComponent<PlayerAttack>());
        TestSceneBuilderUtil.SetValue(controller, "useNewWeaponSystem", true);
        TestSceneBuilderUtil.SetValue(controller, "enemyLayer", 1 << TestSceneBuilderUtil.LayerIndex("Enemy"));

        // Cuatro armas de partida: el sandbox puede añadirlas con F11, pero arrancar con el
        // arquetipo Pulse (el original) equipado hace que la escena se vea "jugable" en el
        // primer frame sin pulsar nada.
        if (context.TestWeapons != null && context.TestWeapons.Length > 0 && context.TestWeapons[0] != null)
        {
            TestSceneBuilderUtil.SetObjectArray(controller, "startingWeapons",
                new WeaponDataSO[] { context.TestWeapons[0] });
        }

        Debug.Log(
            $"[TestSceneBuilder] WeaponController en el Player (sistema nuevo activo, " +
            $"capa enemiga '{LayerMask.LayerToName(TestSceneBuilderUtil.LayerIndex("Enemy"))}').",
            context.Player);
    }

    /// <summary>Cablea las referencias del WeaponController y el catálogo del sandbox.</summary>
    private static void WireWeapons(BuildContext context)
    {
        if (context.WeaponController != null && context.Player != null)
        {
            TestSceneBuilderUtil.SetValue(context.WeaponController, "runStats",
                context.Player.GetComponent<RunStats>());
        }

        // GrantWeapon y PushDerivedStatsToPlayer (Fase 2/3) se apoyan en esta referencia:
        // sin ella, una mejora que concediera un arma falliría en silencio.
        if (context.UpgradeManager != null)
        {
            TestSceneBuilderUtil.SetValue(context.UpgradeManager, "weaponController",
                context.WeaponController);
        }

        TestEnvironmentController test = context.TestController;

        if (test == null)
        {
            return;
        }

        TestSceneBuilderUtil.SetValue(test, "weaponController", context.WeaponController);

        if (context.TestWeapons != null)
        {
            TestSceneBuilderUtil.SetObjectArray(test, "testWeapons", context.TestWeapons);
        }
    }

    /// <summary>
    /// Lee de vuelta un campo de referencia ya escrito en la escena. Comprobar el contexto
    /// no basta: si el nombre del campo hubiera cambiado en producción, SetValue habría
    /// lanzado un LogError por su cuenta, pero aquí se quiere un fallo explícito en la
    /// lista de validación.
    /// </summary>
    private static int ReportWired(Component component, string field, string what)
    {
        if (component == null)
        {
            return Report(false, what + " (componente ausente)");
        }

        SerializedObject serialized = new SerializedObject(component);
        SerializedProperty property = serialized.FindProperty(field);

        bool ok = property != null
            && property.propertyType == SerializedPropertyType.ObjectReference
            && property.objectReferenceValue != null;

        return Report(ok, what);
    }
}
