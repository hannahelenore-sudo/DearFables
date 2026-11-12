using UnityEngine;

/// <summary>
/// Wind streaks that swirl around the player while the wind boost is active.
/// Streaks in front of the player draw over it, streaks behind it draw underneath (so it looks 3D).
/// </summary>
public class WindAura : MonoBehaviour
{
    public int streakCount = 10;
    public Color windColor = new Color(0.8f, 0.95f, 1f, 1f);
    [Tooltip("How fast the streaks circle the player (degrees/second).")]
    public float orbitSpeed = 420f;
    [Tooltip("Streak length as a fraction of the character's height.")]
    public float lengthRatio = 0.45f;
    [Tooltip("How far the streaks float away from the body.")]
    public float radiusPadding = 0.05f;
    [Tooltip("How much of the character's half-width the streaks circle at. 1 = at the body's edge, lower = hugging it.")]
    [Range(0.3f, 1.5f)] public float bodyHug = 0.7f;

    private SpriteRenderer character;
    private Transform root;
    private SpriteRenderer[] streaks;
    private float[] angle, speed, height, radiusMul, lenMul, phase;
    private float intensity;
    private bool active;

    public void Init(SpriteRenderer characterRenderer)
    {
        character = characterRenderer;
        if (root != null) return;

        root = new GameObject("WindAura").transform;
        streaks = new SpriteRenderer[streakCount];
        angle = new float[streakCount];
        speed = new float[streakCount];
        height = new float[streakCount];
        radiusMul = new float[streakCount];
        lenMul = new float[streakCount];
        phase = new float[streakCount];

        Sprite sprite = ItemSprites.Streak;
        for (int i = 0; i < streakCount; i++)
        {
            GameObject go = new GameObject("Streak" + i);
            go.transform.SetParent(root, false);
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            if (character != null) sr.sortingLayerID = character.sortingLayerID;
            sr.enabled = false;
            streaks[i] = sr;

            angle[i] = Random.value * 360f;
            speed[i] = orbitSpeed * Random.Range(0.7f, 1.3f);
            height[i] = Random.value;
            radiusMul[i] = Random.Range(0.9f, 1.1f);
            lenMul[i] = Random.Range(0.7f, 1.3f);
            phase[i] = Random.value * 6.28f;
        }
    }

    public void SetActive(bool value)
    {
        active = value;
    }

    private void LateUpdate()
    {
        if (streaks == null) return;

        float dt = Time.deltaTime;
        intensity = Mathf.MoveTowards(intensity, active ? 1f : 0f, dt * 6f);

        Camera cam = Camera.main;
        if (cam == null) return;

        if (intensity <= 0.01f)
        {
            foreach (SpriteRenderer s in streaks)
                if (s != null && s.enabled) s.enabled = false;
            return;
        }

        float feet = transform.position.y;
        float charH = 2f;
        float halfW = 0.5f;
        if (character != null)
        {
            Bounds b = character.bounds;
            feet = b.min.y;
            charH = Mathf.Max(0.5f, b.size.y);
            halfW = b.size.x * 0.5f;
        }

        float baseRadius = Mathf.Max(0.15f, halfW * bodyHug) + radiusPadding;
        Vector3 center = new Vector3(transform.position.x, feet, transform.position.z);

        Vector3 camFwd = cam.transform.forward;
        camFwd.y = 0f;
        camFwd = camFwd.sqrMagnitude < 0.0001f ? Vector3.forward : camFwd.normalized;

        int baseOrder = character != null ? character.sortingOrder : 0;

        for (int i = 0; i < streaks.Length; i++)
        {
            SpriteRenderer sr = streaks[i];
            if (sr == null) continue;

            angle[i] += speed[i] * dt * (0.5f + 0.5f * intensity);
            float a = angle[i] * Mathf.Deg2Rad;

            Vector3 offset = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (baseRadius * radiusMul[i]);
            Vector3 tangent = new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a)); // direction the streak flies
            Vector3 pos = center + offset + Vector3.up * (charH * Mathf.Lerp(0.1f, 0.95f, height[i]));

            // Point the streak along its flight direction as seen on screen
            Vector3 local = cam.transform.InverseTransformDirection(tangent);
            float flat = Mathf.Sqrt(local.x * local.x + local.y * local.y);
            float ang = Mathf.Atan2(local.y, local.x) * Mathf.Rad2Deg;

            float len = charH * lengthRatio * lenMul[i];

            float env = 0.5f + 0.5f * Mathf.Sin(Time.time * 4f + phase[i]);
            Color c = windColor;
            c.a = windColor.a * intensity * Mathf.Lerp(0.35f, 0.9f, env);

            sr.enabled = true;
            sr.color = c;
            Transform t = sr.transform;
            t.position = pos;
            t.rotation = cam.transform.rotation * Quaternion.Euler(0f, 0f, ang);
            t.localScale = new Vector3(len * Mathf.Lerp(0.3f, 1f, flat), len, 1f);

            // Closer to the camera than the player -> in front, otherwise behind
            float depth = Vector3.Dot(offset, camFwd);
            sr.sortingOrder = depth < 0f ? baseOrder + 2 : baseOrder - 2;
        }
    }

    private void OnDestroy()
    {
        if (root != null) Destroy(root.gameObject);
    }
}