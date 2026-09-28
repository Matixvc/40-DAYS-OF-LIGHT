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

    private void Awake()
    {
        // Si LevelUpUI nos pasa un manager nulo (Inspector sin asignar) se resuelve aquí mismo:
        // evita el error "No hay UpgradeManager asignado" al pulsar la carta.
        ResolveUpgradeManager();
    }

    /// <summary>
    /// Auto-resuelve el UpgradeManager si sigue a null. Se usa en Awake, en SetupCard
    /// (por si LevelUpUI envía null) y antes de aplicar la mejora como último respaldo.
    /// </summary>
    private UpgradeManager ResolveUpgradeManager()
    {
        if (upgradeManager == null)
        {
            upgradeManager = FindAnyObjectByType<UpgradeManager>();
        }

        return upgradeManager;
    }

    /// <summary>
    /// Rellena la carta con una mejora concreta y prepara su botón.
    /// </summary>
    public void SetupCard(UpgradeDataSO data, LevelUpUI ui, UpgradeManager manager)
    {
        currentData = data;
        mainUI = ui;
        upgradeManager = manager;
        alreadyApplied = false;

        // Respaldo: si el nivel no pudo resolver el manager, lo intenta la propia carta.
        ResolveUpgradeManager();

        gameObject.SetActive(true);

        if (titleText != null)
        {
            titleText.text = data.upgradeName;

            // Fase 2: el color de la rareza se aplica al título. Se lee del SO en cada
            // SetupCard (no se cachea) para que un cambio de rareza en el Inspector se vea
            // al instante durante el diseño.
            titleText.color = data.RarityColor;
        }

        if (descriptionText != null)
        {
            // Si la carta v2 no tiene texto propio, se autogenera desde sus modificadores:
            // una carta en blanco es peor que un texto técnico.
            string text = string.IsNullOrWhiteSpace(data.description)
                ? data.BuildModifierSummary()
                : data.description;

            descriptionText.text = text;
        }

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
            // Último respaldo antes de aplicar: por si el manager apareció en escena después del Awake.
            ResolveUpgradeManager();

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