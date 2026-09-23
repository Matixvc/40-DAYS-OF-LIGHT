using UnityEngine;
using UnityEngine.UI;

public class XPBarUI : MonoBehaviour
{
    [Header("Referencias de UI")]
    [SerializeField] private Slider xpSlider;
    [SerializeField] private Image fillImage;

    [Header("Objetivo")]
    [SerializeField] private PlayerLevelSystem playerLevelSystem;

    private void Start()
    {
        if (xpSlider == null)
        {
            xpSlider = GetComponent<Slider>();
        }

        if (fillImage == null && xpSlider != null && xpSlider.fillRect != null)
        {
            fillImage = xpSlider.fillRect.GetComponent<Image>();
        }

        if (playerLevelSystem == null)
        {
            playerLevelSystem = FindAnyObjectByType<PlayerLevelSystem>();
        }

        if (playerLevelSystem != null)
        {
            // Suscribirse a los eventos del jugador
            playerLevelSystem.OnXPChanged += UpdateXPBar;
            playerLevelSystem.OnLevelUp += HandleLevelUp;

            // Inicializar la barra con los valores actuales
            if (playerLevelSystem.Data != null)
            {
                UpdateXPBar(playerLevelSystem.Data.currentXP, playerLevelSystem.Data.GetXPToNextLevel());
            }
        }
    }

    private void OnDestroy()
    {
        if (playerLevelSystem != null)
        {
            playerLevelSystem.OnXPChanged -= UpdateXPBar;
            playerLevelSystem.OnLevelUp -= HandleLevelUp;
        }
    }

    private void HandleLevelUp(int newLevel)
    {
        // Forzar el reseteo visual de la barra a 0 en el instante exacto de subir de nivel
        if (xpSlider != null)
        {
            xpSlider.value = 0f;
        }

        if (fillImage != null)
        {
            fillImage.enabled = false;
        }
    }

    private void UpdateXPBar(float currentXP, float maxXP)
    {
        if (xpSlider != null)
        {
            xpSlider.maxValue = maxXP;
            xpSlider.value = currentXP;
        }

        // Mostrar u ocultar el relleno si la XP actual es mayor a 0
        if (fillImage != null)
        {
            fillImage.enabled = currentXP > 0;
        }
    }
}