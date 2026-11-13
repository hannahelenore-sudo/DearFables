using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Put this on a UI Button. Each click cycles the game speed: 1x -> 2x -> 4x -> back to 1x.
/// Optional: drag a TMP label in to show the current speed, and set a hotkey (default: Tab).
/// </summary>
[RequireComponent(typeof(Button))]
public class FastForwardButton : MonoBehaviour
{
    [Header("Speeds")]
    [Tooltip("The speeds the button cycles through. First one should be 1.")]
    public float[] speeds = { 1f, 2f, 4f };

    [Header("Optional")]
    [Tooltip("Label that shows the current speed, e.g. '>> 2x'.")]
    public TMP_Text label;
    [Tooltip("Tint applied to the button graphic while fast-forwarding.")]
    public Color activeTint = new Color(1f, 0.85f, 0.3f, 1f);
    public bool useHotkey = true;
    public Key hotkey = Key.Tab;
    public AudioClip clickSound;
    [Range(0f, 1f)] public float clickVolume = 1f;

    private Button button;
    private Graphic graphic;
    private Color normalColor;
    private int index;

    private void Awake()
    {
        button = GetComponent<Button>();
        graphic = button.targetGraphic;
        if (graphic != null) normalColor = graphic.color;
        button.onClick.AddListener(Cycle);
    }

    private void OnEnable()
    {
        // pick up whatever speed is already active (e.g. after a scene reload)
        index = 0;
        for (int i = 0; i < speeds.Length; i++)
            if (Mathf.Approximately(speeds[i], GameSpeed.Multiplier)) index = i;
        Refresh();
    }

    private void Update()
    {
        if (useHotkey && Keyboard.current != null && Keyboard.current[hotkey].wasPressedThisFrame)
            Cycle();
    }

    public void Cycle()
    {
        if (speeds == null || speeds.Length == 0) return;
        index = (index + 1) % speeds.Length;
        GameSpeed.Multiplier = Mathf.Max(0.1f, speeds[index]);
        GameSFX.Play(clickSound, clickVolume, GameSpeed.IsFast ? 1.2f : 1f);
        Refresh();
    }

    private void Refresh()
    {
        if (label != null)
            label.text = GameSpeed.IsFast ? ">> " + GameSpeed.Multiplier.ToString("0.#") + "x" : ">>";
        if (graphic != null)
            graphic.color = GameSpeed.IsFast ? activeTint : normalColor;
    }
}
