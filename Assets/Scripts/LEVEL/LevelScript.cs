using System;
using System.Collections;
using NUnit.Framework.Constraints;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Random = UnityEngine.Random;

public class LevelScript : MonoBehaviour
{
    #region Serialized Fields

    [Header("CORE REFERENCES")]
    [SerializeField] private GameObject pool;

    [Header("LEVEL DIMENSIONS")]
    [SerializeField][Range(10, 200)] private int width = 10;
    [SerializeField][Range(10, 200)] private int height = 10;
    [SerializeField][Range(3, 12)] private int pathWidth = 3;
    [SerializeField][Range(1, 10)] private int secondaryPathWidth = 3;

    [Header("PERLIN NOISE GENERATION")]
    [SerializeField][Range(0, 10000)] private int randomSeed = 1234;
    [SerializeField][Range(0, 1)] private float perlinScale = .5f;
    [SerializeField][Range(0, 1)] private float perlinThreshold = .5f;
    [SerializeField][Range(0.01f, 2f)] private float perlinWidth = 0.1f;
    [SerializeField][Range(0.01f, 2f)] private float perlinHeight = 0.1f;

    [Header("DECORATION SETTINGS")]
    [Space(10)]
    [Header("Spawn Chances")]
    [SerializeField][Range(0, 1)] private float grassChance = .01f;
    [SerializeField][Range(0, 1)] private float grassCoverChance = .01f;
    [SerializeField][Range(0, 1)] private float mushroomChance = .01f;
    [SerializeField][Range(0, 1)] private float stoneChance = .01f;
    [SerializeField][Range(0, 1)] private float webChance = .01f;
    [SerializeField][Range(0, 1)] private float gemChance = .01f;
    [Space(10)]
    [Header("Patch Sizes")]
    [SerializeField][Range(2, 12)] private int decorationPatchMinSize = 2;
    [SerializeField][Range(2, 12)] private int decorationPatchMaxSize = 4;

    [Header("BACKGROUND BLOCK SETTINGS")]
    [Space(10)]
    [Header("Distance")]
    [SerializeField][Range(1, 10)] private int blockBackDistance = 4;
    [SerializeField] private bool blockBackRandomDistance = false;
    [SerializeField][Range(1, 10)] private int blockBackMinDistance = 2;
    [SerializeField][Range(1, 10)] private int blockBackMaxDistance = 6;
    [Space(10)]
    [Header("Patch Sizes")]
    [SerializeField][Range(2, 12)] private int blockBackPatchMinSize = 3;
    [SerializeField][Range(2, 12)] private int blockBackPatchMaxSize = 7;

    [Header("ASSET PATHS")]
    [SerializeField] private string spriteDecorationPath;
    [SerializeField] private string spriteBlockPath;

    [Header("RENDERING SETTINGS")]
    [SerializeField][Range(1, 10)] private float cellSize = 5.0f;

    [Header("LIGHTING SETTINGS")]
    [SerializeField][Range(0, 1)] private float globalLightIntensity = 0.025f;

    [Header("RUNTIME & DEBUGGING")]
    [SerializeField][Range(1, 2)] private float regenerationDelay = 0.5f;
    [SerializeField] private bool usePooling = false;

    #endregion

    #region Private Fields

    private PoolScript poolScript;
    private int minWidth = 10;
    private int minHeight = 10;
    private int maxWidth = 200;
    private int maxHeight = 200;
    private Vector2 positionStart;
    private Vector2 positionEnd;
    private bool hasBeenInitialized = false;
    [System.NonSerialized] public string currentProfileName = "Default";
    private static string profileListKey = "LevelScript_ProfileList";
    private int[,] blockBackPatchMap;
    private int[,] decorationPatchMap;
    private Random.State randomState;

    #endregion

    #region Constants

    private const float DEFAULT_GRASS_CHANCE = 0.01f;
    private const float DEFAULT_GRASS_COVER_CHANCE = 0.01f;
    private const float DEFAULT_MUSHROOM_CHANCE = 0.01f;
    private const float DEFAULT_STONE_CHANCE = 0.01f;
    private const float DEFAULT_WEB_CHANCE = 0.01f;
    private const float DEFAULT_GEM_CHANCE = .01f;
    private const int DEFAULT_PATH_WIDTH = 3;
    private const int DEFAULT_SECONDARY_PATH_WIDTH = 3;
    private const int DEFAULT_WIDTH = 10;
    private const int DEFAULT_HEIGHT = 10;
    private const float DEFAULT_PERLIN_SCALE = 0.5f;
    private const float DEFAULT_PERLIN_THRESHOLD = 0.5f;
    private const float DEFAULT_PERLIN_HEIGHT = 0.1f;
    private const float DEFAULT_PERLIN_WIDTH = 0.1f;
    private const int DEFAULT_RANDOM_SEED = 1234;
    private const int DEFAULT_BLOCK_BACK_DISTANCE = 4;
    private const int DEFAULT_BLOCK_BACK_MIN_DISTANCE = 2;
    private const int DEFAULT_BLOCK_BACK_MAX_DISTANCE = 6;
    private const bool DEFAULT_BLOCK_BACK_RANDOM_DISTANCE = false;
    private const int DEFAULT_BLOCK_BACK_PATCH_MIN_SIZE = 3;
    private const int DEFAULT_BLOCK_BACK_PATCH_MAX_SIZE = 7;
    private const int DEFAULT_DECORATION_PATCH_MIN_SIZE = 4;
    private const int DEFAULT_DECORATION_PATCH_MAX_SIZE = 8;
    private const float DEFAULT_GLOBAL_LIGHT_INTENSITY = 0.025f;
    private const float DEFAULT_CELL_SIZE = 5.0f;
    private const float DEFAULT_REGENERATION_DELAY = 0.5f;

    #endregion

    #region Getters
    public int widthGet() { return width; }
    public int heightGet() { return height; }
    public Vector2 positionStartGet() { return new Vector2(positionStart.x, positionStart.y); }
    public Vector2 positionEndGet() { return new Vector2(positionEnd.x, positionEnd.y); }
    public int randomSeedGet { get { return randomSeed; } }

