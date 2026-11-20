using UnityEngine;
using System.Collections.Generic;

public class TreeSpawner : MonoBehaviour
{
    [Header("Tree Styles")]
    public GameObject[] treePrefabs;

    [Header("Spawn Area")]
    public BoxCollider spawnArea;

    [Header("Amount")]
    public int amount = 15;

    [Header("Random Size")]
    public float minScale = 0.8f;
    public float maxScale = 1.2f;

    [Header("Spacing")]
    public float minDistance = 2f;

    [Header("Distance Culling (less lag)")]
    [Tooltip("ON: trees far away from the player are switched off, and back on when the player comes close.")]
    public bool cullByDistance = true;
    [Tooltip("The player. Empty = finds the object tagged 'Player', or falls back to the main camera.")]
    public Transform player;
    public string playerTag = "Player";
    [Tooltip("Trees closer than this to the player (world units) are visible.")]
    public float activeDistance = 30f;
    [Tooltip("Extra distance before an active tree switches off, so trees at the edge don't flicker.")]
    public float cullBuffer = 2f;
    [Tooltip("How often (seconds) the distances are checked.")]
    public float cullCheckInterval = 0.25f;

    private List<Vector3> spawnedPositions = new List<Vector3>();
    private List<GameObject> spawnedTrees = new List<GameObject>();
    private float nextCullCheck;

    void Start()
    {
        for (int i = 0; i < amount; i++)
        {
            SpawnTree();
        }
    }

    void Update()
    {
        if (Time.time < nextCullCheck) return;
        nextCullCheck = Time.time + cullCheckInterval;

        if (!cullByDistance)
        {
            foreach (GameObject t in spawnedTrees)
                if (t != null && !t.activeSelf) t.SetActive(true);
            return;
        }

        if (player == null)
        {
            GameObject found = GameObject.FindGameObjectWithTag(playerTag);
            if (found != null) player = found.transform;
            else if (Camera.main != null) player = Camera.main.transform;
            if (player == null) return;
        }

        Vector3 pp = player.position;
        float onSqr = activeDistance * activeDistance;
        float offDist = activeDistance + Mathf.Max(0f, cullBuffer);
        float offSqr = offDist * offDist;

        for (int i = 0; i < spawnedTrees.Count; i++)
        {
            GameObject t = spawnedTrees[i];
            if (t == null) continue;

            // flat distance (the field lies on the X/Z plane)
            Vector3 pos = t.transform.position;
            float dx = pos.x - pp.x;
            float dz = pos.z - pp.z;
            float sqr = dx * dx + dz * dz;

            bool shouldBeActive = t.activeSelf ? sqr <= offSqr : sqr <= onSqr;
            if (shouldBeActive != t.activeSelf) t.SetActive(shouldBeActive);
        }
    }

    void SpawnTree()
    {
        if (treePrefabs == null || treePrefabs.Length == 0)
            return;

        Bounds bounds = spawnArea.bounds;

        Vector3 position = Vector3.zero;
        bool validPosition = false;

        int attempts = 0;
        int maxAttempts = 50;

        while (!validPosition && attempts < maxAttempts)
        {
            float x = Random.Range(bounds.min.x, bounds.max.x);
            float z = Random.Range(bounds.min.z, bounds.max.z);
            float y = transform.position.y;

            position = new Vector3(x, y, z);

            validPosition = true;

            foreach (Vector3 existingPos in spawnedPositions)
            {
                if (Vector3.Distance(position, existingPos) < minDistance)
                {
                    validPosition = false;
                    break;
                }
            }

            attempts++;
        }

        if (!validPosition)
            return;

        spawnedPositions.Add(position);

        GameObject selectedTree = treePrefabs[Random.Range(0, treePrefabs.Length)];

        if (selectedTree == null)
            return;

        GameObject tree = Instantiate(
            selectedTree,
            position,
            Quaternion.identity,
            transform
        );

        spawnedTrees.Add(tree);

        float scale = Random.Range(minScale, maxScale);

        tree.transform.localScale = new Vector3(scale, scale, scale);

        SpriteRenderer sprite = tree.GetComponent<SpriteRenderer>();

        if (sprite != null)
        {
            sprite.flipX = Random.value > 0.5f;
        }
    }
}