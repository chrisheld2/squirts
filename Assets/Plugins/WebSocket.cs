using System;
using System.Runtime.InteropServices;

public class WebSocket
{
    public static WebSocket Instance;

    public WebSocket()
    {
        Instance = this;
    }

    // Define a delegate for the message received event
    public delegate void MessageReceivedHandler(string message);
    // Define an event based on the delegate
    public event MessageReceivedHandler OnMessageReceivedEvent;

    [DllImport("__Internal")]
    private static extern void WebSocketConnect(string url);

    [DllImport("__Internal")]
    private static extern void WebSocketSend(string message);

    [DllImport("__Internal")]
    private static extern void WebSocketClose();

    public void ReceiveMessage(string message)
    {
        Console.WriteLine("Message received from JavaScript: " + message);
        // Handle the message as needed
    }

    // Call this method to open a WebSocket connection
    public void Connect(string url)
    {
        try
        {
            Console.WriteLine($"Connecting to WebSocket with URL: {url}");
            WebSocketConnect(url);
            Console.WriteLine("WebSocket connected.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error connecting to WebSocket: {ex.Message}");
        }
    }

    // Call this method to send a message through the WebSocket
    public void Send(string message)
    {
        try
        {
            Console.WriteLine($"Sending message: {message}");
            WebSocketSend(message);
            Console.WriteLine("Message sent.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error sending message: {ex.Message}");
        }
    }

    // Call this method to close the WebSocket connection
    public void Close()
    {
        try
        {
            Console.WriteLine("Closing WebSocket...");
            WebSocketClose();
            Console.WriteLine("WebSocket closed.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error closing WebSocket: {ex.Message}");
        }
    }

    // This method will be called by the JavaScript code when a message is received
    public void OnMessageReceived(string message)
    {
        try
        {
            Console.WriteLine($"Message received: {message}");
            // Invoke the event if there are any subscribers
            OnMessageReceivedEvent?.Invoke(message);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error handling received message: {ex.Message}");
        }
    }
}
