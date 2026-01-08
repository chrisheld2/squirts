using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text.RegularExpressions;

/// <summary>
/// Custom Unity Editor window that displays and edits PlayerPrefs.
/// Access via: Window -> USM -> PlayerPrefs Viewer
/// </summary>
public class PlayerPrefsViewerWindow : EditorWindow
{
    private Vector2 scrollPosition;
    private string searchFilter = "";
    private string newKeyName = "";
    private PlayerPrefsType newKeyType = PlayerPrefsType.String;
    
    // Defer actions to prevent GUI group imbalance
    private bool needsExitGUI = false;
    private PlayerPrefEntry entryToDelete = null;
    
    // List of keys to display
    private List<PlayerPrefEntry> entries = new List<PlayerPrefEntry>();
    
    // Foldout states
    private Dictionary<PlayerPrefsType, bool> groupFoldouts = new Dictionary<PlayerPrefsType, bool>();

    // GUI Styles
    private GUIStyle headerStyle;
    private GUIStyle sectionStyle;
    private GUIStyle keyStyle;
    private GUIStyle valueStyle;
    private GUIStyle searchCancelButtonStyle;
    private bool stylesInitialized = false;

    // Auto-refresh
    private float lastRefreshTime;
    private const float REFRESH_INTERVAL = 0.5f; // Refresh every 0.5 seconds

    private enum PlayerPrefsType { String, Int, Float }

    private class PlayerPrefEntry
    {
        public string Key;
        public PlayerPrefsType Type;
        public object Value;
        public bool IsDirty;
    }

    [MenuItem("Window/USM/PlayerPrefs Viewer")]
    public static void ShowWindow()
    {
        var window = GetWindow<PlayerPrefsViewerWindow>("PlayerPrefs");
        window.minSize = new Vector2(400, 400);
        window.Show();
    }

    private void OnEnable()
    {
        RefreshKeys();
        lastRefreshTime = (float)EditorApplication.timeSinceStartup;
        EditorApplication.update += OnEditorUpdate;
    }

    private void OnDisable()
    {
        EditorApplication.update -= OnEditorUpdate;
    }

    private void OnEditorUpdate()
    {
        // Auto-refresh values when playing
        if (EditorApplication.isPlaying)
        {
            float currentTime = (float)EditorApplication.timeSinceStartup;
            if (currentTime - lastRefreshTime >= REFRESH_INTERVAL)
            {
                lastRefreshTime = currentTime;
                RefreshValues();
                Repaint();
            }
        }
    }

    private void InitializeStyles()
    {
        if (stylesInitialized) return;

        headerStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 16,
            alignment = TextAnchor.MiddleLeft,
            normal = { textColor = new Color(0.7f, 0.5f, 1f) }
        };

        sectionStyle = new GUIStyle(EditorStyles.helpBox)
        {
            padding = new RectOffset(10, 10, 10, 10),
            margin = new RectOffset(5, 5, 5, 5)
        };

        keyStyle = new GUIStyle(EditorStyles.label)
        {
            fontSize = 12,
            fontStyle = FontStyle.Bold,
            normal = { textColor = new Color(0.4f, 0.9f, 1f) }
        };

        valueStyle = new GUIStyle(EditorStyles.textField)
        {
            fontSize = 12
        };

        // Try to find the internal search cancel button style
        searchCancelButtonStyle = GUI.skin.FindStyle("ToolbarSeachCancelButton"); // Legacy typo
        if (searchCancelButtonStyle == null)
        {
            searchCancelButtonStyle = GUI.skin.FindStyle("ToolbarSearchCancelButton"); // Correct spelling
        }
        if (searchCancelButtonStyle == null)
        {
            // Fallback to a standard button if not found
            searchCancelButtonStyle = new GUIStyle(EditorStyles.toolbarButton);
        }

