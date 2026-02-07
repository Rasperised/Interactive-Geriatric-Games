using UnityEngine;
using System.IO.Ports;

public class PlayerBallController : MonoBehaviour
{
    [Header("Serial")]
    public string portName = "COM3";
    public int baudRate = 115200;

    [Header("Tilt Settings")]
    public float deadZone = 5f;      // degrees
    public float maxAngle = 30f;     // degrees
    public float moveSpeed = 5f;
    public float maxX = 5f;

    [Header("Directional Movement")]
    public float forwardSpeed = 2.0f;   // how fast the ball moves down
    public float maxTiltAngle = 30f;     // physical tilt limit

    [Header("Smooth Control")]
    public float maxHorizontalSpeed = 4f;
    public float acceleration = 6f;

    private float currentVelocityX = 0f;

    private SerialPort serial;
    private float currentAngle = 0f;

    void Start()
    {
        serial = new SerialPort(portName, baudRate);
        serial.Open();
        serial.ReadTimeout = 20;
    }

    void Update()
    {
        ReadSerial();

        float targetVelocityX = 0f;

        if (Mathf.Abs(currentAngle) > deadZone)
        {
            float normalized =
                Mathf.Clamp(currentAngle / maxAngle, -1f, 1f);

            targetVelocityX = normalized * maxHorizontalSpeed;
        }

        currentVelocityX = Mathf.Lerp(
            currentVelocityX,
            targetVelocityX,
            acceleration * Time.deltaTime
        );

        Vector3 pos = transform.position;
        pos.x += currentVelocityX * Time.deltaTime;
        pos.x = Mathf.Clamp(pos.x, -maxX, maxX);

        transform.position = pos;
    }


    void ReadSerial()
    {
        if (!serial.IsOpen) return;

        try
        {
            string data = serial.ReadLine();
            currentAngle = -float.Parse(data);
        }
        catch { }
    }

    void OnApplicationQuit()
    {
        if (serial != null && serial.IsOpen)
            serial.Close();
    }
}
