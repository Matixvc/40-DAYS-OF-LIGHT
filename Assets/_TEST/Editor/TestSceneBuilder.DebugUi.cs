using TMPro;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public static partial class TestSceneBuilder
{
    private const int DebugCanvasOrder = 999;
    private const float DebugPanelWidth = 560f;
    private const float DebugButtonWidth = 170f;
    private const float DebugButtonHeight = 44f;
    private const float DebugColumnGap = 10f;
    private const float DebugRowGap = 10f;
    private const float DebugFirstRowY = -430f;
    private const int DebugColumns = 3;

    private static readonly Color DebugPanelColor = new Color(0.06f, 0.07f, 0.10f, 0.92f);
    private static readonly Color DebugButtonColor = new Color(0.20f, 0.22f, 0.32f, 1f);
    private static readonly Color DebugActionColor = new Color(0.24f, 0.30f, 0.46f, 1f);
    private static readonly Color DebugDangerColor = new Color(0.42f, 0.18f, 0.20f, 1f);

    /// <summary>
    /// Canvas de depuración, en <c>sortingOrder 999</c> para que quede SIEMPRE por encima
    /// del HUD y del panel de nivel, incluso con la partida congelada.
    /// </summary>
    internal static void BuildDebugUi(BuildContext context)
    {
        Canvas canvas = TestSceneBuilderUi.CreateCanvas("Debug_Canvas", context.UiLayer, DebugCanvasOrder);

        // Anfitrión: NO se desactiva nunca. Aquí viven el controlador y el panel, así que
        // apagarlo los mataría y F10 no podría volver a encenderlos.
        GameObject hostGo = TestSceneBuilderUi.CreateUiObject("DebugPanel", canvas.transform, context.UiLayer);
        TestSceneBuilderUi.Stretch(hostGo.GetComponent<RectTransform>());

        CanvasGroup group = hostGo.AddComponent<CanvasGroup>();
        context.TestController = hostGo.AddComponent<TestEnvironmentController>();
        context.DebugPanel = hostGo.AddComponent<TestDebugPanel>();

        // Contenido visual: esto sí se enciende y apaga, y es el panelRect del script.
        GameObject contentGo = TestSceneBuilderUi.CreateUiObject("DebugPanelContent", hostGo.transform, context.UiLayer);
        RectTransform contentRect = contentGo.GetComponent<RectTransform>();
        TestSceneBuilderUi.SetRect(contentRect,
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(14f, -14f), new Vector2(DebugPanelWidth, 900f));

        Image backdrop = contentGo.AddComponent<Image>();
        backdrop.color = DebugPanelColor;

        // RaycastTarget false: el fondo no debe robarle clics al juego, pero los botones
        // que hay encima sí los reciben porque los crea CreateButton con raycastTarget true.
        backdrop.raycastTarget = false;

        BuildDebugTexts(context, contentRect);
        BuildDebugButtons(context, contentRect);

        TestSceneBuilderUtil.SetValue(context.DebugPanel, "controller", context.TestController);
        TestSceneBuilderUtil.SetValue(context.DebugPanel, "group", group);
        TestSceneBuilderUtil.SetValue(context.DebugPanel, "panelRect", contentRect);
        TestSceneBuilderUtil.SetValue(context.DebugPanel, "startVisible", true);
    }

    private static void BuildDebugTexts(BuildContext context, RectTransform parent)
    {
        TestSceneBuilderUtil.SetValue(context.DebugPanel, "statusText",
            CreateDebugText(context, parent, "StatusText", string.Empty, 21f, -20f, 115f, TextAlignmentOptions.TopLeft));
        TestSceneBuilderUtil.SetValue(context.DebugPanel, "statsText",
            CreateDebugText(context, parent, "StatsText", string.Empty, 19f, -140f, 140f, TextAlignmentOptions.TopLeft));
        TestSceneBuilderUtil.SetValue(context.DebugPanel, "upgradeText",
            CreateDebugText(context, parent, "UpgradeText", string.Empty, 19f, -285f, 95f, TextAlignmentOptions.TopLeft));
        // Dos líneas (estado + loadout de armas): 46 px caben 2 líneas de 18 pt con margen.
        // El borde inferior queda en -428 y la rejilla de botones arranca en -430.
        TestSceneBuilderUtil.SetValue(context.DebugPanel, "footerText",
            CreateDebugText(context, parent, "FooterText", string.Empty, 18f, -382f, 46f, TextAlignmentOptions.TopLeft));
    }

    private static TextMeshProUGUI CreateDebugText(BuildContext context, Transform parent, string name,
        string content, float size, float y, float height, TextAlignmentOptions alignment)
    {
        TextMeshProUGUI text = TestSceneBuilderUi.CreateText(name, parent, context.UiLayer, content, size,
            new Color(0.88f, 0.90f, 0.95f), alignment);

        text.textWrappingMode = TextWrappingModes.Normal;
        TestSceneBuilderUi.SetRect(text.rectTransform,
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(15f, y), new Vector2(DebugPanelWidth - 30f, height));

        return text;
    }
}