    #endregion

    #region Public Members
    public MazeGenerator.MazeCell[,] grid;
    #endregion

    #region Unity Lifecycle
    private void OnEnable()
    {
        // In the Editor, when exiting Play mode, reload the last profile
        // This ensures the currentProfileName field is restored from PlayerPrefs
        if (!Application.isPlaying)
        {
            string lastProfile = PlayerPrefs.GetString("LevelScript_LastProfile", "Default");
            if (string.IsNullOrEmpty(currentProfileName) || currentProfileName != lastProfile)
            {
                currentProfileName = lastProfile;
                DL.Log($"OnEnable: Restored profile to '{currentProfileName}'", "cyan");
            }
        }
    }

    private void Awake()
    {
        PoolInit();
        LoadSettings();
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Vector3 bottomLeft = new Vector3(0, 0, 0);
        Vector3 bottomRight = new Vector3(width, 0, 0);
        Vector3 topLeft = new Vector3(0, height, 0);
        Vector3 topRight = new Vector3(width, height, 0);
        Gizmos.DrawLine(bottomLeft, bottomRight);
        Gizmos.DrawLine(bottomRight, topRight);
        Gizmos.DrawLine(topRight, topLeft);
        Gizmos.DrawLine(topLeft, bottomLeft);
    }

    private void OnValidate()
    {

        // Save settings during runtime when values change
        if (Application.isPlaying && hasBeenInitialized)
        {
            SaveSettings();

            // Cancel any previously scheduled regeneration to debounce
            CancelInvoke(nameof(RegenerateLevel));
            // Schedule a new regeneration after the delay
            Invoke(nameof(RegenerateLevel), regenerationDelay);
        }

    }

    private void RegenerateLevel()
    {
        Create();
        RenderLevel();

        if (usePooling)
            DL.Log("Pool Count: " + poolScript.GetTotalCount().ToString(), "yellow", false);

    }
    #endregion

    #region Level Rendering and Asset Selection
    private void PoolInit()
    {
        if (usePooling)
        {
            pool.TryGetComponent(out poolScript);
            // Optionally preload game objects here
            DL.Log("PoolInit", "green");
            DL.Log("Pooled GameObjects: " + poolScript.GetTotalCount().ToString(), "yellow", false);
        }
        else
        {
            DL.Log("Pooling disabled - using traditional instantiation", "cyan");
        }
    }

