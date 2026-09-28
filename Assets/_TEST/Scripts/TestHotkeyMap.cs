using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Atajos de teclado del sandbox de pruebas, creados por código con el Input System.
///
/// Sigue el mismo patrón que <see cref="PlayerInputReader"/>: acciones construidas por
/// código (no tocan el asset InputSystem_Actions) y una lista <c>ownedActions</c> para
/// poder liberarlas sin afectar al EventSystem ni al input del jugador.
///
/// Importante: nunca se usa <see cref="PlayerInputReader"/>. El sandbox es un consumidor
/// paralelo; si compartiera acciones, al desregistarse dejaría al juego sin input.
///
/// Nota de teclas: <c>R</c> está libre porque PlayerInputReader ya la usa para reiniciar,
/// por eso el reinicio del sandbox va en <c>Backspace</c>.
///
/// Reparto de teclas de la Fase 2/3:
///   F6  → aplicar la mejora seleccionada. Si esa mejora concede un arma (grantsWeapon), la
///         equipa: es la vía directa para probar upgrades v2 y armas nuevas en tiempo real.
///   F11 → equipar (o subir de nivel) la siguiente arma del catálogo de prueba.
///   F12 → alternar entre el sistema de armas nuevo y el pulso original de PlayerAttack.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("TEST/Test Hotkey Map")]
public class TestHotkeyMap : MonoBehaviour
{
    public static TestHotkeyMap Instance { get; private set; }

    [Header("Atajos")]
    [Tooltip("Si se desactiva, el sandbox sigue siendo utilizable desde los botones del panel.")]
    [SerializeField] private bool enableHotkeys = true;

    [Header("Depuración")]
    [SerializeField] private bool logSetup = true;

    // --- Eventos del sandbox ---
    public event Action Spawn10;
    public event Action Spawn50;
    public event Action Spawn100;
    public event Action KillAll;
    public event Action LevelUp;
    public event Action ApplyUpgrade;
    public event Action TogglePlayerAttack;
    public event Action ToggleEnemyAi;
    public event Action ToggleSpawner;
    public event Action TogglePanel;
    public event Action RestartScene;

    // --- Fase 2/3: armas y modificadores ---
    public event Action CycleWeapon;
    public event Action ToggleWeaponSystem;

    /// <summary>Etiquetas de las acciones vivas, para el diagnóstico del panel.</summary>
    public int ActionCount => ownedActions.Count;

    private readonly List<InputAction> ownedActions = new List<InputAction>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[TestHotkeyMap] Ya existe una instancia activa: se desactiva el duplicado.", this);
            enabled = false;
            return;
        }

        Instance = this;

        if (enableHotkeys)
        {
            BuildActions();
        }
    }

    private void OnEnable()
    {
        EnableOwnedActions();
    }

    private void OnDisable()
    {
        DisableOwnedActions();
    }

    private void OnDestroy()
    {
        for (int i = 0; i < ownedActions.Count; i++)
        {
            ownedActions[i].Dispose();
        }

        ownedActions.Clear();

        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>Construye las acciones del sandbox y las registra en <c>ownedActions</c>.</summary>
    private void BuildActions()
    {
        Bind("Test_Spawn10", "<Keyboard>/f1", () => Spawn10);
        Bind("Test_Spawn50", "<Keyboard>/f2", () => Spawn50);
        Bind("Test_Spawn100", "<Keyboard>/f3", () => Spawn100);
        Bind("Test_KillAll", "<Keyboard>/f4", () => KillAll);
        Bind("Test_LevelUp", "<Keyboard>/f5", () => LevelUp);
        Bind("Test_ApplyUpgrade", "<Keyboard>/f6", () => ApplyUpgrade);
        Bind("Test_ToggleAttack", "<Keyboard>/f7", () => TogglePlayerAttack);
        Bind("Test_ToggleEnemyAi", "<Keyboard>/f8", () => ToggleEnemyAi);
        Bind("Test_ToggleSpawner", "<Keyboard>/f9", () => ToggleSpawner);
        Bind("Test_TogglePanel", "<Keyboard>/f10", () => TogglePanel);
        Bind("Test_CycleWeapon", "<Keyboard>/f11", () => CycleWeapon);
        Bind("Test_ToggleWeaponSystem", "<Keyboard>/f12", () => ToggleWeaponSystem);
        Bind("Test_Restart", "<Keyboard>/backspace", () => RestartScene);

        if (logSetup)
        {
            Debug.Log($"[TestHotkeyMap] {ownedActions.Count} atajos de prueba creados por código.", this);
        }
    }

    /// <summary>
    /// Crea una acción de botón con un único binding y engancha el callback de forma PEREZOSA.
    ///
    /// El getter (<c>Func&lt;Action&gt;</c>) se evalúa EN EL MOMENTO de la pulsación. Si se
    /// capturara el delegate en <c>BuildActions()</c> (llamado desde <c>Awake</c>) se congelaría
    /// un snapshot: <see cref="TestEnvironmentController"/> se suscribe en <c>Start</c>, es
    /// decir DESPUÉS, y el snapshot seguiría siendo null, con lo que ningún atajo F1-F12
    /// dispararía nada. Evaluar en el momento hace que el orden de scripts deje de importar.
    /// </summary>
    private void Bind(string name, string bindingPath, Func<Action> callback)
    {
        InputAction action = new InputAction(name, InputActionType.Button);
        action.AddBinding(bindingPath);
        action.performed += _ => callback.Invoke()?.Invoke();

        ownedActions.Add(action);
    }

    private void EnableOwnedActions()
    {
        for (int i = 0; i < ownedActions.Count; i++)
        {
            ownedActions[i].Enable();
        }
    }

    private void DisableOwnedActions()
    {
        for (int i = 0; i < ownedActions.Count; i++)
        {
            ownedActions[i].Disable();
        }
    }
}
