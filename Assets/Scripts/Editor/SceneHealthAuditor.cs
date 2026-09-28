using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Auditor de salud de la escena: comprueba que no queden referencias <c>null</c> en los sistemas
/// críticos (UI, Player, armas y director de partida) y lo resume en UN único informe.
///
/// POR QUÉ EXISTE: un cableado roto en Unity no lanza excepciones al arrancar. Produce el
/// síntoma más caro que hay —"no pasa nada" cuando el jugador hace clic"— y descubrirlo exige
/// arrancar, jugar hasta ese punto y buscar a ciegas. Este script convierte ese fallo invisible
/// en una lista de referencias concretas y sueltas en la consola.
///
/// SOLO LECTURA: no modifica nada de la escena ni de los assets. Es una radiografía, no una
/// reparación, para que el diagnóstico sea fiable y repetible.
///
/// FORMATO: un único bloque con todos los hallazgos. Los problemas se marcan con ERROR y
/// los avisos con AVISO, de modo que se distinguan de un vistazo en un log lleno de ruido.
/// </summary>
public static class SceneHealthAuditor
{
    private const string MenuRoot = "40 Days of Light/Diagnostic/";

    private enum Severity
    {
        Ok,
        Warn,
        Error
    }

    /// <summary>Hallazgo de una comprobación concreta.</summary>
    private readonly struct Finding
    {
        public readonly Severity Severity;
        public readonly string Message;

        public Finding(Severity severity, string message)
        {
            Severity = severity;
            Message = message;
        }
    }

    /// <summary>
    /// Indica que el modo de consola limpia está activo: los logs informativos de bajo nivel
    /// están suprimidos y solo pasan los avisos y errores.
    ///
    /// Se consulta en el informe del auditor para que el estado sea visible sin abrir el
    /// código: si el usuario ve el icono de rayas en la salida, sabe por qué no ve más logs.
    /// </summary>
    public static bool ConsoleCleanMode => consoleCleanMode;

    /// <summary>
    /// Estado real del modo de consola limpia. Es privado: se expone solo por la propiedad
    /// de arriba, para que la lectura sea explícita en los pocos sitios que la usan.
    /// </summary>
    private static bool consoleCleanMode;

    /// <summary>Valores del menú de diagnóstico (ordenados y con separadores por prioridad).</summary>
    private const int PriorityAudit = 0;
    private const int PriorityClean = 20;

    // ======================================================================
    // 1. AUDITORÍA (SOLO DIAGNÓSTICO)
    // ======================================================================

    /// <summary>
    /// Auditoría completa. NO modifica nada de la escena ni de los assets: es una radiografía.
    ///
    /// Si detecta 0 problemas, imprime UN único mensaje verde (requisito de diseño: cuando
    /// todo está bien, el ruido de la propia herramienta es ruido).
    /// </summary>
    [MenuItem(MenuRoot + "1. 🔍 Auditor de Salud e IA (Solo Diagnóstico)", false, PriorityAudit)]
    public static void AuditSceneHealth()
    {
        if (Application.isPlaying)
        {
            Debug.LogWarning("[Diagnostic] Sal de Play Mode para auditar el cableado de la escena.");
            return;
        }

        List<Finding> findings = new List<Finding>();

        AuditPlayer(findings);
        AuditWeaponSystem(findings);
        AuditRunDirector(findings);
        AuditWeaponPanel(findings);
        AuditLevelUpPanel(findings);
        AuditDamageVignette(findings);

        Report(findings);
    }

    // ======================================================================
    // 2. LIMPIEZA DE CONSOLA
    // ======================================================================

