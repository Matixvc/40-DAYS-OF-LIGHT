using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public static partial class TestSceneBuilder
{
    private static int debugButtonIndex;

    /// <summary>
    /// Rejilla de botones del sandbox. Cada botón se cablea con un listener persistente
    /// contra un método público de <see cref="TestEnvironmentController"/>: la UI no decide
    /// nada, sólo invoca. Gracias a eso el mismo conjunto de acciones se puede lanzar
    /// desde los botones o desde el teclado sin duplicar lógica.
    /// </summary>
    private static void BuildDebugButtons(BuildContext context, RectTransform parent)
    {
        debugButtonIndex = 0;

        // --- Hordas masivas ---
        AddDebugButton(context, parent, "Horda 10 (F1)", DebugActionColor, context.TestController.SpawnHordeSmall);
        AddDebugButton(context, parent, "Horda 50 (F2)", DebugActionColor, context.TestController.SpawnHordeMedium);
        AddDebugButton(context, parent, "Horda 100 (F3)", DebugActionColor, context.TestController.SpawnHordeLarge);

        // --- Limpieza ---
        AddDebugButton(context, parent, "Limpiar horda (F4)", DebugButtonColor, context.TestController.ClearTestSpawns);
        AddDebugButton(context, parent, "Matar cerca", DebugButtonColor, context.TestController.KillEnemiesAroundPlayer);
        AddDebugButton(context, parent, "Vaciar pools", DebugButtonColor, context.TestController.ClearEverything);

        // --- Progresión y mejoras ---
        AddDebugButton(context, parent, "Subir nivel (F5)", DebugActionColor, context.TestController.LevelUp);
        AddDebugButton(context, parent, "Mejora ‹", DebugButtonColor, context.TestController.SelectPreviousUpgrade);
        AddDebugButton(context, parent, "Mejora ›", DebugButtonColor, context.TestController.SelectNextUpgrade);

        AddDebugButton(context, parent, "Aplicar (F6)", DebugActionColor, context.TestController.ApplySelectedUpgrade);
        AddDebugButton(context, parent, "Ataque (F7)", DebugButtonColor, context.TestController.TogglePlayerAttack);
        AddDebugButton(context, parent, "IA enemiga (F8)", DebugButtonColor, context.TestController.ToggleEnemyAi);

        // --- Toggles del sistema ---
        AddDebugButton(context, parent, "Spawner (F9)", DebugButtonColor, context.TestController.ToggleSpawner);
        AddDebugButton(context, parent, "Modo dios", DebugButtonColor, context.TestController.ToggleGodMode);
        AddDebugButton(context, parent, "Curar", DebugButtonColor, context.TestController.HealPlayer);

        AddDebugButton(context, parent, "Reset stats", DebugButtonColor, context.TestController.ResetRunStats);
        AddDebugButton(context, parent, "Fin de ronda", DebugButtonColor, context.TestController.ForceEndRound);
        AddDebugButton(context, parent, "Ocultar (F10)", DebugButtonColor, context.DebugPanel.ToggleVisible);

        // --- Armas (Fase 3) ---
        AddDebugButton(context, parent, "Arma ‹", DebugButtonColor, context.TestController.SelectPreviousWeapon);
        AddDebugButton(context, parent, "Arma ›", DebugButtonColor, context.TestController.SelectNextWeapon);
        AddDebugButton(context, parent, "Equipar arma (F11)", DebugActionColor, context.TestController.CycleWeapon);

        AddDebugButton(context, parent, "Sistema armas (F12)", DebugActionColor, context.TestController.ToggleWeaponSystem);
        AddDebugButton(context, parent, "Vaciar armas", DebugDangerColor, context.TestController.ClearWeapons);
        AddDebugButton(context, parent, "Arma seleccionada", DebugButtonColor, context.TestController.EquipSelectedWeapon);

        // --- Reinicio: fila completa ---
        AddWideDebugButton(context, parent, "Reiniciar escena (Supr)", DebugDangerColor, context.TestController.RestartScene);
    }

    /// <summary>Añade un botón en la siguiente celda de la rejilla y lo cablea.</summary>
    private static void AddDebugButton(BuildContext context, RectTransform parent, string label, Color color, UnityAction action)
    {
        int index = debugButtonIndex++;

        int column = index % DebugColumns;
        int row = index / DebugColumns;

        float x = 15f + column * (DebugButtonWidth + DebugColumnGap) + DebugButtonWidth * 0.5f;
        float y = DebugFirstRowY - row * (DebugButtonHeight + DebugRowGap);

        Button button = TestSceneBuilderUi.CreateButton($"Btn_{index}", parent, context.UiLayer, label, 18f, color, Color.white, out _);

        TestSceneBuilderUi.SetRect((RectTransform)button.transform,
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(x, y), new Vector2(DebugButtonWidth, DebugButtonHeight));

        WireDebugButton(button, action);
    }

    /// <summary>Botón de ancho completo para la acción destructiva de reiniciar.</summary>
    private static void AddWideDebugButton(BuildContext context, RectTransform parent, string label, Color color, UnityAction action)
    {
        int row = debugButtonIndex / DebugColumns;

        // Se salta la celda que le tocaría a la rejilla para ocupar la fila entera.
        debugButtonIndex = (row + 1) * DebugColumns;

        float y = DebugFirstRowY - row * (DebugButtonHeight + DebugRowGap);

        Button button = TestSceneBuilderUi.CreateButton("Btn_Restart", parent, context.UiLayer, label, 20f, color, Color.white, out _);

        TestSceneBuilderUi.SetRect((RectTransform)button.transform,
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(DebugPanelWidth * 0.5f, y), new Vector2(DebugPanelWidth - 30f, 50f));

        WireDebugButton(button, action);
    }

    private static void WireDebugButton(Button button, UnityAction action)
    {
        button.onClick.RemoveAllListeners();
        UnityEventTools.AddPersistentListener(button.onClick, action);
    }
}
