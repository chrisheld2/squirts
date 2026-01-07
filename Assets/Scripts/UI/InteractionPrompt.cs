using System;
using UnityEngine;

/// <summary>
/// Manages player interaction detection within a trigger zone.
/// Only one player can interact at a time - the first player to enter the zone.
/// Detects when the player holds down the interaction key for the required duration.
///
/// Broadcasts three types of events to the LEVEL object:
/// - "InteractionStart": When the player begins holding the interaction key
/// - "InteractionComplete": When the player successfully holds the key for the full duration
/// - "InteractionCancel": When the player releases the key before completing the interaction
///
/// Each broadcast includes the interactionID to identify which prompt triggered the event.
/// Note: Visual prompt display should be handled by child GameObjects, not by this script.
/// </summary>
public class InteractionPrompt : MonoBehaviour
{
    #region Serialized Fields

    [Header("━━━━━━━━ Display Settings ━━━━━━━━")]
    [SerializeField, Tooltip("The GameObject containing the sprite/visual element to show when interaction is available")]
    private GameObject displaySprite;

    [Header("━━━━━━━━ Trigger Zone Settings ━━━━━━━━")]
    [SerializeField, Tooltip("Radius of the interaction trigger zone")]
    [Range(0.1f, 10f)]
    private float interactionRadius = 1.5f;

    [Space(5)]
    [SerializeField, Tooltip("Horizontal offset for the trigger collider")]
    [Range(-5f, 5f)]
    private float colliderOffsetX = 0f;

    [SerializeField, Tooltip("Vertical offset for the trigger collider")]
    [Range(-5f, 5f)]
    private float colliderOffsetY = 0f;

    [Header("━━━━━━━━ Layer Settings ━━━━━━━━")]
    [SerializeField, Tooltip("Layer mask for player detection (leave as 'Everything' if unsure)")]
    private LayerMask playerLayer = ~0;

    [Header("━━━━━━━━ Interaction Key Settings ━━━━━━━━")]
    [SerializeField, Tooltip("The key that must be held down to interact")]
    private KeyCode interactionKey = KeyCode.E;

    [Space(5)]
    [SerializeField, Tooltip("How long the key must be held down (in seconds). Set to 0 for instant interaction")]
    [Range(0f, 5f)]
    private float holdDuration = 0.75f;

    [Header("━━━━━━━━ Progress Circle Settings ━━━━━━━━")]
    [SerializeField, Tooltip("Thickness of the progress circle line")]
    [Range(0.01f, 0.2f)]
    private float circleLineThickness = 0.05f;

    [Space(5)]
    [SerializeField, Tooltip("Color of the progress circle (supports transparency)")]
    private Color circleColor = new Color(1f, 1f, 1f, 0.8f);

    #endregion

    #region Private Fields

    private CircleCollider2D triggerCollider;
    private GameObject currentInteractingPlayer;

    // Key hold tracking
    private bool isHoldingKey = false;
    private float keyHoldTime = 0f;
    private bool interactionCompleted = false;

    // Progress circle visualization
    private LineRenderer progressCircle;
    private const int circleSegments = 60;
    private float circleRadius = 0.5f;

    #endregion

    #region Unity Lifecycle Methods

    private void Awake()
    {
        playerLayer = LayerMask.GetMask("Player");

        InitializeTriggerCollider();
        InitializeProgressCircle();

        // Ensure the display sprite starts hidden
        SetDisplaySpriteActive(false);
    }

    private void Update()
    {
        // Only process input if a player is in the interaction zone and is live
        if (currentInteractingPlayer == null)
        {
            ResetInteraction();
            return;
        }

        HandleInteractionInput();
    }