    /// <summary>
    /// Deja la consola del Editor limpia y desactiva los logs ruidosos.
    ///
    /// POR QUÉ IMPORTA DURANTE PLAY MODE: sin esto, los logs de spawn de enemigos y de cambio
    /// de tick llegan a cientos por minuto. Con el ruido de fondo, los errores reales se
    /// pierden en el scroll y nadie los lee.
    ///
    /// LA CONSOLA SE LIMPIA POR REFLEXIÓN porque Unity no expone ninguna API pública:
    /// <c>UnityEditor.LogEntries</c> es interno, así que se busca por reflexión y, si en
    /// una versión futura deja de existir, la operación degrada con un aviso en vez de romper.
    /// </summary>
    [MenuItem(MenuRoot + "2. 🧹 Limpiar Consola y Silenciar Logs Ruidosos", false, PriorityClean)]
    public static void CleanConsoleAndSilence()
    {
        // 1) Forzar el modo silencioso. Vale tanto en Editor como en Play Mode: es la vía
        //    de parar el ruido AHORA MISMO, sin depender del valor guardado en la escena.
        GameStateController.SetVerboseLogs(false);
        consoleCleanMode = true;

        // 2) Limpiar la consola del Editor.
        ClearEditorConsole();
    }

    /// <summary>
    /// Vacía la consola del Editor de Unity vía reflexión sobre <c>UnityEditor.LogEntries</c>.
    ///
    /// <c>LogEntries</c> es una clase interna (no aparece en la API pública), y su firma
    /// <c>Clear()</c> es estática sin parámetros. Se resuelve con GetType/GetMethod en vez
    /// de una referencia directa para que el script compile en cualquier versión de Unity.
    ///
    /// Si no se puede resolver, se avisa con un Warning: la limpieza de consola es una
    /// comodidad, nunca un requisito para que el juego funcione.
    /// </summary>
    private static void ClearEditorConsole()
    {
        try
        {
            // LogEntries es una clase INTERNA de UnityEditor (no está en la API pública), así que
            // solo se puede alcanzar por reflexión. Type.GetType con el nombre cualificado por
            // ensamblado la resuelve directamente sin tener que cargar el assembly antes.
            System.Type logEntriesType = System.Type.GetType("UnityEditor.LogEntries, UnityEditor");

            if (logEntriesType == null)
            {
                Debug.LogWarning(
                    "[Diagnostic] 'UnityEditor.LogEntries' no existe en esta versión de Unity: " +
                    "no se puede limpiar la consola desde código. Los warnings siguen activos.");
                return;
            }

            System.Reflection.MethodInfo clearMethod = logEntriesType.GetMethod(
                "Clear",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);

            if (clearMethod == null)
            {
                Debug.LogWarning(
                    "[Diagnostic] No se encontró el método 'LogEntries.Clear': la consola no se ha limpiado.");
                return;
            }

            clearMethod.Invoke(null, null);
        }
        catch (System.Exception exception)
        {
            // Cualquier fallo aquí es cosmético: nunca debe interrumpir el flujo del editor.
            Debug.LogWarning(
                $"[Diagnostic] No se pudo limpiar la consola ({exception.GetType().Name}). " +
                "Los warnings y errores seguirán apareciendo normalmente.");
        }
    }

    /// <summary>
    /// Check del menú: muestra un tick cuando el modo de consola limpia está activo, para
    /// que el estado sea visible sin entrar al código.
    /// </summary>
    [MenuItem(MenuRoot + "2. 🧹 Limpiar Consola y Silenciar Logs Ruidosos", true)]
    private static bool CleanConsoleValidate()
    {
        return !Application.isPlaying; // Limpiar tiene sentido en Editor, no durante Play.
    }

    // ======================================================================
    // COMPROBACIONES: PLAYER Y ARMAS
    // ======================================================================

