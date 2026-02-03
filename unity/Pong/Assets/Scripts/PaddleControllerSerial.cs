using System.Globalization;
using System.Linq;
using UnityEngine;

public class PaddleControllerSerial : MonoBehaviour
{
    public int baudRate = 115200;

    [Header("Movement Tuning")]
    public float sensitivity = 0.06f;
    public float smoothTime = 0.05f;   // SmoothDamp timing
    public float limitY = 4.2f;
    public float deadZone = 0.5f;      // ignore tiny tilt noise

    float currentRoll = 0f;
    float smoothVelocity = 0f;

    bool readyReceived = false;   // NEW — only addition

    void Start()
    {
        // Serial handled automatically by SerialPortManager
    }

    void Update()
    {
        // Stop during menu or transitions
        if (GameController.Instance == null ||
            GameController.Instance.state != GameController.GameState.Playing)
        {
            return;
        }

        // Use auto-scan serial manager
        var sp = SerialPortManager.Instance;

        if (sp == null || !sp.IsConnected)
            return;

        string line = sp.ReadLineSafe();
        if (string.IsNullOrEmpty(line))
            return;

        // -------------------------------
        // WAIT FOR "READY" FROM ARDUINO
        // -------------------------------
        if (!readyReceived)
        {
            // If Arduino sends READY → good
            if (line.Contains("READY"))
            {
                readyReceived = true;
                Debug.Log("READY received.");
            }
            else
            {
                // Fallback: if data already looks like IMU values, treat as ready
                if (line.Contains(",") && line.Count(c => c == ',') == 2)
                {
                    readyReceived = true;
                    Debug.Log("READY fallback — IMU already running.");
                }
            }

            if (!readyReceived)
                return;
        }


        // -----------------------------------
        // ORIGINAL MOVEMENT LOGIC (unchanged)
        // -----------------------------------
        try
        {
            line = line.Trim(); // "pitch,roll,yaw"
            string[] parts = line.Split(',');

            if (parts.Length >= 2 &&
                float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float roll))
            {
                currentRoll = roll;

                // Apply dead zone to remove small jitters
                float rollInput = Mathf.Abs(currentRoll) < deadZone ? 0f : currentRoll;

                // Convert tilt → target Y position
                float moveY = Mathf.Clamp(rollInput * sensitivity, -limitY, limitY);

                // Smooth movement
                float newY = Mathf.SmoothDamp(
                    transform.position.y,
                    moveY,
                    ref smoothVelocity,
                    smoothTime
                );

                // Apply position
                transform.position = new Vector3(
                    transform.position.x,
                    Mathf.Clamp(newY, -limitY, limitY),
                    transform.position.z
                );
            }
        }
        catch
        {
            // Ignore bad/partial serial reads
        }
    }

    void OnApplicationQuit()
    {
        // SerialPortManager will handle closing ports
    }
}
