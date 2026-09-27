using UnityEditor;
using UnityEngine;

/// <summary>
/// Conexión guiada de los SFX de combate desde el editor (menú 40 Days of Light > Audio).
/// Evita el error humano de dejar un sonido sin asignar:
///   - AtackEnemy.mp3   -> EnemyAI.impAttackSFX (zarpazo: se reproduce en el FRAME DE IMPACTO),
///   - ImapactEnemy.mp3 -> HealthComponent.damageSFX (golpe recibido; 2D prioritario en el Jugador),
///   - ImapactEnemy.mp3 -> AudioManager.fallbackImpactSFX (respaldo cuando no hay clip propio).
/// El paso es idempotente: solo rellena los campos que estén VACÍOS.
/// </summary>
public static class AudioSetupTools
{
    private const string MenuRoot = "40 Days of Light/Audio/";
    private const string AttackClipPath = "Assets/Audio/SFX/AtackEnemy.mp3";
    private const string ImpactClipPath = "Assets/Audio/SFX/ImapactEnemy.mp3";

    // --- Catálogo de variaciones y música (paso 2) ---
    private const string ClickUi01Path = "Assets/Audio/SFX/SFX_ClickUI_01.mp3";
    private const string ClickUi02Path = "Assets/Audio/SFX/SFX_ClickUI_02.mp3";
    private const string DamagePlayer01Path = "Assets/Audio/SFX/SFX_DamagePlayer_01.mp3";
    private const string DamagePlayer02Path = "Assets/Audio/SFX/SFX_DamagePlayer_02.mp3";
    private const string PlayerDeathPath = "Assets/Audio/SFX/SFX_PlayerDeath.mp3";
    private const string StepSandPath = "Assets/Audio/SFX/SFX_StepSand.mp3";
    private const string SprintSandPath = "Assets/Audio/SFX/SFX_SprintSand.mp3";
    private const string XpPickupPath = "Assets/Audio/SFX/SFX_XPOp.mp3";
    private const string NightStartPath = "Assets/Audio/SFX/SFX_NightStart.mp3";
    private const string GameOverMusicPath = "Assets/Audio/SFX/MUS_GameOver.mp3";
    private const string VictoryMusicPath = "Assets/Audio/SFX/MUS_Victory.mp3";

    [MenuItem(MenuRoot + "1. Conectar SFX de ataque, impacto y respaldo", false, 20)]
    private static void ConnectCombatSfx()
    {
        AudioClip attackClip = AssetDatabase.LoadAssetAtPath<AudioClip>(AttackClipPath);
        AudioClip impactClip = AssetDatabase.LoadAssetAtPath<AudioClip>(ImpactClipPath);

        if (attackClip == null || impactClip == null)
        {
            Debug.LogError(
                $"[Audio] No se encontraron los clips '{AttackClipPath}' y/o '{ImpactClipPath}'. " +
                "Revisa las rutas antes de continuar.");
            return;
        }

        int prefabsTouched = AssignToSelectedPrefabs(attackClip, impactClip);
        int sceneTouched = AssignInActiveScene(impactClip);

        if (prefabsTouched == 0 && sceneTouched == 0)
        {
            Debug.LogWarning(
                "[Audio] No había nada que conectar: los campos ya estaban asignados o no hay prefabs " +
                "seleccionados. Selecciona Enemy01.prefab en la ventana Project y repite la operación.");
            return;
        }

        AssetDatabase.SaveAssets();

        Debug.Log(
            $"[Audio] Conexión terminada | prefabs actualizados: {prefabsTouched} | " +
            $"elementos de la escena abierta actualizados: {sceneTouched}. " +
            "Guarda la escena y los prefabs modificados (Ctrl+S).");
    }

