using UnityEngine;
using UnityEngine.EventSystems;

public class NPCTrigger : MonoBehaviour, IPointerClickHandler
{
    [Header("Cutscene")]
    public IntroCutscene cutscene;
    public bool playOnlyOnce = true;

    private bool hasPlayed;

    // Works when the NPC is a UI Image on a Canvas
    public void OnPointerClick(PointerEventData eventData)
    {
        TryPlay();
    }

    // Works when the NPC is a sprite/object in the world with a Collider
    private void OnMouseDown()
    {
        // Ignore clicks that land on UI in front of the NPC
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        TryPlay();
    }

    private void TryPlay()
    {
        if (cutscene == null)
        {
            Debug.LogWarning("NPCTrigger: no cutscene assigned.", this);
            return;
        }

        if (playOnlyOnce && hasPlayed) return;
        if (cutscene.IsPlaying) return;

        // Cutscene object can stay disabled until now
        if (!cutscene.gameObject.activeSelf)
            cutscene.gameObject.SetActive(true);

        hasPlayed = true;
        cutscene.PlayCutscene();
    }
}