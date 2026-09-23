using System.Collections;
using UnityEngine;

public class PlayerAttack : MonoBehaviour
{
    [Header("Configuración de Arma")]
    [SerializeField] private WeaponDataSO weaponData;
    [SerializeField] private LayerMask enemyLayer;

    [Header("Efecto Visual (Hijo del Pastor)")]
    [SerializeField] private GameObject attackVisualArea;

    [Header("Audio")]
    [SerializeField] private AudioClip attackPastorSFX; // Arrastrar AtackPastorPlayer.mp3 aqui

    private float nextAttackTime;

    public WeaponDataSO WeaponData => weaponData;

    private void Start()
    {
        if (attackVisualArea != null)
        {
            attackVisualArea.SetActive(false);
        }

        // Evitar disparo instantaneo en el primer milisegundo de inicio
        if (weaponData != null)
        {
            nextAttackTime = Time.time + weaponData.attackInterval;
        }
    }

    private void Update()
    {
        if (weaponData == null) return;

        if (Time.time >= nextAttackTime)
        {
            PerformAutomaticAttack();
            nextAttackTime = Time.time + weaponData.attackInterval;
        }
    }

    private void PerformAutomaticAttack()
    {
        // Reproduce el pulso celestial
        if (AudioManager.Instance != null && attackPastorSFX != null)
        {
            AudioManager.Instance.PlaySFX(attackPastorSFX, 0.9f, 0.05f);
        }

        Collider[] hitEnemies = Physics.OverlapSphere(transform.position, weaponData.attackRange, enemyLayer);
        foreach (Collider enemyCollider in hitEnemies)
        {
            HealthComponent enemyHealth = enemyCollider.GetComponent<HealthComponent>();
            if (enemyHealth != null)
            {
                enemyHealth.TakeDamage(weaponData.damage);
            }
        }

        if (attackVisualArea != null)
        {
            StopAllCoroutines();
            StartCoroutine(ShowAttackVisualFX());
        }
    }

    private IEnumerator ShowAttackVisualFX()
    {
        float diameter = weaponData.attackRange * 2f;
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