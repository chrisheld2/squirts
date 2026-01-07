using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Controls a single Light2D component (specifically Spot Light) in the GameObject's hierarchy.
/// Provides public methods to toggle light on/off that can be called from event bindings.
/// Supports network synchronization through IUSMSyncable interface.
/// </summary>
public class LightController : MonoBehaviour, IUSMNetworkSync
{
    #region Fields

    private Light2D lightSource;
    private USMNode usmNode = null;
    private UnifiedSyncMatrix usm = null;

    #endregion

    #region Inspector Configuration

    [Header("Light References")]
    [Tooltip("Optional: Manually assign the light. If empty, will auto-find Light2D component in children.")]
    [SerializeField] private Light2D lightSourceOverride;

    [Header("Settings")]
    [SerializeField] private bool isOn = false;

    [Header("Event System")]
    [Tooltip("The receiver ID this light listens for. Leave empty if not using event system.")]
    [SerializeField] private string receiverID = "";

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        usmNode = GetComponent<USMNode>();
        usm = FindFirstObjectByType<UnifiedSyncMatrix>();

        FindLightInHierarchy();
    }
    private void Start()
    {
        // Initialize the light to match the inspector value
        UpdateLightState();

        usmNode.UpdateCachedSyncData(isOn);

        receiverID = usmNode.SyncEventName;

        // Subscribe to USM event system if receiverID is set
        if (usm != null && !string.IsNullOrEmpty(receiverID))
        {
            usm.OnEventTriggered += HandleEventTriggered;
        }
    }
    private void OnDestroy()
    {
        // Unsubscribe from USM event system
        if (usm != null && !string.IsNullOrEmpty(receiverID))
        {
            usm.OnEventTriggered -= HandleEventTriggered;
        }
    }
    #endregion

    #region Private Methods

    /// <summary>
    /// Finds the Light2D component in this GameObject or its children.
    /// </summary>
    private void FindLightInHierarchy()
    {
        if (lightSourceOverride != null)
        {
            lightSource = lightSourceOverride;
        }
        else
        {
            lightSource = GetComponentInChildren<Light2D>(true);
        }

        if (lightSource == null)
        {
            Debug.LogWarning($"LightController on {gameObject.name}: No Light2D component found.", this);
        }
    }

    /// <summary>
    /// Updates the light state when the isEnabled value changes.
    /// Called automatically by USMSyncVar when value changes locally or from network.
    /// </summary>
    private void UpdateLightState()
    {
        if (lightSource != null)
        {
            lightSource.enabled = isOn;
        }
    }

    /// <summary>
    /// Handles events from the UnifiedSyncMatrix event system.
    /// </summary>
    /// <param name="receivedID">The receiver ID from the event.</param>
    /// <param name="action">The action value (e.g., "TurnOn", "TurnOff", "Toggle").</param>
    private void HandleEventTriggered(string receivedID, string action)
    {
        // Only respond if this is our receiverID
        if (receivedID != receiverID) return;

        // Parse the action
        switch (action.ToLower())
        {
            case "true":
                TurnOn();
                break;

            case "false":
                TurnOff();
                break;

            case "toggle":
                Toggle();
                break;

            default:
                Toggle();
                break;
        }

        usmNode.SyncToNetwork(isOn);

    }

    #endregion

    #region IUSMNetworkSync Implementation

    public void OnSyncFromNetwork(ref NetworkMessage message)
    {
        if (message.Bool1.HasValue)
        {
            isOn = message.Bool1.Value;
            UpdateLightState();  // Apply the synced state to the light
        }
    }


    #endregion

    #region Public Methods

    /// <summary>
    /// Turns the light on. Can be called from Unity Events.
    /// </summary>
    public void TurnOn()
    {
        isOn = true;
        // usmNode.SyncVarToNetwork(isOn);
        UpdateLightState();
    }

    /// <summary>
    /// Turns the light off. Can be called from Unity Events.
    /// </summary>
    public void TurnOff()
    {
        isOn = false;
        // usmNode.SyncVarToNetwork(isOn);
        UpdateLightState();
    }

    /// <summary>
    /// Toggles the light between on and off states. Can be called from Unity Events.
    /// </summary>
    public void Toggle()
    {
        isOn = !isOn;
        // usmNode.SyncVarToNetwork(isOn);
        UpdateLightState();
    }


    #endregion


}