    /// <summary>El Player debe tener sus pilares: movimiento, vida, progresión y entrada.</summary>
    private static void AuditPlayer(List<Finding> findings)
    {
        PlayerController player = Object.FindAnyObjectByType<PlayerController>(FindObjectsInactive.Include);

        if (player == null)
        {
            Add(findings, Severity.Error, "PLAYER: no hay ningún GameObject con PlayerController.");
            return;
        }

        // .gameObject explícito: Require() espera un GameObject y player es un
        // PlayerController (un Component), que no es convertible de forma implícita.
        GameObject playerObject = player.gameObject;

        Require(findings, playerObject, "PLAYER", typeof(RunStats));
        Require(findings, playerObject, "PLAYER", typeof(HealthComponent));
        Require(findings, playerObject, "PLAYER", typeof(PlayerLevelSystem));
        Require(findings, playerObject, "PLAYER", typeof(PlayerInputReader));

        HealthComponent health = player.GetComponent<HealthComponent>();

        if (health == null)
        {
            Add(findings, Severity.Error, "PLAYER: sin HealthComponent no se puede recibir daño ni ver la barra de vida.");
        }
        else if (health.MaxHealth <= 0f)
        {
            Add(findings, Severity.Error, $"PLAYER: MaxHealth es {health.MaxHealth}. Debe ser mayor que 0.");
        }
    }

    /// <summary>Sistema de armas: activo, y sin doble fuente de daño.</summary>
    private static void AuditWeaponSystem(List<Finding> findings)
    {
        PlayerController player = Object.FindAnyObjectByType<PlayerController>(FindObjectsInactive.Include);

        if (player == null) return;

        WeaponController controller = player.GetComponent<WeaponController>();

        if (controller == null)
        {
            Add(findings, Severity.Error, "ARMAS: el Player no tiene WeaponController; ningún arma podrá funcionar.");
            return;
        }

        if (!controller.UseNewWeaponSystem)
        {
            Add(findings, Severity.Error,
                "ARMAS: useNewWeaponSystem está DESACTIVADO. El combate usa el PlayerAttack clásico " +
                "y las armas equipadas no atacan ni se ven.");
        }

        // Daño doble: los dos sistemas de ataque vivos a la vez. Es el fallo más caro porque
        // no lanza excepción: el juego simplemente hace el doble de daño y nadie se da cuenta.
        PlayerAttack legacy = player.GetComponent<PlayerAttack>();

        if (controller.UseNewWeaponSystem && legacy != null && legacy.enabled)
        {
            Add(findings, Severity.Error,
                $"ARMAS: '{player.name}' tiene el sistema nuevo ACTIVO y PlayerAttack también: daño doble.");
        }
        else if (legacy == null)
        {
            Add(findings, Severity.Warn, "ARMAS: el Player no tiene PlayerAttack (sistema clásico eliminado).");
        }
    }

    /// <summary>El RunDirector necesita su spawner para poder generar enemigos.</summary>
    private static void AuditRunDirector(List<Finding> findings)
    {
        RunDirector director = Object.FindAnyObjectByType<RunDirector>(FindObjectsInactive.Include);

        if (director == null)
        {
            Add(findings, Severity.Error, "RONDAS: no hay RunDirector, la partida no avanza.");
            return;
        }

        if (director.TotalRounds <= 0)
        {
            Add(findings, Severity.Error, $"RONDAS: TotalRounds es {director.TotalRounds}. Debe ser mayor que 0.");
        }

        if (Object.FindAnyObjectByType<GameStateController>(FindObjectsInactive.Include) == null)
        {
            Add(findings, Severity.Error,
                "RONDAS: no hay GameStateController; no se puede pausar ni devolver el tiempo.");
        }

        // El spawner se busca por tipo porque su campo es privado en el director.
        EnemySpawner spawner = Object.FindAnyObjectByType<EnemySpawner>(FindObjectsInactive.Include);

        if (spawner == null)
        {
            Add(findings, Severity.Error, "RONDAS: no hay EnemySpawner, nunca aparecerán enemigos.");
        }
        else if (!spawner.SpawningEnabled)
        {
            Add(findings, Severity.Warn,
                "RONDAS: el spawner está DESACTIVADO (puede ser el panel de arma inicial, o un fallo).");
        }
    }

