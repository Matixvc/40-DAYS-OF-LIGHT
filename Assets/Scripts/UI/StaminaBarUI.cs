using UnityEngine;
using UnityEngine.UI;

public class StaminaBarUI : MonoBehaviour
{
    [Header("Referencias de UI")]
    [SerializeField] private Slider staminaSlider;
    [SerializeField] private Image fillImage;

    [Header("Colores de Estado")]
    [SerializeField] private Color normalColor = Color.cyan;
    [SerializeField] private Color cooldownColor = Color.red;

    [Header("Referencias del Jugador")]
    [SerializeField] private PlayerController playerController;

    private void Start()
    {
        if (staminaSlider == null)
            staminaSlider = GetComponent<Slider>();

        if (fillImage == null && staminaSlider != null && staminaSlider.fillRect != null)
            fillImage = staminaSlider.fillRect.GetComponent<Image>();

        if (playerController == null)
            playerController = FindAnyObjectByType<PlayerController>();
    }

    private void Update()
    {
        if (playerController == null || staminaSlider == null) return;

        // Actualizar el valor del Slider (de 0 a 1)
        float currentPercent = playerController.SprintPercent;
        staminaSlider.value = currentPercent;

        // Cambiar el color a rojo mientras se recarga, y a cian cuando esté listo
        if (fillImage != null)
        {
            fillImage.color = playerController.IsSprintOnCooldown ? cooldownColor : normalColor;
        }
    }
}