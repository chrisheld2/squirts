using System;
using System.Data.Common;
using UnityEngine;

public class ShipLever : MonoBehaviour, IInteractionComplete
{
    #region Fields

    private SpriteRenderer spriteRenderer;

    #endregion

    [Header("━━━━━━━━ Broadcast Settings ━━━━━━━━")]
    [SerializeField, Tooltip("Unique identifier for this interaction prompt (used in broadcast messages)")]
    private string broadcastID = "";

    #region Inspector Configuration

    [Header("Switch Configuration")]
    public Sprite spriteOn;
    public Sprite spriteOff;
    public bool isOn = false;


    #endregion

    #region Unity Lifecycle

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();

        spriteRenderer.sprite = isOn ? spriteOn : spriteOff;
    }

    void Update()
    {

    }

    #endregion

    #region Player Interaction

    public void Broadcast(InteractivePromptModel interactivePromptModel)
    {
        if (interactivePromptModel.BroadcastID == broadcastID)
        {
            bool state = bool.Parse(interactivePromptModel.Value);
            isOn = state;
            spriteRenderer.sprite = isOn ? spriteOn : spriteOff;
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Method to set lever state by ID
    /// </summary>
    public void SetState(bool state)
    {

        isOn = state;
        spriteRenderer.sprite = isOn ? spriteOn : spriteOff;

        Folders.PLAYERS.BroadcastMessage("Broadcast", new InteractivePromptModel
        {
            BroadcastID = broadcastID,
            Value = state.ToString()
        }, SendMessageOptions.DontRequireReceiver);

    }
    public bool GetState()
    {
        return isOn;
    }

    #endregion

    public void InteractionCompleted(GameObject interactingPlayer)
    {
        isOn = !isOn;

        SetState(isOn);

    }

}
