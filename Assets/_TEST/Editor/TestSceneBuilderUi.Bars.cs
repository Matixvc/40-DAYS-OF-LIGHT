using UnityEngine;
using UnityEngine.UI;

internal static partial class TestSceneBuilderUi
{
    /// <summary>
    /// Crea una barra de progreso (Slider sin handle) con la estructura que espera Unity:
    /// Slider → Fill Area → Fill. El Slider redimensiona <c>fillRect</c> cambiando sus
    /// anchors, así que el Fill debe estar estirado dentro de su Fill Area.
    ///
    /// <paramref name="interactable"/> se pone a false: estas barras son de lectura, y que
    /// el jugador pueda arrastrarlas durante una prueba sería un bug desconcertante.
    /// </summary>
    public static Slider CreateBar(
        string name,
        Transform parent,
        int uiLayer,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 anchoredPosition,
        Vector2 sizeDelta,
        Color background,
        Color fill,
        float initialValue)
    {
        GameObject barGo = CreateUiObject(name, parent, uiLayer);
        RectTransform barRect = barGo.GetComponent<RectTransform>();
        SetRect(barRect, anchorMin, anchorMax, pivot, anchoredPosition, sizeDelta);

        GameObject backgroundGo = CreatePanel("Background", barGo.transform, uiLayer, background);
        Stretch(backgroundGo.GetComponent<RectTransform>());

        GameObject fillAreaGo = CreateUiObject("Fill Area", barGo.transform, uiLayer);
        RectTransform fillAreaRect = fillAreaGo.GetComponent<RectTransform>();
        Stretch(fillAreaRect);
        fillAreaRect.offsetMin = new Vector2(2f, 2f);
        fillAreaRect.offsetMax = new Vector2(-2f, -2f);

        GameObject fillGo = CreatePanel("Fill", fillAreaGo.transform, uiLayer, fill);
        RectTransform fillRect = fillGo.GetComponent<RectTransform>();
        Stretch(fillRect);

        Slider slider = barGo.AddComponent<Slider>();
        slider.fillRect = fillRect;
        slider.handleRect = null;
        slider.targetGraphic = backgroundGo.GetComponent<Image>();
        slider.direction = Slider.Direction.LeftToRight;
        slider.transition = Selectable.Transition.None;
        slider.interactable = false;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = Mathf.Clamp01(initialValue);

        return slider;
    }

    /// <summary>Crea un Canvas en Screen Space Overlay con el escalado de referencia del proyecto.</summary>
    public static Canvas CreateCanvas(string name, int uiLayer, int sortingOrder)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        go.layer = uiLayer;

        Canvas canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        CanvasScaler scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        return canvas;
    }
}
