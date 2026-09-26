using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Habilita los controles táctiles (joystick virtual + botones) en Android o dispositivos con pantalla táctil.
/// En PC el panel queda oculto y se mantiene el esquema teclado + ratón.
/// Todo el input virtual se canaliza a través de <see cref="PlayerInputReader"/>.
/// </summary>
[DisallowMultipleComponent]
public class TouchControlsUI : MonoBehaviour
{
    [Header("Detección de plataforma")]
    [Tooltip("Muestra los controles táctiles cuando la plataforma es móvil o hay pantalla táctil.")]
    [SerializeField] private bool enableOnTouchDevices = true;
    [Tooltip("Fuerza los controles en el Editor para poder probarlos con el ratón.")]
    [SerializeField] private bool forceVisibleInEditor = false;

    [Header("Referencias")]
    [Tooltip("Raíz del panel táctil que se activa/desactiva según la plataforma.")]
    [SerializeField] private GameObject controlsRoot;
    [SerializeField] private VirtualJoystick moveJoystick;
    [SerializeField] private VirtualButton sprintButton;
    [SerializeField] private VirtualButton pauseButton;
    [Tooltip("Texto opcional con el recordatorio de controles.")]
    [SerializeField] private TextMeshProUGUI controlSchemeText;

    [Header("Depuración")]
    [SerializeField] private bool logState = true;

    private PlayerInputReader inputReader;
    private bool touchControlsAllowed;

    /// <summary>True si la plataforma actual usa controles táctiles.</summary>
    public static bool IsTouchPlatform()
    {
        if (Application.isMobilePlatform) return true;

#if UNITY_EDITOR
        // En el Editor solo activar si el desarrollador lo fuerza o es un dispositivo remoto
        return false;
#else
        return Touchscreen.current != null;
#endif
    }

    private void Start()
    {
        touchControlsAllowed = forceVisibleInEditor || (enableOnTouchDevices && IsTouchPlatform());

        SetVisible(touchControlsAllowed);

        if (!touchControlsAllowed)
        {
            if (logState)
            {
                Debug.Log("[TouchControlsUI] Controles táctiles desactivados (escritorio: teclado + ratón/mando).", this);
            }

            return;
        }

        ResolveInputReader();

        if (moveJoystick != null)
        {
            moveJoystick.ValueChanged += HandleMoveChanged;
            inputReader?.SetVirtualMove(moveJoystick.Value);
        }

        if (sprintButton != null)
        {
            sprintButton.Pressed += HandleSprintPressed;
            sprintButton.Released += HandleSprintReleased;
        }

        if (pauseButton != null)
        {
            pauseButton.Pressed += HandlePausePressed;
        }

        if (controlSchemeText != null)
        {
            controlSchemeText.text = "Joystick: mover · SPRINT: correr · II: pausa";
        }

        GameStateController stateController = GameStateController.Instance;
        if (stateController != null)
        {
            stateController.OnStateChanged += HandleGameStateChanged;
        }

        if (logState)
        {
            Debug.Log("[TouchControlsUI] Controles táctiles ACTIVADOS.", this);
        }
    }

    private void OnDestroy()
    {
        if (moveJoystick != null) moveJoystick.ValueChanged -= HandleMoveChanged;

        if (sprintButton != null)
        {
            sprintButton.Pressed -= HandleSprintPressed;
            sprintButton.Released -= HandleSprintReleased;
        }

        if (pauseButton != null) pauseButton.Pressed -= HandlePausePressed;

        if (GameStateController.Instance != null)
        {
            GameStateController.Instance.OnStateChanged -= HandleGameStateChanged;
        }

        // Dejar el input virtual a cero al salir de la escena.
        if (inputReader != null)
        {
            inputReader.SetVirtualMove(Vector2.zero);
            inputReader.SetVirtualSprint(false);
        }
    }

    private void HandleGameStateChanged(GameState previous, GameState current)
    {
        if (!touchControlsAllowed) return;

        // Ocultar los controles táctiles de juego al pausar o mostrar mejoras para evitar solapamientos
        bool shouldBeVisible = (current == GameState.Playing);
        SetVisible(shouldBeVisible);

        if (!shouldBeVisible && inputReader != null)
        {
            inputReader.SetVirtualMove(Vector2.zero);
            inputReader.SetVirtualSprint(false);
        }
    }

    private void SetVisible(bool visible)
    {
        if (controlsRoot == null)
        {
            if (logState)
            {
                Debug.LogWarning("[TouchControlsUI] No hay 'Controls Root' asignado: no se puede ocultar el panel táctil.", this);
            }

            return;
        }

        controlsRoot.SetActive(visible);
    }

    private void ResolveInputReader()
    {
        inputReader = PlayerInputReader.Instance;

        if (inputReader == null)
        {
            inputReader = FindAnyObjectByType<PlayerInputReader>();
        }

        if (inputReader == null && logState)
        {
            Debug.LogWarning(
                "[TouchControlsUI] No se encontró PlayerInputReader: los controles táctiles no moverán al jugador.",
                this);
        }
    }

    private void HandleMoveChanged(Vector2 value)
    {
        inputReader?.SetVirtualMove(value);
    }

    private void HandleSprintPressed()
    {
        inputReader?.SetVirtualSprint(true);
    }

    private void HandleSprintReleased()
    {
        inputReader?.SetVirtualSprint(false);
    }

    private void HandlePausePressed()
    {
        inputReader?.TriggerPause();
    }

    [ContextMenu("Registrar estado de los controles táctiles")]
    private void DebugState()
    {
        Debug.Log(
            $"[TouchControlsUI:'{name}'] Plataforma táctil: {IsTouchPlatform()} | Forzado en Editor: {forceVisibleInEditor} | " +
            $"Root: {(controlsRoot != null ? controlsRoot.name : "NO ASIGNADO")} | " +
            $"Joystick: {moveJoystick != null} · Sprint: {sprintButton != null} · Pausa: {pauseButton != null} | " +
            $"PlayerInputReader: {(inputReader != null ? inputReader.name : "NO ENCONTRADO")}",
            this);
    }
}
