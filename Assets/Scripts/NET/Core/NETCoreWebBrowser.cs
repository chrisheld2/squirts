using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public class NETCoreWebBrowser
{
    #region Variables

    private enum STATES
    {
        NONE,
        INITIALIZED,
        READY,
        HAS_SESSION,
        CONNECTED,
        CLOSING,
        ERROR_CONNECTED
    }


    #region Private
    private STATES state = STATES.NONE;


    private bool isReady = false;
    private bool isClosing = false;

    private WebSocket webSocket;

    private string _url;

    private byte[] bytesKeepAlive;

    private DateTime markKeepAlive;
    private DateTime markTraffic;

    private int _trafficReceived = 0;
    private int _trafficSent = 0;

    private string version = "1.0.1";

    #endregion

    #region Public
    public string Version { get { return version; } }

    // States
    public bool IsReady { get { return isReady; } }
    public bool IsClosing { get { return isClosing; } }

    // Delegates
    public delegate void Delegate_IsReady(string clientGUID);
    public delegate void Delegate_MessageReceived(string data);
    public delegate void Delegate_MessageTraffic(int bpsSent, int bpsReceived, int bpsTotal);

    // Events
    public event Delegate_IsReady OnIsReady;
    public event Delegate_MessageReceived OnMessageReceived;
    public event Delegate_MessageTraffic OnMessageTraffic;

    #endregion

    #endregion

    #region Constants
    private const string YOURNEWGUID = "YNGUD";
    private const string CLIENTGUID = "SCGID";
    #endregion

    #region Public Methods
    public void Init(string serverURL)
    {
        // Ensure the NETStaticCom GameObject exists for WebGL communication
        if (GameObject.Find("NETStaticCom") == null)
        {
            GameObject go = new GameObject("NETStaticCom");
            go.AddComponent<NETStaticCom>();
        }

        isClosing = false;

        markKeepAlive = DateTime.Now;
        markTraffic = DateTime.Now;

        _url = serverURL;

        bytesKeepAlive = Encoding.ASCII.GetBytes(".");

    }
    public async Task WebSocketInit(string sessionGUID)
    {
        if (string.IsNullOrEmpty(sessionGUID))
            throw new InvalidOperationException("No session!");

        if (state == STATES.CONNECTED)
            throw new InvalidOperationException("Already connected!");

        try
        {
            webSocket = new WebSocket();
            webSocket.OnMessageReceivedEvent += OnWebSocketMessageReceived;

            // string url = $"wss://{_serverAddress}:{_serverPort}?session={sessionGUID}";
            string url = $"{_url}?session={sessionGUID}";
            DL.Log("Connecting to: " + url);
            webSocket.Connect(url);

            _ = KeepAliveLoop();
            _ = TrafficLoop();

        }
        catch (Exception ex)
        {
            throw new Exception("WebSocketInit:" + ex.Message);
        }
    }

    public async Task SendToAll(string data)
    {
        if (webSocket != null)
        {
            webSocket.Send(data);
            _trafficSent += data.Length;
        }
    }

    public async Task ShutDown()
    {
        isReady = false;
        isClosing = true;

        if (webSocket != null)
        {
            webSocket.Close();
            webSocket = null;
        }
    }

    public TrafficStats TrafficStatsGet()
    {
        var trafficStats = new TrafficStats()
        {
            sent = _trafficSent,
            received = _trafficReceived,
            total = _trafficSent + _trafficReceived
        };

        return trafficStats;
    }

    #endregion

    #region Private Methods


    private async Task KeepAliveLoop()
    {
        try
        {
            while (!isClosing)
            {
                await Task.Delay(25);

                if (DateTime.Now < markKeepAlive.AddSeconds(2)) continue;

                markKeepAlive = DateTime.Now;

                await SendToAll("⏰");
            }
        }
        catch (Exception ex)
        {
            DL.Error("KeepAlive:" + ex.Message);
        }
    }

    private async Task TrafficLoop()
    {
        try
        {
            while (!isClosing)
            {
                await Task.Delay(50);

                if (DateTime.Now < markTraffic.AddSeconds(1)) continue;

                markTraffic = DateTime.Now;

                int total = _trafficSent + _trafficReceived;

                OnMessageTraffic?.Invoke(_trafficSent, _trafficReceived, total);

                _trafficSent = 0;
                _trafficReceived = 0;
            }
        }
        catch (Exception ex)
        {
            DL.Error("Traffic:" + ex.Message);
        }
    }


    private void OnWebSocketMessageReceived(string message)
    {
        _trafficReceived += message.Length;

        // CheckForYourNewGUID(message);

        OnMessageReceived?.Invoke(message);
    }

    #endregion

    #region Classes
    public struct TrafficStats
    {
        public int sent;
        public int received;
        public int total;
    }
    #endregion
}
