using System;
using System.Collections;
using UnityEngine;

public class Tile : MonoBehaviour
{
    [Header("Renderers")]
    public SpriteRenderer frontRenderer;
    public SpriteRenderer shadowRenderer;

    [Header("Visual settings")]
    [SerializeField] private float snapDuration = 0.18f;
    [SerializeField] private float returnDuration = 0.25f;
    [SerializeField] private float rotateLerpSpeed = 12f;
    [SerializeField] private float dragScale = 1.05f;

    [Header("Sorting")]
    [SerializeField] private int baseSortingOrder = 0;
    [SerializeField] private int dragSortingOrder = 200;

    [Header("Correct Placement Effect")]
    [SerializeField] private GameObject starEffectPrefab;
    [SerializeField] private int starCount = 5;
    [SerializeField] private float starRadius = 0.25f;

    // Identity & state
    public int TileID { get; private set; }
    public bool IsPlaced { get; private set; } = false;
    public Slot CurrentSlot { get; private set; } = null;
    public bool IsLocked { get; private set; } = false;

    // Original transform values for return animations
    private Vector3 originalPosition;
    private Quaternion originalRotation;
    private Vector3 originalScale;

    // Anchor (home) position for this tile (world space)
    private Vector3 anchorPosition;

    // Running animation coroutine
    private Coroutine runningCoroutine;

    // Selection debug
    private bool isSelected = false;
    private Color selectionColor = Color.yellow;

    // Auto-assign common renderer references early to avoid missing inspector bindings
    void Awake()
    {
        // Try to find a child named "Front" first
        if (frontRenderer == null)
        {
            var found = transform.Find("Front")?.GetComponent<SpriteRenderer>();
            if (found != null) frontRenderer = found;
        }

        // If still null, try a SpriteRenderer on the same GameObject
        if (frontRenderer == null)
            frontRenderer = GetComponent<SpriteRenderer>();

        // If still null, grab the first child SpriteRenderer as a last resort
        if (frontRenderer == null)
            frontRenderer = GetComponentInChildren<SpriteRenderer>();

        // Shadow by convention (child named "Shadow")
        if (shadowRenderer == null)
            shadowRenderer = transform.Find("Shadow")?.GetComponent<SpriteRenderer>();
    }

    // Initialize tile with sprite and id (called by GameController)
    public void Initialize(Sprite sprite, int id)
    {
        TileID = id;

        // assign sprite to frontRenderer; if null, try to locate again and warn
        if (frontRenderer != null)
        {
            frontRenderer.sprite = sprite;
        }
        else
        {
            // Try to find any SpriteRenderer in children and use it
            var sr = GetComponentInChildren<SpriteRenderer>();
            if (sr != null)
            {
                frontRenderer = sr;
                frontRenderer.sprite = sprite;
                Debug.LogWarning($"{name}: frontRenderer was null in inspector; assigned first child SpriteRenderer '{sr.name}'.");
            }
            else
            {
                Debug.LogWarning($"{name}: No SpriteRenderer found to assign tile sprite (TileID={id}). Make sure TilePrefab has a 'Front' SpriteRenderer and it's assigned to Tile.frontRenderer.");
            }
        }

        // Capture the current transform values after the GameController sets scale/position
        CaptureOriginalTransform();

        // ensure placed state is false initially
        IsPlaced = false;
    }

    // Capture transform values after GameController sets transform/localScale
    public void CaptureOriginalTransform()
    {
        originalPosition = transform.position;
        originalRotation = transform.rotation;
        originalScale = transform.localScale;
        SetSortingOrder(baseSortingOrder);
    }

    // Anchor setter and accessor
    public void SetAnchorPosition(Vector3 pos)
    {
        anchorPosition = pos;
    }
    public Vector3 AnchorPosition => anchorPosition;

    // Animate tile back to its anchor position
    public void MoveToAnchorAnimated(float duration, Action onComplete = null)
    {
        MoveToPositionAnimated(anchorPosition, 0f, duration, onComplete, captureAtEnd: false);
    }

