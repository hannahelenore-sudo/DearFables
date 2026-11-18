using UnityEngine;

public class GrassSpawner : MonoBehaviour
{
    public GameObject grassPrefab;

    [Header("Spawn Area")]
    public BoxCollider spawnArea;

    [Header("Amount")]
    public int amount = 30;

    [Header("Height")]
    [Tooltip("Raises or lowers all spawned sprites. Positive = up, negative = down.")]
    public float heightOffset = 0f;

    [Tooltip("Optional random +/- variation added on top of the height offset.")]
    public float heightVariation = 0f;

    [Header("Random Size")]
    public float minScale = 0.8f;
    public float maxScale = 1.2f;

    void Start()
    {
        for (int i = 0; i < amount; i++)
        {
            SpawnGrass();
        }
    }

    void SpawnGrass()
    {
        Bounds bounds = spawnArea.bounds;

        float x = Random.Range(bounds.min.x, bounds.max.x);
        float z = Random.Range(bounds.min.z, bounds.max.z);

        float y = transform.position.y
                  + heightOffset
                  + Random.Range(-heightVariation, heightVariation);

        Vector3 position = new Vector3(x, y, z);

        GameObject grass = Instantiate(
            grassPrefab,
            position,
            Quaternion.identity,
            transform
        );

        float scale = Random.Range(minScale, maxScale);
        grass.transform.localScale = new Vector3(scale, scale, scale);

        SpriteRenderer sprite = grass.GetComponent<SpriteRenderer>();

        if (sprite != null)
        {
            sprite.flipX = Random.value > 0.5f;
        }
    }
}