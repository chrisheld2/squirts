using System;
using System.Data.Common;
using UnityEngine;

public class ShipConsole : MonoBehaviour, IInteractionComplete, IUSMNetworkSync
{
    #region Fields

    private USMNode usmNode = null;
    private InteractionPrompt interactionPrompt;

    #endregion

    #region Inspector Configuration

    #endregion

    #region Unity Lifecycle

    void Awake()
    {
        usmNode = GetComponent<USMNode>();
        interactionPrompt = GetComponentInChildren<InteractionPrompt>();
    }

    void Start()
    {
        // usmNode.SyncVarToNetwork(isOn);
    }

    #endregion

    #region IUSMNetworkSync Implementation


    public void OnSyncFromNetwork(ref NetworkMessage message)
    {


    }

    #endregion

    #region IInteractionComplete Implementation

    public void InteractionCompleted(GameObject interactingPlayer)
    {
        usmNode.ActionCall();
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Toggles the InteractionPrompt's enabled state
    /// </summary>
    /// <param name="enabled">True to enable interaction, false to disable it</param>
    public void ToggleInteractionPrompt(bool enabled)
    {
        interactionPrompt.SetInteractionEnabled(enabled);
    }


    #endregion

}
