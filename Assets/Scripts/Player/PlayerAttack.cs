using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerAttack : MonoBehaviour
{
    [Header("Configuración de Arma (valores base)")]
    [SerializeField] private WeaponDataSO weaponData;

    [Header("Estado de partida")]
    [SerializeField] private RunStats runStats;

    [Header("Depuración")]
    [SerializeField] private bool debugStats = true;

    [Header("Detección de Objetivos")]
    [SerializeField] private LayerMask enemyLayer;

    [Header("Efecto Visual (Hijo del Pastor)")]
    [SerializeField] private GameObject attackVisualArea;

    [Header("Audio")]
    [SerializeField] private AudioClip attackPastorSFX; // Arrastrar AtackPastorPlayer.mp3 aqui

    // Reutilizado para no asignar memoria en cada ataque.
    private readonly HashSet<HealthComponent> damagedTargets = new HashSet<HealthComponent>();

    private float nextAttackTime;

    public WeaponDataSO WeaponData => weaponData;

    // Valores de partida: si existe RunStats manda RunStats; si no, el SO base.
    private float AttackDamage => runStats != null ? runStats.AttackDamage : (weaponData != null ? weaponData.damage : 10f);
    private float AttackRange => runStats != null ? runStats.AttackRange : (weaponData != null ? weaponData.attackRange : 4.5f);
    private float AttackInterval => runStats != null ? runStats.AttackInterval : (weaponData != null ? weaponData.attackInterval : 0.8f);

    private void Awake()
    {
        // Resolución temprana: funciona si RunStats está en este mismo GameObject.
        ResolveRunStats(false);
    }

    private void OnEnable()
    {
        SubscribeToRunStats();
    }

    private void Start()
    {
        // Si RunStats vive en otro GameObject, su Awake ya ha terminado en este punto.
        ResolveRunStats(true);
        SubscribeToRunStats();

        if (attackVisualArea != null)
        {
            attackVisualArea.SetActive(false);
        }

        // Evitar disparo instantaneo en el primer milisegundo de inicio
        nextAttackTime = Time.time + AttackInterval;
    }

    private void OnDisable()
    {
        if (runStats != null)
        {
            runStats.OnStatsChanged -= HandleStatsChanged;
        }
    }

    /// <summary>
    /// Busca el RunStats de la partida. Nunca se llama dentro de Update.
    /// </summary>
    private void ResolveRunStats(bool logIfMissing)
    {
        if (runStats == null)
        {
            runStats = GetComponent<RunStats>();
        }

        if (runStats == null)
        {
            runStats = RunStats.Active;
        }

        if (runStats == null)
        {
            if (logIfMissing)
            {
                Debug.LogError(
                    "[PlayerAttack] No se encontró RunStats. El arma usará los valores base del ScriptableObject " +
                    "y las mejoras NO tendrán efecto. Añade RunStats al GameObject del Player o asígnalo en el campo 'Run Stats'.",
                    this);
            }

            return;
        }

        if (logIfMissing && debugStats)
        {
            Debug.Log($"[PlayerAttack] Usando RunStats del GameObject '{runStats.gameObject.name}'.", this);
        }
    }

    private void SubscribeToRunStats()
    {
        if (runStats == null) return;

        runStats.OnStatsChanged -= HandleStatsChanged; // Evita suscripciones duplicadas
        runStats.OnStatsChanged += HandleStatsChanged;
    }

    /// <summary>
    /// Se llama cuando RunStats cambia: aplica la nueva cadencia de inmediato
    /// en lugar de esperar al siguiente ciclo de ataque.
    /// </summary>
    private void HandleStatsChanged()
    {
        nextAttackTime = Mathf.Min(nextAttackTime, Time.time + AttackInterval);

        if (debugStats)
        {
            Debug.Log(
                $"<color=cyan>[PlayerAttack] Stats recibidos | Daño: {AttackDamage:0.0} | Rango: {AttackRange:0.00} | Cadencia: {AttackInterval:0.00}s</color>",
                this);
        }
    }

    private void Update()
    {
        if (runStats == null && weaponData == null) return;

        if (Time.time >= nextAttackTime)
        {
            PerformAutomaticAttack();
            nextAttackTime = Time.time + AttackInterval;
        }
    }

    private void PerformAutomaticAttack()
    {
        // Reproduce el pulso celestial
        if (AudioManager.Instance != null && attackPastorSFX != null)
        {
            AudioManager.Instance.PlaySFX(attackPastorSFX, 0.9f, 0.05f);
        }

        Collider[] hitEnemies = Physics.OverlapSphere(transform.position, AttackRange, enemyLayer);

        damagedTargets.Clear();

        foreach (Collider enemyCollider in hitEnemies)
        {
            HealthComponent enemyHealth = enemyCollider.GetComponentInParent<HealthComponent>();
            if (enemyHealth == null) continue;

            // Evita daño duplicado cuando un enemigo tiene varios colliders
            if (!damagedTargets.Add(enemyHealth)) continue;

            enemyHealth.TakeDamage(AttackDamage);
        }

        if (attackVisualArea != null)
        {
            StopAllCoroutines();
            StartCoroutine(ShowAttackVisualFX());
        }
    }

    private IEnumerator ShowAttackVisualFX()
    {
        float diameter = AttackRange * 2f;
        attackVisualArea.transform.localScale = new Vector3(diameter, 0.01f, diameter);

        attackVisualArea.SetActive(true);
        yield return new WaitForSeconds(0.15f);
        attackVisualArea.SetActive(false);
    }

    private void OnDrawGizmosSelected()
    {
        if (weaponData == null) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, weaponData.attackRange);
    }
}