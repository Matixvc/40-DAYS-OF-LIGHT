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

        // Ocultar de inmediato al arrancar sin desactivar el GameObject
        HidePanelInstant();
        HideAllCards();
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

        PopulateUpgradeCards();

        if (activeCoroutine != null)
        {
            StopCoroutine(activeCoroutine);
        }

        activeCoroutine = StartCoroutine(AnimateShow());
    }

    private void PopulateUpgradeCards()
    {
        if (upgradeCards == null) return;

        // Copiar el pool para no repetir la misma mejora en dos cartas distintas
        List<UpgradeDataSO> pool = new List<UpgradeDataSO>();
        if (availableUpgrades != null)
        {
            pool.AddRange(availableUpgrades);
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

            upgradeCards[i].SetupCard(selectedSO, this, upgradeManager);
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