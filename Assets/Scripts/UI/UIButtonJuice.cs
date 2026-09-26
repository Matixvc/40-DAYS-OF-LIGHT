using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

public class UIButtonJuice : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private Vector3 originalScale;
    private Coroutine pulseCoroutine;

    private void Awake()
    {
        originalScale = transform.localScale;
    }

    private void OnEnable()
    {
        // Animación de respiración continua usando unscaledTime.
        // Se guarda la corrutina para detenerla en OnDisable y evitar loops en segundo plano.
        if (pulseCoroutine == null)
        {
            pulseCoroutine = StartCoroutine(PulseAnimation());
        }
    }

    private void OnDisable()
    {
        if (pulseCoroutine != null)
        {
            StopCoroutine(pulseCoroutine);
            pulseCoroutine = null;
        }

        transform.localScale = originalScale;
    }

    private IEnumerator PulseAnimation()
    {
        while (true)
        {
            float pulse = 1f + Mathf.Sin(Time.unscaledTime * 4f) * 0.04f;
            transform.localScale = originalScale * pulse;
            yield return null;
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        // Efecto al pasar el cursor sobre el botón
        transform.localScale = originalScale * 1.15f;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        transform.localScale = originalScale;
    }
}