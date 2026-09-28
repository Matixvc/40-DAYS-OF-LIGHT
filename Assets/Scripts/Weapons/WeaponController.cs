using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gestor de armas del jugador (Fase 3).
///
/// Por qué existe: <see cref="PlayerAttack"/> resuelve UNA sola arma (un pulso de área fijo).
/// Un roguelite necesita varias activas a la vez, con arquetipos distintos y niveles
/// acumulables. Este componente mantiene la lista, crea el comportamiento de cada arquetipo
/// y delega en él TODO lo demás: aquí no se calcula daño ni se consulta la física.
///
/// RETROCOMPATIBILIDAD: <see cref="useNewWeaponSystem"/> arranca en <c>false</c> y, mientras
/// lo esté, este componente no hace absolutamente nada y el juego sigue usando el pulso de
/// <see cref="PlayerAttack"/>. El generador de la escena TEST lo pone a <c>true</c> para
/// probar las armas nuevas sin tocar Prototype.unity.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Gameplay/Weapon Controller")]
public class WeaponController : MonoBehaviour
{
    /// <summary>Tope duro de ranuras, para que un asset o un script externo no lo desborde.</summary>
    public const int MaxSlotsLimit = 12;

    /// <summary>Nivel máximo de un arma: a partir de ahí, recibirla otra vez no hace nada.</summary>
    public const int MaxWeaponLevel = 8;

    /// <summary>Una entrada de la barra de armas: configuración inmutable + estado de partida.</summary>
    private sealed class WeaponRuntime
    {
        public WeaponDataSO Data;
        public int Level = 1;
        public GameObject Host;
        public IWeaponBehaviour Behaviour;
    }

    [Header("Referencias")]
    [Tooltip("Estadísticas de partida. Si se deja vacío se resuelve en Awake.")]
    [SerializeField] private RunStats runStats;

    [Tooltip("Pulso original. Se DESACTIVA mientras el sistema nuevo esté en marcha para no " +
             "aplicar el daño dos veces.")]
    [SerializeField] private PlayerAttack legacyPulse;

    [Header("Sistema de armas v2 (Fase 3)")]
    [Tooltip("Cambia el sistema de armas. En false el juego usa el pulso de PlayerAttack y este " +
             "componente queda inerte: es el valor por defecto para no alterar Prototype.")]
    [SerializeField] private bool useNewWeaponSystem = false;

    [Tooltip("Armas equipadas al arrancar en Play Mode.")]
    [SerializeField] private WeaponDataSO[] startingWeapons = new WeaponDataSO[0];

    [SerializeField, Min(1)] private int maxWeaponSlots = 6;

    [Tooltip("Daño extra por nivel del arma: 0.15 = +15% del daño base por nivel.")]
    [SerializeField, Min(0f)] private float damagePerWeaponLevel = 0.15f;

    [Tooltip("Reducción de cadencia por nivel del arma: 0.06 = -6% por nivel.")]
    [SerializeField, Min(0f)] private float cooldownReductionPerWeaponLevel = 0.06f;

    [Tooltip("Capa(s) que cuentan como enemigo para todas las armas.")]
    [SerializeField] private LayerMask enemyLayer;

    [Header("Depuración")]
    [SerializeField] private bool logWeapons = true;

    /// <summary>Se lanza cuando cambia la barra de armas (equipar, subir nivel o retirar).</summary>
    public event Action OnLoadoutChanged;

    // Lista preasignada con la capacidad máxima: equipar un arma durante la partida no reserva memoria.
    private readonly List<WeaponRuntime> weapons = new List<WeaponRuntime>(MaxSlotsLimit);

    /// <summary>True si el sistema nuevo está gestionando el combate del jugador.</summary>
    public bool UseNewWeaponSystem => useNewWeaponSystem;

    /// <summary>Estadísticas de partida (null si el Player no tiene RunStats).</summary>
    public RunStats Stats => runStats;

    /// <summary>Máscara de capa de enemigos compartida por todas las armas.</summary>
    public LayerMask EnemyLayer => enemyLayer;

    /// <summary>Número de armas equipadas.</summary>
    public int WeaponCount => weapons.Count;

    /// <summary>
    /// True si las armas están activas. Apagarlas no quita el loadout: es el interruptor de
    /// pruebas (F7) y sirve para comparar con el pulso original sin perder las armas equipadas.
    /// </summary>
    public bool WeaponsActive { get; private set; } = true;

    private void Awake()
    {
        ResolveReferences(false);
    }

