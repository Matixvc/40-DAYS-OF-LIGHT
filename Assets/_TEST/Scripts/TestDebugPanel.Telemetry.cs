using System.Text;
using TMPro;
using UnityEngine;

public partial class TestDebugPanel
{
    // Reutilizado en cada refresco para no generar basura 4 veces por segundo.
    private readonly StringBuilder builder = new StringBuilder(320);

    private void RefreshTelemetry()
    {
        BuildStatus();
        BuildStats();
        BuildUpgrade();
        BuildFooter();

        if (logTelemetry)
        {
            Debug.Log($"[TestDebugPanel] {statusText.text} | {statsText.text}");
        }
    }

    private void BuildStatus()
    {
        if (statusText == null)
        {
            return;
        }

        builder.Clear();
        builder.Append("<b>ARENA DE PRUEBAS</b>   FPS: ")
               .Append(Mathf.RoundToInt(1f / Mathf.Max(0.0001f, Time.smoothDeltaTime)))
               .Append("   frame: ")
               .Append((Time.smoothDeltaTime * 1000f).ToString("0.0"))
               .Append(" ms\n");

        builder.Append("NavMesh: ")
               .Append(controller.NavMeshReady ? "<color=#7CFC00>LISTO</color>" : "<color=#FF4444>PENDIENTE</color>")
               .Append("   Estado: ")
               .Append(GameStateController.Instance != null
                   ? GameStateController.Instance.CurrentState.ToString()
                   : "-")
               .Append("\nEnemigos: ")
               .Append(controller.TotalEnemiesAlive)
               .Append("   Horda del sandbox: ")
               .Append(controller.TestSpawnCount)
               .Append("   Armas: ")
               .Append(controller.EquippedWeaponCount)
               .Append(controller.UsingWeaponSystem
                   ? " <color=#7CFC00>(NUEVO)</color>"
                   : " <color=#A0A0A0>(CLASICO)</color>");

        statusText.text = builder.ToString();
    }

    private void BuildStats()
    {
        if (statsText == null)
        {
            return;
        }

        builder.Clear();

        if (controller.Stats != null)
        {
            RunStats stats = controller.Stats;
            builder.Append("Daño: ").Append(stats.AttackDamage.ToString("0.0"))
                   .Append("   Rango: ").Append(stats.AttackRange.ToString("0.00"))
                   .Append("   Cadencia: ").Append(stats.AttackInterval.ToString("0.00")).Append(" s\n")
                   .Append("Velocidad: ").Append(stats.MoveSpeed.ToString("0.00"))
                   .Append("   Sprint x").Append(stats.SprintMultiplier.ToString("0.00"));
        }
        else
        {
            builder.Append("<color=#FFAA00>RunStats no asignado</color>");
        }

        if (controller.PlayerHealth != null)
        {
            builder.Append("\nVida: ").Append(controller.PlayerHealth.CurrentHealth.ToString("0"))
                   .Append(" / ").Append(controller.PlayerHealth.MaxHealth.ToString("0"));
        }

        if (controller.LevelSystem != null)
        {
            builder.Append("   Nivel: ").Append(controller.LevelSystem.CurrentLevel)
                   .Append("   XP: ").Append(controller.LevelSystem.CurrentXP.ToString("0"))
                   .Append("/").Append(controller.LevelSystem.XPToNextLevel.ToString("0"));
        }

        if (controller.GodMode)
        {
            builder.Append("\n<color=#7CFC00>MODO DIOS ACTIVO</color>");
        }

        statsText.text = builder.ToString();
    }

    private void BuildUpgrade()
    {
        if (upgradeText == null)
        {
            return;
        }

        builder.Clear();
        builder.Append("<b>Mejora seleccionada:</b> ");

        if (controller.HasUpgrades)
        {
            UpgradeDataSO upgrade = controller.SelectedUpgrade;
            builder.Append(upgrade != null ? upgrade.upgradeName : "SIN ASIGNAR");
            builder.Append("   [")
                   .Append(controller.SelectedUpgradeIndex + 1)
                   .Append('/')
                   .Append(controller.TestUpgradeCount)
                   .Append(']');
        }
        else
        {
            builder.Append("<color=#FFAA00>ninguna asignada</color>");
        }

        builder.Append("\n<color=#A0A0A0>F5 nivel · F6 aplicar · F7 ataque · F8 IA · F9 spawner · F10 panel</color>");
        builder.Append("\n<color=#A0A0A0>F11 equipar arma · F12 sistema de armas · Supr reiniciar</color>");

        upgradeText.text = builder.ToString();
    }

    private void BuildFooter()
    {
        if (footerText == null)
        {
            return;
        }

        builder.Clear();
        builder.Append("Ataque: ").Append(controller.AttackOperational ? "ON" : "OFF")
               .Append("   IA: ").Append(controller.EnemyAiFrozen ? "CONGELADA" : "ACTIVA")
               .Append("   Spawner: ").Append(controller.ContinuousSpawnEnabled ? "ON" : "OFF");

        builder.Append("\nArmas: ").Append(controller.LoadoutSummary);

        footerText.text = builder.ToString();
    }
}
