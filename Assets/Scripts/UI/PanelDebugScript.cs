using UnityEngine;

public class PanelDebugScript : MonoBehaviour
{
    private TMPro.TextMeshProUGUI textSessionGUID;
    private TMPro.TextMeshProUGUI textClientGUID;

    void Start()
    {
        if (transform.Find("TextSessionGUID").TryGetComponent(out TMPro.TextMeshProUGUI sessionText))
            textSessionGUID = sessionText;
        if (transform.Find("TextClientGUID").TryGetComponent(out TMPro.TextMeshProUGUI clientText))
            textClientGUID = clientText;

    }

    public void SessionGUIDSet(string guid)
    {
        textSessionGUID.text = "SessionGUID: " + guid;
    }

    public void ClientGUIDSet(string guid)
    {
        textClientGUID.text = "ClientGUID: " + guid;
    }

}
