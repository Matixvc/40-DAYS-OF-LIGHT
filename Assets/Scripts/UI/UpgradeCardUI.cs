using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UpgradeCardUI : MonoBehaviour
{
    [Header("Referencias UI de la Carta")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private Image iconImage;
    [SerializeField] private Button selectButton;

    [Header("Audio")]
    [SerializeField] private AudioClip cardClickSFX; // Asigna LevelUpButton.mp3 o ImapactEnemy.mp3 aquí

    private UpgradeDataSO currentData;
    private LevelUpUI mainUI;

    public void SetupCard(UpgradeDataSO data, LevelUpUI ui)
    {
        currentData = data;
        mainUI = ui;

        // Usamos los nombres de variables de tu UpgradeDataSO
        if (titleText != null) titleText.text = data.upgradeName;
        if (descriptionText != null) descriptionText.text = data.description;
        if (iconImage != null && data.icon != null) iconImage.sprite = data.icon;

        if (selectButton != null)
        {
            selectButton.onClick.RemoveAllListeners();
            selectButton.onClick.AddListener(OnSelectUpgrade);
        }
    }

    private void OnSelectUpgrade()
    {
        if (currentData == null) return;

        // --- REPRODUCIR SONIDO DE CLIC ---
        if (AudioManager.Instance != null && cardClickSFX != null)
        {
            AudioManager.Instance.PlaySFX(cardClickSFX, 0.9f, 0.02f);
        }

        // Aplicar el efecto de la mejora
        PlayerLevelSystem player = FindAnyObjectByType<PlayerLevelSystem>();
        if (player != null)
        {
            switch (currentData.upgradeType)
            {
                case UpgradeType.IncreaseDamage:
                    Debug.Log($"<color=green>+ Daño incrementado en {currentData.value}</color>");
                    break;
                case UpgradeType.IncreaseRange:
                    Debug.Log($"<color=green>+ Rango incrementado en {currentData.value}</color>");
                    break;
                case UpgradeType.IncreaseMoveSpeed:
                    Debug.Log($"<color=green>+ Velocidad incrementada en {currentData.value}</color>");
                    break;
                case UpgradeType.HealPlayer:
                    HealthComponent health = player.GetComponent<HealthComponent>();
                    if (health != null) health.Heal(currentData.value);
                    break;
            }
        }

        if (mainUI != null)
        {
            mainUI.HidePanel();
        }
    }
}