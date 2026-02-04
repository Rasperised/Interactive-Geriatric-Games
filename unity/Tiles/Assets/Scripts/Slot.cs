using UnityEngine;

public class Slot : MonoBehaviour
{
    [Header("Slot identity")]
    [SerializeField] private int slotIndex = -1;

    [Header("Slot size (world units)")]
    [Tooltip("Width (x) and height (y) in world units. If zero, size will be computed from attached SpriteRenderer when available.")]
    public Vector2 slotSize = Vector2.zero;

    [Header("Debug")]
    [SerializeField] private Color gizmoColor = new Color(0f, 0.6f, 1f, 0.25f);

    // Occupant tile (null when empty)
    public Tile Occupant { get; private set; }

    // Public read-only helpers
    public int SlotIndex => slotIndex;
    public Vector2 Center => transform.position;

    // Add or update members
    public SpriteRenderer visualRenderer; // assign in prefab or found in Awake/Initialize
    public Sprite slotSprite; // optional, assign in inspector to override prefab sprite

    // Initialize called by GameController after creation
    public void Initialize(int index, Vector2 size)
    {
        slotIndex = index;
        if (size != Vector2.zero)
            slotSize = size;

        // compute slotSize from SpriteRenderer if still zero (existing logic)
        var sr = GetComponent<SpriteRenderer>();
        if (slotSize == Vector2.zero && sr != null && sr.sprite != null)
        {
            float w = sr.sprite.rect.width / sr.sprite.pixelsPerUnit * transform.localScale.x;
            float h = sr.sprite.rect.height / sr.sprite.pixelsPerUnit * transform.localScale.y;
            slotSize = new Vector2(w, h);
        }

        // Ensure visualRenderer is set (try child named "Visual" then any child SpriteRenderer)
        if (visualRenderer == null)
            visualRenderer = transform.Find("Visual")?.GetComponent<SpriteRenderer>() ?? GetComponentInChildren<SpriteRenderer>();

        if (visualRenderer != null)
        {
            // optionally override sprite
            if (slotSprite != null) visualRenderer.sprite = slotSprite;

            // scale visual to match slotSize (sprite pivot must be centered)
            if (visualRenderer.sprite != null && slotSize.x > 0 && slotSize.y > 0)
            {
                float spriteW = visualRenderer.sprite.rect.width / visualRenderer.sprite.pixelsPerUnit;
                float spriteH = visualRenderer.sprite.rect.height / visualRenderer.sprite.pixelsPerUnit;
                visualRenderer.transform.localScale = new Vector3(slotSize.x / spriteW, slotSize.y / spriteH, 1f);
            }

            // ensure slot visuals are drawn behind tiles
            visualRenderer.sortingOrder = -50;

            // subtle visual: low alpha if sprite has color
            Color c = visualRenderer.color;
            c.a = Mathf.Max(0.10f, c.a);
            visualRenderer.color = c;
        }
    }

    // Mark a tile as occupying this slot
    public void SetOccupant(Tile tile)
    {
        Occupant = tile;
    }

    // Clear occupant
    public void ClearOccupant()
    {
        Occupant = null;
    }

    // Check whether a world-space point lies inside this slot's AABB
    public bool ContainsPoint(Vector2 worldPoint)
    {
        Vector3 local = transform.InverseTransformPoint(worldPoint);
        // Use centered bounds assuming pivot is centered
        float halfW = slotSize.x * 0.5f;
        float halfH = slotSize.y * 0.5f;
        return local.x >= -halfW && local.x <= halfW && local.y >= -halfH && local.y <= halfH;
    }

    // Distance from slot center to a world point
    public float DistanceTo(Vector2 worldPoint)
    {
        return Vector2.Distance(Center, worldPoint);
    }

    // Draw a simple gizmo in the scene view to visualize slot bounds
    void OnDrawGizmos()
    {
        if (slotSize == Vector2.zero)
        {
            var sr = GetComponent<SpriteRenderer>();
            if (sr != null && sr.sprite != null)
            {
                float w = sr.sprite.rect.width / sr.sprite.pixelsPerUnit * transform.localScale.x;
                float h = sr.sprite.rect.height / sr.sprite.pixelsPerUnit * transform.localScale.y;
                Gizmos.color = gizmoColor;
                Gizmos.DrawCube(transform.position, new Vector3(w, h, 0.01f));
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireCube(transform.position, new Vector3(w, h, 0.01f));
                return;
            }
        }

        Gizmos.color = gizmoColor;
        Gizmos.DrawCube(transform.position, new Vector3(slotSize.x, slotSize.y, 0.01f));
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(transform.position, new Vector3(slotSize.x, slotSize.y, 0.01f));
    }
}