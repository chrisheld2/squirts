using UnityEngine;
using TMPro;
using System.Threading.Tasks;

public class USMGame : MonoBehaviour
{
    #region Fields

    private UnifiedSyncMatrix usm;
    private IGameLogic gameLogic;

    // Debug UI References (automatically found)
    private TextMeshProUGUI textSessionGUID;
    private TextMeshProUGUI textClientGUID;
    private TextMeshProUGUI textPool;
    private TextMeshProUGUI textGameSpeed;
    private GameObject panelMainMenuContainer;

    [SerializeField]
    private bool noLevelInit;
    [SerializeField]
    private UnifiedSyncMatrix.GAMEMODE gameMode = UnifiedSyncMatrix.GAMEMODE.SINGLEPLAYER;

    [SerializeField]
    [Range(0f, 1f)]
    private float gameSpeedInspector = 1f;

    // Runtime game speed (loaded from PlayerPrefs)
    private float gameSpeed = 1f;

    // Flag to prevent OnValidate from triggering when loading from PlayerPrefs
    private bool isLoadingFromPrefs = false;

    // Network pause state
    private bool isPaused = false;
    private float preGamePauseSpeed = 1f; // Store speed before pausing

    // Double-tap detection for speed keys
    private float lastMinusKeyTime = -1f;
    private float lastPlusKeyTime = -1f;
    private const float doubleTapThreshold = 0.2f; // Time window for double-tap detection

    #endregion

    #region Properties

    /// <summary>
    /// Gets whether the UnifiedSyncMatrix is in test mode.
    /// </summary>
    public bool NoLevelInit => noLevelInit;

    /// <summary>
    /// Gets the current game mode.
    /// </summary>
    public UnifiedSyncMatrix.GAMEMODE GameMode => gameMode;

    /// <summary>
    /// Gets or sets the game speed (0 to 1).
    /// </summary>
    public float GameSpeed
    {
        get => gameSpeed;
        set
        {
            SetGameSpeed(value, true);
        }
    }

    /// <summary>
    /// Gets whether the game is currently paused.
    /// </summary>
    public bool IsPaused => isPaused;

    /// <summary>
    /// Sets the game speed with optional network synchronization.
    /// </summary>
    /// <param name="value">The new game speed value (0 to 1).</param>
    /// <param name="sendNetworkMessage">Whether to send this change to other clients.</param>
    private void SetGameSpeed(float value, bool sendNetworkMessage)
    {
        gameSpeed = Mathf.Clamp01(value);
        gameSpeedInspector = gameSpeed; // Sync inspector field with runtime value

        // Only apply to Time.timeScale if not paused
        if (!isPaused)
        {
            Time.timeScale = gameSpeed;
        }
        SaveGameSpeed();

        // Update the UI display
        UpdateGameSpeedDisplay();

        // Send network message if in multiplayer mode and requested
        if (sendNetworkMessage && usm != null && usm.IsSessionReady && gameMode != UnifiedSyncMatrix.GAMEMODE.SINGLEPLAYER)
        {
            usm.SendGameMessage("GAME_SPEED", gameSpeed.ToString("F2"));
            DL.Log($"[USMGame] Sent game speed update to network: {gameSpeed:F2}", "cyan");
        }
    }

    /// <summary>
    /// Toggles pause state with optional network synchronization.
    /// </summary>
    /// <param name="sendNetworkMessage">Whether to send this change to other clients.</param>
    private void TogglePause(bool sendNetworkMessage)
    {
        SetPause(!isPaused, sendNetworkMessage);
    }

