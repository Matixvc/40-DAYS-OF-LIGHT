using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Punto ÚNICO de lectura de input del jugador con el paquete Input System.
/// Traduce las acciones (teclado/ratón y mando) a valores y eventos, de forma que
/// PlayerController, PauseMenu y GameManager no conozcan la API de input.
///
/// Resolución en cascada:
///   1) InputActionReference asignada en el Inspector (recomendado).
///   2) Acción del InputActionAsset localizada por nombre (Player/Move, Player/Sprint, ...).
///   3) Acciones construidas por código con bindings de teclado + mando,
///      para que el juego nunca se quede sin input.
/// </summary>
[DefaultExecutionOrder(-500)]
[DisallowMultipleComponent]
public class PlayerInputReader : MonoBehaviour
{
    public static PlayerInputReader Instance { get; private set; }

    [Header("Asset de acciones (InputSystem_Actions)")]
    [Tooltip("Arrastra Assets/Extra/InputSystem_Actions.inputactions")]
    [SerializeField] private InputActionAsset actionsAsset;

    [Header("Acciones explícitas (opcional: tienen prioridad)")]
    [SerializeField] private InputActionReference moveActionReference;
    [SerializeField] private InputActionReference sprintActionReference;
    [SerializeField] private InputActionReference pauseActionReference;
    [SerializeField] private InputActionReference restartActionReference;

    [Header("Nombres de acción dentro del asset (formato 'Mapa/Acción')")]
    [SerializeField] private string moveActionName = "Player/Move";
    [SerializeField] private string sprintActionName = "Player/Sprint";
    [SerializeField] private string pauseActionName = "Player/Pause";
    [SerializeField] private string restartActionName = "Player/Restart";

    [Header("Depuración")]
    [SerializeField] private bool logInputSetup = true;

    // Rutas alternativas si el asset todavía no tiene acciones propias de pausa/reinicio.
    private static readonly string[] PauseFallbackNames = { "UI/Cancel", "UI/Pause" };
    private static readonly string[] RestartFallbackNames = { "UI/Submit", "UI/Restart" };

    private InputAction moveAction;
    private InputAction sprintAction;
    private InputAction pauseAction;
    private InputAction restartAction;

    // Solo estas acciones son nuestras: se pueden desactivar y liberar sin afectar al EventSystem.
    private readonly List<InputAction> ownedActions = new List<InputAction>();

    // Entrada virtual (táctil): se suma al input de hardware (teclado/mando).
    private Vector2 virtualMove;
    private bool virtualSprint;

    /// <summary>Dirección de movimiento normalizada (-1..1). (0,0) si no hay input.</summary>
    public Vector2 Move { get; private set; }

    /// <summary>True mientras se mantiene pulsado sprint (Shift/Espacio o stick del mando).</summary>
    public bool SprintHeld { get; private set; }

    /// <summary>Se lanza al pulsar pausa (Escape o botón del mando).</summary>
    public event Action PausePressed;

