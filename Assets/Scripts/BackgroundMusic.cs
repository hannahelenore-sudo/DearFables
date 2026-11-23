using UnityEngine;
using UnityEngine.SceneManagement;

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
        homeSceneHandle = gameObject.scene.handle;

        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);

        SceneManager.sceneUnloaded += OnSceneUnloaded;
    }

    void OnDestroy()
    {
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
    }

    public void FadeOut()
    {
        leavingScene = true;
    }

    private void OnSceneUnloaded(Scene scene)
    {
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

        if (crossfadeDuration <= 0f)
            active.loop = true;

        active.volume = 0f;
        active.Play();
    }

    void Update()
    {
        fadeInTimer += Time.unscaledDeltaTime;
        float fadeIn = startFadeIn > 0f ? Mathf.Clamp01(fadeInTimer / startFadeIn) : 1f;

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

        if (crossfadeDuration <= 0f)
        {
            active.volume = master;
            return;
        }

        float overlap = Mathf.Min(crossfadeDuration, music.length * 0.4f);

        if (!crossfading)
        {
            active.volume = master;

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

            active.volume = master * Mathf.Cos(t * Mathf.PI * 0.5f);
            other.volume = master * Mathf.Sin(t * Mathf.PI * 0.5f);

            if (t >= 1f)
            {
                active.Stop();

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
        source.spatialBlend = 0f;
        return source;
    }
}