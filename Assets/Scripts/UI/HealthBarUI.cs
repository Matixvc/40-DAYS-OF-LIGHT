using UnityEngine;
using UnityEngine.UI;

public class HealthBarUI : MonoBehaviour
{
    [Header("Referencias de UI")]
    [SerializeField] private Slider healthSlider;
    [SerializeField] private Image fillImage; // Referencia directa a la imagen del relleno verde

    [Header("Objetivo")]
    [SerializeField] private HealthComponent targetHealth;

    private void Start()
    {
        if (healthSlider == null)
        {
            healthSlider = GetComponent<Slider>();
        }

        // Si no asignaste fillImage manualmente, la busca automáticamente en los hijos del Slider
        if (fillImage == null && healthSlider != null && healthSlider.fillRect != null)
        {
            fillImage = healthSlider.fillRect.GetComponent<Image>();
        }

        if (targetHealth == null)
        {
            PlayerController player = FindAnyObjectByType<PlayerController>();
            if (player != null)
            {
                targetHealth = player.GetComponent<HealthComponent>();
            }
        }

        if (targetHealth != null)
        {
            targetHealth.OnHealthChanged += UpdateHealthBar;
            healthSlider.maxValue = targetHealth.MaxHealth;
            UpdateHealthBar(targetHealth.CurrentHealth, targetHealth.MaxHealth);
        }
    }

    private void OnDestroy()
    {
        if (targetHealth != null)
        {
            targetHealth.OnHealthChanged -= UpdateHealthBar;
        }
    }

    private void UpdateHealthBar(float currentHealth, float maxHealth)
    {
        if (healthSlider != null)
        {
            healthSlider.maxValue = maxHealth;
            healthSlider.value = currentHealth;
        }

        // Si la vida llega a 0 (o menos), ocultamos el gráfico verde para que desaparezca por completo
        if (fillImage != null)
        {
            fillImage.enabled = currentHealth > 0;
        }
    }
}