    // Animate tile back to its original (exploded) position captured earlier
    public void MoveToOriginalPositionAnimated(float duration, Action onComplete = null)
    {
        // IMPORTANT: do not recapture originalPosition after moving back
        MoveToPositionAnimated(originalPosition, 0f, duration, onComplete, captureAtEnd: false);
    }

    // Public API used by GameController while dragging
    public void OnBeginDrag()
    {
        if (IsLocked) return; // 🚫 do nothing if locked

        CancelRunningCoroutine();
        SetSortingOrder(dragSortingOrder);
        transform.localScale = originalScale * dragScale;
        IsPlaced = false;

        if (CurrentSlot != null)
        {
            CurrentSlot.ClearOccupant();
            CurrentSlot = null;
        }
    }

    public void OnDragUpdate(Vector2 worldPos)
    {
        transform.position = new Vector3(worldPos.x, worldPos.y, transform.position.z);
        // Gradually rotate toward zero while dragging
        float z = Mathf.LerpAngle(transform.eulerAngles.z, 0f, Time.deltaTime * rotateLerpSpeed);
        transform.rotation = Quaternion.Euler(0f, 0f, z);
    }

    // Called by GameController when drop decision is to snap to a slot
    public void SnapToSlot(Slot slot, Action onComplete = null)
    {
        CancelRunningCoroutine();
        runningCoroutine = StartCoroutine(SnapToSlotCoroutine(slot, snapDuration, onComplete));
    }

    // Called by GameController to return tile to its original position
    public void ReturnToOrigin(Action onComplete = null)
    {
        CancelRunningCoroutine();
        runningCoroutine = StartCoroutine(ReturnToOriginCoroutine(returnDuration, onComplete));
    }
    public void LockCorrectlyPlaced()
    {
        IsLocked = true;
        IsPlaced = true;

        // Visual feedback
        StartCoroutine(CorrectPlacementFeedback());

        PlayStarEffect();

        // Audio feedback
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayPlaceCorrectSound();
    }

    // Star Effect
    void PlayStarEffect()
    {
        if (starEffectPrefab == null) return;

        for (int i = 0; i < starCount; i++)
        {
            float angle = i * (360f / starCount);
            Vector2 dir = Quaternion.Euler(0, 0, angle) * Vector2.up;

            Vector3 spawnPos = transform.position + (Vector3)(dir * starRadius);

            GameObject star = Instantiate(starEffectPrefab, spawnPos, Quaternion.identity);
            StartCoroutine(AnimateStar(star, dir));
        }
    }

    // Simple pulse for celebration
    public void Pulse(float duration = 0.35f)
    {
        CancelRunningCoroutine();
        runningCoroutine = StartCoroutine(PulseCoroutine(duration));
    }

    // Allow external code to force rotation to zero smoothly
    public void RotateToZero(float duration = 0.18f, Action onComplete = null)
    {
        CancelRunningCoroutine();
        runningCoroutine = StartCoroutine(RotateToZeroCoroutine(duration, onComplete));
    }

    // New: stop any running animations on this Tile
    public void StopAllAnimations()
    {
        CancelRunningCoroutine();
    }

    // New: animate this tile to an arbitrary world position/rotation, then optionally mark it unplaced and optionally capture transform
    // captureAtEnd = false prevents overwriting originalPosition (useful when returning to exploded pos)
    public void MoveToPositionAnimated(Vector3 targetPos, float targetZRot, float duration, Action onComplete = null, bool captureAtEnd = true)
    {
        CancelRunningCoroutine();
        runningCoroutine = StartCoroutine(ForceMoveCoroutine(targetPos, targetZRot, duration, onComplete, captureAtEnd));
    }

