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
    [Tooltip("Intervalo mínimo (s) entre dos reproducciones del MISMO clip. Evita el solape global.")]
    [SerializeField] private float minSoundInterval = 0.04f;
    [Tooltip("Voces simultáneas máximas del MISMO clip (0 = sin límite). Evita el muro de sonido cuando " +
             "varios enemigos reciben daño en el mismo frame (p. ej. el Destello de Luz en área).")]
    [SerializeField] private int maxSimultaneousPerClip = 3;
    [Tooltip("Variación de tono (±) por defecto de los SFX posicionales (3D).")]
    [Range(0f, 0.2f)]
    [SerializeField] private float defaultPositionalPitchVariation = 0.08f;
    private Dictionary<AudioClip, float> lastPlayTimes = new Dictionary<AudioClip, float>();

    [Header("Espacialización 3D (SFX posicionales)")]
    [Tooltip("Distancia (m) dentro de la cual un SFX 3D suena a volumen completo. En tercera persona " +
             "el listener está en la cámara (varios metros por detrás/encima del Pastor): con un valor " +
             "demasiado bajo, el zarpazo de un enemigo pegado al jugador sonaría atenuado.")]
    [SerializeField] private float sfxMinDistance = 4f;
    [Tooltip("Distancia (m) a partir de la cual un SFX 3D deja de oírse.")]
    [SerializeField] private float sfxMaxDistance = 30f;

    [Header("Golpe del Jugador (2D)")]
    [Tooltip("Impacto de respaldo para cuando un HealthComponent no tiene 'Damage Sfx' asignado y el " +
             "receptor es el Jugador. Recomendado: ImapactEnemy.mp3. Si se deja vacío se genera un " +
             "golpe sintetizado en runtime para que el jugador SIEMPRE oiga el golpe.")]
    [SerializeField] private AudioClip fallbackImpactSFX;
    private AudioClip proceduralImpactClip;
    private bool warnedAboutMissingImpactClip;

    [Header("Catálogo de Clic de UI (variaciones)")]
    [Tooltip("Clips del clic de interfaz (SFX_ClickUI_01/02). En cada clic se elige uno al azar.")]
    [SerializeField] private AudioClip[] uiClickSFX;
    [Range(0f, 1f)]
    [SerializeField] private float uiClickVolume = 0.6f;
    [Range(0f, 0.2f)]
    [Tooltip("Variación de tono (±) del clic: pulsar muchas veces seguidas no se vuelve monótono.")]
    [SerializeField] private float uiClickPitchVariation = 0.04f;

    [Header("Catálogo de Daño al Jugador (variaciones)")]
    [Tooltip("Variaciones del golpe recibido por el JUGADOR (SFX_DamagePlayer_01/02). Es el respaldo del " +
             "HealthComponent cuando su propio array está vacío.")]
    [SerializeField] private AudioClip[] playerDamageSFX;

    [Header("Catálogo de SFX de Partida")]
    [Tooltip("Muerte del jugador (SFX_PlayerDeath). Se reproduce en 2D sin filtros.")]
    [SerializeField] private AudioClip playerDeathSFX;
    [Tooltip("Gema de XP recogida (SFX_XPOp).")]
    [SerializeField] private AudioClip xpPickupSFX;
    [Tooltip("Aviso de inicio de noche (SFX_NightStart).")]
    [SerializeField] private AudioClip nightStartSFX;
    [Tooltip("Paso sobre arena al caminar (SFX_StepSand).")]
    [SerializeField] private AudioClip stepSandSFX;
    [Tooltip("Paso sobre arena al correr (SFX_SprintSand).")]
    [SerializeField] private AudioClip sprintSandSFX;

    [Header("Volúmenes del Catálogo")]
    [Range(0f, 1f)]
    [SerializeField] private float playerDeathVolume = 0.9f;
    [Range(0f, 1f)]
    [SerializeField] private float xpPickupVolume = 0.7f;
    [Range(0f, 1f)]
    [SerializeField] private float nightStartVolume = 0.85f;
    [Range(0f, 1f)]
    [SerializeField] private float footstepVolume = 0.5f;

    [Header("Música de Fin de Partida")]
    [Tooltip("Música de derrota (MUS_GameOver): sustituye a la playlist con un fundido cruzado.")]
    [SerializeField] private AudioClip gameOverMusic;
    [Tooltip("Música de victoria (MUS_Victory): sustituye a la playlist con un fundido cruzado.")]
    [SerializeField] private AudioClip victoryMusic;
    [Range(0f, 1f)]
    [Tooltip("Volumen de la música de fin de partida (independiente del de la playlist).")]
    [SerializeField] private float runEndMusicVolume = 0.3f;
    [Tooltip("Duración del fundido de salida de la música actual, en segundos.")]
    [SerializeField, Min(0f)] private float musicFadeOutDuration = 1.2f;
    [Tooltip("Duración del fundido de entrada de la música de fin de partida, en segundos.")]
    [SerializeField, Min(0f)] private float musicFadeInDuration = 1.5f;
    private Coroutine musicFadeCoroutine;

    [SerializeField] private float defaultMusicVolume = 0.25f;
    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // DontDestroyOnLoad solo funciona con GameObjects RAÍZ. Si el AudioManager está anidado
        // (por ejemplo dentro de "Managers"), Unity emite el aviso "DontDestroyOnLoad only works
        // for root GameObjects". Para poder persistir sin ese aviso, lo movemos a la raíz.
        if (transform.parent != null)
        {
            Debug.Log(
                "[AudioManager] Estaba anidado en la jerarquía: se mueve a la raíz para poder persistir entre escenas. " +
                "Recomendado: sácalo a la raíz en la escena para que no haga falta en runtime.",
                this);

            transform.SetParent(null, true);
        }

        DontDestroyOnLoad(gameObject);
        InitializePool();
        InitializeMusic();
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

        // Mathf.Max evita un pool vacío (y el consiguiente KeyNotFound al pedir una fuente)
        // si alguien deja poolSize a 0 en el Inspector.
        int safePoolSize = Mathf.Max(1, poolSize);

        for (int i = 0; i < safePoolSize; i++)
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
                musicSource.volume = defaultMusicVolume; // Ajusta el volumen general de la música aquí
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

    /// <summary>
    /// Comprueba el intervalo mínimo entre dos reproducciones del mismo clip.
    /// Actualiza la marca de tiempo al pasar (por eso se llama al final, cuando ya se va a sonar).
    /// </summary>
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

    /// <summary>
    /// Portero común de los SFX: limita las voces simultáneas del MISMO clip y respeta el
    /// intervalo mínimo. Se evalúa ANTES de tocar la marca de tiempo, así descartar un sonido
    /// saturado no bloquea al siguiente.
    /// </summary>
    private bool CanPlayClip(AudioClip clip)
    {
        if (clip == null) return false;

        // Un golpe en área (Destello de Luz) puede pedir el mismo SFX en el mismo frame para
        // varios enemigos: sin este tope la mezcla se satura y el clip se pisa a sí mismo.
        if (maxSimultaneousPerClip > 0 && CountPlaying(clip) >= maxSimultaneousPerClip)
        {
            return false;
        }

        return CanPlaySound(clip);
    }

    /// <summary>Voces activas del clip indicado (asignaciones cero: bucle sobre la lista interna).</summary>
    private int CountPlaying(AudioClip clip)
    {
        if (sfxPool == null) return 0;

        int count = 0;

        for (int i = 0; i < sfxPool.Count; i++)
        {
            AudioSource source = sfxPool[i];

            if (source.isPlaying && source.clip == clip)
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>
    /// SFX 2D (interfaz, feedback del jugador).
    /// <paramref name="pitchRandomness"/> es la variación de tono (±) que evita la fatiga
    /// auditiva al repetir el mismo sonido muchas veces seguidas.
    /// </summary>
    public void PlaySFX(AudioClip clip, float volume = 1f, float pitchRandomness = 0.05f)
    {
        if (!CanPlayClip(clip)) return;

        AudioSource source = GetAvailableSource();

        if (source == null) return;

        source.clip = clip;
        source.volume = volume;
        source.pitch = 1f + Random.Range(-pitchRandomness, pitchRandomness);
        source.spatialBlend = 0f;
        source.Play();
    }

    /// <summary>
    /// SFX 3D en una posición del mundo.
    /// <paramref name="pitchVariation"/> es la variación de tono (±, entre 0.05 y 0.1 para el
    /// combate); si se omite o se pasa un valor negativo se usa
    /// <see cref="defaultPositionalPitchVariation"/> del Inspector.
    /// </summary>
    public void PlaySFXAtPosition(AudioClip clip, Vector3 position, float volume = 1f, float pitchVariation = -1f)
    {
        if (!CanPlayClip(clip)) return;

        AudioSource source = GetAvailableSource();

        if (source == null) return;

        // Sentinel: cualquier valor negativo significa "usa el valor por defecto del manager",
        // así se puede ajustar el tono de todos los SFX 3D sin tocar código.
        float variation = pitchVariation >= 0f ? pitchVariation : defaultPositionalPitchVariation;

        source.transform.position = position;
        source.clip = clip;
        source.volume = Mathf.Clamp01(volume);
        source.pitch = 1f + Random.Range(-variation, variation);
        source.spatialBlend = 1f;
        source.minDistance = Mathf.Max(0.1f, sfxMinDistance);
        source.maxDistance = Mathf.Max(source.minDistance + 0.1f, sfxMaxDistance);
        source.rolloffMode = AudioRolloffMode.Logarithmic;
        source.Play();
    }

    /// <summary>
    /// SFX 2D de máxima prioridad: sin atenuación por distancia (se oye mire donde mire la cámara) y
    /// sin los filtros anti-saturación, porque es feedback crítico: no debe descartarse ni por el
    /// intervalo mínimo ni por el límite de voces del clip. Lo usan el golpe recibido por el JUGADOR
    /// y el SFX de su muerte.
    /// </summary>
    public void PlayPrioritySFX2D(AudioClip clip, float volume = 1f, float pitchVariation = 0.05f)
    {
        if (clip == null) return;

        AudioSource source = GetAvailableSource();

        if (source == null) return;

        float variation = Mathf.Abs(pitchVariation);

        source.transform.position = transform.position; // Irrelevante en 2D, pero evita dejar la fuente "lejos"
        source.clip = clip;
        source.volume = Mathf.Clamp01(volume);
        source.pitch = 1f + Random.Range(-variation, variation);
        source.spatialBlend = 0f;
        source.Play();
    }

    /// <summary>Golpe recibido por el JUGADOR (2D prioritario). Se mantiene por compatibilidad.</summary>
    public void PlayHitOnPlayer(AudioClip clip, float volume = 1f, float pitchVariation = 0.05f)
    {
        PlayPrioritySFX2D(clip, volume, pitchVariation);
    }

    // =========================================================================
    // CATÁLOGO DE SFX: SELECCIÓN Y REPRODUCCIÓN
    // =========================================================================

    /// <summary>
    /// Clip aleatorio de un array. Empieza en un índice al azar y recorre el array desde ahí, de modo
    /// que los huecos vacíos del Inspector no rompan la variedad ni devuelvan null sin motivo.
    /// </summary>
    private static AudioClip PickRandomClip(AudioClip[] clips)
    {
        if (clips == null || clips.Length == 0) return null;

        int startIndex = Random.Range(0, clips.Length);

        for (int i = 0; i < clips.Length; i++)
        {
            AudioClip candidate = clips[(startIndex + i) % clips.Length];

            if (candidate != null) return candidate;
        }

        return null;
    }

    /// <summary>Variación aleatoria del golpe recibido por el Jugador (SFX_DamagePlayer_01/02).</summary>
    public AudioClip GetPlayerDamageClip() => PickRandomClip(playerDamageSFX);

    /// <summary>Variación aleatoria del clic de interfaz (SFX_ClickUI_01/02).</summary>
    public AudioClip GetUIClickClip() => PickRandomClip(uiClickSFX);

    public AudioClip GetXpPickupClip() => xpPickupSFX;
    public AudioClip GetNightStartClip() => nightStartSFX;
    public AudioClip GetPlayerDeathClip() => playerDeathSFX;
    public AudioClip GetGameOverMusic() => gameOverMusic;
    public AudioClip GetVictoryMusic() => victoryMusic;

    /// <summary>Paso de arena: el de correr si se está corriendo y existe; si no, el de caminar.</summary>
    public AudioClip GetStepSandClip(bool sprinting)
    {
        return sprinting && sprintSandSFX != null ? sprintSandSFX : stepSandSFX;
    }

    /// <summary>
    /// Clic de interfaz (2D): el clip propio del botón o, si no hay, una variación aleatoria del
    /// catálogo. El portero anti-saturación del manager es por clip, así que alternar las dos
    /// variaciones permite pulsar botones rápido sin quedarse mudo.
    /// </summary>
    public void PlayUIClick(AudioClip overrideClip = null, float volume = -1f)
    {
        AudioClip clip = overrideClip != null ? overrideClip : GetUIClickClip();

        if (clip == null) return;

        PlaySFX(clip, volume >= 0f ? volume : uiClickVolume, uiClickPitchVariation);
    }

    /// <summary>Gema de XP recogida (SFX_XPOp). 2D: es feedback directo del jugador.</summary>
    public void PlayXpPickupSFX(AudioClip overrideClip = null)
    {
        AudioClip clip = overrideClip != null ? overrideClip : GetXpPickupClip();

        if (clip == null) return;

        PlaySFX(clip, xpPickupVolume, 0.12f);
    }

    /// <summary>Aviso de inicio de noche (SFX_NightStart). 2D: es un aviso global, no posicional.</summary>
    public void PlayNightStartSFX()
    {
        AudioClip clip = GetNightStartClip();

        if (clip == null) return;

        PlaySFX(clip, nightStartVolume, 0.02f);
    }

    /// <summary>
    /// Muerte del jugador (SFX_PlayerDeath): 2D y sin puertas anti-saturación, porque es el golpe
    /// final de la partida y debe oírse siempre, incluso con la música de derrota entrando.
    /// </summary>
    public void PlayPlayerDeathSFX()
    {
        AudioClip clip = GetPlayerDeathClip();

        if (clip == null) return;

        PlayPrioritySFX2D(clip, playerDeathVolume, 0.02f);
    }

    /// <summary>
    /// Paso sobre arena, posicional en el mundo (se oye más cerca al pasar al lado de la cámara).
    /// El clip propio del Player tiene prioridad sobre el del catálogo.
    /// </summary>
    public void PlayFootstepSFX(bool sprinting, Vector3 position, AudioClip overrideClip = null, float volume = -1f, float pitchVariation = 0.12f)
    {
        AudioClip clip = overrideClip != null ? overrideClip : GetStepSandClip(sprinting);

        if (clip == null) return;

        PlaySFXAtPosition(clip, position, volume >= 0f ? volume : footstepVolume, pitchVariation);
    }

    /// <summary>
    /// Clip de impacto por defecto para golpes sin clip propio (p. ej. el del Jugador): el asignado
    /// en el Inspector o, si no hay ninguno, uno sintetizado (se crea una sola vez). Garantiza que el
    /// golpe se oiga aunque nadie haya asignado assets.
    /// </summary>
    public AudioClip GetFallbackImpactClip()
    {
        if (fallbackImpactSFX != null) return fallbackImpactSFX;

        if (proceduralImpactClip == null)
        {
            proceduralImpactClip = CreateProceduralImpactClip();

            if (!warnedAboutMissingImpactClip)
            {
                warnedAboutMissingImpactClip = true;

                Debug.LogWarning(
                    "[AudioManager] No hay ningún clip de impacto asignado ('Fallback Impact SFX' o el " +
                    "'Damage Sfx' del HealthComponent): se usará un golpe sintetizado. Asigna " +
                    "'Assets/Audio/SFX/ImapactEnemy.mp3' para un sonido real.",
                    this);
            }
        }

        return proceduralImpactClip;
    }

    /// <summary>
    /// Golpe corto sintetizado (cuerpo grave 170->70 Hz + chasquido percusivo) usado SOLO como último
    /// recurso. Se genera una única vez: después no asigna memoria ni crea clips nuevos.
    /// </summary>
    private static AudioClip CreateProceduralImpactClip()
    {
        const int sampleRate = 44100;
        const float duration = 0.12f;
        const float decay = 22f;

        int sampleCount = Mathf.Max(1, Mathf.RoundToInt(sampleRate * duration));
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float seconds = i / (float)sampleRate;
            float progress = i / (float)sampleCount;
            float envelope = Mathf.Exp(-decay * seconds);
            float body = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(170f, 70f, progress) * seconds);
            float click = (Random.value * 2f - 1f) * 0.4f;

            samples[i] = Mathf.Clamp((body * 0.9f + click) * envelope, -1f, 1f);
        }

        AudioClip clip = AudioClip.Create("SFX_ImpactoGenerico", sampleCount, 1, sampleRate, false);

        if (!clip.SetData(samples, 0))
        {
            Debug.LogWarning("[AudioManager] No se pudo rellenar el clip de impacto sintetizado.");
        }

        return clip;
    }

    // =========================================================================
    // MÚSICA DE FIN DE PARTIDA (FUNDIDOS)
    // =========================================================================

    /// <summary>
    /// Detiene la playlist en curso: el fundido de fin de partida pasa a controlar el AudioSource de
    /// música (si la corrutina siguiera viva, pondría la canción siguiente encima del tema de fin).
    /// </summary>
    private void StopPlaylist()
    {
        if (musicLoopCoroutine == null) return;

        StopCoroutine(musicLoopCoroutine);
        musicLoopCoroutine = null;
    }

    /// <summary>Música de derrota (MUS_GameOver) con fundido de salida y de entrada.</summary>
    public void PlayGameOverMusic() => FadeMusicTo(GetGameOverMusic());

    /// <summary>Música de victoria (MUS_Victory) con fundido de salida y de entrada.</summary>
    public void PlayVictoryMusic() => FadeMusicTo(GetVictoryMusic());

    /// <summary>
    /// Funde la música actual hacia <paramref name="clip"/>; si el clip es null se comporta como
    /// <see cref="StopMusicWithFade"/>. Usa tiempo NO escalado: la secuencia de fin de partida
    /// termina congelando el juego y el fundido debe seguir avanzando igual.
    /// </summary>
    public void FadeMusicTo(AudioClip clip)
    {
        if (clip == null)
        {
            StopMusicWithFade();
            return;
        }

        if (musicSource == null)
        {
            Debug.LogWarning(
                "[AudioManager] No hay AudioSource de música: no se puede reproducir la música de fin de partida.",
                this);
            return;
        }

        StopPlaylist();
        StopMusicFade();

        musicFadeCoroutine = StartCoroutine(FadeMusicRoutine(
            clip,
            Mathf.Max(0f, musicFadeOutDuration),
            Mathf.Max(0f, musicFadeInDuration),
            Mathf.Clamp01(runEndMusicVolume)));
    }

    /// <summary>Para la música fundiendo a silencio (sin volver a arrancar la playlist).</summary>
    public void StopMusicWithFade(float fadeOutDuration = -1f)
    {
        if (musicSource == null) return;

        StopPlaylist();
        StopMusicFade();

        musicFadeCoroutine = StartCoroutine(FadeMusicRoutine(
            null,
            fadeOutDuration >= 0f ? fadeOutDuration : Mathf.Max(0f, musicFadeOutDuration),
            0f,
            0f));
    }

    /// <summary>
    /// Devuelve la música de partida arrancando la playlist desde la primera pista. Se llama al
    /// reiniciar la partida o al volver al menú: el AudioManager persiste entre escenas y, si no,
    /// seguiría sonando la música de derrota en la escena siguiente.
    /// </summary>
    public void RestorePlaylistMusic()
    {
        if (musicSource == null) return;

        StopPlaylist();
        StopMusicFade();

        musicSource.Stop();
        musicSource.clip = null;
        musicSource.loop = false;
        musicSource.volume = defaultMusicVolume;

        if (playlist != null && playlist.Length > 0)
        {
            StartPlaylist();
        }
    }

    private void StopMusicFade()
    {
        if (musicFadeCoroutine == null) return;

        StopCoroutine(musicFadeCoroutine);
        musicFadeCoroutine = null;
    }

    /// <summary>
    /// Fundido de la música actual a silencio y, si <paramref name="clip"/> no es null, entrada del
    /// tema nuevo. Todo el avance se mide con Time.unscaledDeltaTime.
    /// </summary>
    private IEnumerator FadeMusicRoutine(AudioClip clip, float fadeOut, float fadeIn, float targetVolume)
    {
        // 1. Fundido de salida de la música actual (la playlist o el tema anterior).
        if (musicSource.isPlaying && fadeOut > 0f)
        {
            float startVolume = musicSource.volume;
            float elapsed = 0f;

            while (elapsed < fadeOut)
            {
                elapsed += Time.unscaledDeltaTime;
                musicSource.volume = Mathf.Lerp(startVolume, 0f, Mathf.Clamp01(elapsed / fadeOut));
                yield return null;
            }
        }

        musicSource.Stop();
        musicSource.volume = 0f;

        // 2. Fundido de entrada del tema nuevo (null = solo se quería parar la música).
        if (clip != null)
        {
            musicSource.clip = clip;
            musicSource.loop = true;
            musicSource.Play();

            if (fadeIn > 0f)
            {
                float elapsed = 0f;

                while (elapsed < fadeIn)
                {
                    elapsed += Time.unscaledDeltaTime;
                    musicSource.volume = Mathf.Lerp(0f, targetVolume, Mathf.Clamp01(elapsed / fadeIn));
                    yield return null;
                }
            }

            musicSource.volume = targetVolume;
        }

        musicFadeCoroutine = null;
    }

    /// <summary>
    /// Devuelve una fuente libre. Si todas están sonando reutiliza la que está MÁS CERCA DE
    /// TERMINAR: antes se devolvía siempre la primera del pool, lo que cortaba el sonido en seco
    /// y producía el artefacto de "clic" cuando había más de <see cref="poolSize"/> SFX a la vez.
    /// </summary>
    private AudioSource GetAvailableSource()
    {
        if (sfxPool == null || sfxPool.Count == 0) return null;

        AudioSource reusable = sfxPool[0];
        float shortestRemaining = float.MaxValue;

        for (int i = 0; i < sfxPool.Count; i++)
        {
            AudioSource source = sfxPool[i];

            if (!source.isPlaying) return source;

            float remaining = source.clip != null ? Mathf.Max(0f, source.clip.length - source.time) : 0f;

            if (remaining < shortestRemaining)
            {
                shortestRemaining = remaining;
                reusable = source;
            }
        }

        return reusable;
    }
}