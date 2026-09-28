using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Panel de elección de arma: ofrece 2 armas y el jugador elige una.
/// Se usa en DOS momentos de la partida, ambos vía <see cref="RunWeaponFlow"/>:
///   1) Al empezar (Día 1), antes de la primera oleada: define la build de la partida.
///   2) Al derrotar a un jefe: la recompensa de esa noche.
///
/// POR QUÉ UN PANEL PROPIO Y NO REUSAR EL DE NIVEL: son dos decisiones distintas. La de nivel
/// reparte 3 cartas de cualquier tipo al azar; esta ofrece exactamente 2 ARMAS que el jugador
/// todavía no tiene, con la horda detenida. Mezclarlas haría que el jugador perdiera su
/// elección de arma inicial entre tres cartas de daño genérico.
///
/// No hereda de <see cref="LevelUpUI"/> a propósito: reutiliza sus piezas (las mismas
/// <see cref="UpgradeCardUI"/>) en vez de duplicar su lógica de animación y bloqueo de clic.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CanvasGroup))]
[AddComponentMenu("UI/Weapon Panel")]
public class WeaponPanelUI : MonoBehaviour
{
    [Header("Referencias UI")]
    [Tooltip("CanvasGroup del panel. Si se deja vacío se toma del propio GameObject.")]
    [SerializeField] private CanvasGroup canvasGroup;

    [Tooltip("RectTransform a escalar en la animación de apertura. Si se vacía, se usa el del objeto.")]
    [SerializeField] private RectTransform panelRect;

    [Tooltip("Cartas del panel. La 3ª se oculta automáticamente para mostrar 2 opciones.")]
    [SerializeField] private UpgradeCardUI[] cards = new UpgradeCardUI[0];

    [Header("Catálogo de armas")]
    [Tooltip("Mejoras de tipo arma. El panel sortea 2 de aquí, filtrando las que ya tienes.")]
    [SerializeField] private List<UpgradeDataSO> weaponUpgrades = new List<UpgradeDataSO>();

    [Header("Animación")]
    [SerializeField] private float fadeDuration = 0.25f;

    [Header("Depuración")]
    [SerializeField] private bool logSelections = true;

    /// <summary>True mientras el panel está desplegado o animándose.</summary>
    public bool IsVisible { get; private set; }

    /// <summary>Se lanza cuando el jugador elige un arma y el panel se cierra.</summary>
    public event System.Action<UpgradeDataSO> OnWeaponChosen;

    private readonly List<UpgradeDataSO> pickBuffer = new List<UpgradeDataSO>(8);

    private void Awake()
    {
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }

        if (panelRect == null)
        {
            panelRect = transform as RectTransform;
        }

        if (cards == null || cards.Length == 0)
        {
            cards = GetComponentsInChildren<UpgradeCardUI>(true);
        }

