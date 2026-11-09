using UnityEngine;

/// <summary>
/// Flies in a straight line with a little spin and hits the first enemy it passes.
/// At the end of its range it turns into a physics object (Rigidbody + BoxCollider),
/// falls onto the ground, settles, fades out (card + glow) and disappears.
/// </summary>
public class CardProjectile : MonoBehaviour
{
    private enum State { Flying, Falling, Landed }

    private State state = State.Flying;

    private Vector3 direction;
    private float speed;
    private float maxDistance;
    private float hitRadius;
    private float spinSpeed;
    private float travelled;
    private float spinAngle;
    private EnemyChase[] enemies;
    private Collider[] ignoreColliders;

    private float forwardSpeedWhenFalling;
    private float tumbleSpeed;
    private float thickness;
    private float bounciness;
    private float friction;
    private float maxSettleTime;
    private float groundStayTime;
    private float fadeTime;

    private Rigidbody rb;
    private SpriteRenderer sr;
    private bool touchedGround;
    private float fallTimer;
    private float landedTimer;
    private float killY;

    // Glow
    private SpriteRenderer glowSr;
    private float glowBaseAlpha;
    private float pulseSpeed;
    private float pulseAmount;
    private float pulseOffset;
    private float fade = 1f;

    public void Launch(Vector3 dir, float speed, float range, float hitRadius, float spinSpeed,
                       EnemyChase[] enemies, Collider[] ignoreColliders,
                       float forwardSpeedWhenFalling, float tumbleSpeed, float thickness,
                       float bounciness, float friction, float maxSettleTime,
                       float groundStayTime, float fadeTime)
    {
        this.direction = dir;
        this.speed = speed;
        this.maxDistance = range;
        this.hitRadius = hitRadius;
        this.spinSpeed = spinSpeed;
        this.enemies = enemies;
        this.ignoreColliders = ignoreColliders;
        this.forwardSpeedWhenFalling = forwardSpeedWhenFalling;
        this.tumbleSpeed = tumbleSpeed;
        this.thickness = thickness;
        this.bounciness = bounciness;
        this.friction = friction;
        this.maxSettleTime = maxSettleTime;
        this.groundStayTime = groundStayTime;
        this.fadeTime = fadeTime;

        sr = GetComponent<SpriteRenderer>();
        killY = transform.position.y - 30f; // safety: no ground collider -> card is removed

        Transform g = transform.Find("Glow");
        if (g != null)
        {
            glowSr = g.GetComponent<SpriteRenderer>();
            if (glowSr != null) glowBaseAlpha = glowSr.color.a;
        }
        pulseOffset = Random.value * 10f;
    }

    public void SetGlowPulse(float speed, float amount)
    {
        pulseSpeed = speed;
        pulseAmount = amount;
    }

    private void Update()
    {
        switch (state)
        {
            case State.Flying: UpdateFlying(); break;
            case State.Falling: UpdateFalling(); break;
            case State.Landed: UpdateLanded(); break;
        }

        UpdateGlow();
    }

    private void UpdateGlow()
    {
        if (glowSr == null) return;

        float pulse = 1f + pulseAmount * Mathf.Sin(Time.time * pulseSpeed + pulseOffset);
        Color c = glowSr.color;
        c.a = Mathf.Clamp01(glowBaseAlpha * pulse * fade);
        glowSr.color = c;
    }

    // ---------------- Flying ----------------

    private void UpdateFlying()
    {
        float step = Mathf.Min(speed * Time.deltaTime, maxDistance - travelled);
        Vector3 start = transform.position;
        Vector3 end = start + direction * step;

        // Check the whole path travelled this frame so fast cards can't skip over an enemy
        foreach (EnemyChase enemy in enemies)
        {
            if (enemy == null || !enemy.isActiveAndEnabled) continue;

            if (FlatSegmentDistance(start, end, enemy.transform.position) <= hitRadius)
            {
                enemy.HitByCard();
                Destroy(gameObject);
                return;
            }
        }

        transform.position = end;
        travelled += step;

        // Face the camera and spin
        spinAngle += spinSpeed * Time.deltaTime;
        Camera cam = Camera.main;
        if (cam != null)
            transform.rotation = cam.transform.rotation * Quaternion.Euler(0f, 0f, spinAngle);

        if (travelled >= maxDistance - 0.001f)
            BeginFall();
    }

