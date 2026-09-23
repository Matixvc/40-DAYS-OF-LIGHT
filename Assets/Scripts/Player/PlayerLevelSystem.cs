using UnityEngine;
using System;

public class PlayerLevelSystem : MonoBehaviour
{
    [Header("Fuente de Datos")]
    [SerializeField] private CharacterDataSO playerData;

    [Header("Referencia al Panel Desactivado")]
    [SerializeField] private GameObject levelUpPanel; // Arrastra tu PanelLevelUp aquí en el Inspector

    [Header("Progreso en Tiempo de Ejecución (Si no usas ScriptableObject completo)")]
    [SerializeField] private int currentLevel = 1;
    [SerializeField] private float currentXP = 0f;
    [SerializeField] private float xpToNextLevel = 100f;

    // --- PROPIEDAD 'Data' QUE BUSCAN GameManager Y XPBarUI ---
    public CharacterDataSO Data => playerData;

    // --- EVENTOS QUE BUSCA XPBarUI ---
    public event Action<float, float> OnXPChanged; // (currentXP, maxXP)
    public event Action<int> OnLevelUp;           // (newLevel)

    public int CurrentLevel => currentLevel;
    public float CurrentXP => currentXP;
    public float XPToNextLevel => xpToNextLevel;

    private void Start()
    {
        if (playerData != null)
        {
            // Forzar el reseteo de los datos del SO al iniciar la partida
            playerData.currentLevel = 1;
            playerData.currentXP = 0f;
            
            currentXP = playerData.currentXP;
            xpToNextLevel = playerData.GetXPToNextLevel();
        }
        else
        {
            currentXP = 0f;
            xpToNextLevel = 100f;
        }

        // Refrescar la UI con el nivel 1 y XP 0
        OnXPChanged?.Invoke(currentXP, xpToNextLevel);
    }

    public void AddXP(float amount)
    {
        currentXP += amount;

        if (playerData != null)
        {
            playerData.currentXP = currentXP;
        }

        // Disparar evento para actualizar la barra de UI
        OnXPChanged?.Invoke(currentXP, xpToNextLevel);

        // Verificar si sube de nivel
        if (currentXP >= xpToNextLevel)
        {
            LevelUp();
        }
    }

   private void LevelUp()
    {
        currentXP -= xpToNextLevel;
        currentLevel++;
        xpToNextLevel *= 1.25f;

        if (playerData != null)
        {
            playerData.currentXP = currentXP;
            playerData.currentLevel = currentLevel;
        }

        OnLevelUp?.Invoke(currentLevel);
        OnXPChanged?.Invoke(currentXP, xpToNextLevel);

        // MOSTRAR PANEL
        if (levelUpPanel != null)
        {
            LevelUpUI uiScript = levelUpPanel.GetComponent<LevelUpUI>();
            if (uiScript != null)
            {
                uiScript.ShowPanel();
            }
        }
    }
}