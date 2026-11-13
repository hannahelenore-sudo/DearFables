using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Builds its own always-on-top Canvas with a fast-forward button, so the button works everywhere
/// in the card game (opening sequence, hand, roll screens, score tally...) no matter which other
/// canvases get switched on or off. Add this to ONE empty GameObject in the CardGame scene. Nothing else to set up.
/// </summary>
public class FastForwardOverlay : MonoBehaviour
{
    [Header("Layout")]
    public Vector2 size = new Vector2(110f, 60f);
    [Tooltip("Distance from the top-right corner of the screen.")]
    public Vector2 margin = new Vector2(30f, 30f);
    [Tooltip("Higher than every other canvas in the scene so it is always visible and clickable.")]
    public int sortingOrder = 999;

    [Header("Look")]
    public Color backgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.75f);
    public Color activeTint = new Color(1f, 0.85f, 0.3f, 1f);
    public float fontSize = 32f;

    [Header("Speeds / Sound")]
    public float[] speeds = { 1f, 2f, 4f };
    public AudioClip clickSound;
    [Range(0f, 1f)] public float clickVolume = 1f;

    private void Awake()
    {
        // Canvas
        GameObject canvasGO = new GameObject("FastForwardCanvas");
        canvasGO.transform.SetParent(transform, false);
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = sortingOrder;
        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();

        // Button (created inactive so the label can be assigned before FastForwardButton starts)
        GameObject btnGO = new GameObject("FastForwardButton", typeof(RectTransform));
        btnGO.SetActive(false);
        btnGO.transform.SetParent(canvasGO.transform, false);
        RectTransform rt = (RectTransform)btnGO.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 1f);
        rt.sizeDelta = size;
        rt.anchoredPosition = new Vector2(-margin.x, -margin.y);

        Image img = btnGO.AddComponent<Image>();
        img.color = backgroundColor;
        Button btn = btnGO.AddComponent<Button>();
        btn.targetGraphic = img;

        // Label
        GameObject textGO = new GameObject("Label", typeof(RectTransform));
        textGO.transform.SetParent(btnGO.transform, false);
        RectTransform trt = (RectTransform)textGO.transform;
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = trt.offsetMax = Vector2.zero;
        TextMeshProUGUI label = textGO.AddComponent<TextMeshProUGUI>();
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = fontSize;
        label.color = Color.white;
        label.raycastTarget = false;

        btnGO.AddComponent<InteractiveUIButton>().enableWiggleOnUnsupportedClick = false;

        FastForwardButton ff = btnGO.AddComponent<FastForwardButton>();
        ff.label = label;
        ff.speeds = speeds;
        ff.activeTint = activeTint;
        ff.clickSound = clickSound;
        ff.clickVolume = clickVolume;
        btnGO.SetActive(true);
    }
}