    // ---------------- Falling (physics) ----------------

    private void BeginFall()
    {
        state = State.Falling;

        // Thin box that matches the card sprite (sizes are in local space, the scale is applied by Unity)
        BoxCollider box = gameObject.AddComponent<BoxCollider>();
        if (sr != null && sr.sprite != null)
        {
            Vector3 s = sr.sprite.bounds.size;
            float localThickness = thickness / Mathf.Max(0.0001f, transform.lossyScale.z);
            box.size = new Vector3(s.x, s.y, localThickness);
            box.center = sr.sprite.bounds.center;
        }

        PhysicsMaterial mat = new PhysicsMaterial("CardMat");
        mat.bounciness = bounciness;
        mat.dynamicFriction = friction;
        mat.staticFriction = friction;
        mat.bounceCombine = PhysicsMaterialCombine.Maximum;
        mat.frictionCombine = PhysicsMaterialCombine.Maximum;
        box.material = mat;

        // Never collide with the player who threw it
        if (ignoreColliders != null)
        {
            foreach (Collider c in ignoreColliders)
                if (c != null) Physics.IgnoreCollision(box, c);
        }

        rb = gameObject.AddComponent<Rigidbody>();
        rb.mass = 0.05f;
        rb.useGravity = true;
        rb.linearDamping = 0.5f;
        rb.angularDamping = 1.5f;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        // Keeps a bit of the throw, and tumbles so it lands at a natural angle
        rb.linearVelocity = direction * (speed * forwardSpeedWhenFalling);
        Vector3 axis = Random.onUnitSphere;
        rb.angularVelocity = axis * (tumbleSpeed * Mathf.Deg2Rad);
    }

    private void OnCollisionEnter(Collision collision)
    {
        touchedGround = true;
    }

    private void UpdateFalling()
    {
        fallTimer += Time.deltaTime;

        if (transform.position.y < killY)
        {
            // Fell through the world: the ground has no collider
            Destroy(gameObject);
            return;
        }

        bool resting = touchedGround
                       && rb.linearVelocity.sqrMagnitude < 0.0025f
                       && rb.angularVelocity.sqrMagnitude < 0.04f;

        if (resting || fallTimer >= maxSettleTime)
        {
            state = State.Landed;
            landedTimer = 0f;
        }
    }

    // ---------------- Landed ----------------

    private void UpdateLanded()
    {
        landedTimer += Time.deltaTime;

        if (landedTimer > groundStayTime && fadeTime > 0f)
        {
            fade = 1f - Mathf.Clamp01((landedTimer - groundStayTime) / fadeTime);

            if (sr != null)
            {
                Color c = sr.color;
                c.a = fade;
                sr.color = c;
            }
        }

        if (landedTimer >= groundStayTime + fadeTime)
            Destroy(gameObject);
    }

    // Distance from point p to the segment a-b, ignoring height
    private static float FlatSegmentDistance(Vector3 a, Vector3 b, Vector3 p)
    {
        Vector2 A = new Vector2(a.x, a.z);
        Vector2 B = new Vector2(b.x, b.z);
        Vector2 P = new Vector2(p.x, p.z);

        Vector2 ab = B - A;
        float t = ab.sqrMagnitude < 0.000001f ? 0f : Mathf.Clamp01(Vector2.Dot(P - A, ab) / ab.sqrMagnitude);
        return Vector2.Distance(A + ab * t, P);
    }
}