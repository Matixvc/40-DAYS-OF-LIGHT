using TMPro;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Configuración guiada del sistema de "Juice" desde el editor (menú 40 Days of Light > Juice).
/// Evita el error humano de montar el prefab a mano:
///   1) crea el prefab del número de daño (TextMeshPro World Space + DamageNumber),
///   2) añade CameraShake a la Main Camera de la escena abierta,
///   3) conecta el prefab con los HealthComponent de los enemigos y con el ObjectPoolManager.
/// Los pasos son idempotentes: si algo ya está hecho, solo lo avisa y no lo duplica.
/// </summary>
public static class JuiceSetupTools
{
    private const string MenuRoot = "40 Days of Light/Juice/";
    private const string DamageNumberPrefabPath = "Assets/DamageNumber.prefab";

    // Ajustes por defecto del prefab (se pueden retocar a mano después en el Inspector).
    private const string DamageNumberObjectName = "DamageNumber";
    private const float DefaultFontSize = 36f;
    private const float DefaultPrefabScale = 0.1f;
    private const int DefaultPrewarmCount = 15;
    private const int DefaultMaxSize = 60;

    [MenuItem(MenuRoot + "1. Crear prefab DamageNumber", false, 10)]
    private static void CreateDamageNumberPrefab()
    {
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(DamageNumberPrefabPath);

        if (existing != null)
        {
            Debug.LogWarning($"[Juice] Ya existe '{DamageNumberPrefabPath}'. Bórralo o edítalo a mano si quieres rehacerlo.", existing);
            Selection.activeObject = existing;
            EditorGUIUtility.PingObject(existing);
            return;
        }

        TMP_FontAsset font = TMP_Settings.defaultFontAsset;

        if (font == null)
        {
            Debug.LogError("[Juice] No hay fuente por defecto de TextMeshPro. Importa antes 'Window > TextMeshPro > Import TMP Essential Resources' y vuelve a ejecutarlo.");
            return;
        }

        GameObject root = new GameObject(DamageNumberObjectName);

        try
        {
            // La escala del prefab es el tamaño base del número (DamageNumber la usa para el "pop").
            root.transform.localScale = Vector3.one * DefaultPrefabScale;

            TextMeshPro text = root.AddComponent<TextMeshPro>();
            text.font = font;
            text.text = "0";
            text.fontSize = DefaultFontSize;
            text.alignment = TextAlignmentOptions.Center;
            text.overflowMode = TextOverflowModes.Overflow; // El número nunca se recorta
            text.color = new Color(1f, 0.93f, 0.62f, 1f);    // Dorado "Luz"
            text.sortingOrder = 100;                          // Por encima de los modelos
            text.rectTransform.sizeDelta = new Vector2(2f, 1f);

            DamageNumber damageNumber = root.AddComponent<DamageNumber>();

            SerializedObject serialized = new SerializedObject(damageNumber);
            serialized.FindProperty("textMesh").objectReferenceValue = text;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, DamageNumberPrefabPath);

            if (prefab == null)
            {
                Debug.LogError($"[Juice] No se pudo guardar el prefab en '{DamageNumberPrefabPath}'.");
                return;
            }

            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);