        stylesInitialized = true;
    }

    private void OnGUI()
    {
        InitializeStyles();

        // Header
        EditorGUILayout.BeginVertical(sectionStyle);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("💾 PlayerPrefs Viewer", headerStyle);
        
        if (GUILayout.Button("🔄 Refresh", GUILayout.Width(80)))
        {
            RefreshKeys();
        }
        
        if (GUILayout.Button("🗑️ Delete All", GUILayout.Width(100)))
        {
            if (EditorUtility.DisplayDialog("Delete All PlayerPrefs", 
                "Are you sure you want to delete ALL PlayerPrefs? This cannot be undone.", "Yes", "No"))
            {
                PlayerPrefs.DeleteAll();
                PlayerPrefs.Save();
                RefreshKeys();
            }
        }
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.Space(5);
        
        // Add New Key Section
        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
        newKeyName = EditorGUILayout.TextField("New Key:", newKeyName);
        newKeyType = (PlayerPrefsType)EditorGUILayout.EnumPopup(newKeyType, GUILayout.Width(60));
        
        if (GUILayout.Button("Add", GUILayout.Width(50)))
        {
            if (!string.IsNullOrEmpty(newKeyName))
            {
                AddKey(newKeyName, newKeyType);
                newKeyName = "";
                GUI.FocusControl(null);
            }
        }
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.EndVertical();
        
        // Toolbar
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        
        if (GUILayout.Button("Expand All", EditorStyles.toolbarButton, GUILayout.Width(80)))
        {
            SetAllFoldouts(true);
            needsExitGUI = true;
        }
        if (GUILayout.Button("Collapse All", EditorStyles.toolbarButton, GUILayout.Width(80)))
        {
            SetAllFoldouts(false);
            needsExitGUI = true;
        }
        
        GUILayout.FlexibleSpace();

        // Search
        searchFilter = EditorGUILayout.TextField(searchFilter, EditorStyles.toolbarSearchField, GUILayout.Width(200));
        
        // Use the cached style, handle content based on whether we found the icon style or are using fallback
        string clearButtonText = searchCancelButtonStyle.name.Contains("Cancel") ? "" : "X";
        if (GUILayout.Button(clearButtonText, searchCancelButtonStyle))
        {
            searchFilter = "";
            GUI.FocusControl(null);
            needsExitGUI = true;
        }
        
        EditorGUILayout.EndHorizontal();

        // List
        EditorGUILayout.Space(5);
        
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        
        if (entries.Count == 0)
        {
            EditorGUILayout.HelpBox("No keys found. Try scanning project or adding manually.", MessageType.Info);
            if (GUILayout.Button("Scan Project for Keys"))
            {
                ScanProjectForKeys();
            }
        }
        else
        {
            // Filter entries
            var filteredEntries = entries.Where(e => string.IsNullOrEmpty(searchFilter) || 
                                              e.Key.ToLower().Contains(searchFilter.ToLower())).ToList();

            // Group by type
            var groupedEntries = filteredEntries.GroupBy(e => e.Type).OrderBy(g => g.Key);

            foreach (var group in groupedEntries)
            {
                if (!groupFoldouts.ContainsKey(group.Key))
                {
                    groupFoldouts[group.Key] = true;
                }

                string groupLabel = group.Key switch
                {
                    PlayerPrefsType.String => "📝 Strings",
                    PlayerPrefsType.Int => "🔢 Integers",
                    PlayerPrefsType.Float => "📏 Floats",
                    _ => "Unknown"
                };

                EditorGUILayout.Space(5);
                
                groupFoldouts[group.Key] = EditorGUILayout.Foldout(groupFoldouts[group.Key], $"{groupLabel} ({group.Count()})", true, EditorStyles.foldoutHeader);
                
                if (groupFoldouts[group.Key])
                {
                    foreach (var entry in group)
                    {
                        DrawEntry(entry);
                    }
                }
            }
            
            if (filteredEntries.Count == 0 && entries.Count > 0)
            {
                EditorGUILayout.LabelField("No matching keys found.");
            }
        }

        EditorGUILayout.EndScrollView();
        
        // Footer with Scan button if we have entries
        if (entries.Count > 0)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Scan Project for Keys", GUILayout.Width(150)))
            {
                ScanProjectForKeys();
            }
            EditorGUILayout.EndHorizontal();
        }

        // Finalize any deferred actions
        if (entryToDelete != null)
        {
            PlayerPrefs.DeleteKey(entryToDelete.Key);
            PlayerPrefs.Save();
            entries.Remove(entryToDelete);
            entryToDelete = null;
            needsExitGUI = true;
        }

        if (needsExitGUI)
        {
            needsExitGUI = false;
            GUIUtility.ExitGUI();
        }
    }

    private void DrawEntry(PlayerPrefEntry entry)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.BeginHorizontal();
        
        // Icon based on type
        string icon = entry.Type switch
        {
            PlayerPrefsType.String => "📝",
            PlayerPrefsType.Int => "🔢",
            PlayerPrefsType.Float => "📏",
            _ => "?"
        };
        
        EditorGUILayout.LabelField($"{icon} {entry.Key}", keyStyle, GUILayout.Width(200));
        
        // Value Field
        EditorGUI.BeginChangeCheck();
        object newValue = null;

        switch (entry.Type)
        {
            case PlayerPrefsType.String:
                newValue = EditorGUILayout.TextField((string)entry.Value);
                break;

            case PlayerPrefsType.Int:
                newValue = EditorGUILayout.IntField((int)entry.Value);
                break;

            case PlayerPrefsType.Float:
                newValue = EditorGUILayout.FloatField((float)entry.Value);
                break;
        }

        if (EditorGUI.EndChangeCheck() && newValue != null)
        {
            entry.Value = newValue;
            switch (entry.Type)
            {
                case PlayerPrefsType.String:
                    PlayerPrefs.SetString(entry.Key, (string)newValue);
                    break;
                case PlayerPrefsType.Int:
                    PlayerPrefs.SetInt(entry.Key, (int)newValue);
                    break;
                case PlayerPrefsType.Float:
                    PlayerPrefs.SetFloat(entry.Key, (float)newValue);
                    break;
            }
            entry.IsDirty = true;
        }

        GUILayout.FlexibleSpace();

        // Save button - always render to maintain consistent GUI layout
        EditorGUI.BeginDisabledGroup(!entry.IsDirty);
        if (GUILayout.Button("💾", GUILayout.Width(30)))
        {
            PlayerPrefs.Save();
            entry.IsDirty = false;
        }
        EditorGUI.EndDisabledGroup();

        if (GUILayout.Button("❌", GUILayout.Width(30)))
        {
            if (EditorUtility.DisplayDialog("Delete Key", $"Delete '{entry.Key}'?", "Yes", "No"))
            {
                entryToDelete = entry;
            }
        }
        
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
    }

    private void AddKey(string key, PlayerPrefsType type)
    {
        if (entries.Any(e => e.Key == key)) return;

        switch (type)
        {
            case PlayerPrefsType.String:
                PlayerPrefs.SetString(key, "");
                break;
            case PlayerPrefsType.Int:
                PlayerPrefs.SetInt(key, 0);
                break;
            case PlayerPrefsType.Float:
                PlayerPrefs.SetFloat(key, 0f);
                break;
        }
        PlayerPrefs.Save();
        RefreshKeys();
    }

    private void SetAllFoldouts(bool state)
    {
        foreach (PlayerPrefsType type in System.Enum.GetValues(typeof(PlayerPrefsType)))
        {
            groupFoldouts[type] = state;
        }
    }

    private void RefreshKeys()
    {
        // Since we can't iterate keys natively, we rely on our known list + scanning
        // We'll keep existing entries that still exist
        // And we'll verify their values

        List<PlayerPrefEntry> validEntries = new List<PlayerPrefEntry>();

        // 1. Check existing entries
        foreach (var entry in entries)
        {
            if (PlayerPrefs.HasKey(entry.Key))
            {
                UpdateEntryValue(entry);
                validEntries.Add(entry);
            }
        }

        // 2. Add known common keys if they exist
        AddKnownKeyIfExists(validEntries, "USMGameSpeed", PlayerPrefsType.Float);
        AddKnownKeyIfExists(validEntries, "TimeScale", PlayerPrefsType.Float);
        AddKnownKeyIfExists(validEntries, "sessionKey", PlayerPrefsType.String);
        AddKnownKeyIfExists(validEntries, "LevelScript_LastProfile", PlayerPrefsType.String);

        // 3. Add LevelScript dynamic keys if possible
        if (PlayerPrefs.HasKey("LevelScript_LastProfile"))
        {
            string profile = PlayerPrefs.GetString("LevelScript_LastProfile");
            string prefix = $"LevelScript_Profile_{profile}_";

            string[] levelKeys = new string[]
            {
                "GrassChance", "GrassCoverChance", "MushroomChance", "StoneChance", "WebChance", "GemChance",
                "PathWidth", "SecondaryPathWidth", "Width", "Height",
                "PerlinScale", "PerlinThreshold", "PerlinWidth", "PerlinHeight",
                "RandomSeed", "BlockBackDistance", "BlockBackMinDistance", "BlockBackMaxDistance",
                "BlockBackRandomDistance", "BlockBackPatchMinSize", "BlockBackPatchMaxSize",
                "DecorationPatchMinSize", "DecorationPatchMaxSize",
                "GlobalLightIntensity", "CellSize", "RegenerationDelay"
            };

            foreach (var key in levelKeys)
            {
                // Try float first (most are floats)
                // Actually we need to know the type.
                // From LevelScript.cs:
                // Ints: PathWidth, SecondaryPathWidth, Width, Height, RandomSeed, BlockBack...
                // Floats: Chances, Perlin..., GlobalLight..., CellSize, RegenerationDelay

                string fullKey = prefix + key;
                if (key.Contains("Width") || key.Contains("Height") || key.Contains("Seed") || key.Contains("Distance") || key.Contains("Size"))
                {
                    // Heuristic: Size/Width/Height/Distance/Seed are usually Ints in this project,
                    // EXCEPT PerlinWidth/Height/CellSize/RegenerationDelay which are floats.
                    if (key.StartsWith("Perlin") || key == "CellSize" || key == "RegenerationDelay")
                        AddKnownKeyIfExists(validEntries, fullKey, PlayerPrefsType.Float);
                    else
                        AddKnownKeyIfExists(validEntries, fullKey, PlayerPrefsType.Int);
                }
                else
                {
                    AddKnownKeyIfExists(validEntries, fullKey, PlayerPrefsType.Float);
                }
            }
        }

        entries = validEntries.GroupBy(e => e.Key).Select(g => g.First()).OrderBy(e => e.Key).ToList();
    }

    /// <summary>
    /// Lightweight refresh that only updates values of existing entries (used for auto-refresh during play mode).
    /// </summary>
    private void RefreshValues()
    {
        foreach (var entry in entries)
        {
            if (PlayerPrefs.HasKey(entry.Key) && !entry.IsDirty)
            {
                UpdateEntryValue(entry);
            }
        }
    }

    private void AddKnownKeyIfExists(List<PlayerPrefEntry> list, string key, PlayerPrefsType type)
    {
        if (PlayerPrefs.HasKey(key))
        {
            var entry = new PlayerPrefEntry { Key = key, Type = type };
            UpdateEntryValue(entry);
            list.Add(entry);
        }
    }

    private void UpdateEntryValue(PlayerPrefEntry entry)
    {
        switch (entry.Type)
        {
            case PlayerPrefsType.String:
                entry.Value = PlayerPrefs.GetString(entry.Key);
                break;
            case PlayerPrefsType.Int:
                entry.Value = PlayerPrefs.GetInt(entry.Key);
                break;
            case PlayerPrefsType.Float:
                entry.Value = PlayerPrefs.GetFloat(entry.Key);
                break;
        }
    }

    private void ScanProjectForKeys()
    {
        string[] files = Directory.GetFiles(Application.dataPath, "*.cs", SearchOption.AllDirectories);
        int foundCount = 0;

        foreach (string file in files)
        {
            string content = File.ReadAllText(file);
            
            // Regex for SetString/Int/Float("KEY"
            var matches = Regex.Matches(content, @"PlayerPrefs\.(Set|Get)(String|Int|Float)\s*\(\s*""([^""]+)""");
            
            foreach (Match match in matches)
            {
                string key = match.Groups[3].Value;
                string typeStr = match.Groups[2].Value;
                
                PlayerPrefsType type = typeStr switch
                {
                    "String" => PlayerPrefsType.String,
                    "Int" => PlayerPrefsType.Int,
                    "Float" => PlayerPrefsType.Float,
                    _ => PlayerPrefsType.String
                };

                if (PlayerPrefs.HasKey(key))
                {
                    if (!entries.Any(e => e.Key == key))
                    {
                        var entry = new PlayerPrefEntry { Key = key, Type = type };
                        UpdateEntryValue(entry);
                        entries.Add(entry);
                        foundCount++;
                    }
                }
            }
        }
        
        // Sort
        entries = entries.OrderBy(e => e.Key).ToList();
        
        EditorUtility.DisplayDialog("Scan Complete", $"Found {foundCount} new keys currently stored in PlayerPrefs.", "OK");
    }
}
