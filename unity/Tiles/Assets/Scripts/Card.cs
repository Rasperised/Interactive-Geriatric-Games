using System.Collections;
using UnityEngine;

public class Card : MonoBehaviour
{
    [Header("Renderers (child objects named 'Front' and 'Back')")]
    public SpriteRenderer frontRenderer;
    public SpriteRenderer backRenderer;

    [Header("Animation settings")]
    [SerializeField] private float flipDuration = 0.25f;
    [SerializeField] private float pulseDuration = 0.25f;
    [SerializeField] private float pulseScale = 1.15f;

    private bool isFrontVisible = false;
    private Vector3 originalScale;
    private bool isFlipping = false;

    public int cardID { get; private set; }

    void Awake()
    {
        // Cache front/back renderers if not assigned in inspector
        if (frontRenderer == null)
            frontRenderer = transform.Find("Front")?.GetComponent<SpriteRenderer>();

        if (backRenderer == null)
            backRenderer = transform.Find("Back")?.GetComponent<SpriteRenderer>();

        if (frontRenderer == null || backRenderer == null)
            Debug.LogWarning($"Card '{name}' missing Front/Back SpriteRenderer children.");
    }

    // Deterministic initialization called by GameController after it sets position/scale
    public void Initialize(Sprite frontSprite, int id)
    {
        if (frontRenderer != null)
            frontRenderer.sprite = frontSprite;

        cardID = id;

        // Capture the current localScale as the base for animations
        CaptureOriginalScale();

        // Start face-down by default
        ShowBackInstant();
    }

    // Call after GameController sets transform.localScale so animations use correct base size
    public void CaptureOriginalScale()
    {
        originalScale = transform.localScale;
    }

    // Public API used by GameController
    public bool IsRevealed() => isFrontVisible;

    public void ShowFront() => StartCoroutine(FlipAnimation(showFront: true));

    public void ShowBack() => StartCoroutine(FlipAnimation(showFront: false));

    public void ShowFrontInstant()
    {
        if (frontRenderer != null) frontRenderer.enabled = true;
        if (backRenderer != null) backRenderer.enabled = false;
        isFrontVisible = true;
        isFlipping = false;
        transform.localScale = originalScale;
    }

    public void ShowBackInstant()
    {
        if (frontRenderer != null) frontRenderer.enabled = false;
        if (backRenderer != null) backRenderer.enabled = true;
        isFrontVisible = false;
        isFlipping = false;
        transform.localScale = originalScale;
    }

    // Check whether the given world-space point lies inside this card.
    // Uses sprite geometry when available; falls back to SpriteRenderer.bounds or originalScale.
    public bool ContainsPoint(Vector2 worldPoint)
    {
        // Prefer precise sprite rect test when sprite is present
        if (frontRenderer != null && frontRenderer.sprite != null)
        {
            // Convert world point to local space of the card
            Vector3 localPoint3 = transform.InverseTransformPoint(worldPoint);
            Vector2 localPoint = new Vector2(localPoint3.x, localPoint3.y);

            Sprite sprite = frontRenderer.sprite;
            Rect rect = sprite.rect; // pixels
            Vector2 pivot = sprite.pivot; // pixels
            float ppu = sprite.pixelsPerUnit;

            // sprite size in local units (before scale)
            float spriteW = rect.width / ppu;
            float spriteH = rect.height / ppu;

            // pivot offset in local units (centered coordinate)
            Vector2 pivotOffset = new Vector2(
                (pivot.x / rect.width - 0.5f) * spriteW,
                (pivot.y / rect.height - 0.5f) * spriteH
            );

            // account for object localScale
            float halfW = spriteW * 0.5f * transform.localScale.x;
            float halfH = spriteH * 0.5f * transform.localScale.y;

            Vector2 localRelative = new Vector2(localPoint.x - pivotOffset.x, localPoint.y - pivotOffset.y);

            return localRelative.x >= -halfW && localRelative.x <= halfW
                && localRelative.y >= -halfH && localRelative.y <= halfH;
        }

        // If no sprite, try SpriteRenderer.bounds (world AABB)
        if (frontRenderer != null)
        {
            Bounds b = frontRenderer.bounds;
            return b.Contains(new Vector3(worldPoint.x, worldPoint.y, b.center.z));
        }

        // Fallback: use transform.localScale as size centered on transform.position (assume unit sprite)
        Vector3 pos = transform.position;
        float halfX = originalScale.x * 0.5f;
        float halfY = originalScale.y * 0.5f;
        return worldPoint.x >= pos.x - halfX && worldPoint.x <= pos.x + halfX
            && worldPoint.y >= pos.y - halfY && worldPoint.y <= pos.y + halfY;
    }

    IEnumerator FlipAnimation(bool showFront)
    {
        if (isFlipping) yield break;
        isFlipping = true;

        float duration = Mathf.Max(0.001f, flipDuration);
        float t = 0f;

        // Shrink (flip in)
        while (t < duration)
        {
            t += Time.deltaTime;
            float scaleX = Mathf.Lerp(originalScale.x, 0f, t / duration);
            transform.localScale = new Vector3(scaleX, originalScale.y, originalScale.z);
            yield return null;
        }

        // Swap visible side
        if (frontRenderer != null) frontRenderer.enabled = showFront;
        if (backRenderer != null) backRenderer.enabled = !showFront;
        isFrontVisible = showFront;

        // Expand (flip out)
        t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float scaleX = Mathf.Lerp(0f, originalScale.x, t / duration);
            transform.localScale = new Vector3(scaleX, originalScale.y, originalScale.z);
            yield return null;
        }

        transform.localScale = originalScale;
        isFlipping = false;
    }

    // 🔹 Pulse animation for matched cards
    public IEnumerator MatchPulse()
    {
        float duration = Mathf.Max(0.001f, pulseDuration);
        float maxScale = originalScale.x * Mathf.Max(1f, pulseScale);
        float t = 0f;

        // Enlarge slightly
        while (t < duration)
        {
            t += Time.deltaTime;
            float s = Mathf.Lerp(originalScale.x, maxScale, t / duration);
            transform.localScale = new Vector3(s, s, originalScale.z);
            yield return null;
        }

        // Shrink back to normal
        t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float s = Mathf.Lerp(maxScale, originalScale.x, t / duration);
            transform.localScale = new Vector3(s, s, originalScale.z);
            yield return null;
        }

        transform.localScale = originalScale;
    }
}