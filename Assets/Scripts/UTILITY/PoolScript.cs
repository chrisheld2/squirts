using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

public class PoolScript : MonoBehaviour
{
    #region Singleton
    public static PoolScript Instance { get; private set; }
    #endregion

    #region Serialized Fields
    [SerializeField]
    private TextMeshProUGUI textPool;
    #endregion

    #region Private Fields
    
    // STORAGE ----------------------------------------------------------
    // 1. Available objects ready to be used (Key: Prefab Name)
    private Dictionary<string, Stack<PoolItem>> availableItems = new Dictionary<string, Stack<PoolItem>>();

    // 2. Active objects currently in the world (Key: InstanceID)
    private Dictionary<int, PoolItem> activeItemsByID = new Dictionary<int, PoolItem>();

    // 3. Active objects mapped by custom GUID (Key: GUID string) - Optional
    private Dictionary<string, PoolItem> activeItemsByGUID = new Dictionary<string, PoolItem>();

    // 4. Active objects mapped by Group (Key: Group Name) - Optional
    private Dictionary<string, HashSet<PoolItem>> activeItemsByGroup = new Dictionary<string, HashSet<PoolItem>>();

    // CACHE ------------------------------------------------------------
    // Prevents expensive Resources.Load calls every time we spawn
    private Dictionary<string, GameObject> prefabCache = new Dictionary<string, GameObject>();
    
    // STATS ------------------------------------------------------------
    private int nextItemId = 0;
    private int totalCount;
    private int inUseCount;
    private bool recursiveSearchFallback = true; // Safety toggle

    #endregion

    #region Public Properties
    public int GetTotalCount() => totalCount;
    public int GetTotalInUseCount() => inUseCount;
    public int GetTotalNotInUseCount() => totalCount - inUseCount;
    #endregion

    #region Unity Lifecycle
    public void Awake()
    {
        // Singleton pattern
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        Debug.Log("Pool.Initialize()");

        // Initialize collections
        availableItems = new Dictionary<string, Stack<PoolItem>>();
        activeItemsByID = new Dictionary<int, PoolItem>();
        activeItemsByGUID = new Dictionary<string, PoolItem>();
        activeItemsByGroup = new Dictionary<string, HashSet<PoolItem>>();
        prefabCache = new Dictionary<string, GameObject>();

        totalCount = 0;
        inUseCount = 0;
    }
    
    // Removed Update() loop to save performance. UI is now event-driven.
    #endregion

    #region Public Methods

    // Helper to get all items (expensive, creates garbage, use sparingly)
    public List<PoolItem> GetAllPoolItems()
    {
        var list = new List<PoolItem>();
        list.AddRange(activeItemsByID.Values);
        foreach(var stack in availableItems.Values)
        {
            list.AddRange(stack);
        }
        return list;
    }

    public GameObject GetGameObject(string name, Vector2 position, Transform parent = null, string guid = "", string group = "")
    {
        if (string.IsNullOrEmpty(name))
        {
            Debug.LogError("Pool.GetGameObject() - name is null or empty.");
            return null;
        }

        string key = name.ToLower(); // Standardize key

        // 1. VALIDATE GUID (Architecture Security)
        if (!string.IsNullOrEmpty(guid))
        {
            if (activeItemsByGUID.ContainsKey(guid))
            {
                Debug.LogWarning($"Pool.GetGameObject() - GUID {guid} is already in use. Returning existing object.");
                return activeItemsByGUID[guid].unityGamoObjectInstantiated;
            }
        }
        else
        {
            // Even if no GUID provided, we assign one to internal logic if users ask for it later
            guid = System.Guid.NewGuid().ToString();
        }

        PoolItem item = null;

        // 2. RETRIEVE FROM POOL (Optimization: O(1))
        if (availableItems.TryGetValue(key, out Stack<PoolItem> stack) && stack.Count > 0)
        {
            item = stack.Pop();
        }
        else
        {
            // 3. CREATE NEW (Lazy Instantiation)
            item = CreateNewPoolItem(key);
            if (item == null) return null; // Prefab failed to load
        }

        // 4. SETUP ITEM
        ActivatePoolItem(item, guid, group, position, parent);

        UpdateUI();
        return item.unityGamoObjectInstantiated;
    }

    public bool ReturnGameObjectToPoolByGUID(string guid)
    {
        if (string.IsNullOrEmpty(guid)) return false;

        // Optimization: O(1) Lookup
        if (activeItemsByGUID.TryGetValue(guid, out PoolItem item))
        {
            ReturnInternal(item);
            return true;
        }

        Debug.Log($"Pool.ReturnGameObjectToPoolByGUID() - Could not find active object with GUID: {guid}");
        return false;
    }

