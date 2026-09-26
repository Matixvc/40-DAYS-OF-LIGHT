using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Botón táctil simple que expone eventos de pulsación (para sprint, pausa, etc.).
/// No depende del sistema de botones de UGUI: funciona con punteros táctiles y de ratón.
/// El feedback visual es SOLO escala/color, temporal y reversible: nunca desactiva el
/// GameObject, nunca pone el alfa a 0 y nunca llama a Destroy(), de modo que el botón
/// no puede desaparecer de la UI al presionarlo.
/// </summary>
[DisallowMultipleComponent]
public class VirtualButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    [Header("Feedback de pulsación (temporal y reversible)")]
    [Tooltip("Escala local aplicada mientras el botón está pulsado (1 = sin cambio).")]
    [SerializeField, Range(0.8f, 1f)] private float pressedScale = 0.94f;

    [Tooltip("Atenúa el color RGB mientras se pulsa (1 = sin cambio). El canal Alfa no se toca.")]
    [SerializeField, Range(0.5f, 1f)] private float pressedColorDim = 0.85f;

    public bool IsPressed { get; private set; }

    /// <summary>Se dispara al apoyar el dedo/ratón.</summary>
    public event Action Pressed;

    /// <summary>Se dispara al levantar el dedo/ratón.</summary>
    public event Action Released;

    private Vector3 baseScale;
    private Graphic targetGraphic;
    private Color baseColor;

    private void Awake()
    {
        baseScale = transform.localScale;
        targetGraphic = GetComponent<Graphic>();

        if (targetGraphic != null)
        {
            baseColor = targetGraphic.color;
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        IsPressed = true;
        ApplyPressedVisual();
        Pressed?.Invoke();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        Release();
    }

    private void OnDisable()
    {
        // Si el panel táctil se oculta (p. ej. al pausar) con el botón pulsado,
        // hay que restaurar el estado visual y soltar la pulsación.
        Release();
    }

    private void Release()
    {
        if (!IsPressed) return;

        IsPressed = false;
        RestoreVisual();
        Released?.Invoke();
    }

    private void ApplyPressedVisual()
    {
        transform.localScale = baseScale * pressedScale;

        if (targetGraphic == null) return;

        Color dimmed = baseColor;
        dimmed.r *= pressedColorDim;
        dimmed.g *= pressedColorDim;
        dimmed.b *= pressedColorDim;
        // El alfa se conserva: el botón nunca se oculta.
        targetGraphic.color = dimmed;
    }

    private void RestoreVisual()
    {
        transform.localScale = baseScale;

        if (targetGraphic != null)
        {
            targetGraphic.color = baseColor;
        }
    }
}
