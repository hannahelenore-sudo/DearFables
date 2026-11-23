using System.Collections;
using UnityEngine;

public class BlueprintShopVisual : MonoBehaviour
{
    public enum FitMode
    {
        [Tooltip("Scale the new sprite so it fits inside the empty sprite's area (keeps its proportions).")]
        FitInside,
        [Tooltip("Same width as the empty sprite, height follows the new sprite's proportions.")]
        MatchWidth,
        [Tooltip("Same height as the empty sprite, width follows the new sprite's proportions.")]
        MatchHeight,
        [Tooltip("Force the exact same width AND height (can squash the sprite).")]
        Stretch
    }

    public enum AnchorPoint { Bottom, Center }

    [Header("Structure")]
    [Tooltip("The renderer that shows the structure. Best on its own child object. Found automatically if empty.")]
    public SpriteRenderer target;

    [Tooltip("Nothing built yet. If empty, the sprite the renderer already has is used. This one sets the reference spot and size.")]
    public Sprite emptySprite;
    public Sprite solarSprite;
    public Sprite windSprite;

    [Header("Keep Position & Size")]
    [Tooltip("Fit the new structure sprite to the empty sprite's size.")]
    public bool keepSize = true;
    public FitMode fitMode = FitMode.FitInside;
    [Tooltip("Which point of the sprite stays in the same place: Bottom = the feet stay on the ground.")]
    public AnchorPoint keepAnchor = AnchorPoint.Bottom;

    [Header("Shopkeeper")]
    [Tooltip("The shopkeeper's own sprite renderer (a separate object from the structure).")]
    public SpriteRenderer shopkeeperRenderer;
    [Tooltip("Optional: sets the shopkeeper's sprite. Leave empty to use the sprite already on the renderer.")]
    public Sprite shopkeeperSprite;

    [Header("Change Animation")]
    public float popScale = 1.15f;
    public float popDuration = 0.35f;
    [Tooltip("Optional: played when the sprite changes (build or destroy).")]
    public ParticleSystem changePuff;

    private Vector3 baseScale = Vector3.one;
    private Vector3 basePos;
    private Vector2 refSize = Vector2.one;
    private Vector2 refAnchor;
    private bool hasReference;

    private Sprite shownSprite;
    private Vector3 shownScale = Vector3.one;
    private StructureType shown = (StructureType)(-1);
    private Coroutine popCo;

    private void Awake()
    {
        if (target == null) target = GetComponent<SpriteRenderer>();
        if (target == null) target = GetComponentInChildren<SpriteRenderer>();

        if (target != null)
        {
            if (emptySprite == null) emptySprite = target.sprite;

            baseScale = target.transform.localScale;
            basePos = target.transform.localPosition;
            CaptureReference();
        }

        if (shopkeeperRenderer != null && shopkeeperSprite != null)
            shopkeeperRenderer.sprite = shopkeeperSprite;
    }

    private void CaptureReference()
    {
        if (emptySprite == null) return;

        Bounds b = emptySprite.bounds;
        refSize = new Vector2(b.size.x * Mathf.Abs(baseScale.x), b.size.y * Mathf.Abs(baseScale.y));
        refAnchor = new Vector2(
            basePos.x + b.center.x * baseScale.x,
            basePos.y + AnchorY(b) * baseScale.y);
        hasReference = refSize.x > 0.0001f && refSize.y > 0.0001f;
    }

    private float AnchorY(Bounds b)
    {
        return keepAnchor == AnchorPoint.Bottom ? b.min.y : b.center.y;
    }

    private void OnEnable()
    {
        StructureProgress.Changed += OnChanged;
        Apply(false);
    }

    private void OnDisable()
    {
        StructureProgress.Changed -= OnChanged;
        if (popCo != null) { StopCoroutine(popCo); popCo = null; }
        if (target != null && shownSprite != null) Place(shownSprite, shownScale);
    }

    private void OnChanged()
    {
        if (StructureProgress.Current != shown) Apply(true);
    }

    private void Apply(bool animate)
    {
        if (target == null) return;

        shown = StructureProgress.Current;

        Sprite s = emptySprite;
        if (shown == StructureType.Solar && solarSprite != null) s = solarSprite;
        else if (shown == StructureType.Wind && windSprite != null) s = windSprite;

        if (s != null)
        {
            target.sprite = s;
            shownSprite = s;
            shownScale = FittedScale(s);
            Place(s, shownScale);
        }

        if (animate)
        {
            if (changePuff != null) changePuff.Play();
            if (popCo != null) StopCoroutine(popCo);
            popCo = StartCoroutine(Pop());
        }
    }

    private Vector3 FittedScale(Sprite s)
    {
        if (!keepSize || !hasReference || s == emptySprite) return baseScale;

        Vector3 size = s.bounds.size;
        if (size.x < 0.0001f || size.y < 0.0001f) return baseScale;

        float sx = refSize.x / size.x;
        float sy = refSize.y / size.y;

        float x, y;
        switch (fitMode)
        {
            case FitMode.MatchWidth: x = y = sx; break;
            case FitMode.MatchHeight: x = y = sy; break;
            case FitMode.Stretch: x = sx; y = sy; break;
            default: x = y = Mathf.Min(sx, sy); break;
        }

        return new Vector3(x * Mathf.Sign(baseScale.x), y * Mathf.Sign(baseScale.y), baseScale.z);
    }

    private void Place(Sprite s, Vector3 scale)
    {
        Transform t = target.transform;
        t.localScale = scale;

        if (!keepSize || !hasReference)
        {
            t.localPosition = basePos;
            return;
        }

        Bounds b = s.bounds;
        Vector3 p = t.localPosition;
        p.x = refAnchor.x - b.center.x * scale.x;
        p.y = refAnchor.y - AnchorY(b) * scale.y;
        p.z = basePos.z;
        t.localPosition = p;
    }

    private IEnumerator Pop()
    {
        float t = 0f;
        while (t < popDuration)
        {
            t += Time.unscaledDeltaTime;
            float x = Mathf.Clamp01(t / popDuration);
            float s = 1f + (popScale - 1f) * Mathf.Sin(x * Mathf.PI);
            Place(shownSprite, shownScale * s);
            yield return null;
        }
        Place(shownSprite, shownScale);
        popCo = null;
    }
}