using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// One trinket card inside the sidebar. Handles hover, click, drag and the little animations.
/// Created and driven by TrinketSidebar - you only style the "ItemTemplate" object it clones.
/// </summary>
public class TrinketItemView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler,
    IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public Image background;
    public Image icon;
    public TMP_Text label;
    [Tooltip("Shown while this trinket is selected.")]
    public Outline selectedOutline;

    [NonSerialized] public TrinketData data;
    [NonSerialized] public bool isHovered;
    [NonSerialized] public bool isDragging;

    private RectTransform rt;
    private TrinketSidebar owner;
    private bool dragAccepted;

    private float spawnScale = 1f;
    private float juiceScale = 1f;
    private float juiceTilt;
    private float hoverScale = 1f;
    private float dragTilt;
    private Coroutine animCo;
    private Coroutine juiceCo;

    public RectTransform Rect
    {
        get { if (rt == null) rt = (RectTransform)transform; return rt; }
    }

    public void Bind(TrinketData d, TrinketSidebar o)
    {
        data = d;
        owner = o;

        bool hasIcon = d != null && d.icon != null;
        if (icon != null)
        {
            icon.sprite = hasIcon ? d.icon : null;
            icon.enabled = hasIcon;
        }

        if (label != null)
        {
            label.text = d != null ? d.trinketName : "";
            RectTransform lr = label.rectTransform;
            lr.anchoredPosition = hasIcon ? new Vector2(0f, -38f) : Vector2.zero;
            lr.sizeDelta = hasIcon ? new Vector2(150f, 40f) : new Vector2(150f, 110f);
        }

        SetSelected(false);
        isHovered = false;
        isDragging = false;
        spawnScale = juiceScale = hoverScale = 1f;
        juiceTilt = dragTilt = 0f;
        if (animCo != null) { StopCoroutine(animCo); animCo = null; }
        if (juiceCo != null) { StopCoroutine(juiceCo); juiceCo = null; }
        Rect.localScale = Vector3.one;
        Rect.localRotation = Quaternion.identity;
    }

    public void SetSelected(bool selected)
    {
        if (selectedOutline != null) selectedOutline.enabled = selected;
    }

    private void OnDisable()
    {
        isHovered = false;
        isDragging = false;
        dragAccepted = false;
        animCo = null;
        juiceCo = null;
    }

    private void LateUpdate()
    {
        float k = 1f - Mathf.Exp(-16f * Time.unscaledDeltaTime);
        float hoverTarget = isDragging ? 1.12f : (isHovered ? 1.06f : 1f);
        hoverScale = Mathf.Lerp(hoverScale, hoverTarget, k);
        dragTilt = Mathf.Lerp(dragTilt, isDragging ? -4f : 0f, k);

        float s = spawnScale * juiceScale * hoverScale;
        Rect.localScale = new Vector3(s, s, 1f);
        Rect.localRotation = Quaternion.Euler(0f, 0f, dragTilt + juiceTilt);
    }

    // ------------------------------------------------------------------
    // Animations
    // ------------------------------------------------------------------

    public void PlaySpawn()
    {
        if (animCo != null) StopCoroutine(animCo);
        animCo = StartCoroutine(SpawnRoutine());
    }

    public void PlayRemove(Action done)
    {
        if (animCo != null) StopCoroutine(animCo);
        animCo = StartCoroutine(RemoveRoutine(done));
    }

    /// <summary>The Balatro "bounce" when a trinket triggers. scale=false gives just a wobble (for a failed chance).</summary>
    public void PlayJuice(bool scale = true)
    {
        if (juiceCo != null) StopCoroutine(juiceCo);
        juiceCo = StartCoroutine(JuiceRoutine(scale));
    }

    private IEnumerator SpawnRoutine()
    {
        const float duration = 0.28f;
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float x = Mathf.Clamp01(t / duration);
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            spawnScale = 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
            yield return null;
        }
        spawnScale = 1f;
        animCo = null;
    }

    private IEnumerator RemoveRoutine(Action done)
    {
        const float duration = 0.2f;
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float x = Mathf.Clamp01(t / duration);
            spawnScale = 1f - x * x;
            yield return null;
        }
        spawnScale = 0f;
        animCo = null;
        if (done != null) done();
    }

    private IEnumerator JuiceRoutine(bool scale)
    {
        const float duration = 0.4f;
        float dir = UnityEngine.Random.value < 0.5f ? -1f : 1f;
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float x = Mathf.Clamp01(t / duration);
            juiceScale = scale ? 1f + 0.28f * Mathf.Sin(x * Mathf.PI) : 1f;
            juiceTilt = dir * (scale ? 10f : 6f) * Mathf.Sin(x * Mathf.PI * 2f) * (1f - x);
            yield return null;
        }
        juiceScale = 1f;
        juiceTilt = 0f;
        juiceCo = null;
    }

    // ------------------------------------------------------------------
    // Pointer events
    // ------------------------------------------------------------------

    public void OnPointerEnter(PointerEventData e)
    {
        isHovered = true;
        if (owner != null) owner.OnItemHover(this, true);
    }

    public void OnPointerExit(PointerEventData e)
    {
        isHovered = false;
        if (owner != null) owner.OnItemHover(this, false);
    }

    public void OnPointerClick(PointerEventData e)
    {
        if (e.button != PointerEventData.InputButton.Left) return;
        if (owner != null) owner.OnItemClicked(this);
    }

    public void OnBeginDrag(PointerEventData e)
    {
        if (e.button != PointerEventData.InputButton.Left) return;
        dragAccepted = owner != null && owner.TryBeginDrag(this);
        isDragging = dragAccepted;
    }

    public void OnDrag(PointerEventData e)
    {
        if (dragAccepted) owner.Drag(this, e);
    }

    public void OnEndDrag(PointerEventData e)
    {
        if (!dragAccepted) return;
        dragAccepted = false;
        isDragging = false;
        owner.EndDrag(this);
    }
}
