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
    /// Callback opcional que reemplaza al cierre automático de <see cref="LevelUpUI"/>.
    ///
    /// POR QUÉ EXISTE: la carta nació atada al panel de nivel (cerrarse a sí misma al pulsarse),
    /// pero el <see cref="WeaponPanelUI"/> necesita el control del flujo: aplicar el arma,
    /// ocultar SU panel y reanudar la partida. Con este evento la carta se reutiliza tal cual
    /// en ambos paneles sin duplicar su lógica de botón, bloqueo de doble clic y audio.
    ///
    /// Si es null, se comporta como siempre (delega en LevelUpUI.HidePanel).
    /// </summary>
    public System.Action<UpgradeDataSO> OnSelectedOverride;

    /// <summary>La carta ya se pulsó en esta apertura. Impide el doble clic accidental.</summary>
    public bool AlreadyApplied => alreadyApplied;

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
    /// <param name="data">Mejora a mostrar. Su <c>grantsWeapon</c>, si lo tiene, manda en el texto.</param>
    /// <param name="ui">Panel de nivel dueño de la carta. Null en WeaponPanelUI.</param>
    /// <param name="manager">UpgradeManager que aplica la mejora.</param>
    /// <param name="useWeaponDataText">
    /// Fuerza que el título y la descripción se lean del <see cref="WeaponDataSO"/> en vez del
    /// <see cref="UpgradeDataSO"/>. Lo activa el panel de armas, donde la carta representa un ARMA
    /// y su nombre técnico ("Furia Celestial") no le sirve al jugador que está eligiendo entre
    /// "Órbita Sagrada" y "Aura de Purificación".
    /// </param>
    public void SetupCard(UpgradeDataSO data, LevelUpUI ui, UpgradeManager manager, bool useWeaponDataText = false)
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
            titleText.text = useWeaponDataText && data.grantsWeapon != null
                ? data.grantsWeapon.weaponName
                : data.upgradeName;

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

        // Cerrar SIEMPRE el panel: dejarlo abierto congelaría la partida.
        // Si el WeaponPanelUI registró un callback, es el que decide el cierre (porque su
        // flujo es distinto: además tiene que reanudar el spawner).
        if (OnSelectedOverride != null)
        {
            OnSelectedOverride(currentData);
        }
        else if (mainUI != null)
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