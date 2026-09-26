using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Joystick virtual táctil (UI). Escribe un Vector2 normalizado (-1..1) que
/// <see cref="TouchControlsUI"/> envía al <see cref="PlayerInputReader"/>.
/// Requiere: fondo con Raycast Target activado y un "handle" opcional como hijo.
/// </summary>
[DisallowMultipleComponent]
public class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [Header("Referencias")]
    [Tooltip("Área táctil del joystick (por defecto, este mismo RectTransform).")]
    [SerializeField] private RectTransform background;
    [Tooltip("Pieza que se mueve con el dedo (opcional).")]
    [SerializeField] private RectTransform handle;

    [Header("Sensibilidad")]
    [Range(0f, 0.5f)]
    [Tooltip("Zona muerta central para evitar deriva.")]
    [SerializeField] private float deadZone = 0.12f;
    [Tooltip("Multiplicador del recorrido del dedo (1 = todo el radio del fondo).")]
    [SerializeField] private float sensitivity = 1f;

    /// <summary>Valor actual del joystick, normalizado a 1.</summary>
    public Vector2 Value { get; private set; }

    public event Action<Vector2> ValueChanged;

    private RectTransform area;

    private void Awake()
    {
        area = background != null ? background : GetComponent<RectTransform>();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        UpdateValue(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        UpdateValue(eventData);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        ResetValue();
    }

    private void UpdateValue(PointerEventData eventData)
    {
        if (area == null) return;

        Vector2 localPoint;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(area, eventData.position, eventData.pressEventCamera, out localPoint))
        {
            return;
        }

        Vector2 radius = area.rect.size * 0.5f;
        Vector2 normalized = new Vector2(
            localPoint.x / Mathf.Max(1f, radius.x),
            localPoint.y / Mathf.Max(1f, radius.y));

        Vector2 clamped = Vector2.ClampMagnitude(normalized * sensitivity, 1f);

        if (clamped.magnitude < deadZone)
        {
            clamped = Vector2.zero;
        }

        Value = clamped;

        if (handle != null)
        {
            handle.anchoredPosition = new Vector2(clamped.x * radius.x, clamped.y * radius.y);
        }

        ValueChanged?.Invoke(Value);
    }

    private void ResetValue()
    {
        Value = Vector2.zero;

        if (handle != null)
        {
            handle.anchoredPosition = Vector2.zero;
        }

        ValueChanged?.Invoke(Value);
    }
}
