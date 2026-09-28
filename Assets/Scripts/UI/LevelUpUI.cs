using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// System.Random también existe: el alias evita la ambigüedad CS0104.
using Random = UnityEngine.Random;

[RequireComponent(typeof(CanvasGroup))]
public class LevelUpUI : MonoBehaviour
{
    [Header("Referencias de Animación UI")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private RectTransform panelRect;
    [SerializeField] private float fadeDuration = 0.25f;

    [Header("Pool de Mejoras Disponibles")]
    [SerializeField] private List<UpgradeDataSO> availableUpgrades; // Asigna aquí tus assets SO_Upgrade_Damage, etc.

    [Header("UI de las Cartas u Opciones")]
    [SerializeField] private UpgradeCardUI[] upgradeCards; // Los scripts de cada botón/opción

    [Header("Audio")]
    [SerializeField] private AudioClip levelUpFanfareSFX; // Asigna LevelUpUI.mp3 aquí

    [Header("Aplicación de Mejoras")]
    [SerializeField] private UpgradeManager upgradeManager; // Arrastra aquí el objeto con UpgradeManager

    private Coroutine activeCoroutine;
    private bool isVisible;

    /// <summary>
    /// Opciones forzadas para la próxima apertura del panel. Cuando no es null, el panel
    /// muestra EXACTAMENTE estas mejoras en este orden, ignorando el sorteo aleatorio.
    ///
    /// Se usa para dos casos donde el azar sería un error de diseño:
    ///   1) Elección del arma inicial: el jugador debe ver las 4 armas, no 3 al azar de 11.
    ///   2) Recompensa de jefe: el premio debe ser siempre una carta de arma o evolución.
    /// Se consume en la primera apertura: <see cref="ShowPanel"/> y <see cref="ShowPanelWith"/>
    /// la borran después de repartir, así una selección normal posterior no hereda el forzaje.
    /// </summary>
    private List<UpgradeDataSO> forcedUpgrades;

    /// <summary>True cuando el jugador tiene abierto un panel con opciones forzadas (arma inicial / jefe).</summary>
    public bool IsShowingForcedOptions => forcedUpgrades != null;

    /// <summary>Evita repetir el aviso de "falta UpgradeManager" en cada llamada a ResolveUpgradeManager().</summary>
    private bool missingUpgradeManagerWarned;

    /// <summary>True mientras el panel está desplegado o animándose.</summary>
    public bool IsVisible => isVisible;

    /// <summary>Se lanza cuando el panel termina de cerrarse (una vez por mejora elegida).</summary>
    public event Action OnPanelClosed;

    private void Awake()
    {
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }

        if (upgradeCards == null || upgradeCards.Length == 0)
        {
            upgradeCards = GetComponentsInChildren<UpgradeCardUI>(true);
        }

        // Auto-resolución: si el Inspector quedó vacío se busca el UpgradeManager en escena
        // para evitar el error "No hay UpgradeManager asignado" al elegir una mejora.
        ResolveUpgradeManager();

        // Ocultar de inmediato al arrancar sin desactivar el GameObject
        HidePanelInstant();
        HideAllCards();
    }

    /// <summary>
    /// Busca el UpgradeManager en escena si el campo sigue vacío (Inspector sin asignar
    /// o manager instanciado en runtime). Se llama en Awake y de nuevo al poblar las cartas.
    /// </summary>
    /// <returns>El UpgradeManager resuelto (null si no existe en escena).</returns>
    private UpgradeManager ResolveUpgradeManager()
    {
        if (upgradeManager != null) return upgradeManager;

        upgradeManager = FindAnyObjectByType<UpgradeManager>();

        if (upgradeManager == null && !missingUpgradeManagerWarned)
        {
            // Una sola advertencia por sesión: el método se llama en Awake y en cada reparto de cartas.
            missingUpgradeManagerWarned = true;
            Debug.LogWarning(
                "[LevelUpUI] No hay UpgradeManager en la escena: las mejoras seleccionadas no se aplicarán. " +
                "Añade el componente UpgradeManager a un objeto de la escena.",
                this);
        }

        return upgradeManager;
    }

    /// <summary>Limpia las cartas para que no queden datos de una selección anterior.</summary>
    private void HideAllCards()
    {
        if (upgradeCards == null) return;

        for (int i = 0; i < upgradeCards.Length; i++)
        {
            if (upgradeCards[i] != null)
            {
                upgradeCards[i].HideCard();
            }
        }
    }

