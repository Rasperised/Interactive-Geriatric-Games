using UnityEngine;

public class Ball : MonoBehaviour
{
    private Rigidbody rb;
    private int paddleHitCount = 0;
    private float lastRecordedSpeed;  // Track true ongoing speed
    private Vector3 previousPosition; // Track position for collision detection

    [Header("Bounce Settings")]
    [Tooltip("Prevents purely horizontal movement after wall bounces.")]
    public float minVerticalBounce = 0.25f;

    [Tooltip("How close to the paddle edge counts as an edge hit (0–1).")]
    [Range(0f, 1f)]
    public float edgeThreshold = 0.6f;

    [Tooltip("Separation offset to prevent sticking after collisions.")]
    public float wallSeparationOffset = 0.05f;

    [Tooltip("Default ball speed after scoring.")]
    public float baseSpeed = 6f;

    [Tooltip("Maximum allowed speed to prevent phasing through objects.")]
    public float maxSafeSpeed = 15f;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;

#if UNITY_6000_0_OR_NEWER
        rb.linearDamping = 0f;
        rb.angularDamping = 0f;
#else
        rb.drag = 0f;
        rb.angularDrag = 0f;
#endif

        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation;

        lastRecordedSpeed = baseSpeed;
        previousPosition = transform.position;
        Debug.Log("[Ball] Initialized. Constant speed mode active.");
    }

    void FixedUpdate()
    {
        // Keep ball at consistent speed during movement
        float currentSpeed = rb.linearVelocity.magnitude;
        if (currentSpeed > 0f)
        {
            float targetSpeed = Mathf.Min(
                Mathf.Max(currentSpeed, lastRecordedSpeed, baseSpeed),
                maxSafeSpeed
            );

            rb.linearVelocity = rb.linearVelocity.normalized * targetSpeed;
            lastRecordedSpeed = targetSpeed;
        }

        previousPosition = transform.position;
    }

    void OnCollisionEnter(Collision collision)
    {
        Debug.Log($"[Ball] Collided with {collision.gameObject.name} (Tag: {collision.gameObject.tag})");

        // ============================================================
        // --- WALL BOUNCE ---
        // ============================================================
        if (collision.gameObject.CompareTag("Wall"))
        {
            AudioManager.Instance.PlaySFX(AudioManager.Instance.wallHit);

            Vector3 vel = rb.linearVelocity;
            float preCollisionSpeed = Mathf.Max(vel.magnitude, lastRecordedSpeed, baseSpeed);

            bool isTopWall = collision.transform.position.y > transform.position.y;
            bool isBottomWall = collision.transform.position.y < transform.position.y;

            if (isTopWall || isBottomWall)
            {
                vel.y = -vel.y;

                float minY = minVerticalBounce * 4.0f;
                if (Mathf.Abs(vel.y) < minY)
                {
                    float bounceDirection = isTopWall ? -1f : 1f;
                    vel.y = bounceDirection * (minY + Random.Range(0.2f, 0.6f));
                    Debug.Log($"[Ball] Shallow bounce corrected. New Y: {vel.y:F2}");
                }
            }
            else
            {
                vel.x = -vel.x;
            }

            vel.y *= Random.Range(0.95f, 1.05f);
            vel.z = 0f;

            float newSpeed = Mathf.Min(preCollisionSpeed, maxSafeSpeed);
            rb.linearVelocity = vel.normalized * newSpeed;
            lastRecordedSpeed = newSpeed;

            if (collision.contacts.Length > 0)
            {
                Vector3 separationDirection =
                    isTopWall ? Vector3.down :
                    isBottomWall ? Vector3.up :
                    collision.contacts[0].normal;

                transform.position += separationDirection * wallSeparationOffset;
            }

            Debug.Log($"[Ball] Wall bounce → Speed preserved: {rb.linearVelocity.magnitude:F2}");
            return;
        }

        // ============================================================
        // --- PADDLE BOUNCE ---
        // ============================================================
        if (collision.gameObject.CompareTag("Paddle"))
        {
            AudioManager.Instance.PlaySFX(AudioManager.Instance.paddleHit);

            paddleHitCount++;
            Debug.Log($"[Ball] PADDLE hit #{paddleHitCount}");

            float outX = Mathf.Sign(transform.position.x - collision.transform.position.x);

            float offset = transform.position.y - collision.transform.position.y;
            float halfHeight = collision.collider.bounds.extents.y;

            float normalizedOffset = (halfHeight > 0f)
                ? Mathf.Clamp(offset / halfHeight, -1f, 1f)
                : 0f;

            float curvedOffset =
                Mathf.Sign(normalizedOffset) * normalizedOffset * normalizedOffset;

            Vector3 outDir = new Vector3(outX, curvedOffset, 0f);
            outDir.Normalize();

            bool isEdgeHit = Mathf.Abs(normalizedOffset) >= edgeThreshold;

            if (isEdgeHit && Mathf.Abs(outDir.y) < minVerticalBounce)
            {
                outDir.y = minVerticalBounce *
                    Mathf.Sign(outDir.y == 0f ? normalizedOffset : outDir.y);
                outDir.Normalize();

                Debug.Log("[Ball] Edge hit detected → enforcing vertical escape");
            }

            float speed = Mathf.Max(lastRecordedSpeed, rb.linearVelocity.magnitude, baseSpeed);

            if (GameController.Instance != null &&
                paddleHitCount % GameController.Instance.hitsBeforeSpeedIncrease == 0)
            {
                speed *= GameController.Instance.ballSpeedMultiplier;
                speed = Mathf.Min(speed, Mathf.Min(GameController.Instance.maxBallSpeed, maxSafeSpeed));
                Debug.Log($"[Ball] Speed increased to {speed:F2}");
            }

            speed = Mathf.Min(speed, maxSafeSpeed);

            rb.linearVelocity = outDir * speed;
            lastRecordedSpeed = speed;

            transform.position += new Vector3(outX * 0.12f, 0f, 0f);

            Debug.Log($"[Ball] Paddle bounce → Speed maintained: {rb.linearVelocity.magnitude:F2}");
        }
    }

    // ============================================================
    // --- MANUAL COLLISION DETECTION FOR HIGH SPEEDS ---
    // ============================================================
    void Update()
    {
        if (rb.linearVelocity.magnitude > baseSpeed * 1.5f)
            CheckForPaddleCollision();
    }

    private void CheckForPaddleCollision()
    {
        float checkDistance = rb.linearVelocity.magnitude * Time.deltaTime + 0.5f;
        RaycastHit hit;

        if (Physics.Raycast(previousPosition, rb.linearVelocity.normalized, out hit, checkDistance))
        {
            if (hit.collider.CompareTag("Paddle"))
            {
                Debug.LogWarning("[Ball] Manual paddle collision detected!");

                rb.linearVelocity =
                    new Vector3(-rb.linearVelocity.x, rb.linearVelocity.y, 0f).normalized *
                    rb.linearVelocity.magnitude;

                transform.position = hit.point + hit.normal * 0.2f;
            }
        }
    }

    // ============================================================
    // --- RESET SPEED (called from GameController after scoring)
    // ============================================================
    public void ResetSpeedToBase()
    {
        lastRecordedSpeed = baseSpeed;
        if (rb != null && rb.linearVelocity != Vector3.zero)
        {
            rb.linearVelocity = rb.linearVelocity.normalized * baseSpeed;
        }
        Debug.Log("[Ball] Speed reset to base after scoring.");
    }
}