    /// <summary>
    /// Rellena los SFX de los prefabs seleccionados en la ventana Project (por ejemplo Enemy01.prefab).
    /// </summary>
    private static int AssignToSelectedPrefabs(AudioClip attackClip, AudioClip impactClip)
    {
        GameObject[] selected = Selection.GetFiltered<GameObject>(SelectionMode.Assets | SelectionMode.Editable);
        int touched = 0;

        for (int i = 0; i < selected.Length; i++)
        {
            EnemyAI[] enemies = selected[i].GetComponentsInChildren<EnemyAI>(true);

            for (int e = 0; e < enemies.Length; e++)
            {
                if (AssignIfEmpty(enemies[e], "impAttackSFX", attackClip)) touched++;
            }

            HealthComponent[] healthComponents = selected[i].GetComponentsInChildren<HealthComponent>(true);

            for (int h = 0; h < healthComponents.Length; h++)
            {
                if (AssignIfEmpty(healthComponents[h], "damageSFX", impactClip)) touched++;
            }
        }

        return touched;
    }

    /// <summary>
    /// Rellena los SFX de los objetos de la ESCENA abierta: el impacto de respaldo del AudioManager
    /// y el golpe del Jugador (que se reproduce en 2D y necesita clip propio o el de respaldo).
    /// </summary>
    private static int AssignInActiveScene(AudioClip impactClip)
    {
        int touched = 0;

        AudioManager[] managers = Object.FindObjectsByType<AudioManager>(FindObjectsInactive.Include);

        for (int i = 0; i < managers.Length; i++)
        {
            if (AssignIfEmpty(managers[i], "fallbackImpactSFX", impactClip)) touched++;
        }

        HealthComponent[] healthComponents = Object.FindObjectsByType<HealthComponent>(FindObjectsInactive.Include);

        for (int i = 0; i < healthComponents.Length; i++)
        {
            HealthComponent health = healthComponents[i];

            // Solo el Jugador: es el receptor que depende de un clip para oír su propio golpe.
            if (!health.CompareTag("Player") && health.GetComponent<PlayerController>() == null)
            {
                continue;
            }

            if (AssignIfEmpty(health, "damageSFX", impactClip)) touched++;
        }

        return touched;
    }

    /// <summary>
    /// Clips del catálogo resueltos una sola vez por operación (evita firmas con muchos parámetros).
    /// </summary>
    private sealed class Catalog
    {
        public AudioClip[] clickUi;
        public AudioClip[] playerDamage;
        public AudioClip playerDeath;
        public AudioClip stepSand;
        public AudioClip sprintSand;
        public AudioClip xpPickup;
        public AudioClip nightStart;
        public AudioClip gameOverMusic;
        public AudioClip victoryMusic;
    }

    private static AudioClip LoadClip(string path) => AssetDatabase.LoadAssetAtPath<AudioClip>(path);

    /// <summary>Variaciones existentes de un par de clips (null si no hay ninguna de las dos).</summary>
    private static AudioClip[] LoadVariations(string firstPath, string secondPath)
    {
        AudioClip first = LoadClip(firstPath);
        AudioClip second = LoadClip(secondPath);

        if (first == null && second == null) return null;
        if (second == null) return new[] { first };
        if (first == null) return new[] { second };

        return new[] { first, second };
    }

    /// <summary>Carga de una sola vez todos los clips del catálogo nuevo.</summary>
    private static Catalog LoadCatalog()
    {
        return new Catalog
        {
            clickUi = LoadVariations(ClickUi01Path, ClickUi02Path),
            playerDamage = LoadVariations(DamagePlayer01Path, DamagePlayer02Path),
            playerDeath = LoadClip(PlayerDeathPath),
            stepSand = LoadClip(StepSandPath),
            sprintSand = LoadClip(SprintSandPath),
            xpPickup = LoadClip(XpPickupPath),
            nightStart = LoadClip(NightStartPath),
            gameOverMusic = LoadClip(GameOverMusicPath),
            victoryMusic = LoadClip(VictoryMusicPath)
        };
    }

