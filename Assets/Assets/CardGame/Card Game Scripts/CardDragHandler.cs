using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Lets the player pick a card up and drop it in another spot of the hand (Balatro style).
/// DeckManager adds this to every card it creates, so there is nothing to set up by hand.
/// While dragging, the card follows the mouse and tilts a little with the movement.
/// Dropping it makes it glide into its new slot. A normal click (no drag) still selects the card as before.
/// </summary>
public class CardDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [HideInInspector] public DeckManager manager;
    [HideInInspector] public CardUI card;

    private RectTransform rect;
    private RectTransform parentRect;
    private Vector2 grabOffset;
    private bool dragging;
    private float tilt;

    private void Awake()
    {
        rect = GetComponent<RectTransform>();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (manager == null || card == null || !manager.CanDragCards) return;

        parentRect = rect.parent as RectTransform;
        if (parentRect == null) return;

        Vector2 mouse;
        if (!ToAnchored(eventData, out mouse)) return;

        grabOffset = rect.anchoredPosition - mouse; // the card stays under the same spot you grabbed
        tilt = 0f;
        dragging = true;
        manager.BeginCardDrag(card);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!dragging) return;

        if (manager == null || !manager.CanDragCards)
        {
            Finish();
            return;
        }

        Vector2 mouse;
        if (!ToAnchored(eventData, out mouse)) return;

        Vector2 target = manager.ClampToHandArea(mouse + grabOffset, rect);
        float dx = target.x - rect.anchoredPosition.x;
        rect.anchoredPosition = target;

        // leans into the direction it is moved
        tilt = Mathf.Lerp(tilt, Mathf.Clamp(-dx * 1.2f, -15f, 15f), 0.35f);
        rect.localRotation = Quaternion.Euler(0f, 0f, tilt);

        manager.OnCardDragged(card);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        Finish();
    }

    private void OnDisable()
    {
        Finish();
    }

    private void Finish()
    {
        if (!dragging) return;
        dragging = false;
        if (manager != null) manager.EndCardDrag(card);
    }

    // Mouse position in the same space the card's anchoredPosition uses (anchored to the centre of its parent)
    private bool ToAnchored(PointerEventData eventData, out Vector2 anchored)
    {
        Vector2 local;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, eventData.position, eventData.pressEventCamera, out local))
        {
            anchored = local - parentRect.rect.center;
            return true;
        }
        anchored = Vector2.zero;
        return false;
    }
}
