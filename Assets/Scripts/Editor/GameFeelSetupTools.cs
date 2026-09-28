using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
/// <summary>
/// Configuración guiada del "Game Feel" y de las armas secundarias desde el editor
/// (menú 40 Days of Light > Game Feel).
///
/// POR QUÉ EXISTE: cablear a mano el HitStopManager, la viñeta de daño, el WeaponController
/// y varios ScriptableObjects de mejora son muchas operaciones repetitivas donde un solo clic
/// mal puesto rompe el sistema en silencio. Todas las herramientas son IDEMPOTENTES: si algo ya
/// está hecho, solo lo avisa y no lo duplica.
///
/// Mismo patrón y mismas reglas que <c>JuiceSetupTools</c>: nada de tocar la escena sin avisar,
/// y siempre marcar la escena como sucia para que el cambio se guarde.
/// </summary>
public static class GameFeelSetupTools
{
    private const string MenuRoot = "40 Days of Light/Game Feel/";

    private const string VignetteSpritePath = "Assets/UI/Vignette_Damage.png";
    private const string WeaponFolder = "Assets/Scripts/ScriptableObject/Weapon";
    private const string UpgradeFolder = "Assets/Scripts/ScriptableObject/Upgrade";
    private const string ModifierFolder = "Assets/Scripts/ScriptableObject/Modifier";

    private const int VignetteResolution = 256;

    /// <summary>Número de la capa 'Enemy'. Debe coincidir con la que usa PlayerAttack.</summary>
    private const int EnemyLayerNumber = 8;

    // ==================================================================
    // 0. TODO DE UNA VEZ
    // ==================================================================

    /// <summary>
    /// Ejecuta TODAS las operaciones en el orden correcto. Es la opción recomendada:
    /// deja la escena lista para probar sin tocar el Inspector a mano.
    /// </summary>
    [MenuItem(MenuRoot + "0. ⚡ Configurar TODO (1 clic)", false, 0)]
    private static void SetupEverything()
    {
        // El orden importa: los assets se normalizan DESPUÉS de crearlos, porque la
        // normalización les asigna el título y la descripción definitivos a los recién creados.
        CreateVignetteSprite();
        SetupHitStopManager();
        SetupDamageVignette();
        SetupWeaponController();
        CreateWeaponAssets();
        CreateUpgradeAssets();
        CreateStartingWeaponAssets();
        CreateBossRewardAssets();
        NormalizeUpgradeAssets();
        SetupRunWeaponFlow();
        AuditSetup();

        Debug.Log(
            "<color=green>[GameFeel] Configuración completa. Revisa el informe de auditoría en la consola, " +
            "guarda la escena (Ctrl+S) y entra en Play Mode.</color>");
    }

    // ==================================================================
    // 1. SPRITE DE LA VIÑETA
    // ==================================================================

