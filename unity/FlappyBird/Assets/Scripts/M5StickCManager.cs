using UnityEngine;
using System.IO.Ports;
using System.Threading;

public class M5StickCManager : MonoBehaviour
{
    public int baudRate = 115200;
    public float tiltSensitivity = 3f;

    private SerialPort serialPort;
    private Thread readThread;
    private volatile bool keepReading = true;
    private volatile float currentTilt;
    private volatile bool connected;

    public bool IsConnected => connected;

    void Start()
    {
        TryAutoConnect();
    }

    void Update()
    {
        // If cable removed / M5 rebooted → try reconnect
        if (!connected)
            TryAutoConnect();
    }

    // -----------------------------------------------------
    // AUTO-SCAN FOR ANY COM PORT UNTIL ONE WORKS
    // -----------------------------------------------------
    void TryAutoConnect()
    {
        // already connected? do nothing
        if (serialPort != null && serialPort.IsOpen)
            return;

        string[] ports = SerialPort.GetPortNames();

        foreach (string port in ports)
        {
            try
            {
                SerialPort sp = new SerialPort(port, baudRate);
                sp.ReadTimeout = 1000;
                sp.Open();

                Debug.Log("✅ Auto-connected to M5StickC on " + port);

                serialPort = sp;
                connected = true;

                // start thread if not running
                if (readThread == null || !readThread.IsAlive)
                {
                    keepReading = true;
                    readThread = new Thread(ReadSerialLoop);
                    readThread.Start();
                }

                return; // success — stop scanning
            }
            catch
            {
                // failed for this port — try next one
            }
        }

        connected = false;
    }

    // -----------------------------------------------------
    // BACKGROUND THREAD — READ SERIAL DATA CONTINUOUSLY
    // -----------------------------------------------------
    void ReadSerialLoop()
    {
        while (keepReading)
        {
            if (serialPort == null || !serialPort.IsOpen)
            {
                connected = false;
                Thread.Sleep(500);
                continue;
            }

            try
            {
                string line = serialPort.ReadLine();
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                // Expected: "accY,accX,accZ" or similar
                string[] parts = line.Split(',');

                if (parts.Length >= 1 &&
                    float.TryParse(parts[0], out float accY))
                {
                    float newTilt = Mathf.Clamp(accY * tiltSensitivity, -3f, 3f);
                    currentTilt = newTilt;
                }
            }
            catch
            {
                connected = false;
                Thread.Sleep(500);
            }
        }
    }

    // -----------------------------------------------------
    // PUBLIC ACCESSOR FOR FLAPPY BIRD CONTROLLER
    // -----------------------------------------------------
    public float GetTiltValue() => currentTilt;

    // -----------------------------------------------------
    // CLEANUP
    // -----------------------------------------------------
    void OnDestroy()
    {
        keepReading = false;
        connected = false;

        if (serialPort != null)
        {
            try { serialPort.Close(); } catch { }
        }

        if (readThread != null && readThread.IsAlive)
        {
            try { readThread.Abort(); } catch { }
        }
    }
}
