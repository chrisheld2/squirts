using UnityEngine;

/// <summary>
/// Vault door with animated open/close behavior - uses USM for network sync
/// </summary>
[RequireComponent(typeof(USMNode))]
public class Vault : MonoBehaviour, IUSMNetworkSync
{
    #region Serialized Fields
    [Header("Audio")]
    [SerializeField] private AudioClip hitClip;

    [Header("Editor Animation Preview")]
    [SerializeField, Range(0f, 1f)] private float editorAnimationPosition = 0f;
    [SerializeField] private float editorAnimationDirection = 0f;

    #endregion

    #region Private Fields

    private AudioSource audioSource;
    private Animator animator;
    private USMNode usmNode;
    private UnifiedSyncMatrix usm;

    // Animation state for network sync
    private float currentAnimationTime = 0f;
    private float currentAnimationSpeed = 0f;

    // Event system
    private string receiverID = "";

    // Cache update tracking for efficiency
    private float lastCachedAnimationTime = -1f;
    private float lastCachedAnimationSpeed = 0f;
    private const float CACHE_UPDATE_THRESHOLD = 0.01f; // Only update cache if animation time changes by 1%

    #endregion

    #region Animation Hash Constants

    private static readonly int VaultAnimHash = Animator.StringToHash("Vault");
    private static readonly int SpeedParam = Animator.StringToHash("Speed");

    #endregion

    #region Unity Lifecycle

    void Awake()
    {
        audioSource = GetComponentInChildren<AudioSource>();
        animator = GetComponentInChildren<Animator>();
        usmNode = GetComponent<USMNode>();
        usm = FindFirstObjectByType<UnifiedSyncMatrix>();
    }

    void Start()
    {
        // Initialize animation state from editor values
        currentAnimationTime = editorAnimationPosition;
        currentAnimationSpeed = editorAnimationDirection;

        SetAnimationState(currentAnimationTime, currentAnimationSpeed);

        // Send initial state to network
        usmNode.UpdateCachedSyncData(currentAnimationTime, currentAnimationSpeed);

        // Get receiver ID from USMNode's sync event name
        receiverID = usmNode.SyncEventName;

        // Subscribe to USM event system if receiverID is set
        if (usm != null && !string.IsNullOrEmpty(receiverID))
        {
            usm.OnEventTriggered += HandleEventTriggered;
        }
    }

    void Update()
    {

        if (Time.frameCount % 60 != 0) return;

        // usmNode.SyncToNetwork(currentAnimationTime, currentAnimationSpeed);

        usmNode.UpdateCachedSyncData(currentAnimationTime, currentAnimationSpeed);
    }

    void OnDestroy()
    {
        // Unsubscribe from USM event system
        if (usm != null && !string.IsNullOrEmpty(receiverID))
        {
            usm.OnEventTriggered -= HandleEventTriggered;
        }
    }

    // Use OnEnable because it comes after Awake() and before Start().
    // But mainly because of pooling activities.
    void OnEnable()
    {
        SetAnimationState(editorAnimationPosition, editorAnimationDirection);
    }

    void FixedUpdate()
    {
        // Only update cache during gameplay in multiplayer when this is the live node
        // if (!Application.isPlaying || usm == null)
        //     return;

        // bool isMultiplayer = usm.GameMode != UnifiedSyncMatrix.GAMEMODE.SINGLEPLAYER;
        // if (!isMultiplayer || !usmNode.LiveNode)
        //     return;

        // Only update if animation is actually playing (speed != 0)
        if (Mathf.Abs(currentAnimationSpeed) < 0.01f)
            return;

        // Get current animation state
        float animTime = animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
        float animSpeed = animator.GetFloat(SpeedParam);

        // Only update cached data if animation has changed significantly (efficiency optimization)
        if (Mathf.Abs(animTime - lastCachedAnimationTime) > CACHE_UPDATE_THRESHOLD ||
            Mathf.Abs(animSpeed - lastCachedAnimationSpeed) > 0.01f)
        {
            currentAnimationTime = animTime;
            currentAnimationSpeed = animSpeed;

            // Update USMNode's cached sync data without sending to network yet
            // This keeps the cache current so when SyncToNetwork() is called, it has fresh data
            usmNode.UpdateCachedSyncData(currentAnimationTime, currentAnimationSpeed);

            lastCachedAnimationTime = animTime;
            lastCachedAnimationSpeed = animSpeed;
        }
    }

