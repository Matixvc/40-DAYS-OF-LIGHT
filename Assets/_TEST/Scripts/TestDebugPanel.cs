using TMPro;
using UnityEngine;

/// <summary>
/// Panel de depuración del sandbox. Es la ÚNICA capa que dibuja: no decide nada, se
/// limita a leer las propiedades públicas de <see cref="TestEnvironmentController"/> y a
/// mostrar el resultado de cada comando.
///
/// Los botones se cablean en el Editor con <c>UnityEventTools.AddPersistentListener</c>
/// contra los métodos públicos del controlador: la UI y la lógica quedan desacopladas y
/// el sandbox también se puede pilotar sólo con teclado.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("TEST/Test Debug Panel")]
public partial class TestDebugPanel : MonoBehaviour
{
    [Header("Controlador")]
    [SerializeField] private TestEnvironmentController controller;

    [Header("Visibilidad")]
    [Tooltip("Si es false, el panel arranca oculto y se abre con F10.")]
    [SerializeField] private bool startVisible = true;
    [SerializeField] private CanvasGroup group;
    [SerializeField] private RectTransform panelRect;

    [Header("Textos")]
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private TextMeshProUGUI statsText;
    [SerializeField] private TextMeshProUGUI upgradeText;
    [SerializeField] private TextMeshProUGUI footerText;

    [Header("Refresco")]
    [Tooltip("Segundos entre refrescos. Se mide en tiempo real para que el panel siga " +
             "leyéndose con la partida congelada (level up, pausa o hit stop).")]
    [SerializeField, Min(0.05f)] private float refreshInterval = 0.25f;

    [Header("Depuración")]
    [SerializeField] private bool logTelemetry = false;

    public bool IsVisible => isVisible;

    private float nextRefreshTime;
    private bool isVisible;

    private void Awake()
    {
        if (group == null)
        {
            group = GetComponentInParent<CanvasGroup>();
        }

        SetVisible(startVisible);
    }

    private void Start()
    {
        Bind(controller);
    }

    /// <summary>Conecta el panel con el controlador. Idempotente a propósito.</summary>
    public void Bind(TestEnvironmentController value)
    {
        if (value != null)
        {
            controller = value;
        }

        if (controller == null)
        {
            controller = FindAnyObjectByType<TestEnvironmentController>();
        }

        if (controller == null)
        {
            Debug.LogError(
                "[TestDebugPanel] No hay TestEnvironmentController en la escena: el panel no podrá ejecutar comandos.",
                this);
        }
    }

    public void ToggleVisible() => SetVisible(!isVisible);

    public void SetVisible(bool value)
    {
        isVisible = value;

        if (group != null)
        {
            group.alpha = value ? 1f : 0f;

            // El panel SÍ necesita recibir clics: sus botones son el sandbox. Lo que no
            // puede hacer es robarlos cuando está oculto, de ahí el RaycastTarget=false
            // en los textos y este flags en el CanvasGroup.
            group.blocksRaycasts = value;
            group.interactable = value;
        }

        if (panelRect != null && panelRect.gameObject != gameObject)
        {
            // Nunca se desactiva el propio GameObject: el controlador del sandbox vive
            // aquí, y si se apagara, F10 no podría volver a encender el panel.
            panelRect.gameObject.SetActive(value);
        }

        if (value)
        {
            // Refresco inmediato al abrir: evita mostrar datos rancios medio segundo.
            nextRefreshTime = 0f;
        }
    }

    private void Update()
    {
        if (!isVisible || controller == null || Time.realtimeSinceStartup < nextRefreshTime)
        {
            return;
        }

        nextRefreshTime = Time.realtimeSinceStartup + refreshInterval;
        RefreshTelemetry();
    }
}
