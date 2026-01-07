using UnityEditor;
using UnityEngine;
using System.Linq;

public class AtlasToPrefabs : MonoBehaviour
{
    [MenuItem("Tools/Atlas/Create Prefabs From Sprites")]
    public static void CreatePrefabsFromSprites()
    {
        // Ensure a texture or sprite is selected
        Object selectedObject = Selection.activeObject;
        if (selectedObject == null || !(selectedObject is Texture2D))
        {
            Debug.LogError("Please select a sprite atlas (texture) in the Project window.");
            return;
        }

        // Get the path of the selected texture
        string path = AssetDatabase.GetAssetPath(selectedObject);

        // Load all sprites from the atlas
        Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToArray();

        if (sprites.Length == 0)
        {
            Debug.LogError("No sprites found in the selected atlas. Make sure the sprite mode is set to 'Multiple'.");
            return;
        }

        // Create a folder for prefabs
        string folderPath = "Assets/GeneratedPrefabs";
        if (!AssetDatabase.IsValidFolder(folderPath))
        {
            AssetDatabase.CreateFolder("Assets", "GeneratedPrefabs");
        }

        // Create prefabs from the sprites
        foreach (Sprite sprite in sprites)
        {
            GameObject spriteObject = new GameObject(sprite.name);
            SpriteRenderer renderer = spriteObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;

            string prefabPath = $"{folderPath}/{sprite.name}.prefab";
            PrefabUtility.SaveAsPrefabAsset(spriteObject, prefabPath);

            DestroyImmediate(spriteObject); // Clean up the scene
        }

        Debug.Log($"Successfully created {sprites.Length} prefabs in {folderPath}");
    }
}
