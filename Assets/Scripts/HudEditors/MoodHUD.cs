using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// The little circle that shows the player's mood, with the mood's name at the bottom.
/// Build it with  Tools > HUD > Create Player HUD  and then just swap the sprites.
/// Each mood's face comes from its MoodData asset (Resources/Moods). Moods without a face use Default Portrait.
/// </summary>
public class MoodHUD : MonoBehaviour
{
    [Header("UI")]
    [Tooltip("The face image inside the circle.")]
    public Image portrait;
    [Tooltip("The mood name at the bottom of the circle.")]
    public TMP_Text moodText;
    [Tooltip("The whole circle. It bounces when the mood changes or triggers.")]
    public RectTransform circle;
    [Tooltip("Small badge showing how many fights are left (hidden when the mood has no limit).")]
    public GameObject fightsBadge;
    public TMP_Text fightsText;
    [Tooltip("Optional text that pops up above the circle (for example 'X2 Mult').")]
    public TMP_Text popupText;

    [Header("Portraits")]
    [Tooltip("Used for any mood that has no portrait of its own (for example Neutral).")]
    public Sprite defaultPortrait;

    [Header("Look")]
    public bool outlineText = true;
    public float popupSeconds = 0.9f;

    private static MoodHUD instance;
    private Vector3 baseScale = Vector3.one;
    private Coroutine punchCo;
    private Coroutine popupCo;
    private Color popupBaseColor = Color.white;

    private void Awake()
    {
        instance = this;
        if (circle == null) circle = (RectTransform)transform;
        baseScale = circle.localScale;

        if (outlineText && moodText != null)
        {
            moodText.outlineWidth = 0.3f;
            moodText.outlineColor = new Color32(0, 0, 0, 255);
        }

        if (popupText != null)
        {
            popupBaseColor = popupText.color;
            popupText.gameObject.SetActive(false);
        }
    }

    private void OnEnable()
    {
        instance = this;
        MoodManager.Changed += OnMoodChanged;
        Refresh();
    }

    private void OnDisable()
    {
        MoodManager.Changed -= OnMoodChanged;
        if (instance == this) instance = null;
    }

    private void OnMoodChanged()
    {
        Refresh();
        Punch();
    }

    private void Refresh()
    {
        MoodData mood = MoodManager.Current;

        if (moodText != null)
        {
            moodText.text = mood.moodName;
            moodText.color = mood.textColor;
        }

        if (portrait != null)
        {
            Sprite face = mood.portrait != null ? mood.portrait : defaultPortrait;
            if (face != null) portrait.sprite = face;
        }

        bool timed = !mood.isNeutral && MoodManager.FightsLeft > 0;
        if (fightsBadge != null) fightsBadge.SetActive(timed);
        if (fightsText != null && timed) fightsText.text = MoodManager.FightsLeft.ToString();
    }

    /// <summary>Makes the circle bounce, optionally with a popup text (DeckManager uses this when a mood effect triggers).</summary>
    public static void Pulse(string label = null)
    {
        if (instance == null) return;
        instance.Punch();
        if (!string.IsNullOrEmpty(label)) instance.ShowPopup(label);
    }

    private void Punch()
    {
        if (!isActiveAndEnabled) return;
        if (punchCo != null) StopCoroutine(punchCo);
        punchCo = StartCoroutine(PunchRoutine());
    }

    private IEnumerator PunchRoutine()
    {
        const float duration = 0.45f;
        float dir = Random.value < 0.5f ? -1f : 1f;
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float x = Mathf.Clamp01(t / duration);
            float s = 1f + 0.25f * Mathf.Sin(x * Mathf.PI);
            circle.localScale = baseScale * s;
            circle.localRotation = Quaternion.Euler(0f, 0f, dir * 8f * Mathf.Sin(x * Mathf.PI * 2f) * (1f - x));
            yield return null;
        }
        circle.localScale = baseScale;
        circle.localRotation = Quaternion.identity;
        punchCo = null;
    }

    private void ShowPopup(string label)
    {
        if (popupText == null) return;
        if (popupCo != null) StopCoroutine(popupCo);
        popupCo = StartCoroutine(PopupRoutine(label));
    }

    private IEnumerator PopupRoutine(string label)
    {
        popupText.gameObject.SetActive(true);
        popupText.text = label;

        RectTransform r = popupText.rectTransform;
        Vector2 start = r.anchoredPosition;

        float t = 0f;
        while (t < popupSeconds)
        {
            t += Time.unscaledDeltaTime;
            float x = Mathf.Clamp01(t / popupSeconds);
            r.anchoredPosition = start + Vector2.up * (x * 30f);
            Color c = popupBaseColor;
            c.a = popupBaseColor.a * (x < 0.6f ? 1f : 1f - (x - 0.6f) / 0.4f);
            popupText.color = c;
            yield return null;
        }

        r.anchoredPosition = start;
        popupText.color = popupBaseColor;
        popupText.gameObject.SetActive(false);
        popupCo = null;
    }
}