    private void OnEnable()
    {
        SubscribeToStats();
    }

    private void Start()
    {
        // En Start todos los Awake de la escena han terminado: es el punto seguro para
        // resolver RunStats aunque viva en otro GameObject (misma regla que PlayerController).
        ResolveReferences(true);
        SubscribeToStats();

        EquipStartingWeapons();
        ApplySystemState();
    }

    private void OnDisable()
    {
        if (runStats != null)
        {
            runStats.OnStatsChanged -= HandleStatsChanged;
        }
    }

    private void OnDestroy()
    {
        // No se destruyen hosts ni visuales: son hijos de este GameObject y caen con él.
        // Solo se sueltan las referencias para que ningún comportamiento quede vivo apuntando
        // al controlador (evita MissingReferenceException al cargar la siguiente escena).
        for (int i = 0; i < weapons.Count; i++)
        {
            WeaponRuntime runtime = weapons[i];

            if (runtime.Behaviour != null)
            {
                runtime.Behaviour.Dispose();
            }

            runtime.Behaviour = null;
            runtime.Host = null;
        }

        weapons.Clear();
    }

    private void Update()
    {
        if (!useNewWeaponSystem || !WeaponsActive || weapons.Count == 0)
        {
            return;
        }

        float deltaTime = Time.deltaTime;

        for (int i = 0; i < weapons.Count; i++)
        {
            IWeaponBehaviour behaviour = weapons[i].Behaviour;

            if (behaviour != null)
            {
                behaviour.Tick(deltaTime);
            }
        }
    }

    // ==================================================================
    // API pública
    // ==================================================================

    /// <summary>
    /// Activa o desactiva el sistema nuevo de armas. Al activarlo se apaga el pulso original
    /// (y al revés): si ambos estuvieran activos, cada ataque haría daño dos veces.
    /// </summary>
    public void SetUseNewWeaponSystem(bool value)
    {
        if (useNewWeaponSystem == value)
        {
            return;
        }

        useNewWeaponSystem = value;
        ApplySystemState();

        if (logWeapons)
        {
            Debug.Log(
                $"[WeaponController] Sistema de armas: {(value ? "NUEVO" : "CLASICO (PlayerAttack)")} | " +
                $"{weapons.Count} arma(s) equipada(s).",
                this);
        }
    }

    public void ToggleWeaponSystem() => SetUseNewWeaponSystem(!useNewWeaponSystem);

    /// <summary>Enciende o apaga las armas sin perder el loadout.</summary>
    public void SetWeaponsActive(bool value)
    {
        WeaponsActive = value;

        for (int i = 0; i < weapons.Count; i++)
        {
            WeaponBehaviourBase behaviour = weapons[i].Behaviour as WeaponBehaviourBase;

            if (behaviour != null)
            {
                behaviour.SetVisualsActive(value);
            }
        }

        if (logWeapons)
        {
            Debug.Log($"[WeaponController] Armas: {(value ? "ACTIVAS" : "APAGADAS")}.", this);
        }
    }

    public void ToggleWeaponsActive() => SetWeaponsActive(!WeaponsActive);

    /// <summary>True si el arma ya está equipada.</summary>
    public bool HasWeapon(WeaponDataSO data)
    {
        return FindWeapon(data) != null;
    }

    /// <summary>Nivel del arma equipada (0 si no está).</summary>
    public int GetWeaponLevel(WeaponDataSO data)
    {
        WeaponRuntime runtime = FindWeapon(data);
        return runtime != null ? runtime.Level : 0;
    }

