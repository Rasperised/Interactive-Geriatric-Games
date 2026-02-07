using UnityEngine;
using System.IO.Ports;

public class PlayerBallVerticalController : MonoBehaviour
{
    [Header("Serial")]
    public string portName = "";
    public int baudRate = 115200;

    [Header("Vertical Movement")]
    public float minY = -3f;
    public float maxY = 3f;

    [Header("Smoothing")]
    [Range(0f, 1f)]
    public float filterStrength = 0.15f;

    [Header("Game Control")]
    public GameController gameController;

    private SerialPort serial;
    private float sensorValue;   // 0–1 from Arduino
    private float filteredY;

    // Blocking system
    private bool isBlocked = false;
    private float blockedY;
    private int blockDirection = 0; // 0 = none, 1 = block up, -1 = block down

    void Start()
    {
        if (string.IsNullOrEmpty(portName))
        {
            string[] ports = SerialPort.GetPortNames();
            if (ports.Length > 0)
                portName = ports[0];
            else
            {
                Debug.LogError("No COM ports found");
                enabled = false;
                return;
            }
        }

        serial = new SerialPort(portName, baudRate);
        serial.ReadTimeout = 20;
        serial.Open();

        filteredY = transform.position.y;
    }

    void Update()
    {
        if (gameController != null && gameController.IsGameEnded())
            return;

        ReadSerial();

        // Calculate desired position from sensor
        float targetY = Mathf.Lerp(minY, maxY, sensorValue);
        filteredY = Mathf.Lerp(filteredY, targetY, filterStrength);

        Vector3 pos = transform.position;

        // Apply blocking
        if (isBlocked)
        {
            // Block movement in the blocked direction
            if (blockDirection == 1 && filteredY > blockedY)
            {
                // Block upward movement
                filteredY = blockedY;
            }
            else if (blockDirection == -1 && filteredY < blockedY)
            {
                // Block downward movement
                filteredY = blockedY;
            }
        }

        pos.y = Mathf.Clamp(filteredY, minY, maxY);
        transform.position = pos;
    }

    void ReadSerial()
    {
        if (serial == null || !serial.IsOpen) return;

        try
        {
            string data = serial.ReadLine();
            sensorValue = Mathf.Clamp01(float.Parse(data));
        }
        catch { }
    }

    // Called by TargetBall when hit
    public void SetBlock(float blockY, int direction)
    {
        isBlocked = true;
        blockedY = blockY;
        blockDirection = direction;

        // Snap to position
        Vector3 pos = transform.position;
        pos.y = blockY;
        transform.position = pos;
        filteredY = blockY;
    }

    // Called by TargetBall when leaving
    public void ClearBlock()
    {
        isBlocked = false;
        blockDirection = 0;
    }

    void OnApplicationQuit()
    {
        if (serial != null && serial.IsOpen)
            serial.Close();
    }
}