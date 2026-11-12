using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shows PlayerStamina as a bar. The Fill is shrunk from the right, so any sprite
/// (pixel art, sliced, plain colour) works without needing a "Filled" image type.
/// </summary>
public class StaminaBarHUD : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The Fill rect (stretched to its parent). Its right edge moves with the stamina.")]
    public RectTransform fill;
    public Image fillImage;

    [Header("Look")]
    public Color normalColor = new Color(0.25f, 0.95f, 1f);
    [Tooltip("Colour while locked out after running dry.")]
    public Color exhaustedColor = new Color(1f, 0.35f, 0.3f);
    [Tooltip("How quickly the bar catches up to the real value. Higher = snappier.")]
    public float smoothing = 14f;

    private float shown = 1f;

    private void OnEnable()
    {
        shown = PlayerStamina.Value;
        Apply();
    }

    private void Update()
    {
        shown = Mathf.Lerp(shown, PlayerStamina.Value, 1f - Mathf.Exp(-smoothing * Time.unscaledDeltaTime));
        Apply();
    }

    private void Apply()
    {
        if (fill != null)
            fill.anchorMax = new Vector2(Mathf.Clamp01(shown), 1f);

        if (fillImage != null)
            fillImage.color = PlayerStamina.Exhausted ? exhaustedColor : normalColor;
    }
}
