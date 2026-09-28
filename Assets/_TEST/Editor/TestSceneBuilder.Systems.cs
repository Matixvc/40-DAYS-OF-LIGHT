using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering.Universal;

public static partial class TestSceneBuilder
{
    /// <summary>
    /// Cámara principal con los mismos componentes que la de gameplay: CameraFollow y
    /// CameraShake. Sin <see cref="CameraShake"/> el sandbox no serviría para probar juice,
    /// que es media razón de existir de esta escena.
    /// </summary>
    internal static void BuildCamera(BuildContext context)
    {
        GameObject camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        TestSceneBuilderUtil.SetTag(camGo, "MainCamera");
        camGo.transform.position = new Vector3(0f, 12f, -8f);

        Camera camera = camGo.GetComponent<Camera>();
        camera.fieldOfView = 60f;
        camera.nearClipPlane = 0.3f;
        camera.farClipPlane = 200f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.08f, 0.09f, 0.12f, 1f);

        // URP exige este componente para funcionar; sin él se cae al error pipeline.
        camGo.AddComponent<UniversalAdditionalCameraData>();

        camGo.AddComponent<CameraFollow>();
        camGo.AddComponent<CameraShake>();

        context.Camera = camGo;
    }

    /// <summary>
    /// Crea los sistemas globales de la partida. El orden de los <c>DefaultExecutionOrder</c>
    /// del proyecto ya garantiza el arranque correcto: GameStateController (-1000) antes que
    /// PerformanceBootstrap (-900), PostProcessingBootstrap (-850) y ObjectPoolManager (-800).
    /// </summary>
    internal static void BuildSystems(BuildContext context)
    {
        GameObject systemsRoot = new GameObject("[Systems]");
        context.SystemsRoot = systemsRoot.transform;

        // --- Reloj y estado global: único dueño de Time.timeScale ---
        GameObject stateGo = CreateSystem(context, "GameStateController");
        context.StateController = stateGo.AddComponent<GameStateController>();
        TestSceneBuilderUtil.SetValue(context.StateController, "startPlayingDirectly", true);
        TestSceneBuilderUtil.SetValue(context.StateController, "normalTimeScale", 1f);

        // --- Rendimiento ---
        CreateSystem(context, "PerformanceBootstrap").AddComponent<PerformanceBootstrap>();

        // --- Pool ---
        GameObject poolGo = CreateSystem(context, "ObjectPoolManager");
        context.PoolManager = poolGo.AddComponent<ObjectPoolManager>();
        ConfigurePools(context);

        // --- Postprocesado URP ---
        GameObject postGo = CreateSystem(context, "PostProcessingBootstrap");
        context.PostProcessing = postGo.AddComponent<PostProcessingBootstrap>();

        if (context.Settings.postProcessProfile != null)
        {
            TestSceneBuilderUtil.SetValue(context.PostProcessing, "profile", context.Settings.postProcessProfile);
        }

        // --- Audio ---
        // Se deja con el AudioManager vacío a propósito. En Prototype, el AudioManager vive
        // en el mismo GameObject raíz que TODOS los managers del juego, y cualquier
        // intento de copiarlo (incluido Object.Instantiate sobre el componente) arrastra
        // también GameStateController, RunDirector, ObjectPoolManager... y produce seis
        // singletons duplicados que se autodestruyen al arrancar.
        //
        // No es un problema real: el AudioManager tiene fallbacks procedurales, y los SFX de
        // combate llegan con clips explícitos desde PlayerAttack y HealthComponent.
        // Lo que sí falta es la música y las variaciones del catálogo: se asignan a mano
        // en el Inspector si se echa de menos. Ver Assets/_TEST/README.md.
        context.AudioManagerPlaceholder = CreateSystem(context, "AudioManager");
        context.AudioManagerPlaceholder.AddComponent<AudioManager>();

        // --- Fin de partida y upgrades: se cablean en la fase de wiring ---
        context.GameManager = CreateSystem(context, "GameManager").AddComponent<GameManager>();
        context.UpgradeManager = CreateSystem(context, "UpgradeManager").AddComponent<UpgradeManager>();

        // --- Input ---
        // El Player copiado de Prototype ya trae su propio PlayerInputReader. Crear un
        // segundo dispararía el guardia de singleton: uno de los dos se desactiva y el
        // juego se quedaría apuntando al componente muerto (sin pausa ni reinicio).
        // Se reutiliza el del Player siempre que exista.
        context.InputReader = context.Player != null
            ? context.Player.GetComponentInChildren<PlayerInputReader>(true)
            : null;

        if (context.InputReader != null)
        {
            Debug.Log("[TestSceneBuilder] Se reutiliza el PlayerInputReader del Player (no se crea un duplicado).");
        }
        else
        {
            context.InputReader = CreateSystem(context, "PlayerInputReader").AddComponent<PlayerInputReader>();
        }

        if (context.Settings.inputActions != null)
        {
            TestSceneBuilderUtil.SetValue(context.InputReader, "actionsAsset", context.Settings.inputActions);
        }

        // --- Director de partida ---
        context.RunDirector = CreateSystem(context, "RunDirector").AddComponent<RunDirector>();
        TestSceneBuilderUtil.SetValue(context.RunDirector, "autoStartOnPlay", true);

        // En el sandbox la dificultad siempre es la del Inspector: heredar la del menú
        // principal haría que las pruebas dependieran de un PlayerPrefs.
        TestSceneBuilderUtil.SetValue(context.RunDirector, "useSavedDifficulty", false);

        // --- Atajos del sandbox ---
        context.HotkeyMap = CreateSystem(context, "TestHotkeyMap").AddComponent<TestHotkeyMap>();
    }

    private static GameObject CreateSystem(BuildContext context, string name)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(context.SystemsRoot, false);
        return go;
    }

    /// <summary>
    /// Configura los pools de la arena. El límite del enemigo sube a 400 a propósito:
    /// una horda de 100 más los enemigos del spawner continuo tienen que caber sin que
    /// el pool se quede corto en mitad de una prueba de rendimiento.
    /// </summary>
    private static void ConfigurePools(BuildContext context)
    {
        if (context.EnemyPrefab != null)
        {
            TestSceneBuilderUtil.SetPoolEntry(context.PoolManager, context.EnemyPrefab, 12, 400);
        }

        if (context.XpPrefab != null)
        {
            TestSceneBuilderUtil.SetPoolEntry(context.PoolManager, context.XpPrefab, 16, 120);
        }

        if (context.DamageNumberPrefab != null)
        {
            TestSceneBuilderUtil.SetPoolEntry(context.PoolManager, context.DamageNumberPrefab, 12, 100);
        }

        // Proyectiles de las armas del arquetipo Projectile. El prewarm es generoso a
        // propósito: una ráfaga de varias armas a máxima cadencia dispara muchos proyectiles
        // por segundo y no queremos ver un Instantiate en mitad de una prueba de rendimiento.
        if (context.WeaponProjectilePrefab != null)
        {
            TestSceneBuilderUtil.SetPoolEntry(context.PoolManager, context.WeaponProjectilePrefab, 16, 200);
        }
    }
}