    private void OnValidate()
    {
        // Update the collider properties in the editor when values change
        if (triggerCollider != null)
        {
            triggerCollider.radius = interactionRadius;
            triggerCollider.offset = new Vector2(colliderOffsetX, colliderOffsetY);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Only allow interaction if no player is currently interacting
        if (currentInteractingPlayer != null)
            return;

        // Validate and set the interacting player
        if (TrySetInteractingPlayer(other))
        {
            SetDisplaySpriteActive(true);
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        // Only reset interaction if the leaving object is the current interacting player
        if (currentInteractingPlayer != null && other.gameObject == currentInteractingPlayer)
        {
            currentInteractingPlayer = null;
            ResetInteraction();
            SetDisplaySpriteActive(false);
        }
    }

    private void OnDrawGizmosSelected()
    {
        // Calculate collider center position with offset
        Vector3 colliderCenter = transform.position + new Vector3(colliderOffsetX, colliderOffsetY, 0);

        // Draw the interaction radius in the editor
        Gizmos.color = new Color(0f, 1f, 0f, 0.3f);
        Gizmos.DrawWireSphere(colliderCenter, interactionRadius);

        // Draw a cross at the collider center
        Gizmos.color = Color.green;
        Gizmos.DrawLine(colliderCenter + Vector3.left * 0.2f, colliderCenter + Vector3.right * 0.2f);
        Gizmos.DrawLine(colliderCenter + Vector3.down * 0.2f, colliderCenter + Vector3.up * 0.2f);
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Handles the interaction key input and tracking
    /// </summary>
    private void HandleInteractionInput()
    {
        // Check if the interaction key is being held down
        if (Input.GetKey(interactionKey))
        {
            // First frame of key press
            if (!isHoldingKey)
            {
                isHoldingKey = true;
                keyHoldTime = 0f;
                interactionCompleted = false;
            }

            // Accumulate hold time
            keyHoldTime += Time.deltaTime;

            // Check if we've held long enough
            if (!interactionCompleted && keyHoldTime >= holdDuration)
            {
                interactionCompleted = true;

                // Call parent's InteractionCompleted method if it implements the interface
                if (transform.parent != null)
                {
                    var parentInteraction = transform.parent.GetComponent<IInteractionComplete>();
                    parentInteraction?.InteractionCompleted(currentInteractingPlayer);
                }

                // Hide the progress circle after completion
                UpdateProgressCircle(0f);
            }
            else if (!interactionCompleted)
            {
                // Only update progress circle if not completed yet
                float progress = GetInteractionProgress();
                UpdateProgressCircle(progress);
            }
        }
        // Key was released
        else if (isHoldingKey)
        {
            // If released before completing
            if (!interactionCompleted)
            {
                DL.Log($"InteractionPrompt: Interaction cancelled at {keyHoldTime:F2}s / {holdDuration:F2}s", "yellow", true, true, 16);
            }

            ResetInteraction();
        }
    }

    /// <summary>
    /// Initializes the 2D circle trigger collider
    /// </summary>
    private void InitializeTriggerCollider()
    {
        // Check if a CircleCollider2D already exists
        triggerCollider = GetComponent<CircleCollider2D>();

        if (triggerCollider == null)
        {
            // Create a new CircleCollider2D if one doesn't exist
            triggerCollider = gameObject.AddComponent<CircleCollider2D>();
        }

        // Configure the collider
        triggerCollider.isTrigger = true;
        triggerCollider.radius = interactionRadius;
        triggerCollider.offset = new Vector2(colliderOffsetX, colliderOffsetY);
    }

    /// <summary>
    /// Checks if a GameObject is in the specified LayerMask
    /// </summary>
    private bool IsInLayerMask(GameObject obj, LayerMask layerMask)
    {
        return ((layerMask.value & (1 << obj.layer)) > 0);
    }

    /// <summary>
    /// Validates and attempts to set a collider's GameObject as the current interacting player
    /// </summary>
    /// <returns>True if the player was successfully set, false otherwise</returns>
    private bool TrySetInteractingPlayer(Collider2D collider)
    {
        // Check if the colliding object is on the player layer
        if (!IsInLayerMask(collider.gameObject, playerLayer))
            return false;

        // Check if it's a player (has Player component)
        Player playerComponent = collider.GetComponent<Player>();
        if (playerComponent == null)
            return false;

        // Only allow live players to interact (not remote player copies)
        USMNode usmNode = collider.GetComponent<USMNode>();
        if (usmNode != null)
        {
            // Remote players have NodeType != ACTIVE or LiveNode == false
            bool isRemotePlayer = usmNode.NodeType != USMNode.NODETYPE.ACTIVE || !usmNode.LiveNode;
            if (isRemotePlayer)
                return false;
        }

        // Set this player as the current interacting player
        currentInteractingPlayer = collider.gameObject;

        return true;
    }

    /// <summary>
    /// Shows or hides the display sprite
    /// </summary>
    private void SetDisplaySpriteActive(bool active)
    {
        if (displaySprite != null)
        {
            displaySprite.SetActive(active);
        }
    }

    /// <summary>
    /// Resets the interaction state (key hold tracking) and hides the display sprite
    /// </summary>
    private void ResetInteraction()
    {
        isHoldingKey = false;
        keyHoldTime = 0f;
        interactionCompleted = false;
        UpdateProgressCircle(0f);
    }

    /// <summary>
    /// Initializes the LineRenderer for the progress circle
    /// </summary>
    private void InitializeProgressCircle()
    {
        if (displaySprite == null)
            return;

        // Create a child GameObject for the progress circle
        GameObject circleObject = new GameObject("ProgressCircle");
        circleObject.transform.SetParent(displaySprite.transform);
        circleObject.transform.localPosition = Vector3.zero;
        circleObject.transform.localRotation = Quaternion.identity;
        circleObject.transform.localScale = Vector3.one;

        // Add and configure LineRenderer
        progressCircle = circleObject.AddComponent<LineRenderer>();
        progressCircle.useWorldSpace = false;
        progressCircle.loop = false;
        progressCircle.positionCount = 0;

        // Set line width using configurable thickness
        progressCircle.startWidth = circleLineThickness;
        progressCircle.endWidth = circleLineThickness;

        // Set material and color using configurable values
        progressCircle.material = new Material(Shader.Find("Sprites/Default"));
        progressCircle.startColor = circleColor;
        progressCircle.endColor = circleColor;

        // Set sorting layer to render on top
        progressCircle.sortingLayerName = "UI";
        progressCircle.sortingOrder = 100;

        // Calculate circle radius based on sprite bounds if available
        SpriteRenderer spriteRenderer = displaySprite.GetComponent<SpriteRenderer>();
        if (spriteRenderer != null && spriteRenderer.sprite != null)
        {
            // Add a small margin around the sprite
            circleRadius = Mathf.Max(spriteRenderer.bounds.extents.x, spriteRenderer.bounds.extents.y) + 0.1f;
        }
    }

    /// <summary>
    /// Updates the progress circle based on interaction progress (0 to 1)
    /// </summary>
    private void UpdateProgressCircle(float progress)
    {
        if (progressCircle == null || displaySprite == null || !displaySprite.activeSelf)
            return;

        // Clamp progress between 0 and 1
        progress = Mathf.Clamp01(progress);

        if (progress <= 0f)
        {
            // Hide the circle when no progress
            progressCircle.positionCount = 0;
            return;
        }

        // At 100%, enable loop to close the circle perfectly
        if (progress >= 1f)
        {
            progressCircle.loop = true;
            progressCircle.positionCount = circleSegments;

            // Draw complete circle
            float angleStep = 360f / circleSegments;
            for (int i = 0; i < circleSegments; i++)
            {
                float angle = 90f - (i * angleStep);
                float rad = angle * Mathf.Deg2Rad;

                float x = Mathf.Cos(rad) * circleRadius;
                float y = Mathf.Sin(rad) * circleRadius;

                progressCircle.SetPosition(i, new Vector3(x, y, 0));
            }
        }
        else
        {
            // Partial circle - disable loop
            progressCircle.loop = false;

            // Calculate how many segments to draw based on progress
            int segmentsToDraw = Mathf.CeilToInt(circleSegments * progress);
            progressCircle.positionCount = segmentsToDraw + 1;

            // Draw the circle arc starting from the top and going clockwise
            float angleStep = (360f * progress) / circleSegments;

            for (int i = 0; i <= segmentsToDraw; i++)
            {
                // Start from top (90 degrees) and go clockwise (subtract angle)
                float angle = 90f - (i * angleStep);
                float rad = angle * Mathf.Deg2Rad;

                float x = Mathf.Cos(rad) * circleRadius;
                float y = Mathf.Sin(rad) * circleRadius;

                progressCircle.SetPosition(i, new Vector3(x, y, 0));
            }
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Returns the currently interacting player, or null if no player is interacting
    /// </summary>
    public GameObject GetCurrentInteractingPlayer()
    {
        return currentInteractingPlayer;
    }

    /// <summary>
    /// Manually update the interaction radius
    /// </summary>
    public void SetInteractionRadius(float newRadius)
    {
        interactionRadius = Mathf.Clamp(newRadius, 0.1f, 10f);
        if (triggerCollider != null)
        {
            triggerCollider.radius = interactionRadius;
        }
    }

    /// <summary>
    /// Returns the current key hold progress (0 to 1)
    /// </summary>
    public float GetInteractionProgress()
    {
        if (holdDuration <= 0f) return isHoldingKey ? 1f : 0f;
        return Mathf.Clamp01(keyHoldTime / holdDuration);
    }

    /// <summary>
    /// Returns true if the interaction key is currently being held
    /// </summary>
    public bool IsHoldingInteractionKey()
    {
        return isHoldingKey;
    }

    /// <summary>
    /// Returns true if the interaction has been completed
    /// </summary>
    public bool IsInteractionComplete()
    {
        return interactionCompleted;
    }

    /// <summary>
    /// Toggles the interaction prompt on or off. When disabled, the prompt will not respond to player input.
    /// </summary>
    /// <param name="enabled">True to enable interaction, false to disable it</param>
    public void SetInteractionEnabled(bool enabled)
    {
        if (!enabled)
        {
            // Clear current interaction state
            currentInteractingPlayer = null;
            ResetInteraction();
            SetDisplaySpriteActive(false);
        }

        // Enable or disable the trigger collider
        if (triggerCollider != null)
        {
            triggerCollider.enabled = enabled;
        }
    }

    #endregion
}

/// <summary>
/// Interface for objects that need to respond to interaction completion events
/// </summary>
public interface IInteractionComplete
{
    void InteractionCompleted(GameObject interactingPlayer);
}
