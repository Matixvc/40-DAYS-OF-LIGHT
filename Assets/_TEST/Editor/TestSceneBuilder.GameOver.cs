using TMPro;
using UnityEngine;
using UnityEngine.UI;

public static partial class TestSceneBuilder
{
    /// <summary>
    /// Panel de Game Over. Lo exige <c>GameManager</c> (si falta, el fin de partida se
    /// congela sin mostrar nada), así que el sandbox lo construye aunque no sea su objeto
    /// de prueba: sin él no se puede probar la secuencia de muerte.
    /// </summary>
    private static void BuildGameOverPanel(BuildContext context)
    {
        GameObject panelGo = TestSceneBuilderUi.CreateUiObject("GameOverPanel", context.UiRoot, context.UiLayer);
        RectTransform panelRect = panelGo.GetComponent<RectTransform>();
        TestSceneBuilderUi.Stretch(panelRect);

        Image background = panelGo.AddComponent<Image>();
        background.color = new Color(0.04f, 0.03f, 0.07f, 0.93f);

        CanvasGroup group = panelGo.AddComponent<CanvasGroup>();

        TextMeshProUGUI title = TestSceneBuilderUi.CreateText("Title", panelGo.transform, context.UiLayer,
            "FIN DE LA PARTIDA", 52f, new Color(0.95f, 0.35f, 0.35f), TextAlignmentOptions.Center);
        TestSceneBuilderUi.SetRect(title.rectTransform,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, 120f), new Vector2(800f, 70f));

        TextMeshProUGUI statsText = TestSceneBuilderUi.CreateText("StatsText", panelGo.transform, context.UiLayer,
            "Sobreviviste hasta el Nivel 1", 30f, Color.white, TextAlignmentOptions.Center);
        TestSceneBuilderUi.SetRect(statsText.rectTransform,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, 20f), new Vector2(800f, 50f));

        Button retry = TestSceneBuilderUi.CreateButton("RetryButton", panelGo.transform, context.UiLayer,
            "Reintentar (R)", 30f, new Color(0.28f, 0.22f, 0.40f), Color.white, out TextMeshProUGUI retryLabel);

        TestSceneBuilderUi.SetRect(retryLabel.rectTransform,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        TestSceneBuilderUi.SetRect((RectTransform)retry.transform,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, -80f), new Vector2(340f, 70f));

        context.GameOverPanel = panelGo;
        context.GameOverCanvasGroup = group;
        context.GameOverPanelRect = panelRect;
        context.GameOverStatsText = statsText;
        context.QuitToMenuButton = retry;

        // Arranca oculto: GameManager lo vuelve a managear en su Start().
        panelGo.SetActive(false);
    }
}
