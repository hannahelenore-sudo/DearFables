using TMPro;
using UnityEngine;

public class LoadingTextAnimation : MonoBehaviour
{
    private TMP_Text loadingText;
    private float timer;
    private int index;

    private string[] messages =
    {
        "Loading...",
        "Loading..",
        "Loading."
    };

    void Start()
    {
        loadingText = GetComponent<TMP_Text>();
        loadingText.text = messages[0];
    }

    void Update()
    {
        timer += Time.deltaTime;

        if (timer >= 0.5f)
        {
            timer = 0f;
            index = (index + 1) % messages.Length;
            loadingText.text = messages[index];
        }
    }
}