    public void HidePanelInstant()
    {
        isVisible = false;

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

    /// <summary>
    /// Selecciona 3 mejoras al azar, puebla la UI, reproduce la animación y congela la partida.
    /// </summary>
    public void ShowPanel()
    {
        ShowPanel(null);
    }

    /// <summary>
    /// Abre el panel ofreciendo EXACTAMENTE las mejoras indicadas, en ese orden, saltándose
    /// el sorteo aleatorio. Si la lista es null o vacía, se comporta como <see cref="ShowPanel"/>.
    /// </summary>
    /// <remarks>
    /// El forzaje se consume en esta misma llamada: la siguiente subida de nivel normal vuelve
    /// a sortear de <c>availableUpgrades</c>. Así el arma inicial no contamina el resto del run.
    /// </remarks>
    public void ShowPanelWith(IList<UpgradeDataSO> forcedOptions)
    {
        forcedUpgrades = forcedOptions != null && forcedOptions.Count > 0
            ? new List<UpgradeDataSO>(forcedOptions)
            : null;

        ShowPanel(forcedUpgrades);
    }

    /// <summary>Implementación común de ambas aperturas.</summary>
    private void ShowPanel(List<UpgradeDataSO> forcedOptions)
    {
        // Evita abrir el panel dos veces si ya está visible
        if (isVisible) return;

        GameStateController stateController = GameStateController.Instance;
        if (stateController == null)
        {
            Debug.LogError(
                "[LevelUpUI] No existe GameStateController en la escena: no se puede congelar el juego de forma segura. Añádelo a la escena.",
                this);
            return;
        }

        if (!stateController.RequestLevelUp())
        {
            Debug.LogWarning(
                $"[LevelUpUI] No se puede abrir la selección de mejora (estado actual: {stateController.CurrentState}).",
                this);
            return;
        }

        isVisible = true;

        // --- REPRODUCIR FANFARRIA ---
        if (AudioManager.Instance != null && levelUpFanfareSFX != null)
        {
            AudioManager.Instance.PlaySFX(levelUpFanfareSFX, 1f, 0f);
        }

        PopulateUpgradeCards(forcedOptions);

        // El forzaje es de UN solo uso: la siguiente subida vuelve a sortear al azar.
        forcedUpgrades = null;

        if (activeCoroutine != null)
        {
            StopCoroutine(activeCoroutine);
        }

        activeCoroutine = StartCoroutine(AnimateShow());
    }

    /// <param name="forcedOptions">
    /// Si es null o vacía, se sortean 3 mejoras de <c>availableUpgrades</c> al azar.
    /// </param>
    private void PopulateUpgradeCards(List<UpgradeDataSO> forcedOptions)
    {
        if (upgradeCards == null) return;

        // Se resuelve UNA vez y se reutiliza en todas las cartas: antes se llamaba dentro del
        // bucle y, con el manager sin asignar, eso generaba una búsqueda por carta y por nivel.
        UpgradeManager manager = ResolveUpgradeManager();

        bool useForced = forcedOptions != null && forcedOptions.Count > 0;
        List<UpgradeDataSO> pool = new List<UpgradeDataSO>();

        if (useForced)
        {
            // Rama forzada: se respeta el orden del llamante. No se filtran por CanApply
            // porque un arma inicial debe poder ofrecerse aunque el jugador ya la tenga
            // (repetirla la sube de nivel, que es un resultado válido).
            for (int i = 0; i < forcedOptions.Count; i++)
            {
                if (forcedOptions[i] != null)
                {
                    pool.Add(forcedOptions[i]);
                }
            }
        }
        else if (availableUpgrades != null)
        {
            // Rama normal: copiar el pool para no repetir la misma mejora en dos cartas distintas.
            for (int i = 0; i < availableUpgrades.Count; i++)
            {
                UpgradeDataSO candidate = availableUpgrades[i];

                if (candidate == null)
                {
                    continue;
                }

                // Las mejoras que ya alcanzaron su maxStacks no se ofrecen: una carta sin
                // efecto es peor que una carta menos, porque la elección se gasta igual.
                if (manager != null && !manager.CanApply(candidate))
                {
                    continue;
                }

                pool.Add(candidate);
            }
        }

        int usedCards = 0;

        for (int i = 0; i < upgradeCards.Length; i++)
        {
            if (upgradeCards[i] == null) continue;

            if (pool.Count == 0)
            {
                // Si hay menos mejoras que cartas, ocultamos las sobrantes
                upgradeCards[i].HideCard();
                continue;
            }

            int randomIndex = UnityEngine.Random.Range(0, pool.Count);
            UpgradeDataSO selectedSO = pool[randomIndex];
            pool.RemoveAt(randomIndex); // Evita duplicados en la misma selección

            upgradeCards[i].SetupCard(selectedSO, this, manager);
            usedCards++;
        }

        if (usedCards == 0)
        {
            Debug.LogError(
                "[LevelUpUI] No hay mejoras disponibles. Revisa la lista Available Upgrades en el Inspector.",
                this);
        }
    }

    public void HidePanel()
    {
        if (!isVisible) return;

        isVisible = false;

        // Avisar antes de devolver el control del tiempo: así se pueden encadenar
        // varias subidas de nivel sin perder ninguna mejora.
        OnPanelClosed?.Invoke();

        // Devolver el control del tiempo ANTES de animar el cierre.
        GameStateController stateController = GameStateController.Instance;
        if (stateController != null)
        {
            stateController.RequestCloseLevelUp();
        }

        if (activeCoroutine != null)
        {
            StopCoroutine(activeCoroutine);
        }

        activeCoroutine = StartCoroutine(AnimateHide());
    }

    private IEnumerator AnimateShow()
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

        // El tiempo ya está congelado por GameStateController (estado LevelUpSelection).
    }

    private IEnumerator AnimateHide()
    {
        // El tiempo lo reanuda GameStateController al salir de LevelUpSelection.
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
}