    /// <summary>
    /// Sets the pause state with optional network synchronization.
    /// </summary>
    /// <param name="pause">The new pause state.</param>
    /// <param name="sendNetworkMessage">Whether to send this change to other clients.</param>
    private void SetPause(bool pause, bool sendNetworkMessage)
    {
        isPaused = pause;

        if (isPaused)
        {
            // Store current speed and pause the game
            preGamePauseSpeed = gameSpeed;
            Time.timeScale = 0f;
            DL.Log("[USMGame] Game paused", "yellow");
        }
        else
        {
            // Resume with the stored speed
            Time.timeScale = gameSpeed;
            DL.Log("[USMGame] Game resumed", "lime");
        }

        // Set network sync pause state
        if (usm != null)
        {
            usm.SetPaused(isPaused);
        }

        // Send network message if in multiplayer mode and requested
        if (sendNetworkMessage && usm != null && usm.IsSessionReady && gameMode != UnifiedSyncMatrix.GAMEMODE.SINGLEPLAYER)
        {
            usm.SendGameMessage("GAME_PAUSE", isPaused.ToString());
            DL.Log($"[USMGame] Sent pause state update to network: {isPaused}", "cyan");
        }
    }
    #endregion

    #region Unity Lifecycle

    void Awake()
    {
        Folders.GAMEZONE.gameObject.SetActive(false);


    }

    void OnEnable()
    {
        // Load saved game speed when component is enabled (handles exiting play mode)
        LoadGameSpeed();
    }

    void Start()
    {
        usm = FindFirstObjectByType<UnifiedSyncMatrix>();

        // Find and cache debug UI text components
        InitUI();

        // Load saved game speed
        LoadGameSpeed();

        // Subscribe to all UnifiedSyncMatrix events
        usm.OnServerIsReady += HandleServerIsReady;
        usm.OnClientJoined += HandleClientJoined;
        usm.OnClientLeft += HandleClientLeft;
        usm.OnClientMessage += HandleClientMessage;
        usm.onGuest_QueryRequest += HandleGuestQueryRequest;
        usm.OnHost_QueryRequest += HandleHostQueryRequest;
        usm.OnGameMessage += HandleGameMessage;

        gameLogic = gameObject.AddComponent<GameMode_3Levels>();

        if (gameMode == UnifiedSyncMatrix.GAMEMODE.SINGLEPLAYER)
        {
            StartSinglePlayer();
        }
    }

    void Update()
    {
        // Game update logic

        // Monitor game speed changes during play mode (but respect pause state)
        if (Application.isPlaying && !isPaused && !Mathf.Approximately(Time.timeScale, gameSpeed))
        {
            Time.timeScale = gameSpeed;
            SaveGameSpeed();
        }

        // Handle keyboard input
        HandleKeyboardInput();
    }

