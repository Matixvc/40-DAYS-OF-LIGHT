using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Una carta del panel de subida de nivel.
/// No conoce estadísticas ni ScriptableObjects de configuración: delega siempre
/// en <see cref="UpgradeManager"/> y avisa a <see cref="LevelUpUI"/> para cerrarse.
/// </summary>
public class UpgradeCardUI : MonoBehaviour
{
    [Header("Referencias UI de la Carta")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private Image iconImage;
    [SerializeField] private Button selectButton;

    [Header("Audio")]
    [SerializeField] private AudioClip cardClickSFX; // Asigna LevelUpButton.mp3 o ImpactEnemy.mp3 aquí

    private UpgradeDataSO currentData;
    private LevelUpUI mainUI;
    private UpgradeManager upgradeManager;
    private bool alreadyApplied;

    public UpgradeDataSO CurrentData => currentData;

    /// <summary>
    /// Rellena la carta con una mejora concreta y prepara su botón.
    /// </summary>
    public void SetupCard(UpgradeDataSO data, LevelUpUI ui, UpgradeManager manager)
    {
        currentData = data;
        mainUI = ui;
        upgradeManager = manager;
        alreadyApplied = false;

        gameObject.SetActive(true);

        if (titleText != null) titleText.text = data.upgradeName;
        if (descriptionText != null) descriptionText.text = data.description;

        if (iconImage != null)
        {
            iconImage.sprite = data.icon;
            iconImage.enabled = data.icon != null;
        }

        if (selectButton != null)
        {
            selectButton.interactable = true;
            selectButton.onClick.RemoveAllListeners();
            selectButton.onClick.AddListener(OnSelectUpgrade);
        }
    }

    /// <summary>
    /// Desactiva la carta y limpia su estado (para cuando hay menos mejoras que cartas).
    /// </summary>
    public void HideCard()
    {
        currentData = null;
        alreadyApplied = false;

        if (selectButton != null)
        {
            selectButton.onClick.RemoveAllListeners();
        }

        gameObject.SetActive(false);
    }

    private void OnSelectUpgrade()
    {
        // Bloquea dobles clics accidentales
        if (alreadyApplied) return;
        alreadyApplied = true;

        if (selectButton != null)
        {
            selectButton.interactable = false;
        }

        if (AudioManager.Instance != null && cardClickSFX != null)
        {
            AudioManager.Instance.PlaySFX(cardClickSFX, 0.9f, 0.02f);
        }

        if (currentData != null)
        {
            if (upgradeManager != null)
            {
                upgradeManager.ApplyUpgrade(currentData);
            }
            else
            {
                Debug.LogError(
                    "[UpgradeCardUI] No hay UpgradeManager asignado. Revisa el campo del LevelUpUI en el Inspector.",
                    this);
            }
        }

        // Cerrar siempre el panel: dejarlo abierto congelaría la partida.
        if (mainUI != null)
        {
            mainUI.HidePanel();
        }
    }

    private void OnDisable()
    {
        // Deja la carta lista para reutilizarse la próxima vez que aparezca.
        if (selectButton != null)
        {
            selectButton.interactable = true;
        }
    }
}