    /// <summary>Se lanza al pulsar reinicio (R o botón del mando).</summary>
    public event Action RestartPressed;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning(
                "[PlayerInputReader] Ya existe una instancia activa. Se desactiva el duplicado: mantén solo una, en el Player.",
                this);
            enabled = false;
            return;
        }

        Instance = this;
        ResolveActions();
    }

    private void OnEnable()
    {
        EnableActions();
        SubscribeCallbacks();
    }

    private void OnDisable()
    {
        UnsubscribeCallbacks();
        DisableOwnedActions();

        Move = Vector2.zero;
        SprintHeld = false;

        virtualMove = Vector2.zero;
        virtualSprint = false;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        foreach (InputAction action in ownedActions)
        {
            action.Dispose();
        }

        ownedActions.Clear();
    }

    /// <summary>
    /// Los ejes continuos se leen por polling cada frame (patrón recomendado para Move/Sprint).
    /// </summary>
    private void Update()
    {
        Vector2 hardwareMove = moveAction != null ? moveAction.ReadValue<Vector2>() : Vector2.zero;

        // El joystick táctil se SUMA al input de hardware: teclado, mando y pantalla conviven.
        Move = Vector2.ClampMagnitude(hardwareMove + virtualMove, 1f);

        bool hardwareSprint = sprintAction != null && sprintAction.IsPressed();
        SprintHeld = hardwareSprint || virtualSprint;
    }

    // ======================================================================
    // ENTRADA VIRTUAL (TÁCTIL)
    // ======================================================================

    /// <summary>Fija la dirección del joystick virtual (lo llama TouchControlsUI).</summary>
    public void SetVirtualMove(Vector2 value)
    {
        virtualMove = Vector2.ClampMagnitude(value, 1f);
    }

    /// <summary>Fija el estado del botón virtual de sprint.</summary>
    public void SetVirtualSprint(bool pressed)
    {
        virtualSprint = pressed;
    }

    /// <summary>Dispara la pausa desde un botón táctil.</summary>
    public void TriggerPause()
    {
        PausePressed?.Invoke();
    }

    // ======================================================================
    // RESOLUCIÓN DE ACCIONES
    // ======================================================================

    private void ResolveActions()
    {
        moveAction = ResolveAction(moveActionReference, moveActionName, null, "Move");
        sprintAction = ResolveAction(sprintActionReference, sprintActionName, null, "Sprint");
        pauseAction = ResolveAction(pauseActionReference, pauseActionName, PauseFallbackNames, "Pause");
        restartAction = ResolveAction(restartActionReference, restartActionName, RestartFallbackNames, "Restart");

        if (moveAction == null || sprintAction == null || pauseAction == null || restartAction == null)
        {
            CreateFallbackActions();
        }

        if (logInputSetup)
        {
            Debug.Log(
                "[PlayerInputReader] Input listo | " +
                $"Move: {Describe(moveAction)} | Sprint: {Describe(sprintAction)} | " +
                $"Pause: {Describe(pauseAction)} | Restart: {Describe(restartAction)}",
                this);
        }
    }

    private InputAction ResolveAction(
        InputActionReference reference,
        string actionPath,
        string[] fallbackPaths,
        string label)
    {
        if (reference != null && reference.action != null)
        {
            return reference.action;
        }

        if (actionsAsset == null)
        {
            return null; // Se creará una acción por código
        }

        InputAction action = actionsAsset.FindAction(actionPath, throwIfNotFound: false);

        if (action == null && fallbackPaths != null)
        {
            for (int i = 0; i < fallbackPaths.Length && action == null; i++)
            {
                action = actionsAsset.FindAction(fallbackPaths[i], throwIfNotFound: false);
            }
        }

        if (action == null && logInputSetup)
        {
            Debug.LogWarning(
                $"[PlayerInputReader] No se encontró '{actionPath}' en el asset. Se usará una acción de respaldo por código para '{label}'.",
                this);
        }

        return action;
    }

    /// <summary>
    /// Crea por código las acciones que no se hayan podido resolver del asset,
    /// con bindings de teclado y de mando para no perder el soporte de gamepad.
    /// </summary>
    private void CreateFallbackActions()
    {
        if (moveAction == null)
        {
            moveAction = new InputAction("Move", InputActionType.Value, expectedControlType: "Vector2");

            moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");

            moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow")
                .With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow")
                .With("Right", "<Keyboard>/rightArrow");

            moveAction.AddBinding("<Gamepad>/leftStick");

            ownedActions.Add(moveAction);
        }

        if (sprintAction == null)
        {
            sprintAction = new InputAction("Sprint", InputActionType.Button);
            sprintAction.AddBinding("<Keyboard>/leftShift");
            sprintAction.AddBinding("<Keyboard>/space");
            sprintAction.AddBinding("<Gamepad>/leftStickPress");

            ownedActions.Add(sprintAction);
        }

        if (pauseAction == null)
        {
            pauseAction = new InputAction("Pause", InputActionType.Button);
            pauseAction.AddBinding("<Keyboard>/escape");
            pauseAction.AddBinding("<Gamepad>/start");
            pauseAction.AddBinding("<Gamepad>/buttonEast");

            ownedActions.Add(pauseAction);
        }

        if (restartAction == null)
        {
            restartAction = new InputAction("Restart", InputActionType.Button);
            restartAction.AddBinding("<Keyboard>/r");
            restartAction.AddBinding("<Gamepad>/select");

            ownedActions.Add(restartAction);
        }

        if (logInputSetup && ownedActions.Count > 0)
        {
            Debug.Log(
                $"[PlayerInputReader] Se crearon {ownedActions.Count} acción(es) por código con bindings de teclado y mando.",
                this);
        }
    }

    private static string Describe(InputAction action)
    {
        if (action == null)
        {
            return "—";
        }

        return $"{action.actionMap?.name}/{action.name}".TrimStart('/');
    }

    // ======================================================================
    // ACTIVACIÓN Y CALLBACKS
    // ======================================================================

    private void EnableActions()
    {
        moveAction?.Enable();
        sprintAction?.Enable();
        pauseAction?.Enable();
        restartAction?.Enable();
    }

    private void DisableOwnedActions()
    {
        // Solo se desactivan las acciones creadas por código: las del asset pueden
        // estar en uso por el EventSystem (por ejemplo UI/Cancel o UI/Submit).
        for (int i = 0; i < ownedActions.Count; i++)
        {
            ownedActions[i].Disable();
        }
    }

    private void SubscribeCallbacks()
    {
        if (pauseAction != null)
        {
            pauseAction.performed -= HandlePausePerformed;
            pauseAction.performed += HandlePausePerformed;
        }

        if (restartAction != null)
        {
            restartAction.performed -= HandleRestartPerformed;
            restartAction.performed += HandleRestartPerformed;
        }
    }

    private void UnsubscribeCallbacks()
    {
        if (pauseAction != null)
        {
            pauseAction.performed -= HandlePausePerformed;
        }

        if (restartAction != null)
        {
            restartAction.performed -= HandleRestartPerformed;
        }
    }

    private void HandlePausePerformed(InputAction.CallbackContext context)
    {
        PausePressed?.Invoke();
    }

    private void HandleRestartPerformed(InputAction.CallbackContext context)
    {
        RestartPressed?.Invoke();
    }
}
