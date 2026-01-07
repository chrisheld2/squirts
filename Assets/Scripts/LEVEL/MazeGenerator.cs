using System.Collections.Generic;
using UnityEngine;
using Random = System.Random;

[System.Serializable]
public class MazeData
{
    public MazeGenerator.MazeCell[,] maze;
    public Vector2Int startPoint;
    public Vector2Int endPoint;
    public int width;
    public int height;

    public MazeData(MazeGenerator.MazeCell[,] maze, Vector2Int startPoint, Vector2Int endPoint)
    {
        this.maze = maze;
        this.startPoint = startPoint;
        this.endPoint = endPoint;
        this.width = maze.GetLength(1);
        this.height = maze.GetLength(0);
    }
}

public static class MazeGenerator
{
    // Define maze elements
    public enum MazeCell
    {
        EMPTY,
        SOLID,
        START,
        LEVER,
        END
    }

    private static int _seed;
    public static float PerlinWidth { get; set; } = 0.1f;
    public static float PerlinHeight { get; set; } = 0.1f;
    public static int SecondaryPathWidth { get; set; } = 6;

    /// <summary>
    /// Generates maze data using Perlin noise algorithm for more organic, natural-looking patterns
    /// </summary>
    /// <param name="width">Width of the maze</param>
    /// <param name="height">Height of the maze</param>
    /// <param name="scale">Scale factor for Perlin noise (smaller = more detailed)</param>
    /// <param name="threshold">Threshold value to determine walls vs empty spaces (0.0 to 1.0)</param>
    /// <param name="seed">Seed for random offset in Perlin noise</param>
    /// <param name="pathWidth">Minimum width of paths (default 1)</param>
    /// <param name="secondaryPathWidth">Width of the secondary path from start to end (default 3)</param>
    /// <returns>MazeData containing the maze grid, start point, and end point</returns>
    public static MazeData GeneratePerlinMaze(int width, int height, float scale = 0.1f, float threshold = 0.5f, int seed = 0, int pathWidth = 1, int secondaryPathWidth = 3)
    {
        _seed = seed; // Store the seed for potential future use

        var maze = new MazeCell[height, width];
        var perlinRandom = new Random(seed);

        // Generate random offset for Perlin noise sampling
        float offsetX = perlinRandom.Next(0, 10000);
        float offsetY = perlinRandom.Next(0, 10000);

        // Generate base maze structure using Perlin noise
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                // Sample Perlin noise at this position using separate width and height scaling
                float noiseValue = Mathf.PerlinNoise((x + offsetX) * scale * PerlinHeight, (y + offsetY) * scale * PerlinWidth);

                // Convert noise value to wall or empty space based on threshold
                maze[y, x] = noiseValue > threshold ? MazeCell.EMPTY : MazeCell.SOLID;
            }
        }

        // Ensure borders are walls for proper maze structure
        for (int x = 0; x < width; x++)
        {
            maze[0, x] = MazeCell.SOLID;           // Top border
            maze[height - 1, x] = MazeCell.SOLID; // Bottom border
        }
        for (int y = 0; y < height; y++)
        {
            maze[y, 0] = MazeCell.SOLID;           // Left border
            maze[y, width - 1] = MazeCell.SOLID;   // Right border
        }

        // Post-process to ensure connectivity and remove isolated areas
        EnsureConnectivity(maze, width, height);

        // Clean up small isolated wall clusters
        CleanupIsolatedWalls(maze, width, height);

        // Ensure minimum path width if specified
        if (pathWidth > 1)
        {
            EnforceMinimumPathWidth(maze, width, height, pathWidth);
        }

        // Set start point (force placement on top row)
        Vector2Int startPoint = FindStartPoint(maze, width, height);

        // Set end point (force placement on bottom row)
        Vector2Int endPoint = FindEndPoint(maze, width, height);

        // Create secondary path from start to end
        CreateSecondaryPath(maze, width, height, startPoint, endPoint, secondaryPathWidth);

        return new MazeData(maze, startPoint, endPoint);
    }

    /// <summary>
    /// Ensures the maze has proper connectivity by removing walls that create isolated areas
    /// </summary>
    private static void EnsureConnectivity(MazeCell[,] maze, int width, int height)
    {
        var visited = new bool[height, width];
        var regions = new List<List<Vector2Int>>();

        // Find all connected regions
        for (int y = 1; y < height - 1; y++)
        {
            for (int x = 1; x < width - 1; x++)
            {
                if (!visited[y, x] && maze[y, x] == MazeCell.EMPTY)
                {
                    var region = new List<Vector2Int>();
                    FloodFill(maze, visited, x, y, width, height, region);
                    if (region.Count > 0)
                    {
                        regions.Add(region);
                    }
                }
            }
        }

        // If we have multiple regions, connect them
        if (regions.Count > 1)
        {
            ConnectRegions(maze, regions, width, height);
        }

        // If no regions exist, create a basic path
        if (regions.Count == 0)
        {
            CreateBasicPath(maze, width, height);
        }
    }

    /// <summary>
    /// Flood fill algorithm to find connected regions
    /// </summary>
    private static void FloodFill(MazeCell[,] maze, bool[,] visited, int x, int y, int width, int height, List<Vector2Int> region)
    {
        if (x < 1 || y < 1 || x >= width - 1 || y >= height - 1 ||
            visited[y, x] || maze[y, x] != MazeCell.EMPTY)
        {
            return;
        }

        visited[y, x] = true;
        region.Add(new Vector2Int(x, y));

        // Check 4-directional neighbors
        FloodFill(maze, visited, x + 1, y, width, height, region);
        FloodFill(maze, visited, x - 1, y, width, height, region);
        FloodFill(maze, visited, x, y + 1, width, height, region);
        FloodFill(maze, visited, x, y - 1, width, height, region);
    }

    /// <summary>
    /// Connects isolated regions by carving paths between them
    /// </summary>
    private static void ConnectRegions(MazeCell[,] maze, List<List<Vector2Int>> regions, int width, int height)
    {
        // Sort regions by size (largest first)
        regions.Sort((a, b) => b.Count.CompareTo(a.Count));

        var mainRegion = regions[0];

        // Connect each smaller region to the main region
        for (int i = 1; i < regions.Count; i++)
        {
            var smallerRegion = regions[i];
            ConnectTwoRegions(maze, mainRegion, smallerRegion, width, height);

            // Add the smaller region to main region after connecting
            mainRegion.AddRange(smallerRegion);
        }
    }

    /// <summary>
    /// Connects two regions by carving the shortest path between them
    /// </summary>
    private static void ConnectTwoRegions(MazeCell[,] maze, List<Vector2Int> region1, List<Vector2Int> region2, int width, int height)
    {
        float minDistance = float.MaxValue;
        Vector2Int bestPoint1 = Vector2Int.zero;
        Vector2Int bestPoint2 = Vector2Int.zero;

        // Find the closest points between the two regions
        foreach (var point1 in region1)
        {
            foreach (var point2 in region2)
            {
                float distance = Vector2Int.Distance(point1, point2);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    bestPoint1 = point1;
                    bestPoint2 = point2;
                }
            }
        }

        // Carve a path between the closest points
        CarvePathBetweenPoints(maze, bestPoint1, bestPoint2, width, height);
    }

    /// <summary>
    /// Carves a path between two points using a simple line algorithm
    /// </summary>
    private static void CarvePathBetweenPoints(MazeCell[,] maze, Vector2Int start, Vector2Int end, int width, int height)
    {
        int x0 = start.x, y0 = start.y;
        int x1 = end.x, y1 = end.y;

        int dx = Mathf.Abs(x1 - x0);
        int dy = Mathf.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1;
        int sy = y0 < y1 ? 1 : -1;
        int err = dx - dy;

        while (true)
        {
            if (x0 >= 0 && x0 < width && y0 >= 0 && y0 < height)
            {
                maze[y0, x0] = MazeCell.EMPTY;
            }

            if (x0 == x1 && y0 == y1) break;

            int e2 = 2 * err;
            if (e2 > -dy)
            {
                err -= dy;
                x0 += sx;
            }
            if (e2 < dx)
            {
                err += dx;
                y0 += sy;
            }
        }
    }

    /// <summary>
    /// Creates a basic path when no regions exist
    /// </summary>
    private static void CreateBasicPath(MazeCell[,] maze, int width, int height)
    {
        // Create a simple path from top-left to bottom-right
        for (int x = 1; x < width - 1; x++)
        {
            maze[1, x] = MazeCell.EMPTY;
        }
        for (int y = 1; y < height - 1; y++)
        {
            maze[y, width - 2] = MazeCell.EMPTY;
        }
    }

    /// <summary>
    /// Finds a suitable start point on the top row of the maze
    /// </summary>
    private static Vector2Int FindStartPoint(MazeCell[,] maze, int width, int height)
    {
        var random = new Random(_seed + 500);

        // Force placement on the top row (y = height - 1)
        // Choose a random x position excluding the border walls, ensuring space for 2-wide start
        int startX = random.Next(1, width - 2);

        // Clear 2 cells wide for the start area but only mark the middle position as START
        maze[height - 1, startX] = MazeCell.EMPTY;
        maze[height - 1, startX + 1] = MazeCell.EMPTY;

        // Place single START marker consistently at the first cell of the two-cell area
        // The vault positioning will center it between both cells
        int startPointX = startX;
        maze[height - 1, startPointX] = MazeCell.START;

        return new Vector2Int(startX, height - 1);
    }

    /// <summary>
    /// Finds a suitable end point on the bottom row of the maze
    /// </summary>
    private static Vector2Int FindEndPoint(MazeCell[,] maze, int width, int height)
    {
        var random = new Random(_seed + 600);

        // Force placement on the bottom row (y = 0)
        // Choose a random x position excluding the border walls, ensuring space for 2-wide end
        int endX = random.Next(1, width - 2);

        // Clear 2 cells wide for the end area but only mark the middle position as END
        maze[0, endX] = MazeCell.EMPTY;
        maze[0, endX + 1] = MazeCell.EMPTY;

        // Place single END marker consistently at the first cell of the two-cell area
        // The vault positioning will center it between both cells
        int endPointX = endX;
        maze[0, endPointX] = MazeCell.END;

        return new Vector2Int(endPointX, 0);
    }
    private static void CleanupIsolatedWalls(MazeCell[,] maze, int width, int height)
    {
        for (int y = 1; y < height - 1; y++)
        {
            for (int x = 1; x < width - 1; x++)
            {
                if (maze[y, x] == MazeCell.SOLID)
                {
                    // Count neighboring walls
                    int wallNeighbors = 0;
                    int[] dx = { -1, 0, 1, 0 };
                    int[] dy = { 0, -1, 0, 1 };

                    for (int i = 0; i < 4; i++)
                    {
                        int nx = x + dx[i];
                        int ny = y + dy[i];
                        if (nx >= 0 && nx < width && ny >= 0 && ny < height && maze[ny, nx] == MazeCell.SOLID)
                        {
                            wallNeighbors++;
                        }
                    }

                    // Remove isolated walls (walls with few neighbors)
                    if (wallNeighbors <= 1)
                    {
                        maze[y, x] = MazeCell.EMPTY;
                    }
                }
            }
        }
    }







    /// <summary>
    /// Converts a maze grid to a visual string representation using emojis for Unity Debug.Log
    /// Displays the maze with proper orientation matching the game world
    /// </summary>
    /// <param name="maze">The maze grid to convert</param>
    /// <returns>String representation of the maze with emojis and decorative formatting</returns>
    public static string MazeToString(MazeCell[,] maze)
    {
        if (maze == null)
        {
            return "🚫 ERROR: Maze is null! 🚫";
        }

        int height = maze.GetLength(0);
        int width = maze.GetLength(1);

        if (height == 0 || width == 0)
        {
            return "🚫 ERROR: Invalid maze dimensions! 🚫";
        }

        var result = new System.Text.StringBuilder();

        result.AppendLine($"MAZE ({width}×{height})");
        result.AppendLine("Seed: " + _seed);
        result.AppendLine("Legend: 🟫=Wall ⬛️=Empty 🚪=Start ⛳️=End ❓=Unknown");

        for (int y = height - 1; y >= 0; y--)
        {
            for (int x = 0; x < width; x++)
            {
                switch (maze[y, x])
                {
                    case MazeCell.EMPTY:
                        result.Append("⬛️"); // Empty space - white square
                        break;
                    case MazeCell.SOLID:
                        result.Append("🟫"); // Wall - black square
                        break;
                    case MazeCell.START:
                        result.Append("🚪"); // Start point - green circle
                        break;
                    case MazeCell.END:
                        result.Append("⛳️"); // End point - red circle
                        break;
                    default:
                        result.Append("❓"); // Unknown cell type
                        break;
                }
            }
            result.AppendLine(); // New line after each row
        }




        return result.ToString();
    }

    /// <summary>
    /// Enforces minimum path width by widening narrow passages
    /// </summary>
    private static void EnforceMinimumPathWidth(MazeCell[,] maze, int width, int height, int minPathWidth)
    {
        // Create a working copy to avoid modifying while iterating
        var workingMaze = (MazeCell[,])maze.Clone();

        // Scan for narrow passages and widen them
        for (int y = 1; y < height - 1; y++)
        {
            for (int x = 1; x < width - 1; x++)
            {
                if (maze[y, x] == MazeCell.EMPTY)
                {
                    // Check if this empty cell is part of a path that's too narrow
                    if (IsPathTooNarrow(maze, x, y, width, height, minPathWidth))
                    {
                        WidenPath(workingMaze, x, y, width, height, minPathWidth);
                    }
                }
            }
        }

        // Copy back the working maze
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                maze[y, x] = workingMaze[y, x];
            }
        }
    }

    /// <summary>
    /// Checks if a path at the given position is narrower than the minimum width
    /// </summary>
    private static bool IsPathTooNarrow(MazeCell[,] maze, int x, int y, int width, int height, int minWidth)
    {
        // Check horizontal corridor width
        int horizontalWidth = 1;

        // Count empty spaces to the left
        for (int leftX = x - 1; leftX >= 0 && maze[y, leftX] == MazeCell.EMPTY; leftX--)
        {
            horizontalWidth++;
        }

        // Count empty spaces to the right
        for (int rightX = x + 1; rightX < width && maze[y, rightX] == MazeCell.EMPTY; rightX++)
        {
            horizontalWidth++;
        }

        // Check vertical corridor width
        int verticalWidth = 1;

        // Count empty spaces above
        for (int upY = y - 1; upY >= 0 && maze[upY, x] == MazeCell.EMPTY; upY--)
        {
            verticalWidth++;
        }

        // Count empty spaces below
        for (int downY = y + 1; downY < height && maze[downY, x] == MazeCell.EMPTY; downY++)
        {
            verticalWidth++;
        }

        // Path is too narrow if both horizontal and vertical dimensions are less than minimum
        return (horizontalWidth < minWidth || verticalWidth < minWidth);
    }

    /// <summary>
    /// Widens a path at the given position by removing nearby walls
    /// </summary>
    private static void WidenPath(MazeCell[,] maze, int centerX, int centerY, int width, int height, int minWidth)
    {
        int radius = (minWidth - 1) / 2;

        // Create a wider path area around the center point
        for (int dy = -radius; dy <= radius; dy++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                int newX = centerX + dx;
                int newY = centerY + dy;

                // Check bounds and avoid border walls
                if (newX > 0 && newX < width - 1 && newY > 0 && newY < height - 1)
                {
                    // Only convert walls to empty space if within the desired radius
                    if (System.Math.Abs(dx) + System.Math.Abs(dy) <= radius)
                    {
                        maze[newY, newX] = MazeCell.EMPTY;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Creates a secondary path from start to end point with turns and specified width
    /// </summary>
    private static void CreateSecondaryPath(MazeCell[,] maze, int width, int height, Vector2Int startPoint, Vector2Int endPoint, int secondaryPathWidth)
    {
        var pathRandom = new Random(_seed + 1000);
        var pathPoints = new List<Vector2Int>();

        // Start from the start point
        pathPoints.Add(startPoint);

        Vector2Int currentPoint = startPoint;
        Vector2Int targetPoint = endPoint;

        // Generate path with turns while moving generally toward the target
        while (Vector2Int.Distance(currentPoint, targetPoint) > secondaryPathWidth)
        {
            Vector2Int nextPoint = GetNextPathPoint(currentPoint, targetPoint, width, height, pathRandom, secondaryPathWidth);
            pathPoints.Add(nextPoint);
            currentPoint = nextPoint;
        }

        // Add the end point
        pathPoints.Add(endPoint);

        // Carve the path with specified width
        for (int i = 0; i < pathPoints.Count - 1; i++)
        {
            CarveWidePath(maze, pathPoints[i], pathPoints[i + 1], width, height, secondaryPathWidth);
        }
    }

    /// <summary>
    /// Gets the next point in the path, adding some randomness for turns
    /// </summary>
    private static Vector2Int GetNextPathPoint(Vector2Int current, Vector2Int target, int width, int height, Random random, int secondaryPathWidth)
    {
        // Calculate the general direction toward the target
        Vector2Int direction = new Vector2Int(
            target.x > current.x ? 1 : (target.x < current.x ? -1 : 0),
            target.y > current.y ? 1 : (target.y < current.y ? -1 : 0)
        );

        // Add some randomness to create turns
        int stepSize = random.Next(2, 6); // Move 2-5 cells at a time

        // Choose whether to move primarily horizontally or vertically
        bool moveHorizontally = random.NextDouble() > 0.5;

        Vector2Int nextPoint;
        if (moveHorizontally && direction.x != 0)
        {
            // Move horizontally toward target
            int deltaX = direction.x * stepSize;
            nextPoint = new Vector2Int(current.x + deltaX, current.y);
        }
        else if (direction.y != 0)
        {
            // Move vertically toward target
            int deltaY = direction.y * stepSize;
            nextPoint = new Vector2Int(current.x, current.y + deltaY);
        }
        else
        {
            // Default movement if we're already aligned
            nextPoint = new Vector2Int(current.x + direction.x * stepSize, current.y + direction.y * stepSize);
        }

        // Ensure the point stays within bounds (with margin for path width)
        int margin = secondaryPathWidth / 2 + 1;
        nextPoint.x = Mathf.Clamp(nextPoint.x, margin, width - margin - 1);
        nextPoint.y = Mathf.Clamp(nextPoint.y, margin, height - margin - 1);

        return nextPoint;
    }

    /// <summary>
    /// Carves a wide path between two points
    /// </summary>
    private static void CarveWidePath(MazeCell[,] maze, Vector2Int start, Vector2Int end, int width, int height, int pathWidth)
    {
        // Get all points along the line between start and end
        var linePoints = GetLinePoints(start, end);

        // For each point on the line, carve a wide area
        foreach (var point in linePoints)
        {
            CarveWideArea(maze, point, width, height, pathWidth);
        }
    }

    /// <summary>
    /// Gets all points along a line between two points using Bresenham's algorithm
    /// </summary>
    private static List<Vector2Int> GetLinePoints(Vector2Int start, Vector2Int end)
    {
        var points = new List<Vector2Int>();

        int x0 = start.x, y0 = start.y;
        int x1 = end.x, y1 = end.y;

        int dx = Mathf.Abs(x1 - x0);
        int dy = Mathf.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1;
        int sy = y0 < y1 ? 1 : -1;
        int err = dx - dy;

        while (true)
        {
            points.Add(new Vector2Int(x0, y0));

            if (x0 == x1 && y0 == y1) break;

            int e2 = 2 * err;
            if (e2 > -dy)
            {
                err -= dy;
                x0 += sx;
            }
            if (e2 < dx)
            {
                err += dx;
                y0 += sy;
            }
        }

        return points;
    }

    /// <summary>
    /// Carves a wide area around a center point
    /// </summary>
    private static void CarveWideArea(MazeCell[,] maze, Vector2Int center, int width, int height, int pathWidth)
    {
        int radius = pathWidth / 2;

        for (int dy = -radius; dy <= radius; dy++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                int x = center.x + dx;
                int y = center.y + dy;

                // Check bounds and avoid border walls
                if (x > 0 && x < width - 1 && y > 0 && y < height - 1)
                {
                    // Only carve if within circular radius to create smoother paths
                    if (dx * dx + dy * dy <= radius * radius)
                    {
                        // Preserve start and end markers
                        if (maze[y, x] != MazeCell.START && maze[y, x] != MazeCell.END)
                        {
                            maze[y, x] = MazeCell.EMPTY;
                        }
                    }
                }
            }
        }
    }
}