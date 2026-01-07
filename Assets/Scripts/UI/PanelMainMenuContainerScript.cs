using UnityEngine;
using UnityEngine.UI;
using System.IO;
using TMPro;

public class PanelMainMenuContainerScript : MonoBehaviour
{
    [Header("Game")]
    [SerializeField]
    private USMGame game;

    [Header("Fields - Main Menu")]
    [SerializeField]
    private Button buttonSinglePlayer;
    [SerializeField]
    private Button buttonMultiPlayer;
    [SerializeField]
    private Button buttonSettings;
    [SerializeField]
    private Button buttonCredits;

    [Header("Fields - Multiplayer")]
    [SerializeField]
    private GameObject panelMultiplayer;
    [SerializeField]
    private GameObject buttonHost;
    [SerializeField]
    private GameObject buttonJoin;
    [SerializeField]
    private GameObject buttonBack;

    [Header("Fields - Panels")]
    [SerializeField]
    private GameObject panelMain;
    [SerializeField]
    private GameObject panelSettings;
    [SerializeField]
    private GameObject panelCredits;
    [SerializeField]
    private GameObject panelSessionKey;

    [Header("Fields - Common")]
    [SerializeField]
    private Button buttonQuit;
    [SerializeField]
    private TextMeshProUGUI textBuildNumber;


    private enum MenuState
    {
        MainMenu,
        MultiPlayer,
        Settings,
        Credits
    }
    private MenuState menuState;

    void Start()
    {
        menuState = MenuState.MainMenu;
        LoadAndDisplayBuildNumber();
        SetupButtonListeners();
        ShowMainMenu();
    }

    private void SetupButtonListeners()
    {
        // Main Menu Buttons
        if (buttonSinglePlayer != null)
            buttonSinglePlayer.onClick.AddListener(OnSinglePlayerClicked);
        if (buttonMultiPlayer != null)
            buttonMultiPlayer.onClick.AddListener(OnMultiPlayerClicked);
        if (buttonSettings != null)
            buttonSettings.onClick.AddListener(OnSettingsClicked);
        if (buttonCredits != null)
            buttonCredits.onClick.AddListener(OnCreditsClicked);
        if (buttonQuit != null)
            buttonQuit.onClick.AddListener(OnQuitClicked);

        // Multiplayer Buttons
        if (buttonHost != null && buttonHost.TryGetComponent(out Button hostBtn))
            hostBtn.onClick.AddListener(OnHostClicked);
        if (buttonJoin != null && buttonJoin.TryGetComponent(out Button joinBtn))
            joinBtn.onClick.AddListener(OnJoinClicked);
        if (buttonBack != null && buttonBack.TryGetComponent(out Button backBtn))
            backBtn.onClick.AddListener(OnBackClicked);
    }

    private void LoadAndDisplayBuildNumber()
    {
        string buildNumberPath = "BuildNumber";
        TextAsset buildNumberAsset = Resources.Load<TextAsset>(buildNumberPath);

        if (buildNumberAsset != null && textBuildNumber != null)
        {
            textBuildNumber.text = "Build: " + buildNumberAsset.text;
        }
        else if (textBuildNumber != null)
        {
            textBuildNumber.text = "Build: Unknown";
        }
    }

