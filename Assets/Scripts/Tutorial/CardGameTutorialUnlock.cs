using System.Collections;
using UnityEngine;

public class CardGameTutorialUnlock : MonoBehaviour
{
    [Header("Player (the wall blocks this)")]
    [Tooltip("Drag the player here: the component that moves it (CharacterController, Rigidbody, or just its Transform). Leave empty to find the object tagged 'Player'.")]
    public Component player;
    public string playerTag = "Player";
    [Tooltip("Extra safety for players that move by changing their position directly (no CharacterController / physics): the player is pushed back out if it ends up inside the wall.")]
    public bool pushPlayerOut = true;

    [Header("Wall")]
    [Tooltip("Drag the wall(s) in here. They block the player until the tutorial card game is finished, then they disappear.")]
    public GameObject[] wallsToRemove;
    [Tooltip("If a wall has no collider at all, give it a solid Box Collider so it blocks the player.")]
    public bool addColliderIfMissing = true;
    [Tooltip("Seconds the wall takes to shrink away (0 = vanishes instantly).")]
    public float disappearDuration = 0.4f;

    [Header("Sound")]
    public AudioClip disappearSound;
    [Range(0f, 1f)] public float disappearVolume = 1f;

    private bool removed;
    private bool playerLinked;
    private Collider[] wallColliders = new Collider[0];
    private Collider[] playerColliders = new Collider[0];
    private CharacterController playerController;
    private Rigidbody playerBody;

    private void Awake()
    {
        if (CardGameTutorialGate.Completed)
        {
            Remove(false);
            return;
        }

        var found = new System.Collections.Generic.List<Collider>();
        if (wallsToRemove != null)
        {
            foreach (GameObject wall in wallsToRemove)
            {
                if (wall == null) continue;
                wall.SetActive(true);

                Collider[] cols3D = wall.GetComponentsInChildren<Collider>(true);
                Collider2D[] cols2D = wall.GetComponentsInChildren<Collider2D>(true);

                if (cols3D.Length == 0 && cols2D.Length == 0 && addColliderIfMissing)
                    cols3D = new Collider[] { wall.AddComponent<BoxCollider>() };

                foreach (Collider c in cols3D)
                {
                    c.enabled = true;
                    c.isTrigger = false;
                    found.Add(c);
                }
                foreach (Collider2D c in cols2D) { c.enabled = true; c.isTrigger = false; }
            }
        }
        wallColliders = found.ToArray();

        TryLinkPlayer();
    }

    private void Update()
    {
        if (removed) return;

        if (CardGameTutorialGate.Completed)
        {
            Remove(true);
            return;
        }

        if (!playerLinked) TryLinkPlayer();
    }

    private void LateUpdate()
    {
        if (removed || !pushPlayerOut || !playerLinked) return;
        PushPlayerOut();
    }

    private void TryLinkPlayer()
    {
        if (player == null)
        {
            GameObject tagged = null;
            try { tagged = GameObject.FindGameObjectWithTag(playerTag); } catch { }
            if (tagged != null) player = tagged.transform;
        }
        if (player == null) return;

        playerColliders = player.GetComponentsInChildren<Collider>();
        playerController = player.GetComponentInParent<CharacterController>();
        playerBody = player.GetComponentInParent<Rigidbody>();

        if (wallsToRemove != null)
            foreach (GameObject wall in wallsToRemove)
            {
                if (wall == null) continue;
                Physics.IgnoreLayerCollision(wall.layer, player.gameObject.layer, false);
            }
        foreach (Collider w in wallColliders)
            foreach (Collider p in playerColliders)
                if (w != null && p != null) Physics.IgnoreCollision(w, p, false);

        playerLinked = true;
    }

    private void PushPlayerOut()
    {
        if (playerController != null) return;
        if (playerBody != null && !playerBody.isKinematic) return;

        foreach (Collider w in wallColliders)
        {
            if (w == null || !w.enabled || !w.gameObject.activeInHierarchy) continue;

            foreach (Collider p in playerColliders)
            {
                if (p == null || !p.enabled || p.isTrigger || !p.gameObject.activeInHierarchy) continue;

                if (Physics.ComputePenetration(
                        p, p.transform.position, p.transform.rotation,
                        w, w.transform.position, w.transform.rotation,
                        out Vector3 direction, out float distance))
                {
                    player.transform.position += direction * distance;
                }
            }
        }
    }

    private void Remove(bool animate)
    {
        removed = true;

        if (animate && disappearSound != null) GameSFX.Play(disappearSound, disappearVolume, 1f);
        if (wallsToRemove == null) return;

        foreach (GameObject wall in wallsToRemove)
        {
            if (wall == null) continue;

            foreach (Collider c in wall.GetComponentsInChildren<Collider>(true)) c.enabled = false;
            foreach (Collider2D c in wall.GetComponentsInChildren<Collider2D>(true)) c.enabled = false;

            if (animate && disappearDuration > 0f && wall.activeInHierarchy)
                StartCoroutine(Shrink(wall));
            else
                wall.SetActive(false);
        }
    }

    private IEnumerator Shrink(GameObject wall)
    {
        Transform t = wall.transform;
        Vector3 baseScale = t.localScale;

        float elapsed = 0f;
        while (elapsed < disappearDuration && t != null)
        {
            elapsed += Time.unscaledDeltaTime;
            float x = Mathf.Clamp01(elapsed / disappearDuration);
            t.localScale = baseScale * (1f - x * x);
            yield return null;
        }

        if (wall != null)
        {
            t.localScale = baseScale;
            wall.SetActive(false);
        }
    }
}