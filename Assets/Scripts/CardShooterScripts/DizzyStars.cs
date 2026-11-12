using UnityEngine;

/// <summary>
/// Cartoon stars circling an enemy's head while it is stunned.
/// Added automatically by EnemyChase the first time the enemy is stunned.
/// </summary>
public class DizzyStars : MonoBehaviour
{
    public int starCount = 4;
    [Tooltip("Star size as a fraction of the enemy's height.")]
    public float starHeightRatio = 0.2f;
    public float orbitSpeed = 260f;
    [Tooltip("Spin of each star around itself (degrees/second).")]
    public float selfSpin = 300f;

    private SpriteRenderer target;
    private Transform root;
    private SpriteRenderer[] stars;
    private float intensity;
    private bool active;
    private float angle;

    public void Init(SpriteRenderer enemySprite)
    {
        target = enemySprite;
        if (root != null) return;

        root = new GameObject("DizzyStars").transform;
        stars = new SpriteRenderer[starCount];
        Sprite sprite = ItemSprites.Star;

        for (int i = 0; i < starCount; i++)
        {
            GameObject go = new GameObject("Star" + i);
            go.transform.SetParent(root, false);
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            if (target != null) sr.sortingLayerID = target.sortingLayerID;
            sr.enabled = false;
            stars[i] = sr;
        }
    }

    public void SetActive(bool value)
    {
        active = value;
    }

    private void LateUpdate()
    {
        if (stars == null) return;

        intensity = Mathf.MoveTowards(intensity, active ? 1f : 0f, Time.deltaTime * 6f);

        Camera cam = Camera.main;
        if (cam == null) return;

        if (intensity <= 0.01f)
        {
            foreach (SpriteRenderer s in stars)
                if (s != null && s.enabled) s.enabled = false;
            return;
        }

        float height = 1f;
        float radius = 0.4f;
        Vector3 top = transform.position + Vector3.up * 1.5f;
        int baseOrder = 0;
        if (target != null)
        {
            Bounds b = target.bounds;
            height = Mathf.Max(0.3f, b.size.y);
            radius = Mathf.Max(0.25f, b.size.x * 0.4f);
            top = new Vector3(transform.position.x, b.max.y, transform.position.z);
            baseOrder = target.sortingOrder;
        }
        top += Vector3.up * (height * 0.08f);

        angle += orbitSpeed * Time.deltaTime;

        Vector3 camFwd = cam.transform.forward;
        camFwd.y = 0f;
        camFwd = camFwd.sqrMagnitude < 0.0001f ? Vector3.forward : camFwd.normalized;

        for (int i = 0; i < stars.Length; i++)
        {
            SpriteRenderer sr = stars[i];
            if (sr == null) continue;

            float a = (angle + i * 360f / stars.Length) * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius;
            float bob = Mathf.Sin(Time.time * 6f + i * 1.7f) * height * 0.03f;

            sr.enabled = true;
            Transform t = sr.transform;
            t.position = top + offset + Vector3.up * bob;
            t.rotation = cam.transform.rotation * Quaternion.Euler(0f, 0f, Time.time * selfSpin + i * 40f);
            t.localScale = Vector3.one * (height * starHeightRatio * intensity);

            // In front of the head when closer to the camera, behind it otherwise
            float depth = Vector3.Dot(offset, camFwd);
            sr.sortingOrder = depth < 0f ? baseOrder + 2 : baseOrder - 2;
        }
    }

    private void OnDestroy()
    {
        if (root != null) Destroy(root.gameObject);
    }
}
