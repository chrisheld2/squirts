using UnityEngine;
using System.Runtime.InteropServices;

public class DL : MonoBehaviour
{
    private static bool debugOn = true;

    public static bool DebugOn
    {
        get
        {
            return debugOn;
        }
        set
        {
            debugOn = value;
            Debug.Log($"DebugOn set to: {debugOn}");
        }
    }

    public static void Error(string message)
    {
        Log(message, "red", true, true);
    }

    public static void Warning(string message)
    {
        Log(message, "yellow", true, true);
    }

    public static void Info(string message)
    {
        Log(message, "orange", true, true);
    }

    public static void Big(string message)
    {
        Log(message, "green", true, true, 16);
    }

    [DllImport("__Internal")]
    private static extern void ConsoleLog(string str);

    private static string EscapeJavaScriptString(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        var sb = new System.Text.StringBuilder(input.Length);
        foreach (char c in input)
        {
            switch (c)
            {
                case '\\': sb.Append("\\\\"); break;
                case '\'': sb.Append("\\'"); break;
                case '"': sb.Append("\\\""); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                case '\b': sb.Append("\\b"); break;
                case '\f': sb.Append("\\f"); break;
                default:
                    // Handle other control characters
                    if (c < 32)
                        sb.Append($"\\u{((int)c):x4}");
                    else
                        sb.Append(c);
                    break;
            }
        }
        return sb.ToString();
    }

    private static string GetBrowserColor(string unityColor)
    {
        return unityColor switch
        {
            "red" => "#ff0000",
            "yellow" => "#ffff00",
            "orange" => "#ffa500",
            "green" => "#00ff00",
            "white" => "#ffffff",
            _ => "#ffffff"
        };
    }

    public static void Log(string message, string color = "white", bool bold = false, bool italic = false, int size = 13)
    {
        if (!debugOn) return;

#if UNITY_WEBGL && !UNITY_EDITOR
        // Browser console formatting
        string browserColor = GetBrowserColor(color);
        string style = $"color: {browserColor};";
        
        if (bold) style += " font-weight: bold;";
        if (italic) style += " font-style: italic;";
        if (size > 13) style += $" font-size: {size}px;";
        
        try
        {
            // Manually escape the message for JavaScript string literal
            string escapedMessage = EscapeJavaScriptString(message);
            string escapedStyle = EscapeJavaScriptString(style);
            ConsoleLog($"console.log('%c{escapedMessage}', '{escapedStyle}');");
        }
        catch
        {
            Debug.Log(message);
        }
#else
        // Unity editor rich text formatting
        string formattedMessage = $"<color={color}>{message}</color>";

        if (bold)
        {
            formattedMessage = $"<b>{formattedMessage}</b>";
        }

        if (italic)
        {
            formattedMessage = $"<i>{formattedMessage}</i>";
        }

        if (size > 0)
        {
            formattedMessage = $"<size={size}>{formattedMessage}</size>";
        }

        Debug.Log(formattedMessage);
#endif
    }
}