    /// <summary>
    /// Handles all keyboard input for game controls.
    /// </summary>
    private void HandleKeyboardInput()
    {
        // Handle P key to toggle network pause
        if (Input.GetKeyDown(KeyCode.P))
        {
            TogglePause(true);
        }

        // Handle +/= keys to increase game speed by 10% or set to max on double-tap
        if (Input.GetKeyDown(KeyCode.Plus) || Input.GetKeyDown(KeyCode.KeypadPlus) ||
            Input.GetKeyDown(KeyCode.Equals))
        {
            float currentTime = Time.unscaledTime;
            if (currentTime - lastPlusKeyTime <= doubleTapThreshold)
            {
                // Double-tap detected - set to maximum speed
                SetGameSpeed(1f, true);
                DL.Log("[USMGame] Double-tap detected - Game speed set to maximum (1.0)", "lime");
                lastPlusKeyTime = -1f; // Reset to prevent triple-tap
            }
            else
            {
                // Single tap - increase by 10%
                IncreaseGameSpeed();
                lastPlusKeyTime = currentTime;
            }
        }

        // Handle -/_ keys to decrease game speed by 10% or set to min on double-tap
        if (Input.GetKeyDown(KeyCode.Minus) || Input.GetKeyDown(KeyCode.KeypadMinus) ||
            Input.GetKeyDown(KeyCode.Underscore))
        {
            float currentTime = Time.unscaledTime;
            if (currentTime - lastMinusKeyTime <= doubleTapThreshold)
            {
                // Double-tap detected - set to minimum speed
                SetGameSpeed(0.1f, true);
                DL.Log("[USMGame] Double-tap detected - Game speed set to minimum (0.1)", "yellow");
                lastMinusKeyTime = -1f; // Reset to prevent triple-tap
            }
            else
            {
                // Single tap - decrease by 10%
                DecreaseGameSpeed();
                lastMinusKeyTime = currentTime;
            }
        }

        // Handle Escape key to quit the game
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Quit();
        }
    }

    void OnDisable()
    {
        SaveGameSpeed();
    }

    void OnDestroy()
    {
        // Unsubscribe from all events to prevent memory leaks
        if (usm != null)
        {
            usm.OnServerIsReady -= HandleServerIsReady;
            usm.OnClientJoined -= HandleClientJoined;
            usm.OnClientLeft -= HandleClientLeft;
            usm.OnClientMessage -= HandleClientMessage;
            usm.onGuest_QueryRequest -= HandleGuestQueryRequest;
            usm.OnHost_QueryRequest -= HandleHostQueryRequest;
            usm.OnGameMessage -= HandleGameMessage;
        }
    }

    void OnValidate()
    {
        // Load saved game speed from PlayerPrefs first (OnValidate runs before Awake)
        // Only load once at initialization, not when user is changing the value
        if (!isLoadingFromPrefs && PlayerPrefs.HasKey("USMGameSpeed"))
        {
            isLoadingFromPrefs = true;
            float savedSpeed = PlayerPrefs.GetFloat("USMGameSpeed");
            savedSpeed = Mathf.Clamp01(savedSpeed);
            gameSpeedInspector = savedSpeed;
            gameSpeed = savedSpeed;
            return; // Exit early to avoid applying changes
        }

        // Clamp the inspector value to valid range
        gameSpeedInspector = Mathf.Clamp01(gameSpeedInspector);

        // Apply game speed changes in real-time and sync to network
        if (Application.isPlaying)
        {
            // User changed the inspector value during play mode
            // Apply it to runtime and sync to network
            SetGameSpeed(gameSpeedInspector, true);
        }
    }

    #endregion

    #region Event Handlers

    private void HandleServerIsReady(string clientGUID, string sessionGUID)
    {
        DL.Log($"[USMGame] Server is ready - Client GUID: {clientGUID}, Session GUID: {sessionGUID}", "magenta");

        // Update debug UI labels
        UpdateDebugLabels(clientGUID, sessionGUID);

        gameLogic.Init(UnifiedSyncMatrix.GAMEMODE.MULTIPLAYER_HOST);
        // Note: gameLogic.Init() is called in GameMain.cs ServerIsReady -> GameModeInit
        // Don't call it here to avoid double initialization

    }

    private void HandleClientJoined(string clientGUID)
    {
        DL.Log("ClientJoined:" + clientGUID, "magenta");

        // Send current game speed to the new client so they sync up immediately
        if (usm.GameMode == UnifiedSyncMatrix.GAMEMODE.MULTIPLAYER_HOST)
        {
            usm.SendGameMessage("GAME_SPEED", gameSpeed.ToString("F2"));
            DL.Log($"[USMGame] Sent GAME_SPEED ({gameSpeed:F2}) to newly joined client {clientGUID}", "lime");
        }
    }

    private void HandleClientLeft(string clientGUID)
    {
        DL.Log($"[USMGame] Client left: {clientGUID}", "magenta");
    }

    private void HandleClientMessage(NetworkMessage networkMessage)
    {
        Debug.Log($"[USMGame] Client message received: {networkMessage}");
        // Process network messages from other clients
    }

    private void HandleGuestQueryRequest(string queryId, string queryData)
    {
        Debug.Log($"[USMGame] Guest query request - ID: {queryId}, Data: {queryData}");
        // Handle query requests when acting as guest client
    }

    private void HandleHostQueryRequest(string queryData)
    {
        Debug.Log($"[USMGame] Host query request: {queryData}");
        // Handle query requests when acting as host
    }

    private void HandleGameMessage(string messageType, string messageData)
    {
        DL.Log($"[USMGame] Game message - Type: {messageType}, Data: {messageData}", "cyan");

        switch (messageType)
        {
            case "GAME_SPEED":
                HandleGameSpeedMessage(messageData);
                break;
            case "GAME_PAUSE":
                HandleGamePauseMessage(messageData);
                break;
            case "PlayerMove":
                // Handle player movement
                break;
            case "PlayerAction":
                // Handle player actions
                break;
            case "GameState":
                // Handle game state updates
                break;
            default:
                Debug.LogWarning($"[USMGame] Unknown game message type: {messageType}");
                break;
        }
    }

    /// <summary>
    /// Handles incoming game speed synchronization messages from other clients.
    /// </summary>
    /// <param name="speedValue">The game speed value as a string.</param>
    private void HandleGameSpeedMessage(string speedValue)
    {
        if (float.TryParse(speedValue, out float newSpeed))
        {
            // Apply the speed change without sending another network message (to avoid loops)
            SetGameSpeed(newSpeed, false);
            DL.Log($"[USMGame] Received game speed update from network: {newSpeed:F2}", "lime");
        }
        else
        {
            Debug.LogWarning($"[USMGame] Failed to parse game speed value: {speedValue}");
        }
    }

    /// <summary>
    /// Handles incoming game pause synchronization messages from other clients.
    /// </summary>
    /// <param name="pauseValue">The pause state value as a string.</param>
    private void HandleGamePauseMessage(string pauseValue)
    {
        if (bool.TryParse(pauseValue, out bool newPauseState))
        {
            // Apply the pause change without sending another network message (to avoid loops)
            SetPause(newPauseState, false);
            DL.Log($"[USMGame] Received pause state update from network: {newPauseState}", "lime");
        }
        else
        {
            Debug.LogWarning($"[USMGame] Failed to parse pause state value: {pauseValue}");
        }
    }
    #endregion

    #region UI
    /// <summary>
    /// Finds and caches references to debug UI text components by name.
    /// </summary>
    private void InitUI()
    {
        // Find PanelDebug GameObject

        var panelDebug = GameObject.Find("PanelDebug");


        panelMainMenuContainer = GameObject.Find("PanelMainMenuContainer");
        Transform sessionGUIDTransform = panelDebug.transform.Find("TextSessionGUID");
        textSessionGUID = sessionGUIDTransform.GetComponent<TextMeshProUGUI>();

        Transform clientGUIDTransform = panelDebug.transform.Find("TextClientGUID");
        textClientGUID = clientGUIDTransform.GetComponent<TextMeshProUGUI>();

        Transform poolTransform = panelDebug.transform.Find("TextPool");
        textPool = poolTransform.GetComponent<TextMeshProUGUI>();

        // Find PanelGameToolBar GameObject
        var panelGameToolBar = GameObject.Find("PanelGameToolBar");
        if (panelGameToolBar != null)
        {
            Transform gameSpeedTransform = panelGameToolBar.transform.Find("TextGameSpeed");
            if (gameSpeedTransform != null)
            {
                textGameSpeed = gameSpeedTransform.GetComponent<TextMeshProUGUI>();
            }
        }

        // Initialize game speed display
        UpdateGameSpeedDisplay();
    }

    /// <summary>
    /// Updates the debug panel labels with current session information.
    /// </summary>
    private void UpdateDebugLabels(string clientGUID, string sessionGUID)
    {
        if (textSessionGUID != null)
        {
            textSessionGUID.text = $"Session GUID: {sessionGUID}";
        }

        if (textClientGUID != null)
        {
            textClientGUID.text = $"Client GUID: {clientGUID}";
        }

        if (textPool != null)
        {
            // You can add pool-related info here if needed
            textPool.text = $"Pool: Ready";
        }
    }

    /// <summary>
    /// Updates the game speed display in the toolbar.
    /// </summary>
    private void UpdateGameSpeedDisplay()
    {
        if (textGameSpeed != null)
        {
            textGameSpeed.text = $"Speed: {gameSpeed:F1}x";
        }
    }

    /// <summary>
    /// Loads the saved game speed from PlayerPrefs.
    /// </summary>
    private void LoadGameSpeed()
    {
        if (PlayerPrefs.HasKey("USMGameSpeed"))
        {
            gameSpeed = PlayerPrefs.GetFloat("USMGameSpeed");
            gameSpeed = Mathf.Clamp01(gameSpeed);
            Time.timeScale = gameSpeed;

            // Sync the inspector field with the loaded value
            gameSpeedInspector = gameSpeed;
        }
        else
        {
            // No saved value, use the inspector's default value
            gameSpeed = gameSpeedInspector;
            Time.timeScale = gameSpeed;
        }

        // Update the UI display
        UpdateGameSpeedDisplay();
    }

    /// <summary>
    /// Saves the current game speed to PlayerPrefs.
    /// </summary>
    private void SaveGameSpeed()
    {
        PlayerPrefs.SetFloat("USMGameSpeed", gameSpeed);
        PlayerPrefs.Save();
    }

    #endregion

    #region Game Control

    /// <summary>
    /// Increases the game speed by 10%.
    /// </summary>
    private void IncreaseGameSpeed()
    {
        float newSpeed = gameSpeed + 0.1f;
        newSpeed = Mathf.Round(newSpeed * 10f) / 10f; // Round to 1 decimal place
        SetGameSpeed(newSpeed, true);
        DL.Log($"[USMGame] Game speed increased to {gameSpeed:F1}", "lime");
    }

    /// <summary>
    /// Decreases the game speed by 10%.
    /// </summary>
    private void DecreaseGameSpeed()
    {
        float newSpeed = gameSpeed - 0.1f;
        newSpeed = Mathf.Round(newSpeed * 10f) / 10f; // Round to 1 decimal place
        SetGameSpeed(newSpeed, true);
        DL.Log($"[USMGame] Game speed decreased to {gameSpeed:F1}", "yellow");
    }

    public void StartSinglePlayer()
    {
        gameMode = UnifiedSyncMatrix.GAMEMODE.SINGLEPLAYER;

        Folders.GAMEZONE.gameObject.SetActive(true);
        panelMainMenuContainer.SetActive(false);

        usm.Init(gameMode);

        // Initialize game logic for single-player mode
        // (multiplayer initialization happens via OnServerIsReady event in GameMain.cs)
        gameLogic.Init(gameMode);

    }

    public async void StartMultiplayerAsHost()
    {
        gameMode = UnifiedSyncMatrix.GAMEMODE.MULTIPLAYER_HOST;

        Folders.GAMEZONE.gameObject.SetActive(true);


        usm.Init(gameMode); // Initialize network for multiplayer
        await usm.GameConnect("*NEW*");


    }

    public async void StartMultiplayerAsClient(string sessionGUID)
    {
        gameMode = UnifiedSyncMatrix.GAMEMODE.MULTIPLAYER_GUEST;

        Folders.GAMEZONE.gameObject.SetActive(true);

        usm.Init(gameMode);
        await usm.GameConnect(sessionGUID);


    }

    /// <summary>
    /// Public method to quit the game with proper cleanup.
    /// Closes network connections, saves state, and exits the application.
    /// </summary>
    public async void Quit()
    {
        await ShutdownGame();
    }

    private async Task ShutdownGame()
    {
        DL.Log("[USMGame] Shutting down game...", "yellow");

        // Save game speed before closing
        SaveGameSpeed();

        // Close network connections
        if (usm != null)
        {
            await usm.Close();
            DL.Log("[USMGame] Network connections closed", "lime");
        }

#if UNITY_EDITOR
        // Stop playing in the Unity Editor
        UnityEditor.EditorApplication.isPlaying = false;
#elif UNITY_WEBGL
        // For WebGL, close the browser tab/window
        Application.ExternalEval("window.close();");
#else
        // For standalone builds, quit the application
        Application.Quit();
#endif
    }

    #endregion
}

