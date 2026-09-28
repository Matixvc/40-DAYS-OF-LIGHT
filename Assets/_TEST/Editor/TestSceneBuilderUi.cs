using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Constructores de UI para la escena TEST. Se construyen con colores planos en lugar de
/// sprites a propósito: el sandbox debe ser robusto y reconstruible, no bonito. Si un
/// sprite falta, la escena seguiría funcionando; eso no pasa con un TMP sin fuente.
/// </summary>
internal static partial class TestSceneBuilderUi
{
    /// <summary>Crea un objeto de UI con RectTransform, en la capa UI y sin parenting escaparate.</summary>
    public static GameObject CreateUiObject(string name, Transform parent, int uiLayer)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = uiLayer;

        if (parent != null)
        {
            go.transform.SetParent(parent, false);
        }

        return go;
    }

    /// <summary>Ancla a los cuatro bordes con margen cero.</summary>
    public static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    /// <summary>Fija un ancla, un tamaño y una posición en píxeles de la resolución de referencia.</summary>
    public static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;
    }

    /// <summary>Crea un rectángulo de color (fondo de panel, fondo de barra, etc.).</summary>
    public static GameObject CreatePanel(string name, Transform parent, int uiLayer, Color color)
    {
        GameObject go = CreateUiObject(name, parent, uiLayer);
        Image image = go.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return go;
    }

    /// <summary>
    /// Crea un texto TMP. raycastTarget se apaga siempre: si no, el texto se come los
    /// clics del botón que lo contiene y las cartas dejan de responder.
    /// </summary>
    public static TextMeshProUGUI CreateText(string name, Transform parent, int uiLayer, string content, float fontSize, Color color, TextAlignmentOptions alignment)
    {
        GameObject go = CreateUiObject(name, parent, uiLayer);
        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();

        if (TMP_Settings.defaultFontAsset != null)
        {
            text.font = TMP_Settings.defaultFontAsset;
        }

        text.text = content;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = alignment;
        text.raycastTarget = false;

        // enableWordWrapping está obsoleto en TMP de Unity 6: se usa textWrappingMode.
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.richText = true;

        return text;
    }

    /// <summary>
    /// Crea un botón con su etiqueta TMP ya cableada. El <see cref="Image"/> es el
    /// targetGraphic, que es lo que permite que el botón dispare su transición de color.
    /// </summary>
    public static Button CreateButton(string name, Transform parent, int uiLayer, string label, float fontSize, Color background, Color textColor, out TextMeshProUGUI labelText)
    {
        GameObject go = CreateUiObject(name, parent, uiLayer);
        Image image = go.AddComponent<Image>();
        image.color = background;

        Button button = go.AddComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.ColorTint;

        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
        colors.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
        colors.selectedColor = Color.white;
        colors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.5f);
        colors.colorMultiplier = 1f;
        button.colors = colors;

        labelText = CreateText("Label", go.transform, uiLayer, label, fontSize, textColor, TextAlignmentOptions.Center);
        Stretch(labelText.rectTransform);

        return button;
    }
}
