using UnityEngine;

public class FogSpawner : MonoBehaviour
{
    public GameObject fogPrefab; // Prefab to be instantiated
    public int width = 200; // Width of the grid
    public int height = 200; // Height of the grid
    public float spacing = .1f; // Space between each fog instance

    void Start()
    {
        SpawnFogGrid();
    }

    void SpawnFogGrid()
    {
        Vector2 startPoint = new Vector2(0, 0);
        int widthHalf = width / 2;
        int heightHalf = height / 2;

        for (int x = -widthHalf - 1; x < widthHalf; x++)
        {

            for (int y = -heightHalf; y < heightHalf; y++)
            {
                Vector2 spawnPosition = startPoint + new Vector2(x * spacing, y * spacing);
                Instantiate(fogPrefab, spawnPosition, Quaternion.identity);
            }
        }
    }
}
