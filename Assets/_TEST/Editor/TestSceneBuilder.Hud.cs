using TMPro;
using UnityEngine;
using UnityEngine.UI;

public static partial class TestSceneBuilder
{
    private static readonly Color BarBackground = new Color(0.08f, 0.08f, 0.10f, 0.85f);

    /// <summary>
    /// HUD mínimo: vida, XP, sprint y los textos que consume <see cref="RunHUD"/>.
    /// Los tres scripts de barra auto-resuelven su objetivo, pero se cablea igualmente
    /// para que una referencia mal formada falle en el Editor y no en Play Mode.
    /// </summary>
    private static void BuildHud(BuildContext context)
    {
        GameObject hudGo = TestSceneBuilderUi.CreateUiObject("HUD", context.UiRoot, context.UiLayer);
        RectTransform hudRect = hudGo.GetComponent<RectTransform>();
        TestSceneBuilderUi.Stretch(hudRect);

        HealthComponent playerHealth = context.Player != null
            ? context.Player.GetComponent<HealthComponent>()
            : null;

        PlayerController playerController = context.Player != null
            ? context.Player.GetComponent<PlayerController>()
            : null;

        PlayerLevelSystem levelSystem = context.Player != null
            ? context.Player.GetComponent<PlayerLevelSystem>()
            : null;

        // --- Vida ---
        Slider healthSlider = TestSceneBuilderUi.CreateBar("HealthBar", hudRect, context.UiLayer,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -40f), new Vector2(620f, 34f),
            BarBackground, new Color(0.30f, 0.80f, 0.35f), 1f);

        HealthBarUI healthUi = healthSlider.gameObject.AddComponent<HealthBarUI>();
        TestSceneBuilderUtil.SetValue(healthUi, "healthSlider", healthSlider);
        TestSceneBuilderUtil.SetValue(healthUi, "fillImage", healthSlider.fillRect.GetComponent<Image>());
        TestSceneBuilderUtil.SetValue(healthUi, "targetHealth", playerHealth);

        // --- XP ---
        Slider xpSlider = TestSceneBuilderUi.CreateBar("XPBar", hudRect, context.UiLayer,
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 40f), new Vector2(620f, 22f),
            BarBackground, new Color(0.45f, 0.75f, 1f), 0f);

        XPBarUI xpUi = xpSlider.gameObject.AddComponent<XPBarUI>();
        TestSceneBuilderUtil.SetValue(xpUi, "xpSlider", xpSlider);
        TestSceneBuilderUtil.SetValue(xpUi, "fillImage", xpSlider.fillRect.GetComponent<Image>());
        TestSceneBuilderUtil.SetValue(xpUi, "playerLevelSystem", levelSystem);

        // --- Sprint ---
        Slider staminaSlider = TestSceneBuilderUi.CreateBar("StaminaBar", hudRect, context.UiLayer,
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(200f, -40f), new Vector2(280f, 22f),
            BarBackground, Color.cyan, 1f);

        StaminaBarUI staminaUi = staminaSlider.gameObject.AddComponent<StaminaBarUI>();
        TestSceneBuilderUtil.SetValue(staminaUi, "staminaSlider", staminaSlider);
        TestSceneBuilderUtil.SetValue(staminaUi, "fillImage", staminaSlider.fillRect.GetComponent<Image>());
        TestSceneBuilderUtil.SetValue(staminaUi, "playerController", playerController);

        BuildRunHudTexts(context, hudRect);
    }

    /// <summary>Textos y temporizador que <see cref="RunHUD"/> mantiene al día.</summary>
    private static void BuildRunHudTexts(BuildContext context, Transform parent)
    {
        TextMeshProUGUI dayText = CreateHudText(context, parent, "DayText", "Día 1 / 40", 30f,
            new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-180f, -40f), new Vector2(320f, 40f));
        TextMeshProUGUI difficultyText = CreateHudText(context, parent, "DifficultyText", "Dificultad: Normal", 22f,
            new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-180f, -82f), new Vector2(320f, 32f));
        TextMeshProUGUI warningText = CreateHudText(context, parent, "WarningText", string.Empty, 30f,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -230f), new Vector2(900f, 50f));
        TextMeshProUGUI timerText = CreateHudText(context, parent, "TimerText", "0 s", 24f,
            new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-180f, -125f), new Vector2(320f, 32f));

        warningText.gameObject.SetActive(false);

        Slider timerSlider = TestSceneBuilderUi.CreateBar("TimerSlider", parent, context.UiLayer,
            new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(-180f, -165f), new Vector2(320f, 16f),
            BarBackground, new Color(1f, 0.75f, 0.25f), 1f);

        RunHUD runHud = parent.gameObject.AddComponent<RunHUD>();
        TestSceneBuilderUtil.SetValue(runHud, "dayText", dayText);
        TestSceneBuilderUtil.SetValue(runHud, "difficultyText", difficultyText);
        TestSceneBuilderUtil.SetValue(runHud, "warningText", warningText);
        TestSceneBuilderUtil.SetValue(runHud, "timerText", timerText);
        TestSceneBuilderUtil.SetValue(runHud, "timerSlider", timerSlider);
        TestSceneBuilderUtil.SetValue(runHud, "runDirector", context.RunDirector);
    }

    private static TextMeshProUGUI CreateHudText(BuildContext context, Transform parent, string name, string content,
        float size, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 rectSize)
    {
        TextMeshProUGUI text = TestSceneBuilderUi.CreateText(name, parent, context.UiLayer, content, size,
            Color.white, TextAlignmentOptions.Right);

        TestSceneBuilderUi.SetRect(text.rectTransform, anchorMin, anchorMax, pivot, position, rectSize);
        return text;
    }
}
