using UnityEngine;

/// <summary>
/// Arquetipo Pulse: pulso de área instantáneo alrededor del jugador. Es la versión
/// "sistema nuevo" del comportamiento original de <see cref="PlayerAttack"/>, con las mismas
/// reglas de daño (una sola aplicación por enemigo aunque tenga varios colliders) para que el
/// balance no cambie al activar <c>useNewWeaponSystem</c>.
/// </summary>
[AddComponentMenu("Gameplay/Weapons/Pulse Weapon Behaviour")]
public class PulseWeaponBehaviour : WeaponBehaviourBase
{
    /// <summary>Segundos que el destello permanece visible tras el pulso.</summary>
    private const float FlashDuration = 0.15f;

    /// <summary>
    /// Objetivos ya golpeados en el pulso actual. Es un HashSet reutilizado, no uno nuevo por
    /// ataque: reservar memoria en cada pulso (2-3 veces por segundo con 100 enemigos) es
    /// justo el tipo de basura que el ObjectPoolManager existe para evitar.
    /// </summary>
    private readonly System.Collections.Generic.HashSet<HealthComponent> damagedThisPulse =
        new System.Collections.Generic.HashSet<HealthComponent>();

    private GameObject flashVisual;
    private float nextFireTime;
    private float flashEndTime;

    public override void SetLevel(int level)
    {
        base.SetLevel(level);

        // Subir de nivel acelera el pulso: la cadencia se recalcula sola en el siguiente Tick,
        // pero adelantar el temporizador hace que la mejora se note de inmediato.
        nextFireTime = Mathf.Min(nextFireTime, Time.time);
    }

    protected override void BuildVisuals()
    {
        if (Data == null)
        {
            return;
        }

        // El destello arranca oculto: se enciende FlashDuration segundos al golpear.
        flashVisual = CreateVisual(Data.visualPrefab, Vector3.up * 0.2f, Data.visualScale);
        ApplyFlashScale();

        if (flashVisual != null)
        {
            flashVisual.SetActive(false);
        }

        // Primer ataque tras una cadencia completa: evita el pico de daño en el frame 1,
        // igual que hace PlayerAttack.Start().
        nextFireTime = Time.time + Mathf.Max(0.05f, CurrentCooldown);
    }

    public override void Tick(float deltaTime)
    {
        UpdateFlashVisibility();

        if (Time.time < nextFireTime)
        {
            return;
        }

        Fire();
        nextFireTime = Time.time + Mathf.Max(0.05f, CurrentCooldown);
    }

    private void Fire()
    {
        float radius = ScaleArea(Data != null ? Data.attackRange : 4f);

        int found = Physics.OverlapSphereNonAlloc(transform.position, radius, colliderBuffer, EnemyLayer);

        damagedThisPulse.Clear();

        for (int i = 0; i < found; i++)
        {
            Collider candidate = colliderBuffer[i];

            if (candidate == null)
            {
                continue;
            }

            HealthComponent target = WeaponTargeting.ResolveTarget(candidate);

            if (!WeaponTargeting.IsValidTarget(target) || !damagedThisPulse.Add(target))
            {
                continue;
            }

            WeaponTargeting.ApplyDamage(target, CurrentDamage, Stats, out _);
        }

        if (damagedThisPulse.Count > 0 && CameraShake.Instance != null)
        {
            CameraShake.Instance.Shake(0.12f, 0.15f);
        }

        flashEndTime = Time.time + FlashDuration;

        if (flashVisual != null && VisualsEnabled)
        {
            ApplyFlashScale();
            flashVisual.SetActive(true);
        }
    }

    /// <summary>Escala el aro al radio real del pulso, que cambia con las mejoras de área.</summary>
    private void ApplyFlashScale()
    {
        if (flashVisual == null || Data == null)
        {
            return;
        }

        float diameter = ScaleArea(Data.attackRange) * 2f;
        flashVisual.transform.localScale = new Vector3(diameter, 0.08f, diameter);
    }

    /// <summary>
    /// Apaga el destello cuando expira. Se hace con un temporizador en Tick y no con una
    /// corrutina para no crear el objeto de corrutina en cada pulso.
    /// </summary>
    private void UpdateFlashVisibility()
    {
        if (flashVisual == null || !flashVisual.activeSelf)
        {
            return;
        }

        if (!VisualsEnabled || Time.time >= flashEndTime)
        {
            flashVisual.SetActive(false);
        }
    }
}