    IEnumerator ForceMoveCoroutine(Vector3 targetPos, float targetZRot, float duration, Action onComplete, bool captureAtEnd)
    {
        // Immediately clear current slot so slot ownership is consistent (prevents double-occupancy)
        if (CurrentSlot != null)
        {
            CurrentSlot.ClearOccupant();
            CurrentSlot = null;
            IsPlaced = false;
        }

        Vector3 startPos = transform.position;
        Quaternion startRot = transform.rotation;
        Quaternion targetRot = Quaternion.Euler(0f, 0f, targetZRot);
        float t = 0f;
        float d = Mathf.Max(0.001f, duration);
        while (t < d)
        {
            t += Time.deltaTime;
            float a = Mathf.Clamp01(t / d);
            transform.position = Vector3.Lerp(startPos, targetPos, a);
            transform.rotation = Quaternion.Slerp(startRot, targetRot, a);
            yield return null;
        }

        transform.position = targetPos;
        transform.rotation = targetRot;

        // mark unplaced; slot already cleared above
        CurrentSlot = null;
        IsPlaced = false;

        // Capture this position as its new "origin" for future ReturnToOrigin calls only if requested
        if (captureAtEnd)
            CaptureOriginalTransform();

        runningCoroutine = null;
        onComplete?.Invoke();
    }

    // Public API to set selection state (used by GameController)
    public void SetSelected(bool selected)
    {
        isSelected = selected;
    }

    // Check whether world point is inside this tile (sprite-aware)
    public bool ContainsPoint(Vector2 worldPoint)
    {
        if (frontRenderer != null && frontRenderer.sprite != null)
        {
            // Project the clicked world point onto the renderer's plane (z) so we test against the same plane
            float rendererZ = frontRenderer.bounds.center.z;
            Vector3 worldPoint3 = new Vector3(worldPoint.x, worldPoint.y, rendererZ);

            // Convert into the tile's local space (this accounts for parent transforms & rotation)
            Vector3 localPoint3 = transform.InverseTransformPoint(worldPoint3);
            Vector2 localPoint = new Vector2(localPoint3.x, localPoint3.y);

            Sprite sprite = frontRenderer.sprite;
            Rect rect = sprite.rect;
            Vector2 pivot = sprite.pivot;
            float ppu = sprite.pixelsPerUnit;

            // sprite size in LOCAL units (before transform.localScale)
            float spriteW = rect.width / ppu;
            float spriteH = rect.height / ppu;

            // pivot offset in LOCAL units (same space as localPoint)
            Vector2 pivotOffset = new Vector2(
                (pivot.x / rect.width - 0.5f) * spriteW,
                (pivot.y / rect.height - 0.5f) * spriteH
            );

            // half extents in LOCAL space (do NOT multiply by transform.localScale here)
            float halfW_local = spriteW * 0.5f;
            float halfH_local = spriteH * 0.5f;

            // local point relative to sprite center (LOCAL SPACE)
            Vector2 localRelative = new Vector2(localPoint.x - pivotOffset.x, localPoint.y - pivotOffset.y);

            bool hit = localRelative.x >= -halfW_local && localRelative.x <= halfW_local
                     && localRelative.y >= -halfH_local && localRelative.y <= halfH_local;

            // Diagnostic logging (keeps previous behavior: warns only on mismatch where bounds says inside but sprite test says outside)
            bool insideBounds = frontRenderer.bounds.Contains(new Vector3(worldPoint.x, worldPoint.y, frontRenderer.bounds.center.z));
            if (!hit && insideBounds)
            {
                Debug.LogWarning($"{name}.ContainsPoint mismatch: worldPoint={worldPoint} worldPoint3.z={worldPoint3.z} rendererZ={rendererZ} localPoint3={localPoint3} localRel={localRelative} halfW_local={halfW_local:F3} halfH_local={halfH_local:F3} bounds={frontRenderer.bounds.size}");
            }

            return hit;
        }

        // Fallback to renderer bounds (authoritative)
        if (frontRenderer != null)
        {
            Bounds b = frontRenderer.bounds;
            return b.Contains(new Vector3(worldPoint.x, worldPoint.y, b.center.z));
        }

        // Final fallback: axis-aligned box from originalScale
        Vector3 pos = transform.position;
        float halfX = originalScale.x * 0.5f;
        float halfY = originalScale.y * 0.5f;
        return worldPoint.x >= pos.x - halfX && worldPoint.x <= pos.x + halfX
            && worldPoint.y >= pos.y - halfY && worldPoint.y <= pos.y + halfY;
    }

