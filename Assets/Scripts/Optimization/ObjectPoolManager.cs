using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pool de objetos genérico y único de la partida (enemigos, gemas de XP y VFX).
/// Sustituye Instantiate/Destroy durante el gameplay para no generar basura (GC) ni tirones en Android.
/// Vive en la ESCENA DE GAMEPLAY: al recargar la escena se destruye junto con sus instancias.
/// </summary>
[DefaultExecutionOrder(-800)]
[DisallowMultipleComponent]
public class ObjectPoolManager : MonoBehaviour
{
    public static ObjectPoolManager Instance { get; private set; }

    [Serializable]
    public class PoolDefinition
    {
        [Tooltip("Prefab que se recicla.")]
        public GameObject prefab;

        [Tooltip("Instancias creadas al arrancar la partida (evita el tirón del primer spawn).")]
        public int prewarmCount = 20;

        [Tooltip("Máximo de instancias simultáneas de este prefab (0 = sin límite).")]
        public int maxSize = 200;

        [NonSerialized] public List<GameObject> available = new List<GameObject>();
        [NonSerialized] public HashSet<GameObject> active = new HashSet<GameObject>();
    }

    [Header("Pools de la partida")]
    [Tooltip("Arrastra aquí el prefab del enemigo, el de la gema de XP y los VFX que quieras reciclar.")]
    [SerializeField] private List<PoolDefinition> pools = new List<PoolDefinition>();

    [Header("Arranque")]
    [SerializeField] private bool prewarmOnStart = true;

    [Header("Depuración")]
    [SerializeField] private bool logPoolActivity = false;
    [SerializeField] private bool logWarnings = true;

    // Propietario permanente de cada instancia (nunca se borra hasta que la instancia muere).
    private readonly Dictionary<GameObject, PoolDefinition> poolByInstance = new Dictionary<GameObject, PoolDefinition>();
    private readonly Dictionary<GameObject, PoolDefinition> poolByPrefab = new Dictionary<GameObject, PoolDefinition>();
    private readonly Dictionary<GameObject, IPooledObject[]> resettablesByInstance = new Dictionary<GameObject, IPooledObject[]>();

    private Transform poolRoot;

    /// <summary>True si la instancia pertenece a este pool (se puede reciclar en lugar de destruir).</summary>
    public bool IsPooled(GameObject instance)
    {
        return instance != null && poolByInstance.ContainsKey(instance);
    }

    /// <summary>Número de instancias activas de un prefab (0 si no hay pool). Cero asignaciones: no usa Find.</summary>
    public int GetActiveCount(GameObject prefab)
    {
        if (prefab != null && poolByPrefab.TryGetValue(prefab, out PoolDefinition pool) && pool != null && pool.active != null)
        {
            return pool.active.Count;
        }

        return 0;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[ObjectPool] Ya existe un ObjectPoolManager activo: se desactiva el duplicado.", this);
            enabled = false;
            return;
        }

        Instance = this;

        poolRoot = new GameObject("PooledObjects").transform;
        poolRoot.SetParent(transform, false);

