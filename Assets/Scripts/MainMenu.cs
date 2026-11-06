using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>
/// Main menu: Start plays an intro video (fading in over the menu), then loads the next scene.
/// Quit closes the game.
/// The video screen is created automatically on your Canvas - you just need to assign the clip.
/// </summary>
public class MainMenu : MonoBehaviour
{
    [Header("Scenes")]
    [Tooltip("Scene to load after the video. Must be in the Build Profiles scene list.")]
    public string gameScene = "GameScene";
    [Tooltip("Load the game scene in the background while the video plays, so there's no wait afterwards.")]
    public bool preloadWhileVideoPlays = true;

    [Header("Intro Video")]
    [Tooltip("Drag your video clip here. Leave empty to skip the video.")]
    public VideoClip introVideo;
    public float videoFadeInDuration = 1.5f;
    [Tooltip("Click or press any key to skip the video.")]
    public bool allowSkip = true;

    [Header("Optional")]
    [Tooltip("The object holding your Start/Quit buttons. Its buttons are disabled once Start is pressed.")]
    public GameObject menuPanel;

    [Header("Music")]
    [Tooltip("The BackgroundMusic object in this scene. It fades out when the video starts. Leave empty to find it automatically.")]
    public BackgroundMusic menuMusic;

    [Header("Button Sounds")]
    public AudioClip startClickSound;
    public AudioClip quitClickSound;
    [Range(0f, 1f)] public float clickVolume = 1f;

    private bool isStarting;
    private bool isQuitting;
    private AsyncOperation preload;
    private AudioSource sfx;

    private void Awake()
    {
        // Audio source used for the button click sounds
        sfx = gameObject.AddComponent<AudioSource>();
        sfx.playOnAwake = false;
        sfx.spatialBlend = 0f;

        if (menuMusic == null)
            menuMusic = FindFirstObjectByType<BackgroundMusic>();
    }

    private void PlaySound(AudioClip clip)
    {
        if (clip != null)
            sfx.PlayOneShot(clip, clickVolume);
    }

    // ---- Hook these up to your buttons' On Click () ----

    public void StartGame()
    {
        if (isStarting || isQuitting) return;
        isStarting = true;

        PlaySound(startClickSound);

        // New game: stars back to 0 and all enemies come back
        PlayerProgress.ResetAll();
        EnemyChase.ResetCaughtEnemies();

        if (introVideo == null)
        {
            GoToGame();
            return;
        }

        StartCoroutine(PlayIntroThenLoad());
    }

    public void QuitGame()
    {
        if (isStarting || isQuitting) return;
        isQuitting = true;

        StartCoroutine(QuitAfterSound());
    }

    private IEnumerator QuitAfterSound()
    {
        // Let the click sound finish before the game closes (max 2 seconds)
        float wait = 0f;
        if (quitClickSound != null)
        {
            PlaySound(quitClickSound);
            wait = Mathf.Min(quitClickSound.length, 2f);
        }
        yield return new WaitForSecondsRealtime(wait);

        Debug.Log("Quit Game");

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ----------------------------------------------------

    private IEnumerator PlayIntroThenLoad()
    {
        // Stop any more button clicks
        if (menuPanel != null)
        {
            CanvasGroup menuGroup = menuPanel.GetComponent<CanvasGroup>();
            if (menuGroup == null) menuGroup = menuPanel.AddComponent<CanvasGroup>();
            menuGroup.interactable = false;
            menuGroup.blocksRaycasts = false;
        }

        // Start loading the game in the background (it won't switch until the video is done)
        if (preloadWhileVideoPlays)
        {
            preload = SceneManager.LoadSceneAsync(gameScene);
            preload.allowSceneActivation = false;
        }

        // Find a canvas to put the video on
        Canvas canvas = menuPanel != null ? menuPanel.GetComponentInParent<Canvas>() : null;
        if (canvas == null) canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("MainMenu: no Canvas found in the scene, skipping the video.");
            GoToGame();
            yield break;
        }
        canvas = canvas.rootCanvas;

        // ---- Build a black panel with the video inside it (starts invisible) ----
        GameObject panelObj = new GameObject("IntroVideoPanel", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        panelObj.transform.SetParent(canvas.transform, false);
        panelObj.transform.SetAsLastSibling(); // on top of the menu
        Stretch(panelObj.GetComponent<RectTransform>());

        panelObj.GetComponent<Image>().color = Color.black;
        CanvasGroup group = panelObj.GetComponent<CanvasGroup>();
        group.alpha = 0f;

        GameObject videoObj = new GameObject("IntroVideo", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
        videoObj.transform.SetParent(panelObj.transform, false);
        Stretch(videoObj.GetComponent<RectTransform>());

        AspectRatioFitter fitter = videoObj.GetComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent; // keeps the video's shape, black bars if needed
        fitter.aspectRatio = (float)introVideo.width / introVideo.height;

        // ---- Video player drawing into a texture ----
        RenderTexture rt = new RenderTexture((int)introVideo.width, (int)introVideo.height, 0);
        videoObj.GetComponent<RawImage>().texture = rt;

        VideoPlayer vp = gameObject.AddComponent<VideoPlayer>();
        vp.playOnAwake = false;
        vp.isLooping = false;
        vp.clip = introVideo;
        vp.renderMode = VideoRenderMode.RenderTexture;
        vp.targetTexture = rt;
        vp.audioOutputMode = VideoAudioOutputMode.Direct;

        bool finished = false;
        vp.loopPointReached += _ => finished = true;
        vp.errorReceived += (player, message) =>
        {
            Debug.LogError("MainMenu: video error: " + message);
            finished = true;
        };

        vp.Prepare();
        while (!vp.isPrepared && !finished)
            yield return null;

        if (!finished)
        {
            SetVolume(vp, 0f);
            vp.Play();

            // The video has started: fade the menu music out
            if (menuMusic != null)
                menuMusic.FadeOut();

            // ---- Play, fading in as it goes ----
            float elapsed = 0f;
            while (!finished)
            {
                if (allowSkip && elapsed > 0.3f && Input.anyKeyDown) break;

                elapsed += Time.unscaledDeltaTime;
                float a = videoFadeInDuration > 0f ? Mathf.Clamp01(elapsed / videoFadeInDuration) : 1f;
                group.alpha = a;
                SetVolume(vp, a);

                yield return null;
            }

            vp.Stop();
        }

        rt.Release();
        GoToGame();
    }

    private void GoToGame()
    {
        if (preload != null)
            preload.allowSceneActivation = true; // already loaded in the background, switch now
        else
            SceneManager.LoadScene(gameScene);
    }

    private void SetVolume(VideoPlayer vp, float volume)
    {
        if (vp.audioTrackCount > 0)
            vp.SetDirectAudioVolume(0, volume);
    }

    private void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}