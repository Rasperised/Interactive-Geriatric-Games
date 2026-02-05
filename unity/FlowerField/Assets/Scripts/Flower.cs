using UnityEngine;

public class Flower : MonoBehaviour
{
    public float lifetime = 2f;      // How long flower stays visible
    public float fadeDuration = 1f;  // How long it takes to fade away

    private SpriteRenderer sr;
    private float timer = 0f;

    void Start()
    {
        sr = GetComponent<SpriteRenderer>();
    }

    void Update()
    {
        timer += Time.deltaTime;

        // Start fading once we are near the end of the lifetime
        if (timer >= lifetime)
        {
            float fadeProgress = (timer - lifetime) / fadeDuration;
            Color color = sr.color;
            color.a = Mathf.Lerp(1f, 0f, fadeProgress);
            sr.color = color;

            if (fadeProgress >= 1f)
                Destroy(gameObject);
        }
    }
}
