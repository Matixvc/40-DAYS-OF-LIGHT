using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public static partial class TestSceneBuilder
{
    /// <summary>
    /// Una carta de mejora: fondo, botón y el componente <see cref="UpgradeCardUI"/>.
    /// Las tres cartas se crean a mano en posiciones fijas en vez de con un
    /// HorizontalLayoutGroup: el resultado es idéntico y el cableado es trivial de leer.
    /// </summary>
    private static UpgradeCardUI CreateUpgradeCard(BuildContext context, Transform parent, string name, float xOffset)
    {
        GameObject cardGo = TestSceneBuilderUi.CreateUiObject(name, parent, context.UiLayer);
        RectTransform cardRect = cardGo.GetComponent<RectTransform>();
        TestSceneBuilderUi.SetRect(cardRect,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(xOffset, -20f), new Vector2(300f, 420f));

        Image background = cardGo.AddComponent<Image>();
        background.color = new Color(0.16f, 0.13f, 0.26f, 0.96f);

        Button button = cardGo.AddComponent<Button>();
        button.targetGraphic = background;

        Transform content = cardGo.transform;

        TextMeshProUGUI title = TestSceneBuilderUi.CreateText("Title", content, context.UiLayer,
            "—", 26f, Color.white, TextAlignmentOptions.Top);
        TestSceneBuilderUi.SetRect(title.rectTransform,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -30f), new Vector2(260f, 70f));

        TextMeshProUGUI description = TestSceneBuilderUi.CreateText("Description", content, context.UiLayer,
            "—", 19f, new Color(0.82f, 0.82f, 0.88f), TextAlignmentOptions.Top);
        description.textWrappingMode = TextWrappingModes.Normal;
        TestSceneBuilderUi.SetRect(description.rectTransform,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -200f), new Vector2(260f, 180f));

        GameObject iconGo = TestSceneBuilderUi.CreatePanel("Icon", content, context.UiLayer, Color.white);
        TestSceneBuilderUi.SetRect(iconGo.GetComponent<RectTransform>(),
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -120f), new Vector2(96f, 96f));

        Image icon = iconGo.GetComponent<Image>();
        icon.enabled = false;

        UpgradeCardUI card = cardGo.AddComponent<UpgradeCardUI>();
        TestSceneBuilderUtil.SetValue(card, "titleText", title);
        TestSceneBuilderUtil.SetValue(card, "descriptionText", description);
        TestSceneBuilderUtil.SetValue(card, "iconImage", icon);
        TestSceneBuilderUtil.SetValue(card, "selectButton", button);

        return card;
    }

    /// <summary>
    /// EventSystem con el módulo del Input System nuevo. El proyecto tiene
    /// <c>activeInputHandler: 1</c>, así que el StandaloneInputModule heredado no recibe
    /// ningún evento: sin este módulo los botones del sandbox se quedarían muertos.
    /// </summary>
    private static void EnsureEventSystem()
    {
        if (Object.FindAnyObjectByType<EventSystem>() != null)
        {
            return;
        }

        GameObject eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        InputSystemUIInputModule module = eventSystem.GetComponent<InputSystemUIInputModule>();

        // Sin asset de acciones asignado, el módulo necesita las suyas por defecto para
        // que Point, Click y Submit lleguen a los botones.
        if (module.actionsAsset == null)
        {
            module.AssignDefaultActions();
        }
    }
}