    /// <summary>
    /// Equipa un arma nueva o sube de nivel una ya equipada. Es el único punto de entrada de
    /// armas: lo usan <see cref="UpgradeManager"/> (mejoras con grantsWeapon), el sandbox y
    /// cualquier sistema futuro, para que las reglas de ranuras y nivel se apliquen siempre igual.
    /// </summary>
    /// <returns><c>true</c> si la barra de armas cambió.</returns>
    public bool EquipWeapon(WeaponDataSO data)
    {
        if (data == null)
        {
            Debug.LogWarning("[WeaponController] Se intentó equipar un WeaponDataSO nulo.", this);
            return false;
        }

        WeaponRuntime existing = FindWeapon(data);

        if (existing != null)
        {
            if (existing.Level >= MaxWeaponLevel)
            {
                if (logWeapons)
                {
                    Debug.Log(
                        $"[WeaponController] '{data.weaponName}' ya está al nivel máximo ({MaxWeaponLevel}).",
                        this);
                }

                return false;
            }

            existing.Level++;

            if (existing.Behaviour != null)
            {
                existing.Behaviour.SetLevel(existing.Level);
            }

            if (logWeapons)
            {
                Debug.Log($"[WeaponController] '{data.weaponName}' sube a nivel {existing.Level}.", this);
            }

            OnLoadoutChanged?.Invoke();
            return true;
        }

        int slots = Mathf.Min(maxWeaponSlots, MaxSlotsLimit);

        if (weapons.Count >= slots)
        {
            Debug.LogWarning(
                $"[WeaponController] No caben más armas ({weapons.Count}/{slots}). " +
                $"'{data.weaponName}' no se ha equipado: amplía 'Max Weapon Slots' o retira otra arma.",
                this);
            return false;
        }

        WeaponRuntime created = CreateWeaponRuntime(data);

        if (created == null)
        {
            return false;
        }

        weapons.Add(created);

        // Un arma recién equipada debe respetar el interruptor: si las armas están apagadas,
        // no puede aparecer un orbe nuevo girando alrededor del jugador.
        if (created.Behaviour is WeaponBehaviourBase baseBehaviour)
        {
            baseBehaviour.SetVisualsActive(WeaponsActive);
        }

        if (logWeapons)
        {
            Debug.Log(
                $"<color=green>[WeaponController] Arma equipada: {data.weaponName} ({data.archetype}) | " +
                $"{weapons.Count}/{slots} ranuras</color>",
                this);
        }

        OnLoadoutChanged?.Invoke();
        return true;
    }

    /// <summary>Retira un arma del loadout (no afecta a las demás).</summary>
    public bool RemoveWeapon(WeaponDataSO data)
    {
        WeaponRuntime runtime = FindWeapon(data);

        if (runtime == null)
        {
            return false;
        }

        DisposeRuntime(runtime);
        weapons.Remove(runtime);

        if (logWeapons)
        {
            Debug.Log($"[WeaponController] Arma retirada: {data.weaponName}.", this);
        }

        OnLoadoutChanged?.Invoke();
        return true;
    }

    /// <summary>Retira todas las armas (nueva partida, reinicio del sandbox...).</summary>
    public void ClearWeapons()
    {
        if (weapons.Count == 0)
        {
            return;
        }

        for (int i = 0; i < weapons.Count; i++)
        {
            DisposeRuntime(weapons[i]);
        }

        weapons.Clear();
        OnLoadoutChanged?.Invoke();
    }

    /// <summary>Multiplicador de daño por nivel de arma (lo consultan los comportamientos).</summary>
    public float GetWeaponDamageMultiplier(int level)
    {
        return 1f + Mathf.Max(0, level - 1) * damagePerWeaponLevel;
    }

    /// <summary>Multiplicador de cadencia por nivel de arma (&lt;1 = más rápido).</summary>
    public float GetWeaponCooldownMultiplier(int level)
    {
        return Mathf.Max(0.25f, 1f - Mathf.Max(0, level - 1) * cooldownReductionPerWeaponLevel);
    }

    /// <summary>Resumen del loadout para el panel de depuración.</summary>
    public string GetLoadoutSummary()
    {
        if (weapons.Count == 0)
        {
            return "sin armas";
        }

        System.Text.StringBuilder summary = new System.Text.StringBuilder(96);

        for (int i = 0; i < weapons.Count; i++)
        {
            if (i > 0)
            {
                summary.Append(" · ");
            }

            summary.Append(weapons[i].Data.weaponName).Append(" Nv").Append(weapons[i].Level);
        }

        return summary.ToString();
    }

    // ==================================================================
    // Interno
    // ==================================================================

    /// <summary>
    /// Resuelve RunStats y el pulso heredado. <c>logIfMissing</c> se activa en Start: en Awake
    /// el orden entre GameObjects no está garantizado y un aviso prematuro sería ruido.
    /// </summary>
    private void ResolveReferences(bool logIfMissing)
    {
        if (runStats == null)
        {
            runStats = GetComponent<RunStats>();
        }

        if (runStats == null)
        {
            runStats = RunStats.Active;
        }

        if (legacyPulse == null)
        {
            legacyPulse = GetComponent<PlayerAttack>();
        }

        if (logIfMissing)
        {
            if (runStats == null)
            {
                Debug.LogError(
                    "[WeaponController] No se encontró RunStats: las armas usarán solo los valores de sus " +
                    "ScriptableObjects y las mejoras de estadísticas NO tendrán efecto.",
                    this);
            }

            if (useNewWeaponSystem && enemyLayer.value == 0)
            {
                Debug.LogError(
                    "[WeaponController] 'Enemy Layer' está vacío: ninguna arma encontrará enemigos. " +
                    "Asigna la capa Enemy en el Inspector.",
                    this);
            }
        }
    }