    /// <summary>El panel de armas necesita cartas completas y un catálogo con armas.</summary>
    private static void AuditWeaponPanel(List<Finding> findings)
    {
        GameObject panelObject = FindByName("WeaponPanelUI");

        if (panelObject == null)
        {
            Add(findings, Severity.Error, "PANEL ARMAS: no existe el GameObject 'WeaponPanelUI'.");
            return;
        }

        if (!panelObject.activeInHierarchy)
        {
            Add(findings, Severity.Error, "PANEL ARMAS: el GameObject está INACTIVO (o lo está un padre).");
        }

        WeaponPanelUI panel = panelObject.GetComponent<WeaponPanelUI>();

        if (panel == null)
        {
            Add(findings, Severity.Error, "PANEL ARMAS: falta el componente WeaponPanelUI.");
            return;
        }

        Require(findings, panelObject, "PANEL ARMAS", typeof(CanvasGroup));

        // WeaponPanelUI declara su array como 'cards'.
        AuditCards(panelObject, panel, "cards", "PANEL ARMAS", findings);
        AuditWeaponCatalog(panel, "PANEL ARMAS", findings);
    }

    /// <summary>El panel de nivel comparte tipo de carta, pero su catálogo sí incluye stats.</summary>
    private static void AuditLevelUpPanel(List<Finding> findings)
    {
        GameObject panelObject = FindByName("LevelUpPanel");

        if (panelObject == null)
        {
            Add(findings, Severity.Warn, "PANEL NIVEL: no existe el GameObject 'LevelUpPanel'.");
            return;
        }

        LevelUpUI panel = panelObject.GetComponent<LevelUpUI>();

        if (panel == null)
        {
            Add(findings, Severity.Error, "PANEL NIVEL: falta el componente LevelUpUI.");
            return;
        }

        Require(findings, panelObject, "PANEL NIVEL", typeof(CanvasGroup));

        // LevelUpUI declara su array como 'upgradeCards', NO como 'cards'. Usar el nombre
        // equivocado aquí era lo que provocaba el falso "campo 'cards' está vacío".
        AuditCards(panelObject, panel, "upgradeCards", "PANEL NIVEL", findings);

        SerializedObject serialized = new SerializedObject(panel);
        SerializedProperty available = serialized.FindProperty("availableUpgrades");
        int count = available != null ? available.arraySize : 0;

        if (count == 0)
        {
            Add(findings, Severity.Error, "PANEL NIVEL: 'availableUpgrades' está vacío, no repartirá cartas.");
        }
        else if (count < 3)
        {
            Add(findings, Severity.Warn, $"PANEL NIVEL: solo {count} mejora(s) para 3 cartas.");
        }

        if (IsNull(serialized, "upgradeManager"))
        {
            Add(findings, Severity.Warn,
                "PANEL NIVEL: 'upgradeManager' sin asignar (se autoresuelve, pero es frágil).");
        }
    }

    /// <summary>La viñeta de daño necesita su sprite para verse.</summary>
    private static void AuditDamageVignette(List<Finding> findings)
    {
        DamageVignetteUI vignette = Object.FindAnyObjectByType<DamageVignetteUI>(FindObjectsInactive.Include);

        if (vignette == null)
        {
            Add(findings, Severity.Warn, "VIÑETA: no hay DamageVignetteUI (el flash de daño no se verá).");
            return;
        }

        SerializedObject serialized = new SerializedObject(vignette);
        SerializedProperty image = serialized.FindProperty("vignetteImage");

        if (IsNull(serialized, "vignetteImage"))
        {
            Add(findings, Severity.Error, "VIÑETA: sin 'vignetteImage' asignada, no se verá nada.");
            return;
        }

        Image uiImage = image.objectReferenceValue as Image;

        if (uiImage != null && uiImage.sprite == null)
        {
            Add(findings, Severity.Error, "VIÑETA: la Image no tiene sprite (se verá un rectángulo plano).");
        }
    }