    // --- Coroutines for animations ---

    IEnumerator SnapToSlotCoroutine(Slot slot, float duration, Action onComplete)
    {
        IsPlaced = true;
        CurrentSlot = slot;
        slot.SetOccupant(this);

        Vector3 startPos = transform.position;
        Quaternion startRot = transform.rotation;
        Vector3 targetPos = new Vector3(slot.transform.position.x, slot.transform.position.y, transform.position.z);
        Quaternion targetRot = Quaternion.identity;

        float t = 0f;
        float d = Mathf.Max(0.001f, duration);
        while (t < d)
        {
            t += Time.deltaTime;
            float a = Mathf.Clamp01(t / d);
            transform.position = Vector3.Lerp(startPos, targetPos, a);
            transform.rotation = Quaternion.Slerp(startRot, targetRot, a);
            yield return null;
        }

        transform.position = targetPos;
        transform.rotation = targetRot;
        transform.localScale = originalScale;
        SetSortingOrder(baseSortingOrder);
        runningCoroutine = null;
        onComplete?.Invoke();
    }
    IEnumerator AnimateStar(GameObject star, Vector2 direction)
    {
        SpriteRenderer sr = star.GetComponent<SpriteRenderer>();
        if (sr == null)
        {
            Destroy(star);
            yield break;
        }

        Color startColor = sr.color;
        Vector3 startPos = star.transform.position;
        Vector3 endPos = startPos + (Vector3)(direction * 0.15f);

        float duration = 0.4f;
        float t = 0f;

        while (t < duration)
        {
            t += Time.deltaTime;
            float a = t / duration;

            star.transform.position = Vector3.Lerp(startPos, endPos, a);
            sr.color = new Color(startColor.r, startColor.g, startColor.b, 1f - a);

            yield return null;
        }

        Destroy(star);
    }


    IEnumerator CorrectPlacementFeedback()
    {
        if (frontRenderer == null)
            yield break;

        Color original = frontRenderer.color;
        Color highlight = new Color(0.7f, 1f, 0.7f); // soft green

        float t = 0f;
        float d = 0.15f;

        // Fade to highlight
        while (t < d)
        {
            t += Time.deltaTime;
            frontRenderer.color = Color.Lerp(original, highlight, t / d);
            yield return null;
        }

        // Small pulse
        Vector3 startScale = transform.localScale;
        Vector3 peak = startScale * 1.08f;
        t = 0f;

        while (t < d)
        {
            t += Time.deltaTime;
            transform.localScale = Vector3.Lerp(startScale, peak, t / d);
            yield return null;
        }

        t = 0f;
        while (t < d)
        {
            t += Time.deltaTime;
            transform.localScale = Vector3.Lerp(peak, startScale, t / d);
            yield return null;
        }

        frontRenderer.color = original;
        transform.localScale = startScale;
    }

    IEnumerator ReturnToOriginCoroutine(float duration, Action onComplete)
    {
        IsPlaced = false;

        Vector3 startPos = transform.position;
        Quaternion startRot = transform.rotation;
        Vector3 targetPos = originalPosition;
        Quaternion targetRot = originalRotation;
        Vector3 startScale = transform.localScale;

        float t = 0f;
        float d = Mathf.Max(0.001f, duration);
        while (t < d)
        {
            t += Time.deltaTime;
            float a = Mathf.Clamp01(t / d);
            transform.position = Vector3.Lerp(startPos, targetPos, a);
            transform.rotation = Quaternion.Slerp(startRot, targetRot, a);
            transform.localScale = Vector3.Lerp(startScale, originalScale, a);
            yield return null;
        }

        transform.position = targetPos;
        transform.rotation = targetRot;
        transform.localScale = originalScale;
        SetSortingOrder(baseSortingOrder);
        runningCoroutine = null;
        onComplete?.Invoke();
    }

