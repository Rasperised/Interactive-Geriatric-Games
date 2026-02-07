using UnityEngine;

public class TestController : MonoBehaviour
{
    public float minY = -3f;
    public float maxY = 3f;
    public float speed = 3f;

    public GameController gameController; // Add reference to game controller

    private int blockedDirection = 0;
    //  0 = no block
    //  1 = block UP
    // -1 = block DOWN

    void Update()
    {
        // Don't accept input if game has ended
        if (gameController != null)
        {
            if (gameController.IsGameEnded())
            {
                Debug.Log("Game ended - blocking input"); // Debug to verify this is called
                return;
            }
        }
        else
        {
            Debug.LogWarning("GameController reference is missing on TestController!"); // Warning if not assigned
        }

        float input = Input.GetAxis("Vertical"); // W/S or ↑↓

        // Block movement INTO the target
        if ((blockedDirection == 1 && input > 0f) ||
            (blockedDirection == -1 && input < 0f))
        {
            input = 0f;
        }

        Vector3 pos = transform.position;
        pos.y += input * speed * Time.deltaTime;
        pos.y = Mathf.Clamp(pos.y, minY, maxY);
        transform.position = pos;
    }

    // Called when a target is hit
    public void SnapAndBlock(float snapY, int blockDir)
    {
        Vector3 pos = transform.position;
        pos.y = snapY;
        transform.position = pos;
        blockedDirection = blockDir;
    }

    // Called when leaving the target
    public void ClearBlock()
    {
        blockedDirection = 0;
    }

    // NEW: Force position (for physical blocking)
    public void ForcePosition(float yPos)
    {
        Vector3 pos = transform.position;
        pos.y = yPos;
        transform.position = pos;
    }

    // NEW: Set block without snapping
    public void SetBlock(int blockDir)
    {
        blockedDirection = blockDir;
    }
}