        BuildIndex();
    }

    private void Start()
    {
        if (prewarmOnStart)
        {
            PrewarmAll();
        }
    }

    private void OnDestroy()
    {
        // Al descargar la escena dejamos el pool limpio: nada activo y sin referencias colgando.
        ReleaseAll();

        if (Instance == this)
        {
            Instance = null;
        }
    }

    // ======================================================================
    // PREPARACIÓN
    // ======================================================================

    private void BuildIndex()
    {
        poolByPrefab.Clear();

        for (int i = 0; i < pools.Count; i++)
        {
            PoolDefinition pool = pools[i];

            if (pool == null || pool.prefab == null)
            {
                if (logWarnings) Debug.LogWarning($"[ObjectPool] El pool #{i} no tiene prefab asignado.", this);
                continue;
            }

            if (pool.available == null) pool.available = new List<GameObject>();
            if (pool.active == null) pool.active = new HashSet<GameObject>();

            poolByPrefab[pool.prefab] = pool;
        }
    }

    private void PrewarmAll()
    {
        for (int i = 0; i < pools.Count; i++)
        {
            PoolDefinition pool = pools[i];

            if (pool == null || pool.prefab == null) continue;

            Prewarm(pool);
        }
    }

    private void Prewarm(PoolDefinition pool)
    {
        int target = Mathf.Max(0, pool.prewarmCount);

        while (pool.available.Count < target)
        {
            GameObject instance = CreateInstance(pool);

            if (instance == null) break;

            pool.available.Add(instance);
        }

        if (logPoolActivity)
        {
            Debug.Log($"[ObjectPool] Prewarm '{pool.prefab.name}': {pool.available.Count} instancias listas.", this);
        }
    }

    // ======================================================================
    // API PÚBLICA
    // ======================================================================

    /// <summary>Saca una instancia del pool (la crea si hace falta) y la activa en la posición indicada.</summary>
    public GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (prefab == null) return null;

        PoolDefinition pool = GetOrCreatePool(prefab);
        GameObject instance = TakeInstance(pool);

        if (instance == null)
        {
            if (logWarnings)
            {
                Debug.LogWarning(
                    $"[ObjectPool] '{prefab.name}' alcanzó su límite de {pool.maxSize} instancias simultáneas.",
                    this);
            }

            return null;
        }

        instance.transform.SetPositionAndRotation(position, rotation);
        instance.SetActive(true);

        pool.active.Add(instance);
        poolByInstance[instance] = pool;

        NotifyResettables(instance, spawned: true);

        if (logPoolActivity)
        {
            Debug.Log($"[ObjectPool] Spawn '{prefab.name}' | activos: {pool.active.Count} | libres: {pool.available.Count}", instance);
        }

        return instance;
    }

    /// <summary>Genera un efecto temporal que vuelve al pool solo (partículas, impactos, destellos).</summary>
    public GameObject SpawnEffect(GameObject prefab, Vector3 position, Quaternion rotation, float lifetime)
    {
        GameObject instance = Spawn(prefab, position, rotation);

        if (instance != null && lifetime > 0f)
        {
            StartCoroutine(ReturnToPoolAfter(instance, lifetime));
        }

        return instance;
    }

    /// <summary>Devuelve una instancia al pool: la desactiva y la deja lista para reutilizar.</summary>
    public bool Despawn(GameObject instance)
    {
        if (instance == null) return false;

        if (!poolByInstance.TryGetValue(instance, out PoolDefinition pool))
        {
            // No pertenece al pool: se destruye para no dejar basura en la escena.
            if (logWarnings)
            {
                Debug.LogWarning($"[ObjectPool] '{instance.name}' no pertenece al pool: se destruye.", instance);
            }

            Destroy(instance);
            return false;
        }

        if (!pool.active.Remove(instance))
        {
            // Ya estaba devuelta: se ignora para no duplicar entradas en la lista de disponibles.
            if (logWarnings)
            {
                Debug.LogWarning($"[ObjectPool] '{instance.name}' ya estaba en el pool (doble devolución ignorada).", instance);
            }

            return false;
        }

        NotifyResettables(instance, spawned: false);

        instance.SetActive(false);

        if (poolRoot != null)
        {
            instance.transform.SetParent(poolRoot, false);
        }

        pool.available.Add(instance);

        if (logPoolActivity)
        {
            Debug.Log($"[ObjectPool] Despawn '{instance.name}' | activos: {pool.active.Count} | libres: {pool.available.Count}", instance);
        }

        return true;
    }

    /// <summary>Devuelve al pool TODAS las instancias activas (reinicio de partida o cambio de escena).</summary>
    public void ReleaseAll()
    {
        List<GameObject> instances = new List<GameObject>(poolByInstance.Keys);

        for (int i = 0; i < instances.Count; i++)
        {
            Despawn(instances[i]); // Ignora los que ya estaban devueltos
        }
    }

    public int GetAvailableCount(GameObject prefab)
    {
        return poolByPrefab.TryGetValue(prefab, out PoolDefinition pool) ? pool.available.Count : 0;
    }

    // ======================================================================
    // INTERNO
    // ======================================================================

    private PoolDefinition GetOrCreatePool(GameObject prefab)
    {
        if (poolByPrefab.TryGetValue(prefab, out PoolDefinition existing))
        {
            return existing;
        }

        PoolDefinition pool = new PoolDefinition
        {
            prefab = prefab,
            prewarmCount = 0,
            maxSize = 0 // Sin límite para prefabs no configurados
        };

        pools.Add(pool);
        poolByPrefab[prefab] = pool;

        if (logWarnings)
        {
            Debug.LogWarning(
                $"[ObjectPool] El prefab '{prefab.name}' no estaba en la lista: se creó un pool dinámico sin límite. " +
                "Añádelo al ObjectPoolManager para controlar el prewarm y el límite.",
                this);
        }

        return pool;
    }

    private GameObject TakeInstance(PoolDefinition pool)
    {
        while (pool.available.Count > 0)
        {
            int lastIndex = pool.available.Count - 1;
            GameObject candidate = pool.available[lastIndex];
            pool.available.RemoveAt(lastIndex);

            if (candidate != null)
            {
                return candidate;
            }
        }

        bool hasLimit = pool.maxSize > 0;

        if (hasLimit && pool.active.Count >= pool.maxSize)
        {
            return null;
        }

        return CreateInstance(pool);
    }

    private GameObject CreateInstance(PoolDefinition pool)
    {
        if (pool.prefab == null) return null;

        GameObject instance = Instantiate(pool.prefab, poolRoot);
        instance.SetActive(false);

        poolByInstance[instance] = pool;
        resettablesByInstance[instance] = instance.GetComponentsInChildren<IPooledObject>(true);

        return instance;
    }

    private void NotifyResettables(GameObject instance, bool spawned)
    {
        if (!resettablesByInstance.TryGetValue(instance, out IPooledObject[] resettables) || resettables == null)
        {
            return;
        }

        for (int i = 0; i < resettables.Length; i++)
        {
            IPooledObject resettable = resettables[i];

            if (resettable == null) continue; // Componente eliminado del prefab

            if (spawned)
            {
                resettable.OnPoolSpawned();
            }
            else
            {
                resettable.OnPoolDespawned();
            }
        }
    }

    private IEnumerator ReturnToPoolAfter(GameObject instance, float seconds)
    {
        yield return new WaitForSeconds(seconds);
        Despawn(instance);
    }

    [ContextMenu("Registrar estado del pool")]
    private void DebugPoolState()
    {
        for (int i = 0; i < pools.Count; i++)
        {
            PoolDefinition pool = pools[i];

            if (pool == null || pool.prefab == null) continue;

            Debug.Log(
                $"[ObjectPool] '{pool.prefab.name}' | activos: {pool.active.Count} | libres: {pool.available.Count} | límite: {pool.maxSize}",
                this);
        }
    }

    [ContextMenu("Devolver todo al pool")]
    private void DebugReleaseAll()
    {
        ReleaseAll();
        Debug.Log("[ObjectPool] Todas las instancias activas se han devuelto al pool.", this);
    }
}