    /// <summary>
    /// Genera el sprite de viñeta radial: un PNG blanco con alpha que cae del centro
    /// (transparente) hacia los bordes (opaco). Al teñirlo de rojo, el flash queda
    /// concentrado en la periferia y el centro de la pantalla queda limpio.
    /// </summary>
    [MenuItem(MenuRoot + "1. Crear sprite de viñeta radial", false, 20)]
    private static void CreateVignetteSprite()
    {
        if (File.Exists(VignetteSpritePath))
        {
            Debug.LogWarning($"[GameFeel] '{VignetteSpritePath}' ya existe. Bórralo si quieres regenerarlo.");
            return;
        }

        Texture2D texture = new Texture2D(VignetteResolution, VignetteResolution, TextureFormat.RGBA32, false)
        {
            name = "Vignette_Damage",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            anisoLevel = 0
        };

        float half = VignetteResolution * 0.5f;
        Color32[] pixels = new Color32[VignetteResolution * VignetteResolution];

        for (int y = 0; y < VignetteResolution; y++)
        {
            for (int x = 0; x < VignetteResolution; x++)
            {
                float dx = (x + 0.5f - half) / half;
                float dy = (y + 0.5f - half) / half;
                float distance = Mathf.Sqrt((dx * dx) + (dy * dy));

                // 0 en el centro, 1 en la esquina. SmoothStep concentra el efecto en el borde
                // sin el anillo duro que produce un degradado lineal simple.
                float alpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(distance * 0.85f));
                alpha *= alpha; // Interior limpio, solo la franja exterior.

                pixels[(y * VignetteResolution) + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply();

        EnsureFolder(Path.GetDirectoryName(VignetteSpritePath));

        byte[] png = texture.EncodeToPNG();
        Object.DestroyImmediate(texture);

        File.WriteAllBytes(VignetteSpritePath, png);
        AssetDatabase.ImportAsset(VignetteSpritePath, ImportAssetOptions.ForceUpdate);

        Sprite sprite = LoadVignetteSprite();

        if (sprite == null)
        {
            Debug.LogError($"[GameFeel] No se pudo cargar el sprite de viñeta desde '{VignetteSpritePath}'.");
            return;
        }

        Selection.activeObject = sprite;
        EditorGUIUtility.PingObject(sprite);

        Debug.Log($"[GameFeel] Sprite de viñeta creado en '{VignetteSpritePath}'.", sprite);
    }

    /// <summary>Carga el sprite forzando el importador a tipo Sprite si hace falta.</summary>
    private static Sprite LoadVignetteSprite()
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(VignetteSpritePath);
        if (sprite != null) return sprite;

        TextureImporter importer = AssetImporter.GetAtPath(VignetteSpritePath) as TextureImporter;
        if (importer == null) return null;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Sprite>(VignetteSpritePath);
    }
    // ==================================================================
    // 2. HIT STOP MANAGER
    // ==================================================================

    /// <summary>
    /// Añade el HitStopManager al GameObject 'Managers', junto al GameStateController que
    /// consume su señal. Si ya existe, solo comprueba que los valores sean los recomendados.
    /// </summary>
    [MenuItem(MenuRoot + "2. Añadir HitStopManager a Managers", false, 30)]
    private static void SetupHitStopManager()
    {
        if (Application.isPlaying)
        {
            Debug.LogWarning("[GameFeel] Sal de Play Mode antes de modificar la escena.");
            return;
        }

        GameObject managers = FindOrCreateManagersObject();
        if (managers == null) return;

        HitStopManager manager = managers.GetComponent<HitStopManager>();

        if (manager == null)
        {
            manager = managers.AddComponent<HitStopManager>();
        }
        else
        {
            Debug.Log("[GameFeel] El GameObject 'Managers' ya tenía un HitStopManager.", manager);
        }

        SerializedObject serialized = new SerializedObject(manager);

        SetFloat(serialized, "defaultDuration", 0.04f);
        SetFloat(serialized, "maxDuration", 0.15f);
        SetFloat(serialized, "hitStopTimeScale", 0.05f);
        SetFloat(serialized, "minIntervalBetweenStops", 0.06f);
        SetBool(serialized, "logHitStops", false);

        serialized.ApplyModifiedProperties();
        EditorUtility.SetDirty(manager);
        MarkActiveSceneDirty();

        Selection.activeObject = manager;
        EditorGUIUtility.PingObject(manager);

        Debug.Log("<color=green>[GameFeel] HitStopManager listo en 'Managers' (0.04s a escala 0.05).</color>", manager);
    }

    // ==================================================================
    // 3. VIÑETA DE DAÑO
    // ==================================================================

    /// <summary>
    /// Crea el objeto 'DamageVignette' como PRIMER hijo del Canvas del HUD.
    ///
    /// Va primero a propósito: en UI el orden de los hermanos es el orden de dibujado, así que
    /// ser el primero lo deja DETRÁS de las barras de vida, el temporizador y los botones. Una
    /// viñeta por encima del HUD taparía justo la información que el jugador debe leer.
    /// </summary>
    [MenuItem(MenuRoot + "3. Montar DamageVignette en el Canvas", false, 31)]
    private static void SetupDamageVignette()
    {
        if (Application.isPlaying)
        {
            Debug.LogWarning("[GameFeel] Sal de Play Mode antes de modificar la escena.");
            return;
        }

        Canvas canvas = FindHudCanvas();

        if (canvas == null)
        {
            Debug.LogError("[GameFeel] No se encontró el Canvas del HUD. Abre 'Assets/Scenes/Prototype.unity' y vuelve a intentarlo.");
            return;
        }

        DamageVignetteUI existing = canvas.GetComponentInChildren<DamageVignetteUI>(true);

        if (existing != null)
        {
            Debug.Log($"[GameFeel] '{existing.name}' ya está en el Canvas. Se re-normaliza su rect.", existing);
            NormalizeVignetteRect(existing.transform as RectTransform);
            return;
        }

        Sprite sprite = LoadVignetteSprite();

        if (sprite == null)
        {
            Debug.LogError("[GameFeel] Falta el sprite de la viñeta. Ejecuta antes la opción '1. Crear sprite de viñeta radial'.");
            return;
        }

        GameObject vignette = new GameObject(
            "DamageVignette",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(DamageVignetteUI));

        RectTransform rect = vignette.GetComponent<RectTransform>();
        rect.SetParent(canvas.transform, false);
        NormalizeVignetteRect(rect);

        Image image = vignette.GetComponent<Image>();
        image.sprite = sprite;
        image.color = Color.clear;      // DamageVignetteUI.Awake la deja a clear y luego anima el alfa.
        image.raycastTarget = false;   // Decorativa: no debe robarle el ratón a los botones.
        image.preserveAspect = false;

        rect.SetAsFirstSibling();      // La viñeta es la capa más baja del HUD.

        HealthComponent playerHealth = FindPlayerHealth();

        SerializedObject serialized = new SerializedObject(vignette.GetComponent<DamageVignetteUI>());
        SetObject(serialized, "vignetteImage", image);

        if (playerHealth != null)
        {
            SetObject(serialized, "targetHealth", playerHealth);
        }

        serialized.ApplyModifiedProperties();

        MarkActiveSceneDirty();
        Selection.activeObject = vignette;
        EditorGUIUtility.PingObject(vignette);

        Debug.Log(
            $"<color=green>[GameFeel] Viñeta de daño montada en '{canvas.name}'. " +
            $"Target: {(playerHealth != null ? playerHealth.gameObject.name : "se resolverá en runtime")}.</color>",
            vignette);
    }

    /// <summary>Restaura el quad a pantalla completa si alguien lo movió o escaló a mano.</summary>
    private static void NormalizeVignetteRect(RectTransform rect)
    {
        if (rect == null) return;

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }
    // ==================================================================
    // 4. WEAPON CONTROLLER EN EL PLAYER
    // ==================================================================

    /// <summary>
    /// Añade el WeaponController al Player y lo cablea contra sus propias piezas.
    ///
    /// IMPORTANTE: <c>useNewWeaponSystem</c> se deja en <c>false</c> a propósito. Con
    /// <c>false</c> el componente queda inerte y el juego sigue usando el pulso de
    /// PlayerAttack, exactamente igual que antes: es un sistema aditivo, no un reemplazo.
    /// El jugador sigue empezando solo con el Destello de Luz y las armas secundarias se
    /// GANAN en el panel de nivel, que es el diseño del roguelite.
    /// </summary>
    [MenuItem(MenuRoot + "4. Añadir WeaponController al Player", false, 40)]
    private static void SetupWeaponController()
    {
        if (Application.isPlaying)
        {
            Debug.LogWarning("[GameFeel] Sal de Play Mode antes de modificar la escena.");
            return;
        }

        PlayerController player = Object.FindAnyObjectByType<PlayerController>(FindObjectsInactive.Include);

        if (player == null)
        {
            Debug.LogError("[GameFeel] No se encontró el Player en la escena abierta.");
            return;
        }

        RunStats runStats = player.GetComponent<RunStats>();
        HealthComponent playerHealth = player.GetComponent<HealthComponent>();
        PlayerAttack legacyPulse = player.GetComponent<PlayerAttack>();

        WeaponController controller = player.GetComponent<WeaponController>();
        bool created = false;

        if (controller == null)
        {
            controller = player.gameObject.AddComponent<WeaponController>();
            created = true;
        }
        else
        {
            Debug.Log($"[GameFeel] '{player.name}' ya tenía un WeaponController.", controller);
        }

        SerializedObject serialized = new SerializedObject(controller);

        SetObject(serialized, "runStats", runStats);
        SetObject(serialized, "legacyPulse", legacyPulse);

        // Capa 'Enemy' (bit 8 = m_Bits 256), la misma que ya usa PlayerAttack.
        SerializedProperty layerProp = serialized.FindProperty("enemyLayer");
        if (layerProp != null) layerProp.intValue = 1 << EnemyLayerNumber;

        SetInt(serialized, "maxWeaponSlots", 4);
        SetFloat(serialized, "damagePerWeaponLevel", 0.15f);
        SetFloat(serialized, "cooldownReductionPerWeaponLevel", 0.06f);
        SetBool(serialized, "useNewWeaponSystem", false);

        // Arranca SIN armas: se ganan en el panel de nivel.
        SerializedProperty starting = serialized.FindProperty("startingWeapons");
        if (starting != null) starting.arraySize = 0;

        serialized.ApplyModifiedProperties();
        EditorUtility.SetDirty(controller);

        // El UpgradeManager necesita conocer al WeaponController para poder equipar armas.
        bool wired = WireUpgradeManager(controller, runStats, playerHealth);

        MarkActiveSceneDirty();
        Selection.activeObject = controller;
        EditorGUIUtility.PingObject(controller);

        Debug.Log(
            $"<color=green>[GameFeel] WeaponController {(created ? "añadido" : "ya presente")} en '{player.name}'. " +
            $"Sistema nuevo: DESACTIVADO (sigue mandando el pulso de PlayerAttack). " +
            $"{(wired ? "UpgradeManager enlazado." : "AVISO: no se encontró UpgradeManager en la escena.")}</color>",
            controller);
    }

    /// <summary>Asigna el WeaponController, el RunStats y la vida del Player al UpgradeManager.</summary>
    private static bool WireUpgradeManager(WeaponController controller, RunStats runStats, HealthComponent playerHealth)
    {
        UpgradeManager[] managers = Object.FindObjectsByType<UpgradeManager>(FindObjectsInactive.Include);

        if (managers == null || managers.Length == 0)
        {
            return false;
        }

        for (int i = 0; i < managers.Length; i++)
        {
            SerializedObject serialized = new SerializedObject(managers[i]);

            SetObject(serialized, "weaponController", controller);
            SetObject(serialized, "runStats", runStats);
            SetObject(serialized, "playerHealth", playerHealth);

            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(managers[i]);
        }

        return true;
    }

    // ==================================================================
    // 5. SCRIPTABLEOBJECTS DE LAS ARMAS SECUNDARIAS
    // ==================================================================

    /// <summary>
    /// Crea los tres WeaponDataSO de los arquetipos que WeaponController ya sabe ejecutar
    /// (Orbit, Projectile, Aura). Son idempotentes: si el asset existe, se reutiliza.
    /// </summary>
    [MenuItem(MenuRoot + "5. Crear assets de armas secundarias", false, 50)]
    private static void CreateWeaponAssets()
    {
        EnsureFolder(WeaponFolder);

        // --- Órbita: orbes que giran y golpean al contacto. Daño moderado y muy constante.
        WeaponDataSO orbit = LoadOrCreate<WeaponDataSO>($"{WeaponFolder}/SO_Weapon_OrbitaLuz.asset");
        SetString(orbit, "weaponName", "Órbita de Luz");
        SetEnum(orbit, "archetype", WeaponArchetype.Orbit);
        SetFloat(orbit, "damage", 12f);
        SetFloat(orbit, "attackRange", 2.2f);
        SetFloat(orbit, "attackInterval", 0.5f);
        SetInt(orbit, "orbitCount", 2);
        SetFloat(orbit, "orbitRadius", 2.2f);
        SetFloat(orbit, "orbitDegreesPerSecond", 130f);
        SetFloat(orbit, "orbitHitInterval", 0.4f);
        SetFloat(orbit, "orbitOrbRadius", 0.45f);
        SetColor(orbit, "flashColor", new Color(1f, 0.85f, 0.35f, 0.6f));

        // --- Proyectil: daño alto por impacto y pocos golpes. Pierce 1 para no atravesar la horda entera.
        WeaponDataSO projectile = LoadOrCreate<WeaponDataSO>($"{WeaponFolder}/SO_Weapon_LanzasLuz.asset");
        SetString(projectile, "weaponName", "Lanzas de Luz");
        SetEnum(projectile, "archetype", WeaponArchetype.Projectile);
        SetFloat(projectile, "damage", 18f);
        SetFloat(projectile, "attackRange", 12f);
        SetFloat(projectile, "attackInterval", 1.1f);
        SetInt(projectile, "projectileCount", 1);
        SetFloat(projectile, "projectileSpeed", 18f);
        SetFloat(projectile, "projectileLifetime", 1.6f);
        SetFloat(projectile, "projectileHitRadius", 0.6f);
        SetInt(projectile, "projectilePierce", 1);
        SetFloat(projectile, "projectileSpreadDegrees", 6f);
        SetColor(projectile, "flashColor", new Color(0.6f, 0.85f, 1f, 0.7f));

        // --- Aura: daño continuo por ticks. Daño bajo porque se aplica cada 0.6s sin parar.
        WeaponDataSO aura = LoadOrCreate<WeaponDataSO>($"{WeaponFolder}/SO_Weapon_AuraSagrada.asset");
        SetString(aura, "weaponName", "Aura Sagrada");
        SetEnum(aura, "archetype", WeaponArchetype.Aura);
        SetFloat(aura, "damage", 6f);
        SetFloat(aura, "attackRange", 3.2f);
        SetFloat(aura, "attackInterval", 0.6f);
        SetFloat(aura, "auraRadius", 3.2f);
        SetFloat(aura, "auraTickInterval", 0.6f);
        SetColor(aura, "flashColor", new Color(1f, 0.6f, 0.85f, 0.45f));

        EditorUtility.SetDirty(orbit);
        EditorUtility.SetDirty(projectile);
        EditorUtility.SetDirty(aura);
        AssetDatabase.SaveAssets();

        Debug.Log("<color=green>[GameFeel] 3 armas secundarias listas: Órbita, Lanzas y Aura.</color>", orbit);
    }
    // ==================================================================
    // 6. SCRIPTABLEOBJECTS DE MEJORAS
    // ==================================================================

    /// <summary>
    /// Crea las mejoras nuevas y las añade a la lista de LevelUpUI. Tres de ellas otorgan un
    /// arma secundaria (campo <c>grantsWeapon</c>); las otras tres son mejoras de estadística
    /// que usan el sistema v2 de StatModifierSO.
    ///
    /// Los assets heredados (SO_Upgrade_Damage, Range, AttackSpeed, Speed, Heal) NO se tocan:
    /// siguen funcionando por el camino viejo de <c>upgradeType</c>/<c>value</c>.
    /// </summary>
    [MenuItem(MenuRoot + "6. Crear mejoras y conectarlas al LevelUpUI", false, 51)]
    private static void CreateUpgradeAssets()
    {
        EnsureFolder(ModifierFolder);
        EnsureFolder(UpgradeFolder);

        WeaponDataSO orbit = AssetDatabase.LoadAssetAtPath<WeaponDataSO>($"{WeaponFolder}/SO_Weapon_OrbitaLuz.asset");
        WeaponDataSO projectile = AssetDatabase.LoadAssetAtPath<WeaponDataSO>($"{WeaponFolder}/SO_Weapon_LanzasLuz.asset");
        WeaponDataSO aura = AssetDatabase.LoadAssetAtPath<WeaponDataSO>($"{WeaponFolder}/SO_Weapon_AuraSagrada.asset");

        List<UpgradeDataSO> created = new List<UpgradeDataSO>();

        // --- Mejoras de ARMA: otorgan el arma y la suben de nivel si ya la tienes ---
        created.Add(CreateWeaponUpgrade(
            "SO_Upgrade_OrbitaLuz", "Órbita de Luz",
            "Unas orbes de luz orbitan a tu alrededor y golpean al contacto.",
            UpgradeRarity.Uncommon, 3, orbit,
            new ModifierSpec(StatType.IncreaseAreaSize, StatOperation.Add, 0.2f)));

        created.Add(CreateWeaponUpgrade(
            "SO_Upgrade_LanzasLuz", "Lanzas de Luz",
            "Disparas lanzas de luz al enemigo más cercano.",
            UpgradeRarity.Rare, 3, projectile,
            new ModifierSpec(StatType.IncreaseProjectileCount, StatOperation.Add, 1f)));

        created.Add(CreateWeaponUpgrade(
            "SO_Upgrade_AuraSagrada", "Aura Sagrada",
            "Un aura sagrada daña continuamente a los enemigos cercanos.",
            UpgradeRarity.Epic, 3, aura,
            new ModifierSpec(StatType.IncreaseAreaSize, StatOperation.Add, 0.3f)));

        // --- Mejoras de ESTADÍSTICA: sistema v2 de StatModifierSO ---
        created.Add(CreateStatUpgrade(
            "SO_Upgrade_VidaMaxima", "Vitalidad", "+25 de vida máxima y la rellenas al instante.",
            UpgradeRarity.Common, 5,
            new ModifierSpec(StatType.IncreaseMaxHealth, StatOperation.Add, 25f)));

        created.Add(CreateStatUpgrade(
            "SO_Upgrade_Armadura", "Piel de Piedra", "Reduce el daño recibido en 3 puntos por golpe.",
            UpgradeRarity.Uncommon, 5,
            new ModifierSpec(StatType.IncreaseArmor, StatOperation.Add, 3f)));

        created.Add(CreateStatUpgrade(
            "SO_Upgrade_Critico", "Golpe Crítico", "10% de probabilidad de crítico (x1.5 de daño).",
            UpgradeRarity.Rare, 3,
            new ModifierSpec(StatType.IncreaseCritChance, StatOperation.Add, 0.1f)));

        AssetDatabase.SaveAssets();

        int linked = LinkUpgradesToLevelUpUI(created);

        Debug.Log($"<color=green>[GameFeel] {created.Count} mejoras creadas y {linked} enlazadas a LevelUpUI.</color>");
    }

    /// <summary>Crea (o reutiliza) una mejora que otorga un arma y además trae un modificador.</summary>
    private static UpgradeDataSO CreateWeaponUpgrade(
        string fileName, string displayName, string description,
        UpgradeRarity rarity, int maxStacks, WeaponDataSO weapon, ModifierSpec modifier)
    {
        UpgradeDataSO upgrade = LoadOrCreate<UpgradeDataSO>($"{UpgradeFolder}/{fileName}.asset");

        SetString(upgrade, "upgradeName", displayName);
        SetString(upgrade, "description", description);
        SetEnum(upgrade, "upgradeType", UpgradeType.IncreaseDamage);
        SetFloat(upgrade, "value", 0f); // Irrelevante: con modifiers[] manda la lista.
        SetObject(upgrade, "grantsWeapon", weapon);
        SetEnum(upgrade, "rarity", rarity);
        SetInt(upgrade, "maxStacks", maxStacks);
        SetInt(upgrade, "weight", WeightFor(rarity));
        SetModifiers(upgrade, modifier);

        EditorUtility.SetDirty(upgrade);
        return upgrade;
    }

    /// <summary>Crea (o reutiliza) una mejora puramente de estadística.</summary>
    private static UpgradeDataSO CreateStatUpgrade(
        string fileName, string displayName, string description,
        UpgradeRarity rarity, int maxStacks, ModifierSpec modifier)
    {
        UpgradeDataSO upgrade = LoadOrCreate<UpgradeDataSO>($"{UpgradeFolder}/{fileName}.asset");

        SetString(upgrade, "upgradeName", displayName);
        SetString(upgrade, "description", description);
        SetEnum(upgrade, "upgradeType", UpgradeType.IncreaseDamage);
        SetFloat(upgrade, "value", 0f);
        // T explícito: al pasar 'null' el compilador no puede inferir el tipo genérico (CS0411).
        SetObject<UpgradeDataSO>(upgrade, "grantsWeapon", null);
        SetEnum(upgrade, "rarity", rarity);
        SetInt(upgrade, "maxStacks", maxStacks);
        SetInt(upgrade, "weight", WeightFor(rarity));
        SetModifiers(upgrade, modifier);

        EditorUtility.SetDirty(upgrade);
        return upgrade;
    }
    /// <summary>Un modificador de estadística a crear como asset dentro de un UpgradeDataSO.</summary>
    private struct ModifierSpec
    {
        public StatType Stat;
        public StatOperation Operation;
        public float Value;

        public ModifierSpec(StatType stat, StatOperation operation, float value)
        {
            Stat = stat;
            Operation = operation;
            Value = value;
        }
    }

    /// <summary>Un asset <c>StatModifierSO</c> con su nombre derivado del efecto.</summary>
    private static StatModifierSO CreateModifierAsset(ModifierSpec spec)
    {
        string name = $"SM_{spec.Stat}";

        StatModifierSO modifier = LoadOrCreate<StatModifierSO>($"{ModifierFolder}/{name}.asset");

        SetEnum(modifier, "stat", spec.Stat);
        SetEnum(modifier, "operation", spec.Operation);
        SetFloat(modifier, "value", spec.Value);

        EditorUtility.SetDirty(modifier);
        return modifier;
    }

    /// <summary>
    /// Escribe el array <c>modifiers[]</c> de la mejora con un único elemento.
    ///
    /// OJO: <c>modifiers</c> es un array de REFERENCIAS a StatModifierSO, no de structs
    /// serializados en línea. Por eso aquí solo se asigna la referencia: los valores
    /// (stat, operation, value) ya los lleva el asset que crea <see cref="CreateModifierAsset"/>.
    /// Intentar escribir los hijos con FindPropertyRelative sobre este array daría null.
    /// </summary>
    private static void SetModifiers(UpgradeDataSO upgrade, ModifierSpec spec)
    {
        StatModifierSO modifier = CreateModifierAsset(spec);

        SerializedObject serialized = new SerializedObject(upgrade);
        SerializedProperty array = serialized.FindProperty("modifiers");

        if (array == null) return;

        array.arraySize = 1;
        array.GetArrayElementAtIndex(0).objectReferenceValue = modifier;

        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>Peso de sorteo por rareza. Las raras deben salir menos que las comunes.</summary>
    private static int WeightFor(UpgradeRarity rarity)
    {
        switch (rarity)
        {
            case UpgradeRarity.Legendary: return 10;
            case UpgradeRarity.Epic:     return 30;
            case UpgradeRarity.Rare:     return 60;
            case UpgradeRarity.Uncommon: return 80;
            default:                      return 100;
        }
    }

    /// <summary>
    /// Añade las mejoras nuevas a la lista <c>availableUpgrades</c> del LevelUpUI sin borrar
    /// las que ya había, y sin duplicar ninguna. Con 5 heredadas + 6 nuevas hay 11 candidatas
    /// para 3 cartas: la variedad sube mucho sin tocar el código del sorteo.
    /// </summary>
    private static int LinkUpgradesToLevelUpUI(List<UpgradeDataSO> upgrades)
    {
        LevelUpUI[] panels = Object.FindObjectsByType<LevelUpUI>(FindObjectsInactive.Include);

        if (panels == null || panels.Length == 0)
        {
            Debug.LogWarning("[GameFeel] No se encontró LevelUpUI: las mejoras se crearon pero no se enlazaron.");
            return 0;
        }

        int totalLinked = 0;

        for (int p = 0; p < panels.Length; p++)
        {
            SerializedObject serialized = new SerializedObject(panels[p]);
            SerializedProperty array = serialized.FindProperty("availableUpgrades");

            if (array == null) continue;

            // Índice de la primera casilla vacía: se rellena sin reordenar lo que ya había.
            int writeIndex = array.arraySize;

            for (int i = 0; i < upgrades.Count; i++)
            {
                bool alreadyListed = false;

                for (int j = 0; j < array.arraySize; j++)
                {
                    if (array.GetArrayElementAtIndex(j).objectReferenceValue == upgrades[i])
                    {
                        alreadyListed = true;
                        break;
                    }
                }

                if (alreadyListed) continue;

                array.InsertArrayElementAtIndex(writeIndex);
                array.GetArrayElementAtIndex(writeIndex).objectReferenceValue = upgrades[i];
                writeIndex++;
                totalLinked++;
            }

            // Referencia al UpgradeManager: sin ella, elegir una carta no aplicaría nada.
            SetObject(serialized, "upgradeManager", Object.FindAnyObjectByType<UpgradeManager>());

            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(panels[p]);
        }

        return totalLinked;
    }
    // ==================================================================
    // 7. AUDITORÍA
    // ==================================================================

    /// <summary>
    /// Revisa el cableado completo y reporta en consola lo que falta. NO modifica nada:
    /// sirve para confirmar de un vistazo que todo está en su sitio antes de probar.
    /// </summary>
    [MenuItem(MenuRoot + "7. Auditar cableado de Game Feel", false, 90)]
    private static void AuditSetup()
    {
        List<string> problems = new List<string>();
        List<string> notes = new List<string>();

        // --- HitStopManager ---
        HitStopManager hitStop = Object.FindAnyObjectByType<HitStopManager>(FindObjectsInactive.Include);

        if (hitStop == null)
        {
            problems.Add("Falta HitStopManager: ejecuta la opción 2.");
        }
        else if (Object.FindAnyObjectByType<GameStateController>(FindObjectsInactive.Include) == null)
        {
            problems.Add("Falta GameStateController: sin él RequestHitStop no hace nada.");
        }
        else
        {
            notes.Add("HitStopManager + GameStateController presentes.");
        }

        // --- Viñeta de daño ---
        DamageVignetteUI vignette = Object.FindAnyObjectByType<DamageVignetteUI>(FindObjectsInactive.Include);

        if (vignette == null)
        {
            problems.Add("Falta DamageVignetteUI: ejecuta la opción 3.");
        }
        else
        {
            int siblingIndex = vignette.transform.GetSiblingIndex();
            notes.Add($"Viñeta de daño montada (índice de hermano {siblingIndex} del Canvas).");

            if (siblingIndex > 0)
            {
                problems.Add("La viñeta NO es el primer hijo del Canvas: taparía el HUD. Ejecuta de nuevo la opción 3.");
            }

            if (AssetDatabase.LoadAssetAtPath<Sprite>(VignetteSpritePath) == null)
            {
                problems.Add($"Falta el sprite '{VignetteSpritePath}': se verá como un rectángulo plano.");
            }
        }

        // --- Armas ---
        PlayerController player = Object.FindAnyObjectByType<PlayerController>(FindObjectsInactive.Include);

        if (player == null)
        {
            problems.Add("No se encontró el Player.");
        }
        else if (player.GetComponent<WeaponController>() == null)
        {
            problems.Add("Falta WeaponController en el Player: ninguna mejora podrá otorgar armas. Ejecuta la opción 4.");
        }
        else
        {
            notes.Add("WeaponController presente en el Player.");
        }

        int weaponAssets = AssetDatabase.IsValidFolder(WeaponFolder)
            ? AssetDatabase.FindAssets("t:WeaponDataSO", new[] { WeaponFolder }).Length
            : 0;

        if (weaponAssets == 0)
        {
            problems.Add("No hay assets de armas secundarias: ejecuta la opción 5.");
        }
        else
        {
            notes.Add($"{weaponAssets} arma(s) secundaria(s) creadas.");
        }

        // --- Mejoras ---
        LevelUpUI panel = Object.FindAnyObjectByType<LevelUpUI>(FindObjectsInactive.Include);

        if (panel == null)
        {
            problems.Add("Falta LevelUpUI en la escena.");
        }
        else
        {
            SerializedObject serialized = new SerializedObject(panel);
            SerializedProperty list = serialized.FindProperty("availableUpgrades");
            int count = list != null ? list.arraySize : 0;

            if (count == 0)
            {
                problems.Add("LevelUpUI no tiene mejoras asignadas: el panel no repartirá cartas.");
            }
            else if (count < 3)
            {
                problems.Add($"LevelUpUI solo tiene {count} mejora(s): no puede llenar las 3 cartas.");
            }
            else
            {
                notes.Add($"LevelUpUI tiene {count} mejoras disponibles para 3 cartas.");
            }
        }

        if (Object.FindAnyObjectByType<EnemySpawner>(FindObjectsInactive.Include) == null)
        {
            problems.Add("No se encontró EnemySpawner.");
        }

        // --- Flujo de armas ---
        RunWeaponFlow flow = Object.FindAnyObjectByType<RunWeaponFlow>(FindObjectsInactive.Include);

        if (flow == null)
        {
            problems.Add("Falta RunWeaponFlow: no habrá elección de arma inicial ni recompensa de jefe. Ejecuta la opción 11.");
        }
        else
        {
            notes.Add("RunWeaponFlow cableado (arma inicial + recompensa de jefe).");
        }

        WeaponFlowCache cache = AssetDatabase.LoadAssetAtPath<WeaponFlowCache>($"{WeaponFolder}/SO_WeaponFlowCache.asset");

        if (cache == null)
        {
            problems.Add("Falta WeaponFlowCache: ejecuta las opciones 8 y 9 para crear las cartas de arma inicial y jefe.");
        }
        else
        {
            int startCount = cache.startingWeapons != null ? cache.startingWeapons.Length : 0;
            int bossCount = cache.bossRewards != null ? cache.bossRewards.Length : 0;

            if (startCount == 0)
            {
                problems.Add("No hay cartas de arma inicial configuradas: la partida arrancará con el arma por defecto.");
            }
            else
            {
                notes.Add($"{startCount} carta(s) de arma inicial.");
            }

            if (bossCount == 0)
            {
                problems.Add("No hay cartas de recompensa de jefe: se aplicará el sorteo normal al derrotarlo.");
            }
            else
            {
                notes.Add($"{bossCount} carta(s) de recompensa de jefe.");
            }
        }

        // --- Informe ---
        Debug.Log($"<color=cyan>──── Game Feel: {notes.Count} correcto(s), {problems.Count} problema(s) ────</color>");

        for (int i = 0; i < notes.Count; i++)
        {
            Debug.Log($"  <color=green>OK</color>  {notes[i]}");
        }

        for (int i = 0; i < problems.Count; i++)
        {
            Debug.LogWarning($"  <color=yellow>FALTA</color>  {problems[i]}");
        }
    }
    // ==================================================================
    // HELPERS
    // ==================================================================

    /// <summary>
    /// Localiza el GameObject 'Managers' de la escena. Si no existe, lo crea: es el contenedor
    /// natural de los singletons y el sitio donde ya vive el GameStateController.
    /// </summary>
    private static GameObject FindOrCreateManagersObject()
    {
        GameObject[] roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();

        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i] != null && roots[i].name == "Managers")
            {
                return roots[i];
            }
        }

