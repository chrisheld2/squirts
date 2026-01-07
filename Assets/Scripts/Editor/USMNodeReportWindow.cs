using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Text;

/// <summary>
/// Custom Unity Editor window that displays the USM Node Report in real-time.
/// Access via: Window -> USM -> Node Report
/// </summary>
public class USMNodeReportWindow : EditorWindow
{
    private Vector2 scrollPosition;
    private UnifiedSyncMatrix syncMatrix;
    private USMGame usmGame;
    private double lastUpdateTime;
    private const double UPDATE_INTERVAL = 0.3; // Update every 0.3 seconds

    // View mode toggle
    private bool isCondensedView = false;

    // Foldout states - track which nodes have their component properties expanded
    private Dictionary<string, Dictionary<string, bool>> componentFoldouts = new Dictionary<string, Dictionary<string, bool>>();

    // Foldout states for node type groups in condensed view
    private bool staticNodesFoldout = true;
    private bool activeNodesFoldout = true;
    private bool copyNodesFoldout = true;

    // Search and filter states
    private bool enableTextSearch = false;
    private string searchText = "";
    private int nodeTypeFilter = 0; // 0 = All, 1 = Static, 2 = Active
    private int liveFilter = 0; // 0 = All, 1 = LIVE Only, 2 = COPY Only
    private List<string> searchHistory = new List<string>();
    private const int MAX_SEARCH_HISTORY = 10;

    // GUI Styles
    private GUIStyle headerStyle;
    private GUIStyle nodeHeaderStyle;
    private GUIStyle labelStyle;
    private GUIStyle valueStyle;
    private GUIStyle sectionStyle;
    private bool stylesInitialized = false;

    [MenuItem("Window/USM/Node Report")]
    public static void ShowWindow()
    {
        var window = GetWindow<USMNodeReportWindow>("USM Node Report");
        window.minSize = new Vector2(400, 300);
        window.Show();
    }

    private void OnEnable()
    {
        // Subscribe to editor update
        EditorApplication.update += OnEditorUpdate;
        lastUpdateTime = EditorApplication.timeSinceStartup;

        // Load saved preferences
        LoadPreferences();
    }

    private void OnDisable()
    {
        EditorApplication.update -= OnEditorUpdate;

        // Save preferences
        SavePreferences();
    }

    private void LoadPreferences()
    {
        enableTextSearch = EditorPrefs.GetBool("USMNodeReport_EnableTextSearch", false);
        searchText = EditorPrefs.GetString("USMNodeReport_SearchText", "");
        nodeTypeFilter = EditorPrefs.GetInt("USMNodeReport_NodeTypeFilter", 0);
        liveFilter = EditorPrefs.GetInt("USMNodeReport_LiveFilter", 0);
        isCondensedView = EditorPrefs.GetBool("USMNodeReport_CondensedView", false);

        // Load search history
        searchHistory.Clear();
        int historyCount = EditorPrefs.GetInt("USMNodeReport_SearchHistoryCount", 0);
        for (int i = 0; i < historyCount; i++)
        {
            string historyItem = EditorPrefs.GetString($"USMNodeReport_SearchHistory_{i}", "");
            if (!string.IsNullOrEmpty(historyItem))
            {
                searchHistory.Add(historyItem);
            }
        }
    }

    private void SavePreferences()
    {
        EditorPrefs.SetBool("USMNodeReport_EnableTextSearch", enableTextSearch);
        EditorPrefs.SetString("USMNodeReport_SearchText", searchText);
        EditorPrefs.SetInt("USMNodeReport_NodeTypeFilter", nodeTypeFilter);
        EditorPrefs.SetInt("USMNodeReport_LiveFilter", liveFilter);
        EditorPrefs.SetBool("USMNodeReport_CondensedView", isCondensedView);

        // Save search history
        EditorPrefs.SetInt("USMNodeReport_SearchHistoryCount", searchHistory.Count);
        for (int i = 0; i < searchHistory.Count; i++)
        {
            EditorPrefs.SetString($"USMNodeReport_SearchHistory_{i}", searchHistory[i]);
        }
    }

    private void AddToSearchHistory(string search)
    {
        if (string.IsNullOrWhiteSpace(search))
            return;

        // Remove if already exists (to move to top)
        searchHistory.Remove(search);

        // Add to beginning
        searchHistory.Insert(0, search);

        // Limit history size
        if (searchHistory.Count > MAX_SEARCH_HISTORY)
        {
            searchHistory.RemoveRange(MAX_SEARCH_HISTORY, searchHistory.Count - MAX_SEARCH_HISTORY);
        }

        SavePreferences();
    }

    private void ShowSearchHistoryMenu()
    {
        // Add current search to history if it's not empty
        if (!string.IsNullOrWhiteSpace(searchText))
        {
            AddToSearchHistory(searchText);
        }

        GenericMenu menu = new GenericMenu();

        if (searchHistory.Count == 0)
        {
            menu.AddDisabledItem(new GUIContent("No search history"));
        }
        else
        {
            foreach (string historyItem in searchHistory)
            {
                string item = historyItem; // Capture for lambda
                menu.AddItem(new GUIContent(item), false, () =>
                {
                    searchText = item;
                    Repaint();
                });
            }

            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Clear History"), false, () =>
            {
                searchHistory.Clear();
                SavePreferences();
                Repaint();
            });
        }

