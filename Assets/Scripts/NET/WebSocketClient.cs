using System;
using UnityEngine;

public class WebSocketClient : MonoBehaviour
{
    #region Serialized Fields
    [SerializeField] private string url;
    #endregion

    #region Private Variables
    private WebSocket webSocket;
    #endregion

    #region Unity Lifecycle
    private void Start()
    {
        InitializeWebSocket();
    }

    private void Update()
    {
        // Reserved for future input handling
        // Currently no update logic needed
    }

    private void OnDestroy()
    {
        CloseWebSocket();
    }
    #endregion

    #region WebSocket Management
    private void InitializeWebSocket()
    {
        try
        {
            Debug.Log("Initializing WebSocket...");
            webSocket = new WebSocket();
            webSocket.OnMessageReceivedEvent += OnMessageReceived;

            Debug.Log($"Connecting to URL: {url}");
            webSocket.Connect(url);
            Debug.Log("WebSocket connected.");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error during WebSocket initialization: {ex.Message}");
        }
    }

    private void CloseWebSocket()
    {
        try
        {
            if (webSocket != null)
            {
                Debug.Log("Closing WebSocket...");
                webSocket.OnMessageReceivedEvent -= OnMessageReceived;
                webSocket.Close();
                webSocket = null;
                Debug.Log("WebSocket closed.");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error closing WebSocket: {ex.Message}");
        }
    }
    #endregion

    #region Event Handlers
    private void OnMessageReceived(string message)
    {
        try
        {
            Debug.Log($"Message received: {message}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error handling received message: {ex.Message}");
        }
    }
    #endregion

    #region Public Methods
    public void SendMessage(string message)
    {
        try
        {
            if (webSocket != null)
            {
                Debug.Log($"Sending message: {message}");
                webSocket.Send(message);
            }
            else
            {
                Debug.LogWarning("WebSocket is not initialized. Cannot send message.");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error sending message: {ex.Message}");
        }
    }
    #endregion
}
