using UnityEngine;
using System;   // needed for the Action event

public class Bird : MonoBehaviour
{
    [Header("Movement Settings")]
    public float smoothFollowSpeed = 5f;
    private bool isAlive = true;

    // Event that GameController listens to
    public event Action OnBirdHitPipe;

    // Called every frame by GameController to move the bird toward the mouse
    public void MoveTo(Vector3 targetPosition)
    {
        if (!isAlive) return;

        targetPosition.z = 0f;
        transform.position = Vector3.Lerp(
            transform.position,
            targetPosition,
            Time.deltaTime * smoothFollowSpeed
        );
    }

    // Detect collision with pipes
    private void OnTriggerEnter2D(Collider2D collision)
    {
        Transform root = collision.transform.root;
        if (root.CompareTag("Pipe"))
        {
            Debug.Log("Bird hit a pipe!");
            isAlive = false;

            // Notify GameController
            OnBirdHitPipe?.Invoke();
        }
    }

    // Reset function for reuse after restart
    public void ResetBird()
    {
        isAlive = true;
    }
}