    void OnValidate()
    {
        if (!Application.isPlaying)
        {
            // Cache the animator if not already cached (since Awake() doesn't run in edit mode)
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }

            SetAnimationState(editorAnimationPosition, editorAnimationDirection);
        }
    }

    #endregion

    #region Event Handling

    /// <summary>
    /// Handles events from the UnifiedSyncMatrix event system.
    /// Called when a USMNode.ActionCall() is triggered with this vault's receiverID.
    /// </summary>
    /// <param name="receivedID">The receiver ID from the event.</param>
    /// <param name="action">The action value (typically "true" or "false" for open/close).</param>
    private void HandleEventTriggered(string receivedID, string action)
    {
        // Only respond if this is our receiverID
        if (receivedID != receiverID)
            return;

        // Parse the action
        bool state;
        switch (action?.ToLower())
        {
            case "true":
            case "open":
            case "1":
                state = true;
                break;

            case "false":
            case "close":
            case "0":
                state = false;
                break;

            case "toggle":
                // Toggle current state based on animation direction
                state = currentAnimationSpeed <= 0f;
                break;

            default:
                // Try parsing as boolean
                if (!bool.TryParse(action, out state))
                {
                    state = true; // Default to open
                }
                break;
        }

        SetState(state);
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Plays a sound effect on the audio source
    /// </summary>
    private void PlaySound(AudioClip clip)
    {
        if (audioSource == null || clip == null)
            return;

        audioSource.clip = clip;
        audioSource.Play();
    }

    /// <summary>
    /// Centralized method to set the animation state. All animation changes should go through here.
    /// </summary>
    private void SetAnimationState(float normalizedPosition, float direction)
    {
        if (animator == null)
            return;

        normalizedPosition = Mathf.Clamp01(normalizedPosition);

        animator.enabled = true;
        animator.SetFloat(SpeedParam, direction);
        animator.Play(VaultAnimHash, 0, normalizedPosition);
        animator.Update(0f);

        // Update cached state
        currentAnimationTime = normalizedPosition;
        currentAnimationSpeed = direction;

        // Only sync editor params during gameplay, not during initialization
        if (Application.isPlaying)
        {
            editorAnimationPosition = animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
            editorAnimationDirection = animator.GetFloat(SpeedParam);
        }
    }

    /// <summary>
    /// Sets the vault door state (open/close) and syncs to network
    /// In multiplayer: only the live/authoritative node should initiate state changes
    /// In single-player: always allows state changes
    /// </summary>
    private void SetState(bool state)
    {

        PlaySound(hitClip);

        // Get current animation position
        float currentTime = animator.GetCurrentAnimatorStateInfo(0).normalizedTime;

        // Set animation direction and continue from current position
        float newSpeed = state ? 1f : -1f;
        SetAnimationState(currentTime, newSpeed);

        // Sync to network after state change (like LightController does in HandleEventTriggered)
        usmNode.SyncToNetwork(currentAnimationTime, currentAnimationSpeed);
    }

    #endregion

    #region IUSMNetworkSync Implementation

    /// <summary>
    /// Receive animation state updates from the network
    /// Called automatically by USMNode when network data arrives
    /// </summary>
    public void OnSyncFromNetwork(ref NetworkMessage message)
    {
        // Sync animation state (Float1 = normalizedTime, Float2 = speed)
        if (message.Float1.HasValue && message.Float2.HasValue)
        {
            float normalizedTime = message.Float1.Value;
            float speed = message.Float2.Value;

            // Update cached state
            currentAnimationTime = normalizedTime;
            currentAnimationSpeed = speed;

            // Apply the received state to the animator
            SetAnimationState(normalizedTime, speed);

        }
    }

    #endregion
}
