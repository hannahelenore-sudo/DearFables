using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class FadeIn : MonoBehaviour
{
    public float fadeDuration = 1.5f;

    private Image image;

    private void Awake()
    {
        image = GetComponent<Image>();
    }

    private IEnumerator Start()
    {
        Color color = image.color;

        float timer = 0f;

        while (timer < fadeDuration)
        {
            timer += Time.deltaTime;

            color.a = Mathf.Lerp(1f, 0f, timer / fadeDuration);
            image.color = color;

            yield return null;
        }

        color.a = 0f;
        image.color = color;

        gameObject.SetActive(false);
    }
}