using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

public static partial class TestSceneBuilder
{
    private const float GroundSize = 60f;      // Plano de 10x10 escalado.
    private const float WallRingSize = 40f;    // Cuadrado interior por el que se mueve el jugador.
    private const float WallHeight = 3f;
    private const float WallThickness = 1f;
    private const string ArenaMaterialPath = "Assets/_TEST/Materials/ArenaFloor.mat";

    /// <summary>
    /// Crea la arena de pruebas: suelo, muro perimetral y la superficie de NavMesh.
    ///
    /// El NavMesh se hornea en RUNTIME mediante <see cref="TestNavMeshGate"/>, no al
    /// guardar la escena. Es una decisión consciente: permite iterar la geometría en Play
    /// Mode y rehornear con un clic, a cambio de un único frame de coste al entrar.
    /// </summary>
    internal static void BuildArena(BuildContext context)
    {
        GameObject arenaRoot = new GameObject("[Arena]");
        context.ArenaRoot = arenaRoot.transform;

        Material floorMaterial = GetOrCreateFloorMaterial();

        // --- Suelo: su MeshCollider es la fuente de la geometría del NavMesh ---
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.SetParent(arenaRoot.transform, false);
        ground.transform.localScale = new Vector3(GroundSize / 10f, 1f, GroundSize / 10f);
        TestSceneBuilderUtil.SetLayer(ground, context.TerrainLayer);

        if (floorMaterial != null && ground.TryGetComponent(out MeshRenderer groundRenderer))
        {
            groundRenderer.sharedMaterial = floorMaterial;
        }

        // --- Muros: mantienen la horda dentro de la arena ---
        BuildWalls(context, arenaRoot.transform, floorMaterial);

        // --- Superficie de NavMesh ---
        GameObject navGo = new GameObject("NavMesh");
        navGo.transform.SetParent(arenaRoot.transform, false);

        NavMeshSurface surface = navGo.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.All;
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;

        // Sólo se recoge la capa Terreno: así los enemigos, el jugador y los VFX con
        // colisión propia nunca se convierten en obstáculo.
        surface.layerMask = 1 << context.TerrainLayer;
        surface.defaultArea = 0;
        surface.overrideTileSize = false;
        surface.overrideVoxelSize = false;

        context.NavMeshGate = navGo.AddComponent<TestNavMeshGate>();
        TestSceneBuilderUtil.SetValue(context.NavMeshGate, "surface", surface);

        Debug.Log(
            $"[TestSceneBuilder] Arena creada: suelo {GroundSize}x{GroundSize}, muro {WallRingSize}x{WallRingSize}, " +
            $"NavMesh sobre la capa {LayerMask.LayerToName(context.TerrainLayer)} (horneado en runtime).",
            navGo);
    }

    /// <summary>
    /// Muro perimetral. Se deja como geometría normal: el NavMesh lo usa como obstáculo y
    /// los agentes no pueden escalarlo, porque el step height del proyecto es 0.4.
    /// </summary>
    private static void BuildWalls(BuildContext context, Transform parent, Material material)
    {
        Transform wallsRoot = new GameObject("Walls").transform;
        wallsRoot.SetParent(parent, false);

        float half = WallRingSize * 0.5f;
        float length = WallRingSize + WallThickness * 2f;

        CreateWall(context, wallsRoot, material, "Wall_North", new Vector3(0f, WallHeight * 0.5f, half),
            new Vector3(length, WallHeight, WallThickness));
        CreateWall(context, wallsRoot, material, "Wall_South", new Vector3(0f, WallHeight * 0.5f, -half),
            new Vector3(length, WallHeight, WallThickness));
        CreateWall(context, wallsRoot, material, "Wall_East", new Vector3(half, WallHeight * 0.5f, 0f),
            new Vector3(WallThickness, WallHeight, length));
        CreateWall(context, wallsRoot, material, "Wall_West", new Vector3(-half, WallHeight * 0.5f, 0f),
            new Vector3(WallThickness, WallHeight, length));
    }

    private static void CreateWall(BuildContext context, Transform parent, Material material, string name, Vector3 position, Vector3 scale)
    {
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = name;
        wall.transform.SetParent(parent, false);
        wall.transform.localPosition = position;
        wall.transform.localScale = scale;
        TestSceneBuilderUtil.SetLayer(wall, context.TerrainLayer);

        if (material != null && wall.TryGetComponent(out MeshRenderer wallRenderer))
        {
            wallRenderer.sharedMaterial = material;
        }
    }

    /// <summary>
    /// Material de la arena. Vive en Assets/_TEST/Materials para que la escena no salga
    /// magenta por el material por defecto de Unity bajo URP, y para no contaminar
    /// Assets/Materials del juego real.
    /// </summary>
    private static Material GetOrCreateFloorMaterial()
    {
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(ArenaMaterialPath);

        if (existing != null)
        {
            return existing;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

        if (shader == null)
        {
            Debug.LogWarning("[TestSceneBuilder] No se encontró shader de URP ni Standard para la arena.");
            return null;
        }

        Material material = new Material(shader) { name = "ArenaFloor" };
        material.color = new Color(0.36f, 0.34f, 0.30f, 1f);

        if (material.HasProperty("_Smoothness"))
        {
            material.SetFloat("_Smoothness", 0.05f);
        }

        if (!AssetDatabase.IsValidFolder("Assets/_TEST/Materials"))
        {
            AssetDatabase.CreateFolder("Assets/_TEST", "Materials");
        }

        AssetDatabase.CreateAsset(material, ArenaMaterialPath);
        return material;
    }

    /// <summary>Luz direccional mínima para ver la arena.</summary>
    internal static void BuildLighting(BuildContext context)
    {
        GameObject lightGo = new GameObject("Directional Light");
        context.LightingRoot = lightGo.transform;

        Light light = lightGo.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = new Color(1f, 0.96f, 0.88f);
        light.intensity = 1.15f;
        light.shadows = LightShadows.Soft;

        lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
    }
}