    // ======================================================================
    // HELPERS DE COMPROBACIÓN
    // ======================================================================

    /// <summary>Comprueba que un GameObject tenga un componente obligatorio.</summary>
    private static void Require(List<Finding> findings, GameObject owner, string scope, System.Type type)
    {
        if (owner.GetComponent(type) == null)
        {
            Add(findings, Severity.Error, $"{scope}: '{owner.name}' no tiene {type.Name}.");
        }
    }

    /// <summary>
    /// Verifica que las cartas tengan botón y textos, y que el array del componente no esté
    /// vacío (puede haber cartas daughters y el array sin rellenar: el síntoma clásico de
    /// "las cartas existen pero no hacen nada").
    /// </summary>
    /// <param name="cardsFieldName">
    /// NOMBRE REAL del array de cartas en ese panel. NO es el mismo en todos:
    /// <c>LevelUpUI</c> lo llama <c>upgradeCards</c> y <c>WeaponPanelUI</c> lo llama <c>cards</c>.
    /// Buscar siempre 'cards' daba un falso positivo en el panel de nivel, que sí estaba
    /// cableado correctamente.
    /// </param>
    private static void AuditCards(
        GameObject owner,
        UnityEngine.Object panelComponent,
        string cardsFieldName,
        string scope,
        List<Finding> findings)
    {
        UpgradeCardUI[] cards = owner.GetComponentsInChildren<UpgradeCardUI>(true);

        if (cards == null || cards.Length == 0)
        {
            Add(findings, Severity.Error, $"{scope}: '{owner.name}' no tiene ninguna UpgradeCardUI.");
            return;
        }

        SerializedObject panelSO = new SerializedObject(panelComponent);
        SerializedProperty array = panelSO.FindProperty(cardsFieldName);

        if (array == null)
        {
            Add(findings, Severity.Error,
                $"{scope}: el componente no expone el campo '{cardsFieldName}'; no se puede verificar el cableado.");
        }
        else if (array.arraySize == 0)
        {
            Add(findings, Severity.Error,
                $"{scope}: el campo '{cardsFieldName}' está vacío aunque hay {cards.Length} carta(s) en la " +
                $"jerarquía. Ejecuta la opción de auto-cableado.");
        }
        else if (array.arraySize != cards.Length)
        {
            Add(findings, Severity.Warn,
                $"{scope}: '{cardsFieldName}' tiene {array.arraySize} entrada(s) pero hay {cards.Length} " +
                "carta(s) en la jerarquía.");
        }

        for (int i = 0; i < cards.Length; i++)
        {
            if (cards[i] == null) continue;

            SerializedObject cardSO = new SerializedObject(cards[i]);

            if (IsNull(cardSO, "selectButton"))
            {
                Add(findings, Severity.Error, $"{scope}: carta '{cards[i].name}' sin 'selectButton' (no será pulsable).");
            }

            if (IsNull(cardSO, "titleText"))
            {
                Add(findings, Severity.Warn, $"{scope}: carta '{cards[i].name}' sin 'titleText' (se verá sin título).");
            }

            if (IsNull(cardSO, "descriptionText"))
            {
                Add(findings, Severity.Warn, $"{scope}: carta '{cards[i].name}' sin 'descriptionText'.");
            }
        }
    }

