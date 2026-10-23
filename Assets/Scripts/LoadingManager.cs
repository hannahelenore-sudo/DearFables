using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections;

public class SceneLoader : MonoBehaviour
{
    public TMP_Text loadingText;

    private string[] messages =
    {
        "Loading...",
        "Loading..",
        "Loading."
    };

    private int index = 0;

    void Start()
    {
        StartCoroutine(LoadGame());
        StartCoroutine(AnimateText());
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

    IEnumerator LoadGame()
    {
        yield return null;

        AsyncOperation operation = SceneManager.LoadSceneAsync("GameScene");

        operation.allowSceneActivation = false;

        while (operation.progress < 0.9f)
        {
            yield return null;
        }

        yield return new WaitForSeconds(0.5f);

        operation.allowSceneActivation = true;
    }
}