using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

public class PauseMenu : MonoBehaviour
{
    [Header("Referencias UI")]
    [SerializeField] private GameObject pauseMenuUI;
    [SerializeField] private Slider musicSlider;
    [SerializeField] private Slider sfxSlider;

    [Header("Audio Mixer")]
    [SerializeField] private AudioMixer mainMixer;

    [Header("Ajuste de Música en Pausa")]
    [Tooltip("Reducción de decibelios de la música en pausa (ej. -12dB la atenúa suavemente, -80dB la silencia por completo)")]
    [SerializeField] private float pauseMusicDeductionDb = -15f; 

    private bool isPaused = false;
    private float currentMusicDb = 0f;

    private void Start()
    {
        if (pauseMenuUI != null)
        {
            pauseMenuUI.SetActive(false);
        }

        if (musicSlider != null) musicSlider.onValueChanged.AddListener(SetMusicVolume);
        if (sfxSlider != null) sfxSlider.onValueChanged.AddListener(SetSFXVolume);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P))
        {
            if (isPaused)
            {
                ResumeGame();
            }
            else
            {
                PauseGame();
            }
        }
    }

    public void PauseGame()
    {
        isPaused = true;
        if (pauseMenuUI != null) pauseMenuUI.SetActive(true);

        // Obtener el volumen actual de la música antes de pausar
        if (mainMixer != null)
        {
            mainMixer.GetFloat("MusicVol", out currentMusicDb);
            // Atenuar la música en pausa
            mainMixer.SetFloat("MusicVol", currentMusicDb + pauseMusicDeductionDb);
        }

        Time.timeScale = 0f; // Congelar la lógica del juego
    }

    public void ResumeGame()
    {
        isPaused = false;
        if (pauseMenuUI != null) pauseMenuUI.SetActive(false);

        // Restaurar el volumen original de la música
        if (mainMixer != null)
        {
            mainMixer.SetFloat("MusicVol", currentMusicDb);
        }

        Time.timeScale = 1f; // Reanudar el tiempo
    }

    public void SetMusicVolume(float value)
    {
        float dbValue = Mathf.Log10(Mathf.Max(0.0001f, value)) * 20f;
        currentMusicDb = dbValue; // Actualizar base

        if (mainMixer != null && !isPaused)
        {
            mainMixer.SetFloat("MusicVol", dbValue);
        }
    }

    public void SetSFXVolume(float value)
    {
        if (mainMixer != null)
        {
            float dbValue = Mathf.Log10(Mathf.Max(0.0001f, value)) * 20f;
            mainMixer.SetFloat("SFXVol", dbValue);
        }
    }
}