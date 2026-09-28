using UnityEngine;

public partial class TestEnvironmentController
{
    [Header("Armas de prueba (Fase 3)")]
    [Tooltip("Catálogo que recorre F11. Cada pulsación selecciona la siguiente y la equipa " +
             "(o la sube de nivel si ya estaba equipada).")]
    [SerializeField] private WeaponDataSO[] testWeapons = new WeaponDataSO[0];

    /// <summary>Etiqueta del arma seleccionada en el catálogo (para logs y panel).</summary>
    public string SelectedWeaponLabel
    {
        get
        {
            if (TestWeaponCount == 0)
            {
                return "(catálogo vacío)";
            }

            WeaponDataSO weapon = testWeapons[SelectedWeaponIndex];
            return weapon != null ? $"{weapon.weaponName} [{weapon.archetype}]" : "(entrada vacía)";
        }
    }

    /// <summary>Selección de arma en el catálogo, para navegar con ‹ › sin equipar.</summary>
    public void SelectPreviousWeapon()
    {
        if (TestWeaponCount == 0)
        {
            return;
        }

        SelectedWeaponIndex = (SelectedWeaponIndex - 1 + TestWeaponCount) % TestWeaponCount;

        if (logActions)
        {
            Debug.Log($"[TestEnvironmentController] Arma seleccionada: {SelectedWeaponLabel}.", this);
        }
    }

    /// <summary>Selección de arma en el catálogo, para navegar con ‹ › sin equipar.</summary>
    public void SelectNextWeapon()
    {
        if (TestWeaponCount == 0)
        {
            return;
        }

        SelectedWeaponIndex = (SelectedWeaponIndex + 1) % TestWeaponCount;

        if (logActions)
        {
            Debug.Log($"[TestEnvironmentController] Arma seleccionada: {SelectedWeaponLabel}.", this);
        }
    }

    /// <summary>Equipa (o sube de nivel) el arma sobre la que está el cursor del catálogo.</summary>
    public void EquipSelectedWeapon()
    {
        if (weaponController == null)
        {
            LogMissingWeaponController("equipar armas");
            return;
        }

        if (TestWeaponCount == 0)
        {
            if (logActions)
            {
                Debug.LogWarning(
                    "[TestEnvironmentController] El catálogo de armas de prueba está vacío: " +
                    "asigna WeaponDataSO en el campo 'Test Weapons'.",
                    this);
            }

            return;
        }

        WeaponDataSO weapon = testWeapons[SelectedWeaponIndex];

        if (weapon == null)
        {
            return;
        }

        bool changed = weaponController.EquipWeapon(weapon);

        if (logActions)
        {
            Debug.Log(
                $"[TestEnvironmentController] '{weapon.weaponName}' ({weapon.archetype}): " +
                $"{(changed ? "EQUIPADA/MEJORADA" : "SIN CAMBIOS")} | {weaponController.GetLoadoutSummary()}",
                this);
        }
    }

    /// <summary>
    /// Selecciona la siguiente arma del catálogo y la equipa. Repetir F11 sube el arma de nivel
    /// en cuanto el catálogo da la vuelta, así que sirve para comprobar el escalado por nivel.
    /// </summary>
    public void CycleWeapon()
    {
        if (weaponController == null)
        {
            LogMissingWeaponController("equipar armas");
            return;
        }

        if (TestWeaponCount == 0)
        {
            if (logActions)
            {
                Debug.LogWarning(
                    "[TestEnvironmentController] El catálogo de armas de prueba está vacío: " +
                    "asigna WeaponDataSO en el campo 'Test Weapons'.",
                    this);
            }

            return;
        }

        WeaponDataSO weapon = testWeapons[Mathf.Clamp(SelectedWeaponIndex, 0, TestWeaponCount - 1)];

        // El índice avanza SIEMPRE, aunque el arma no se pueda equipar (ranuras llenas o nivel
        // máximo): así F11 nunca se queda atascada en una entrada inválida.
        SelectedWeaponIndex = (SelectedWeaponIndex + 1) % TestWeaponCount;

        if (weapon == null)
        {
            return;
        }

        bool changed = weaponController.EquipWeapon(weapon);

        if (logActions)
        {
            Debug.Log(
                $"[TestEnvironmentController] F11 -> '{weapon.weaponName}' ({weapon.archetype}): " +
                $"{(changed ? "EQUIPADA/MEJORADA" : "SIN CAMBIOS")} | {weaponController.GetLoadoutSummary()}",
                this);
        }
    }

