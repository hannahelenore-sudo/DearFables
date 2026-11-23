using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Video;
using TMPro;
using System.Collections;

public class SceneLoader : MonoBehaviour
{
    [Header("Scene")]
    [Tooltip("Exact scene name. Must be in the Build Profiles scene list.")]
    public string sceneToLoad = "GameScene";

    [Header("Video")]
    [Tooltip("A VideoPlayer in this scene with your clip assigned. Leave empty to skip the video.")]
    public VideoPlayer videoPlayer;
    [Tooltip("Click or press any key to skip the video.")]
    public bool allowSkip = true;
    [Tooltip("Start loading the game scene in the background while the video plays.")]
    public bool preloadWhileVideoPlays = true;

    [Header("Loading Text")]
    public TMP_Text loadingText;
    [Tooltip("The loading screen stays up at least this long (after the video), so it doesn't just flash by.")]
    public float minimumLoadingScreenTime = 1.5f;

    private string[] messages =
    {
        "Loading...",
        "Loading..",
        "Loading."
    };

    private int index = 0;
    private AsyncOperation operation;

    void Start()
    {
        if (loadingText != null)
            loadingText.gameObject.SetActive(false);

        StartCoroutine(RunLoader());
    }

    IEnumerator RunLoader()
    {
        yield return null;

        if (preloadWhileVideoPlays)
            StartLoading();

        if (videoPlayer != null)
            yield return StartCoroutine(PlayVideo());

        if (loadingText != null)
        {
            loadingText.gameObject.SetActive(true);
            StartCoroutine(AnimateText());
        }

        if (operation == null)
            StartLoading();

        float timer = 0f;
        while (operation.progress < 0.9f || timer < minimumLoadingScreenTime)
        {
            timer += Time.deltaTime;
            yield return null;
        }

        operation.allowSceneActivation = true;
    }

    void StartLoading()
    {
        operation = SceneManager.LoadSceneAsync(sceneToLoad);
        operation.allowSceneActivation = false;
    }

    IEnumerator PlayVideo()
    {
        Camera cam = Camera.main;
        if (cam == null) cam = FindFirstObjectByType<Camera>();
        if (cam == null)
        {
            Debug.LogWarning("SceneLoader: no Camera in the loading scene, skipping the video.");
            yield break;
        }

        videoPlayer.playOnAwake = false;
        videoPlayer.isLooping = false;
        videoPlayer.renderMode = VideoRenderMode.CameraNearPlane;
        videoPlayer.targetCamera = cam;

        bool finished = false;
        videoPlayer.loopPointReached += _ => finished = true;
        videoPlayer.errorReceived += (vp, message) =>
        {
            Debug.LogError("SceneLoader: video error: " + message);
            finished = true;
        };

        videoPlayer.Prepare();
        while (!videoPlayer.isPrepared && !finished)
            yield return null;

        if (finished) yield break;

        videoPlayer.Play();

        while (!finished)
        {
            if (allowSkip && Input.anyKeyDown) break;
            yield return null;
        }

        videoPlayer.Stop();
    }

    IEnumerator AnimateText()
    {
        while (true)
        {
            loadingText.text = messages[index];
            index = (index + 1) % messages.Length;

            yield return new WaitForSeconds(0.5f);
        }
    }
}