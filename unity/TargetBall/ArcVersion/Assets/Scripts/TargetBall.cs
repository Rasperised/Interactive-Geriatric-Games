using UnityEngine;
using static GameController;

public class TargetBall : MonoBehaviour
{
    public GameController gameController;
    public AudioSource hitSound;
    public TargetSide mySide;

    [Header("Visual Effects")]
    public GameObject hitEffectPrefab; // Drag your HitEffect prefab here
    public Color effectColor = Color.yellow; // Color for this target's effect

    private float lastHitTime = -999f;
    private float hitCooldown = 0.5f;

    private void OnCollisionEnter(Collision collision)
    {
        Debug.Log($"Collision with {collision.gameObject.name} on {mySide}");

        if (!collision.gameObject.CompareTag("Player")) return;
        if (gameController == null)
        {
            Debug.LogError("GameController not assigned!");
            return;
        }
        if (gameController.IsGameEnded()) return;

        // Cooldown check
        if (Time.time - lastHitTime < hitCooldown)
        {
            Debug.Log("Hit rejected - cooldown");
            return;
        }

        // Check if allowed to hit this target
        if (!gameController.CanHitTarget(mySide))
        {
            Debug.Log($"Hit rejected - can't hit {mySide} right now");
            return;
        }

        Debug.Log($"HIT ACCEPTED on {mySide}!");

        lastHitTime = Time.time;
        gameController.RegisterHit(mySide);

        // Play hit effect
        SpawnHitEffect(collision.contacts[0].point);

        if (hitSound != null)
            hitSound.Play();
    }

    void SpawnHitEffect(Vector3 position)
    {
        Debug.Log($"Spawning effect at {position}"); // ADD THIS

        if (hitEffectPrefab == null)
        {
            Debug.LogError("Hit Effect Prefab is NULL!"); // ADD THIS
            return;
        }

        // Spawn the effect at collision point
        GameObject effect = Instantiate(hitEffectPrefab, position, Quaternion.identity);

        Debug.Log($"Effect spawned: {effect.name}"); // ADD THIS

        // Set the color
        ParticleSystem ps = effect.GetComponent<ParticleSystem>();
        if (ps != null)
        {
            var main = ps.main;
            main.startColor = effectColor;
            ps.Play(); // ADD THIS - Make sure it plays!
        }
        else
        {
            Debug.LogError("ParticleSystem not found on effect!"); // ADD THIS
        }

        // Destroy after 2 seconds
        Destroy(effect, 2f);
    }
}