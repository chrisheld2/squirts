using UnityEngine;

public static class Helpers
{
    public static Transform FindChildByName(string name, Transform parent)
    {
        if (parent.name == name)
            return parent;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == name)
                return child;

            Transform found = FindChildByName(name, child);
            if (found != null)
                return found;
        }

        return null;
    }

    /// <summary>
    /// Recursively searches through all folders in Resources to find and load a GameObject by name.
    /// </summary>
    /// <param name="objectName">The name of the GameObject to find (without file extension)</param>
    /// <param name="resourcesRelativePath">The starting folder path within Resources (empty string for root)</param>
    /// <returns>The loaded GameObject if found, null otherwise</returns>
    public static GameObject LoadGameObjectFromResources(string objectName, string resourcesRelativePath = "")
    {
        // Try to load directly from the current folder path
        string loadPath = string.IsNullOrEmpty(resourcesRelativePath) ? objectName : resourcesRelativePath + "/" + objectName;
        GameObject obj = Resources.Load<GameObject>(loadPath);

        if (obj != null)
            return obj;

#if UNITY_WEBGL && !UNITY_EDITOR
        // WebGL doesn't support System.IO directory operations
        // Use Resources.LoadAll to find all GameObjects and search by name
        GameObject[] allObjects = Resources.LoadAll<GameObject>("");

        foreach (GameObject go in allObjects)
        {
            if (go.name == objectName)
                return go;
        }

        return null;
#else
        // Get the physical Resources folder path
        string resourcesPath = System.IO.Path.Combine(UnityEngine.Application.dataPath, "Resources");

        // Build the full physical path to search
        string searchPath = string.IsNullOrEmpty(resourcesRelativePath)
            ? resourcesPath
            : System.IO.Path.Combine(resourcesPath, resourcesRelativePath);

        // If the directory doesn't exist, return null
        if (!System.IO.Directory.Exists(searchPath))
            return null;

        // Get all subdirectories
        string[] subdirectories = System.IO.Directory.GetDirectories(searchPath);

        // Search each subdirectory recursively
        foreach (string subdirectory in subdirectories)
        {
            string folderName = System.IO.Path.GetFileName(subdirectory);

            // Skip hidden/system folders
            if (folderName.StartsWith(".") || folderName.StartsWith("~"))
                continue;

            // Build new relative path for Resources.Load
            string newRelativePath = string.IsNullOrEmpty(resourcesRelativePath)
                ? folderName
                : resourcesRelativePath + "/" + folderName;

            // Recursively search in this subdirectory
            GameObject found = LoadGameObjectFromResources(objectName, newRelativePath);
            if (found != null)
                return found;
        }

        return null;
#endif
    }

}
