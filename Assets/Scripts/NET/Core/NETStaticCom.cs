using UnityEngine;

public class NETStaticCom : MonoBehaviour
{
    private void Awake()
    {
        name = "NETStaticCom";
        DontDestroyOnLoad(gameObject);
    }

    public void HandleMessageReceived(string data)
    {
        if (WebSocket.Instance != null)
        {
            WebSocket.Instance.OnMessageReceived(data);
        }
        else
        {
            Debug.LogError("NETStaticCom: WebSocket Instance is null!");
        }
    }
}
