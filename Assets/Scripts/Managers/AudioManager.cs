using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Audio Mixer Groups")]
    [SerializeField] private AudioMixerGroup musicGroup;
    [SerializeField] private AudioMixerGroup sfxGroup;

    [Header("Configuración de Lista de Música")]
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioClip[] playlist; // Arrastra aquí tus 2 (o más) canciones
    private int currentTrackIndex = 0;
    private Coroutine musicLoopCoroutine;

    [Header("Configuración del Pool de SFX")]
    [SerializeField] private int poolSize = 12;
    private List<AudioSource> sfxPool;

    [Header("Anti-Saturación")]
    [SerializeField] private float minSoundInterval = 0.04f;
    private Dictionary<AudioClip, float> lastPlayTimes = new Dictionary<AudioClip, float>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializePool();
            InitializeMusic();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        // Iniciar la reproducción en cadena de la playlist
        if (playlist != null && playlist.Length > 0)
        {
            StartPlaylist();
        }
    }

    private void InitializeMusic()
    {
        if (musicSource == null)
        {
            GameObject musicObj = new GameObject("Music_Source");
            musicObj.transform.SetParent(transform);
            musicSource = musicObj.AddComponent<AudioSource>();
        }

        musicSource.playOnAwake = false;
        musicSource.loop = false; // Desactivado para detectar cuando termina cada canción
        musicSource.outputAudioMixerGroup = musicGroup;
    }

    private void InitializePool()
    {
        sfxPool = new List<AudioSource>();

        for (int i = 0; i < poolSize; i++)
        {
            GameObject sfxObj = new GameObject($"SFX_Source_{i}");
            sfxObj.transform.SetParent(transform);
            AudioSource source = sfxObj.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.outputAudioMixerGroup = sfxGroup;
            sfxPool.Add(source);
        }
    }

    // =========================================================================
    // LÓGICA DE PLAYLIST EN BUCLE
    // =========================================================================
    public void StartPlaylist()
    {
        if (musicLoopCoroutine != null)
        {
            StopCoroutine(musicLoopCoroutine);
        }
        musicLoopCoroutine = StartCoroutine(PlayMusicPlaylistRoutine());
    }

    private IEnumerator PlayMusicPlaylistRoutine()
    {
        while (playlist != null && playlist.Length > 0)
        {
            AudioClip currentClip = playlist[currentTrackIndex];

            if (currentClip != null)
            {
                musicSource.clip = currentClip;
                musicSource.volume = 0.5f; // Ajusta el volumen general de la música aquí
                musicSource.Play();

                // Esperar exactamente la duración de la canción actual
                yield return new WaitForSeconds(currentClip.length);
            }
            else
            {
                yield return null;
            }

            // Avanzar al siguiente índice (al llegar al final vuelve a 0 automáticamente)
            currentTrackIndex = (currentTrackIndex + 1) % playlist.Length;
        }
    }

    // =========================================================================
    // REPRODUCCIÓN DE EFECTOS (SFX)
    // =========================================================================
    private bool CanPlaySound(AudioClip clip)
    {
        if (clip == null) return false;

        if (lastPlayTimes.TryGetValue(clip, out float lastTime))
        {
            if (Time.unscaledTime - lastTime < minSoundInterval)
            {
                return false;
            }
        }

        lastPlayTimes[clip] = Time.unscaledTime;
        return true;
    }

    public void PlaySFX(AudioClip clip, float volume = 1f, float pitchRandomness = 0.05f)
    {
        if (!CanPlaySound(clip)) return;

        AudioSource source = GetAvailableSource();
        source.clip = clip;
        source.volume = volume;
        source.pitch = 1f + Random.Range(-pitchRandomness, pitchRandomness);
        source.spatialBlend = 0f;
        source.Play();
    }

    public void PlaySFXAtPosition(AudioClip clip, Vector3 position, float volume = 1f)
    {
        if (!CanPlaySound(clip)) return;

        AudioSource source = GetAvailableSource();
        source.transform.position = position;
        source.clip = clip;
        source.volume = Mathf.Clamp01(volume);
        source.pitch = Random.Range(0.92f, 1.08f);
        source.spatialBlend = 1f;
        source.minDistance = 3f;
        source.maxDistance = 15f;
        source.rolloffMode = AudioRolloffMode.Logarithmic;
        source.Play();
    }

    private AudioSource GetAvailableSource()
    {
        foreach (var source in sfxPool)
        {
            if (!source.isPlaying) return source;
        }
        return sfxPool[0];
    }
}