    IEnumerator RotateToZeroCoroutine(float duration, Action onComplete)
    {
        Quaternion startRot = transform.rotation;
        Quaternion targetRot = Quaternion.identity;
        float t = 0f;
        float d = Mathf.Max(0.001f, duration);
        while (t < d)
        {
            t += Time.deltaTime;
            float a = Mathf.Clamp01(t / d);
            transform.rotation = Quaternion.Slerp(startRot, targetRot, a);
            yield return null;
        }

        transform.rotation = targetRot;
        runningCoroutine = null;
        onComplete?.Invoke();
    }

    IEnumerator PulseCoroutine(float duration)
    {
        float d = Mathf.Max(0.001f, duration);
        float t = 0f;
        Vector3 startScale = transform.localScale;
        Vector3 peak = startScale * 1.12f;

        // enlarge
        while (t < d)
        {
            t += Time.deltaTime;
            float a = Mathf.Clamp01(t / d);
            transform.localScale = Vector3.Lerp(startScale, peak, a);
            yield return null;
        }

        // shrink back
        t = 0f;
        while (t < d)
        {
            t += Time.deltaTime;
            float a = Mathf.Clamp01(t / d);
            transform.localScale = Vector3.Lerp(peak, startScale, a);
            yield return null;
        }

        transform.localScale = startScale;
        runningCoroutine = null;
    }

    // --- Helpers ---

    private void SetSortingOrder(int order)
    {
        if (frontRenderer != null) frontRenderer.sortingOrder = order;
        if (shadowRenderer != null) shadowRenderer.sortingOrder = order - 1;
    }

    private void CancelRunningCoroutine()
    {
        if (runningCoroutine != null)
        {
            StopCoroutine(runningCoroutine);
            runningCoroutine = null;
        }
    }

    // Draw selection rectangle each frame while selected (uses ContainsPoint geometry)
    private void LateUpdate()
    {
        if (!isSelected) return;
        if (frontRenderer == null || frontRenderer.sprite == null) return;

        Sprite sprite = frontRenderer.sprite;
        Rect rect = sprite.rect;
        Vector2 pivot = sprite.pivot;
        float ppu = sprite.pixelsPerUnit;

        float spriteW = rect.width / ppu;
        float spriteH = rect.height / ppu;

        Vector2 pivotOffset = new Vector2(
            (pivot.x / rect.width - 0.5f) * spriteW,
            (pivot.y / rect.height - 0.5f) * spriteH
        );

        float halfW = spriteW * 0.5f * transform.localScale.x;
        float halfH = spriteH * 0.5f * transform.localScale.y;

        // corners in local space (match ContainsPoint)
        Vector3 bl = new Vector3(pivotOffset.x - halfW, pivotOffset.y - halfH, 0f);
        Vector3 tl = new Vector3(pivotOffset.x - halfW, pivotOffset.y + halfH, 0f);
        Vector3 tr = new Vector3(pivotOffset.x + halfW, pivotOffset.y + halfH, 0f);
        Vector3 br = new Vector3(pivotOffset.x + halfW, pivotOffset.y - halfH, 0f);

        // transform to world
        Vector3 wbl = transform.TransformPoint(bl);
        Vector3 wtl = transform.TransformPoint(tl);
        Vector3 wtr = transform.TransformPoint(tr);
        Vector3 wbr = transform.TransformPoint(br);

        // draw rectangle lines (visible in Scene; Game view if Gizmos enabled)
        Debug.DrawLine(wbl, wtl, selectionColor);
        Debug.DrawLine(wtl, wtr, selectionColor);
        Debug.DrawLine(wtr, wbr, selectionColor);
        Debug.DrawLine(wbr, wbl, selectionColor);
    }
}