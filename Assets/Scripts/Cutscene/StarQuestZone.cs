using UnityEngine;

public class StarQuestZone : MonoBehaviour
{
    [Tooltip("The Star Quest NPC whose Intro dialogue this zone starts. Leave empty to auto-find the one in the scene.")]
    public StarQuestNPC npc;
    [Tooltip("ON: once the quest is accepted the zone's collider and this script switch off. OFF: the zone stays but does nothing. (Only this collider is switched off, never the whole object.)")]
    public bool disappearWhenAccepted = true;

    private Collider zoneCollider;

    private void Reset()
    {
        Collider c = GetComponent<Collider>();
        if (c == null) c = gameObject.AddComponent<BoxCollider>();
        c.isTrigger = true;
    }

    private void Awake()
    {
        zoneCollider = GetComponent<Collider>();
        if (zoneCollider == null)
            Debug.LogWarning("StarQuestZone: this object has no Collider. Add a Box Collider (it is made a trigger for you).", this);
        else
            zoneCollider.isTrigger = true;

        if (npc == null) npc = FindAnyObjectByType<StarQuestNPC>();
        if (npc == null)
            Debug.LogWarning("StarQuestZone: no Star Quest NPC assigned or found in the scene.", this);

        CheckAccepted();
    }

    private void Update()
    {
        CheckAccepted();
    }

    private void CheckAccepted()
    {
        if (!disappearWhenAccepted || npc == null || !npc.Accepted) return;

        if (zoneCollider != null) zoneCollider.enabled = false;
        enabled = false;
    }

    private void OnTriggerStay(Collider other)
    {
        if (npc != null) npc.ZoneTriggered(other);
    }
}