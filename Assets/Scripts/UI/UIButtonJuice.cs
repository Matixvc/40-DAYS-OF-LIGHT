using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

public class UIButtonJuice : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private Vector3 originalScale;

    private void Awake()
    {
        originalScale = transform.localScale;
    }

    private void OnEnable()
    {
        // Animación de respiración continua usando unscaledTime
        StartCoroutine(PulseAnimation());
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