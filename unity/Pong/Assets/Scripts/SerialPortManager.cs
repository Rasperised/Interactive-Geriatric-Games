using UnityEngine;
using System.IO.Ports;

public class SerialPortManager : MonoBehaviour
{
    public static SerialPortManager Instance;

    public SerialPort serialPort;
    public int baudRate = 115200;
    public int readTimeout = 20;

    public bool IsConnected => serialPort != null && serialPort.IsOpen;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        AutoScanPorts();
    }

    void AutoScanPorts()
    {
        string[] ports = SerialPort.GetPortNames();

        Debug.Log("[Serial] Scanning ports…");

        foreach (string port in ports)
        {
            try
            {
                SerialPort sp = new SerialPort(port, baudRate);
                sp.ReadTimeout = readTimeout;
                sp.Open();

                // NEW: flush old junk from buffer
                sp.DiscardInBuffer();
                sp.DiscardOutBuffer();

                Debug.Log("[Serial] Connected on " + port);
                serialPort = sp;
                return; // success — stop scanning
            }
            catch
            {
                Debug.Log("[Serial] Failed: " + port);
            }
        }

        Debug.LogWarning("[Serial] No valid COM port found.");
    }

    public string ReadLineSafe()
    {
        if (!IsConnected) return null;

        try
        {
            return serialPort.ReadLine();
        }
        catch
        {
            return null;
        }
    }
}