    public void RenderLevel()
    {
        // Safety check: ensure grid is initialized
        if (grid == null || grid.GetLength(0) != height || grid.GetLength(1) != width)
        {
            DL.Log("RenderLevel: Grid not properly initialized", "red");
            return;
        }

        RenderClear();

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Vector2 position = new Vector2(x * cellSize, y * cellSize);
                MazeGenerator.MazeCell cell = grid[y, x];

                if (cell == MazeGenerator.MazeCell.EMPTY) continue;

                if (cell == MazeGenerator.MazeCell.LEVER)
                {
                    // check if the left is empty, then place a Cell_Side_Left_Lever gameobject
                    if (x > 0 && grid[y, x - 1] == MazeGenerator.MazeCell.EMPTY)
                    {
                        SpawnGameObject("Cell_Side_Left_Lever", position);
                    }
                    else if (x < width - 1 && grid[y, x + 1] == MazeGenerator.MazeCell.EMPTY)
                    {
                        SpawnGameObject("Cell_Side_Right_Lever", position);
                    }

                    continue;
                }

                if (cell == MazeGenerator.MazeCell.SOLID)
                {
                    string blockName = GetContextualBlockAsset(x, y);

                    // Check layer depth for center blocks before creating - don't render if too deep
                    if (blockName.StartsWith("Cell_Center_"))
                    {
                        int layerDepth = GetLayerDepth(x, y);
                        if (layerDepth >= 3)
                        {
                            // Don't render blocks that are 3+ layers deep (keep outer edge and 1 level in)
                            continue;
                        }
                    }

                    SpawnGameObject(blockName, position);

                    continue;
                }

                if (cell == MazeGenerator.MazeCell.START)
                {
                    // Place start point marker - center it between the two-cell start area
                    // The START marker is at the first cell, so center between this cell and the next
                    Vector2 centeredPosition = new Vector2(x * cellSize + cellSize, y * cellSize + cellSize * 0.5f);
                    SpawnGameObject("VaultStart", centeredPosition);
                }
                else if (cell == MazeGenerator.MazeCell.END)
                {
                    // Place end point marker - center it between the two-cell end area
                    // The END marker is at the first cell, so center between this cell and the next
                    Vector2 centeredPosition = new Vector2(x * cellSize + cellSize, y * cellSize + cellSize * 0.5f);
                    SpawnGameObject("VaultEnd", centeredPosition);
                }
            }
        }

    }

    private string GetContextualBlockAsset(int x, int y)
    {
        // Check if this cell is on the border/edge of the grid
        if (x == 0 || x == width - 1 || y == 0 || y == height - 1)
        {
            return "Cell_Center_" + GetRandomRange(1, 3).ToString();
        }

        // Check 4-directional neighbors
        bool hasTop = IsNeighborSolid(x, y + 1);
        bool hasBottom = IsNeighborSolid(x, y - 1);
        bool hasLeft = IsNeighborSolid(x - 1, y);
        bool hasRight = IsNeighborSolid(x + 1, y);

        // Select asset based on neighbor configuration
        if (!hasTop && hasBottom)
        {
            // Only bottom neighbor - use top asset
            return "Cell_Top_" + GetRandomRange(1, 3).ToString();
        }
        else if (hasTop && !hasBottom && !hasLeft && !hasRight)
        {
            // Only top neighbor - use bottom asset
            return "Cell_Bottom_1";
        }
        else if (hasLeft && !hasRight)
        {
            // Only left neighbor - use right asset
            return "Cell_Side_Right_" + GetRandomRange(1, 3).ToString();
        }
        else if (!hasLeft && hasRight)
        {
            // Only right neighbor - use left asset
            return "Cell_Side_Left_1";
        }
        else if ((hasTop && hasBottom) || (hasLeft && hasRight))
        {
            // Connected in straight line (vertical or horizontal) - use center variation
            return "Cell_Center_" + GetRandomRange(1, 3).ToString();
        }
        // else if (hasTop || hasBottom || hasLeft || hasRight)
        // {
        //     // Has at least one connection but not a straight line - use basic center
        //     return "Cell_Center_1";
        // }
        else
        {
            // No solid neighbors - isolated block - use basic center
            return "Cell_Center_1";
        }
    }

    private bool IsNeighborSolid(int x, int y)
    {
        // Check bounds
        if (x < 0 || x >= width || y < 0 || y >= height)
            return false;

        // Safety check: ensure grid is initialized
        if (grid == null || y >= grid.GetLength(0) || x >= grid.GetLength(1))
            return false;

        // Check if neighbor is solid
        return grid[y, x] == MazeGenerator.MazeCell.SOLID;
    }

    private int GetLayerDepth(int x, int y)
    {
        // Safety check: ensure grid is initialized and coordinates are valid
        if (grid == null || x < 0 || x >= width || y < 0 || y >= height)
            return 5; // Return maximum layer for invalid positions

        // Use BFS to find the minimum distance to any empty cell
        var queue = new System.Collections.Generic.Queue<Vector2Int>();
        var visited = new bool[height, width];

        queue.Enqueue(new Vector2Int(x, y));
        visited[y, x] = true;

        int layer = 0;

        while (queue.Count > 0)
        {
            int levelSize = queue.Count;
            layer++;

            for (int i = 0; i < levelSize; i++)
            {
                var current = queue.Dequeue();

                // Check all 4 cardinal directions
                int[] dx = { -1, 1, 0, 0 };
                int[] dy = { 0, 0, -1, 1 };

                for (int dir = 0; dir < 4; dir++)
                {
                    int newX = current.x + dx[dir];
                    int newY = current.y + dy[dir];

                    // Check bounds
                    if (newX < 0 || newX >= width || newY < 0 || newY >= height)
                        continue;

                    // Skip if already visited
                    if (visited[newY, newX])
                        continue;

                    // If we found an empty cell, return the current layer
                    if (grid[newY, newX] == MazeGenerator.MazeCell.EMPTY)
                        return layer;

                    // If it's a solid cell, add it to the queue for next layer
                    if (grid[newY, newX] == MazeGenerator.MazeCell.SOLID)
                    {
                        visited[newY, newX] = true;
                        queue.Enqueue(new Vector2Int(newX, newY));
                    }
                }
            }
        }

        // If no empty cell is reachable, return maximum layer
        return 5;
    }

    private GameObject SpawnGameObject(string objectName, Vector2 position)
    {
        if (usePooling)
        {
            return poolScript.GetGameObject(objectName, position, transform, "", "level");
        }
        else
        {
            // Use the Helpers class to recursively search for prefab in Resources folder
            GameObject prefab = Helpers.LoadGameObjectFromResources(objectName);
            if (prefab == null)
            {
                DL.Log($"Failed to load prefab: {objectName} (searched recursively in Resources)", "red");
                return null;
            }

            // Instantiate the prefab
            GameObject instance = Instantiate(prefab, new Vector3(position.x, position.y, 0), Quaternion.identity, transform);
            return instance;
        }
    }
    #endregion

    #region Decoration System

    private void GenerateDecorationPatchMap()
    {
        // Safety check: ensure grid is initialized
        if (grid == null || width <= 0 || height <= 0)
        {
            DL.Log("GenerateDecorationPatchMap: Invalid grid or dimensions", "red");
            return;
        }

        // Initialize the decoration patch map
        decorationPatchMap = new int[height, width];

        // Fill with -1 (unassigned)
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                decorationPatchMap[y, x] = -1;
            }
        }

        // Generate patches for decoration types
        // 0 = Grass, 1 = GrassCover, 2 = Mushroom, 3 = Stone, 4 = Web
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                // Skip if already assigned
                if (decorationPatchMap[y, x] != -1)
                    continue;

                // Check if this position is suitable for decorations
                if (!IsDecorationSuitable(x, y))
                    continue;

                // Determine decoration type with weighted probabilities
                int decorationType = GetRandomDecorationType();

                // Random patch size
                int patchSize = GetRandomRange(decorationPatchMinSize, decorationPatchMaxSize + 1);

                // Fill patch using flood fill
                FillDecorationPatch(x, y, decorationType, patchSize);
            }
        }
    }

    private bool IsDecorationSuitable(int x, int y)
    {
        // Safety check: ensure grid is initialized and coordinates are valid
        if (grid == null || x < 0 || x >= width || y < 0 || y >= height)
            return false;

        // Check various decoration placement conditions
        if (y >= height - 1) return false;

        // For most decorations: solid below, empty above
        if (grid[y, x] == MazeGenerator.MazeCell.SOLID && grid[y + 1, x] == MazeGenerator.MazeCell.EMPTY)
            return true;

        // For web: check corner conditions
        if (grid[y, x] == MazeGenerator.MazeCell.EMPTY)
        {
            // Check for web-suitable corners
            if (x > 0 && y < height - 1 &&
                grid[y, x - 1] == MazeGenerator.MazeCell.SOLID &&
                grid[y + 1, x - 1] == MazeGenerator.MazeCell.SOLID &&
                grid[y + 1, x] == MazeGenerator.MazeCell.SOLID)
                return true;

            if (x < width - 1 && y < height - 1 &&
                grid[y, x + 1] == MazeGenerator.MazeCell.SOLID &&
                grid[y + 1, x + 1] == MazeGenerator.MazeCell.SOLID &&
                grid[y + 1, x] == MazeGenerator.MazeCell.SOLID)
                return true;
        }

        return false;
    }

    private bool IsGemStackSuitable(int x, int y, out Vector2 position, out float rotation)
    {
        position = Vector2.zero;
        rotation = 0f;

        // Safety check: ensure grid is initialized and coordinates are valid
        if (grid == null || x < 0 || x >= width || y < 0 || y >= height)
            return false;

        // Check if current cell is empty
        if (grid[y, x] != MazeGenerator.MazeCell.EMPTY) return false;

        // Check floor (solid below, empty above)
        if (y > 0 && grid[y - 1, x] == MazeGenerator.MazeCell.SOLID)
        {
            position = new Vector2(x, y);
            rotation = 0f; // Normal orientation
            return true;
        }

        // Check ceiling (solid above, empty below)
        if (y < height - 1 && grid[y + 1, x] == MazeGenerator.MazeCell.SOLID)
        {
            position = new Vector2(x, y + 1);
            rotation = 180f; // Upside down
            return true;
        }

        // Check left wall (solid to the left, empty to the right)
        if (x > 0 && grid[y, x - 1] == MazeGenerator.MazeCell.SOLID)
        {
            position = new Vector2(x, y);
            rotation = -90f; // Rotated 90 degrees counter-clockwise
            return true;
        }

        // Check right wall (solid to the right, empty to the left)
        if (x < width - 1 && grid[y, x + 1] == MazeGenerator.MazeCell.SOLID)
        {
            position = new Vector2(x, y);
            rotation = 90f; // Rotated 90 degrees clockwise
            return true;
        }

        return false;
    }

    private int GetRandomDecorationType()
    {
        float totalWeight = grassChance + grassCoverChance + mushroomChance + stoneChance + webChance;
        float randomValue = GetRandomValue() * totalWeight;

        if (randomValue < grassChance) return 0; // Grass
        randomValue -= grassChance;

        if (randomValue < grassCoverChance) return 1; // GrassCover
        randomValue -= grassCoverChance;

        if (randomValue < mushroomChance) return 2; // Mushroom
        randomValue -= mushroomChance;

        if (randomValue < stoneChance) return 3; // Stone

        return 4; // Web
    }

    private void FillDecorationPatch(int startX, int startY, int decorationType, int maxSize)
    {
        // Safety check: ensure decorationPatchMap is initialized
        if (decorationPatchMap == null || width <= 0 || height <= 0)
            return;

        var patchCells = new System.Collections.Generic.Queue<Vector2Int>();
        var visited = new bool[height, width];

        patchCells.Enqueue(new Vector2Int(startX, startY));
        visited[startY, startX] = true;
        int cellsAssigned = 0;

        while (patchCells.Count > 0 && cellsAssigned < maxSize)
        {
            var current = patchCells.Dequeue();
            int x = current.x;
            int y = current.y;

            // Skip if out of bounds or not suitable for decoration
            if (x < 0 || x >= width || y < 0 || y >= height ||
                !IsDecorationSuitable(x, y) ||
                decorationPatchMap[y, x] != -1)
                continue;

            // Assign decoration type
            decorationPatchMap[y, x] = decorationType;
            cellsAssigned++;

            // Add neighbors to queue
            Vector2Int[] neighbors = {
                new Vector2Int(x + 1, y), new Vector2Int(x - 1, y),
                new Vector2Int(x, y + 1), new Vector2Int(x, y - 1)
            };

            foreach (var neighbor in neighbors)
            {
                int nx = neighbor.x;
                int ny = neighbor.y;

                if (nx >= 0 && nx < width && ny >= 0 && ny < height &&
                    !visited[ny, nx] && IsDecorationSuitable(nx, ny))
                {
                    visited[ny, nx] = true;
                    patchCells.Enqueue(neighbor);
                }
            }
        }
    }

    #endregion

    #region Random Utilities
    private void InitializeRandomState()
    {
        Random.InitState(randomSeed);
        randomState = Random.state;
    }

    private void RestoreRandomState()
    {
        Random.state = randomState;
    }

    private int GetRandomRange(int min, int max)
    {
        RestoreRandomState();
        int result = Random.Range(min, max);
        randomState = Random.state;
        return result;
    }

    private float GetRandomValue()
    {
        RestoreRandomState();
        float result = Random.value;
        randomState = Random.state;
        return result;
    }
    #endregion

    #region Utility Methods
    private void SpecialEffects()
    {
        var globalLight = GameObject.Find("Global Light 2D");
        if (globalLight != null && globalLight.TryGetComponent<Light2D>(out var light2D))
            light2D.intensity = globalLightIntensity;
    }

    #endregion

    #region Settings and Profile Management
    private void SaveSettings()
    {
        DL.Log($"Saving LevelScript settings to profile '{currentProfileName}' - Width: {width}, Height: {height}, PerlinWidth: {perlinWidth}, PerlinHeight: {perlinHeight}", "yellow");

        // Save to the current profile instead of generic keys
        if (!string.IsNullOrEmpty(currentProfileName))
        {
            // Use the profile name as-is (it's already been formatted when LoadProfile was called)
            string prefix = $"LevelScript_Profile_{currentProfileName}_";

            PlayerPrefs.SetFloat(prefix + "GrassChance", grassChance);
            PlayerPrefs.SetFloat(prefix + "GrassCoverChance", grassCoverChance);
            PlayerPrefs.SetFloat(prefix + "MushroomChance", mushroomChance);
            PlayerPrefs.SetFloat(prefix + "StoneChance", stoneChance);
            PlayerPrefs.SetFloat(prefix + "WebChance", webChance);
            PlayerPrefs.SetFloat(prefix + "GemChance", gemChance);
            PlayerPrefs.SetInt(prefix + "PathWidth", pathWidth);
            PlayerPrefs.SetInt(prefix + "SecondaryPathWidth", secondaryPathWidth);
            PlayerPrefs.SetInt(prefix + "Width", width);
            PlayerPrefs.SetInt(prefix + "Height", height);
            PlayerPrefs.SetFloat(prefix + "PerlinScale", perlinScale);
            PlayerPrefs.SetFloat(prefix + "PerlinThreshold", perlinThreshold);
            PlayerPrefs.SetFloat(prefix + "PerlinWidth", perlinWidth);
            PlayerPrefs.SetFloat(prefix + "PerlinHeight", perlinHeight);
            PlayerPrefs.SetInt(prefix + "RandomSeed", randomSeed);
            PlayerPrefs.SetInt(prefix + "BlockBackDistance", blockBackDistance);
            PlayerPrefs.SetInt(prefix + "BlockBackMinDistance", blockBackMinDistance);
            PlayerPrefs.SetInt(prefix + "BlockBackMaxDistance", blockBackMaxDistance);
            PlayerPrefs.SetInt(prefix + "BlockBackRandomDistance", blockBackRandomDistance ? 1 : 0);
            PlayerPrefs.SetInt(prefix + "BlockBackPatchMinSize", blockBackPatchMinSize);
            PlayerPrefs.SetInt(prefix + "BlockBackPatchMaxSize", blockBackPatchMaxSize);
            PlayerPrefs.SetInt(prefix + "DecorationPatchMinSize", decorationPatchMinSize);
            PlayerPrefs.SetInt(prefix + "DecorationPatchMaxSize", decorationPatchMaxSize);
            PlayerPrefs.SetFloat(prefix + "GlobalLightIntensity", globalLightIntensity);
            PlayerPrefs.SetFloat(prefix + "CellSize", cellSize);
            PlayerPrefs.SetFloat(prefix + "RegenerationDelay", regenerationDelay);

            // Remember this is the last used profile
            PlayerPrefs.SetString("LevelScript_LastProfile", currentProfileName);
            PlayerPrefs.Save();
        }
        else
        {
            // Fallback to generic keys if no profile is set (shouldn't normally happen)
            PlayerPrefs.SetFloat("LevelScript_GrassChance", grassChance);
            PlayerPrefs.SetFloat("LevelScript_GrassCoverChance", grassCoverChance);
            PlayerPrefs.SetFloat("LevelScript_MushroomChance", mushroomChance);
            PlayerPrefs.SetFloat("LevelScript_StoneChance", stoneChance);
            PlayerPrefs.SetFloat("LevelScript_WebChance", webChance);
            PlayerPrefs.SetFloat("LevelScript_GemChance", gemChance);
            PlayerPrefs.SetInt("LevelScript_PathWidth", pathWidth);
            PlayerPrefs.SetInt("LevelScript_SecondaryPathWidth", secondaryPathWidth);
            PlayerPrefs.SetInt("LevelScript_Width", width);
            PlayerPrefs.SetInt("LevelScript_Height", height);
            PlayerPrefs.SetFloat("LevelScript_PerlinScale", perlinScale);
            PlayerPrefs.SetFloat("LevelScript_PerlinThreshold", perlinThreshold);
            PlayerPrefs.SetFloat("LevelScript_PerlinWidth", perlinWidth);
            PlayerPrefs.SetFloat("LevelScript_PerlinHeight", perlinHeight);
            PlayerPrefs.SetInt("LevelScript_RandomSeed", randomSeed);
            PlayerPrefs.SetFloat("LevelScript_GlobalLightIntensity", globalLightIntensity);
            PlayerPrefs.SetFloat("LevelScript_CellSize", cellSize);
            PlayerPrefs.SetFloat("LevelScript_RegenerationDelay", regenerationDelay);
            PlayerPrefs.Save();
        }
    }

    private void LoadSettings()
    {
        // Load the last used profile or default to "Default"
        string lastProfile = PlayerPrefs.GetString("LevelScript_LastProfile", "Default");
        LoadProfile(lastProfile);

        // Force WebGL to use default dimensions (overrides saved PlayerPrefs)
#if UNITY_WEBGL && !UNITY_EDITOR
        width = DEFAULT_WIDTH;
        height = DEFAULT_HEIGHT;
        DL.Log($"WebGL: Forced dimensions to {width}x{height}", "yellow");
#endif
    }

    [ContextMenu("Reset to Defaults")]
    public void ResetToDefaults()
    {
        grassChance = DEFAULT_GRASS_CHANCE;
        grassCoverChance = DEFAULT_GRASS_COVER_CHANCE;
        mushroomChance = DEFAULT_MUSHROOM_CHANCE;
        stoneChance = DEFAULT_STONE_CHANCE;
        webChance = DEFAULT_WEB_CHANCE;
        gemChance = DEFAULT_GEM_CHANCE;
        pathWidth = DEFAULT_PATH_WIDTH;
        secondaryPathWidth = DEFAULT_SECONDARY_PATH_WIDTH;
        width = DEFAULT_WIDTH;
        height = DEFAULT_HEIGHT;
        perlinScale = DEFAULT_PERLIN_SCALE;
        perlinThreshold = DEFAULT_PERLIN_THRESHOLD;
        perlinWidth = DEFAULT_PERLIN_WIDTH;
        perlinHeight = DEFAULT_PERLIN_HEIGHT;
        randomSeed = DEFAULT_RANDOM_SEED;
        blockBackDistance = DEFAULT_BLOCK_BACK_DISTANCE;
        blockBackMinDistance = DEFAULT_BLOCK_BACK_MIN_DISTANCE;
        blockBackMaxDistance = DEFAULT_BLOCK_BACK_MAX_DISTANCE;
        blockBackRandomDistance = DEFAULT_BLOCK_BACK_RANDOM_DISTANCE;
        blockBackPatchMinSize = DEFAULT_BLOCK_BACK_PATCH_MIN_SIZE;
        blockBackPatchMaxSize = DEFAULT_BLOCK_BACK_PATCH_MAX_SIZE;
        decorationPatchMinSize = DEFAULT_DECORATION_PATCH_MIN_SIZE;
        decorationPatchMaxSize = DEFAULT_DECORATION_PATCH_MAX_SIZE;
        globalLightIntensity = DEFAULT_GLOBAL_LIGHT_INTENSITY;
        cellSize = DEFAULT_CELL_SIZE;
        regenerationDelay = DEFAULT_REGENERATION_DELAY;

        SaveSettings();

        if (Application.isPlaying && hasBeenInitialized)
        {
            Create();
        }
    }

    public int CreateWithNewSeed(int randomSeed = 0, int width = 0, int height = 0)
    {
        if (randomSeed != 0)
            this.randomSeed = randomSeed;
        else
            this.randomSeed = UnityEngine.Random.Range(0, 99999);

        if (width != 0)
            this.width = width;
        if (height != 0)
            this.height = height;


        SaveSettings();

        Create(this.width, this.height);


        return this.randomSeed;
    }

    public void SaveProfile(string profileName)
    {
        if (string.IsNullOrEmpty(profileName))
            profileName = "Default";

        // Add dimensions prefix unless it's the Default profile
        if (profileName != "Default")
        {
            profileName = $"{width}x{height} {profileName}";
        }

        string prefix = $"LevelScript_Profile_{profileName}_";

        DL.Log($"Saving profile: {profileName}", "green");

        PlayerPrefs.SetFloat(prefix + "GrassChance", grassChance);
        PlayerPrefs.SetFloat(prefix + "GrassCoverChance", grassCoverChance);
        PlayerPrefs.SetFloat(prefix + "MushroomChance", mushroomChance);
        PlayerPrefs.SetFloat(prefix + "StoneChance", stoneChance);
        PlayerPrefs.SetFloat(prefix + "WebChance", webChance);
        PlayerPrefs.SetFloat(prefix + "GemChance", gemChance);
        PlayerPrefs.SetInt(prefix + "PathWidth", pathWidth);
        PlayerPrefs.SetInt(prefix + "SecondaryPathWidth", secondaryPathWidth);
        PlayerPrefs.SetInt(prefix + "Width", width);
        PlayerPrefs.SetInt(prefix + "Height", height);
        PlayerPrefs.SetFloat(prefix + "PerlinScale", perlinScale);
        PlayerPrefs.SetFloat(prefix + "PerlinThreshold", perlinThreshold);
        PlayerPrefs.SetFloat(prefix + "PerlinWidth", perlinWidth);
        PlayerPrefs.SetFloat(prefix + "PerlinHeight", perlinHeight);
        PlayerPrefs.SetInt(prefix + "RandomSeed", randomSeed);
        PlayerPrefs.SetInt(prefix + "BlockBackDistance", blockBackDistance);
        PlayerPrefs.SetInt(prefix + "BlockBackMinDistance", blockBackMinDistance);
        PlayerPrefs.SetInt(prefix + "BlockBackMaxDistance", blockBackMaxDistance);
        PlayerPrefs.SetInt(prefix + "BlockBackRandomDistance", blockBackRandomDistance ? 1 : 0);
        PlayerPrefs.SetInt(prefix + "BlockBackPatchMinSize", blockBackPatchMinSize);
        PlayerPrefs.SetInt(prefix + "BlockBackPatchMaxSize", blockBackPatchMaxSize);
        PlayerPrefs.SetInt(prefix + "DecorationPatchMinSize", decorationPatchMinSize);
        PlayerPrefs.SetInt(prefix + "DecorationPatchMaxSize", decorationPatchMaxSize);
        PlayerPrefs.SetFloat(prefix + "GlobalLightIntensity", globalLightIntensity);
        PlayerPrefs.SetFloat(prefix + "CellSize", cellSize);
        PlayerPrefs.SetFloat(prefix + "RegenerationDelay", regenerationDelay);

        // Add to profile list if not already there
        AddToProfileList(profileName);

        PlayerPrefs.Save();
        currentProfileName = profileName;

        // Remember the last used profile
        PlayerPrefs.SetString("LevelScript_LastProfile", profileName);
    }

    public void LoadProfile(string profileName)
    {
        if (string.IsNullOrEmpty(profileName))
            profileName = "Default";

        string prefix = $"LevelScript_Profile_{profileName}_";

        DL.Log($"Loading profile: {profileName}", "cyan");

        grassChance = PlayerPrefs.GetFloat(prefix + "GrassChance", DEFAULT_GRASS_CHANCE);
        grassCoverChance = PlayerPrefs.GetFloat(prefix + "GrassCoverChance", DEFAULT_GRASS_COVER_CHANCE);
        mushroomChance = PlayerPrefs.GetFloat(prefix + "MushroomChance", DEFAULT_MUSHROOM_CHANCE);
        stoneChance = PlayerPrefs.GetFloat(prefix + "StoneChance", DEFAULT_STONE_CHANCE);
        webChance = PlayerPrefs.GetFloat(prefix + "WebChance", DEFAULT_WEB_CHANCE);
        gemChance = PlayerPrefs.GetFloat(prefix + "GemChance", DEFAULT_GEM_CHANCE);
        pathWidth = PlayerPrefs.GetInt(prefix + "PathWidth", DEFAULT_PATH_WIDTH);
        secondaryPathWidth = PlayerPrefs.GetInt(prefix + "SecondaryPathWidth", DEFAULT_SECONDARY_PATH_WIDTH);
        width = PlayerPrefs.GetInt(prefix + "Width", DEFAULT_WIDTH);
        height = PlayerPrefs.GetInt(prefix + "Height", DEFAULT_HEIGHT);
        perlinScale = PlayerPrefs.GetFloat(prefix + "PerlinScale", DEFAULT_PERLIN_SCALE);
        perlinThreshold = PlayerPrefs.GetFloat(prefix + "PerlinThreshold", DEFAULT_PERLIN_THRESHOLD);
        perlinWidth = PlayerPrefs.GetFloat(prefix + "PerlinWidth", DEFAULT_PERLIN_WIDTH);
        perlinHeight = PlayerPrefs.GetFloat(prefix + "PerlinHeight", DEFAULT_PERLIN_HEIGHT);
        randomSeed = PlayerPrefs.GetInt(prefix + "RandomSeed", DEFAULT_RANDOM_SEED);
        blockBackDistance = PlayerPrefs.GetInt(prefix + "BlockBackDistance", DEFAULT_BLOCK_BACK_DISTANCE);
        blockBackMinDistance = PlayerPrefs.GetInt(prefix + "BlockBackMinDistance", DEFAULT_BLOCK_BACK_MIN_DISTANCE);
        blockBackMaxDistance = PlayerPrefs.GetInt(prefix + "BlockBackMaxDistance", DEFAULT_BLOCK_BACK_MAX_DISTANCE);
        blockBackRandomDistance = PlayerPrefs.GetInt(prefix + "BlockBackRandomDistance", DEFAULT_BLOCK_BACK_RANDOM_DISTANCE ? 1 : 0) == 1;
        blockBackPatchMinSize = PlayerPrefs.GetInt(prefix + "BlockBackPatchMinSize", DEFAULT_BLOCK_BACK_PATCH_MIN_SIZE);
        blockBackPatchMaxSize = PlayerPrefs.GetInt(prefix + "BlockBackPatchMaxSize", DEFAULT_BLOCK_BACK_PATCH_MAX_SIZE);
        decorationPatchMinSize = PlayerPrefs.GetInt(prefix + "DecorationPatchMinSize", DEFAULT_DECORATION_PATCH_MIN_SIZE);
        decorationPatchMaxSize = PlayerPrefs.GetInt(prefix + "DecorationPatchMaxSize", DEFAULT_DECORATION_PATCH_MAX_SIZE);
        globalLightIntensity = PlayerPrefs.GetFloat(prefix + "GlobalLightIntensity", DEFAULT_GLOBAL_LIGHT_INTENSITY);
        cellSize = PlayerPrefs.GetFloat(prefix + "CellSize", DEFAULT_CELL_SIZE);
        regenerationDelay = PlayerPrefs.GetFloat(prefix + "RegenerationDelay", DEFAULT_REGENERATION_DELAY);

        currentProfileName = profileName;

        // Remember the last used profile
        PlayerPrefs.SetString("LevelScript_LastProfile", profileName);

        if (Application.isPlaying && hasBeenInitialized)
        {
            Create();
        }
    }

    public string[] GetAvailableProfiles()
    {
        string profileListString = PlayerPrefs.GetString(profileListKey, "Default");
        return profileListString.Split(',');
    }

    private void AddToProfileList(string profileName)
    {
        var profiles = new System.Collections.Generic.List<string>(GetAvailableProfiles());
        if (!profiles.Contains(profileName))
        {
            profiles.Add(profileName);
            PlayerPrefs.SetString(profileListKey, string.Join(",", profiles));
        }
    }

    public void DeleteProfile(string profileName)
    {
        if (profileName == "Default") return; // Can't delete default profile

        string prefix = $"LevelScript_Profile_{profileName}_";

        // Delete all keys for this profile
        PlayerPrefs.DeleteKey(prefix + "GrassChance");
        PlayerPrefs.DeleteKey(prefix + "GrassCoverChance");
        PlayerPrefs.DeleteKey(prefix + "MushroomChance");
        PlayerPrefs.DeleteKey(prefix + "StoneChance");
        PlayerPrefs.DeleteKey(prefix + "WebChance");
        PlayerPrefs.DeleteKey(prefix + "GemChance");
        PlayerPrefs.DeleteKey(prefix + "PathWidth");
        PlayerPrefs.DeleteKey(prefix + "SecondaryPathWidth");
        PlayerPrefs.DeleteKey(prefix + "Width");
        PlayerPrefs.DeleteKey(prefix + "Height");
        PlayerPrefs.DeleteKey(prefix + "PerlinScale");
        PlayerPrefs.DeleteKey(prefix + "PerlinThreshold");
        PlayerPrefs.DeleteKey(prefix + "PerlinWidth");
        PlayerPrefs.DeleteKey(prefix + "PerlinHeight");
        PlayerPrefs.DeleteKey(prefix + "RandomSeed");
        PlayerPrefs.DeleteKey(prefix + "BlockBackDistance");
        PlayerPrefs.DeleteKey(prefix + "BlockBackMinDistance");
        PlayerPrefs.DeleteKey(prefix + "BlockBackMaxDistance");
        PlayerPrefs.DeleteKey(prefix + "BlockBackRandomDistance");
        PlayerPrefs.DeleteKey(prefix + "BlockBackPatchMinSize");
        PlayerPrefs.DeleteKey(prefix + "BlockBackPatchMaxSize");
        PlayerPrefs.DeleteKey(prefix + "DecorationPatchMinSize");
        PlayerPrefs.DeleteKey(prefix + "DecorationPatchMaxSize");
        PlayerPrefs.DeleteKey(prefix + "GlobalLightIntensity");
        PlayerPrefs.DeleteKey(prefix + "CellSize");
        PlayerPrefs.DeleteKey(prefix + "RegenerationDelay");

        // Remove from profile list
        var profiles = new System.Collections.Generic.List<string>(GetAvailableProfiles());
        profiles.Remove(profileName);
        PlayerPrefs.SetString(profileListKey, string.Join(",", profiles));

        PlayerPrefs.Save();

        DL.Log($"Deleted profile: {profileName}", "red");
    }

    #endregion

    #region Level Generation Methods
    public void Create()
    {
        Create(width, height);
    }
    public void Create(int width, int height)
    {
        if (width <= 0 || height <= 0)
        {
            DL.Log("Width and Height must be positive integers", "red");
            return;
        }

        if (width > 0 && height > 0)
        {
            this.width = width;
            this.height = height;
        }

        if (width < minWidth || height < minHeight)
        {
            DL.Log("Width and Height must be at least 5", "red");
            return;
        }
        if (width > maxWidth || height > maxHeight)
        {
            DL.Log("Width and Height must be at most 100", "red");
            return;
        }

        // Validate pathWidth
        if (pathWidth < 1)
        {
            DL.Log("Path width must be at least 1", "red");
            return;
        }

        // Calculate maximum allowed path width (more lenient for small dimensions)
        int maxPathWidth = Mathf.Max(3, Mathf.Min(width, height) / 3);
        if (pathWidth > maxPathWidth)
        {
            DL.Log($"Path width ({pathWidth}) is too large for maze dimensions ({width}x{height}). Max allowed: {maxPathWidth}", "red");
            return;
        }

        // Use the randomSeed field instead of generating a random one
        int seed = randomSeed;

        // Initialize deterministic random state
        InitializeRandomState();

        // Set the Perlin noise scaling factors in MazeGenerator
        MazeGenerator.PerlinWidth = perlinWidth;
        MazeGenerator.PerlinHeight = perlinHeight;

        var generatedMaze = MazeGenerator.GeneratePerlinMaze(width, height, perlinScale, perlinThreshold, seed, pathWidth, secondaryPathWidth);
        // var generatedMaze = MazeGenerator.GenerateMaze(width, height);

        grid = generatedMaze.maze;

        // positionStart = generatedMaze.startPoint - new Vector2(0, generatedMaze.height);
        positionStart = generatedMaze.startPoint;
        positionEnd = generatedMaze.endPoint;

        // SpecialEffects();
        // RenderLevel();
        // Draw_Decoration();
        string mazeString = MazeGenerator.MazeToString(grid);
        DL.Log($"Maze Generated with seed: {seed}, pathWidth: {pathWidth}", "green");
        DL.Log("Maze String: " + mazeString, "white", false, false, 12);

        hasBeenInitialized = true;
    }
    public void RenderClear()
    {

        // Iterate backwards through children to avoid index issues during destruction
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            GameObject childObject = child.gameObject;

            if (usePooling)
            {
                // USM handles node cleanup automatically when objects are destroyed/pooled
                // Return the object to the pool instead of destroying
                poolScript.ReturnGameObjectToPoolByUnityGameObjectID(childObject.GetInstanceID());
            }
            else
            {

                // Unparent the object first to remove it from hierarchy
                child.SetParent(null);

                // Immediately destroy the GameObject (no delay)
                DestroyImmediate(childObject);
            }
        }

        // Verify all children were removed
        if (transform.childCount > 0)
        {
            DL.Log($"RenderClear: Warning - {transform.childCount} children still remain after cleanup", "orange");
        }

    }
    public void Draw_Decoration()
    {
        // Safety check: ensure grid is initialized
        if (grid == null || width <= 0 || height <= 0)
        {
            DL.Log("Draw_Decoration: Invalid grid or dimensions", "red");
            return;
        }

        // Generate decoration patch map for consistent patch-based sprite placement
        GenerateDecorationPatchMap();

        // Safety check: ensure decoration patch map was created successfully
        if (decorationPatchMap == null)
        {
            DL.Log("Draw_Decoration: Failed to create decoration patch map", "red");
            return;
        }

        // Pre-define sprite name arrays for performance
        string[] grassNames = { "Grass1", "Grass2", "Grass3" };
        string[] grassCoverNames = { "GrassCover1", "GrassCover2" };
        string[] mushroomNames = { "Mushroom1", "Mushroom2" };
        string[] stoneNames = { "Stone1", "Stone2" };
        string[] webLeftNames = { "WebLeft1", "WebLeft2" };
        string[] webRightNames = { "WebRight1", "WebRight2" };

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height - 1; y++)
            {
                // Only place decorations at suitable locations
                if (!IsDecorationSuitable(x, y)) continue;

                // Get the decoration type from the patch map
                int decorationType = decorationPatchMap[y, x];
                if (decorationType == -1) continue; // No decoration assigned

                string spriteName = "";
                Vector2 position = Vector2.zero;

                switch (decorationType)
                {
                    case 0: // Grass
                        spriteName = grassNames[GetRandomRange(0, grassNames.Length)];
                        position = new Vector2(x, y + 1);
                        break;

                    case 1: // GrassCover
                        spriteName = grassCoverNames[GetRandomRange(0, grassCoverNames.Length)];
                        position = new Vector2(x, y);
                        break;

                    case 2: // Mushroom
                        spriteName = mushroomNames[GetRandomRange(0, mushroomNames.Length)];
                        position = new Vector2(x, y + 1);
                        break;

                    case 3: // Stone
                        spriteName = stoneNames[GetRandomRange(0, stoneNames.Length)];
                        position = new Vector2(x, y + 1);
                        break;

                    case 4: // Web
                        // Determine WebLeft or WebRight based on corner position
                        if (x > 0 && y < height - 1 &&
                            grid[y, x - 1] == MazeGenerator.MazeCell.SOLID &&
                            grid[y + 1, x - 1] == MazeGenerator.MazeCell.SOLID &&
                            grid[y + 1, x] == MazeGenerator.MazeCell.SOLID &&
                            grid[y, x] == MazeGenerator.MazeCell.EMPTY)
                        {
                            spriteName = webLeftNames[GetRandomRange(0, webLeftNames.Length)];
                            position = new Vector2(x, y);
                        }
                        else if (x < width - 1 && y < height - 1 &&
                                 grid[y, x + 1] == MazeGenerator.MazeCell.SOLID &&
                                 grid[y + 1, x + 1] == MazeGenerator.MazeCell.SOLID &&
                                 grid[y + 1, x] == MazeGenerator.MazeCell.SOLID &&
                                 grid[y, x] == MazeGenerator.MazeCell.EMPTY)
                        {
                            spriteName = webRightNames[GetRandomRange(0, webRightNames.Length)];
                            position = new Vector2(x, y);
                        }
                        break;

                }

                // Create the decoration sprite if a valid sprite name was determined
                if (!string.IsNullOrEmpty(spriteName))
                {
                    SpawnGameObject(spriteName, position);
                }
            }
        }

        // Place GemStacks separately using random logic
        PlaceGemStacks();
    }
    private void PlaceGemStacks()
    {
        // Safety check: ensure grid is initialized
        if (grid == null || width <= 0 || height <= 0)
        {
            DL.Log("PlaceGemStacks: Invalid grid or dimensions", "red");
            return;
        }

        string[] gemStackNames = { "GemStack" };

        // Randomly place GemStacks on any suitable surface
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                // Check if we should place a GemStack here based on chance
                if (GetRandomValue() > gemChance) continue;

                Vector2 position;
                float rotation;

                // Check if this location is suitable for GemStack placement
                if (IsGemStackSuitable(x, y, out position, out rotation))
                {
                    string spriteName = gemStackNames[GetRandomRange(0, gemStackNames.Length)];
                    var gemStackObject = SpawnGameObject(spriteName, position);

                    // Apply rotation based on surface orientation
                    if (gemStackObject != null)
                    {
                        gemStackObject.transform.rotation = Quaternion.Euler(0, 0, rotation);
                    }
                }
            }
        }
    }

    #endregion
}