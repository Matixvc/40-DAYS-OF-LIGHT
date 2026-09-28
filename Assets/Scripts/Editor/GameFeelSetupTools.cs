using System.Collections.Generic;
using System.IO;
using TMPro;
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
    // Jerarquía final de ScriptableObjects. El singular (Weapon/Upgrade/Modifier) es la antigua;
    // estas constantes apuntan al plural para que TODO el código escriba ya en su destino.
    // AssetDatabase.MoveAsset (en ReorganizeAssetsAndMigrate) traslada lo existente y conserva
    // el GUID, así que ninguna referencia de escena se rompe por el cambio de carpeta.
    private const string WeaponFolder = "Assets/Scripts/ScriptableObject/Weapons";
    private const string UpgradeFolder = "Assets/Scripts/ScriptableObject/Upgrades";
    private const string ModifierFolder = "Assets/Scripts/ScriptableObject/Modifiers";

    private const int VignetteResolution = 256;

    /// <summary>Número de la capa 'Enemy'. Debe coincidir con la que usa PlayerAttack.</summary>
    private const int EnemyLayerNumber = 8;

    [Header("Ruido de Consola")]
    [Tooltip("Al terminar el flujo de 1 clic, limpia la consola del Editor para dejar visible " +
             "solo el informe final. Útil tras varias ejecuciones seguidas.")]
    // Campo de clase (NO static): Unity no serializa campos estáticos, así que un
    // [SerializeField] aquí sería un warning sin efecto. El valor de abajo es el predeterminado
    // y se puede cambiar por código si hace falta.
    private static bool cleanConsoleAtEnd = true;

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
        FixAndSaveUpgradeAssets();
        ReorganizeAssetsAndMigrate();
        SetupWeaponVisuals();
        SetupWeaponsAndController();
        SetupRunWeaponFlow();
        SetupWeaponPanelUI();
        AutoConnectWeaponPanelCards();
        AutoConnectLevelUpCards();
        EnsureWeaponPanelActive();
        FinalizeMvpAudit();
        SceneHealthAuditor.AuditSceneHealth();
        AuditSetup();

        // Cierra el flujo limpiando la consola: tras 18 pasos el panel está lleno de progreso,
        // y lo que el usuario necesita ahora es ver SOLO el informe final. Los warnings y
        // errores previos se pierden, pero el auditor final los resume todos en su informe.
        if (cleanConsoleAtEnd)
        {
            SceneHealthAuditor.CleanConsoleAndSilence();
        }

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

        // SISTEMA NUEVO ACTIVO. Con false el componente quedaba inerte y el combate lo llevaba
        // el PlayerAttack heredado, así que las armas equipadas no se veían ni atacaban.
        // WeaponController.ApplySystemState() desactiva el legacyPulse al activarse, de modo
        // que no hay doble daño: son mutuamente excluyentes por diseño.
        SetBool(serialized, "useNewWeaponSystem", true);

        // Arranca SIN armas: la elige el jugador en el WeaponPanelUI al empezar el Día 1.
        SerializedProperty starting = serialized.FindProperty("startingWeapons");
        if (starting != null) starting.arraySize = 0;

        serialized.ApplyModifiedProperties();
        EditorUtility.SetDirty(controller);

        // Auditoría del Player: PlayerAttack y WeaponController no pueden coexistir activos.
        AuditPlayerScripts(controller, legacyPulse);

        // El UpgradeManager necesita conocer al WeaponController para poder equipar armas.
        bool wired = WireUpgradeManager(controller, runStats, playerHealth);

        MarkActiveSceneDirty();
        Selection.activeObject = controller;
        EditorGUIUtility.PingObject(controller);

        Debug.Log(
            $"<color=green>[GameFeel] WeaponController {(created ? "añadido" : "ya presente")} en '{player.name}'. " +
            $"Sistema nuevo: ACTIVO (PlayerAttack queda desactivado). " +
            $"{(wired ? "UpgradeManager enlazado." : "AVISO: no se encontró UpgradeManager en la escena.")}</color>",
            controller);
    }

    /// <summary>
    /// Audita los scripts de ataque del Player y deja el sistema de armas como único gestor.
    ///
    /// POR QUÉ: el Player arrastra el <see cref="PlayerAttack"/> original (el pulso de luz).
    /// Con el <see cref="WeaponController"/> activo, los dos dispararían a la vez y cada golpe
    /// haría daño doble, además de aparecer dos visuales superpuestos. El sistema nuevo ya
    /// se desactiva a sí mismo en <c>ApplySystemState()</c>, pero solo si el <c>legacyPulse</c>
    /// le está asignado: sin esa referencia, el PlayerAttack sigue vivo.
    ///
    /// Aquí se comprueba y se corrige esa referencia. NO se borra el componente: se conserva
    /// para poder volver al sistema clásico con un clic (y su SFX y configuración siguen vivos).
    /// </summary>
    private static void AuditPlayerScripts(WeaponController controller, PlayerAttack legacyPulse)
    {
        GameObject player = controller.gameObject;

        // 1) Vincular el PlayerAttack al WeaponController: es lo que permite que el sistema
        //    nuevo lo desactive al activarse. Sin esta línea, ambos atacan a la vez.
        SerializedObject controllerSO = new SerializedObject(controller);
        bool pulseLinked = false;

        if (legacyPulse != null)
        {
            SetObject(controllerSO, "legacyPulse", legacyPulse);
            controllerSO.ApplyModifiedProperties();
            EditorUtility.SetDirty(controller);
            pulseLinked = true;
        }

        // 2) Desactivar el PlayerAttack de forma explícita, aunque WeaponController ya lo
        //    haga en runtime: dejarlo apagado en el Inspector evita el frame en el que ambos
        //    systems están vivos (Awake del controller vs. primer Update del attack).
        if (legacyPulse != null && legacyPulse.enabled)
        {
            legacyPulse.enabled = false;
            EditorUtility.SetDirty(legacyPulse);
        }

        // 3) Avisar de cualquier otro script de ataque que compita por el mismo daño.
        List<string> conflicting = new List<string>();
        MonoBehaviour[] behaviours = player.GetComponents<MonoBehaviour>();

        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];

            if (behaviour == null) continue;

            // Ni el WeaponController ni el propio PlayerAttack son conflictos entre sí.
            if (behaviour is WeaponController || behaviour is PlayerAttack) continue;

            string typeName = behaviour.GetType().Name;

            if (typeName.IndexOf("Attack", System.StringComparison.Ordinal) >= 0)
            {
                conflicting.Add(typeName);
            }
        }

        string summary = pulseLinked
            ? "PlayerAttack vinculado y desactivado"
            : "PlayerAttack no encontrado (el sistema nuevo queda como único gestor)";

        if (conflicting.Count > 0)
        {
            Debug.LogWarning(
                $"[GameFeel] El Player tiene {conflicting.Count} script(s) de ataque potencialmente " +
                $"duplicado(s): {string.Join(", ", conflicting)}. Revisa que no apliquen daño " +
                "además del WeaponController.",
                player);
        }

        Debug.Log($"<color=green>[GameFeel] Auditoría del Player: {summary}.</color>", player);
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
    /// <summary>
    /// AUDITORÍA LEGADA del cableado de Game Feel.
    ///
    /// Nota de organización: la auditoría completa vive ahora en
    /// <c>40 Days of Light &gt; Diagnostic &gt; 1. 🔍 Auditor de Salud e IA</c>, que es
    /// más completa y no modifica nada. Esta opción se conserva porque valida puntos
    /// concretos de juice (hit-stop, viñeta, assets de arma) que el auditor nuevo no cubre.
    /// </summary>
    [MenuItem(MenuRoot + "7. 🎮 Auditar cableado de Game Feel", false, 7)]
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

        // --- Armas: sistema activo y con visuales ---
        PlayerController auditPlayer = Object.FindAnyObjectByType<PlayerController>(FindObjectsInactive.Include);

        if (auditPlayer != null)
        {
            WeaponController auditWeapons = auditPlayer.GetComponent<WeaponController>();

            if (auditWeapons == null)
            {
                problems.Add("Falta WeaponController en el Player. Ejecuta la opción 4.");
            }
            else
            {
                notes.Add("WeaponController presente en el Player.");

                if (!auditWeapons.UseNewWeaponSystem)
                {
                    problems.Add(
                        "El sistema de armas nuevo está DESACTIVADO: el combate sigue con PlayerAttack " +
                        "y las armas equipadas no atacan ni se ven. Ejecuta la opción 4.");
                }
                else
                {
                    PlayerAttack auditPulse = auditPlayer.GetComponent<PlayerAttack>();

                    if (auditPulse != null && auditPulse.enabled)
                    {
                        problems.Add(
                            "PlayerAttack sigue ACTIVO junto al sistema nuevo: ambos harían daño. " +
                            "Ejecuta la opción 4 para desactivarlo.");
                    }
                    else
                    {
                        notes.Add("PlayerAttack desactivado (sin doble daño).");
                    }
                }
            }
        }

        // Cada arquetipo debe tener un visualPrefab propio: sin él, todas las armas caen en la
        // esfera gris de respaldo y son indistinguibles entre sí.
        int missingVisuals = CountWeaponDataWithoutVisual();

        if (missingVisuals > 0)
        {
            problems.Add($"{missingVisuals} WeaponDataSO sin visualPrefab. Ejecuta la opción 13.");
        }
        else
        {
            notes.Add("Todas las armas tienen visual asignado.");
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
    [MenuItem(MenuRoot + "10. Reparar y guardar assets de mejora", false, 70)]
    private static void FixAndSaveUpgradeAssets()
    {
        string[] guids = AssetDatabase.FindAssets("t:UpgradeDataSO");

        if (guids == null || guids.Length == 0)
        {
            Debug.LogWarning("[GameFeel] No se encontró ningún UpgradeDataSO en Assets/.");
            return;
        }

        HashSet<string> usedTitles = new HashSet<string>();
        int fixedCount = 0;
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

            // Las cartas de ARMA INICIAL y de JEFE ya tienen nombre de diseño: son la
            // primera decisión del jugador y su nombre es intencional, no técnico.
            bool isSpecial = asset.name.StartsWith("SO_Start_", System.StringComparison.Ordinal)
                          || asset.name.StartsWith("SO_Boss_", System.StringComparison.Ordinal);

            string title = isSpecial
                ? asset.upgradeName
                : BuildUniqueTitle(BuildTitleFor(asset), usedTitles);

            if (!isSpecial && !string.Equals(title, asset.upgradeName, System.StringComparison.Ordinal))
            {
                renamed++;
            }

            usedTitles.Add(title);

            SetString(asset, "upgradeName", title);
            SetString(asset, "description", isSpecial ? asset.description : BuildDescriptionFor(asset));
            SetEnum(asset, "rarity", ResolveRarityFor(asset));

            // CRÍTICO: sin SetDirty el cambio vive solo en memoria y NUNCA se escribe en el
            // .asset. Es la razón por la que las mejoras 'no se guardan' al ejecutar la
            // herramienta: se veían bien en el Inspector y desaparecían al reabrir el asset.
            EditorUtility.SetDirty(asset);
            fixedCount++;
        }

        // Orden de escritura importante: SaveAssets vacía a disco, Refresh hace que el
        // AssetDatabase vuelva a indexar. Refresh SIN SaveAssets previo perdería los cambios.
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            $"<color=green>[GameFeel] {fixedCount} mejoras reparadas y GUARDAS en disco " +
            $"({renamed} títulos nuevos). Assets sincronizados con Refresh().</color>");
    }

    /// <summary>
    /// Título legible deducido del efecto real de la mejora. Se usa un mapa por estadística
    /// (y no el nombre del enum) porque "IncreaseMaxHealth" no le dice nada a un jugador.
    ///
    /// Los nombres son temáticos a propósito: "Vigor Inquebrantable" se recuerda y se decide
    /// mucho mejor que "+25 vida". Una carta de mejora es una decisión de build, y una build
    /// se elige por fantasía, no por porcentaje.
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
                    case StatType.IncreaseMaxHealth:       return "Vigor Inquebrantable";
                    case StatType.IncreaseArmor:           return "Piel de Obsidiana";
                    case StatType.IncreaseCritChance:      return "Ojo del Halcón";
                    case StatType.IncreaseCritDamage:      return "Impacto Devastador";
                    case StatType.HealthRegen:             return "Cuerpo Imortal";
                    case StatType.IncreaseMoveSpeed:       return "Pasos del Viento";
                    case StatType.IncreaseDamage:          return "Furia Celestial";
                    case StatType.IncreaseRange:           return "Alcance del Alba";
                    case StatType.IncreasePickupRadius:    return "Instinto de Caza";
                    case StatType.IncreaseXPGain:          return "Sed de Luz";
                    case StatType.IncreaseProjectileCount: return "Lluvia de Lanzas";
                    case StatType.IncreaseAreaSize:        return "Aura Sagrada";
                    case StatType.IncreaseDuration:        return "Eternidad";
                    case StatType.DecreaseAttackInterval:  return "Cadencia Letal";
                    case StatType.DecreaseCooldown:        return "Reflejo del Cielo";
                }
            }
        }

        // Sin modificadores ni arma: no hay nombre temático deducible. El asset ya trae el
        // que el diseño le dio; no se inventa uno que lo contradiga.
        return string.IsNullOrWhiteSpace(asset.upgradeName) ? "Mejora" : asset.upgradeName;
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
    /// la frase del arma cuando la hay.
    /// </summary>
    private static string BuildDescriptionFor(UpgradeDataSO asset)
    {
        string modifierSummary = asset.BuildModifierSummary();

        if (asset.grantsWeapon != null)
        {
            string weaponName = asset.grantsWeapon.weaponName;
            return string.IsNullOrWhiteSpace(modifierSummary)
                ? $"Obtienes el arma {weaponName}."
                : $"Obtienes {weaponName}. {modifierSummary}";
        }

        // Sin arma, el resumen de modificadores ES la descripción: ya sale formateado
        // ("+20% Daño", "x1.5 Área") y es lo que el jugador debe leer.
        return string.IsNullOrWhiteSpace(modifierSummary)
            ? "Mejora tus estadísticas."
            : modifierSummary;
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

        // Sin modificadores ni arma: la carta no tiene efecto, así que se marca como común.
        // (Ya no se puede leer 'value': ese campo del sistema v1 se eliminó.)
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

    // ==================================================================
    // 11. CABLEADO DEL WEAPON PANEL UI
    // ==================================================================

    /// <summary>
    /// Busca el GameObject 'WeaponPanelUI' en la escena, le añade el componente
    /// <see cref="WeaponPanelUI"/> y le cablea las cartas automáticamente.
    ///
    /// El GameObject se duplica a mano desde 'LevelUpPanel', así que sus hijos son Imgs vacías
    /// y no cartas jugables. Aquí se clona una carta real del panel de nivel dentro de él, de
    /// modo que el panel sea jugable sin trabajo manual de diseño.
    /// </summary>
    [MenuItem(MenuRoot + "12. Cablear WeaponPanelUI (2 opciones)", false, 42)]
    private static void SetupWeaponPanelUI()
    {
        if (Application.isPlaying)
        {
            Debug.LogWarning("[GameFeel] Sal de Play Mode antes de modificar la escena.");
            return;
        }

        GameObject panelObject = FindGameObjectByName("WeaponPanelUI");

        if (panelObject == null)
        {
            Debug.LogWarning(
                "[GameFeel] No se encontró el GameObject 'WeaponPanelUI'. " +
                "Duplícalo desde 'LevelUpPanel' y vuelve a ejecutar la opción 12.");
            return;
        }

        WeaponPanelUI panel = panelObject.GetComponent<WeaponPanelUI>();
        bool created = false;

        if (panel == null)
        {
            panel = panelObject.AddComponent<WeaponPanelUI>();
            created = true;
        }
        else
        {
            Debug.Log("[GameFeel] 'WeaponPanelUI' ya tenía el componente WeaponPanelUI.", panel);
        }

        CanvasGroup canvasGroup = panelObject.GetComponent<CanvasGroup>();

        if (canvasGroup == null)
        {
            Debug.LogWarning(
                "[GameFeel] 'WeaponPanelUI' no tiene CanvasGroup. WeaponPanelUI.Awake lo busca y lo " +
                "avisa, pero conviene tenerlo visible en el Inspector para poder editarlo.", panel);
        }

        // Catálogo: SOLO upgrades que otorgan un arma, en todo Assets/.
        List<UpgradeDataSO> catalog = CollectWeaponUpgrades();

        // Cartas: se reutilizan las que haya o se clonan de una real del panel de nivel.
        UpgradeCardUI[] cards = ResolveOrCreateCards(panelObject, 2);

        SerializedObject serialized = new SerializedObject(panel);

        SetObject(serialized, "canvasGroup", canvasGroup);
        SetObject(serialized, "panelRect", panelObject.transform as RectTransform);
        SetCardArray(serialized, "cards", cards);

        SerializedProperty catalogProp = serialized.FindProperty("weaponUpgrades");

        if (catalogProp != null)
        {
            catalogProp.arraySize = catalog.Count;

            for (int i = 0; i < catalog.Count; i++)
            {
                catalogProp.GetArrayElementAtIndex(i).objectReferenceValue = catalog[i];
            }
        }

        SetFloat(serialized, "fadeDuration", 0.25f);
        SetBool(serialized, "logSelections", true);

        serialized.ApplyModifiedProperties();
        EditorUtility.SetDirty(panel);

        LinkWeaponPanelToFlow(panel);

        MarkActiveSceneDirty();
        Selection.activeObject = panelObject;
        EditorGUIUtility.PingObject(panelObject);

        Debug.Log(
            $"<color=green>[GameFeel] WeaponPanelUI {(created ? "añadido" : "ya presente")} | " +
            $"Cartas: {cards.Length} | Catálogo de armas: {catalog.Count}</color>",
            panel);
    }

    /// <summary>Busca un GameObject por nombre exacto en toda la escena, incluidos los inactivos.</summary>
    private static GameObject FindGameObjectByName(string targetName)
    {
        GameObject[] roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();

        for (int i = 0; i < roots.Length; i++)
        {
            Transform[] all = roots[i].GetComponentsInChildren<Transform>(true);

            for (int j = 0; j < all.Length; j++)
            {
                if (all[j] != null && all[j].name == targetName)
                {
                    return all[j].gameObject;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Recolecta todos los <see cref="UpgradeDataSO"/> que otorgan un arma. El panel de
    /// elección de armas solo debe ofrecer cartas de arma: una carta de "+20% daño" entre dos
    /// armas rompe la promesa de la pantalla.
    /// </summary>
    private static List<UpgradeDataSO> CollectWeaponUpgrades()
    {
        List<UpgradeDataSO> result = new List<UpgradeDataSO>();
        string[] guids = AssetDatabase.FindAssets("t:UpgradeDataSO");

        for (int i = 0; i < guids.Length; i++)
        {
            UpgradeDataSO asset = AssetDatabase.LoadAssetAtPath<UpgradeDataSO>(AssetDatabase.GUIDToAssetPath(guids[i]));

            if (asset != null && asset.grantsWeapon != null)
            {
                result.Add(asset);
            }
        }

        // Orden estable: el catálogo no debe reordenarse entre ejecuciones de la herramienta.
        result.Sort((a, b) => string.CompareOrdinal(AssetDatabase.GetAssetPath(a), AssetDatabase.GetAssetPath(b)));

        return result;
    }

    /// <summary>
    /// Devuelve las cartas del panel, creándolas si hace falta.
    ///
    /// Si el GameObject ya tiene <see cref="UpgradeCardUI"/> (porque el usuario ya las dejó a
    /// mano) se respetan tal cual. Si no, se clonan desde el panel de nivel para heredar el
    /// arte, los textos y el sonido de clic, en vez de inventar una carta pelada.
    /// </summary>
    private static UpgradeCardUI[] ResolveOrCreateCards(GameObject panelObject, int wanted)
    {
        UpgradeCardUI[] existing = panelObject.GetComponentsInChildren<UpgradeCardUI>(true);

        if (existing != null && existing.Length > 0)
        {
            return existing;
        }

        // Fuente de la plantilla: la primera carta real del panel de nivel.
        LevelUpUI levelUp = Object.FindAnyObjectByType<LevelUpUI>(FindObjectsInactive.Include);

        if (levelUp == null)
        {
            Debug.LogWarning("[GameFeel] No se encontró LevelUpUI para clonar cartas: el panel quedará sin cartas jugables.");
            return new UpgradeCardUI[0];
        }

        UpgradeCardUI[] template = levelUp.GetComponentsInChildren<UpgradeCardUI>(true);

        if (template == null || template.Length == 0)
        {
            Debug.LogWarning("[GameFeel] El LevelUpUI no tiene UpgradeCardUI: no hay plantilla que clonar.");
            return new UpgradeCardUI[0];
        }

        List<UpgradeCardUI> created = new List<UpgradeCardUI>(wanted);
        Transform parent = panelObject.transform;

        for (int i = 0; i < wanted; i++)
        {
            UpgradeCardUI source = template[i % template.Length];
            UpgradeCardUI clone = Object.Instantiate(source, parent, false);

            clone.name = $"WeaponCard_{i + 1}";

            // El WeaponPanelUI gestiona el cierre con su propio callback: la carta no debe
            // intentar cerrar un LevelUpUI que no participa en este flujo.
            clone.OnSelectedOverride = null;

            created.Add(clone);
        }

        Debug.Log($"[GameFeel] {created.Count} cartas clonadas desde el LevelUpUI dentro de 'WeaponPanelUI'.");

        return created.ToArray();
    }

    /// <summary>Escribe un array de UpgradeCardUI en el campo serializado indicado.</summary>
    private static void SetCardArray(SerializedObject serialized, string field, UpgradeCardUI[] items)
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

    // ==================================================================
    // 14. AUDITORÍA FINAL DEL MVP
    // ==================================================================

    /// <summary>Prefab y material de arte dedicados en Assets/Materials/Visuals/.</summary>
    private const string ArtVisualsFolder = "Assets/Materials/Visuals";

    /// <summary>
    /// Auditoría final del catálogo de armas: deja los 4 ScriptableObjects oficiales pulidos,
    /// sus materiales URP sincronizados, el WeaponFlowCache limpio y el Player sin doble daño.
    ///
    /// POR QUÉ ESTE PASO EXISTE: cada herramienta anterior corrige una cosa y otra la deshace
    /// o la deja a medias. Este es el cierre de contrato: no crea nada nuevo, solo verifica y
    /// reescribe sobre el estado real. Es idempotente, así que se puede ejecutar tantas veces
    /// como haga falta hasta que la auditoría salga en verde.
    ///
    /// Corrige tres fallos reales detectados en el proyecto:
    ///   1. Los 4 assets tenían <c>weaponName = "Destello de Luz"</c>: cuatro cartas idénticas.
    ///   2. Aura y Pulso estaban en <c>_Surface = 0</c> (opaco) pese a tener alpha 0.35/0.45,
    ///      así que se veían como un bloque opaco en lugar de una luz.
    ///   3. El WeaponFlowCache conservaba referencias a assets ya borrados (entradas nulas).
    /// </summary>
    [MenuItem(MenuRoot + "15. 🔧 Auditoría y Reparación Final del MVP", false, 15)]
    private static void FinalizeMvpAudit()
    {
        if (Application.isPlaying)
        {
            Debug.LogWarning("[GameFeel] Sal de Play Mode antes de modificar la escena.");
            return;
        }

        List<string> problems = new List<string>();
        int fixedCount = 0;

        fixedCount += FinalizeWeapon("SO_Weapon_PulsoReluciente", "Pulso Reluciente", WeaponArchetype.Pulse, problems);
        fixedCount += FinalizeWeapon("SO_Weapon_OrbitaSagrada", "Órbita Sagrada", WeaponArchetype.Orbit, problems);
        fixedCount += FinalizeWeapon("SO_Weapon_LanzaCelestial", "Lanza Celestial", WeaponArchetype.Projectile, problems);
        fixedCount += FinalizeWeapon("SO_Weapon_AuraPurificacion", "Aura de Purificación", WeaponArchetype.Aura, problems);

        // Catálogo limpio: SOLO las 4 oficiales, sin restos de assets borrados.
        if (!RebuildCleanCatalog(problems))
        {
            problems.Add("No se pudo reconstruir el catálogo de armas.");
        }

        // Sincronización de materiales URP.
        if (!SyncWeaponMaterials(problems))
        {
            problems.Add("No se pudieron sincronizar los materiales URP de las armas.");
        }

        // Estado de combate del Player.
        VerifyPlayerCombatState(problems);

        // PERSISTENCIA TOTAL: el orden importa. SaveAssets vuelca a disco y Refresh reindexa;
        // hacer Refresh antes de SaveAssets perdería los cambios del frame.
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        MarkActiveSceneDirty();

        Debug.Log(
            $"<color=cyan>──── Auditoría final del MVP: {fixedCount} campo(s) corregido(s), " +
            $"{problems.Count} problema(s) pendiente(s) ────</color>");

        for (int i = 0; i < problems.Count; i++)
        {
            Debug.LogWarning("  " + problems[i]);
        }

        if (problems.Count == 0)
        {
            Debug.Log(
                "<color=green>[GameFeel] Catálogo listo: 4 armas oficiales con nombre único, " +
                "material URP correcto y Player sin doble daño.</color>");
        }
    }

    /// <summary>
    /// Verifica y reescribe un WeaponDataSO oficial. Devuelve cuántos campos corrigió.
    ///
    /// No crea el asset si falta: si falta, se reporta como problema en vez de inventarse
    /// un arma durante la auditoría, porque un asset improvisado en el cierre sería peor que
    /// un aviso claro de que hay que ejecutar antes el paso de creación.
    /// </summary>
    private static int FinalizeWeapon(string fileName, string expectedName, WeaponArchetype archetype, List<string> problems)
    {
        string path = $"{WeaponFolder}/{fileName}.asset";
        WeaponDataSO weapon = AssetDatabase.LoadAssetAtPath<WeaponDataSO>(path);

        if (weapon == null)
        {
            problems.Add($"Falta el asset oficial '{fileName}'. Ejecuta antes la opción 14.");
            return 0;
        }

        int fixedFields = 0;

        // 1) Nombre único. Se compara ANTES de escribir para contar solo lo que cambia.
        if (!string.Equals(weapon.weaponName, expectedName, System.StringComparison.Ordinal))
        {
            Debug.Log($"[GameFeel] '{fileName}': nombre '{weapon.weaponName}' → '{expectedName}'.");

            SetString(weapon, "weaponName", expectedName);
            fixedFields++;
        }

        // 2) Arquetipo: garantiza que el comportamiento en runtime sea el esperado.
        if (weapon.archetype != archetype)
        {
            SetEnum(weapon, "archetype", archetype);
            fixedFields++;
        }

        // 3) Visual dedicado de Materials/Visuals/ (arte real del proyecto, no primitivas).
        GameObject artVisual = LoadArtVisual(archetype);

        if (artVisual != null && weapon.visualPrefab != artVisual)
        {
            SetObject(weapon, "visualPrefab", artVisual);
            fixedFields++;
        }

        // 4) PERSISTENCIA del asset. SetDirty es lo que hace que el cambio llegue al .asset:
        //    sin esta llamada el nombre se ve correcto en memoria y vuelve al anterior al
        //    recargar el dominio. Es el fallo que dejó los 4 assets con el mismo nombre.
        EditorUtility.SetDirty(weapon);
        fixedFields++;

        return fixedFields;
    }

    /// <summary>Busca el prefab de arte dedicado del arquetipo en Materials/Visuals/.</summary>
    private static GameObject LoadArtVisual(WeaponArchetype archetype)
    {
        string prefabName = archetype == WeaponArchetype.Pulse ? "WeaponPulseRing"
            : archetype == WeaponArchetype.Orbit ? "WeaponOrb"
            : archetype == WeaponArchetype.Projectile ? "WeaponProjectile"
            : "WeaponAuraRing";

        return AssetDatabase.LoadAssetAtPath<GameObject>($"{ArtVisualsFolder}/{prefabName}.prefab");
    }

    /// <summary>Devuelve el material dedicado del arquetipo, si existe en el proyecto.</summary>
    private static Material LoadArtMaterial(WeaponArchetype archetype)
    {
        string materialName = archetype == WeaponArchetype.Pulse ? "WeaponPulseRingMat"
            : archetype == WeaponArchetype.Orbit ? "WeaponOrbMat"
            : archetype == WeaponArchetype.Projectile ? "WeaponProjectileMat"
            : "WeaponAuraRingMat";

        return AssetDatabase.LoadAssetAtPath<Material>($"{ArtVisualsFolder}/{materialName}.mat");
    }

    /// <summary>
    /// Reconstruye el WeaponFlowCache con EXCLUSIVAMENTE las 4 armas oficiales, purgando
    /// cualquier referencia a assets ya borrados.
    ///
    /// POR QUÉ PURGAR: un asset borrado deja su entrada en el array como null. WeaponPanelUI
    /// ofrecería entonces una carta vacía, y el panel no se llenaría bien. El array se
    /// reescribe entero en vez de intentar quitar entradas sueltas: es más simple y no deja
    /// huecos a mitad del array.
    /// </summary>
    private static bool RebuildCleanCatalog(List<string> problems)
    {
        WeaponFlowCache cache = AssetDatabase.LoadAssetAtPath<WeaponFlowCache>($"{WeaponFolder}/SO_WeaponFlowCache.asset");

        if (cache == null)
        {
            problems.Add("Falta WeaponFlowCache. Ejecuta antes la opción 11.");
            return false;
        }

        // Se purga lo muerto para que quede constancia de lo que había antes de limpiar.
        int purged = 0;

        for (int i = 0; i < cache.startingWeapons.Length; i++)
        {
            if (cache.startingWeapons[i] == null) purged++;
        }

        for (int i = 0; i < cache.bossRewards.Length; i++)
        {
            if (cache.bossRewards[i] == null) purged++;
        }

        if (purged > 0)
        {
            Debug.Log($"[GameFeel] WeaponFlowCache: {purged} referencia(s) a assets borrados se purgan.");
        }

        List<UpgradeDataSO> catalog = BuildCatalogUpgrades();
        List<UpgradeDataSO> bossPicks = BuildBossPicks(catalog);

        SerializedObject serialized = new SerializedObject(cache);
        SetUpgradeArrayProperty(serialized, "startingWeapons", catalog.ToArray());
        SetUpgradeArrayProperty(serialized, "bossRewards", bossPicks.ToArray());
        serialized.ApplyModifiedPropertiesWithoutUndo();

        // PERSISTENCIA del cache: sin SetDirty el array reescrito se pierde al recargar.
        EditorUtility.SetDirty(cache);

        Debug.Log($"[GameFeel] Catálogo oficial: {catalog.Count} arma(s) al inicio, {bossPicks.Count} de jefe.");

        return catalog.Count > 0;
    }

    /// <summary>
    /// Catálogo de upgrades de arma: una por <see cref="WeaponDataSO"/> distinto, sin
    /// duplicar y sin referencias muertas.
    /// </summary>
    private static List<UpgradeDataSO> BuildCatalogUpgrades()
    {
        List<UpgradeDataSO> catalog = new List<UpgradeDataSO>();
        string[] guids = AssetDatabase.FindAssets("t:UpgradeDataSO", new[] { "Assets" });

        for (int i = 0; i < guids.Length; i++)
        {
            UpgradeDataSO upgrade = AssetDatabase.LoadAssetAtPath<UpgradeDataSO>(AssetDatabase.GUIDToAssetPath(guids[i]));

            // Filtro de asset muerto: una upgrade cuyo grantsWeapon se borró se descarta, así
            // que una referencia nula nunca llega al panel.
            if (upgrade == null || upgrade.grantsWeapon == null) continue;

            bool alreadyPresent = false;

            for (int j = 0; j < catalog.Count; j++)
            {
                if (catalog[j].grantsWeapon == upgrade.grantsWeapon)
                {
                    alreadyPresent = true;
                    break;
                }
            }

            if (!alreadyPresent)
            {
                catalog.Add(upgrade);
            }
        }

        return catalog;
    }

    /// <summary>
    /// Recompensas de jefe: prefiere las evoluciones (<c>SO_Boss_</c>) y, si no hay, cae al
    /// catálogo inicial. Nunca devuelve vacío si hay armas: un jefe que no premia nada
    /// rompe la promesa de la partida.
    /// </summary>
    private static List<UpgradeDataSO> BuildBossPicks(List<UpgradeDataSO> catalog)
    {
        List<UpgradeDataSO> bossPicks = new List<UpgradeDataSO>();
        string[] guids = AssetDatabase.FindAssets("t:UpgradeDataSO", new[] { "Assets" });

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);

            if (!path.Contains("SO_Boss_")) continue;

            UpgradeDataSO upgrade = AssetDatabase.LoadAssetAtPath<UpgradeDataSO>(path);

            if (upgrade != null && upgrade.grantsWeapon != null)
            {
                bossPicks.Add(upgrade);
            }
        }

        if (bossPicks.Count == 0)
        {
            // Sin evoluciones, el catálogo inicial sirve como premio: mejor una carta
            // repetida que un jefe que no otorga nada.
            bossPicks.AddRange(catalog);
        }

        return bossPicks;
    }

    /// <summary>
    /// Sincroniza los materiales URP dedicados de las armas con su modo de render.
    ///
    /// EL FALLO QUE CORRIGE: Aura y Pulso tenían el alpha correcto en <c>_BaseColor</c> (0.35 y
    /// 0.45) pero estaban en <c>_Surface = 0</c>, es decir MODO OPAQUE. En URP el canal alfa
    /// solo se respeta si el material está en modo transparente: con _Surface = 0 el aura se
    /// veía como un disco opaco, que es el "bloque negro" que aparecía en pantalla.
    ///
    /// Órbita y Lanza sí deben quedar opacas: son sólidos que se leen por silueta, y un orbe
    /// translúcido se perdería contra el fondo.
    /// </summary>
    private static bool SyncWeaponMaterials(List<string> problems)
    {
        WeaponArchetype[] archetypes =
        {
            WeaponArchetype.Pulse,
            WeaponArchetype.Orbit,
            WeaponArchetype.Projectile,
            WeaponArchetype.Aura
        };

        int synced = 0;

        for (int i = 0; i < archetypes.Length; i++)
        {
            WeaponArchetype archetype = archetypes[i];
            Material material = LoadArtMaterial(archetype);

            if (material == null)
            {
                problems.Add($"Falta el material dedicado de {archetype} en '{ArtVisualsFolder}'.");
                continue;
            }

            // Pulso y Aura son luz: necesitan transparencia para verse como luz.
            // Órbita y Lanza son sólidos: opacos para leerse por silueta.
            bool needsTransparency = archetype == WeaponArchetype.Pulse || archetype == WeaponArchetype.Aura;

            if (needsTransparency)
            {
                ConfigureTransparent(material);
            }
            else
            {
                ConfigureOpaque(material);
            }

            // PERSISTENCIA del material: sin SetDirty el cambio se pierde al recargar.
            EditorUtility.SetDirty(material);
            synced++;
        }

        if (synced == 0)
        {
            return false;
        }

        Debug.Log($"[GameFeel] {synced} material(es) URP sincronizados (Pulso y Aura en modo transparente).");

        return true;
    }

    /// <summary>
    /// Fuerza el material a modo TRANSPARENTE en URP.
    ///
    /// Los valores son los que usa internamente Lit/Unlit de URP: _Surface = 1 (Transparent),
    /// SrcAlpha/OneMinusSrcAlpha, ZWrite off y renderQueue 3000. Se escriben también las
    /// keywords porque el material puede haberse creado desde versiones distintas del shader
    /// y hay que cubrir ambos caminos de configuración.
    /// </summary>
    private static void ConfigureTransparent(Material material)
    {
        if (material == null) return;

        if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_Blend")) material.SetFloat("_Blend", 0f);
        if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
        if (material.HasProperty("_AlphaClip")) material.SetFloat("_AlphaClip", 0f);
        if (material.HasProperty("_Cull")) material.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
        if (material.HasProperty("_QueueOffset")) material.SetFloat("_QueueOffset", 0f);

        material.SetOverrideTag("RenderType", "Transparent");
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.DisableKeyword("_ALPHATEST_ON");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        material.DisableKeyword("_ALPHAMODULATE_ON");

        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
    }

    /// <summary>Fuerza el material a modo OPAQUE, para sólidos que deben leerse por silueta.</summary>
    private static void ConfigureOpaque(Material material)
    {
        if (material == null) return;

        if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 0f);
        if (material.HasProperty("_AlphaClip")) material.SetFloat("_AlphaClip", 0f);
        if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 1f);
        if (material.HasProperty("_Cull")) material.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Back);

        material.SetOverrideTag("RenderType", "Opaque");
        material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.DisableKeyword("_ALPHATEST_ON");

        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;
    }

    /// <summary>
    /// Verifica el estado de combate del Player: sistema de armas nuevo activo y PlayerAttack
    /// desactivado. Ambas cosas a la vez provocarían daño doble, así que se comprueban y
    /// corrigen como un par atómico.
    /// </summary>
    private static void VerifyPlayerCombatState(List<string> problems)
    {
        PlayerController player = Object.FindAnyObjectByType<PlayerController>(FindObjectsInactive.Include);

        if (player == null)
        {
            problems.Add("No se encontró el Player en la escena.");
            return;
        }

        WeaponController controller = player.GetComponent<WeaponController>();

        if (controller == null)
        {
            problems.Add("El Player no tiene WeaponController. Ejecuta la opción 4.");
            return;
        }

        PlayerAttack legacyPulse = player.GetComponent<PlayerAttack>();

        if (legacyPulse == null)
        {
            problems.Add("El Player no tiene PlayerAttack: revisa que el combate tenga una sola fuente de daño.");
            return;
        }

        // 1) Vincular el pulso legado: sin esta referencia el WeaponController no puede
        //    desactivarlo por sí mismo en runtime.
        SerializedObject controllerSO = new SerializedObject(controller);
        SetObject(controllerSO, "legacyPulse", legacyPulse);
        SetBool(controllerSO, "useNewWeaponSystem", true);
        controllerSO.ApplyModifiedProperties();

        // 2) Desactivar el pulso heredado: evita el frame en el que ambos sistemas están vivos
        //    (el Awake del controller corre después del primer Update del PlayerAttack).
        if (legacyPulse.enabled)
        {
            legacyPulse.enabled = false;
            EditorUtility.SetDirty(legacyPulse);
        }

        // 3) Persistencia de escena: el Player es un objeto de escena, no un asset, así que
        //    SetDirty + escena sucia bastan (Ctrl+S lo persiste).
        EditorUtility.SetDirty(controller);
        EditorUtility.SetDirty(player);
        EditorUtility.SetDirty(legacyPulse);

        Debug.Log(
            "[GameFeel] Player: useNewWeaponSystem=true, PlayerAttack vinculado y desactivado (sin daño doble).",
            player);
    }

    private const string PulseWeaponFile = "SO_Weapon_PulsoReluciente";
    private const string OrbitWeaponFile = "SO_Weapon_OrbitaSagrada";
    private const string ProjectileWeaponFile = "SO_Weapon_LanzaCelestial";
    private const string AuraWeaponFile = "SO_Weapon_AuraPurificacion";

    /// <summary>
    /// Crea los 4 <see cref="WeaponDataSO"/> de producción (uno por arquetipo), les asigna
    /// nombre, stats y visual, y los engancha como CATÁLOGO del WeaponController.
    ///
    /// POR QUÉ SOBRESCRIBIR Y NO RESPETAR LO EXISTENTE: el proyecto tenía tres assets de arma
    /// con <c>weaponName = "Destello de Luz"</c> los tres. Con nombres idénticos el jugador ve
    /// tres cartas iguales en el panel de armas y elige a ciegas: es un fallo de UX, no un
    /// detalle de texto. Aquí se fuerza un nombre único y temático por arquetipo, siempre.
    ///
    /// Los 4 arquetipos reciben stats en rangos distintos a propósito: el jugador debe notar
    /// que la Lanza hace un golpe fuerte y espaciado, y el Aura hace mucho pero leve y continuo.
    ///
    /// IDEMPOTENTE: si el asset ya existe se corrige, no se duplica.
    /// </summary>
    [MenuItem(MenuRoot + "14. Poblar y configurar las 4 armas", false, 52)]
    private static void SetupWeaponsAndController()
    {
        if (Application.isPlaying)
        {
            Debug.LogWarning("[GameFeel] Sal de Play Mode antes de modificar la escena.");
            return;
        }

        EnsureFolder(WeaponFolder);

        // El género es lo que decide el comportamiento: cada uno va a su arquetipo.
        WeaponDataSO pulse = ConfigureWeapon(
            PulseWeaponFile, "Pulso Reluciente", WeaponArchetype.Pulse,
            "Emite una onda expansiva de luz a tu alrededor. Golpea a TODO lo que esté cerca de " +
            "forma inmediata y constante: mucho daño por segundo en cuerpo a cuerpo, pero no " +
            "alcanza lejos y no tiene dirección.",

            damage: 18f, attackRange: 4.2f, attackInterval: 0.85f, visualScale: 1f);

        WeaponDataSO orbit = ConfigureWeapon(
            OrbitWeaponFile, "Órbita Sagrada", WeaponArchetype.Orbit,
            "Invoca orbes de luz que giran a tu alrededor y golpean por contacto. Daño moderado " +
            "pero permanente y en todas direcciones: cobertura total, muy vulnerable frente a " +
            "enemigos rápidos.",

            damage: 11f, attackRange: 2.4f, attackInterval: 0.45f, visualScale: 1f);

        WeaponDataSO projectile = ConfigureWeapon(
            ProjectileWeaponFile, "Lanza Celestial", WeaponArchetype.Projectile,
            "Dispara lanzas de luz hacia el enemigo más cercano. El golpe más fuerte por impacto " +
            "del juego y a larga distancia, pero es el más lento: si no hay a quién disparar, " +
            "no hace nada.",

            damage: 32f, attackRange: 14f, attackInterval: 1.25f, visualScale: 1f);

        WeaponDataSO aura = ConfigureWeapon(
            AuraWeaponFile, "Aura de Purificación", WeaponArchetype.Aura,
            "Un aura sagrada daña sin parar a todo lo que pisa tu alrededor. El daño por golpe " +
            "es bajo, pero nunca deja de aplicar: la mejor opción contra hordas cerradas.",

            damage: 7f, attackRange: 3.4f, attackInterval: 0.5f, visualScale: 1f);

        // --- Stats específicos por arquetipo que el constructor anterior no cubre ---
        orbit.orbitCount = 3;
        orbit.orbitRadius = 2.4f;
        orbit.orbitDegreesPerSecond = 140f;
        orbit.orbitHitInterval = 0.4f;
        orbit.orbitOrbRadius = 0.42f;

        projectile.projectileCount = 1;
        projectile.projectileSpeed = 20f;
        projectile.projectileLifetime = 1.5f;
        projectile.projectileHitRadius = 0.55f;
        projectile.projectilePierce = 1;
        projectile.projectileSpreadDegrees = 5f;

        aura.auraRadius = 3.4f;
        aura.auraTickInterval = 0.5f;

        pulse.flashColor = new Color(1f, 0.92f, 0.45f, 0.45f);
        orbit.flashColor = new Color(1f, 0.82f, 0.3f, 1f);
        projectile.flashColor = new Color(0.6f, 0.88f, 1f, 1f);
        aura.flashColor = new Color(0.85f, 0.45f, 0.95f, 0.3f);

        // PERSISTENCIA: SetDirty en cada uno + SaveAssets + Refresh al final del lote.
        // Sin SetDirty los cambios viven solo en memoria y desaparecen al recargar el dominio.
        MarkDirty(pulse, orbit, projectile, aura);

        // --- POBLAR EL WEAPONCONTROLLER DEL PLAYER ---
        bool populated = PopulateWeaponController(new List<WeaponDataSO> { pulse, orbit, projectile, aura });

        // --- ASEGURAR QUE EL PANEL DE UI ESTÁ ACTIVO ---
        bool panelActive = EnsureWeaponPanelActive();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        MarkActiveSceneDirty();

        Debug.Log(
            "<color=green>[GameFeel] 4 armas configuradas (Pulso/Órbita/Lanza/Aura) con nombres únicos, " +
            $"stats diferenciadas y visual asignado. WeaponController: {(populated ? "poblado" : "NO ENCONTRADO")}. " +
            $"WeaponPanelUI: {(panelActive ? "activo" : "NO ENCONTRADO")}. Guardado en disco.</color>");
    }

    private const string VisualFolder = "Assets/UI/WeaponVisuals";

    private enum VisualShape
    {
        Sphere,
        Capsule,
        Disc
    }

    /// <summary>
    /// Crea un prefab visual distinto para cada arquetipo y lo asigna a su
    /// <see cref="WeaponDataSO"/>. Sin esto los cuatro arquetipos caen en el
    /// <c>CreatePrimitiveFallback</c> (una esfera gris genérica) y las armas son
    /// indistinguibles: una "Lanza Sagrada" se ve igual que un "Aura Sagrada".
    ///
    /// Cada forma se elige para LEERSE de un vistazo en movimiento:
    ///   - Pulse:      esfera translúcida grande (se expande y desvanece).
    ///   - Orbit:      orbe pequeño y sólido (debe verse girando alrededor).
    ///   - Projectile: cápsula alargada (lee la dirección de vuelo).
    ///   - Aura:       disco plano tumbado (marca el suelo, no flota).
    /// </summary>
    [MenuItem(MenuRoot + "13. Crear y asignar visuales de las armas", false, 51)]
    private static void SetupWeaponVisuals()
    {
        EnsureFolder(VisualFolder);

        GameObject pulseVisual = CreateOrLoadWeaponVisual("Visual_Pulse", VisualShape.Sphere, 0.9f, new Color(1f, 0.92f, 0.45f, 0.35f), true);
        GameObject orbitVisual = CreateOrLoadWeaponVisual("Visual_Orbit", VisualShape.Sphere, 0.28f, new Color(1f, 0.82f, 0.3f, 1f), false);
        GameObject projectileVisual = CreateOrLoadWeaponVisual("Visual_Projectile", VisualShape.Capsule, 0.3f, new Color(0.6f, 0.88f, 1f, 1f), false);
        GameObject auraVisual = CreateOrLoadWeaponVisual("Visual_Aura", VisualShape.Disc, 1f, new Color(0.85f, 0.45f, 0.95f, 0.25f), true);

        AssignVisualToArchetype(WeaponArchetype.Pulse, pulseVisual);
        AssignVisualToArchetype(WeaponArchetype.Orbit, orbitVisual);
        AssignVisualToArchetype(WeaponArchetype.Projectile, projectileVisual);
        AssignVisualToArchetype(WeaponArchetype.Aura, auraVisual);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            "<color=green>[GameFeel] Visuales de armas creados y asignados: " +
            "Pulso (esfera), Órbita (orbe), Lanzas (proyectil) y Aura (disco).</color>");
    }

    /// <summary>
    /// Crea (o reutiliza) el prefab visual de un arquetipo. Idempotente: si el prefab ya
    /// existe en disco se devuelve tal cual, para no perder retoques manuales.
    /// </summary>
    private static GameObject CreateOrLoadWeaponVisual(
        string prefabName, VisualShape shape, float size, Color color, bool translucent)
    {
        string path = $"{VisualFolder}/{prefabName}.prefab";

        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);

        if (existing != null)
        {
            return existing;
        }

        GameObject root = new GameObject(prefabName);

        // El Disco es un cilindro achatado: tumbado marca el suelo y no se confunde con un orbe.
        PrimitiveType primitiveType = shape == VisualShape.Disc
            ? PrimitiveType.Cylinder
            : (shape == VisualShape.Capsule ? PrimitiveType.Capsule : PrimitiveType.Sphere);

        GameObject primitive = GameObject.CreatePrimitive(primitiveType);
        primitive.name = "Mesh";
        primitive.transform.SetParent(root.transform, false);

        primitive.transform.localScale = shape == VisualShape.Disc
            ? new Vector3(size, 0.02f, size)                 // disco plano tumbado
            : shape == VisualShape.Capsule
                ? new Vector3(size * 0.5f, size, size * 0.5f) // alargado = dirección de vuelo
                : Vector3.one * size;

        // Sin collider: el daño usa OverlapSphere, no triggers. Un collider aquí haría que
        // el arma empujara al propio jugador y a los enemigos.
        Collider collider = primitive.GetComponent<Collider>();

        if (collider != null)
        {
            Object.DestroyImmediate(collider);
        }

        // Material propio en el prefab: el tintado por MaterialPropertyBlock del runtime
        // no controla el canal alfa, así que un aura translúcida lo necesita en el asset.
        MeshRenderer renderer = primitive.GetComponent<MeshRenderer>();

        if (renderer != null)
        {
            renderer.sharedMaterial = CreateOrGetVisualMaterial(color, translucent);
        }

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);

        return prefab;
    }

    /// <summary>Marca como sucios varios assets para forzar su escritura en disco.</summary>
    private static void MarkDirty(params ScriptableObject[] assets)
    {
        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] == null) continue;

            // CRÍTICO para la persistencia: sin SetDirty el cambio se ve en el Inspector pero
            // NUNCA se escribe en el .asset, y al recargar el dominio el asset vuelve a su
            // estado original en disco.
            EditorUtility.SetDirty(assets[i]);
        }
    }

    /// <summary>
    /// Crea (o corrige) un <see cref="WeaponDataSO"/> con nombre, arquetipo, stats y visual.
    /// Idempotente: si el asset existe se sobrescribe en vez de duplicarlo.
    /// </summary>
    private static WeaponDataSO ConfigureWeapon(
        string fileName,
        string weaponName,
        WeaponArchetype archetype,
        string description,
        float damage,
        float attackRange,
        float attackInterval,
        float visualScale)
    {
        WeaponDataSO weapon = LoadOrCreate<WeaponDataSO>($"{WeaponFolder}/{fileName}.asset");

        SetString(weapon, "weaponName", weaponName);
        SetEnum(weapon, "archetype", archetype);
        SetFloat(weapon, "damage", damage);
        SetFloat(weapon, "attackRange", attackRange);
        SetFloat(weapon, "attackInterval", attackInterval);
        SetFloat(weapon, "visualScale", visualScale);

        WriteWeaponDescription(weapon, description);

        // Visual: se reutiliza el prefab del arquetipo, creado por SetupWeaponVisuals.
        GameObject visual = GetArchetypeVisual(archetype);

        if (visual != null)
        {
            SetObject(weapon, "visualPrefab", visual);
        }

        return weapon;
    }

    /// <summary>
    /// Escribe la descripción técnica en el asset del arma.
    ///
    /// WeaponDataSO no declara un campo <c>description</c> en su versión actual, así que se
    /// busca por reflexión: si el campo existe (o se añade en el futuro) se escribe; si no,
    /// el texto vive igualmente en la carta de mejora, que es donde el jugador lo lee.
    /// </summary>
    private static void WriteWeaponDescription(WeaponDataSO weapon, string description)
    {
        if (weapon == null) return;

        SerializedObject serialized = new SerializedObject(weapon);
        SerializedProperty field = serialized.FindProperty("description");

        if (field == null)
        {
            Debug.Log($"[GameFeel] '{weapon.name}': el SO no tiene campo 'description'. " +
                     "El texto se aplicará a su carta de mejora en su lugar.");
            return;
        }

        field.stringValue = description;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>Devuelve el prefab visual ya creado para ese arquetipo, si existe.</summary>
    private static GameObject GetArchetypeVisual(WeaponArchetype archetype)
    {
        string prefabName = archetype == WeaponArchetype.Pulse ? "Visual_Pulse"
            : archetype == WeaponArchetype.Orbit ? "Visual_Orbit"
            : archetype == WeaponArchetype.Projectile ? "Visual_Projectile"
            : "Visual_Aura";

        return AssetDatabase.LoadAssetAtPath<GameObject>($"{VisualFolder}/{prefabName}.prefab");
    }

    private static Material CreateOrGetVisualMaterial(Color color, bool translucent)
    {
        string path = translucent
            ? $"{VisualFolder}/Mat_VisualTranslucent.mat"
            : $"{VisualFolder}/Mat_VisualSolid.mat";

        Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);

        if (existing != null)
        {
            SetMaterialColor(existing, color);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        Shader shader = FindVisualShader();

        if (shader == null)
        {
            Debug.LogWarning("[GameFeel] No se encontró un shader URP válido: los visuales usarán el material por defecto.");
            return null;
        }

        Material material = new Material(shader)
        {
            name = System.IO.Path.GetFileNameWithoutExtension(path)
        };

        SetMaterialColor(material, color);

        // El proyecto usa URP, donde el alfa solo se respeta si el material está en modo
        // "Transparent". Sin esto, el aura y el pulso se verían como sólidos opacos.
        if (translucent)
        {
            ConfigureTransparent(material);
        }

        material.EnableKeyword("_EMISSION");

        AssetDatabase.CreateAsset(material, path);
        EditorUtility.SetDirty(material);

        return material;
    }

    private static void SetMaterialColor(Material material, Color color)
    {
        if (material == null) return;

        Color opaque = new Color(color.r, color.g, color.b, 1f);

        // BaseColor y Color cubren URP Lit y Standard; Emission hace que el arma destaque
        // con el Bloom que ya tiene el volumen de post-proceso del proyecto.
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", opaque * 0.8f);
    }

    private static Shader FindVisualShader()
    {
        // El proyecto usa URP (PC_RPAsset / Mobile_RPAsset), así que se prioriza su Lit.
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");

        if (shader != null) return shader;

        shader = Shader.Find("Universal Render Pipeline/Unlit");

        if (shader != null) return shader;

        return Shader.Find("Standard");
    }

    /// <summary>Asigna el visual correspondiente a todos los WeaponDataSO de ese arquetipo.</summary>
    private static void AssignVisualToArchetype(WeaponArchetype archetype, GameObject visual)
    {
        if (visual == null) return;

        string[] guids = AssetDatabase.FindAssets("t:WeaponDataSO", new[] { "Assets" });
        int assigned = 0;

        for (int i = 0; i < guids.Length; i++)
        {
            WeaponDataSO asset = AssetDatabase.LoadAssetAtPath<WeaponDataSO>(AssetDatabase.GUIDToAssetPath(guids[i]));

            if (asset == null || asset.archetype != archetype) continue;

            // CRÍTICO: sin SetDirty el cambio se ve en memoria pero NUNCA llega al .asset.
            // Tras recargar el dominio, el arma vuelve a quedarse sin visual.
            SetObject(asset, "visualPrefab", visual);
            EditorUtility.SetDirty(asset);
            assigned++;
        }

        if (assigned > 0)
        {
            Debug.Log($"[GameFeel] Visual asignado a {assigned} asset(s) de tipo {archetype}.");
        }
    }

    /// <summary>
    /// Cuenta los WeaponDataSO sin <c>visualPrefab</c>. Cada arquetipo debe tener el suyo: sin
    /// él, <c>CreatePrimitiveFallback</c> genera una esfera gris idéntica para todas y las
    /// armas dejan de distinguirse a simple vista.
    /// </summary>
    private static int CountWeaponDataWithoutVisual()
    {
        string[] guids = AssetDatabase.FindAssets("t:WeaponDataSO", new[] { "Assets" });
        int missing = 0;

        for (int i = 0; i < guids.Length; i++)
        {
            WeaponDataSO asset = AssetDatabase.LoadAssetAtPath<WeaponDataSO>(AssetDatabase.GUIDToAssetPath(guids[i]));

            if (asset != null && asset.visualPrefab == null)
            {
                missing++;
            }
        }

        return missing;
    }

    /// <summary>
    /// Puebla el WeaponController del Player y su catálogo de armas.
    ///
    /// NOTA DE DISEÑO: WeaponController NO tiene campo de catálogo. Sus armas se equipan una
    /// a una desde el panel, y equiparlas todas aquí anularía la decisión del jugador en el
    /// Día 1. Por eso "poblar" significa: asegurar el componente, activarlo, dejar el loadout
    /// vacío y cargar el catálogo de upgrades que el panel puede ofrecer.
    /// </summary>
    private static bool PopulateWeaponController(List<WeaponDataSO> weapons)
    {
        PlayerController player = Object.FindAnyObjectByType<PlayerController>(FindObjectsInactive.Include);

        if (player == null)
        {
            Debug.LogWarning("[GameFeel] No se encontró el Player para poblar el WeaponController.");
            return false;
        }

        WeaponController controller = player.GetComponent<WeaponController>();

        if (controller == null)
        {
            controller = player.gameObject.AddComponent<WeaponController>();
            Debug.Log("[GameFeel] Se añadió WeaponController al Player durante el poblado.");
        }

        // Loadout VACÍO a propósito: la primera arma la elige el jugador en el panel del Día 1.
        SerializedObject serialized = new SerializedObject(controller);

        SerializedProperty starting = serialized.FindProperty("startingWeapons");

        if (starting != null)
        {
            starting.arraySize = 0;
        }

        SetBool(serialized, "useNewWeaponSystem", true);
        serialized.ApplyModifiedProperties();

        // El Player es un objeto de ESCENA, no un asset: SetDirty + escena sucia para que
        // el cambio se persista con Ctrl+S.
        EditorUtility.SetDirty(controller);
        EditorUtility.SetDirty(player);

        // Catálogo que consume el panel de armas: una upgrade por ARMA, deduplicada.
        // Se llama a la versión sin parámetros: la lista de armas ya se aplicó al loadout
        // del controller, y el catálogo se reconstruye escaneando los assets (así recoge
        // también las evoluciones de jefe que se hayan creado).
        List<UpgradeDataSO> catalog = BuildCatalogUpgrades();
        CacheWeaponFlowList("startingWeapons", catalog);

        Debug.Log($"[GameFeel] WeaponController poblado: {catalog.Count} arma(s) en catálogo, loadout inicial vacío.");

        return true;
    }

    /// <summary>
    /// Asegura que el GameObject 'WeaponPanelUI' esté ACTIVO en la jerarquía.
    ///
    /// Si el panel se desactivó al probar el flujo, deja de recibir Awake y no vuelve a abrir.
    /// La visibilidad real la gestiona el CanvasGroup dentro de WeaponPanelUI, no el GameObject:
    /// dejarlo activo no lo muestra.
    /// </summary>
    private static bool EnsureWeaponPanelActive()
    {
        GameObject panelObject = FindGameObjectByName("WeaponPanelUI");

        if (panelObject == null)
        {
            Debug.LogWarning("[GameFeel] No se encontró el GameObject 'WeaponPanelUI' para activarlo.");
            return false;
        }

        if (!panelObject.activeSelf)
        {
            panelObject.SetActive(true);
            Debug.Log("[GameFeel] 'WeaponPanelUI' estaba desactivado: se ha reactivado.");
        }

        if (!panelObject.activeInHierarchy)
        {
            Debug.LogWarning(
                "[GameFeel] 'WeaponPanelUI' sigue inactivo en la jerarquía: revisa si un padre está apagado.",
                panelObject);
        }

        EditorUtility.SetDirty(panelObject);

        return panelObject.activeSelf;
    }

    /// <summary>
    /// Reconstruye el cableado interno del <see cref="WeaponPanelUI"/>: componente, array de
    /// cartas y referencias internas de cada carta (botón, imagen, textos).
    ///
    /// POR QUÉ ESTE PASO: arrastrar referencias en el Inspector es propenso a errores y no es
    /// reproducible. Aquí se resuelve todo por código y se VERIFICA después, de modo que un
    /// cableado roto se detecta en la consola en lugar de fallar en silencio al hacer clic.
    ///
    /// IDEMPOTENTE: se puede ejecutar tantas veces como haga falta.
    /// </summary>
    [MenuItem(MenuRoot + "16. Auto-conectar tarjetas del WeaponPanelUI", false, 43)]
    private static void AutoConnectWeaponPanelCards()
    {
        if (Application.isPlaying)
        {
            Debug.LogWarning("[GameFeel] Sal de Play Mode antes de modificar la escena.");
            return;
        }

        GameObject panelObject = FindGameObjectByName("WeaponPanelUI");

        if (panelObject == null)
        {
            Debug.LogWarning(
                "[GameFeel] No se encontró el GameObject 'WeaponPanelUI'. " +
                "Duplícalo desde 'LevelUpPanel' y vuelve a ejecutar esta opción.");
            return;
        }

        WeaponPanelUI panel = panelObject.GetComponent<WeaponPanelUI>();

        if (panel == null)
        {
            panel = panelObject.AddComponent<WeaponPanelUI>();
            Debug.Log("[GameFeel] Se ha añadido el componente WeaponPanelUI al GameObject.", panel);
        }

        // 1) Recolectar cartas: se reutilizan las existentes; si no hay, se clonan del LevelUpUI.
        UpgradeCardUI[] cards = ResolveOrCreateCards(panelObject, 2);

        if (cards.Length == 0)
        {
            Debug.LogError(
                "[GameFeel] WeaponPanelUI se queda sin cartas: necesita objetos con UpgradeCardUI " +
                "como hijos del panel.");
            return;
        }

        // 2) Reparar cada carta: botón, imagen, textos y limpieza de eventos heredados.
        int repaired = 0;

        for (int i = 0; i < cards.Length; i++)
        {
            if (RepairCard(cards[i]))
            {
                repaired++;
            }
        }

        // 3) Grabar el array, el catálogo y las referencias raíz en el componente.
        CanvasGroup canvasGroup = panelObject.GetComponent<CanvasGroup>();

        if (canvasGroup == null)
        {
            canvasGroup = panelObject.AddComponent<CanvasGroup>();
            Debug.Log("[GameFeel] Se ha añadido CanvasGroup al WeaponPanelUI (lo requiere).", panel);
        }

        SerializedObject serialized = new SerializedObject(panel);

        SetObject(serialized, "canvasGroup", canvasGroup);
        SetObject(serialized, "panelRect", panelObject.transform as RectTransform);
        SetCardArray(serialized, "cards", cards);

        // Catálogo: SOLO armas. WeaponPanelUI filtra por grantsWeapon, así que alimentarlo con
        // cartas de estadística le haría intentar ofrecer armas que no existen.
        SerializedProperty catalog = serialized.FindProperty("weaponUpgrades");

        if (catalog != null)
        {
            List<UpgradeDataSO> clean = BuildCatalogUpgrades();
            catalog.arraySize = clean.Count;

            for (int i = 0; i < clean.Count; i++)
            {
                catalog.GetArrayElementAtIndex(i).objectReferenceValue = clean[i];
            }
        }

        SetFloat(serialized, "fadeDuration", 0.25f);
        SetBool(serialized, "logSelections", true);
        serialized.ApplyModifiedProperties();

        // 4) PERSISTENCIA: componente, escena y assets.
        EditorUtility.SetDirty(panel);
        EditorUtility.SetDirty(panelObject);
        MarkActiveSceneDirty();
        AssetDatabase.SaveAssets();

        // 5) Verificación posterior: confirma en consola que quedó usable.
        VerifyWeaponPanelWiring(panel, cards, repaired);

        Selection.activeObject = panelObject;
        EditorGUIUtility.PingObject(panelObject);
    }

    /// <summary>
    /// Repara una carta: garantiza botón pulsable, imagen y textos, y le quita cualquier
    /// rastro del LevelUpUI del que se clonó.
    /// </summary>
    /// <returns>True si la carta quedó completa y utilizable.</returns>
    private static bool RepairCard(UpgradeCardUI card)
    {
        if (card == null) return false;

        GameObject cardObject = card.gameObject;

        // Botón: sin él la carta no es pulsable. Se reutiliza el existente si lo hay.
        Button button = cardObject.GetComponent<Button>();

        if (button == null)
        {
            button = cardObject.GetComponentInChildren<Button>(true);
        }

        if (button == null)
        {
            // Último recurso: se añade al propio objeto. Con transition None sigue siendo
            // pulsable, aunque se pierde el resaltado visual del hover.
            button = cardObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
        }

        TextMeshProUGUI title = ResolveTitle(cardObject);
        TextMeshProUGUI description = ResolveDescription(cardObject);
        Image icon = cardObject.GetComponentInChildren<Image>(true);

        SerializedObject serialized = new SerializedObject(card);

        SetObject(serialized, "titleText", title);
        SetObject(serialized, "descriptionText", description);
        SetObject(serialized, "iconImage", icon);
        SetObject(serialized, "selectButton", button);

        // LIMPIEZA DE EVENTOS HEREDADOS: la carta se clonó del LevelUpUI, así que su botón
        // puede conservar un onClick apuntando al panel viejo, que cerraría el panel de nivel
        // en vez del de armas. WeaponPanelUI inyecta su callback en cada apertura, pero los
        // listeners persistentes se limpian aquí.
        button.onClick.RemoveAllListeners();

        // El callback del LevelUpUI es justo lo que NO debe quedar en este panel.
        card.OnSelectedOverride = null;

        serialized.ApplyModifiedProperties();
        EditorUtility.SetDirty(card);
        EditorUtility.SetDirty(button);

        return button != null && title != null && description != null;
    }

    /// <summary>Localiza el texto de título: el TMP con mayor tamaño de fuente en la carta.</summary>
    private static TextMeshProUGUI ResolveTitle(GameObject cardObject)
    {
        TextMeshProUGUI[] all = cardObject.GetComponentsInChildren<TextMeshProUGUI>(true);

        if (all == null || all.Length == 0) return null;

        TextMeshProUGUI best = all[0];

        for (int i = 1; i < all.Length; i++)
        {
            if (all[i].fontSize > best.fontSize)
            {
                best = all[i];
            }
        }

        return best;
    }

    /// <summary>Localiza el texto de descripción: el TMP que no es el título.</summary>
    private static TextMeshProUGUI ResolveDescription(GameObject cardObject)
    {
        TextMeshProUGUI[] all = cardObject.GetComponentsInChildren<TextMeshProUGUI>(true);
        TextMeshProUGUI title = ResolveTitle(cardObject);

        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != title) return all[i];
        }

        return null;
    }

    /// <summary>
    /// Comprueba el cableado después de repararlo y lo reporta en consola. Convertir el fallo
    /// silencioso en un mensaje es lo que evita horas de depuración a ciegas: sin esto, una
    /// carta sin botón solo se descubre cuando el jugador hace clic y no pasa nada.
    /// </summary>
    private static void VerifyWeaponPanelWiring(WeaponPanelUI panel, UpgradeCardUI[] cards, int repaired)
    {
        List<string> issues = new List<string>();

        SerializedObject serialized = new SerializedObject(panel);
        SerializedProperty array = serialized.FindProperty("cards");

        if (array == null || array.arraySize == 0)
        {
            issues.Add("El campo 'cards' quedó vacío: el panel no podrá mostrar opciones.");
        }

        for (int i = 0; i < cards.Length; i++)
        {
            if (cards[i] == null)
            {
                issues.Add($"La carta #{i} es nula.");
                continue;
            }

            SerializedObject cardSO = new SerializedObject(cards[i]);

            if (cardSO.FindProperty("selectButton").objectReferenceValue == null)
            {
                issues.Add($"'{cards[i].name}': sin botón pulsable.");
            }

            if (cardSO.FindProperty("titleText").objectReferenceValue == null)
            {
                issues.Add($"'{cards[i].name}': sin texto de título.");
            }

            if (cardSO.FindProperty("descriptionText").objectReferenceValue == null)
            {
                issues.Add($"'{cards[i].name}': sin texto de descripción.");
            }
        }

        SerializedProperty catalog = serialized.FindProperty("weaponUpgrades");
        int catalogCount = catalog != null ? catalog.arraySize : 0;

        Debug.Log(
            $"<color=cyan>[GameFeel] WeaponPanelUI: {cards.Length} carta(s), {repaired} reparada(s), " +
            $"{catalogCount} arma(s) en catálogo.</color>",
            panel);

        for (int i = 0; i < issues.Count; i++)
        {
            Debug.LogWarning("  " + issues[i], panel);
        }
    }

    /// <summary>
    /// Rellena el array de cartas del <see cref="LevelUpUI"/> con todos los
    /// <see cref="UpgradeCardUI"/> de su jerarquía.
    ///
    /// NOTA IMPORTANTE: el campo de <see cref="LevelUpUI"/> se llama <c>upgradeCards</c>, NO
    /// <c>cards</c> (ese nombre es el de <see cref="WeaponPanelUI"/>). Buscar 'cards' aquí
    /// devolvía null y provocaba un falso positivo del auditor.
    ///
    /// IDEMPOTENTE: si el array ya está correcto, no escribe nada.
    /// </summary>
    [MenuItem(MenuRoot + "17. Auto-conectar cartas del LevelUpUI", false, 44)]
    private static void AutoConnectLevelUpCards()
    {
        if (Application.isPlaying)
        {
            Debug.LogWarning("[GameFeel] Sal de Play Mode antes de modificar la escena.");
            return;
        }

        GameObject panelObject = FindGameObjectByName("LevelUpPanel");

        if (panelObject == null)
        {
            Debug.LogWarning("[GameFeel] No se encontró el GameObject 'LevelUpPanel'.");
            return;
        }

        LevelUpUI panel = panelObject.GetComponent<LevelUpUI>();

        if (panel == null)
        {
            panel = panelObject.AddComponent<LevelUpUI>();
            Debug.Log("[GameFeel] Se ha añadido el componente LevelUpUI al GameObject.", panel);
        }

        // Se recogen TODAS las cartas de la jerarquía. includeInactive: las cartas empiezan
        // desactivadas (HideCard) y si se filtraran quedarían fuera del array.
        UpgradeCardUI[] cards = panelObject.GetComponentsInChildren<UpgradeCardUI>(true);

        if (cards == null || cards.Length == 0)
        {
            Debug.LogError(
                "[GameFeel] 'LevelUpPanel' no tiene ningún UpgradeCardUI en su jerarquía: " +
                "no hay cartas que autoconectar.");
            return;
        }

        SerializedObject serialized = new SerializedObject(panel);
        SerializedProperty array = serialized.FindProperty("upgradeCards");

        if (array == null)
        {
            Debug.LogError(
                "[GameFeel] LevelUpUI no expone el campo 'upgradeCards'. " +
                "Revisa que el script LevelUpUI.cs no haya cambiado.");
            return;
        }

        // Solo se escribe si difiere: evita ensuciar la escena con un Ctrl+Z vacío cuando
        // el cableado ya era correcto (que es el caso normal).
        bool needsWrite = array.arraySize != cards.Length;

        if (!needsWrite)
        {
            for (int i = 0; i < cards.Length; i++)
            {
                if (array.GetArrayElementAtIndex(i).objectReferenceValue != cards[i])
                {
                    needsWrite = true;
                    break;
                }
            }
        }

        if (!needsWrite)
        {
            Debug.Log($"[GameFeel] LevelUpUI ya tenía sus {cards.Length} carta(s) conectadas.", panel);
            return;
        }

        array.arraySize = cards.Length;

        for (int i = 0; i < cards.Length; i++)
        {
            array.GetArrayElementAtIndex(i).objectReferenceValue = cards[i];
        }

        serialized.ApplyModifiedProperties();

        // PERSISTENCIA: SetDirty en el componente + escena sucia (Ctrl+S lo guarda).
        EditorUtility.SetDirty(panel);
        MarkActiveSceneDirty();
        AssetDatabase.SaveAssets();

        Debug.Log(
            $"<color=green>[GameFeel] LevelUpUI: {cards.Length} carta(s) conectadas al campo 'upgradeCards'.</color>",
            panel);
    }

    // ==================================================================
    // 18. REORGANIZACIÓN DE ASSETS Y MIGRACIÓN A v2
    // ==================================================================

    /// <summary>Carpeta de los prefabs visuales de arma (arte dedicado del proyecto).</summary>
    private const string WeaponVisualsFolder = "Assets/Prefabs/Weapons";

    /// <summary>
    /// Reorganiza las carpetas de assets a la jerarquía final y migra todas las mejoras
    /// heredadas (v1) al sistema v2 de <see cref="StatModifierSO"/>.
    ///
    /// POR QUÉ MIGRA Y NO SOLO MUEVE: el "Efecto v1" (upgradeType + value) ya no existe en
    /// <see cref="UpgradeDataSO"/>. Un asset que aún lo usara se quedaría con una carta vacía
    /// que no hace nada. Migrarlos aquí es la última red antes de que desaparezcan.
    /// </summary>
    [MenuItem(MenuRoot + "18. Reorganizar assets y migrar a v2", false, 90)]
    private static void ReorganizeAssetsAndMigrate()
    {
        if (Application.isPlaying)
        {
            Debug.LogWarning("[GameFeel] Sal de Play Mode antes de modificar los assets.");
            return;
        }

        EnsureFolder(UpgradeFolder);
        EnsureFolder(ModifierFolder);
        EnsureFolder(WeaponFolder);
        EnsureFolder(WeaponVisualsFolder);

        MoveAssetsTo("Modifier", ModifierFolder);
        MoveAssetsTo("Upgrade", UpgradeFolder);
        MoveAssetsTo("Weapon", WeaponFolder);

        int created = CreateMissingModifiers();
        int migrated = MigrateLegacyUpgrades();
        int moved = MoveWeaponVisualPrefabs();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        MarkActiveSceneDirty();

        Debug.Log(
            $"<color=green>[GameFeel] Reorganización completa: {moved} prefab(s) de arma movidos, " +
            $"{created} SM_ creado(s), {migrated} mejora(s) migrada(s) a v2.</color>");
    }

    /// <summary>
    /// Mueve los .asset de una carpeta a otra preservando el .meta (y por tanto el GUID).
    ///
    /// MOVER EL .META JUNTO AL .ASSET ES LO CRÍTICO: sin él Unity generaría un GUID nuevo y
    /// todas las escenas y ScriptableObjects que lo referencian se quedarían con un
    /// MissingReference.
    /// </summary>
    private static int MoveAssetsTo(string sourceFolderName, string destinationFolder)
    {
        string source = $"Assets/Scripts/ScriptableObject/{sourceFolderName}";

        if (!AssetDatabase.IsValidFolder(source)) return 0;

        string[] guids = AssetDatabase.FindAssets("t:ScriptableObject", new[] { source });
        int moved = 0;

        for (int i = 0; i < guids.Length; i++)
        {
            string oldPath = AssetDatabase.GUIDToAssetPath(guids[i]);

            if (oldPath == destinationFolder) continue;

            string fileName = oldPath.Substring(oldPath.LastIndexOf('/') + 1);
            string newPath = $"{destinationFolder}/{fileName}";

            // MoveAsset mueve el .meta con el asset: el GUID se conserva.
            string error = AssetDatabase.MoveAsset(oldPath, newPath);

            if (string.IsNullOrEmpty(error))
            {
                moved++;
            }
            else
            {
                Debug.LogWarning($"[GameFeel] No se pudo mover '{oldPath}' a '{newPath}': {error}");
            }
        }

        // La carpeta vieja se elimina solo si quedó vacía; Unity deja el .meta.
        if (AssetDatabase.GetSubFolders(source).Length == 0 && AssetDatabase.FindAssets("", new[] { source }).Length == 0)
        {
            AssetDatabase.DeleteAsset(source);
        }

        return moved;
    }

    /// <summary>Mueve los prefabs visuales de arma a Assets/Prefabs/Weapons/.</summary>
    private static int MoveWeaponVisualPrefabs()
    {
        // Origen histórico: el arte dedicado llegó a Assets/Materials/Visuals y la herramienta
        // antigua generaba otro set en Assets/UI/WeaponVisuals. Se recogen ambos.
        string[] sources = { "Assets/Materials/Visuals", "Assets/UI/WeaponVisuals", "Assets/Prefab/WeaponVisuals" };

        int moved = 0;

        for (int s = 0; s < sources.Length; s++)
        {
            if (!AssetDatabase.IsValidFolder(sources[s])) continue;

            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { sources[s] });

            for (int i = 0; i < guids.Length; i++)
            {
                string oldPath = AssetDatabase.GUIDToAssetPath(guids[i]);
                string fileName = oldPath.Substring(oldPath.LastIndexOf('/') + 1);

                // Solo los prefabs de ARMA; el resto del contenido se queda donde estaba.
                if (!fileName.StartsWith("Weapon")) continue;

                string newPath = $"{WeaponVisualsFolder}/{fileName}";
                string error = AssetDatabase.MoveAsset(oldPath, newPath);

                if (string.IsNullOrEmpty(error))
                {
                    moved++;
                }
            }
        }

        return moved;
    }

    /// <summary>Devuelve el nombre canónico del asset SM_ de una estadística.</summary>
    private static string ModifierAssetName(StatType stat)
    {
        return $"SM_{stat}";
    }

    /// <summary>
    /// Crea un <see cref="StatModifierSO"/> por cada estadística pasiva del juego.
    ///
    /// Los valores son "un punto de mejora": equilibrados y pensados como punto de partida,
    /// no como decisión de balance final. Un diseñador los ajusta en el Inspector sin código.
    /// </summary>
    /// <returns>Cuántos se crearon nuevos (los ya existentes se reutilizan sin tocar).</returns>
    private static int CreateMissingModifiers()
    {
        // HealPlayer se EXCLUYE a propósito: no es una estadística acumulable, la gestiona
        // UpgradeManager.ApplyHeal. Meterlo en un SM_ daría una carta que nunca hace nada.
        ModifierSpec[] defaults =
        {
            new ModifierSpec(StatType.IncreaseDamage,          StatOperation.PercentIncrease, 0.10f),
            new ModifierSpec(StatType.IncreaseRange,           StatOperation.PercentIncrease, 0.10f),
            new ModifierSpec(StatType.DecreaseAttackInterval,  StatOperation.PercentIncrease, 0.10f),
            new ModifierSpec(StatType.IncreaseMoveSpeed,       StatOperation.PercentIncrease, 0.10f),
            new ModifierSpec(StatType.IncreaseMaxHealth,       StatOperation.Add,           20f),
            new ModifierSpec(StatType.IncreaseArmor,           StatOperation.Add,            3f),
            new ModifierSpec(StatType.IncreaseCritChance,      StatOperation.Add,            0.10f),
            new ModifierSpec(StatType.IncreaseCritDamage,      StatOperation.Add,            0.50f),
            new ModifierSpec(StatType.HealthRegen,             StatOperation.Add,            1f),
            new ModifierSpec(StatType.IncreasePickupRadius,    StatOperation.Add,            1f),
            new ModifierSpec(StatType.IncreaseXPGain,          StatOperation.Add,            0.10f),
            new ModifierSpec(StatType.IncreaseProjectileCount, StatOperation.Add,            1f),
            new ModifierSpec(StatType.IncreaseAreaSize,        StatOperation.Add,            0.10f),
            new ModifierSpec(StatType.IncreaseDuration,        StatOperation.Add,            0.10f),
            new ModifierSpec(StatType.DecreaseCooldown,        StatOperation.Add,            0.05f),
        };

        int created = 0;

        for (int i = 0; i < defaults.Length; i++)
        {
            string path = $"{ModifierFolder}/{ModifierAssetName(defaults[i].Stat)}.asset";

            if (AssetDatabase.LoadAssetAtPath<StatModifierSO>(path) != null)
            {
                continue; // Ya existe: se respeta el ajuste del diseñador.
            }

            StatModifierSO modifier = ScriptableObject.CreateInstance<StatModifierSO>();

            SetEnum(modifier, "stat", defaults[i].Stat);
            SetEnum(modifier, "operation", defaults[i].Operation);
            SetFloat(modifier, "value", defaults[i].Value);

            AssetDatabase.CreateAsset(modifier, path);
            EditorUtility.SetDirty(modifier);
            created++;
        }

        return created;
    }

    /// <summary>
    /// Migra las mejoras que aún declaran el camino v1 (upgradeType + value) al sistema v2.
    ///
    /// Sin esto, esas cartas se quedarían SIN EFECTO tras eliminar el campo v1: el jugador
    /// elegiría una carta que no hace nada. Es la última red antes de que el formato
    /// antiguo desaparezca del todo.
    /// </summary>
    /// <returns>Cuántas se migraron.</returns>
    private static int MigrateLegacyUpgrades()
    {
        int migrated = 0;
        string[] guids = AssetDatabase.FindAssets("t:UpgradeDataSO", new[] { "Assets" });

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            UpgradeDataSO upgrade = AssetDatabase.LoadAssetAtPath<UpgradeDataSO>(path);

            if (upgrade == null) continue;

            // Ya migrada, o carta de arma pura: nada que hacer.
            if (upgrade.UsesModifiers || upgrade.grantsWeapon != null) continue;

            int legacyType = ReadLegacyIntFromYaml(path, "upgradeType:");

            if (legacyType < 0) continue; // No tenía efecto v1: es una carta sin modificadores.

            StatType mapped = MapLegacyType(legacyType);

            if (mapped == StatType.HealPlayer)
            {
                // La curación NO es una estadística acumulable. Se avisa para traducirla a mano
                // desde UpgradeManager.ApplyHeal, que es donde vive ahora.
                Debug.LogWarning(
                    $"[GameFeel] '{upgrade.name}' usaba el efecto v1 'HealPlayer'. Esa curación no " +
                    "es una estadística: aplícala desde UpgradeManager.ApplyHeal, no con un SM_.",
                    upgrade);
                continue;
            }

            // El porcentaje v1 se copia tal cual, con la MISMA operación, para que el efecto
            // final sea IDÉNTICO al que el jugador ya conoce.
            float legacyValue = ReadLegacyFloatFromYaml(path, "value:", 0.2f);

            StatModifierSO modifier = LoadOrCreateModifierFor(
                new ModifierSpec(mapped, StatOperation.PercentIncrease, legacyValue));

            SerializedObject serialized = new SerializedObject(upgrade);
            SerializedProperty array = serialized.FindProperty("modifiers");

            if (array != null)
            {
                array.arraySize = 1;
                array.GetArrayElementAtIndex(0).objectReferenceValue = modifier;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            EditorUtility.SetDirty(upgrade);
            migrated++;

            Debug.Log(
                $"[GameFeel] '{upgrade.name}' migrada de v1 a v2 ({mapped} +{legacyValue * 100f:0}%).",
                upgrade);
        }

        return migrated;
    }

    /// <summary>Traduce el índice v1 al StatType equivalente.</summary>
    private static StatType MapLegacyType(int legacyType)
    {
        switch (legacyType)
        {
            case 1: return StatType.IncreaseRange;
            case 2: return StatType.DecreaseAttackInterval;
            case 3: return StatType.IncreaseMoveSpeed;
            case 4: return StatType.HealPlayer;
            default: return StatType.IncreaseDamage;
        }
    }

    /// <summary>
    /// Lee un entero legacy directamente del YAML del .asset.
    ///
    /// Hace falta porque el campo ya no existe en la clase C#: la clave sigue en el archivo
    /// hasta que Unity lo reescriba, y durante la migración es la única fuente de verdad.
    /// </summary>
    /// <returns>El valor leído, o -1 si la clave no existe en el archivo.</returns>
    private static int ReadLegacyIntFromYaml(string assetPath, string keyPrefix)
    {
        if (!File.Exists(assetPath)) return -1;

        string[] lines = File.ReadAllLines(assetPath);

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();

            if (line.StartsWith(keyPrefix, System.StringComparison.Ordinal))
            {
                int parsed;
                string raw = line.Substring(keyPrefix.Length).Trim();

                return int.TryParse(raw, out parsed) ? parsed : -1;
            }
        }

        return -1;
    }

    /// <summary>Lee un float legacy del YAML, con valor por defecto si no existe o no parsea.</summary>
    private static float ReadLegacyFloatFromYaml(string assetPath, string keyPrefix, float defaultValue)
    {
        if (!File.Exists(assetPath)) return defaultValue;

        string[] lines = File.ReadAllLines(assetPath);

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();

            if (line.StartsWith(keyPrefix, System.StringComparison.Ordinal))
            {
                float parsed;
                string raw = line.Substring(keyPrefix.Length).Trim();

                // InvariantCulture es OBLIGATORIO: el YAML usa punto decimal, y con la
                // configuración regional española un "0.2" se parsearía como 0 (coma).
                return float.TryParse(raw, System.Globalization.NumberStyles.Float,
                           System.Globalization.CultureInfo.InvariantCulture, out parsed)
                    ? parsed
                    : defaultValue;
            }
        }

        return defaultValue;
    }


    /// <summary>Carga (o crea) el SM_ de una especificación concreta.</summary>
    private static StatModifierSO LoadOrCreateModifierFor(ModifierSpec spec)
    {
        string path = $"{ModifierFolder}/{ModifierAssetName(spec.Stat)}.asset";
        StatModifierSO modifier = AssetDatabase.LoadAssetAtPath<StatModifierSO>(path);

        if (modifier == null)
        {
            modifier = ScriptableObject.CreateInstance<StatModifierSO>();
            AssetDatabase.CreateAsset(modifier, path);
        }

        SetEnum(modifier, "stat", spec.Stat);
        SetEnum(modifier, "operation", spec.Operation);
        SetFloat(modifier, "value", spec.Value);
        EditorUtility.SetDirty(modifier);

        return modifier;
    }

    /// <summary>Asigna el WeaponPanelUI al RunWeaponFlow de la escena, si existe.</summary>
    private static void LinkWeaponPanelToFlow(WeaponPanelUI panel)
    {
        RunWeaponFlow[] flows = Object.FindObjectsByType<RunWeaponFlow>(FindObjectsInactive.Include);

        if (flows == null || flows.Length == 0)
        {
            Debug.LogWarning(
                "[GameFeel] No se encontró RunWeaponFlow: el panel existe pero nadie lo abrirá. " +
                "Ejecuta antes la opción 11.");
            return;
        }

        for (int i = 0; i < flows.Length; i++)
        {
            SerializedObject serialized = new SerializedObject(flows[i]);

            SetObject(serialized, "weaponPanel", panel);
            SetBool(serialized, "preferWeaponPanel", true);

            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(flows[i]);
        }
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