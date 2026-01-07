using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SessionKeyScript : MonoBehaviour
{
    #region Serialized Fields

    [SerializeField]
    private USMGame usmGame;

    [SerializeField]
    private PanelMainMenuContainerScript menuContainer;

    [SerializeField]
    private Button buttonCancel;

    [SerializeField]
    private Button buttonConnect;

    #endregion

    #region Private Fields

    private string sessionKey;
    private TMP_InputField inputFieldSessionKey;

    #endregion

    #region Unity Lifecycle

    void Start()
    {
        if (transform.Find("InputFieldSessionKey").TryGetComponent(out TMP_InputField inputField))
        {
            inputFieldSessionKey = inputField;
        }

        sessionKey = PlayerPrefs.GetString("sessionKey", "");

        // Only populate UI if input field was found
        if (inputFieldSessionKey != null)
        {
            SessionKeyGet();
        }

        SetupButtonListeners();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            OnCancelClicked();
        }
        else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            OnConnectClicked();
        }
    }

    #endregion

    #region Initialization

    private void SetupButtonListeners()
    {
        if (buttonCancel != null)
            buttonCancel.onClick.AddListener(OnCancelClicked);
        if (buttonConnect != null)
            buttonConnect.onClick.AddListener(OnConnectClicked);
    }

    #endregion

    #region Button Event Handlers

    private void OnCancelClicked()
    {
        if (menuContainer != null)
        {
            menuContainer.HideSessionKeyPanel();
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    private void OnConnectClicked()
    {
        if (usmGame == null)
        {
            Debug.LogError("[SessionKeyScript] USMGame reference is missing!");
            return;
        }

        if (inputFieldSessionKey == null)
        {
            Debug.LogError("[SessionKeyScript] Input field reference is missing!");
            return;
        }

        if (string.IsNullOrWhiteSpace(inputFieldSessionKey.text))
        {
            Debug.LogWarning("[SessionKeyScript] Session key cannot be empty!");
            return;
        }

        SessionKeySet();
        usmGame.StartMultiplayerAsClient(sessionKey);
    }

    #endregion

    #region Session Key Management

    public void SessionKeyGet()
    {
        if (inputFieldSessionKey != null && !string.IsNullOrEmpty(sessionKey))
        {
            inputFieldSessionKey.text = sessionKey;
        }
    }

    public void SessionKeySet()
    {
        sessionKey = inputFieldSessionKey.text.ToUpper();

        // Update UI to show uppercase version for visual consistency
        inputFieldSessionKey.text = sessionKey;

        PlayerPrefs.SetString("sessionKey", sessionKey);
        PlayerPrefs.Save();
    }

    #endregion
}
