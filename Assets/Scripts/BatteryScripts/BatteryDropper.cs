using System.Collections;
using UnityEngine;

/// <summary>
/// Spawns BatteryPickups that burst out like Mario coins.
/// Two ways to use it:
///  1) On an ENEMY: call GetComponent&lt;BatteryDropper&gt;().Drop() when that enemy is defeated.
///  2) One in the game scene (Tools > Battery > Create Battery Drops): after you WIN a fight, the batteries you earned
///     drop where you were standing when the fight started. Nothing else to hook up.
/// </summary>
public class BatteryDropper : MonoBehaviour
{
    [Tooltip("The battery pickup to spawn (Tools > Battery > Create Battery Pickup Prefab). If empty, a plain yellow dot is used.")]
    public GameObject pickupPrefab;

    [Header("Drop() settings (for enemies)")]
    [Tooltip("Batteries dropped when you call Drop().")]
    public int amount = BatteryManager.FightBaseReward;
    [Tooltip("How many batteries each pickup is worth. 1 = one pickup per battery.")]
    public int valuePerPickup = 1;
    [Tooltip("Never spawn more pickups than this (the value is split between them instead).")]
    public int maxPickups = 15;
    public Vector3 spawnOffset = new Vector3(0f, 0.6f, 0f);

    [Header("Pop-out")]
    public float popUpSpeed = 7f;
    [Tooltip("How far sideways they fly.")]
    public float scatterSpeed = 3.5f;

    [Header("After a won fight (scene-level dropper)")]
    [Tooltip("When the scene starts, drop the batteries earned in the last fight.")]
    public bool dropPendingOnStart = false;
    public float startDelay = 0.8f;
    [Tooltip("Keeps remembering where the player stands, so the drop appears where the fight happened.")]
    public bool trackPlayerPosition = false;

    private PlayerMovement player;

    private void Start()
    {
        if (!dropPendingOnStart) return;

        // Read these NOW, before the position tracking below overwrites the fight location
        int total = BatteryManager.TakePendingDrop();
        if (total <= 0) return;

        Vector3 where = BatteryManager.HasLastPlayerPosition ? BatteryManager.LastPlayerPosition : transform.position;
        StartCoroutine(DropAfterDelay(where, total));
    }

    private void Update()
    {
        if (!trackPlayerPosition) return;

        if (player == null) player = Object.FindAnyObjectByType<PlayerMovement>();
        if (player != null && Time.timeScale > 0f)
            BatteryManager.RememberPlayerPosition(player.transform.position);
    }

    private IEnumerator DropAfterDelay(Vector3 where, int total)
    {
        if (startDelay > 0f) yield return new WaitForSeconds(startDelay);
        DropAt(where, total);
    }

    /// <summary>Drops 'amount' batteries at this object.</summary>
    public void Drop()
    {
        DropAt(transform.position, amount);
    }

    public void Drop(int total)
    {
        DropAt(transform.position, total);
    }

    public void DropAt(Vector3 position, int total)
    {
        if (total <= 0) return;

        int pickups = Mathf.Clamp(Mathf.CeilToInt(total / (float)Mathf.Max(1, valuePerPickup)), 1, Mathf.Max(1, maxPickups));
        int remaining = total;

        for (int i = 0; i < pickups; i++)
        {
            int value = Mathf.Max(1, Mathf.CeilToInt(remaining / (float)(pickups - i)));
            remaining -= value;

            GameObject go = pickupPrefab != null ? Instantiate(pickupPrefab) : CreatePlaceholderPickup();
            go.transform.position = position + spawnOffset;

            BatteryPickup pickup = go.GetComponent<BatteryPickup>();
            if (pickup == null) pickup = go.AddComponent<BatteryPickup>();
            pickup.value = value;

            // Spread them around in a circle, each with a little random push
            float angle = (i / (float)pickups) * Mathf.PI * 2f + Random.Range(-0.4f, 0.4f);
            float speed = Random.Range(scatterSpeed * 0.5f, scatterSpeed);
            Vector3 v = new Vector3(Mathf.Cos(angle) * speed, popUpSpeed * Random.Range(0.8f, 1.2f), Mathf.Sin(angle) * speed);
            pickup.Launch(v, position.y);
        }
    }

    // Used when no prefab is assigned: a plain yellow dot, so it works with zero setup
    private static Sprite placeholderSprite;

    private static GameObject CreatePlaceholderPickup()
    {
        if (placeholderSprite == null)
        {
            const int size = 32;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            Vector2 c = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    tex.SetPixel(x, y, Vector2.Distance(new Vector2(x, y), c) <= size * 0.5f - 1f ? new Color(1f, 0.85f, 0.2f) : Color.clear);
            tex.Apply();
            placeholderSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 64f);
        }

        GameObject go = new GameObject("BatteryPickup");
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = placeholderSprite;
        return go;
    }
}