    public bool ReturnGameObjectToPoolByUnityGameObjectID(int id)
    {
        // Optimization: O(1) Lookup
        if (activeItemsByID.TryGetValue(id, out PoolItem item))
        {
            ReturnInternal(item);
            return true;
        }
        
        // Silent fail is common for "cleanup" scripts that just try to destroy everything
        return false;
    }

    public void ReturnGameObjectToPoolByGroup(string group)
    {
        if (string.IsNullOrEmpty(group)) return;

        // Optimization: O(1) Lookup
        if (activeItemsByGroup.TryGetValue(group, out HashSet<PoolItem> groupSet))
        {
            // We must convert to list to avoid "Collection Modified" error during iteration
            var itemsToReturn = groupSet.ToList();
            foreach (var item in itemsToReturn)
            {
                ReturnInternal(item);
            }
        }
    }

    public void ReturnAllGameObjects(bool skipPlayers = true)
    {
        // Optimization: Instead of searching the hierarchy (slow), let's use our internal tracker
        // We create a copy of the values to avoid modification errors
        var allActive = activeItemsByID.Values.ToList();

        foreach (var item in allActive)
        {
            if (skipPlayers && item.unityGamoObjectInstantiated.CompareTag("Player")) continue;
            
            ReturnInternal(item);
        }
    }

    public bool AddExistingGameObjectToPool(GameObject gameObject, string name, string group = "")
    {
        if (gameObject == null || string.IsNullOrEmpty(name)) return false;

        string key = name.ToLower();
        string itemId = $"item_{nextItemId++}";

        var poolItem = new PoolItem
        {
            unityGamoObjectInstantiated = gameObject,
            name = key,
            group = group,
            inUse = false,
            guid = string.Empty,
            instanceId = gameObject.GetInstanceID(),
        };

        gameObject.SetActive(false);
        gameObject.transform.SetParent(transform);

        // Add to available stack
        if (!availableItems.ContainsKey(key))
            availableItems[key] = new Stack<PoolItem>();
        
        availableItems[key].Push(poolItem);
        totalCount++;
        
        UpdateUI();
        return true;
    }

    public void Preload(string name, int count, string group = "")
    {
        string key = name.ToLower();
        for (int i = 0; i < count; i++)
        {
            var item = CreateNewPoolItem(key);
            if (item != null)
            {
                // Ensure it's in the stack (CreateNewPoolItem returns it "loose")
                if (!availableItems.ContainsKey(key))
                    availableItems[key] = new Stack<PoolItem>();
                    
                availableItems[key].Push(item);
            }
        }
        UpdateUI();
    }

    // This method is rarely safe to use in a pool, but implemented for compatibility
    public bool DestroyGameObjectByGUID(string guid)
    {
        if (string.IsNullOrEmpty(guid)) return false;

        if (activeItemsByGUID.TryGetValue(guid, out PoolItem item))
        {
            DestroyInternal(item);
            return true;
        }
        return false;
    }
    
    public bool DestroyGameObjectByUnityGameObjectID(int id)
    {
        if (activeItemsByID.TryGetValue(id, out PoolItem item))
        {
            DestroyInternal(item);
            return true;
        }
        // Also check available items? (Expensive, skipping for now unless needed)
        return false;
    }
    #endregion

    #region Private Methods

    private void ActivatePoolItem(PoolItem item, string guid, string group, Vector2 position, Transform parent)
    {
        item.inUse = true;
        item.guid = guid;
        item.group = group;

        // Update Unity Object keys
        var t = item.unityGamoObjectInstantiated.transform;
        t.position = position;
        t.SetParent(parent ? parent : transform);
        item.unityGamoObjectInstantiated.SetActive(true);

        // REGISTER IN LOOKUPS (O(1))
        activeItemsByID[item.instanceId] = item;

        if (!string.IsNullOrEmpty(guid))
            activeItemsByGUID[guid] = item;

        if (!string.IsNullOrEmpty(group))
        {
            if (!activeItemsByGroup.ContainsKey(group))
                activeItemsByGroup[group] = new HashSet<PoolItem>();
            activeItemsByGroup[group].Add(item);
        }

        inUseCount++;
    }

