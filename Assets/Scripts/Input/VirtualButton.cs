using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Botón táctil simple que expone eventos de pulsación (para sprint, pausa, etc.).
/// No depende del sistema de botones de UGUI: funciona con punteros táctiles y de ratón.
/// IMPORTANTE: Si pressedVisual es el mismo GameObject del botón, NO se debe desactivar
/// ya que apagaría el propio botón y los handlers de eventos táctiles.
/// </summary>
[DisallowMultipleComponent]
public class VirtualButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    [Header("Visual (opcional)")]
    [Tooltip("Objeto visual adicional o indicador secundario (NO debe ser el mismo GameObject de este botón).")]
    [SerializeField] private GameObject pressedVisual;

    public bool IsPressed { get; private set; }

    /// <summary>Se dispara al apoyar el dedo/ratón.</summary>
    public event Action Pressed;

    /// <summary>Se dispara al levantar el dedo/ratón.</summary>
    public event Action Released;

    public void OnPointerDown(PointerEventData eventData)
    {
        IsPressed = true;

        if (pressedVisual != null && pressedVisual != gameObject)
        {
            pressedVisual.SetActive(true);
        }

        Pressed?.Invoke();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        Release();
    }

    private void OnDisable()
    {
        Release();
    }

    private void Release()
    {
        if (!IsPressed) return;

        IsPressed = false;

        if (pressedVisual != null && pressedVisual != gameObject)
        {
            pressedVisual.SetActive(false);
        }

        Released?.Invoke();
    }
}