    /// <summary>
    /// Paso 2: conecta el catálogo nuevo en la ESCENA abierta, en los prefabs seleccionados y en los
    /// botones de la interfaz. Idempotente: solo rellena lo que esté vacío.
    /// </summary>
    [MenuItem(MenuRoot + "2. Conectar catálogo de SFX (variaciones), pasos y música", false, 21)]
    private static void ConnectAudioCatalog()
    {
        Catalog catalog = LoadCatalog();

        int sceneTouched = AssignCatalogInActiveScene(catalog);
        int prefabsTouched = AssignCatalogInSelectedPrefabs(catalog);
        int buttonsTouched = AttachClickSfxToSceneButtons();

        AssetDatabase.SaveAssets();

        Debug.Log(
            $"[Audio] Catálogo conectado | escena: {sceneTouched} campos | prefabs seleccionados: {prefabsTouched} campos | " +
            $"botones con UIClickSfx añadidos: {buttonsTouched}. " +
            "Guarda la escena y los prefabs modificados (Ctrl+S).");
    }

    /// <summary>Asigna el clip al campo indicado SOLO si está vacío (idempotente).</summary>
    private static bool AssignIfEmpty(Object target, string propertyName, AudioClip clip)
    {
        if (target == null || clip == null) return false;

        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(propertyName);

        if (property == null)
        {
            Debug.LogWarning(
                $"[Audio] '{target.name}' no expone el campo '{propertyName}': asígnalo a mano en el Inspector.",
                target);

            return false;
        }

        if (property.objectReferenceValue != null) return false;

        property.objectReferenceValue = clip;
        serialized.ApplyModifiedProperties();

        EditorUtility.SetDirty(target);
        MarkSceneDirtyIfNeeded(target);

        Debug.Log($"[Audio] '{target.name}': '{propertyName}' = '{clip.name}'.", target);

        return true;
    }

    /// <summary>Rellena un array de clips SOLO si TODAS sus entradas están vacías (idempotente).</summary>
    private static bool AssignArrayIfEmpty(Object target, string propertyName, AudioClip[] clips)
    {
        if (target == null || clips == null || clips.Length == 0) return false;

        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(propertyName);

        if (property == null || !property.isArray)
        {
            Debug.LogWarning(
                $"[Audio] '{target.name}' no expone el array '{propertyName}': asígnalo a mano en el Inspector.",
                target);

            return false;
        }

        for (int i = 0; i < property.arraySize; i++)
        {
            if (property.GetArrayElementAtIndex(i).objectReferenceValue != null) return false;
        }

        property.arraySize = clips.Length;

        for (int i = 0; i < clips.Length; i++)
        {
            property.GetArrayElementAtIndex(i).objectReferenceValue = clips[i];
        }

        serialized.ApplyModifiedProperties();

        EditorUtility.SetDirty(target);
        MarkSceneDirtyIfNeeded(target);

        Debug.Log($"[Audio] '{target.name}': '{propertyName}' = {clips.Length} variaciones.", target);

        return true;
    }

