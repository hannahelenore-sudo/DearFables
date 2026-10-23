using UnityEngine;
using System.Collections.Generic;

public class TreeSpawner : MonoBehaviour
{
    public GameObject treePrefab;

    [Header("Spawn Area")]
    public BoxCollider spawnArea;

    [Header("Amount")]
    public int amount = 15;

    [Header("Random Size")]
    public float minScale = 0.8f;
    public float maxScale = 1.2f;

    [Header("Spacing")]
    public float minDistance = 2f;

    private List<Vector3> spawnedPositions = new List<Vector3>();

    void Start()
    {
        for (int i = 0; i < amount; i++)
        {
            SpawnTree();
        }
    }

    void SpawnTree()
    {
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

        GameObject tree = Instantiate(
            treePrefab,
            position,
            Quaternion.identity,
            transform
        );

        float scale = Random.Range(minScale, maxScale);

        tree.transform.localScale = new Vector3(
            scale,
            scale,
            scale
        );

        SpriteRenderer sprite = tree.GetComponent<SpriteRenderer>();

        if (sprite != null)
        {
            sprite.flipX = Random.value > 0.5f;
        }
    }
}