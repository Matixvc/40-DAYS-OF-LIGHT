using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

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

    private Coroutine activeCoroutine;

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
    }

    public void HidePanelInstant()
    {
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
        if (availableUpgrades == null || availableUpgrades.Count == 0 || upgradeCards == null) return;

        // Copiar el pool para no repetir la misma mejora en dos cartas distintas
        List<UpgradeDataSO> pool = new List<UpgradeDataSO>(availableUpgrades);

        for (int i = 0; i < upgradeCards.Length; i++)
        {
            if (pool.Count == 0) break;

            int randomIndex = Random.Range(0, pool.Count);
            UpgradeDataSO selectedSO = pool[randomIndex];
            pool.RemoveAt(randomIndex); // Evita duplicados en la misma selección

            upgradeCards[i].SetupCard(selectedSO, this);
        }
    }

    public void HidePanel()
    {
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

        // Congelar el tiempo tras desplegar
        Time.timeScale = 0f;
    }

    private IEnumerator AnimateHide()
    {
        // Reanudar el tiempo al iniciar el cierre
        Time.timeScale = 1f;

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