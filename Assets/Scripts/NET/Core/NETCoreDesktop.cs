using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public class NETCoreDesktop
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

    private STATES state = STATES.NONE;
    private ClientWebSocket webSocket;
    private string _url;
    private readonly object stateLock = new object();
    private bool isReceiveLoopRunning = false;
    private readonly SemaphoreSlim receiveSemaphore = new SemaphoreSlim(1, 1);

    private int _trafficReceived = 0;
    private int _trafficSent = 0;

    private readonly string version = "1.0.0";
    private byte[] _sendBuffer = new byte[8192]; // Reusable send buffer

    public string Version => version;

    public delegate void Delegate_MessageReceived(string data);
    public delegate void Delegate_MessageTraffic(int bpsSent, int bpsReceived, int bpsTotal);

    public event Delegate_MessageReceived OnMessageReceived;
    public event Delegate_MessageTraffic OnMessageTraffic;

    #endregion

    #region Public Methods

    public void Init(string serverURL)
    {
        if (state != STATES.NONE)
            throw new InvalidOperationException("Already initialized!");

        _url = serverURL;

        state = STATES.INITIALIZED;
    }

    public async Task WebSocketInit(string sessionGUID)
    {
        if (string.IsNullOrEmpty(sessionGUID))
            throw new InvalidOperationException("No session!");

        lock (stateLock)
        {
            if (state == STATES.CONNECTED || isReceiveLoopRunning)
                throw new InvalidOperationException("Already connected!");
        }

        try
        {
            // Dispose of old WebSocket if it exists
            if (webSocket != null)
            {
                try
                {
                    webSocket.Dispose();
                }
                catch { }
                webSocket = null;
            }

            webSocket = new ClientWebSocket();
            // string url = $"wss://{_serverAddress}:{_serverPort}?session={sessionGUID}";
            string url = $"{_url}?session={sessionGUID}";

            DL.Big("WebSocketInit: " + url);

            await webSocket.ConnectAsync(new Uri(url), CancellationToken.None);

            lock (stateLock)
            {
                state = STATES.CONNECTED;
                isReceiveLoopRunning = true;
            }

            _ = Task.Run(ReceiveLoop);
        }
        catch (Exception ex)
        {
            lock (stateLock)
            {
                state = STATES.ERROR_CONNECTED;
                isReceiveLoopRunning = false;
            }

            // Clean up on failure
            if (webSocket != null)
            {
                try
                {
                    webSocket.Dispose();
                }
                catch { }
                webSocket = null;
            }

            throw new InvalidOperationException("WebSocketInit failed: " + ex.Message);
        }
    }

    public async Task SendToAll(string data)
    {
        if (webSocket == null || webSocket.State != WebSocketState.Open || state != STATES.CONNECTED)
            return;

        try
        {
            int byteCount = Encoding.UTF8.GetByteCount(data);

            // Resize buffer if needed
            if (byteCount > _sendBuffer.Length)
            {
                _sendBuffer = new byte[byteCount];
            }

            // Write directly to reusable buffer
            int actualBytes = Encoding.UTF8.GetBytes(data, 0, data.Length, _sendBuffer, 0);

            await webSocket.SendAsync(new ArraySegment<byte>(_sendBuffer, 0, actualBytes), WebSocketMessageType.Text, true, CancellationToken.None);
            _trafficSent += actualBytes;
        }
        catch (WebSocketException wsEx)
        {
            DL.Error($"WebSocket send error: {wsEx.Message}");
            state = STATES.ERROR_CONNECTED;
        }
        catch (Exception ex)
        {
            DL.Error($"Unexpected error sending message: {ex.Message}");
            state = STATES.ERROR_CONNECTED;
        }
    }

    public async Task ShutDown()
    {
        lock (stateLock)
        {
            state = STATES.CLOSING;
        }

        if (webSocket != null)
        {
            try
            {
                // Only attempt to close if the connection is still open or closing
                if (webSocket.State == WebSocketState.Open || webSocket.State == WebSocketState.CloseReceived)
                {
                    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                }
            }
            catch (Exception ex)
            {
                DL.Error($"Error during WebSocket shutdown: {ex.Message}");
            }
            finally
            {
                webSocket.Dispose();
                webSocket = null;
            }
        }

        // Wait a moment for the receive loop to exit cleanly
        await Task.Delay(100);

        lock (stateLock)
        {
            state = STATES.NONE;
            isReceiveLoopRunning = false;
        }

        // Reset the semaphore if it's currently taken
        if (receiveSemaphore.CurrentCount == 0)
        {
            receiveSemaphore.Release();
        }
    }

    public TrafficStats TrafficStatsGet()
    {
        return new TrafficStats
        {
            sent = _trafficSent,
            received = _trafficReceived,
            total = _trafficSent + _trafficReceived
        };
    }

    #endregion

    #region Private Methods

    private async Task ReceiveLoop()
    {
        // Prevent multiple receive loops from running
        if (!await receiveSemaphore.WaitAsync(0))
        {
            DL.Error("ReceiveLoop already running - preventing duplicate execution");
            return;
        }

        var buffer = new byte[1024];
        var messageBuffer = new List<byte>();
        string receivedString = "";

        try
        {
            DL.Log("ReceiveLoop started");

            while (state == STATES.CONNECTED)
            {
                // Check WebSocket state before attempting to receive
                if (webSocket == null || webSocket.State != WebSocketState.Open)
                {
                    DL.Log($"WebSocket is no longer open (State: {webSocket?.State.ToString() ?? "null"}), exiting receive loop");
                    lock (stateLock)
                    {
                        if (state == STATES.CONNECTED)
                        {
                            state = STATES.CLOSING;
                        }
                    }
                    break;
                }

                try
                {
                    var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        DL.Log("WebSocket close message received from server");
                        lock (stateLock)
                        {
                            state = STATES.CLOSING;
                        }
                        await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
                        break;
                    }
                    else
                    {
                        try
                        {
                            // Accumulate bytes until we have the complete message
                            for (int i = 0; i < result.Count; i++)
                            {
                                messageBuffer.Add(buffer[i]);
                            }

                            _trafficReceived += result.Count;

                            // Only process when we have the complete message
                            if (result.EndOfMessage)
                            {
                                receivedString = Encoding.UTF8.GetString(messageBuffer.ToArray());
                                messageBuffer.Clear();

                                // DL.Big("receivedString: " + receivedString);

                                OnMessageReceived?.Invoke(receivedString);
                            }
                        }
                        catch (Exception invokeEx)
                        {
                            DL.Error($"Error invoking OnMessageReceived event: {invokeEx?.Message ?? "null exception"}");
                            DL.Error($"InvokeEx stack trace: {invokeEx?.StackTrace ?? "no stack trace"}");
                            messageBuffer.Clear(); // Clear buffer on error
                            throw; // Re-throw to be caught by outer exception handler
                        }
                    }

                }
                catch (WebSocketException wsEx)
                {
                    // Only log error if it's not a normal closure scenario
                    if (webSocket != null && webSocket.State != WebSocketState.Closed && webSocket.State != WebSocketState.Aborted)
                    {
                        string wsErrorMessage = wsEx != null ? wsEx.Message : "Unknown WebSocket error (exception was null)";
                        DL.Error($"WebSocket error in receive loop: {wsErrorMessage}");
                    }
                    else
                    {
                        DL.Log("WebSocket connection closed during receive");
                    }
                    lock (stateLock)
                    {
                        state = STATES.ERROR_CONNECTED;
                    }
                    break;
                }
                catch (Exception ex)
                {
                    string errorMessage = ex != null ? ex.Message : "Unknown error (exception was null)";
                    string stackTrace = ex != null ? ex.StackTrace : "No stack trace available";

                    DL.Error($"Unexpected error in receive loop: {errorMessage}");
                    DL.Error($"Stack trace: {stackTrace}");

                    if (!string.IsNullOrEmpty(receivedString))
                    {
                        DL.Error("Last received string: " + receivedString);
                    }
                    lock (stateLock)
                    {
                        state = STATES.ERROR_CONNECTED;
                    }
                    break;
                }

            }
        }
        finally
        {
            lock (stateLock)
            {
                isReceiveLoopRunning = false;
            }
            receiveSemaphore.Release();
            DL.Log("WebSocket receive loop ended");
        }
    }

    #endregion

    #region Classes

    [System.Serializable]
    private class SessionResponse
    {
        public string sessionGUID;
    }
    public struct TrafficStats
    {
        public int sent;
        public int received;
        public int total;
    }

    #endregion
}