    /// <summary>
    /// Asigna el catálogo en la ESCENA abierta: AudioManager (clics, daño del jugador, muerte, XP,
    /// noche, pasos y música) y los componentes del Player (variaciones de daño y pasos).
    /// </summary>
    private static int AssignCatalogInActiveScene(Catalog catalog)
    {
        int touched = 0;

        AudioManager[] managers = Object.FindObjectsByType<AudioManager>(FindObjectsInactive.Include);

        for (int i = 0; i < managers.Length; i++)
        {
            AudioManager manager = managers[i];

            if (AssignArrayIfEmpty(manager, "uiClickSFX", catalog.clickUi)) touched++;
            if (AssignArrayIfEmpty(manager, "playerDamageSFX", catalog.playerDamage)) touched++;
            if (AssignIfEmpty(manager, "playerDeathSFX", catalog.playerDeath)) touched++;
            if (AssignIfEmpty(manager, "xpPickupSFX", catalog.xpPickup)) touched++;
            if (AssignIfEmpty(manager, "nightStartSFX", catalog.nightStart)) touched++;
            if (AssignIfEmpty(manager, "stepSandSFX", catalog.stepSand)) touched++;
            if (AssignIfEmpty(manager, "sprintSandSFX", catalog.sprintSand)) touched++;
            if (AssignIfEmpty(manager, "gameOverMusic", catalog.gameOverMusic)) touched++;
            if (AssignIfEmpty(manager, "victoryMusic", catalog.victoryMusic)) touched++;
        }

        PlayerController[] players = Object.FindObjectsByType<PlayerController>(FindObjectsInactive.Include);

        for (int i = 0; i < players.Length; i++)
        {
            if (AssignIfEmpty(players[i], "stepSandSFX", catalog.stepSand)) touched++;
            if (AssignIfEmpty(players[i], "sprintSandSFX", catalog.sprintSand)) touched++;

            HealthComponent health = players[i].GetComponent<HealthComponent>();

            if (health != null && AssignArrayIfEmpty(health, "playerDamageSFX", catalog.playerDamage)) touched++;
        }

        return touched;
    }

    /// <summary>
    /// Prefabs seleccionados en la ventana Project: gemas de XP (SFX_XPOp) y, si el prefab es el del
    /// Player, sus pasos y las variaciones del golpe recibido.
    /// </summary>
    private static int AssignCatalogInSelectedPrefabs(Catalog catalog)
    {
        GameObject[] selected = Selection.GetFiltered<GameObject>(SelectionMode.Assets | SelectionMode.Editable);
        int touched = 0;

        for (int i = 0; i < selected.Length; i++)
        {
            XPGem[] gems = selected[i].GetComponentsInChildren<XPGem>(true);

            for (int g = 0; g < gems.Length; g++)
            {
                if (AssignIfEmpty(gems[g], "gemPickupSFX", catalog.xpPickup)) touched++;
            }

            PlayerController[] players = selected[i].GetComponentsInChildren<PlayerController>(true);

            for (int p = 0; p < players.Length; p++)
            {
                if (AssignIfEmpty(players[p], "stepSandSFX", catalog.stepSand)) touched++;
                if (AssignIfEmpty(players[p], "sprintSandSFX", catalog.sprintSand)) touched++;
            }

            HealthComponent[] healthComponents = selected[i].GetComponentsInChildren<HealthComponent>(true);

            for (int h = 0; h < healthComponents.Length; h++)
            {
                // Solo el Player: en los enemigos el golpe recibido usa su propio 'Damage Sfx'.
                if (healthComponents[h].GetComponent<PlayerController>() == null) continue;

                if (AssignArrayIfEmpty(healthComponents[h], "playerDamageSFX", catalog.playerDamage)) touched++;
            }
        }

        return touched;
    }

    /// <summary>Los objetos de escena necesitan marcarse para que el cambio no se pierda al guardar.</summary>
    /// <summary>
    /// Añade UIClickSfx a TODOS los botones de la escena abierta (incluidos los de paneles apagados:
    /// pausa, subida de nivel, Game Over y Victoria). Idempotente: los que ya lo tienen no se tocan.
    /// </summary>
    private static int AttachClickSfxToSceneButtons()
    {
        UnityEngine.UI.Button[] buttons = Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsInactive.Include);
        int added = 0;

        for (int i = 0; i < buttons.Length; i++)
        {
            GameObject target = buttons[i].gameObject;

            if (target.GetComponent<UIClickSfx>() != null) continue;

            UIClickSfx clickSfx = target.AddComponent<UIClickSfx>();

            EditorUtility.SetDirty(clickSfx);
            MarkSceneDirtyIfNeeded(clickSfx);

            added++;
        }

        if (added > 0)
        {
            Debug.Log($"[Audio] UIClickSfx añadido a {added} botones (de {buttons.Length} encontrados).");
        }

