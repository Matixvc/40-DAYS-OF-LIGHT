using TMPro;
using UnityEngine;
using UnityEngine.UI;

public static partial class TestSceneBuilder
{
    private const int GameplayCanvasOrder = 10;
    private const int UpgradeCardCount = 3;

    /// <summary>
    /// Canvas principal de la partida: panel de nivel, HUD y Game Over.
    /// Se construye desde cero, en vez de copiarlo de Prototype, para que la escena TEST
    /// sea mínima y su cableado se lea de un vistazo.
    /// </summary>
    internal static void BuildGameplayUi(BuildContext context)
    {
        Canvas canvas = TestSceneBuilderUi.CreateCanvas("UI_Canvas", context.UiLayer, GameplayCanvasOrder);
        context.UiCanvas = canvas;
        context.UiRoot = canvas.transform;

        BuildLevelUpPanel(context);
        BuildHud(context);
        BuildGameOverPanel(context);
        EnsureEventSystem();
    }

    /// <summary>
    /// Panel de selección de mejora. <see cref="LevelUpUI"/> exige un CanvasGroup (por
    /// RequireComponent) y anima un RectTransform: ambos se crean aquí explícitamente para
    /// cablearlos sin depender de la auto-resolución del propio script.
    /// </summary>
    private static void BuildLevelUpPanel(BuildContext context)
    {
        GameObject panelGo = TestSceneBuilderUi.CreateUiObject("LevelUpPanel", context.UiRoot, context.UiLayer);
        RectTransform panelRect = panelGo.GetComponent<RectTransform>();
        TestSceneBuilderUi.Stretch(panelRect);

        CanvasGroup group = panelGo.AddComponent<CanvasGroup>();

        GameObject backdrop = TestSceneBuilderUi.CreatePanel("Backdrop", panelGo.transform, context.UiLayer,
            new Color(0f, 0f, 0f, 0.7f));
        TestSceneBuilderUi.Stretch(backdrop.GetComponent<RectTransform>());

        TextMeshProUGUI title = TestSceneBuilderUi.CreateText("Title", panelGo.transform, context.UiLayer,
            "ELIGE TU MEJORA", 44f, new Color(1f, 0.88f, 0.45f), TextAlignmentOptions.Center);
        TestSceneBuilderUi.SetRect(title.rectTransform,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -60f), new Vector2(700f, 60f));

        LevelUpUI levelUp = panelGo.AddComponent<LevelUpUI>();
        context.LevelUpPanel = panelGo;
        context.LevelUpUi = levelUp;

        TestSceneBuilderUtil.SetValue(levelUp, "canvasGroup", group);
        TestSceneBuilderUtil.SetValue(levelUp, "panelRect", panelRect);
        TestSceneBuilderUtil.SetObjectArray(levelUp, "availableUpgrades", context.Upgrades);

        UpgradeCardUI[] cards = new UpgradeCardUI[UpgradeCardCount];

        for (int i = 0; i < UpgradeCardCount; i++)
        {
            cards[i] = CreateUpgradeCard(context, panelGo.transform, $"UpgradeButton_{i}", (i - 1) * 330f);
        }

        TestSceneBuilderUtil.SetObjectArray(levelUp, "upgradeCards", cards);
    }
}
