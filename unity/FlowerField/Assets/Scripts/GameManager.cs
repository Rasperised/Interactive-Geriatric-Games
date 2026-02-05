using UnityEngine;

public class GameManager : MonoBehaviour
{
    public GameObject[] flowerPrefabs;
    public float spawnInterval = 0.02f;

    private float spawnTimer = 0f;
    private bool isDragging = false;

    public float[] sensorValues = new float[5];

    // spacing control
    private float lastSpawnX = float.NaN;
    public float minSpacing = 0.5f;

    // ---- NEW: jitter amount ----
    public float jitterAmountX = 0.3f;  // randomness in X
    public float jitterAmountY = 0.3f;  // randomness in Y

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
            isDragging = true;
        if (Input.GetMouseButtonUp(0))
            isDragging = false;

        spawnTimer += Time.deltaTime;

        if (spawnTimer >= spawnInterval)
        {
            if (isDragging)
                SpawnFlowerAtMouse();

            SpawnFlowerUsingHandTracking();
            spawnTimer = 0f;
        }
    }

    // ---------------- FULL-SCREEN HAND TRACKING ----------------
    void SpawnFlowerUsingHandTracking()
    {
        float totalWeight = 0f;
        float weightedIndex = 0f;  // 0 → 4
        float minDistance = 999f;  // for Y

        for (int i = 0; i < 5; i++)
        {
            float d = sensorValues[i];

            if (d > 0f && d < 35f)
            {
                float w = 1f / Mathf.Max(d, 1f);
                totalWeight += w;

                weightedIndex += w * i;  // X index

                if (d < minDistance)
                    minDistance = d;
            }
        }

        if (totalWeight == 0f)
            return;

        // ------------------ MAP X TO FULL SCREEN ------------------
        float normalizedX = (weightedIndex / totalWeight) / 4f;

        float screenMinX = Camera.main.ViewportToWorldPoint(new Vector3(0, 0.5f, 10)).x;
        float screenMaxX = Camera.main.ViewportToWorldPoint(new Vector3(1, 0.5f, 10)).x;

        float handX = Mathf.Lerp(screenMinX, screenMaxX, normalizedX);

        // ------------------ MAP Y TO FULL SCREEN ------------------
        float normalizedY = Mathf.InverseLerp(5f, 35f, minDistance);

        float screenMinY = Camera.main.ViewportToWorldPoint(new Vector3(0.5f, 0, 10)).y;
        float screenMaxY = Camera.main.ViewportToWorldPoint(new Vector3(0.5f, 1, 10)).y;

        float handY = Mathf.Lerp(screenMinY, screenMaxY, normalizedY);

        // ------------------ SPACING CONTROL ------------------
        if (!float.IsNaN(lastSpawnX) && Mathf.Abs(handX - lastSpawnX) < minSpacing)
        {
            // Still spawn, but jitter prevents straight lines
            // DO NOT return
        }

        lastSpawnX = handX;

        // ------------------ NEW: JITTER HERE ------------------
        float jitterX = Random.Range(-jitterAmountX, jitterAmountX);
        float jitterY = Random.Range(-jitterAmountY, jitterAmountY);

        Vector3 pos = new Vector3(handX + jitterX, handY + jitterY, 0f);
        // ------------------------------------------------------

        SpawnFlower(pos);
    }

    void SpawnFlower(Vector3 pos)
    {
        int index = Random.Range(0, flowerPrefabs.Length);
        Instantiate(flowerPrefabs[index], pos, Quaternion.identity);
    }

    void SpawnFlowerAtMouse()
    {
        Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mousePos.z = 0f;
        SpawnFlower(mousePos);
    }

    public void UpdateSensorInput(float[] values)
    {
        for (int i = 0; i < 5; i++)
            sensorValues[i] = values[i];
    }
}
