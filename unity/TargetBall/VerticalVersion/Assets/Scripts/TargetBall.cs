using UnityEngine;

public class TargetBall : MonoBehaviour
{
    public GameController gameController;
    public AudioSource hitSound;
    public GameController.TargetSide mySide;

    [Header("Visual Effects")]
    public GameObject hitEffectPrefab;
    public Color effectColor = Color.yellow;

    [Header("Collision Settings")]
    public float playerRadius = 0.5f;
    public float targetRadius = 0.5f;

    private bool isActive = true;
    private float lastHitTime = -999f;
    private float hitCooldown = 0.2f;
    private Collider myCollider;
    private Renderer myRenderer;

    void Start()
    {
        myCollider = GetComponent<Collider>();
        myRenderer = GetComponent<Renderer>();
    }

    void Update()
    {
        // Reactivate this target only when the other target was hit
        if (!isActive && gameController != null)
        {
            if (gameController.CanHitTarget(mySide))
            {
                isActive = true;
                UpdateVisuals();
                Debug.Log($"{mySide} target reactivated");
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        TryHit(other);
    }

    private void OnTriggerStay(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        // Continuously enforce blocking on inactive targets
        if (!isActive)
        {
            EnforceBlock(other);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        PlayerBallVerticalController controller = other.GetComponent<PlayerBallVerticalController>();
        if (controller != null)
        {
            controller.ClearBlock();
        }
    }

    private void TryHit(Collider other)
    {
        if (!isActive) return;
        if (gameController == null) return;
        if (gameController.IsGameEnded()) return;
        if (Time.time - lastHitTime < hitCooldown) return;
        if (!gameController.CanHitTarget(mySide)) return;

        lastHitTime = Time.time;
        isActive = false;
        UpdateVisuals();

        Debug.Log($"{mySide} target HIT!");

        gameController.RegisterHit(mySide);

        PlayerBallVerticalController controller = other.GetComponent<PlayerBallVerticalController>();
        if (controller != null)
        {
            float snapOffset = playerRadius + targetRadius;
            float snapY = transform.position.y;
            int blockDir = 0;

            if (mySide == GameController.TargetSide.Top)
            {
                snapY -= snapOffset;
                blockDir = 1; // block upward
            }
            else if (mySide == GameController.TargetSide.Bottom)
            {
                snapY += snapOffset;
                blockDir = -1; // block downward
            }

            controller.SetBlock(snapY, blockDir);
        }

        SpawnHitEffect(other.ClosestPoint(transform.position));

        if (hitSound != null)
            hitSound.Play();
    }

    private void EnforceBlock(Collider other)
    {
        PlayerBallVerticalController controller = other.GetComponent<PlayerBallVerticalController>();
        if (controller == null) return;

        Vector3 playerPos = other.transform.position;
        Vector3 targetPos = transform.position;
        float threshold = playerRadius + targetRadius;

        if (mySide == GameController.TargetSide.Top)
        {
            float blockY = targetPos.y - threshold;
            if (playerPos.y > blockY - 0.1f) // Small tolerance
            {
                controller.SetBlock(blockY, 1); // block UP
            }
        }
        else if (mySide == GameController.TargetSide.Bottom)
        {
            float blockY = targetPos.y + threshold;
            if (playerPos.y < blockY + 0.1f) // Small tolerance
            {
                controller.SetBlock(blockY, -1); // block DOWN
            }
        }
    }

    void UpdateVisuals()
    {
        if (myRenderer != null)
        {
            Color c = myRenderer.material.color;
            c.a = isActive ? 1f : 0.5f;
            myRenderer.material.color = c;
        }
    }

    void SpawnHitEffect(Vector3 position)
    {
        if (hitEffectPrefab == null) return;

        GameObject effect = Instantiate(hitEffectPrefab, position, Quaternion.identity);
        ParticleSystem ps = effect.GetComponent<ParticleSystem>();
        if (ps != null)
        {
            var main = ps.main;
            main.startColor = effectColor;
            ps.Play();
        }

        Destroy(effect, 2f);
    }
}