    /// <summary>El catálogo del panel de armas solo debe contener armas con asset vivo.</summary>
    private static void AuditWeaponCatalog(WeaponPanelUI panel, string scope, List<Finding> findings)
    {
        SerializedObject serialized = new SerializedObject(panel);
        SerializedProperty catalog = serialized.FindProperty("weaponUpgrades");

        if (catalog == null)
        {
            Add(findings, Severity.Warn, $"{scope}: el componente no expone el campo 'weaponUpgrades'.");
            return;
        }

        if (catalog.arraySize == 0)
        {
            Add(findings, Severity.Error, $"{scope}: 'weaponUpgrades' está vacío, no tendrá armas que ofrecer.");
            return;
        }

        int valid = 0;
        int notAWeapon = 0;

        for (int i = 0; i < catalog.arraySize; i++)
        {
            UpgradeDataSO upgrade = catalog.GetArrayElementAtIndex(i).objectReferenceValue as UpgradeDataSO;

            // Referencia a un asset borrado: el panel la ofrecería como carta vacía.
            if (upgrade == null)
            {
                Add(findings, Severity.Error, $"{scope}: entrada #{i} del catálogo apunta a un asset inexistente.");
                continue;
            }

            if (upgrade.grantsWeapon == null)
            {
                // No es grave (WeaponPanelUI lo filtra al abrir) pero es ruido que confunde.
                notAWeapon++;
            }
            else
            {
                valid++;
            }
        }

        if (valid == 0)
        {
            Add(findings, Severity.Error, $"{scope}: ninguna entrada del catálogo otorga un arma.");
        }
        else if (notAWeapon > 0)
        {
            Add(findings, Severity.Warn,
                $"{scope}: {notAWeapon} entrada(s) del catálogo no otorga arma; se descartarán al abrir el panel.");
        }
    }

    /// <summary>True si el campo no existe o apunta a null.</summary>

    // ======================================================================
    // INFORME
    // ======================================================================

    /// <summary>
    /// Imprime UN único bloque con todos los hallazgos. Se agrupan por severidad para que lo
    /// grave salte a la vista y lo leve se lea de un vistazo. Si todo está bien, una sola línea.
    /// </summary>
    private static void Report(List<Finding> findings)
    {
        int errors = 0;
        int warnings = 0;

        for (int i = 0; i < findings.Count; i++)
        {
            if (findings[i].Severity == Severity.Error) errors++;
            else if (findings[i].Severity == Severity.Warn) warnings++;
        }

        StringBuilder header = new StringBuilder(160);
        header.Append("──── Salud de la Escena: ");
        header.Append(errors).Append(" error(es), ");
        header.Append(warnings).Append(" aviso(s) ────");

        // Se informa del estado de silencio: si el usuario ve muy pocos logs durante la partida,
        // que sepa que es por esto y no por un fallo. Da uso real al flag y quita el CS0414.
        if (ConsoleCleanMode)
        {
            header.Append(" (🔇 logs informativos silenciados)");
        }

        // Requisito de diseño: si todo está bien, UN solo mensaje verde y nada más. El ruido
        // de la propia herramienta sería contradicción con el objetivo de una consola limpia.
        if (errors == 0 && warnings == 0)
        {
            Debug.Log($"<color=green>{header} Sin incidencias.</color>");
            return;
        }

        if (errors == 0)
        {
            Debug.LogWarning(header.ToString());
        }
        else
        {
            Debug.LogError(header.ToString());
        }

        for (int i = 0; i < findings.Count; i++)
        {
            Finding finding = findings[i];

            if (finding.Severity == Severity.Error)
            {
                Debug.LogError("  [ERROR] " + finding.Message);
            }
            else if (finding.Severity == Severity.Warn)
            {
                Debug.LogWarning("  [AVISO] " + finding.Message);
            }
        }
    }

    // ======================================================================
    // HELPERS DE COMPROBACIÓN
    // ======================================================================

    /// <summary>True si el campo no existe o apunta a null.</summary>
    private static bool IsNull(SerializedObject serialized, string field)
    {
        SerializedProperty property = serialized.FindProperty(field);

        return property == null || property.objectReferenceValue == null;
    }

    private static void Add(List<Finding> findings, Severity severity, string message)
    {
        findings.Add(new Finding(severity, message));
    }

    /// <summary>Busca un GameObject por nombre exacto, incluidos los inactivos.</summary>
    private static GameObject FindByName(string targetName)
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
}
