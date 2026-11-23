using UnityEngine;

[DisallowMultipleComponent]
public class StarQuestTutorialTrigger : MonoBehaviour
{
    [Tooltip("Only count hits/touches while the NPC's EnemyChase is enabled (i.e. it became an enemy).")]
    public bool requireEnemyChaseEnabled = true;

    [Tooltip("A non-player object that touches the NPC counts as a card hit if its name contains one of these.")]
    public string[] cardNameHints = { "card" };

    [Tooltip("Player counts as touching the NPC within this many world units (XZ distance).")]
    public float touchDistance = 1.3f;

    public string playerTag = "Player";

    Behaviour enemyChase;
    Transform player;

    void Awake()
    {
        CardGameTutorialGate.Pending = false;
        enemyChase = GetComponent("EnemyChase") as Behaviour;
    }

    void Start()
    {
        var p = GameObject.FindGameObjectWithTag(playerTag);
        if (p != null) player = p.transform;
    }

    bool IsEnemyNow()
    {
        if (!isActiveAndEnabled) return false;
        if (!requireEnemyChaseEnabled || enemyChase == null) return true;
        return enemyChase.enabled;
    }

    void Update()
    {
        if (player == null || !IsEnemyNow()) return;

        Vector3 d = player.position - transform.position;
        d.y = 0f;
        if (d.sqrMagnitude <= touchDistance * touchDistance)
            CardGameTutorialGate.NoteStarQuestEngaged();
    }

    void Hit(GameObject other)
    {
        if (other == null || !IsEnemyNow()) return;

        if (other.CompareTag(playerTag) || other.transform.root.CompareTag(playerTag))
        {
            CardGameTutorialGate.NoteStarQuestEngaged();
            return;
        }

        string n = other.name.ToLowerInvariant();
        foreach (var hint in cardNameHints)
        {
            if (!string.IsNullOrEmpty(hint) && n.Contains(hint.ToLowerInvariant()))
            {
                CardGameTutorialGate.NoteStarQuestEngaged();
                return;
            }
        }
    }

    void OnCollisionEnter(Collision c) { Hit(c.gameObject); }
    void OnTriggerEnter(Collider c) { Hit(c.gameObject); }
    void OnCollisionEnter2D(Collision2D c) { Hit(c.gameObject); }
    void OnTriggerEnter2D(Collider2D c) { Hit(c.gameObject); }
}