            Debug.Log(
                $"[Juice] Prefab creado en '{DamageNumberPrefabPath}'. " +
                "Si el número se ve demasiado grande o pequeño, ajusta 'Font Size' (TextMeshPro) " +
                "o la escala del prefab y guarda. Después ejecuta '3. Conectar DamageNumber con enemigos y pool'.",
                prefab);
        }
        finally
        {
            // El GameObject temporal solo se usó como molde para el prefab.
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    [MenuItem(MenuRoot + "2. Añadir CameraShake a la Main Camera", false, 11)]
    private static void AddCameraShake()
    {
        Camera mainCamera = Camera.main;

        if (mainCamera == null)
        {
            GameObject tagged = GameObject.FindGameObjectWithTag("MainCamera");

            if (tagged != null)
            {
                mainCamera = tagged.GetComponent<Camera>();
            }
        }

        if (mainCamera == null)
        {
            Debug.LogError("[Juice] No se encontró la Main Camera en la escena abierta. Ábrela y vuelve a ejecutar esta opción.");
            return;
        }

        CameraShake existing = mainCamera.GetComponent<CameraShake>();

        if (existing != null)
        {
            Debug.Log($"[Juice] La cámara '{mainCamera.name}' ya tiene CameraShake.", existing);
            Selection.activeGameObject = mainCamera.gameObject;
            return;
        }

        CameraShake shake = Undo.AddComponent<CameraShake>(mainCamera.gameObject);
        EditorUtility.SetDirty(shake);
        MarkActiveSceneDirty();

        Selection.activeGameObject = mainCamera.gameObject;

        Debug.Log(
            $"[Juice] CameraShake añadido a '{mainCamera.name}'. " +
            "La intensidad se ajusta en PlayerAttack (shakeDuration / shakeMagnitude). " +
            "Guarda la escena (Ctrl+S).",
            shake);
    }

    [MenuItem(MenuRoot + "3. Conectar DamageNumber con enemigos y pool", false, 12)]
    private static void ConnectDamageNumber()
    {
        GameObject damageNumberPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DamageNumberPrefabPath);

        if (damageNumberPrefab == null)
        {
            Debug.LogError($"[Juice] No existe '{DamageNumberPrefabPath}'. Ejecuta antes '1. Crear prefab DamageNumber'.");
            return;
        }

        int assigned = AssignToSelectedPrefabs(damageNumberPrefab);
        bool registered = RegisterInPool(damageNumberPrefab);

        Debug.Log(
            $"[Juice] Conexión terminada | prefabs de enemigo actualizados: {assigned} | " +
            $"pool de la escena: {(registered ? "entrada añadida" : "sin cambios")}. " +
            "Guarda la escena y el prefab modificado (Ctrl+S).");
    }

    /// <summary>
    /// Asigna el prefab del número de daño a los HealthComponent de los prefabs
    /// seleccionados en la ventana Project (por ejemplo, Enemy01.prefab).
    /// </summary>
    private static int AssignToSelectedPrefabs(GameObject damageNumberPrefab)
    {
        GameObject[] selectedPrefabs = Selection.GetFiltered<GameObject>(SelectionMode.Assets | SelectionMode.Editable);
        int assigned = 0;

        for (int i = 0; i < selectedPrefabs.Length; i++)
        {
            HealthComponent[] healthComponents = selectedPrefabs[i].GetComponentsInChildren<HealthComponent>(true);

            for (int h = 0; h < healthComponents.Length; h++)
            {
                SerializedObject serialized = new SerializedObject(healthComponents[h]);
                SerializedProperty prefabProperty = serialized.FindProperty("damageNumberPrefab");

                if (prefabProperty == null) continue;
                if (prefabProperty.objectReferenceValue == damageNumberPrefab) continue;

                prefabProperty.objectReferenceValue = damageNumberPrefab;
                serialized.ApplyModifiedProperties();

                EditorUtility.SetDirty(healthComponents[h]);
                assigned++;
            }
        }

        if (assigned > 0)
        {
            AssetDatabase.SaveAssets();
            return assigned;
        }

        Debug.LogWarning(
            "[Juice] Ningún prefab seleccionado necesitaba el número de daño. " +
            "Selecciona en la ventana Project los prefabs de enemigo (por ejemplo Enemy01.prefab) y vuelve a ejecutar esta opción.");

        return 0;
    }

    /// <summary>
    /// Añade el prefab al pool de la escena para que se recicle (cero GC) en lugar de usar el Instantiate de respaldo.
    /// </summary>
    private static bool RegisterInPool(GameObject damageNumberPrefab)
    {
        ObjectPoolManager pool = UnityEngine.Object.FindAnyObjectByType<ObjectPoolManager>(FindObjectsInactive.Include);

        if (pool == null)
        {
            Debug.LogWarning(
                "[Juice] La escena abierta no tiene ObjectPoolManager. Abre 'Assets/Scenes/Prototype.unity' y vuelve a ejecutar esta opción, " +
                "o añade el prefab a mano en la lista 'Pools' del ObjectPoolManager.");

            return false;
        }

        SerializedObject serialized = new SerializedObject(pool);
        SerializedProperty poolsProperty = serialized.FindProperty("pools");

        if (poolsProperty == null)
        {
            Debug.LogWarning("[Juice] El ObjectPoolManager no expone la lista 'pools': añade la entrada a mano.", pool);
            return false;
        }

        for (int i = 0; i < poolsProperty.arraySize; i++)
        {
            SerializedProperty prefabProperty = poolsProperty.GetArrayElementAtIndex(i).FindPropertyRelative("prefab");

            if (prefabProperty != null && prefabProperty.objectReferenceValue == damageNumberPrefab)
            {
                Debug.Log($"[Juice] '{damageNumberPrefab.name}' ya estaba registrado en el ObjectPoolManager.", pool);
                return false;
            }
        }

        poolsProperty.arraySize++;

        SerializedProperty newEntry = poolsProperty.GetArrayElementAtIndex(poolsProperty.arraySize - 1);
        newEntry.FindPropertyRelative("prefab").objectReferenceValue = damageNumberPrefab;
        newEntry.FindPropertyRelative("prewarmCount").intValue = DefaultPrewarmCount;
        newEntry.FindPropertyRelative("maxSize").intValue = DefaultMaxSize;

        serialized.ApplyModifiedProperties();

        EditorUtility.SetDirty(pool);
        MarkActiveSceneDirty();

        Debug.Log(
            $"[Juice] '{damageNumberPrefab.name}' registrado en el ObjectPoolManager " +
            $"(prewarm {DefaultPrewarmCount}, límite {DefaultMaxSize}).",
            pool);

        return true;
    }

    private static void MarkActiveSceneDirty()
    {
        if (Application.isPlaying)
        {
            return;
        }

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());
    }
}
