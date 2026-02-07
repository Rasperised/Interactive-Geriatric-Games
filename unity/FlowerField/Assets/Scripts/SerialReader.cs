using UnityEngine;
using System.IO.Ports;
using System.Threading;
using System.Linq;

public class SerialReader : MonoBehaviour
{
    [Header("Serial Settings")]
    public bool autoDetectPort = true;
    public string portName = "COM3";
    public int baudRate = 115200;

    private SerialPort serial;
    private Thread readThread;
    private bool running = false;

    private GameManager gameManager;
    private float[] latestValues = new float[5];

    void Start()
    {
        gameManager = FindObjectOfType<GameManager>();

        if (autoDetectPort)
        {
            portName = AutoDetectPort();
        }

        if (string.IsNullOrEmpty(portName))
        {
            Debug.LogWarning("No valid serial port found.");
            return;
        }

        serial = new SerialPort(portName, baudRate);
        serial.ReadTimeout = 50;

        // IMPORTANT: Prevent ESP32 from resetting
        serial.DtrEnable = false;
        serial.RtsEnable = false;

        try
        {
            serial.Open();
            Debug.Log("Serial connected to: " + portName);

            running = true;

            // Start background reading thread
            readThread = new Thread(ReadSerial);
            readThread.Start();
        }
        catch
        {
            Debug.LogWarning("Failed to open serial port: " + portName);
        }
    }

    // Try to detect the correct COM port automatically
    private string AutoDetectPort()
    {
        string[] ports = SerialPort.GetPortNames();

        if (ports.Length == 0)
        {
            Debug.LogWarning("No COM ports detected.");
            return null;
        }

        Debug.Log("Detected COM Ports: " + string.Join(", ", ports));

        // Optional: prefer lower COM numbers first
        ports = ports.OrderBy(p => p).ToArray();

        foreach (string p in ports)
        {
            try
            {
                SerialPort testPort = new SerialPort(p, baudRate);
                testPort.ReadTimeout = 200;

                testPort.DtrEnable = false;
                testPort.RtsEnable = false;

                testPort.Open();

                // Try reading a line (ESP32 should be sending JSON)
                string line = testPort.ReadLine().Trim();

                if (line.StartsWith("{") && line.Contains("s1"))
                {
                    Debug.Log("ESP32 detected on: " + p);
                    testPort.Close();
                    return p;
                }

                testPort.Close();
            }
            catch
            {
                // ignore and try next port
            }
        }

        Debug.LogWarning("Could not auto-detect ESP32 port.");
        return null;
    }

    // Background thread reading serial
    private void ReadSerial()
    {
        while (running && serial != null && serial.IsOpen)
        {
            try
            {
                string line = serial.ReadLine().Trim();
                SensorPacket packet = JsonUtility.FromJson<SensorPacket>(line);

                latestValues[0] = packet.s1;
                latestValues[1] = packet.s2;
                latestValues[2] = packet.s3;
                latestValues[3] = packet.s4;
                latestValues[4] = packet.s5;
            }
            catch { }
        }
    }

    void Update()
    {
        if (gameManager != null)
            gameManager.UpdateSensorInput(latestValues);
    }

    void OnApplicationQuit()
    {
        running = false;

        if (readThread != null && readThread.IsAlive)
            readThread.Join();

        if (serial != null && serial.IsOpen)
            serial.Close();
    }
}

[System.Serializable]
public class SensorPacket
{
    public float s1;
    public float s2;
    public float s3;
    public float s4;
    public float s5;
}
