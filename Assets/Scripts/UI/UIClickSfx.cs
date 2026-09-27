using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// SFX de clic para CUALQUIER botón de la interfaz al que se le añada este componente.
/// El clip lo elige el AudioManager al azar entre las variaciones del catálogo
/// (SFX_ClickUI_01/02), así que pulsar seguido no suena repetitivo.
/// La herramienta "40 Days of Light > Audio > 2 ..." lo añade automáticamente a todos los botones de
/// la escena abierta, de modo que ningún botón queda mudo.
/// El sonido va por el pool de SFX del AudioManager, que usa tiempo NO escalado: sigue sonando con el
/// juego pausado o congelado (menús de pausa y de fin de partida).
/// </summary>
[RequireComponent(typeof(Button))]
[DisallowMultipleComponent]
public class UIClickSfx : MonoBehaviour
{
    [Tooltip("Clip propio de este botón. Si se deja vacío se usa una variación aleatoria del catálogo.")]
    [SerializeField] private AudioClip clickSFX;

    [Range(0f, 1f)]
    [Tooltip("Volumen del clic (independiente del volumen general de la música).")]
    [SerializeField] private float volume = 0.6f;

    [Tooltip("No sonar si el botón no es interactivo (desactivado o bloqueado por código).")]
    [SerializeField] private bool ignoreNonInteractable = true;

    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();
    }

    private void OnEnable()
    {
        if (button == null) return;

        // RemoveListener antes de AddListener: el botón puede reactivarse varias veces (paneles que se
        // abren y se cierran) y sin esto el clic sonaría duplicado.
        button.onClick.RemoveListener(PlayClick);
        button.onClick.AddListener(PlayClick);
    }

    private void OnDisable()
    {
        if (button == null) return;

        button.onClick.RemoveListener(PlayClick);
    }

    /// <summary>Reproduce el clic. Es el listener registrado en el onClick del botón.</summary>
    public void PlayClick()
    {
        if (ignoreNonInteractable && button != null && !button.IsInteractable()) return;

        AudioManager audio = AudioManager.Instance;

        if (audio == null) return;

        audio.PlayUIClick(clickSFX, volume);
    }
}