    void Update()
    {
        switch (menuState)
        {
            case MenuState.MainMenu:
                if (Input.GetKeyDown(KeyCode.S))
                {
                    buttonSinglePlayer.onClick.Invoke();
                }
                else if (Input.GetKeyDown(KeyCode.M))
                {
                    menuState = MenuState.MultiPlayer;
                    buttonMultiPlayer.onClick.Invoke();
                }
                else if (Input.GetKeyDown(KeyCode.T))
                {
                    menuState = MenuState.Settings;
                    buttonSettings.onClick.Invoke();
                }
                else if (Input.GetKeyDown(KeyCode.C))
                {
                    buttonCredits.onClick.Invoke();
                }
                else if (Input.GetKeyDown(KeyCode.Q))
                {
                    buttonQuit.onClick.Invoke();
                }

                break;
            case MenuState.MultiPlayer:

                if (Input.GetKeyDown(KeyCode.H))
                {
                    if (buttonHost.TryGetComponent(out Button hostButton))
                        hostButton.onClick.Invoke();
                }
                else if (Input.GetKeyDown(KeyCode.J))
                {
                    if (buttonJoin.TryGetComponent(out Button joinButton))
                        joinButton.onClick.Invoke();
                }
                else if (Input.GetKeyDown(KeyCode.B))
                {
                    menuState = MenuState.MainMenu;
                    if (buttonBack.TryGetComponent(out Button backButton))
                        backButton.onClick.Invoke();
                }
                else if (Input.GetKeyDown(KeyCode.Q))
                {
                    buttonQuit.onClick.Invoke();
                }

                break;
            case MenuState.Settings:

                break;
            case MenuState.Credits:

                break;
        }
    }

    #region Button Click Handlers

    private void OnSinglePlayerClicked()
    {
        if (game != null)
        {
            game.StartSinglePlayer();
        }
    }

    private void OnMultiPlayerClicked()
    {
        menuState = MenuState.MultiPlayer;
        ShowMultiplayerMenu();
    }

    private void OnSettingsClicked()
    {
        menuState = MenuState.Settings;
        ShowSettingsMenu();
    }

    private void OnCreditsClicked()
    {
        menuState = MenuState.Credits;
        ShowCreditsMenu();
    }

    private void OnQuitClicked()
    {
        if (game != null)
        {
            game.Quit();
        }
    }

    private void OnHostClicked()
    {
        if (game != null)
        {
            game.StartMultiplayerAsHost();
        }
    }

    private void OnJoinClicked()
    {
        ShowSessionKeyPanel();
    }

    private void OnBackClicked()
    {
        menuState = MenuState.MainMenu;
        ShowMainMenu();
    }

    #endregion

    #region Menu Display Methods

    private void ShowMainMenu()
    {
        if (panelMain != null) panelMain.SetActive(true);
        if (panelMultiplayer != null) panelMultiplayer.SetActive(false);
        if (panelSettings != null) panelSettings.SetActive(false);
        if (panelCredits != null) panelCredits.SetActive(false);
        if (panelSessionKey != null) panelSessionKey.SetActive(false);
    }

    private void ShowMultiplayerMenu()
    {
        if (panelMain != null) panelMain.SetActive(false);
        if (panelMultiplayer != null) panelMultiplayer.SetActive(true);
        if (panelSettings != null) panelSettings.SetActive(false);
        if (panelCredits != null) panelCredits.SetActive(false);
        if (panelSessionKey != null) panelSessionKey.SetActive(false);
    }

    private void ShowSettingsMenu()
    {
        if (panelMain != null) panelMain.SetActive(false);
        if (panelMultiplayer != null) panelMultiplayer.SetActive(false);
        if (panelSettings != null) panelSettings.SetActive(true);
        if (panelCredits != null) panelCredits.SetActive(false);
        if (panelSessionKey != null) panelSessionKey.SetActive(false);
    }

    private void ShowCreditsMenu()
    {
        if (panelMain != null) panelMain.SetActive(false);
        if (panelMultiplayer != null) panelMultiplayer.SetActive(false);
        if (panelSettings != null) panelSettings.SetActive(false);
        if (panelCredits != null) panelCredits.SetActive(true);
        if (panelSessionKey != null) panelSessionKey.SetActive(false);
    }

    private void ShowSessionKeyPanel()
    {
        if (panelSessionKey != null) panelSessionKey.SetActive(true);
    }

    public void HideSessionKeyPanel()
    {
        if (panelSessionKey != null) panelSessionKey.SetActive(false);
    }

    #endregion
}