    /// <summary>
    /// Equipa el arma indicada del catálogo, sin mover el cursor. Es la vía de los botones.
    /// </summary>
    public void EquipWeaponAt(int index)
    {
        if (weaponController == null)
        {
            LogMissingWeaponController("equipar armas");
            return;
        }

        if (testWeapons == null || index < 0 || index >= testWeapons.Length)
        {
            return;
        }

        WeaponDataSO weapon = testWeapons[index];

        if (weapon == null)
        {
            return;
        }

        SelectedWeaponIndex = index;

        bool changed = weaponController.EquipWeapon(weapon);

        if (logActions)
        {
            Debug.Log(
                $"[TestEnvironmentController] Arma '{weapon.weaponName}' ({weapon.archetype}): " +
                $"{(changed ? "OK" : "SIN CAMBIOS")} | {weaponController.GetLoadoutSummary()}",
                this);
        }
    }

    /// <summary>Equipa (al nivel 1 si es nueva) cada arma del catálogo, en orden.</summary>
    public void EquipAllTestWeapons()
    {
        if (weaponController == null)
        {
            LogMissingWeaponController("equipar armas");
            return;
        }

        for (int i = 0; i < TestWeaponCount; i++)
        {
            if (testWeapons[i] != null)
            {
                weaponController.EquipWeapon(testWeapons[i]);
            }
        }

        if (logActions)
        {
            Debug.Log($"[TestEnvironmentController] Catálogo completo equipado: {weaponController.GetLoadoutSummary()}.", this);
        }
    }

    /// <summary>Retira todas las armas equipadas.</summary>
    public void ClearWeapons()
    {
        if (weaponController == null)
        {
            LogMissingWeaponController("retirar armas");
            return;
        }

        weaponController.ClearWeapons();

        if (logActions)
        {
            Debug.Log("[TestEnvironmentController] Armario vaciado: no quedan armas equipadas.", this);
        }
    }

    /// <summary>
    /// Alterna entre el sistema de armas nuevo y el pulso clásico de <see cref="PlayerAttack"/>.
    /// Es la comparación A/B más útil de la Fase 3: mismo escenario, misma horda, dos sistemas.
    /// </summary>
    public void ToggleWeaponSystem()
    {
        if (weaponController == null)
        {
            LogMissingWeaponController("cambiar de sistema de armas");
            return;
        }

        weaponController.ToggleWeaponSystem();

        if (logActions)
        {
            Debug.Log(
                $"[TestEnvironmentController] Sistema de armas: " +
                $"{(weaponController.UseNewWeaponSystem ? "NUEVO (WeaponController)" : "CLASICO (PlayerAttack)")}.",
                this);
        }
    }

    /// <summary>
    /// Equipa la primera arma del catálogo. Pensado para que la escena TEST tenga armas en
    /// cuanto arranca, sin depender de un clic.
    /// </summary>
    public void EquipFirstTestWeapon()
    {
        if (TestWeaponCount == 0 || testWeapons[0] == null || weaponController == null)
        {
            return;
        }

        weaponController.EquipWeapon(testWeapons[0]);
        SelectedWeaponIndex = 1 % TestWeaponCount;
    }

    private void LogMissingWeaponController(string what)
    {
        if (!logActions)
        {
            return;
        }

        Debug.LogWarning(
            $"[TestEnvironmentController] No hay WeaponController en el Player: no se puede {what}. " +
            "Vuelve a generar la escena con Tools/40 Days of Light/Construir escena TEST.",
            this);
    }
}