        HidePanelInstant();
    }

    /// <summary>
    /// Abre el panel ofreciendo <paramref name="count"/> armas que el jugador aún no tiene.
    ///
    /// Si el prefab tiene 3 cartas pero solo hay 2 armas disponibles, la 3ª se oculta: mostrar
    /// un hueco vacío en un panel de 2 opciones se lee como un fallo, no como un hueco.
    /// </summary>
    public void ShowWeaponSelection(int count = 2)
    {
        if (IsVisible) return;

        if (cards == null || cards.Length == 0)
        {
            Debug.LogError("[WeaponPanelUI] No hay cartas asignadas: el panel no puede ofrecer armas.", this);
            return;
        }

        GameStateController stateController = GameStateController.Instance;

        if (stateController == null)
        {
            Debug.LogError(
                "[WeaponPanelUI] No hay GameStateController en la escena: no se puede pausar la partida de forma segura.",
                this);
            return;
        }

        int wanted = Mathf.Clamp(count, 1, cards.Length);
        List<UpgradeDataSO> picks = PickWeaponsNotOwned(wanted);

        if (picks.Count == 0)
        {
            // Ya tiene todas las armas del catálogo: no hay nada que ofrecer. Es un caso
            // normal (no un error), así que solo se registra y la partida continúa.
            if (logSelections)
            {
                Debug.Log("[WeaponPanelUI] El jugador ya posee todas las armas del catálogo: se omite la elección.", this);
            }

            return;
        }

        if (!stateController.RequestLevelUp())
        {
            Debug.LogWarning(
                $"[WeaponPanelUI] No se puede pausar la partida (estado actual: {stateController.CurrentState}).",
                this);
            return;
        }

        IsVisible = true;
        PopulateCards(picks);

        StartCoroutine(AnimateShow());
    }

    /// <summary>
    /// Sortea <paramref name="count"/> armas que el jugador NO tiene todavía equipadas.
    /// Fisher-Yates parcial sobre una lista reutilizada: sin asignaciones por apertura y sin
    /// repetir la misma arma en las dos cartas.
    /// </summary>
    private List<UpgradeDataSO> PickWeaponsNotOwned(int count)
    {
        pickBuffer.Clear();

        WeaponController weapons = FindAnyObjectByType<WeaponController>(FindObjectsInactive.Include);

        for (int i = 0; i < weaponUpgrades.Count; i++)
        {
            UpgradeDataSO candidate = weaponUpgrades[i];

            if (candidate == null) continue;

            // Solo cartas que otorgan un arma: una carta de "+20% daño" no tiene sentido
            // en un panel que promete "elige un arma".
            if (candidate.grantsWeapon == null) continue;

            // Filtro clave: no ofrecer lo que ya tiene. Reaparecer la misma arma en el
            // panel del jefe hace que la "recompensa" no parezca una recompensa.
            if (weapons != null && weapons.HasWeapon(candidate.grantsWeapon)) continue;

            pickBuffer.Add(candidate);
        }

        int toTake = Mathf.Min(count, pickBuffer.Count);

        // Barajado parcial: solo se desordena lo que se va a usar.
        for (int i = 0; i < toTake; i++)
        {
            int swapIndex = Random.Range(i, pickBuffer.Count);
            UpgradeDataSO temp = pickBuffer[i];
            pickBuffer[i] = pickBuffer[swapIndex];
            pickBuffer[swapIndex] = temp;
        }

        return pickBuffer.GetRange(0, toTake);
    }

    /// <summary>Reparte las opciones elegidas y oculta las cartas sobrantes.</summary>
    private void PopulateCards(List<UpgradeDataSO> picks)
    {
        UpgradeManager manager = FindAnyObjectByType<UpgradeManager>(FindObjectsInactive.Include);

        for (int i = 0; i < cards.Length; i++)
        {
            UpgradeCardUI card = cards[i];

            if (card == null) continue;

            // El WeaponPanelUI no es un LevelUpUI: se pasa null y el callback gestiona el cierre.
            card.OnSelectedOverride = HandleCardSelected;

            if (i < picks.Count)
            {
                // useWeaponDataText = true: el título y la descripción salen del WeaponDataSO
                // ("Órbita Sagrada"), no del UpgradeDataSO. En un panel que promete "elige un
                // arma", mostrar el nombre técnico de la mejora confunde la elección.
                card.SetupCard(picks[i], null, manager, true);
            }
            else
            {
                // Sobrante (la 3ª carta con solo 2 armas): fuera, no un hueco vacío.
                card.HideCard();
            }
        }
    }

    /// <summary>
    /// Callback de pulsación de una carta. La <see cref="UpgradeCardUI"/> ya aplicó la mejora
    /// (y por tanto ya equipó el arma vía <see cref="UpgradeManager"/>); aquí solo queda
    /// cerrar el panel y devolver el control del tiempo.
    /// </summary>
    private void HandleCardSelected(UpgradeDataSO selected)
    {
        if (logSelections && selected != null && selected.grantsWeapon != null)
        {
            Debug.Log($"<color=cyan>[WeaponPanelUI] Arma elegida: {selected.grantsWeapon.weaponName}.</color>", this);
        }

        HidePanel();
        OnWeaponChosen?.Invoke(selected);
    }

    /// <summary>Cierra el panel y reanuda la partida.</summary>
    public void HidePanel()
    {
        if (!IsVisible) return;

        IsVisible = false;

        // El callback se retira ANTES de cerrar: si no, un panel reutilizado más adelante
        // podría invocarlo otra vez sobre un GameObject ya destruido.
        ClearCardCallbacks();

        // Devolver el control del tiempo ANTES de animar: si la animación se corta, la
        // partida no puede quedarse congelada.
        GameStateController stateController = GameStateController.Instance;

        if (stateController != null)
        {
            stateController.RequestCloseLevelUp();
        }

        StartCoroutine(AnimateHide());
    }

    /// <summary>
    /// Oculta el panel sin animación ni eventos. Para el Awake y el reinicio de partida.
    /// No toca el GameStateController: al arrancar no hay nada que reanudar.
    /// </summary>
    public void HidePanelInstant()
    {
        IsVisible = false;
        ClearCardCallbacks();

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        if (panelRect != null)
        {
            panelRect.localScale = Vector3.zero;
        }
    }

    /// <summary>Retira el callback de todas las cartas. Evita referencias a paneles ya cerrados.</summary>
    private void ClearCardCallbacks()
    {
        if (cards == null) return;

        for (int i = 0; i < cards.Length; i++)
        {
            if (cards[i] != null)
            {
                cards[i].OnSelectedOverride = null;
            }
        }
    }

    private System.Collections.IEnumerator AnimateShow()
    {
        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = true;
            canvasGroup.interactable = true;
        }

        float timer = 0f;
        Vector3 startScale = Vector3.one * 0.7f;
        Vector3 targetScale = Vector3.one;

        while (timer < fadeDuration)
        {
            // unscaledDeltaTime: el tiempo ya está congelado por el GameStateController, así
            // que con deltaTime escalado esta animación no avanzaría nunca.
            timer += Time.unscaledDeltaTime;
            float progress = timer / fadeDuration;

            if (canvasGroup != null)
            {
                canvasGroup.alpha = Mathf.Lerp(0f, 1f, progress);
            }

            if (panelRect != null)
            {
                panelRect.localScale = Vector3.Lerp(startScale, targetScale, Mathf.SmoothStep(0f, 1f, progress));
            }

            yield return null;
        }

        if (canvasGroup != null) canvasGroup.alpha = 1f;
        if (panelRect != null) panelRect.localScale = targetScale;
    }

    private System.Collections.IEnumerator AnimateHide()
    {
        if (canvasGroup != null)
        {
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        float timer = 0f;
        Vector3 startScale = panelRect != null ? panelRect.localScale : Vector3.one;
        Vector3 targetScale = Vector3.one * 0.7f;

        while (timer < fadeDuration)
        {
            timer += Time.unscaledDeltaTime;
            float progress = timer / fadeDuration;

            if (canvasGroup != null)
            {
                canvasGroup.alpha = Mathf.Lerp(1f, 0f, progress);
            }

            if (panelRect != null)
            {
                panelRect.localScale = Vector3.Lerp(startScale, targetScale, Mathf.SmoothStep(0f, 1f, progress));
            }

            yield return null;
        }

        if (canvasGroup != null) canvasGroup.alpha = 0f;
    }

    [ContextMenu("Probar apertura con 2 armas")]
    private void DebugShowPanel()
    {
        ShowWeaponSelection(2);
    }
}
