using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Background music for ONE scene. Put it on an empty GameObject in each scene that needs music.
/// - Fades in when the scene starts.
/// - Loops smoothly by crossfading the end of the track into the start.
/// - Fades out (instead of cutting off) when the player leaves the scene, then removes itself.
/// </summary>
public class BackgroundMusic : MonoBehaviour
{
    [Header("Music")]
    public AudioClip music;
    [Range(0f, 1f)] public float volume = 0.6f;
    [Tooltip("Seconds to fade the music in at the start.")]
    public float startFadeIn = 2f;
    [Tooltip("Seconds to fade the music out when the scene is left.")]
    public float fadeOutDuration = 1.5f;

    [Header("Looping")]
    [Tooltip("Seconds the end of the track overlaps with the start of the next loop. 0 = plain loop (use this if your track already loops perfectly).")]
    public float crossfadeDuration = 2f;

    private AudioSource active;
    private AudioSource other;
    private bool crossfading;
    private float fadeTimer;
    private float fadeInTimer;

    private int homeSceneHandle;
    private bool leavingScene;
    private float fadeOutTimer;

    void Awake()
    {
        // Remember which scene we belong to, then survive the scene change just long enough to fade out
        homeSceneHandle = gameObject.scene.handle;

        transform.SetParent(null); // DontDestroyOnLoad only works on top-level objects
        DontDestroyOnLoad(gameObject);

        SceneManager.sceneUnloaded += OnSceneUnloaded;
    }

    void OnDestroy()
    {
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
    }

    /// <summary>
    /// Fades the music out now and removes this object. Call it from other scripts,
    /// for example when a video starts playing.
    /// </summary>
    public void FadeOut()
    {
        leavingScene = true;
    }

    private void OnSceneUnloaded(Scene scene)
    {
        // Our scene is gone -> start fading out
        if (scene.handle == homeSceneHandle)
            leavingScene = true;
    }

    void Start()
    {
        if (music == null)
        {
            Debug.LogWarning("BackgroundMusic: no music clip assigned.", this);
            enabled = false;
            return;
        }

        active = CreateSource();
        other = CreateSource();

        // Plain loop mode: a single source that loops by itself
        if (crossfadeDuration <= 0f)
            active.loop = true;

        active.volume = 0f;
        active.Play();
    }

    void Update()
    {
        // Fade-in multiplier (0 -> 1)
        fadeInTimer += Time.unscaledDeltaTime;
        float fadeIn = startFadeIn > 0f ? Mathf.Clamp01(fadeInTimer / startFadeIn) : 1f;

        // Fade-out multiplier (1 -> 0) once the scene has been left
        float fadeOut = 1f;
        if (leavingScene)
        {
            fadeOutTimer += Time.unscaledDeltaTime;
            fadeOut = fadeOutDuration > 0f ? 1f - Mathf.Clamp01(fadeOutTimer / fadeOutDuration) : 0f;

            if (fadeOut <= 0f)
            {
                Destroy(gameObject);
                return;
            }
        }

        float master = volume * fadeIn * fadeOut;

        // Plain loop
        if (crossfadeDuration <= 0f)
        {
            active.volume = master;
            return;
        }

        // Never overlap for more than ~40% of the track
        float overlap = Mathf.Min(crossfadeDuration, music.length * 0.4f);

        if (!crossfading)
        {
            active.volume = master;

            // Close to the end of the track: start the next loop underneath
            if (active.isPlaying && music.length - active.time <= overlap)
            {
                other.time = 0f;
                other.volume = 0f;
                other.Play();

                crossfading = true;
                fadeTimer = 0f;
            }
        }
        else
        {
            fadeTimer += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(fadeTimer / overlap);

            // Equal-power crossfade keeps the loudness steady (no dip in the middle)
            active.volume = master * Mathf.Cos(t * Mathf.PI * 0.5f);
            other.volume = master * Mathf.Sin(t * Mathf.PI * 0.5f);

            if (t >= 1f)
            {
                active.Stop();

                // Swap roles
                AudioSource temp = active;
                active = other;
                other = temp;

                crossfading = false;
            }
        }
    }

    private AudioSource CreateSource()
    {
        AudioSource source = gameObject.AddComponent<AudioSource>();
        source.clip = music;
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f; // 2D sound, same volume everywhere
        return source;
    }
}