    private void ReturnInternal(PoolItem item)
    {
        if (item == null || !item.inUse) return;

        // Cleanup Lookups (O(1))
        activeItemsByID.Remove(item.instanceId);

        if (!string.IsNullOrEmpty(item.guid))
            activeItemsByGUID.Remove(item.guid);

        if (!string.IsNullOrEmpty(item.group) && activeItemsByGroup.TryGetValue(item.group, out HashSet<PoolItem> set))
        {
            set.Remove(item);
            // Optional: Remove key if empty to save memory, or keep for speed
            if (set.Count == 0) activeItemsByGroup.Remove(item.group);
        }

        // Reset State
        item.inUse = false;
        item.guid = string.Empty;
        // item.group = string.Empty; // Optional: Keep group derived from creation? Usually safer to clear.

        // Unity Ops
        var go = item.unityGamoObjectInstantiated;
        if (go != null)
        {
            go.SetActive(false);
            go.transform.SetParent(transform);
        }

        // Return to Stack (O(1))
        if (!availableItems.ContainsKey(item.name))
            availableItems[item.name] = new Stack<PoolItem>();
        
        availableItems[item.name].Push(item);

        inUseCount--;
        UpdateUI();
    }

    private void DestroyInternal(PoolItem item)
    {
        // Remove from all lookups
        if (item.inUse)
        {
            ReturnInternal(item); // This moves it to available, but we want to destroy it
            // So we pop it back off
             if (availableItems.TryGetValue(item.name, out Stack<PoolItem> stack))
             {
                 if (stack.Count > 0) stack.Pop();
             }
        }
        else
        {
             // It's in the available stack... 
             // Removing from a stack is O(N) and hard. 
             // We mark the GameObject as null and let the system clean it up later 
             // OR we just destroy the GameObject and when we try to pop it next time, we check for null.
             // OPTION B: Lazy deletion
        }

        if (item.unityGamoObjectInstantiated != null)
            Destroy(item.unityGamoObjectInstantiated);

        totalCount--;
        UpdateUI();
    }

    private PoolItem CreateNewPoolItem(string name, string group = "")
    {
        GameObject prefab = GetPrefab(name);
        if (prefab == null) return null;

        GameObject instance = Instantiate(prefab);
        
        // Clean name (Less GC than string replace if we just set it)
        instance.name = name; 

        PoolItem poolItem = new PoolItem
        {
            unityGamoObjectInstantiated = instance,
            name = name,
            group = group,
            inUse = false, 
            guid = string.Empty,
            instanceId = instance.GetInstanceID(),
        };
        
        // NOT adding to totalCount/Stack here because this is a helper 
        // that returns a "floating" item. The caller decides where to put it.
        // However, for total count tracking, we increment here.
        totalCount++;
        
        return poolItem;
    }

    private GameObject GetPrefab(string name)
    {
        // 1. Check Cache
        if (prefabCache.TryGetValue(name, out GameObject cached))
            return cached;

        // 2. Try Direct Load
        GameObject loaded = Resources.Load<GameObject>(name);
        if (loaded != null)
        {
            prefabCache[name] = loaded;
            return loaded;
        }

        // 3. Recursive Fallback (Only if enabled)
        if (recursiveSearchFallback)
        {
            loaded = SearchPrefabRecursively("", name);
            if (loaded != null)
            {
                prefabCache[name] = loaded;
                return loaded;
            }
        }

        Debug.LogError($"Pool: Could not find prefab '{name}'");
        return null;
    }

    private GameObject SearchPrefabRecursively(string basePath, string prefabName)
    {
        // This is still expensive, but since we CACHE the result, it only happens ONCE per prefab type.
        var allPrefabs = Resources.LoadAll<GameObject>(basePath);

        foreach (var prefab in allPrefabs)
        {
            if (prefab.name.Equals(prefabName, System.StringComparison.OrdinalIgnoreCase))
                return prefab;
        }

        string[] commonFolders = { "Blocks", "Foliage", "Networked", "UI", "Equipment", "Rooms" };
        foreach (var folder in commonFolders)
        {
            var nestedPath = System.IO.Path.Combine(basePath, folder);
            // Resources.LoadAll only works on folders relative to Resources, not full paths
            // Implementing a simpler specific search based on known folders helps performance
            var p = Resources.Load<GameObject>(nestedPath + "/" + prefabName);
            if(p != null) return p;
        }

        return null;
    }

    private void UpdateUI()
    {
        if (textPool != null)
            textPool.text = $"Pool: {totalCount} | InUse: {inUseCount} | NotInUse: {totalCount - inUseCount}";
    }
    #endregion

    #region Nested Classes
    public class PoolItem
    {
        public string guid { get; set; }
        public bool inUse { get; set; }
        public string path { get; set; } // Legacy field kept for safety
        public string name { get; set; }
        public string group { get; set; }
        public GameObject unityGamoObjectInstantiated { get; set; }
        public int instanceId { get; set; }
    }
    #endregion
}