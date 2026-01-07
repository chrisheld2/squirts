using System;
using UnityEngine;

public class USMNode : MonoBehaviour
{
    #region Enums
    public enum NODETYPE
    {
        STATIC,
        ACTIVE
    }
    #endregion

    #region Serialized Fields
    [SerializeField]
    private string nodeID;
    [SerializeField]
    private string clientGUID;
    [SerializeField]
    private NODETYPE nodeType = NODETYPE.STATIC;
    [SerializeField]
    private bool liveNode;
    [SerializeField]
    private string gameObjectName;
    [SerializeField]
    private string syncEventName;
    [SerializeField]
    private bool scanned;

    [Tooltip("Distance threshold for position changes (units). Set to 0 to disable position syncing.")]
    [SerializeField]
    private float positionThreshold = 0.1f;

    [Tooltip("Angle threshold for rotation changes (degrees). Set to 0 to disable rotation syncing.")]
    [SerializeField]
    private float rotationThreshold = 5f;

    [Tooltip("Velocity threshold for Rigidbody2D linear velocity (units/s). Set to 0 to disable velocity syncing.")]
    [SerializeField]
    private float velocityThreshold = 0.1f;

    [Tooltip("Angular velocity threshold for Rigidbody2D (deg/s). Set to 0 to disable angular velocity syncing.")]
    [SerializeField]
    private float angularVelocityThreshold = 10f;

    [Tooltip("Enable smooth interpolation for remote objects. Recommended for smoother visuals.")]
    [SerializeField]
    private bool enableInterpolation = true;

    [Tooltip("Interpolation speed multiplier. Higher = faster catch-up. Typical range: 5-20.")]
    [SerializeField]
    private float interpolationSpeed = 10f;

    [Tooltip("Distance threshold for teleporting instead of interpolating (units). Prevents slow catch-up on large gaps.")]
    [SerializeField]
    private float teleportThreshold = 5f;

    [Tooltip("Enable live ownership transfer when a live player touches this object.")]
    [SerializeField]
    private bool swapLive = false;

    [Tooltip("Radius of the trigger collider for live swap detection (units).")]
    [SerializeField]
    private float swapTriggerRadius = 1f;

    [Tooltip("Offset of the trigger collider center from the object's pivot.")]
    [SerializeField]
    private Vector2 swapTriggerOffset = Vector2.zero;

    [Tooltip("Minimum time interval between live swaps (seconds). Prevents rapid ownership ping-pong.")]
    [SerializeField]
    private float swapCooldown = 1.5f;
    [Tooltip("Force syncing in world space even if the object has a parent.")]
    [SerializeField]
    private bool alwaysUseWorldSpace = true;

    [Tooltip("Enable live/remote sprite coloring. When enabled, live nodes will be tinted green and remote nodes red.")]
    [SerializeField]
    private bool enableSpriteColoring = true;
    #endregion

    #region Private Fields
    private UnifiedSyncMatrix unifiedSyncMatrix;
    private NetworkMessage cachedSyncData = new NetworkMessage();

    private bool firstSync = true;

    // Auto-sync system
    private DateTime lastNetworkSync;

    // Auto-tracked values for automatic sync
    private Vector2 lastSyncedPosition;
    private float lastSyncedRotation;
    private Vector2 lastSyncedVelocity;
    private float lastSyncedAngularVelocity;

    // Rigidbody component caching
    private Rigidbody2D rb2D;
    private bool hasRigidbody;

    // Interpolation targets for remote objects
    private Vector2 targetPosition;
    private float targetRotation;
    private Vector2 targetVelocity;
    private float targetAngularVelocity;
    private bool hasReceivedNetworkUpdate;

    // Sprite coloring for live/remote distinction
    private SpriteRenderer[] spriteRenderers;
    private Color[] originalColors;

    // Live swap system
    private CircleCollider2D swapTriggerCollider;
    private DateTime lastSwapTime;
    #endregion

    #region Events
    public event Action<string> OnInit;
    #endregion

    #region Properties
    public string NodeID => nodeID;
    public string ClientGUID => clientGUID;
    public NODETYPE NodeType => nodeType;
    public string GameObjectName => gameObjectName;
    public string SyncEventName => syncEventName;
    public bool LiveNode => liveNode;
    private bool UseLocalSpace => !alwaysUseWorldSpace && transform.parent != null;
    public bool Scanned => scanned;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        gameObjectName = gameObject.name.Replace("(Clone)", "").Trim();
        unifiedSyncMatrix = FindFirstObjectByType<UnifiedSyncMatrix>();


        // Cache Rigidbody2D component if it exists
        rb2D = GetComponent<Rigidbody2D>();
        hasRigidbody = rb2D != null;

        // Cache sprite renderers and their original colors
        CacheSpriteRenderers();

        // Initialize live swap system if enabled
        if (swapLive)
        {
            InitializeSwapTrigger();
        }

        // Initialize cached sync data with name (set once, never changes)
        cachedSyncData.Name = gameObjectName;

