using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Random = UnityEngine.Random;

public class GameMode_3Levels : MonoBehaviour, IGameLogic
{
    #region Fields

    private UnifiedSyncMatrix usm;
    private LevelScript levelScript;
    private GameObject player;
    private int currentLevel;
    private GameObject ship;
    private UnifiedSyncMatrix.GAMEMODE gameMode = UnifiedSyncMatrix.GAMEMODE.SINGLEPLAYER;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        DL.Log("Levels3 Awake called", "blue");
    }

    private void Update()
    {

        if (Input.GetKeyDown(KeyCode.R) && (usm.GameMode == UnifiedSyncMatrix.GAMEMODE.MULTIPLAYER_HOST || gameMode == UnifiedSyncMatrix.GAMEMODE.SINGLEPLAYER))
        {
            int seed = GenerateLevel();

            // Only send network messages if in multiplayer host mode
            if (usm != null && !usm.GameMode.Equals(UnifiedSyncMatrix.GAMEMODE.SINGLEPLAYER))
            {
                Vector2 startPos = levelScript.positionStartGet();
                int width = levelScript.widthGet();
                int height = levelScript.heightGet();
                usm.SendGameMessage("NewLevelSeed", $"{seed},{width},{height}");
            }

            RepositionPlayerToStart();

            usm.NodeScan();
            // RepositionShipToStart();

        }
    }

    private void OnDestroy()
    {
        if (usm != null)
        {
            usm.OnGameMessage -= OnGameMessageReceived;
            usm.OnClientJoined -= OnClientJoinedHandler;
        }
    }

    #endregion

    #region Initialization

    public void Init(UnifiedSyncMatrix.GAMEMODE gameMode)
    {
        this.gameMode = gameMode;

        // Get required components with validation
        levelScript = Folders.LEVEL.GetComponent<LevelScript>();
        if (levelScript == null)
        {
            DL.Log("LevelScript not found on LEVEL folder", "red");
            return;
        }

        usm = FindFirstObjectByType<UnifiedSyncMatrix>();
        if (usm == null)
        {
            DL.Log("UnifiedSyncMatrix not found in scene", "red");
            return;
        }

        if (gameMode != UnifiedSyncMatrix.GAMEMODE.SINGLEPLAYER)
        {
            usm.OnClientJoined += OnClientJoinedHandler;
            usm.OnGameMessage += OnGameMessageReceived;
        }




        var usmGame = FindFirstObjectByType<USMGame>();
        if (!usmGame.TestMode)
        {
            GenerateLevel();

            SpawnAndPositionPlayerByLevelStart();

        }
        else
        {

            var TESTING = GameObject.Find("GAMEZONE/LEVEL/TESTING");
            if (TESTING != null)
                TESTING.SetActive(true);

            if (gameMode == UnifiedSyncMatrix.GAMEMODE.SINGLEPLAYER)
                SpawnAndPositionPlayerByIndex(0);
            else
                SpawnAndPositionPlayerByClientIndex();
        }

        // Handle mode-specific initialization





    }

    private void SpawnAndPositionPlayerByIndex(int playerIndex)
    {
        // Find all PlayerStart objects in the scene
        PlayerStart[] playerStarts = FindObjectsByType<PlayerStart>(FindObjectsSortMode.None);

        PlayerStart matchingStart = null;
        foreach (PlayerStart start in playerStarts)
        {
            if (start.PlayerIndex - 1 == playerIndex)
            {
                matchingStart = start;
                break;
            }
        }

        Vector2 startPosition = Vector2.zero;
        if (matchingStart != null)
        {
            startPosition = (Vector2)matchingStart.transform.localPosition + new Vector2(Random.Range(-0.5f, 0.5f), 0);
            DL.Log($"Player positioned at PlayerStart with index {playerIndex}", "cyan");
        }
        else
        {
            DL.Warning($"PlayerStart with index {playerIndex} not found in scene");
        }

        // Spawn player at the determined start position
        player = SpawnPlayer(true, startPosition);
    }

    private void SpawnAndPositionPlayerByClientIndex()
    {
        if (usm == null)
        {
            DL.Log("SpawnAndPositionPlayerByClientIndex: syncMatrix is null", "red");
            return;
        }

        // Get the current client's index (0-based)
        int clientIndex = usm.CurrentClientIndex;

        if (clientIndex == -1)
        {
            DL.Warning("SpawnAndPositionPlayerByClientIndex: Client index not found, using default position");
            SpawnAndPositionPlayerByIndex(0);
            return;
        }

        DL.Log($"Spawning player at PlayerStart with index {clientIndex} (Client Index: {clientIndex}, Total Clients: {usm.ConnectedClientCount})", "cyan");

        SpawnAndPositionPlayerByIndex(clientIndex);
    }

    private void SpawnAndPositionPlayerByLevelStart()
    {
        SpawnPlayer(true, levelScript.positionStartGet() * 5 + new Vector2(4f + Random.Range(-0.5f, 0.5f), .5f));


    }

    private void PoolInit()
    {

        PoolScript.Instance.Preload("Flare", 6);

        DL.Log("Pooled GameObjects: " + PoolScript.Instance.GetTotalCount().ToString(), "yellow", false);


    }

    #endregion

    #region Network Events

    private void OnClientJoinedHandler(string clientGUID)
    {
        if (usm.GameMode == UnifiedSyncMatrix.GAMEMODE.MULTIPLAYER_HOST)
        {
            int currentSeed = levelScript.randomSeedGet;
            int width = levelScript.widthGet();
            int height = levelScript.heightGet();
            usm.SendGameMessage("NewLevelSeed", $"{currentSeed},{width},{height}");
        }
    }

    public void OnGameMessageReceived(string message, string value)
    {
        switch (message)
        {
            case "NewLevelSeed":
                // Check if we're in test mode - if so, skip level generation
                var usmGame = FindFirstObjectByType<USMGame>();
                if (usmGame != null && usmGame.TestMode)
                {
                    DL.Log("TestMode is enabled - skipping level generation from network message", "yellow");
                    return;
                }

                string[] seedData = value.Split(',');
                int newSeed = int.Parse(seedData[0]);
                int width = int.Parse(seedData[1]);
                int height = int.Parse(seedData[2]);

                levelScript.CreateWithNewSeed(newSeed, width, height);
                ReplaceSideCellWithLever();
                levelScript.RenderLevel();

                DL.Log($"LEVEL is active: {Folders.LEVEL.gameObject.activeSelf}", "blue");

                RepositionPlayerToStart();
                // RepositionShipToStart();

                break;


        }
    }

    // int IGameLogic.ClientJoined()
    // {
    //     if (usm.GameMode == UnifiedSyncMatrix.GAMEMODE.MULTIPLAYER_HOST)
    //     {
    //         int currentSeed = levelScript.randomSeedGet;
    //         int width = levelScript.widthGet();
    //         int height = levelScript.heightGet();
    //         usm.SendGameMessage("NewLevelSeed", $"{currentSeed},{width},{height}");

    //         return currentSeed;
    //     }

    //     return 0;
    // }

    #endregion

    #region Level Generation

    public int GenerateLevel()
    {
        int currentWidth = levelScript.widthGet();
        int currentHeight = levelScript.heightGet();

        int newSeed = levelScript.CreateWithNewSeed(0, currentWidth, currentHeight);
        ReplaceSideCellWithLever();
        levelScript.RenderLevel();

        return newSeed;

    }

    private void ReplaceSideCellWithLever()
    {
        if (levelScript == null)
        {
            DL.Log("LevelScript is null, cannot replace side cell with lever", "red");
            return;
        }

        // Safety check: ensure grid is initialized
        if (levelScript.grid == null)
        {
            DL.Log("LevelScript grid is null, cannot replace side cell with lever", "red");
            return;
        }

        // Get the level grid and dimensions
        int width = levelScript.widthGet();
        int height = levelScript.heightGet();

        // Safety check: ensure dimensions are valid
        if (width <= 2 || height <= 2)
        {
            DL.Log("Level dimensions too small to place lever", "yellow");
            return;
        }

        // Safety check: verify grid dimensions match
        if (levelScript.grid.GetLength(0) != height || levelScript.grid.GetLength(1) != width)
        {
            DL.Log("Grid dimensions don't match level dimensions", "red");
            return;
        }

        // Find a suitable side cell to replace with a lever
        int attempts = 0;
        int maxAttempts = width * height; // Avoid infinite loops

        while (attempts < maxAttempts)
        {
            attempts++;
            int x = Random.Range(1, width - 1);
            int y = Random.Range(1, height - 1);

            if (IsALeftOrRightCell(x, y))
            {
                levelScript.grid[y, x] = MazeGenerator.MazeCell.LEVER;
                DL.Log($"Placed lever at ({x}, {y})", "green");
                return; // Exit after placing one lever
            }
        }

        DL.Log("Failed to find a suitable cell for the lever after " + maxAttempts + " attempts.", "yellow");
    }

    private bool IsALeftOrRightCell(int x, int y)
    {
        // Safety check: ensure levelScript and grid are valid
        if (levelScript == null || levelScript.grid == null)
            return false;

        // Safety check: ensure coordinates are within bounds
        int width = levelScript.widthGet();
        int height = levelScript.heightGet();

        if (x <= 0 || x >= width - 1 || y < 0 || y >= height)
            return false;

        // Additional safety check for grid dimensions
        if (y >= levelScript.grid.GetLength(0) || x >= levelScript.grid.GetLength(1))
            return false;

        // Check 4-directional neighbors
        // bool hasTop = IsNeighborSolidForType(x, y + 1, grid, width, height);
        // bool hasBottom = IsNeighborSolidForType(x, y - 1, grid, width, height);
        if (levelScript.grid[y, x] == MazeGenerator.MazeCell.EMPTY) return false;

        bool hasLeft = levelScript.grid[y, x - 1] == MazeGenerator.MazeCell.SOLID;
        bool hasRight = levelScript.grid[y, x + 1] == MazeGenerator.MazeCell.SOLID;

        return (!hasLeft && hasRight) || (hasLeft && !hasRight);

    }

    #endregion

    #region Spawning

    private GameObject SpawnPlayer(bool isLocalPlayer = true, Vector2 position = default)
    {
        // Use UnifiedSyncMatrix to instantiate the networked player
        player = usm.InstantiateNetworkedObject(
            "Player",
            position,
            Folders.PLAYERS,
            forceIsLive: isLocalPlayer
        );

        if (player == null)
        {
            DL.Log("Failed to spawn player", "red");
            return null;
        }

        if (isLocalPlayer)
        {
            var mainCamera = GameObject.Find("Main Camera");
            if (mainCamera.TryGetComponent(out CameraSway cameraSwayComponent))
                cameraSwayComponent.target = player.transform;
        }

        return player;
    }

    private void SpawnShip()
    {
        if (gameMode == UnifiedSyncMatrix.GAMEMODE.SINGLEPLAYER)
        {
            GameObject shipPrefab = Helpers.LoadGameObjectFromResources("Ship");
            ship = Instantiate(shipPrefab, Vector2.zero, Quaternion.identity);
            ship.name = ship.name.Replace("(Clone)", "").Trim();
        }
        else
        {
            // ship = netManager.GameObject_Spawn("Ship", GetPlayerStartingPosition(), netManager.IsTheHost);
        }


    }

    #endregion

    #region Positioning & Accessors

    public GameObject GetPlayer()
    {
        return player;
    }

    public Vector2 GetPlayerStartingPosition()
    {
        return levelScript.positionStartGet();


    }

    public void RepositionPlayerToStart()
    {
        if (player != null && levelScript != null)
        {
            if (player.TryGetComponent(out Rigidbody2D playerRb))
                playerRb.linearVelocity = Vector2.zero; // Reset player velocity
            player.transform.position = levelScript.positionStartGet() * 5;
            player.transform.position += new Vector3(4f + Random.Range(-0.5f, 0.5f), .5f, 0);
        }
    }

    public void RepositionShipToStart()
    {
        if (ship != null && levelScript != null)
        {
            ship.transform.position = levelScript.positionEndGet() * 5 + new Vector2(0, 3); // Slightly above ground
        }
    }

    #endregion

}
