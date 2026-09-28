using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static partial class TestSceneBuilder
{
    /// <summary>
    /// Coloca al Player en la arena y normaliza su identidad (tag, capa, posición).
    /// No toca sus componentes: llegan cableados desde donde vengan.
    /// </summary>
    internal static void BuildPlayer(BuildContext context)
    {
        GameObject player = ResolvePlayer(context);

        if (player == null)
        {
            Debug.LogError(
                "[TestSceneBuilder] No se pudo resolver el Player. Ninguna de las tres vías funcionó. " +
                "Asigna un prefab en TestBuildSettings (Tools/40 Days of Light/Ajustes de la escena TEST).",
                null);
            return;
        }

        player.name = "Player_Dawit";
        TestSceneBuilderUtil.SetTag(player, "Player");
        TestSceneBuilderUtil.SetLayer(player, context.PlayerLayer);

        // Un pelo por encima del suelo: el CharacterController resuelve solo, pero evita
        // el primer frame de penetración al arrancar.
        player.transform.position = new Vector3(0f, 1f, 0f);
        player.transform.rotation = Quaternion.identity;

        context.Player = player;

        Debug.Log($"[TestSceneBuilder] Player listo ({context.PlayerSource}).", player);
    }

    /// <summary>
    /// Cascada de tres vías para obtener el Player, en el mismo espíritu que la resolución
    /// en cascada de <c>PlayerInputReader</c>:
    ///
    ///   1) Prefab asignado a mano en TestBuildSettings (la vía más fiable).
    ///   2) Búsqueda por nombre en el proyecto.
    ///   3) Copia de la jerarquía del Player desde Prototype.unity.
    ///
    /// La vía 3 es la que aplica hoy: el Player es un FBX (<c>Assets/Models/PlayerPastor.fbx</c>)
    /// montado dentro de la escena de gameplay, no un prefab. Prototype se abre aditivamente
    /// y se cierra SIN guardar, así que el juego real no se toca.
    /// </summary>
    private static GameObject ResolvePlayer(BuildContext context)
    {
        // --- Vía 1: prefab asignado en los ajustes ---
        if (context.Settings.playerPrefab != null)
        {
            GameObject instance = InstantiatePrefabSafely(context.Settings.playerPrefab, context.Scene);

            if (instance != null)
            {
                context.PlayerSource = $"prefab '{context.Settings.playerPrefab.name}' (ajustes manuales)";
                return instance;
            }
        }

        // --- Vía 2: cualquier prefab llamado "Player" en el proyecto ---
        GameObject found = FindPrefab("Player t:Prefab");

        if (found != null)
        {
            GameObject instance = InstantiatePrefabSafely(found, context.Scene);

            if (instance != null)
            {
                context.PlayerSource = $"prefab '{found.name}' (búsqueda por nombre)";
                return instance;
            }
        }

        // --- Vía 3: copiar la jerarquía desde Prototype.unity ---
        return CopyPlayerFromPrototype(context);
    }

    /// <summary>
    /// Abre Prototype.unity en aditivo, clona su jerarquía de Player y cierra la escena
    /// sin guardar. Es la única forma de obtener un Player correctamente cableado
    /// (Animator, RunStats, HealthComponent, PlayerAttack, daño...) sin reconstruirlo a mano.
    /// </summary>
    private static GameObject CopyPlayerFromPrototype(BuildContext context)
    {
        if (!PrototypeSceneExists)
        {
            Debug.LogError($"[TestSceneBuilder] No existe {PrototypeScenePath}: no hay de dónde copiar el Player.");
            return null;
        }

        Scene prototypeScene = EditorSceneManager.OpenScene(PrototypeScenePath, OpenSceneMode.Additive);
        GameObject source = FindPlayerInScene(prototypeScene);

        if (source == null)
        {
            EditorSceneManager.CloseScene(prototypeScene, true);
            Debug.LogError(
                $"[TestSceneBuilder] En {PrototypeScenePath} no se encontró ningún objeto raíz con el tag " +
                "'Player' ni con un PlayerController.",
                null);
            return null;
        }

        GameObject clone = Object.Instantiate(source);
        clone.name = "Player_Dawit";

        // Garantiza que el clon vive en la escena destino ANTES de cerrar la de origen.
        SceneManager.MoveGameObjectToScene(clone, context.Scene);

        // El nombre se lee ANTES de cerrar: al cerrar Prototype, 'source' queda destruido
        // y cualquier acceso posterior lanzaría MissingReferenceException.
        string sourceName = source.name;

        EditorSceneManager.CloseScene(prototypeScene, true);

        context.PlayerSource = $"copia de '{sourceName}' en {PrototypeScenePath}";

        Debug.Log(
            "[TestSceneBuilder] Las referencias que apuntaban a objetos de Prototype " +
            "(LevelUpPanel, EnemySpawner...) se recablean en la fase de wiring.",
            null);

        return clone;
    }

    /// <summary>Localiza la jerarquía del Player por tag y, si no aparece, por componente.</summary>
    private static GameObject FindPlayerInScene(Scene scene)
    {
        GameObject[] roots = scene.GetRootGameObjects();

        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i].CompareTag("Player"))
            {
                return roots[i];
            }
        }

        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i].GetComponentInChildren<PlayerController>(true) != null)
            {
                return roots[i];
            }
        }

        return null;
    }

    /// <summary>Instancia un prefab en la escena destino o devuelve null si no es posible.</summary>
    private static GameObject InstantiatePrefabSafely(GameObject prefab, Scene targetScene)
    {
        GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;

        if (instance == null)
        {
            return null;
        }

        if (instance.scene != targetScene)
        {
            SceneManager.MoveGameObjectToScene(instance, targetScene);
        }

        return instance;
    }

    /// <summary>
    /// Retirado a propósito. Ver el comentario en TestSceneBuilder.Systems.cs.
    ///
    /// En Prototype el AudioManager comparte GameObject raíz con TODOS los managers del
    /// juego, así que cualquier intento de clonarlo arrastra singletons duplicados:
    /// Object.Instantiate sobre el componente clona el GameObject entero con sus hijos,
    /// no sólo el componente. Se conserva el código como registro del intento fallido.
    /// </summary>
    private static void AdoptPrototypeAudioManager(BuildContext context, GameObject placeholder)
    {
        // Retirado a propósito. Si alguna vez hace falta, la vía segura es copiar los
        // campos del AudioManager de Prototype con SerializedObject/SerializedProperty
        // (no Object.Instantiate, que clona el GameObject entero y sus hijos), saltando
        // 'musicSource' por ser una referencia de escena.
        Debug.LogWarning(
            "[TestSceneBuilder] La adopción del AudioManager de Prototype está desactivada: " +
            "compartiría GameObject raíz con los demás managers. Asigna el catálogo a mano si lo necesitas.",
            null);
    }
}