        // Initialize physical properties in cached sync data
        InitializePhysicalProperties();
    }
    private void Start()
    {
        if (unifiedSyncMatrix.GameMode != UnifiedSyncMatrix.GAMEMODE.SINGLEPLAYER)
        {


            // Initial sync removed - each component (Player, Lever, etc.) handles its own initial sync
            // This prevents duplicate messages on startup

            lastNetworkSync = DateTime.Now;

            // Initialize last synced values and interpolation targets
            lastSyncedPosition = GetPosition();
            lastSyncedRotation = GetRotation();
            targetPosition = lastSyncedPosition;
            targetRotation = lastSyncedRotation;

            if (hasRigidbody && rb2D != null)
            {
                lastSyncedVelocity = rb2D.linearVelocity;
                lastSyncedAngularVelocity = rb2D.angularVelocity;
                targetVelocity = lastSyncedVelocity;
                targetAngularVelocity = lastSyncedAngularVelocity;
            }

            // Initialize last swap time
            lastSwapTime = DateTime.Now;
        }

        UpdateSpriteColors();
    }

    private void FixedUpdate()
    {
        if (nodeType == NODETYPE.STATIC) return;

        // Auto-sync system: uses custom delegates from IUSMNetworkSync interface if present,
        // otherwise automatically syncs transform and Rigidbody2D
        TryAutoSync();

        // Interpolation system: smoothly moves remote objects toward their network target positions
        if (!liveNode && hasReceivedNetworkUpdate && enableInterpolation)
        {
            InterpolateToTarget();
        }
    }

    private void OnDestroy()
    {
        // Immediately unsubscribe from network updates to prevent any further callbacks
        // This must happen FIRST before any other cleanup
        if (unifiedSyncMatrix != null)
        {
            unifiedSyncMatrix.OnNetworkUpdate -= SyncFromNetwork;

            // Don't unregister if the system is shutting down or not ready
            // This prevents sending network messages during application quit
            if (unifiedSyncMatrix.IsClosing || !unifiedSyncMatrix.IsSessionReady)
                return;

            // Unregister this node from the UnifiedSyncMatrix
            if (!string.IsNullOrEmpty(nodeID))
            {
                unifiedSyncMatrix.UnregisterNode(nodeID);
            }
        }
    }

    /// <summary>
    /// Updates the swap trigger collider properties when values change in the editor.
    /// Called automatically by Unity when serialized fields are modified.
    /// </summary>
    private void OnValidate()
    {
        if (swapLive)
        {
            // Find existing trigger collider
            if (swapTriggerCollider == null)
            {
                CircleCollider2D[] colliders = GetComponents<CircleCollider2D>();
                foreach (var col in colliders)
                {
                    if (col.isTrigger)
                    {
                        swapTriggerCollider = col;
                        break;
                    }
                }
            }

            // Create trigger collider if it doesn't exist
            if (swapTriggerCollider == null)
            {
                swapTriggerCollider = gameObject.AddComponent<CircleCollider2D>();
                swapTriggerCollider.isTrigger = true;
            }

            // Update trigger properties
            swapTriggerCollider.radius = swapTriggerRadius;
            swapTriggerCollider.offset = swapTriggerOffset;
        }
        else
        {
            // If swapLive is disabled, remove the trigger collider
            if (swapTriggerCollider != null)
            {
                if (Application.isPlaying)
                    Destroy(swapTriggerCollider);
                else
                    DestroyImmediate(swapTriggerCollider);

                swapTriggerCollider = null;
            }
        }
    }

    /// <summary>
    /// Draws the swap trigger collider as a wireframe circle in the Scene view.
    /// Only visible when the GameObject is selected and swapLive is enabled.
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        if (!swapLive)
            return;

        // Draw the trigger radius as a green wireframe circle
        Gizmos.color = liveNode ? Color.green : Color.red;

        // Calculate world position with offset
        Vector3 worldOffset = transform.TransformPoint(swapTriggerOffset);

        // Draw circle (approximated with line segments)
        int segments = 32;
        float angleStep = 360f / segments;
        Vector3 prevPoint = worldOffset + new Vector3(swapTriggerRadius, 0, 0);

        for (int i = 1; i <= segments; i++)
        {
            float angle = angleStep * i * Mathf.Deg2Rad;
            Vector3 newPoint = worldOffset + new Vector3(
                Mathf.Cos(angle) * swapTriggerRadius,
                Mathf.Sin(angle) * swapTriggerRadius,
                0
            );

            Gizmos.DrawLine(prevPoint, newPoint);
            prevPoint = newPoint;
        }

        // Draw cross at center to show offset
        Gizmos.DrawLine(worldOffset + Vector3.up * 0.2f, worldOffset + Vector3.down * 0.2f);
        Gizmos.DrawLine(worldOffset + Vector3.left * 0.2f, worldOffset + Vector3.right * 0.2f);
    }

    /// <summary>
    /// Called when another collider enters this object's trigger.
    /// Handles live ownership transfer when a live player touches a non-live object.
    /// </summary>
    private void OnTriggerEnter2D(Collider2D other)
    {
        // Only process if swap is enabled and we're in multiplayer
        if (!swapLive || unifiedSyncMatrix == null || unifiedSyncMatrix.GameMode == UnifiedSyncMatrix.GAMEMODE.SINGLEPLAYER)
            return;

        // Only process if this object is NOT live (we want to become live)
        if (liveNode)
            return;

        // Check if the other object is a Player with a live USMNode
        if (!other.CompareTag("Player"))
            return;

        USMNode otherNode = other.GetComponent<USMNode>();
        if (otherNode == null || !otherNode.LiveNode)
            return;

        // Check cooldown to prevent rapid swapping
        double timeSinceLastSwap = (DateTime.Now - lastSwapTime).TotalSeconds;
        if (timeSinceLastSwap < swapCooldown)
            return;

        // Transfer ownership to this node
        TakeOwnership();
    }
    #endregion

    #region Public Methods
    public void Init()
    {
        if (unifiedSyncMatrix.GameMode != UnifiedSyncMatrix.GAMEMODE.SINGLEPLAYER)
        {
            unifiedSyncMatrix.OnNetworkUpdate += SyncFromNetwork;
            unifiedSyncMatrix.RegisterNode(gameObject);
        }

        // Invoke the OnInit event, passing the NodeID to subscribers
        OnInit?.Invoke(nodeID);
    }
    public void SetLive(bool value)
    {
        liveNode = value;
        UpdateSpriteColors();

        // Update Rigidbody2D body type if present
        // In singleplayer mode, always keep rigidbodies as Dynamic
        if (hasRigidbody && rb2D != null)
        {
            // Check if we're in multiplayer mode
            bool isMultiplayer = unifiedSyncMatrix != null && unifiedSyncMatrix.GameMode != UnifiedSyncMatrix.GAMEMODE.SINGLEPLAYER;

            if (liveNode || !isMultiplayer)
            {
                // Live objects or singleplayer objects should be Dynamic (fully simulated physics)
                rb2D.bodyType = RigidbodyType2D.Dynamic;
            }
            else
            {
                // Remote/copy objects in multiplayer should be Kinematic (controlled by network updates)
                rb2D.bodyType = RigidbodyType2D.Kinematic;
            }
        }
    }
    public void SetScanned(bool value)
    {
        scanned = value;
    }
    public void SetAlwaysUseWorldSpace(bool value)
    {
        alwaysUseWorldSpace = value;
    }
    public void SetNodeID(string id)
    {
        nodeID = id;
        cachedSyncData.GUID = id;
    }
    public void SetNodeType(NODETYPE type)
    {
        nodeType = type;
    }
    public void SetClientGUID(string clientGUID)
    {
        this.clientGUID = clientGUID;
        cachedSyncData.ClientGUID = clientGUID;
    }
    /// <summary>
    /// Triggers an event on the UnifiedSyncMatrix event system using this node's syncEventName.
    /// This is used to notify other objects that are listening for this event.
    /// </summary>
    /// <param name="value">Optional value to pass with the event</param>
    public void ActionCall(object value = null)
    {
        // Safety check: if this object is being destroyed, ignore calls
        if (this == null || gameObject == null || unifiedSyncMatrix == null)
            return;

        // Only trigger event if syncEventName is configured
        if (!string.IsNullOrEmpty(syncEventName))
        {
            unifiedSyncMatrix.Call(syncEventName, value);
        }
    }

    /// <summary>
    /// Forces an immediate network sync on the next Update, bypassing the network interval timer.
    /// Useful for critical state changes that need to be synced immediately (e.g., health changes, state transitions).
    /// </summary>
    public void ForceNextSync()
    {
        lastNetworkSync = DateTime.MinValue;
    }

    /// <summary>
    /// Gets a copy of the cached sync data for this node.
    /// UnifiedSyncMatrix can call this to retrieve the node's current sync properties.
    /// Returns a shallow copy to prevent external modifications from affecting the cached data.
    /// </summary>
    /// <returns>A copy of the cached NetworkMessage containing this node's sync data.</returns>
    public NetworkMessage GetCachedSyncData()
    {
        return cachedSyncData.ShallowCopy();
    }

    /// <summary>
    /// Manually transfers ownership to this node (makes it live) and notifies the network.
    /// This can be called by external systems that want to forcefully take ownership.
    /// </summary>
    public void TakeOwnership()
    {
        // Safety check: if this object is being destroyed, ignore calls
        if (this == null || gameObject == null)
            return;

        if (liveNode)
        {
            // Already live, nothing to do
            return;
        }

        // Check cooldown
        double timeSinceLastSwap = (DateTime.Now - lastSwapTime).TotalSeconds;
        if (timeSinceLastSwap < swapCooldown)
        {
            return;
        }

        // Make this node live
        SetLive(true);
        lastSwapTime = DateTime.Now;

        // Send network message to swap the remote copy
        if (unifiedSyncMatrix != null && unifiedSyncMatrix.IsSessionReady && unifiedSyncMatrix.GameMode != UnifiedSyncMatrix.GAMEMODE.SINGLEPLAYER)
        {
            var swapMessage = new NetworkMessage
            {
                GUID = nodeID,
                ClientGUID = unifiedSyncMatrix.ClientGUID,
                LiveSwap = true
            };

            unifiedSyncMatrix.SendNetworkData(ref swapMessage);
        }
    }
    #endregion



    /// <summary>
    /// Updates the cached sync data without sending to the network.
    /// Useful for preparing data that will be sent later or for local caching.
    /// </summary>
    /// <param name="values">The values to cache.</param>
    public void UpdateCachedSyncData(params object[] values)
    {
        UpdateSyncData(values);
    }

    /// <summary>
    /// Syncs variables to the network.
    /// Respects the network interval from UnifiedSyncMatrix to avoid sending too frequently.
    /// </summary>
    /// <param name="eventSenderID">Optional event sender ID to trigger an event alongside the sync. Must be registered in UnifiedSyncMatrix eventsList.</param>
    /// <param name="values">The values to sync to the network.</param>
    public void SyncToNetwork(params object[] values)
    {
        // Safety check: if this object is being destroyed, ignore calls
        if (this == null || gameObject == null || unifiedSyncMatrix == null)
            return;

        // Only sync if this is a live node (not a guest copy)
        if (!liveNode && nodeType == NODETYPE.ACTIVE)
            // if (!liveNode)
            return;

        // Check if enough time has passed since last sync (respect network interval)
        if (unifiedSyncMatrix != null)
        {
            int networkInterval = unifiedSyncMatrix.networkInterval;
            double timeSinceLastSync = (DateTime.Now - lastNetworkSync).TotalMilliseconds;

            // Skip if not enough time has passed
            if (timeSinceLastSync < networkInterval)
            {
                return;
            }
        }

        // Update the cached sync data
        UpdateSyncData(values);

        // Send the cached data to the network
        unifiedSyncMatrix.SendNetworkData(ref cachedSyncData);

        // Update last sync timestamp to enforce network interval
        lastNetworkSync = DateTime.Now;
    }

    public void SyncPhysical(Vector2 position, Vector2 velocity, params float[] additionalValues)
    {
        SyncPhysical(position, velocity, false, additionalValues);

    }
    /// <summary>
    /// Syncs to network using Physical properties (Position, Velocity) along with custom Float values.
    /// This method is specifically for objects that need to use the NetworkMessage Physical properties
    /// instead of the generic Vector2_X slots.
    /// </summary>
    /// <param name="position">The position to sync (uses Physical Position property)</param>
    /// <param name="velocity">The velocity to sync (uses Physical Velocity property)</param>
    /// <param name="additionalValues">Additional values to sync (will use Float1, Float2, etc.)</param>
    public void SyncPhysical(Vector2 position, Vector2 velocity, bool force = false, params float[] additionalValues)
    {
        // Safety check: if this object is being destroyed, ignore calls
        if (this == null || gameObject == null)
            return;

        // Only sync if this is a live node OR a static node (static nodes can be modified by any client)
        // Non-live ACTIVE nodes (remote players, remote projectiles) should not sync
        if (!liveNode && nodeType == NODETYPE.ACTIVE)
            return;

        // Check if enough time has passed since last sync
        // Early exit if UnifiedSyncMatrix is not available
        if (unifiedSyncMatrix == null)
        {
            return;
        }

        if (!force)
        {
            // Check network interval
            int networkInterval = unifiedSyncMatrix.networkInterval;
            double timeSinceLastSync = (DateTime.Now - lastNetworkSync).TotalMilliseconds;

            if (timeSinceLastSync < networkInterval)
            {
                return;
            }
        }

        // Prepare the message
        cachedSyncData.ClearValues();

        // Set required network identifiers
        cachedSyncData.GUID = nodeID;
        cachedSyncData.ClientGUID = unifiedSyncMatrix.ClientGUID;

        // Set Physical properties
        cachedSyncData.Position = position;
        cachedSyncData.Velocity = velocity;

        // Set additional float values
        int floatIndex = 1;
        foreach (float value in additionalValues)
        {
            AssignFloat(ref cachedSyncData, value, ref floatIndex);
        }

        // Send the data
        if (firstSync)

            firstSync = false;
        else
            unifiedSyncMatrix.SendNetworkData(ref cachedSyncData);

        lastNetworkSync = DateTime.Now;
    }

    /// <summary>
    /// Handles incoming network updates for this node.
    /// Called when UnifiedSyncMatrix receives a network message for this node's ID.
    /// Automatically applies position/rotation/velocity for remote objects if no IUSMSyncable handles it.
    /// </summary>
    /// <param name="receivedNodeID">The node ID from the network message</param>
    /// <param name="message">The network message containing sync data</param>
    private void SyncFromNetwork(string receivedNodeID, NetworkMessage message)
    {
        // Safety check: if this object is being destroyed, ignore network updates
        if (this == null || gameObject == null)
            return;

        // Only process messages intended for this node
        if (receivedNodeID != nodeID)
            return;

        // Update cached sync data with received values to prevent stale data
        // This is critical for STATIC nodes that can be modified by any client
        // and ensures custom values (Life, health, etc.) stay in sync
        UpdateCachedFromMessage(ref message);

        // Check if this is a remote copy (not the live/local object)
        bool isRemote = nodeType != NODETYPE.ACTIVE || !liveNode;

        // Apply default sync behavior first for remote objects (position, rotation, rigidbody)
        // This handles the basic transform/physics sync automatically
        if (isRemote && nodeType != NODETYPE.STATIC)
        {
            ApplyDefaultNetworkSync(message);
        }

        // Then notify any IUSMNetworkSync components for custom value handling
        // Components can now focus only on their custom values (health, state, etc.)
        var networkSyncComponents = GetComponents<IUSMNetworkSync>();
        foreach (var syncComponent in networkSyncComponents)
        {
            syncComponent.OnSyncFromNetwork(ref message);
        }
    }



    #region Private Methods

    /// <summary>
    /// Updates the cached sync data with values from an incoming network message.
    /// This prevents stale data by ensuring cachedSyncData reflects the most recent network state.
    /// Critical for STATIC nodes that can be modified by any client.
    /// </summary>
    /// <param name="message">The incoming network message</param>
    private void UpdateCachedFromMessage(ref NetworkMessage message)
    {
        // Update custom sync values from the message
        // We update all the custom value slots (Int, Float, Bool, String, Vector2)
        // but NOT the physical properties (Position, Rotation, Velocity, AngularVelocity)
        // because those are tracked separately by lastSyncedPosition, etc.

        if (message.Int1.HasValue) cachedSyncData.Int1 = message.Int1.Value;
        if (message.Int2.HasValue) cachedSyncData.Int2 = message.Int2.Value;
        if (message.Int3.HasValue) cachedSyncData.Int3 = message.Int3.Value;
        if (message.Int4.HasValue) cachedSyncData.Int4 = message.Int4.Value;
        if (message.Int5.HasValue) cachedSyncData.Int5 = message.Int5.Value;

        if (message.Float1.HasValue) cachedSyncData.Float1 = message.Float1.Value;
        if (message.Float2.HasValue) cachedSyncData.Float2 = message.Float2.Value;
        if (message.Float3.HasValue) cachedSyncData.Float3 = message.Float3.Value;
        if (message.Float4.HasValue) cachedSyncData.Float4 = message.Float4.Value;
        if (message.Float5.HasValue) cachedSyncData.Float5 = message.Float5.Value;
        if (message.Float6.HasValue) cachedSyncData.Float6 = message.Float6.Value;
        if (message.Float7.HasValue) cachedSyncData.Float7 = message.Float7.Value;
        if (message.Float8.HasValue) cachedSyncData.Float8 = message.Float8.Value;
        if (message.Float9.HasValue) cachedSyncData.Float9 = message.Float9.Value;
        if (message.Float10.HasValue) cachedSyncData.Float10 = message.Float10.Value;

        if (message.Bool1.HasValue) cachedSyncData.Bool1 = message.Bool1.Value;
        if (message.Bool2.HasValue) cachedSyncData.Bool2 = message.Bool2.Value;
        if (message.Bool3.HasValue) cachedSyncData.Bool3 = message.Bool3.Value;
        if (message.Bool4.HasValue) cachedSyncData.Bool4 = message.Bool4.Value;
        if (message.Bool5.HasValue) cachedSyncData.Bool5 = message.Bool5.Value;

        if (message.String1 != null) cachedSyncData.String1 = message.String1;
        if (message.String2 != null) cachedSyncData.String2 = message.String2;
        if (message.String3 != null) cachedSyncData.String3 = message.String3;
        if (message.String4 != null) cachedSyncData.String4 = message.String4;
        if (message.String5 != null) cachedSyncData.String5 = message.String5;

        if (message.Vector2_1.HasValue) cachedSyncData.Vector2_1 = message.Vector2_1.Value;
        if (message.Vector2_2.HasValue) cachedSyncData.Vector2_2 = message.Vector2_2.Value;
        if (message.Vector2_3.HasValue) cachedSyncData.Vector2_3 = message.Vector2_3.Value;
        if (message.Vector2_4.HasValue) cachedSyncData.Vector2_4 = message.Vector2_4.Value;
        if (message.Vector2_5.HasValue) cachedSyncData.Vector2_5 = message.Vector2_5.Value;
    }

    /// <summary>
    /// Gets the current position (local or world based on UseLocalSpace).
    /// </summary>
    private Vector2 GetPosition() => UseLocalSpace ? (Vector2)transform.localPosition : (Vector2)transform.position;

    /// <summary>
    /// Sets the position (local or world based on UseLocalSpace).
    /// </summary>
    private void SetPosition(Vector2 position)
    {
        if (UseLocalSpace)
            transform.localPosition = position;
        else
            transform.position = position;
    }

    /// <summary>
    /// Gets the current Z rotation (local or world based on UseLocalSpace).
    /// </summary>
    private float GetRotation() => UseLocalSpace ? transform.localEulerAngles.z : transform.eulerAngles.z;

    /// <summary>
    /// Sets the Z rotation (local or world based on UseLocalSpace).
    /// </summary>
    private void SetRotation(float rotation)
    {
        if (UseLocalSpace)
            transform.localRotation = Quaternion.Euler(0, 0, rotation);
        else
            transform.rotation = Quaternion.Euler(0, 0, rotation);
    }

    /// <summary>
    /// Checks if the object should teleport instead of interpolating to the target position.
    /// Returns true if the distance exceeds the teleport threshold.
    /// </summary>
    private bool ShouldTeleport(Vector2 targetPos)
    {
        return Vector2.Distance(GetPosition(), targetPos) > teleportThreshold;
    }

    /// <summary>
    /// Initializes the swap trigger collider for live ownership transfer.
    /// Creates a CircleCollider2D configured as a trigger.
    /// </summary>
    private void InitializeSwapTrigger()
    {
        // Check if we already have a swap trigger collider
        CircleCollider2D[] colliders = GetComponents<CircleCollider2D>();
        foreach (var col in colliders)
        {
            if (col.isTrigger && col.gameObject == gameObject)
            {
                swapTriggerCollider = col;
                break;
            }
        }

        // Create new trigger collider if not found
        if (swapTriggerCollider == null)
        {
            swapTriggerCollider = gameObject.AddComponent<CircleCollider2D>();
        }

        // Configure the trigger collider
        swapTriggerCollider.isTrigger = true;
        swapTriggerCollider.radius = swapTriggerRadius;
        swapTriggerCollider.offset = swapTriggerOffset;
    }

    /// <summary>
    /// Applies default network sync for position, rotation, and Rigidbody2D properties.
    /// This runs automatically for all remote objects, before custom IUSMNetworkSync handlers.
    /// Components implementing IUSMNetworkSync can focus on custom values only.
    /// </summary>
    private void ApplyDefaultNetworkSync(NetworkMessage message)
    {
        // Safety check: verify object still exists before applying network sync
        if (this == null || gameObject == null)
            return;

        if (enableInterpolation)
        {
            // Set interpolation targets instead of directly applying values
            if (message.Position.HasValue)
            {
                if (ShouldTeleport(message.Position.Value))
                {
                    // Teleport directly for large gaps
                    if (hasRigidbody && rb2D != null && rb2D.bodyType == RigidbodyType2D.Kinematic)
                    {
                        if (UseLocalSpace && transform.parent != null)
                            rb2D.MovePosition(transform.parent.TransformPoint(message.Position.Value));
                        else
                            rb2D.MovePosition(message.Position.Value);
                    }
                    else
                    {
                        SetPosition(message.Position.Value);
                    }
                }

                targetPosition = message.Position.Value;
            }

            if (message.Velocity.HasValue)
            {
                targetVelocity = message.Velocity.Value;
            }

            if (message.Rotation.HasValue)
            {
                targetRotation = message.Rotation.Value;
            }

            if (message.AngularVelocity.HasValue)
            {
                targetAngularVelocity = message.AngularVelocity.Value;
            }

            hasReceivedNetworkUpdate = true;
        }
        else
        {
            // Direct application without interpolation (original behavior)
            if (message.Position.HasValue)
            {
                if (hasRigidbody && rb2D != null && rb2D.bodyType == RigidbodyType2D.Kinematic)
                {
                    if (UseLocalSpace && transform.parent != null)
                        rb2D.MovePosition(transform.parent.TransformPoint(message.Position.Value));
                    else
                        rb2D.MovePosition(message.Position.Value);
                }
                else
                {
                    SetPosition(message.Position.Value);
                }
            }

            if (hasRigidbody && rb2D != null && message.Velocity.HasValue)
            {
                rb2D.linearVelocity = message.Velocity.Value;
            }

            if (message.Rotation.HasValue)
            {
                SetRotation(message.Rotation.Value);
            }

            if (hasRigidbody && rb2D != null && message.AngularVelocity.HasValue)
            {
                rb2D.angularVelocity = message.AngularVelocity.Value;
            }
        }
    }

    /// <summary>
    /// Smoothly interpolates the remote object toward its network target position/rotation/velocity.
    /// Called every FixedUpdate for remote objects when interpolation is enabled.
    /// </summary>
    private void InterpolateToTarget()
    {
        float deltaTime = Time.fixedDeltaTime;
        float lerpAmount = interpolationSpeed * deltaTime;

        // Interpolate position
        Vector2 newPosition = Vector2.Lerp(GetPosition(), targetPosition, lerpAmount);

        if (hasRigidbody && rb2D != null && rb2D.bodyType == RigidbodyType2D.Kinematic)
        {
            if (UseLocalSpace && transform.parent != null)
                rb2D.MovePosition(transform.parent.TransformPoint(newPosition));
            else
                rb2D.MovePosition(newPosition);
        }
        else
        {
            SetPosition(newPosition);
        }

        // Interpolate rotation
        float newRotation = Mathf.LerpAngle(GetRotation(), targetRotation, lerpAmount);
        SetRotation(newRotation);

        // Interpolate Rigidbody2D properties if present
        if (hasRigidbody && rb2D != null)
        {
            // Interpolate linear velocity
            Vector2 newVelocity = Vector2.Lerp(rb2D.linearVelocity, targetVelocity, lerpAmount);
            rb2D.linearVelocity = newVelocity;

            // Interpolate angular velocity
            float newAngularVelocity = Mathf.Lerp(rb2D.angularVelocity, targetAngularVelocity, lerpAmount);
            rb2D.angularVelocity = newAngularVelocity;
        }
    }


    /// <summary>
    /// Automatically handles network sync timing and threshold checks.
    /// Called every frame. Only syncs if this is a live node, enough time has passed,
    /// and values have changed beyond thresholds.
    /// </summary>
    private void TryAutoSync()
    {
        // Only sync if this is a live node (not a guest copy)
        if (!liveNode)
            return;

        // Skip if in single-player mode
        if (unifiedSyncMatrix == null || unifiedSyncMatrix.GameMode == UnifiedSyncMatrix.GAMEMODE.SINGLEPLAYER)
            return;

        // Check if enough time has passed since last sync (respect network interval)
        int networkInterval = unifiedSyncMatrix.networkInterval;
        double timeSinceLastSync = (DateTime.Now - lastNetworkSync).TotalMilliseconds;

        if (timeSinceLastSync < networkInterval)
            return;

        // Check if transform or Rigidbody2D values have changed beyond thresholds
        if (ShouldSyncTransform())
        {
            PerformSyncTransform();
            lastNetworkSync = DateTime.Now;
        }
    }

    /// <summary>
    /// Built-in check for transform and physics changes (position, rotation, velocity, angular velocity).
    /// Automatically detects Rigidbody2D and tracks velocity if present.
    /// </summary>
    /// <returns>True if position, rotation, or physics values changed beyond thresholds</returns>
    private bool ShouldSyncTransform()
    {
        bool changed = false;

        // Check position if threshold is enabled
        if (positionThreshold > 0)
        {
            changed |= Vector2.Distance(GetPosition(), lastSyncedPosition) > positionThreshold;
        }

        // Check rotation if threshold is enabled
        if (rotationThreshold > 0)
        {
            changed |= Mathf.Abs(GetRotation() - lastSyncedRotation) > rotationThreshold;
        }

        // Check rigidbody physics if present and thresholds enabled
        if (hasRigidbody && rb2D != null)
        {
            // Check linear velocity if threshold is enabled
            if (velocityThreshold > 0)
            {
                Vector2 currentVelocity = rb2D.linearVelocity;
                changed |= Vector2.Distance(currentVelocity, lastSyncedVelocity) > velocityThreshold;
            }

            // Check angular velocity if threshold is enabled
            if (angularVelocityThreshold > 0)
            {
                float currentAngularVelocity = rb2D.angularVelocity;
                changed |= Mathf.Abs(currentAngularVelocity - lastSyncedAngularVelocity) > angularVelocityThreshold;
            }
        }

        return changed;
    }

    /// <summary>
    /// Built-in sync for transform and physics values (position, rotation, velocity, angular velocity).
    /// Automatically syncs Rigidbody2D properties if present.
    /// Uses NetworkMessage specific properties instead of generic values.
    /// </summary>
    private void PerformSyncTransform()
    {
        // Capture physical properties to cached sync data
        CapturePhysicalProperties();

        // Send the cached data to the network
        unifiedSyncMatrix.SendNetworkData(ref cachedSyncData);

        // Update last sync timestamp to enforce network interval
        lastNetworkSync = DateTime.Now;
    }

    /// <summary>
    /// Initializes physical properties in cachedSyncData during Awake().
    /// This sets up the initial state without requiring UnifiedSyncMatrix to be fully ready.
    /// </summary>
    private void InitializePhysicalProperties()
    {
        // Initialize basic properties that are safe to set during Awake
        cachedSyncData.Position = GetPosition();
        cachedSyncData.Rotation = GetRotation();

        // Initialize rigidbody properties if available
        if (hasRigidbody && rb2D != null)
        {
            cachedSyncData.Velocity = rb2D.linearVelocity;
            cachedSyncData.AngularVelocity = rb2D.angularVelocity;
        }

        // Note: GUID and ClientGUID are not set here as UnifiedSyncMatrix may not be ready yet
        // These will be set when CapturePhysicalProperties() or UpdateSyncData() is called later
    }

    /// <summary>
    /// Captures current physical properties (position, rotation, velocity, angular velocity)
    /// and assigns them to cachedSyncData without sending to the network.
    /// This is useful for preparing sync data that will be sent later or for manual sync control.
    /// </summary>
    private void CapturePhysicalProperties()
    {
        // Prepare sync data using NetworkMessage properties
        cachedSyncData.ClearValues();

        // Set required network identifiers
        // Note: ClearValues() preserves GUID and ClientGUID, but we need to ensure they're set
        cachedSyncData.GUID = nodeID;
        if (unifiedSyncMatrix != null)
        {
            cachedSyncData.ClientGUID = unifiedSyncMatrix.ClientGUID;
        }

        // Set transform properties
        Vector2 currentPosition = GetPosition();
        float currentRotation = GetRotation();

        cachedSyncData.Position = currentPosition;
        cachedSyncData.Rotation = currentRotation;

        // Set rigidbody properties if available
        if (hasRigidbody && rb2D != null)
        {
            Vector2 currentVelocity = rb2D.linearVelocity;
            float currentAngularVelocity = rb2D.angularVelocity;

            cachedSyncData.Velocity = currentVelocity;
            cachedSyncData.AngularVelocity = currentAngularVelocity;

            // Update last synced rigidbody values
            lastSyncedVelocity = currentVelocity;
            lastSyncedAngularVelocity = currentAngularVelocity;
        }

        // Update last synced transform values
        lastSyncedPosition = currentPosition;
        lastSyncedRotation = currentRotation;
    }

    private void AssignInt(ref NetworkMessage msg, int value, ref int index)
    {
        switch (index)
        {
            case 1: msg.Int1 = value; break;
            case 2: msg.Int2 = value; break;
            case 3: msg.Int3 = value; break;
            case 4: msg.Int4 = value; break;
            case 5: msg.Int5 = value; break;
        }
        index++;
    }

    private void AssignFloat(ref NetworkMessage msg, float value, ref int index)
    {
        switch (index)
        {
            case 1: msg.Float1 = value; break;
            case 2: msg.Float2 = value; break;
            case 3: msg.Float3 = value; break;
            case 4: msg.Float4 = value; break;
            case 5: msg.Float5 = value; break;
            case 6: msg.Float6 = value; break;
            case 7: msg.Float7 = value; break;
            case 8: msg.Float8 = value; break;
            case 9: msg.Float9 = value; break;
            case 10: msg.Float10 = value; break;
        }
        index++;
    }

    private void AssignBool(ref NetworkMessage msg, bool value, ref int index)
    {
        switch (index)
        {
            case 1: msg.Bool1 = value; break;
            case 2: msg.Bool2 = value; break;
            case 3: msg.Bool3 = value; break;
            case 4: msg.Bool4 = value; break;
            case 5: msg.Bool5 = value; break;
        }
        index++;
    }

    private void AssignString(ref NetworkMessage msg, string value, ref int index)
    {
        switch (index)
        {
            case 1: msg.String1 = value; break;
            case 2: msg.String2 = value; break;
            case 3: msg.String3 = value; break;
            case 4: msg.String4 = value; break;
            case 5: msg.String5 = value; break;
        }
        index++;
    }

    private void AssignVector2(ref NetworkMessage msg, Vector2 value, ref int index)
    {
        switch (index)
        {
            case 1: msg.Vector2_1 = value; break;
            case 2: msg.Vector2_2 = value; break;
            case 3: msg.Vector2_3 = value; break;
            case 4: msg.Vector2_4 = value; break;
            case 5: msg.Vector2_5 = value; break;
        }
        index++;
    }

    /// <summary>
    /// Common method to update the cachedSyncData with provided values.
    /// Used by both UpdateCachedSyncData() and SyncToNetwork().
    /// </summary>
    /// <param name="values">The values to assign to the cached sync data.</param>
    private void UpdateSyncData(params object[] values)
    {
        cachedSyncData.ClearValues();

        // Set required network identifiers
        // Note: ClearValues() preserves GUID and ClientGUID, but we need to ensure they're set
        cachedSyncData.GUID = nodeID;
        cachedSyncData.ClientGUID = unifiedSyncMatrix.ClientGUID;

        int intIndex = 1;
        int floatIndex = 1;
        int boolIndex = 1;
        int stringIndex = 1;
        int vector2Index = 1;

        // Process each value and assign to cached NetworkMessage
        foreach (var value in values)
        {
            switch (value)
            {
                case int intValue:
                    AssignInt(ref cachedSyncData, intValue, ref intIndex);
                    break;
                case float floatValue:
                    AssignFloat(ref cachedSyncData, floatValue, ref floatIndex);
                    break;
                case double doubleValue:
                    AssignFloat(ref cachedSyncData, (float)doubleValue, ref floatIndex);
                    break;
                case bool boolValue:
                    AssignBool(ref cachedSyncData, boolValue, ref boolIndex);
                    break;
                case string stringValue:
                    AssignString(ref cachedSyncData, stringValue, ref stringIndex);
                    break;
                case Vector2 vector2Value:
                    AssignVector2(ref cachedSyncData, vector2Value, ref vector2Index);
                    break;
                case Vector3 vector3Value:
                    // Convert Vector3 to Vector2, ignoring Z coordinate for tight network messages
                    AssignVector2(ref cachedSyncData, new Vector2(vector3Value.x, vector3Value.y), ref vector2Index);
                    break;
                default:
                    // For unsupported types, convert to string
                    AssignString(ref cachedSyncData, value?.ToString(), ref stringIndex);
                    break;
            }
        }
    }

    /// <summary>
    /// Caches all SpriteRenderer components on this GameObject and its children recursively,
    /// excluding FloatingText objects.
    /// Stores original colors for restoration if needed.
    /// </summary>
    private void CacheSpriteRenderers()
    {
        // Get all sprite renderers on this GameObject and ALL children recursively (including inactive)
        var allRenderers = GetComponentsInChildren<SpriteRenderer>(true);

        // Filter out FloatingText objects
        var filteredList = new System.Collections.Generic.List<SpriteRenderer>();
        foreach (var sr in allRenderers)
        {
            if (sr != null && !IsFloatingTextChild(sr.transform))
            {
                filteredList.Add(sr);
            }
        }

        spriteRenderers = filteredList.ToArray();
        originalColors = new Color[spriteRenderers.Length];

        // Store original colors
        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            if (spriteRenderers[i] != null)
            {
                originalColors[i] = spriteRenderers[i].color;
            }
        }
    }

    /// <summary>
    /// Checks if a transform is part of a FloatingText hierarchy.
    /// </summary>
    private bool IsFloatingTextChild(Transform t)
    {
        Transform current = t;
        while (current != null)
        {
            if (current.name.Contains("FloatingText") || current.name.Contains("floatingText"))
            {
                return true;
            }
            current = current.parent;
        }
        return false;
    }

    /// <summary>
    /// Updates sprite colors based on whether this is a Live node (green) or Remote/Copy node (red).
    /// Only applies colors in multiplayer mode when enableSpriteColoring is true.
    /// </summary>
    private void UpdateSpriteColors()
    {
        // Skip if sprite coloring is disabled
        if (!enableSpriteColoring)
        {
            RestoreOriginalColors();
            return;
        }

        // Only apply coloring in multiplayer mode
        if (unifiedSyncMatrix == null || unifiedSyncMatrix.GameMode == UnifiedSyncMatrix.GAMEMODE.SINGLEPLAYER)
        {
            RestoreOriginalColors();
            return;
        }

        if (spriteRenderers == null || spriteRenderers.Length == 0)
            return;

        Color targetColor = liveNode ? Color.green : Color.red;

        foreach (var sr in spriteRenderers)
        {
            if (sr != null)
            {
                sr.color = targetColor;
            }
        }
    }

    /// <summary>
    /// Restores original sprite colors (used when switching to single-player mode).
    /// </summary>
    private void RestoreOriginalColors()
    {
        if (spriteRenderers == null || originalColors == null)
            return;

        for (int i = 0; i < spriteRenderers.Length && i < originalColors.Length; i++)
        {
            if (spriteRenderers[i] != null)
            {
                spriteRenderers[i].color = originalColors[i];
            }
        }
    }
    #endregion
}