    private void SubscribeToStats()
    {
        if (runStats == null)
        {
            return;
        }

        // -= antes de += : evita suscripciones duplicadas si OnEnable se llama más de una vez.
        runStats.OnStatsChanged -= HandleStatsChanged;
        runStats.OnStatsChanged += HandleStatsChanged;
    }

    /// <summary>
    /// Las armas leen sus estadísticas en cada Tick, así que aquí no hay que recalcular nada:
    /// basta con no perder la suscripción. Existe para que el contrato quede explícito y para
    /// poder añadir efectos de "mejora aplicada" sin tocar los comportamientos.
    /// </summary>
    private void HandleStatsChanged()
    {
        // Intencionadamente vacío.
    }

    /// <summary>
    /// Aplica el modo de combate: nuevo sistema o pulso heredado. Nunca los dos a la vez.
    /// </summary>
    private void ApplySystemState()
    {
        if (legacyPulse != null)
        {
            legacyPulse.enabled = !useNewWeaponSystem;
        }
    }

    private void EquipStartingWeapons()
    {
        if (startingWeapons == null || startingWeapons.Length == 0)
        {
            return;
        }

        for (int i = 0; i < startingWeapons.Length; i++)
        {
            if (startingWeapons[i] != null)
            {
                EquipWeapon(startingWeapons[i]);
            }
        }
    }

    private WeaponRuntime FindWeapon(WeaponDataSO data)
    {
        if (data == null)
        {
            return null;
        }

        for (int i = 0; i < weapons.Count; i++)
        {
            if (weapons[i].Data == data)
            {
                return weapons[i];
            }
        }

        return null;
    }

    /// <summary>
    /// Crea el host del arma (hijo del jugador) y le añade el comportamiento de su arquetipo.
    /// El host es un GameObject propio para que el arma tenga su transform independiente y
    /// sus hijos (orbes, aros) no ensucien la jerarquía del Player.
    /// </summary>
    private WeaponRuntime CreateWeaponRuntime(WeaponDataSO data)
    {
        GameObject host = new GameObject($"Weapon_{data.weaponName}");
        host.transform.SetParent(transform, false);
        host.transform.localPosition = Vector3.zero;

        WeaponBehaviourBase behaviour = AddBehaviourFor(host, data.archetype);

        if (behaviour == null)
        {
            Debug.LogError(
                $"[WeaponController] El arquetipo '{data.archetype}' no tiene comportamiento asociado. " +
                "Revisa AddBehaviourFor: falta un case para ese valor de WeaponArchetype.",
                data);
            Destroy(host);
            return null;
        }

        WeaponRuntime runtime = new WeaponRuntime
        {
            Data = data,
            Level = 1,
            Host = host,
            Behaviour = behaviour
        };

        behaviour.Initialize(this, data, runtime.Level);

        return runtime;
    }

    /// <summary>
    /// Traduce arquetipo → comportamiento. Es el único mapa de este tipo del proyecto: si se
    /// añade un arquetipo nuevo, el compilador no avisa, así que el <c>default</c> registra un
    /// error explícito en lugar de equipar un arma muda.
    /// </summary>
    private static WeaponBehaviourBase AddBehaviourFor(GameObject host, WeaponArchetype archetype)
    {
        switch (archetype)
        {
            case WeaponArchetype.Orbit:
                return host.AddComponent<OrbitWeaponBehaviour>();

            case WeaponArchetype.Projectile:
                return host.AddComponent<ProjectileWeaponBehaviour>();

            case WeaponArchetype.Aura:
                return host.AddComponent<AuraWeaponBehaviour>();

            case WeaponArchetype.Pulse:
                return host.AddComponent<PulseWeaponBehaviour>();

            default:
                return null;
        }
    }

    private void DisposeRuntime(WeaponRuntime runtime)
    {
        if (runtime == null)
        {
            return;
        }

        if (runtime.Behaviour != null)
        {
            runtime.Behaviour.Dispose();
        }

        if (runtime.Host != null)
        {
            Destroy(runtime.Host);
        }

        runtime.Behaviour = null;
        runtime.Host = null;
    }
}