        menu.ShowAsContext();
    }

    private void OnEditorUpdate()
    {
        // Auto-refresh every second when playing
        if (EditorApplication.isPlaying &&
            EditorApplication.timeSinceStartup - lastUpdateTime >= UPDATE_INTERVAL)
        {
            lastUpdateTime = EditorApplication.timeSinceStartup;
            Repaint();
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

        nodeHeaderStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 15,
            normal = { textColor = Color.white }
        };

        labelStyle = new GUIStyle(EditorStyles.label)
        {
            fontSize = 13,
            normal = { textColor = new Color(0.7f, 0.7f, 0.7f) }
        };

        valueStyle = new GUIStyle(EditorStyles.label)
        {
            fontSize = 13,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white }
        };

        sectionStyle = new GUIStyle(EditorStyles.helpBox)
        {
            padding = new RectOffset(10, 10, 10, 10),
            margin = new RectOffset(5, 5, 5, 5)
        };

        stylesInitialized = true;
    }

    private void OnGUI()
    {
        InitializeStyles();

        // Find the UnifiedSyncMatrix in the scene
        if (syncMatrix == null && EditorApplication.isPlaying)
        {
            syncMatrix = FindObjectOfType<UnifiedSyncMatrix>();
        }

        if (usmGame == null && EditorApplication.isPlaying)
        {
            usmGame = FindObjectOfType<USMGame>();
        }

        // Header
        EditorGUILayout.BeginVertical(sectionStyle);
        EditorGUILayout.LabelField("🎮 USM Node Report", headerStyle);

        if (!EditorApplication.isPlaying)
        {
            EditorGUILayout.HelpBox("Enter Play Mode to view node data", MessageType.Info);
            EditorGUILayout.EndVertical();
            return;
        }

        if (syncMatrix == null)
        {
            EditorGUILayout.HelpBox("UnifiedSyncMatrix not found in scene", MessageType.Warning);
            EditorGUILayout.EndVertical();
            return;
        }

        var nodes = syncMatrix.GetAllNodes();
        if (nodes == null || nodes.Count == 0)
        {
            EditorGUILayout.HelpBox("No USMNodes registered", MessageType.Info);
            EditorGUILayout.EndVertical();
            return;
        }

        // First row - Total Nodes and Game Mode
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"Total Nodes: {nodes.Count}", EditorStyles.miniLabel, GUILayout.Width(150));
        EditorGUILayout.LabelField($"Game Mode: {syncMatrix.GameMode}", EditorStyles.miniLabel, GUILayout.Width(200));
        EditorGUILayout.LabelField($"Last Update: {System.DateTime.Now:HH:mm:ss}", EditorStyles.miniLabel);
        EditorGUILayout.EndHorizontal();

        // Second row - Game Speed (more prominent)
        if (usmGame != null)
        {
            EditorGUILayout.Space(3);
            EditorGUILayout.BeginHorizontal();
            GUIStyle speedLabelStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 12,
                normal = { textColor = new Color(0.4f, 0.9f, 1f) }
            };
            GUI.color = new Color(0.4f, 0.9f, 1f);
            EditorGUILayout.LabelField("⚡ Game Speed:", speedLabelStyle, GUILayout.Width(100));

            GUIStyle speedValueStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 13,
                normal = { textColor = new Color(0.5f, 1f, 0.5f) }
            };
            GUI.color = new Color(0.5f, 1f, 0.5f);
            EditorGUILayout.LabelField($"{usmGame.GameSpeed:F2}x", speedValueStyle, GUILayout.Width(60));

            GUI.color = Color.white;
            EditorGUILayout.EndHorizontal();
        }

        // Toggle button for view mode
        EditorGUILayout.Space(5);
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();

        string buttonText = isCondensedView ? "📋 Switch to Detailed View" : "📝 Switch to Condensed View";
        if (GUILayout.Button(buttonText, GUILayout.Width(200), GUILayout.Height(25)))
        {
            isCondensedView = !isCondensedView;
        }

        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();

        // Search and Filter Controls
        EditorGUILayout.Space(5);
        DisplaySearchAndFilters();

        EditorGUILayout.Space(5);

        // Display Client List and Events List (only in detailed view)
        if (!isCondensedView)
        {
            DisplayClientList();
            EditorGUILayout.Space(5);
            DisplayEventsList();
        }

        EditorGUILayout.Space(5);

        // Scrollable area for nodes
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        // Group nodes by type in both views
        DisplayNodesByType(nodes, isCondensedView);

        EditorGUILayout.EndScrollView();
    }

    private void DisplaySearchAndFilters()
    {
        EditorGUILayout.BeginVertical(sectionStyle);

        // Text Search Toggle and Field
        EditorGUILayout.BeginHorizontal();

        bool previousEnableTextSearch = enableTextSearch;
        enableTextSearch = EditorGUILayout.Toggle("Enable Text Search", enableTextSearch, GUILayout.Width(150));

        GUI.enabled = enableTextSearch;

        // Text field with Enter key detection
        GUI.SetNextControlName("SearchTextField");
        string previousSearchText = searchText;
        searchText = EditorGUILayout.TextField(searchText, GUILayout.ExpandWidth(true));

        // Detect Enter key press
        if (Event.current.type == EventType.KeyDown &&
            Event.current.keyCode == KeyCode.Return &&
            GUI.GetNameOfFocusedControl() == "SearchTextField" &&
            !string.IsNullOrWhiteSpace(searchText))
        {
            AddToSearchHistory(searchText);
            Event.current.Use(); // Consume the event
        }

        // Clear button
        if (GUILayout.Button("✕", GUILayout.Width(25)))
        {
            searchText = "";
            GUI.FocusControl(null); // Clear focus from text field
        }

        // History dropdown button
        if (GUILayout.Button("▼", GUILayout.Width(25)))
        {
            ShowSearchHistoryMenu();
        }

        GUI.enabled = true;

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(5);

        // Filter Options Row 1 - Node Type
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Node Type:", GUILayout.Width(70));
        nodeTypeFilter = GUILayout.Toolbar(nodeTypeFilter, new string[] { "All", "STATIC", "ACTIVE" }, GUILayout.Width(240));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(3);

        // Filter Options Row 2 - Live Status
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Live Status:", GUILayout.Width(70));
        liveFilter = GUILayout.Toolbar(liveFilter, new string[] { "All", "LIVE Only", "COPY Only" }, GUILayout.Width(240));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
    }

    private bool PassesFilters(USMNode node)
    {
        // Text search filter
        if (enableTextSearch && !string.IsNullOrEmpty(searchText))
        {
            string lowerSearch = searchText.ToLower();
            bool matchesName = node.gameObject.name.ToLower().Contains(lowerSearch);
            bool matchesID = node.NodeID.ToLower().Contains(lowerSearch);

            if (!matchesName && !matchesID)
                return false;
        }

        // Node type filter
        if (nodeTypeFilter == 1 && node.NodeType != USMNode.NODETYPE.STATIC)
            return false;
        if (nodeTypeFilter == 2 && node.NodeType != USMNode.NODETYPE.ACTIVE)
            return false;

        // Live status filter (0 = All, 1 = LIVE Only, 2 = COPY Only)
        if (liveFilter == 1 && !node.LiveNode)
            return false;
        if (liveFilter == 2 && node.LiveNode)
            return false;

        return true;
    }

    private void DisplayNodesByType(Dictionary<string, GameObject> nodes, bool condensed)
    {
        // Separate nodes by type
        List<USMNode> staticNodes = new List<USMNode>();
        List<USMNode> activeNodes = new List<USMNode>();
        List<USMNode> copyNodes = new List<USMNode>();

        foreach (var kvp in nodes)
        {
            if (kvp.Value == null) continue;

            USMNode node = kvp.Value.GetComponent<USMNode>();
            if (node == null) continue;

            // Apply filters
            if (!PassesFilters(node))
                continue;

            switch (node.NodeType)
            {
                case USMNode.NODETYPE.STATIC:
                    staticNodes.Add(node);
                    break;
                case USMNode.NODETYPE.ACTIVE:
                    activeNodes.Add(node);
                    break;
            }
        }

        // Display STATIC nodes group
        if (staticNodes.Count > 0)
        {
            DisplayNodeTypeGroup("🔴 STATIC Nodes", staticNodes.Count, new Color(0.8f, 0.4f, 0.5f), ref staticNodesFoldout, staticNodes, condensed);
        }

        // Display ACTIVE nodes group
        if (activeNodes.Count > 0)
        {
            DisplayNodeTypeGroup("⚡ ACTIVE Nodes", activeNodes.Count, new Color(0.0f, 0.85f, 0.78f), ref activeNodesFoldout, activeNodes, condensed);
        }

        // Display COPY nodes group
        if (copyNodes.Count > 0)
        {
            DisplayNodeTypeGroup("📋 COPY Nodes", copyNodes.Count, new Color(0.98f, 0.74f, 0.02f), ref copyNodesFoldout, copyNodes, condensed);
        }
    }

    private void DisplayNodeTypeGroup(string title, int count, Color color, ref bool foldoutState, List<USMNode> nodes, bool condensed)
    {
        EditorGUILayout.Space(3);
        GUI.backgroundColor = color;
        EditorGUILayout.BeginVertical(sectionStyle);
        GUI.backgroundColor = Color.white;

        // Group header with foldout
        GUIStyle groupHeaderStyle = new GUIStyle(EditorStyles.foldout)
        {
            fontSize = 14,
            fontStyle = FontStyle.Bold,
            normal = { textColor = color },
            onNormal = { textColor = color },
            hover = { textColor = color },
            onHover = { textColor = color },
            focused = { textColor = color },
            onFocused = { textColor = color },
            active = { textColor = color },
            onActive = { textColor = color }
        };

        string displayText = $"{title} ({count})";
        foldoutState = EditorGUILayout.Foldout(foldoutState, displayText, true, groupHeaderStyle);

        // Display nodes if expanded
        if (foldoutState)
        {
            EditorGUILayout.Space(3);
            foreach (var node in nodes)
            {
                // Skip destroyed nodes
                if (node == null)
                    continue;

                if (condensed)
                {
                    DisplayNodeCondensed(node);
                }
                else
                {
                    DisplayNode(node);
                }
            }
        }

        EditorGUILayout.EndVertical();
    }

    private void DisplayNodeCondensed(USMNode node)
    {
        // Check if node has been destroyed
        if (node == null || !node)
            return;

        Color borderColor = GetNodeTypeColor(node.NodeType);

        // Create a colored border effect
        GUI.backgroundColor = borderColor;
        EditorGUILayout.BeginVertical(sectionStyle);
        GUI.backgroundColor = Color.white;

        EditorGUILayout.BeginHorizontal();

        // Node ID
        EditorGUILayout.LabelField($"{node.NodeID}", nodeHeaderStyle, GUILayout.Width(90));

        // GameObject Name (green if LiveNode is true)
        GUI.color = node.LiveNode ? Color.green : Color.white;
        GUIStyle nameStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 14 };
        EditorGUILayout.LabelField(node.gameObject.name, nameStyle, GUILayout.Width(120));
        GUI.color = Color.white;

        // Client GUID
        NetworkMessage cachedData = node.GetCachedSyncData();
        if (cachedData != null && !string.IsNullOrEmpty(cachedData.ClientGUID))
        {
            GUI.color = new Color(1f, 0.9f, 0.4f);
            EditorGUILayout.LabelField($"👤 {cachedData.ClientGUID}", EditorStyles.miniLabel, GUILayout.Width(150));
            GUI.color = Color.white;
        }
        else
        {
            GUI.color = Color.gray;
            EditorGUILayout.LabelField("👤 No Client", EditorStyles.miniLabel, GUILayout.Width(150));
            GUI.color = Color.white;
        }

        // Event Subscription Status Column
        bool isSubscribed = IsNodeSubscribedToNetworkUpdate(node);
        GUIStyle eventStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 12 };

        if (isSubscribed)
        {
            GUI.color = new Color(0.5f, 1f, 0.5f);
            EditorGUILayout.LabelField("🔗 Linked", eventStyle, GUILayout.Width(70));
        }
        else
        {
            GUI.color = new Color(1f, 0.3f, 0.3f);
            EditorGUILayout.LabelField("⚠️ NOT Linked", eventStyle, GUILayout.Width(90));
        }
        GUI.color = Color.white;

        // Spacer to push delete button to the right
        GUILayout.FlexibleSpace();

        // Delete button
        GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
        if (GUILayout.Button("🗑️", GUILayout.Width(30), GUILayout.Height(20)))
        {
            // Unregister from network first (sends delete message to other clients)
            if (syncMatrix != null && !string.IsNullOrEmpty(node.NodeID))
            {
                syncMatrix.UnregisterNode(node.NodeID, syncNetwork: true);
            }
            // Then destroy the local GameObject
            DestroyImmediate(node.gameObject);
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(2);
    }

    private void DisplayClientList()
    {
        var clientList = syncMatrix.GetClientGUIDList();
        string currentClientGUID = syncMatrix.ClientGUID;
        var nodes = syncMatrix.GetAllNodes();

        EditorGUILayout.BeginVertical(sectionStyle);
        EditorGUILayout.LabelField("👥 Connected Clients", EditorStyles.boldLabel);

        if (clientList == null || clientList.Count == 0)
        {
            EditorGUILayout.LabelField("No clients connected", EditorStyles.miniLabel);
        }
        else
        {
            EditorGUILayout.LabelField($"Total: {clientList.Count}", EditorStyles.miniLabel);
            EditorGUILayout.Space(3);

            int clientIndex = 0;
            foreach (var clientGUID in clientList)
            {
                clientIndex++;

                // Find nodes associated with this client
                List<string> clientNodeIDs = new List<string>();
                if (nodes != null)
                {
                    foreach (var kvp in nodes)
                    {
                        if (kvp.Value != null)
                        {
                            USMNode node = kvp.Value.GetComponent<USMNode>();
                            if (node != null)
                            {
                                NetworkMessage cachedData = node.GetCachedSyncData();
                                if (cachedData != null && cachedData.ClientGUID == clientGUID)
                                {
                                    clientNodeIDs.Add(node.NodeID);
                                }
                            }
                        }
                    }
                }

                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(10);

                // Highlight current client
                bool isCurrentClient = clientGUID == currentClientGUID;
                GUI.color = isCurrentClient ? new Color(0.5f, 1f, 0.5f) : new Color(0.8f, 0.8f, 1f);

                string clientLabel = isCurrentClient ? $"👤 Client #{clientIndex} (You)" : $"👤 Client #{clientIndex}";
                EditorGUILayout.LabelField(clientLabel, GUILayout.Width(120));

                GUI.color = new Color(1f, 0.9f, 0.4f);
                EditorGUILayout.LabelField(clientGUID, EditorStyles.miniLabel, GUILayout.Width(150));

                // Display associated node IDs
                if (clientNodeIDs.Count > 0)
                {
                    GUI.color = new Color(0.7f, 1f, 0.7f);
                    string nodeIDsText = $"Nodes: {string.Join(", ", clientNodeIDs)}";
                    EditorGUILayout.LabelField(nodeIDsText, EditorStyles.miniLabel);
                }
                else
                {
                    GUI.color = Color.gray;
                    EditorGUILayout.LabelField("No nodes", EditorStyles.miniLabel);
                }

                GUI.color = Color.white;
                EditorGUILayout.EndHorizontal();
            }
        }

        EditorGUILayout.EndVertical();
    }

    private void DisplayEventsList()
    {
        var eventsList = syncMatrix.GetEventsList();

        EditorGUILayout.BeginVertical(sectionStyle);
        EditorGUILayout.LabelField("📡 Events List", EditorStyles.boldLabel);

        if (eventsList == null || eventsList.Count == 0)
        {
            EditorGUILayout.LabelField("No events registered", EditorStyles.miniLabel);
        }
        else
        {
            EditorGUILayout.LabelField($"Total: {eventsList.Count}", EditorStyles.miniLabel);
            EditorGUILayout.Space(3);

            foreach (var entry in eventsList)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(10);

                GUI.color = new Color(0.5f, 0.8f, 1f); // Light blue
                EditorGUILayout.LabelField(entry.senderID ?? "null", GUILayout.Width(150));

                GUI.color = Color.white;
                EditorGUILayout.LabelField("→", GUILayout.Width(20));

                GUI.color = new Color(1f, 0.9f, 0.4f); // Yellow
                EditorGUILayout.LabelField(entry.receiverID ?? "null", GUILayout.Width(150));

                GUI.color = Color.white;
                EditorGUILayout.EndHorizontal();
            }
        }

        EditorGUILayout.EndVertical();
    }

    private bool IsNodeSubscribedToNetworkUpdate(USMNode node)
    {
        if (syncMatrix == null)
            return false;

        // Use reflection to check if the node's SyncFromNetwork method is subscribed to the event
        var eventField = typeof(UnifiedSyncMatrix).GetField("OnNetworkUpdate",
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.NonPublic);

        if (eventField == null)
            return false;

        var eventDelegate = eventField.GetValue(syncMatrix) as System.Delegate;
        if (eventDelegate == null)
            return false;

        // Get the invocation list and check if our node's SyncFromNetwork is in there
        var invocationList = eventDelegate.GetInvocationList();
        foreach (var handler in invocationList)
        {
            if (handler.Target == node)
            {
                return true;
            }
        }

        return false;
    }

    private void DisplayNode(USMNode node)
    {
        // Check if node has been destroyed
        if (node == null || !node)
            return;

        Color borderColor = GetNodeTypeColor(node.NodeType);

        // Create a colored border effect
        GUI.backgroundColor = borderColor;
        EditorGUILayout.BeginVertical(sectionStyle);
        GUI.backgroundColor = Color.white;

        // Node header
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(node.gameObject.name, nodeHeaderStyle, GUILayout.Width(150));

        // Node ID with larger font and different color
        GUIStyle nodeIDStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 16,
            normal = { textColor = new Color(1f, 0.9f, 0.4f) } // Yellow color
        };
        GUI.color = new Color(1f, 0.9f, 0.4f);
        EditorGUILayout.LabelField(node.NodeID, nodeIDStyle, GUILayout.Width(100));
        GUI.color = Color.white;

        // Client GUID on same line
        NetworkMessage cachedDataForGUID = node.GetCachedSyncData();
        if (cachedDataForGUID != null && !string.IsNullOrEmpty(cachedDataForGUID.ClientGUID))
        {
            GUI.color = new Color(1f, 0.9f, 0.4f);
            EditorGUILayout.LabelField($"👤 {cachedDataForGUID.ClientGUID}", valueStyle);
            GUI.color = Color.white;
        }
        else
        {
            GUI.color = Color.gray;
            EditorGUILayout.LabelField("👤 No Client", EditorStyles.miniLabel);
            GUI.color = Color.white;
        }

        // Spacer to push delete button to the right
        GUILayout.FlexibleSpace();

        // Delete button
        GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
        if (GUILayout.Button("🗑️ Delete", GUILayout.Width(70), GUILayout.Height(25)))
        {
            // Unregister from network first (sends delete message to other clients)
            if (syncMatrix != null && !string.IsNullOrEmpty(node.NodeID))
            {
                syncMatrix.UnregisterNode(node.NodeID, syncNetwork: true);
            }
            // Then destroy the local GameObject
            DestroyImmediate(node.gameObject);
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(3);

        // Live Node status
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("💫 Live Node:", labelStyle, GUILayout.Width(120));
        GUI.color = node.LiveNode ? Color.green : Color.gray;
        EditorGUILayout.LabelField($"{(node.LiveNode ? "🟢" : "⚪")} {node.LiveNode}", valueStyle);
        GUI.color = Color.white;
        EditorGUILayout.EndHorizontal();

        // Event Name
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("📡 Event:", labelStyle, GUILayout.Width(120));
        EditorGUILayout.LabelField(node.SyncEventName, valueStyle);
        EditorGUILayout.EndHorizontal();

        // Network Event Subscription Status
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("🔗 Event Subscribed:", labelStyle, GUILayout.Width(120));
        bool isSubscribed = IsNodeSubscribedToNetworkUpdate(node);
        GUI.color = isSubscribed ? Color.green : Color.red;
        EditorGUILayout.LabelField($"{(isSubscribed ? "🟢" : "🔴")} {isSubscribed}", valueStyle);
        GUI.color = Color.white;
        EditorGUILayout.EndHorizontal();

        // Cached Sync Data
        NetworkMessage cachedData = node.GetCachedSyncData();
        if (cachedData != null && HasSyncData(cachedData))
        {
            EditorGUILayout.Space(3);
            EditorGUILayout.LabelField("🔧 Cached Sync Data:", EditorStyles.boldLabel);
            DisplaySyncData(cachedData);
        }

        // Syncable Components with collapsible properties
        var syncableComponents = node.gameObject.GetComponents<IUSMNetworkSync>();
        if (syncableComponents.Length > 0)
        {
            EditorGUILayout.Space(3);
            EditorGUILayout.LabelField($"🔌 Syncable Components ({syncableComponents.Length}):", EditorStyles.boldLabel);

            // Ensure this node has a foldout dictionary
            if (!componentFoldouts.ContainsKey(node.NodeID))
            {
                componentFoldouts[node.NodeID] = new Dictionary<string, bool>();
            }

            foreach (var syncable in syncableComponents)
            {
                if (syncable is MonoBehaviour component)
                {
                    DisplaySyncableComponent(component, node.NodeID);
                }
            }
        }

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(5);
    }

    private void DisplaySyncableComponent(MonoBehaviour component, string nodeID)
    {
        string componentKey = component.GetType().Name + component.GetInstanceID();

        // Initialize foldout state if not present
        if (!componentFoldouts[nodeID].ContainsKey(componentKey))
        {
            componentFoldouts[nodeID][componentKey] = false;
        }

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        // Foldout header
        EditorGUILayout.BeginHorizontal();
        GUILayout.Space(10);

        bool foldoutState = componentFoldouts[nodeID][componentKey];
        GUI.color = new Color(0.5f, 0.8f, 1f);

        // Draw foldout triangle and component name
        string arrow = foldoutState ? "▼" : "▶";
        bool newFoldoutState = EditorGUILayout.Foldout(foldoutState, $"{arrow} 📦 {component.GetType().Name}", true);
        componentFoldouts[nodeID][componentKey] = newFoldoutState;

        GUI.color = Color.white;
        EditorGUILayout.EndHorizontal();

        // Show component properties if expanded
        if (componentFoldouts[nodeID][componentKey])
        {
            DisplayComponentProperties(component);
        }

        EditorGUILayout.EndVertical();
    }

    private void DisplayComponentProperties(MonoBehaviour component)
    {
        EditorGUILayout.BeginVertical();
        GUILayout.Space(5);

        // Get all serialized fields and properties
        var serializedObject = new SerializedObject(component);
        var iterator = serializedObject.GetIterator();
        bool hasProperties = false;

        // Move past the script reference
        iterator.NextVisible(true);

        while (iterator.NextVisible(false))
        {
            hasProperties = true;
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(30);

            // Property name
            GUI.color = new Color(0.8f, 0.8f, 0.5f);
            EditorGUILayout.LabelField(iterator.displayName, GUILayout.Width(150));

            // Property value with color based on type
            GUI.color = GetPropertyColor(iterator.propertyType);
            string valueString = GetPropertyValueString(iterator);
            EditorGUILayout.LabelField(valueString, EditorStyles.miniLabel);

            GUI.color = Color.white;
            EditorGUILayout.EndHorizontal();
        }

        // If no serialized properties, show common Transform properties for reference
        if (!hasProperties || component is Transform)
        {
            DisplayTransformProperties(component.transform);
        }

        // Show additional public properties via reflection (non-serialized)
        DisplayPublicProperties(component);

        GUILayout.Space(5);
        EditorGUILayout.EndVertical();
    }

    private void DisplayTransformProperties(Transform transform)
    {
        EditorGUILayout.BeginHorizontal();
        GUILayout.Space(30);
        GUI.color = new Color(0.8f, 0.8f, 0.5f);
        EditorGUILayout.LabelField("Position", GUILayout.Width(150));
        GUI.color = new Color(1f, 0.4f, 1f);
        EditorGUILayout.LabelField($"({transform.position.x:F2}, {transform.position.y:F2}, {transform.position.z:F2})", EditorStyles.miniLabel);
        GUI.color = Color.white;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        GUILayout.Space(30);
        GUI.color = new Color(0.8f, 0.8f, 0.5f);
        EditorGUILayout.LabelField("Rotation", GUILayout.Width(150));
        GUI.color = new Color(1f, 0.4f, 1f);
        EditorGUILayout.LabelField($"({transform.eulerAngles.x:F2}, {transform.eulerAngles.y:F2}, {transform.eulerAngles.z:F2})", EditorStyles.miniLabel);
        GUI.color = Color.white;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        GUILayout.Space(30);
        GUI.color = new Color(0.8f, 0.8f, 0.5f);
        EditorGUILayout.LabelField("Scale", GUILayout.Width(150));
        GUI.color = new Color(1f, 0.4f, 1f);
        EditorGUILayout.LabelField($"({transform.localScale.x:F2}, {transform.localScale.y:F2}, {transform.localScale.z:F2})", EditorStyles.miniLabel);
        GUI.color = Color.white;
        EditorGUILayout.EndHorizontal();
    }

    private void DisplayPublicProperties(MonoBehaviour component)
    {
        var type = component.GetType();
        var properties = type.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

        foreach (var prop in properties)
        {
            // Skip inherited properties from MonoBehaviour/Component
            if (prop.DeclaringType == typeof(MonoBehaviour) ||
                prop.DeclaringType == typeof(Component) ||
                prop.DeclaringType == typeof(UnityEngine.Object))
                continue;

            // Only show properties with public getters
            if (!prop.CanRead || prop.GetMethod == null || !prop.GetMethod.IsPublic)
                continue;

            try
            {
                object value = prop.GetValue(component);
                string valueString = FormatValue(value);

                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(30);
                GUI.color = new Color(0.6f, 0.9f, 0.6f);
                EditorGUILayout.LabelField($"⚡ {prop.Name}", GUILayout.Width(150));
                GUI.color = new Color(0.8f, 1f, 0.8f);
                EditorGUILayout.LabelField(valueString, EditorStyles.miniLabel);
                GUI.color = Color.white;
                EditorGUILayout.EndHorizontal();
            }
            catch
            {
                // Skip properties that throw exceptions when accessed
            }
        }
    }

    private Color GetPropertyColor(SerializedPropertyType propertyType)
    {
        return propertyType switch
        {
            SerializedPropertyType.Integer => new Color(0.5f, 0.8f, 1f),
            SerializedPropertyType.Float => new Color(1f, 0.9f, 0.4f),
            SerializedPropertyType.Boolean => Color.green,
            SerializedPropertyType.String => new Color(1f, 0.6f, 0.2f),
            SerializedPropertyType.Vector2 => new Color(1f, 0.4f, 1f),
            SerializedPropertyType.Vector3 => new Color(1f, 0.4f, 1f),
            SerializedPropertyType.Vector4 => new Color(1f, 0.4f, 1f),
            SerializedPropertyType.Color => new Color(0.8f, 0.5f, 0.8f),
            SerializedPropertyType.ObjectReference => new Color(0.7f, 0.9f, 0.7f),
            _ => Color.white
        };
    }

    private string GetPropertyValueString(SerializedProperty property)
    {
        return property.propertyType switch
        {
            SerializedPropertyType.Integer => property.intValue.ToString(),
            SerializedPropertyType.Float => property.floatValue.ToString("F2"),
            SerializedPropertyType.Boolean => property.boolValue.ToString(),
            SerializedPropertyType.String => property.stringValue,
            SerializedPropertyType.Vector2 => $"({property.vector2Value.x:F2}, {property.vector2Value.y:F2})",
            SerializedPropertyType.Vector3 => $"({property.vector3Value.x:F2}, {property.vector3Value.y:F2}, {property.vector3Value.z:F2})",
            SerializedPropertyType.Vector4 => $"({property.vector4Value.x:F2}, {property.vector4Value.y:F2}, {property.vector4Value.z:F2}, {property.vector4Value.w:F2})",
            SerializedPropertyType.Color => $"RGBA({property.colorValue.r:F2}, {property.colorValue.g:F2}, {property.colorValue.b:F2}, {property.colorValue.a:F2})",
            SerializedPropertyType.ObjectReference => property.objectReferenceValue != null ? property.objectReferenceValue.name : "null",
            SerializedPropertyType.Enum => property.enumNames[property.enumValueIndex],
            _ => property.type
        };
    }

    private string FormatValue(object value)
    {
        if (value == null) return "null";

        return value switch
        {
            float f => f.ToString("F2"),
            double d => d.ToString("F2"),
            Vector2 v2 => $"({v2.x:F2}, {v2.y:F2})",
            Vector3 v3 => $"({v3.x:F2}, {v3.y:F2}, {v3.z:F2})",
            Color c => $"RGBA({c.r:F2}, {c.g:F2}, {c.b:F2}, {c.a:F2})",
            _ => value.ToString()
        };
    }

    private Color GetNodeTypeColor(USMNode.NODETYPE nodeType)
    {
        return nodeType switch
        {
            USMNode.NODETYPE.STATIC => new Color(0.8f, 0.4f, 0.5f), // Red
            USMNode.NODETYPE.ACTIVE => new Color(0.0f, 0.85f, 0.78f), // Cyan
            _ => Color.white
        };
    }

    private bool HasSyncData(NetworkMessage msg)
    {
        return msg.Int1.HasValue || msg.Int2.HasValue || msg.Int3.HasValue || msg.Int4.HasValue || msg.Int5.HasValue ||
               msg.Float1.HasValue || msg.Float2.HasValue || msg.Float3.HasValue || msg.Float4.HasValue || msg.Float5.HasValue ||
               msg.Float6.HasValue || msg.Float7.HasValue || msg.Float8.HasValue || msg.Float9.HasValue || msg.Float10.HasValue ||
               msg.Bool1.HasValue || msg.Bool2.HasValue || msg.Bool3.HasValue || msg.Bool4.HasValue || msg.Bool5.HasValue ||
               !string.IsNullOrEmpty(msg.String1) || !string.IsNullOrEmpty(msg.String2) ||
               !string.IsNullOrEmpty(msg.String3) || !string.IsNullOrEmpty(msg.String4) ||
               !string.IsNullOrEmpty(msg.String5) ||
               msg.Vector2_1.HasValue || msg.Vector2_2.HasValue ||
               msg.Vector2_3.HasValue || msg.Vector2_4.HasValue ||
               msg.Vector2_5.HasValue ||
               msg.Position.HasValue || msg.Velocity.HasValue || msg.Scale.HasValue ||
               msg.AngularVelocity.HasValue || msg.Rotation.HasValue || msg.Speed.HasValue ||
               msg.Magnitude.HasValue || !string.IsNullOrEmpty(msg.Action) || !string.IsNullOrEmpty(msg.ActionValue) ||
               msg.LiveSwap.HasValue || msg.Pause.HasValue || msg.Handshake.HasValue ||
               msg.StartGame.HasValue || msg.Update.HasValue || !string.IsNullOrEmpty(msg.Query) ||
               !string.IsNullOrEmpty(msg.QueryResponse);
    }

    private void DisplaySyncData(NetworkMessage msg)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        // Display physical properties first (most commonly used)
        if (msg.Position.HasValue && msg.Position.Value != Vector2.zero)
            DisplayDataField("Position", $"({msg.Position.Value.x:F2}, {msg.Position.Value.y:F2})", new Color(0.4f, 0.9f, 1f));
        if (msg.Velocity.HasValue && msg.Velocity.Value != Vector2.zero)
            DisplayDataField("Velocity", $"({msg.Velocity.Value.x:F2}, {msg.Velocity.Value.y:F2})", new Color(0.5f, 1f, 0.5f));
        if (msg.Scale.HasValue && msg.Scale.Value != Vector2.zero)
            DisplayDataField("Scale", $"({msg.Scale.Value.x:F2}, {msg.Scale.Value.y:F2})", new Color(1f, 0.8f, 0.4f));
        if (msg.AngularVelocity.HasValue && msg.AngularVelocity.Value != 0)
            DisplayDataField("Angular Velocity", msg.AngularVelocity.Value.ToString("F2"), new Color(1f, 0.6f, 0.8f));
        if (msg.Rotation.HasValue && msg.Rotation.Value != 0)
            DisplayDataField("Rotation", msg.Rotation.Value.ToString("F2"), new Color(0.9f, 0.7f, 1f));
        if (msg.Speed.HasValue && msg.Speed.Value != 0)
            DisplayDataField("Speed", msg.Speed.Value.ToString("F2"), new Color(0.7f, 1f, 0.7f));
        if (msg.Magnitude.HasValue && msg.Magnitude.Value != 0)
            DisplayDataField("Magnitude", msg.Magnitude.Value.ToString("F2"), new Color(1f, 1f, 0.5f));

        // Display action properties
        if (!string.IsNullOrEmpty(msg.Action))
            DisplayDataField("Action", msg.Action, new Color(1f, 0.5f, 0.3f));
        if (!string.IsNullOrEmpty(msg.ActionValue))
            DisplayDataField("Action Value", msg.ActionValue, new Color(1f, 0.5f, 0.3f));

        // Display state properties
        if (msg.LiveSwap.HasValue)
            DisplayDataField("Live Swap", msg.LiveSwap.Value.ToString(), new Color(0.5f, 1f, 0.8f));
        if (msg.Pause.HasValue)
            DisplayDataField("Pause", msg.Pause.Value.ToString(), new Color(1f, 0.8f, 0.5f));
        if (msg.Handshake.HasValue)
            DisplayDataField("Handshake", msg.Handshake.Value.ToString(), new Color(0.6f, 0.8f, 1f));
        if (msg.StartGame.HasValue)
            DisplayDataField("Start Game", msg.StartGame.Value.ToString(), new Color(0.5f, 1f, 0.5f));
        if (msg.Update.HasValue)
            DisplayDataField("Update", msg.Update.Value.ToString(), new Color(0.8f, 0.8f, 1f));

        // Display query properties
        if (!string.IsNullOrEmpty(msg.Query))
            DisplayDataField("Query", msg.Query, new Color(1f, 0.7f, 0.5f));
        if (!string.IsNullOrEmpty(msg.QueryResponse))
            DisplayDataField("Query Response", msg.QueryResponse, new Color(1f, 0.7f, 0.5f));

        // Display integers
        if (msg.Int1.HasValue && msg.Int1.Value != 0) DisplayDataField("Int1", msg.Int1.Value.ToString(), new Color(0.5f, 0.8f, 1f));
        if (msg.Int2.HasValue && msg.Int2.Value != 0) DisplayDataField("Int2", msg.Int2.Value.ToString(), new Color(0.5f, 0.8f, 1f));
        if (msg.Int3.HasValue && msg.Int3.Value != 0) DisplayDataField("Int3", msg.Int3.Value.ToString(), new Color(0.5f, 0.8f, 1f));
        if (msg.Int4.HasValue && msg.Int4.Value != 0) DisplayDataField("Int4", msg.Int4.Value.ToString(), new Color(0.5f, 0.8f, 1f));
        if (msg.Int5.HasValue && msg.Int5.Value != 0) DisplayDataField("Int5", msg.Int5.Value.ToString(), new Color(0.5f, 0.8f, 1f));

        // Display floats
        if (msg.Float1.HasValue && msg.Float1.Value != 0) DisplayDataField("Float1", msg.Float1.Value.ToString("F2"), new Color(1f, 0.9f, 0.4f));
        if (msg.Float2.HasValue && msg.Float2.Value != 0) DisplayDataField("Float2", msg.Float2.Value.ToString("F2"), new Color(1f, 0.9f, 0.4f));
        if (msg.Float3.HasValue && msg.Float3.Value != 0) DisplayDataField("Float3", msg.Float3.Value.ToString("F2"), new Color(1f, 0.9f, 0.4f));
        if (msg.Float4.HasValue && msg.Float4.Value != 0) DisplayDataField("Float4", msg.Float4.Value.ToString("F2"), new Color(1f, 0.9f, 0.4f));
        if (msg.Float5.HasValue && msg.Float5.Value != 0) DisplayDataField("Float5", msg.Float5.Value.ToString("F2"), new Color(1f, 0.9f, 0.4f));
        if (msg.Float6.HasValue && msg.Float6.Value != 0) DisplayDataField("Float6", msg.Float6.Value.ToString("F2"), new Color(1f, 0.9f, 0.4f));
        if (msg.Float7.HasValue && msg.Float7.Value != 0) DisplayDataField("Float7", msg.Float7.Value.ToString("F2"), new Color(1f, 0.9f, 0.4f));
        if (msg.Float8.HasValue && msg.Float8.Value != 0) DisplayDataField("Float8", msg.Float8.Value.ToString("F2"), new Color(1f, 0.9f, 0.4f));
        if (msg.Float9.HasValue && msg.Float9.Value != 0) DisplayDataField("Float9", msg.Float9.Value.ToString("F2"), new Color(1f, 0.9f, 0.4f));
        if (msg.Float10.HasValue && msg.Float10.Value != 0) DisplayDataField("Float10", msg.Float10.Value.ToString("F2"), new Color(1f, 0.9f, 0.4f));

        // Display booleans
        if (msg.Bool1.HasValue) DisplayDataField("Bool1", msg.Bool1.Value.ToString(), Color.green);
        if (msg.Bool2.HasValue) DisplayDataField("Bool2", msg.Bool2.Value.ToString(), Color.green);
        if (msg.Bool3.HasValue) DisplayDataField("Bool3", msg.Bool3.Value.ToString(), Color.green);
        if (msg.Bool4.HasValue) DisplayDataField("Bool4", msg.Bool4.Value.ToString(), Color.green);
        if (msg.Bool5.HasValue) DisplayDataField("Bool5", msg.Bool5.Value.ToString(), Color.green);

        // Display strings
        if (!string.IsNullOrEmpty(msg.String1)) DisplayDataField("String1", msg.String1, new Color(1f, 0.6f, 0.2f));
        if (!string.IsNullOrEmpty(msg.String2)) DisplayDataField("String2", msg.String2, new Color(1f, 0.6f, 0.2f));
        if (!string.IsNullOrEmpty(msg.String3)) DisplayDataField("String3", msg.String3, new Color(1f, 0.6f, 0.2f));
        if (!string.IsNullOrEmpty(msg.String4)) DisplayDataField("String4", msg.String4, new Color(1f, 0.6f, 0.2f));
        if (!string.IsNullOrEmpty(msg.String5)) DisplayDataField("String5", msg.String5, new Color(1f, 0.6f, 0.2f));

        // Display Vector2s
        if (msg.Vector2_1.HasValue && msg.Vector2_1.Value != Vector2.zero)
            DisplayDataField("Vector2_1", $"({msg.Vector2_1.Value.x:F2}, {msg.Vector2_1.Value.y:F2})", new Color(1f, 0.4f, 1f));
        if (msg.Vector2_2.HasValue && msg.Vector2_2.Value != Vector2.zero)
            DisplayDataField("Vector2_2", $"({msg.Vector2_2.Value.x:F2}, {msg.Vector2_2.Value.y:F2})", new Color(1f, 0.4f, 1f));
        if (msg.Vector2_3.HasValue && msg.Vector2_3.Value != Vector2.zero)
            DisplayDataField("Vector2_3", $"({msg.Vector2_3.Value.x:F2}, {msg.Vector2_3.Value.y:F2})", new Color(1f, 0.4f, 1f));
        if (msg.Vector2_4.HasValue && msg.Vector2_4.Value != Vector2.zero)
            DisplayDataField("Vector2_4", $"({msg.Vector2_4.Value.x:F2}, {msg.Vector2_4.Value.y:F2})", new Color(1f, 0.4f, 1f));
        if (msg.Vector2_5.HasValue && msg.Vector2_5.Value != Vector2.zero)
            DisplayDataField("Vector2_5", $"({msg.Vector2_5.Value.x:F2}, {msg.Vector2_5.Value.y:F2})", new Color(1f, 0.4f, 1f));

        EditorGUILayout.EndVertical();
    }

    private void DisplayDataField(string label, string value, Color color)
    {
        EditorGUILayout.BeginHorizontal();
        GUILayout.Space(10);
        EditorGUILayout.LabelField($"🔢 {label}:", labelStyle, GUILayout.Width(100));
        GUI.color = color;
        EditorGUILayout.LabelField(value, valueStyle);
        GUI.color = Color.white;
        EditorGUILayout.EndHorizontal();
    }
}