        return added;
    }

    /// <summary>
    /// Paso 3: diagnóstico. Registra qué clips del catálogo existen en disco y qué campos de la escena
    /// siguen vacíos (para saber si falta ejecutar el paso 2 o asignar algo a mano).
    /// </summary>
    [MenuItem(MenuRoot + "3. Registrar estado del catálogo de audio", false, 22)]
    private static void LogAudioCatalogState()
    {
        Catalog catalog = LoadCatalog();

        UnityEngine.UI.Button[] buttons = Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsInactive.Include);
        int buttonsWithClick = 0;

        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i].GetComponent<UIClickSfx>() != null) buttonsWithClick++;
        }

        Debug.Log(
            "[Audio] Catálogo en disco | clics: " + CountLoaded(catalog.clickUi) + "/2" +
            " | daño del jugador: " + CountLoaded(catalog.playerDamage) + "/2" +
            " | muerte: " + DescribeLoaded(catalog.playerDeath) +
            " | pasos: " + DescribeLoaded(catalog.stepSand) + " + " + DescribeLoaded(catalog.sprintSand) +
            " | XP: " + DescribeLoaded(catalog.xpPickup) +
            " | noche: " + DescribeLoaded(catalog.nightStart) +
            " | MUS_GameOver: " + DescribeLoaded(catalog.gameOverMusic) +
            " | MUS_Victory: " + DescribeLoaded(catalog.victoryMusic));

        AudioManager[] managers = Object.FindObjectsByType<AudioManager>(FindObjectsInactive.Include);

        if (managers.Length == 0)
        {
            Debug.LogWarning("[Audio] No hay AudioManager en la escena abierta: ejecuta el paso 2 en la escena de partida.");
            return;
        }

        for (int i = 0; i < managers.Length; i++)
        {
            AudioManager manager = managers[i];

            Debug.Log(
                "[Audio] '" + manager.name + "' | uiClickSFX: " + DescribeField(manager, "uiClickSFX") +
                " | playerDamageSFX: " + DescribeField(manager, "playerDamageSFX") +
                " | playerDeathSFX: " + DescribeField(manager, "playerDeathSFX") +
                " | nightStartSFX: " + DescribeField(manager, "nightStartSFX") +
                " | xpPickupSFX: " + DescribeField(manager, "xpPickupSFX") +
                " | stepSandSFX: " + DescribeField(manager, "stepSandSFX") +
                " | sprintSandSFX: " + DescribeField(manager, "sprintSandSFX") +
                " | gameOverMusic: " + DescribeField(manager, "gameOverMusic") +
                " | victoryMusic: " + DescribeField(manager, "victoryMusic"),
                manager);
        }

        Debug.Log($"[Audio] Botones con clic conectado: {buttonsWithClick}/{buttons.Length} (el paso 2 los añade).");
    }

    private static int CountLoaded(AudioClip[] clips) => clips != null ? clips.Length : 0;

    private static string DescribeLoaded(AudioClip clip) => clip != null ? "sí" : "NO";

    /// <summary>Describe un campo serializado: nombre del clip, 'VACÍO' o 'rellenos/total' si es un array.</summary>
    private static string DescribeField(Object target, string propertyName)
    {
        if (target == null) return "sin componente";

        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(propertyName);

        if (property == null) return "campo inexistente";

        if (property.isArray)
        {
            int filled = 0;

            for (int i = 0; i < property.arraySize; i++)
            {
                if (property.GetArrayElementAtIndex(i).objectReferenceValue != null) filled++;
            }

            return filled + "/" + property.arraySize;
        }

        return property.objectReferenceValue != null ? property.objectReferenceValue.name : "VACÍO";
    }

    private static void MarkSceneDirtyIfNeeded(Object target)
    {
        if (Application.isPlaying) return;

        Component component = target as Component;

        if (component == null) return;

        UnityEngine.SceneManagement.Scene scene = component.gameObject.scene;

        if (scene.IsValid())
        {
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        }
    }
}
