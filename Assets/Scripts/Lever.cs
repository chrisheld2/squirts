using System;
using System.Data.Common;
using UnityEngine;

public class Lever : MonoBehaviour, IInteractionComplete, IUSMNetworkSync
{
    #region Fields

    private SpriteRenderer spriteRenderer;
    private USMNode usmNode = null;
    private FloatingText floatingText = null;
    #endregion

    #region Inspector Configuration

    [Header("Switch Configuration")]
    public Sprite spriteOn;
    public Sprite spriteOff;

    private bool isOn = false;

    #endregion

    #region Unity Lifecycle

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        usmNode = GetComponent<USMNode>();

        Transform floatingTextTransform = transform.Find("FloatingText");
        floatingText = floatingTextTransform.GetComponent<FloatingText>();

    }
    void Start()
    {
        usmNode.UpdateCachedSyncData(isOn);
        UpdateLight();
        UpdateFloatingText();
    }



    #endregion

    #region Private Methods

    private void UpdateLight()
    {
        spriteRenderer.sprite = isOn ? spriteOn : spriteOff;
    }

    private void UpdateFloatingText()
    {
        if (floatingText != null)
        {
            floatingText.Text = isOn ? "ON" : "OFF";
            floatingText.BackgroundColor = isOn ? Color.green : Color.red;
            floatingText.Color = isOn ? Color.black : Color.white;
        }
    }

    #endregion

    #region IUSMNetworkSync Implementation

    public void OnSyncFromNetwork(ref NetworkMessage message)
    {
        if (message.Bool1.HasValue)
        {
            isOn = message.Bool1.Value;
            UpdateLight();
            UpdateFloatingText();
        }
    }


    #endregion

    #region IInteractionComplete Implementation

    public void InteractionCompleted(GameObject interactingPlayer)
    {
        isOn = !isOn;
        usmNode.SyncToNetwork(isOn);
        usmNode.ActionCall(isOn);
        UpdateLight();
        UpdateFloatingText();
    }

    #endregion
}