        GameObject created = new GameObject("Managers");
        Debug.LogWarning("[GameFeel] No existía el GameObject 'Managers': se ha creado uno nuevo.", created);
        return created;
    }

    /// <summary>El Canvas del HUD: el primero en modo Overlay, que es el de juego por definición.</summary>
    private static Canvas FindHudCanvas()
    {
        Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include);

        if (canvases == null || canvases.Length == 0) return null;

        for (int i = 0; i < canvases.Length; i++)
        {
            if (canvases[i] != null && canvases[i].renderMode == RenderMode.ScreenSpaceOverlay)
            {
                return canvases[i];
            }
        }

        return canvases[0];
    }

    private static HealthComponent FindPlayerHealth()
    {
        PlayerController player = Object.FindAnyObjectByType<PlayerController>(FindObjectsInactive.Include);
        return player != null ? player.GetComponent<HealthComponent>() : null;
    }

    /// <summary>Crea la carpeta (y sus niveles intermedios) si no existe.</summary>
    private static void EnsureFolder(string folderPath)
    {
        if (string.IsNullOrEmpty(folderPath)) return;

        string normalized = folderPath.Replace('\\', '/');

        if (AssetDatabase.IsValidFolder(normalized)) return;

        string[] parts = normalized.Split('/');
        string current = parts[0];

        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];

            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }

            current = next;
        }
    }

    /// <summary>Carga el asset o lo crea vacío si no existe. Idempotente por diseño.</summary>
    private static T LoadOrCreate<T>(string assetPath) where T : ScriptableObject
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);

        if (asset != null) return asset;

        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, assetPath);

        Debug.Log($"[GameFeel] Asset creado: {assetPath}", asset);
        return asset;
    }

    // --- Escritores de SerializedProperty: null-safe y silenciosos si el campo no existe. ---

    private static void SetFloat(SerializedObject serialized, string field, float value)
    {
        SerializedProperty property = serialized.FindProperty(field);
        if (property != null && !property.isArray) property.floatValue = value;
    }

    private static void SetInt(SerializedObject serialized, string field, int value)
    {
        SerializedProperty property = serialized.FindProperty(field);
        if (property != null && !property.isArray) property.intValue = value;
    }

    private static void SetBool(SerializedObject serialized, string field, bool value)
    {
        SerializedProperty property = serialized.FindProperty(field);
        if (property != null && !property.isArray) property.boolValue = value;
    }

    private static void SetString(SerializedObject serialized, string field, string value)
    {
        SerializedProperty property = serialized.FindProperty(field);
        if (property != null && !property.isArray) property.stringValue = value;
    }

    /// <summary>Los enums de Unity se serializan por índice, igual que en runtime.</summary>
    private static void SetEnum<T>(SerializedObject serialized, string field, T value) where T : System.Enum
    {
        SerializedProperty property = serialized.FindProperty(field);
        if (property != null && !property.isArray) property.intValue = System.Convert.ToInt32(value);
    }

    private static void SetColor(SerializedObject serialized, string field, Color value)
    {
        SerializedProperty property = serialized.FindProperty(field);
        if (property != null && !property.isArray) property.colorValue = value;
    }

    private static void SetObject<T>(SerializedObject serialized, string field, T value) where T : Object
    {
        SerializedProperty property = serialized.FindProperty(field);
        if (property != null && !property.isArray) property.objectReferenceValue = value;
    }

    // --- Sobrecargas para ScriptableObject: cada asset se abre y se cierra en una llamada. ---

    private static void SetString(ScriptableObject asset, string field, string value)
    {
        Apply(asset, serialized => SetString(serialized, field, value));
    }

    private static void SetFloat(ScriptableObject asset, string field, float value)
    {
        Apply(asset, serialized => SetFloat(serialized, field, value));
    }

    private static void SetInt(ScriptableObject asset, string field, int value)
    {
        Apply(asset, serialized => SetInt(serialized, field, value));
    }

    private static void SetEnum<T>(ScriptableObject asset, string field, T value) where T : System.Enum
    {
        Apply(asset, serialized => SetEnum(serialized, field, value));
    }

    private static void SetColor(ScriptableObject asset, string field, Color value)
    {
        Apply(asset, serialized => SetColor(serialized, field, value));
    }

    private static void SetObject<T>(ScriptableObject asset, string field, T value) where T : Object
    {
        Apply(asset, serialized => SetObject(serialized, field, value));
    }

    /// <summary>
    /// Abre el asset, delega la escritura y confirma. Sin undo: son migraciones de una sola
    /// vez y guardarlas en la pila de undo solo haría que Ctrl+Z las revierta una a una.
    /// </summary>
    private static void Apply(ScriptableObject asset, System.Action<SerializedObject> write)
    {
        SerializedObject serialized = new SerializedObject(asset);
        write(serialized);
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>Marca la escena activa como modificada para que Ctrl+S la guarde.</summary>
    private static void MarkActiveSceneDirty()
    {
        if (Application.isPlaying) return;

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());
    }

    // ==================================================================
    // 8. ARMAS INICIALES Y RECOMPENSA DE JEFE
    // ==================================================================

    /// <summary>
    /// Crea las 4 cartas de ARMA INICIAL que se ofrecen al empezar la partida: Pulso,
    /// Órbita, Lanzas y Aura. La primera decisión real del jugador define su build, así que
    /// el título importa: "Destello Inicial" suena más débil que "Órbita Reluciente".
    /// </summary>
    [MenuItem(MenuRoot + "8. Crear cartas de arma inicial", false, 60)]
    private static void CreateStartingWeaponAssets()
    {
        EnsureFolder(UpgradeFolder);

        WeaponDataSO pulse = LoadPlayerPulseWeapon();
        WeaponDataSO orbit = LoadOrCreateWeaponAsset("SO_Weapon_OrbitaLuz");
        WeaponDataSO projectile = LoadOrCreateWeaponAsset("SO_Weapon_LanzasLuz");
        WeaponDataSO aura = LoadOrCreateWeaponAsset("SO_Weapon_AuraSagrada");

        List<UpgradeDataSO> starting = new List<UpgradeDataSO>();

        starting.Add(CreateWeaponUpgrade(
            "SO_Start_DestelloLuz", "Destello de Luz",
            "Tu primera arma: un pulso de luz que golpea a todo lo que te rodea.",
            UpgradeRarity.Common, 5, pulse, default(ModifierSpec)));

        starting.Add(CreateWeaponUpgrade(
            "SO_Start_OrbitaReluciente", "Órbita Reluciente",
            "Unas orbes de luz orbitan a tu alrededor y golpean al contacto.",
            UpgradeRarity.Common, 5, orbit,
            new ModifierSpec(StatType.IncreaseAreaSize, StatOperation.Add, 0.1f)));

        starting.Add(CreateWeaponUpgrade(
            "SO_Start_LanzadaSagrada", "Lanzada Sagrada",
            "Lanzas de luz disparadas al enemigo más cercano.",
            UpgradeRarity.Common, 5, projectile, default(ModifierSpec)));

        starting.Add(CreateWeaponUpgrade(
            "SO_Start_AuraPurificadora", "Aura de Purificación",
            "Un aura sagrada daña continuamente a los enemigos cercanos.",
            UpgradeRarity.Common, 5, aura, default(ModifierSpec)));

        CacheWeaponFlowList("startingWeapons", starting);

        Debug.Log($"<color=green>[GameFeel] {starting.Count} cartas de arma inicial creadas.</color>");
    }

    /// <summary>
    /// Crea las cartas de RECOMPENSA DE JEFE: evoluciones de las armas ya existentes.
    ///
    /// Son más fuertes y de rareza alta a propósito: el jefe es el momento de la partida en el
    /// que un salto de poder se lee como un premio, no como un imbalance. Solo se ofrecen al
    /// derrotar a uno (ver <see cref="SetupRunWeaponFlow"/>).
    /// </summary>
    [MenuItem(MenuRoot + "9. Crear cartas de recompensa de jefe", false, 61)]
    private static void CreateBossRewardAssets()
    {
        EnsureFolder(UpgradeFolder);

        WeaponDataSO pulse = LoadPlayerPulseWeapon();
        WeaponDataSO orbit = LoadOrCreateWeaponAsset("SO_Weapon_OrbitaLuz");
        WeaponDataSO projectile = LoadOrCreateWeaponAsset("SO_Weapon_LanzasLuz");
        WeaponDataSO aura = LoadOrCreateWeaponAsset("SO_Weapon_AuraSagrada");

        List<UpgradeDataSO> rewards = new List<UpgradeDataSO>();

        rewards.Add(CreateWeaponUpgrade(
            "SO_Boss_OrbitaCelestial", "Órbita Celestial",
            "Tus orbes giran más amplias y golpean con más fuerza.",
            UpgradeRarity.Epic, 3, orbit,
            new ModifierSpec(StatType.IncreaseAreaSize, StatOperation.Add, 0.3f)));

        rewards.Add(CreateWeaponUpgrade(
            "SO_Boss_LanzasDivinas", "Lanzas Divinas",
            "Disparas una lanza adicional por ataque.",
            UpgradeRarity.Epic, 3, projectile,
            new ModifierSpec(StatType.IncreaseProjectileCount, StatOperation.Add, 1f)));

        rewards.Add(CreateWeaponUpgrade(
            "SO_Boss_AuraInfernal", "Aura Infernal",
            "Tu aura se expande y arde con más furia.",
            UpgradeRarity.Legendary, 3, aura,
            new ModifierSpec(StatType.IncreaseAreaSize, StatOperation.Add, 0.4f)));

        rewards.Add(CreateWeaponUpgrade(
            "SO_Boss_DestelloDivino", "Destello Divino",
            "Tu pulso de luz se expande y golpea con fuerza divina.",
            UpgradeRarity.Legendary, 3, pulse,
            new ModifierSpec(StatType.IncreaseAreaSize, StatOperation.Add, 0.5f)));

        CacheWeaponFlowList("bossRewards", rewards);

        Debug.Log($"<color=green>[GameFeel] {rewards.Count} cartas de recompensa de jefe creadas.</color>");
    }

    // ==================================================================
    // 9. NORMALIZACIÓN DE UPGRADES
    // ==================================================================

    /// <summary>
    /// Normaliza TODOS los <see cref="UpgradeDataSO"/> del proyecto: título único, descripción
    /// clara y rareza coherente con lo que la mejora hace de verdad.
    ///
    /// POR QUÉ IMPORTA: el título de una carta es la primera decisión que el jugador toma sobre
    /// ella. Un catálogo con cinco "Más Daño" y tres "Aumento de daño" se lee como relleno y hace
    /// que elegir sea un acto a ciegas.
    ///
    /// IDEMPOTENTE: se puede repetir sin efectos acumulativos (no añade sufijos tipo "II").
    /// </summary>
    [MenuItem(MenuRoot + "10. Normalizar títulos y rarezas de mejoras", false, 70)]
    private static void NormalizeUpgradeAssets()
    {
        string[] guids = AssetDatabase.FindAssets("t:UpgradeDataSO", new[] { "Assets" });

        if (guids == null || guids.Length == 0)
        {
            Debug.LogWarning("[GameFeel] No se encontró ningún UpgradeDataSO en Assets/.");
            return;
        }

        HashSet<string> usedTitles = new HashSet<string>();
        int normalized = 0;
        int renamed = 0;

        // Orden estable por ruta: el resultado no depende del orden que devuelva AssetDatabase,
        // así que ejecutar la herramienta dos veces produce exactamente el mismo catálogo.
        List<UpgradeDataSO> all = new List<UpgradeDataSO>();

        for (int i = 0; i < guids.Length; i++)
        {
            UpgradeDataSO asset = AssetDatabase.LoadAssetAtPath<UpgradeDataSO>(AssetDatabase.GUIDToAssetPath(guids[i]));

            if (asset != null)
            {
                all.Add(asset);
            }
        }

        all.Sort((a, b) => string.CompareOrdinal(AssetDatabase.GetAssetPath(a), AssetDatabase.GetAssetPath(b)));

        for (int i = 0; i < all.Count; i++)
        {
            UpgradeDataSO asset = all[i];

            // Las cartas de ARMA INICIAL ya tienen nombre de diseño: son la primera decisión
            // del jugador y su nombre es intencional, no técnico. Solo se les normaliza la rareza.
            bool isStartingCard = asset.name.StartsWith("SO_Start_", System.StringComparison.Ordinal);

            string title = isStartingCard
                ? asset.upgradeName
                : BuildUniqueTitle(BuildTitleFor(asset), usedTitles);

            if (!isStartingCard && !string.Equals(title, asset.upgradeName, System.StringComparison.Ordinal))
            {
                renamed++;
            }

            usedTitles.Add(title);

            SetString(asset, "upgradeName", title);
            SetString(asset, "description", isStartingCard ? asset.description : BuildDescriptionFor(asset));
            SetEnum(asset, "rarity", ResolveRarityFor(asset));

            EditorUtility.SetDirty(asset);
            normalized++;
        }

        AssetDatabase.SaveAssets();

        Debug.Log(
            $"<color=green>[GameFeel] {normalized} mejoras normalizadas ({renamed} títulos nuevos, " +
            $"{normalized - renamed} ya estaban bien). Títulos únicos garantizados.</color>");
    }

    /// <summary>
    /// Título legible deducido del efecto real de la mejora. Se usa un mapa por estadística
    /// (y no el nombre del enum) porque "IncreaseMaxHealth" no le dice nada a un jugador.
    /// </summary>
    private static string BuildTitleFor(UpgradeDataSO asset)
    {
        // Si otorga un arma, el nombre del arma es el mejor título posible.
        if (asset.grantsWeapon != null && !string.IsNullOrWhiteSpace(asset.grantsWeapon.weaponName))
        {
            return asset.grantsWeapon.weaponName;
        }

        if (asset.modifiers != null && asset.modifiers.Length > 0)
        {
            StatModifierSO first = asset.modifiers[0];

            if (first != null)
            {
                switch (first.stat)
                {
                    case StatType.IncreaseMaxHealth:       return "Vigor Vital";
                    case StatType.IncreaseArmor:           return "Piel de Piedra";
                    case StatType.IncreaseCritChance:      return "Ojo de Halcón";
                    case StatType.IncreaseCritDamage:      return "Impacto Devastador";
                    case StatType.HealthRegen:             return "Vitalidad Sagrada";
                    case StatType.IncreaseMoveSpeed:       return "Pies Ligeros";
                    case StatType.IncreaseDamage:          return "Furia";
                    case StatType.IncreaseRange:           return "Alcance";
                    case StatType.IncreasePickupRadius:    return "Instinto de Caza";
                    case StatType.IncreaseXPGain:          return "Sed de Luz";
                    case StatType.IncreaseProjectileCount: return "Multiplicación";
                    case StatType.IncreaseAreaSize:        return "Alcance Divino";
                    case StatType.IncreaseDuration:        return "Persistencia";
                    case StatType.DecreaseAttackInterval:  return "Cadencia Letal";
                    case StatType.DecreaseCooldown:        return "Reflejo";
                }
            }
        }

        // Sin modificadores ni arma: se usa el camino heredado (upgradeType).
        switch (asset.upgradeType)
        {
            case UpgradeType.IncreaseDamage:         return "Furia";
            case UpgradeType.IncreaseRange:          return "Alcance";
            case UpgradeType.DecreaseAttackInterval: return "Cadencia Letal";
            case UpgradeType.IncreaseMoveSpeed:      return "Pies Ligeros";
            case UpgradeType.HealPlayer:             return "Curación";
        }

        return "Mejora";
    }

    /// <summary>
    /// Título único garantizado. En vez de añadir "II", "III" (que se acumulan al repetir la
    /// herramienta), usa sufijos temáticos legibles: "Furia", "Furia Ígnea", "Furia Sagrada"...
    /// Así el catálogo se mantiene limpio aunque se ejecute muchas veces.
    /// </summary>
    private static string BuildUniqueTitle(string baseTitle, HashSet<string> used)
    {
        if (!used.Contains(baseTitle))
        {
            return baseTitle;
        }

        string[] suffixes = { "Ígnea", "Sagrada", "Celestial", "Sombria", "Ancestral", "Eterna" };

        for (int i = 0; i < suffixes.Length; i++)
        {
            string candidate = baseTitle + " " + suffixes[i];

            if (!used.Contains(candidate))
            {
                return candidate;
            }
        }

        // Red de seguridad: si se agotan los sufijos temáticos, se recurre al índice.
        int index = 2;
        string numbered = baseTitle + " " + index;

        while (used.Contains(numbered))
        {
            index++;
            numbered = baseTitle + " " + index;
        }

        return numbered;
    }

    /// <summary>
    /// Descripción en lenguaje de jugador. Si el asset tiene modificadores, se apoya en
    /// <see cref="UpgradeDataSO.BuildModifierSummary"/> (que ya formatea el efecto) y le añade
    /// la frase del arma cuando la hay. Si no, describe el efecto heredado.
    /// </summary>
    private static string BuildDescriptionFor(UpgradeDataSO asset)
    {
        string modifierSummary = asset.BuildModifierSummary();
        bool hasUsefulSummary = !string.IsNullOrWhiteSpace(modifierSummary) && modifierSummary != asset.upgradeType.ToString();

        if (asset.grantsWeapon != null)
        {
            string weaponName = asset.grantsWeapon.weaponName;
            return hasUsefulSummary
                ? $"Obtienes {weaponName}. {modifierSummary}"
                : $"Obtienes el arma {weaponName}.";
        }

        if (hasUsefulSummary)
        {
            return modifierSummary;
        }

        // Camino heredado (upgradeType + value): se redacta a mano, sin decimales crudos.
        switch (asset.upgradeType)
        {
            case UpgradeType.IncreaseDamage:
                return $"Aumenta el daño de tus armas un {asset.value * 100f:0}%.";
            case UpgradeType.IncreaseRange:
                return $"Aumenta el alcance de tus armas un {asset.value * 100f:0}%.";
            case UpgradeType.DecreaseAttackInterval:
                return $"Atacas un {asset.value * 100f:0}% más rápido.";
            case UpgradeType.IncreaseMoveSpeed:
                return $"Te mueves un {asset.value * 100f:0}% más rápido.";
            case UpgradeType.HealPlayer:
                return "Recuperas vida al instante.";
        }

        return "Mejora tus estadísticas.";
    }

    /// <summary>
    /// Rareza deducida del contenido, no elegida a ojo. Regla de diseño: un ARMA COMPLETA es
    /// un salto de poder mayor que cualquier porcentaje. Los porcentajes se clasifican por
    /// magnitud: un +5% de daño es común y un +40% ya es épica.
    /// </summary>
    private static UpgradeRarity ResolveRarityFor(UpgradeDataSO asset)
    {
        // Las cartas de jefe y las de arma inicial fijan su rareza a propósito: son un caso especial.
        if (asset.name.StartsWith("SO_Boss_", System.StringComparison.Ordinal))
        {
            return asset.maxStacks <= 3 ? UpgradeRarity.Legendary : UpgradeRarity.Epic;
        }

        if (asset.name.StartsWith("SO_Start_", System.StringComparison.Ordinal))
        {
            return UpgradeRarity.Common;
        }

        bool grantsWeapon = asset.grantsWeapon != null;

        if (asset.modifiers != null && asset.modifiers.Length > 0)
        {
            StatModifierSO first = asset.modifiers[0];

            if (first != null)
            {
                float magnitude = Mathf.Abs(first.value);

                // Las bonificaciones de área/proyectiles son planas: 1 proyectil es enorme.
                if (first.stat == StatType.IncreaseProjectileCount && magnitude >= 1f)
                {
                    return UpgradeRarity.Epic;
                }

                if (magnitude >= 0.35f) return UpgradeRarity.Epic;
                if (magnitude >= 0.20f) return UpgradeRarity.Rare;
                if (magnitude >= 0.10f) return UpgradeRarity.Uncommon;

                // Valores planos (vida, armadura): se normalizan a una escala comparable.
                if (first.operation == StatOperation.Add && magnitude >= 20f) return UpgradeRarity.Rare;
                if (first.operation == StatOperation.Add && magnitude >= 5f) return UpgradeRarity.Uncommon;

                return grantsWeapon ? UpgradeRarity.Rare : UpgradeRarity.Common;
            }
        }

        if (grantsWeapon)
        {
            return UpgradeRarity.Rare;
        }

        // Efecto heredado: se usa la magnitud del value original.
        float legacy = Mathf.Abs(asset.value);

        if (legacy >= 0.3f) return UpgradeRarity.Rare;
        if (legacy >= 0.15f) return UpgradeRarity.Uncommon;
        if (legacy <= 0.001f) return UpgradeRarity.Common;

        return UpgradeRarity.Common;
    }

    // ==================================================================
    // 10. CABLEADO DEL FLUJO DE ARMAS EN LA ESCENA
    // ==================================================================

    /// <summary>
    /// Añade el <see cref="RunWeaponFlow"/> al GameObject 'Managers' y le cablea el panel, el
    /// director, el spawner y el controlador de armas, más las listas de cartas de arma
    /// inicial y de recompensa de jefe leídas del asset <see cref="WeaponFlowCache"/>.
    ///
    /// Va en el 'Managers' porque es un sistema de toda la partida, como el RunDirector.
    /// </summary>
    [MenuItem(MenuRoot + "11. Cablear flujo de armas (arma inicial + jefe)", false, 41)]
    private static void SetupRunWeaponFlow()
    {
        if (Application.isPlaying)
        {
            Debug.LogWarning("[GameFeel] Sal de Play Mode antes de modificar la escena.");
            return;
        }

        GameObject managers = FindOrCreateManagersObject();

        if (managers == null) return;

        RunWeaponFlow flow = managers.GetComponent<RunWeaponFlow>();

        if (flow == null)
        {
            flow = managers.AddComponent<RunWeaponFlow>();
        }
        else
        {
            Debug.Log("[GameFeel] El GameObject 'Managers' ya tenía un RunWeaponFlow.", flow);
        }

        LevelUpUI panel = Object.FindAnyObjectByType<LevelUpUI>(FindObjectsInactive.Include);
        RunDirector director = Object.FindAnyObjectByType<RunDirector>(FindObjectsInactive.Include);
        EnemySpawner spawner = Object.FindAnyObjectByType<EnemySpawner>(FindObjectsInactive.Include);

        PlayerController player = Object.FindAnyObjectByType<PlayerController>(FindObjectsInactive.Include);
        WeaponController weapons = player != null ? player.GetComponent<WeaponController>() : null;

        WeaponFlowCache cache = AssetDatabase.LoadAssetAtPath<WeaponFlowCache>($"{WeaponFolder}/SO_WeaponFlowCache.asset");

        if (cache == null)
        {
            Debug.LogWarning(
                "[GameFeel] No se encontró WeaponFlowCache: las listas se dejarán vacías. " +
                "Ejecuta antes las opciones 8 y 9.",
                flow);
        }

        SerializedObject serialized = new SerializedObject(flow);

        SetObject(serialized, "levelUpUI", panel);
        SetObject(serialized, "runDirector", director);
        SetObject(serialized, "enemySpawner", spawner);
        SetObject(serialized, "weaponController", weapons);
        SetBool(serialized, "enableStartingWeaponChoice", true);
        SetBool(serialized, "pauseSpawnerDuringChoice", true);
        SetBool(serialized, "enableBossReward", true);
        SetBool(serialized, "logFlow", true);

        if (cache != null)
        {
            SetUpgradeArrayProperty(serialized, "startingWeaponUpgrades", cache.startingWeapons);
            SetUpgradeArrayProperty(serialized, "bossRewardUpgrades", cache.bossRewards);
        }

        serialized.ApplyModifiedProperties();
        EditorUtility.SetDirty(flow);
        MarkActiveSceneDirty();

        Selection.activeObject = flow;
        EditorGUIUtility.PingObject(flow);

        int startingCount = cache != null ? cache.startingWeapons.Length : 0;
        int bossCount = cache != null ? cache.bossRewards.Length : 0;

        Debug.Log(
            $"<color=green>[GameFeel] Flujo de armas cableado en 'Managers' | " +
            $"Armas iniciales: {startingCount} | Recompensas de jefe: {bossCount}</color>",
            flow);
    }

    /// <summary>Vuelca un array de assets del caché al array serializado del componente.</summary>
    private static void SetUpgradeArrayProperty(SerializedObject serialized, string field, UpgradeDataSO[] items)
    {
        SerializedProperty array = serialized.FindProperty(field);

        if (array == null) return;

        int count = items != null ? items.Length : 0;
        array.arraySize = count;

        for (int i = 0; i < count; i++)
        {
            array.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
        }
    }

    /// <summary>Escribe una lista de mejoras en el asset WeaponFlowCache y lo marca sucio.</summary>
    private static void CacheWeaponFlowList(string field, List<UpgradeDataSO> items)
    {
        EnsureFolder(WeaponFolder);

        WeaponFlowCache cache = LoadOrCreate<WeaponFlowCache>($"{WeaponFolder}/SO_WeaponFlowCache.asset");

        SerializedObject serialized = new SerializedObject(cache);
        SerializedProperty array = serialized.FindProperty(field);

        if (array != null)
        {
            array.arraySize = items.Count;

            for (int i = 0; i < items.Count; i++)
            {
                array.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
            }
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(cache);
    }

    /// <summary>Carga (o crea) uno de los assets de arma secundaria por nombre de archivo.</summary>
    private static WeaponDataSO LoadOrCreateWeaponAsset(string fileName)
    {
        EnsureFolder(WeaponFolder);
        return LoadOrCreate<WeaponDataSO>($"{WeaponFolder}/{fileName}.asset");
    }

    /// <summary>
    /// El arma de pulso base (el "Destello de Luz" que ya usa PlayerAttack). Se reutiliza SU
    /// asset en vez de crear un duplicado: si el pulso se copiara, una mejora aplicada a uno
    /// no tocaría el arma que el jugador lleva realmente equipada.
    /// </summary>
    private static WeaponDataSO LoadPlayerPulseWeapon()
    {
        // El asset original vive en la raíz de ScriptableObject, no en Weapon/.
        WeaponDataSO pulse = AssetDatabase.LoadAssetAtPath<WeaponDataSO>(
            "Assets/Scripts/ScriptableObject/SO_DestelloLuzData.asset");

        if (pulse != null) return pulse;

        // Respaldo por búsqueda: si alguien movió el asset, se localiza por arquetipo.
        string[] guids = AssetDatabase.FindAssets("t:WeaponDataSO", new[] { "Assets" });

        for (int i = 0; i < guids.Length; i++)
        {
            WeaponDataSO candidate = AssetDatabase.LoadAssetAtPath<WeaponDataSO>(AssetDatabase.GUIDToAssetPath(guids[i]));

            if (candidate != null && candidate.archetype == WeaponArchetype.Pulse)
            {
                return candidate;
            }
        }

        // Último recurso: uno nuevo, para que la herramienta nunca falle en seco.
        return LoadOrCreate<WeaponDataSO>($"{WeaponFolder}/SO_Weapon_DestelloLuz.asset");
    }
}