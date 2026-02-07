using UnityEngine;
using System.IO.Ports;

public class PlayerBallArcController : MonoBehaviour
{
    [Header("Serial")]
    [Tooltip("Leave empty to auto-detect COM port")]
    public string portName = "";
    public int baudRate = 115200;

    [Header("References")]
    public Transform topCenter;
    public Transform targetRight;
    public Transform leftTarget;
    public Transform rightTarget;

    [Header("Tilt Mapping")]
    public float deadZoneDeg = 4f;
    public float maxTiltDeg = 50f;

    [Header("Smoothing")]
    [Range(0f, 1f)]
    public float filterStrength = 0.12f;

    [Header("Collision")]
    public float ballRadius = 0.5f;
    public LayerMask obstacleLayer;

    [Header("Game Control")]
    public GameController gameController; // Add reference to game controller

    private SerialPort serial;
    private float currentAngleDeg = 0f;
    private float radius;
    private float thetaMax;
    private float filteredT = 0f;
    private Vector3 currentPosition;

    void Start()
    {
        // 🔹 AUTO-DETECT PORT
        if (string.IsNullOrEmpty(portName))
        {
            string[] ports = SerialPort.GetPortNames();
            if (ports.Length > 0)
            {
                portName = ports[0]; // pick first available
                Debug.Log($"[Serial] Auto-selected port: {portName}");
            }
            else
            {
                Debug.LogError("[Serial] No COM ports found!");
                enabled = false;
                return;
            }
        }

        serial = new SerialPort(portName, baudRate);
        serial.Open();
        serial.ReadTimeout = 20;

        // Arc setup
        Vector3 top = topCenter.position;
        Vector3 trg = targetRight.position;

        float x = Mathf.Abs(trg.x - top.x);
        float yDrop = Mathf.Max(0.001f, top.y - trg.y);

        radius = (x * x + yDrop * yDrop) / (2f * yDrop);
        thetaMax = Mathf.Asin(Mathf.Clamp(x / radius, 0f, 0.9999f));

        currentPosition = top;
        transform.position = top;
    }

    void Update()
    {
        // Don't accept input if game has ended
        if (gameController != null && gameController.IsGameEnded())
        {
            return;
        }

        ReadSerial();

        float a = currentAngleDeg;
        if (Mathf.Abs(a) < deadZoneDeg)
            a = 0f;

        float rawT = Mathf.Clamp(a / maxTiltDeg, -1f, 1f);
        filteredT = Mathf.Lerp(filteredT, rawT, filterStrength);

        float theta = filteredT * thetaMax;

        Vector3 top = topCenter.position;
        float cx = top.x;
        float cy = top.y - radius;

        Vector3 desiredPosition = new Vector3(
            cx + radius * Mathf.Sin(theta),
            cy + radius * Mathf.Cos(theta),
            top.z
        );

        Vector3 newPosition = MoveWithCollision(currentPosition, desiredPosition);
        currentPosition = newPosition;
        transform.position = newPosition;
    }

    Vector3 MoveWithCollision(Vector3 from, Vector3 to)
    {
        Vector3 direction = to - from;
        float distance = direction.magnitude;

        if (distance < 0.001f)
            return from;

        direction.Normalize();

        if (Physics.SphereCast(
            from,
            ballRadius * 0.9f,
            direction,
            out RaycastHit hit,
            distance,
            obstacleLayer,
            QueryTriggerInteraction.Ignore))
        {
            float safeDistance = Mathf.Max(0, hit.distance - 0.01f);
            return from + direction * safeDistance;
        }

        return to;
    }

    void ReadSerial()
    {
        if (serial == null || !serial.IsOpen) return;

        try
        {
            string data = serial.ReadLine();
            currentAngleDeg = float.Parse(data);
        }
        catch { }
    }

    void OnApplicationQuit()
    {
        if (serial != null && serial.IsOpen)
            serial.Close();
    }
}