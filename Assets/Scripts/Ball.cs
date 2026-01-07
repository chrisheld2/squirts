using UnityEngine;

/// <summary>
/// Ball physics object with audio feedback and automatic network synchronization.
/// Uses USMNode's built-in Rigidbody2D syncing - no manual sync code needed!
/// USMNode automatically handles position, velocity, rotation, and angular velocity sync.
/// </summary>
public class Ball : MonoBehaviour, IUSMNetworkSync
{
    #region Serialized Fields

    [Header("Audio")]
    [SerializeField] private AudioClip hitClip;

    #endregion

    #region Component References

    private AudioSource audioSource;
    private USMNode usmNode;
    private FloatingText floatingText;

    #endregion

    #region Unity Lifecycle

    void Awake()
    {
        // Get components
        audioSource = GetComponent<AudioSource>();
        usmNode = GetComponent<USMNode>();

        // Setup audio
        if (audioSource != null && hitClip != null)
        {
            audioSource.clip = hitClip;
        }

        // Get FloatingText component for displaying NodeID
        var floatingTextObj = transform.Find("FloatingText");
        if (floatingTextObj != null && floatingTextObj.TryGetComponent(out floatingText))
        {
            floatingText.Visible = true;
        }
    }

    void Start()
    {
        DL.Log("Ball started", "green");

        // UnifiedSyncMatrix automatically calls usmNode.Init() during NodeScan()
        // No manual initialization needed!

        // USMNode automatically handles:
        // - Rigidbody2D body type (Dynamic for live, Kinematic for remote)
        // - Position, velocity, rotation, and angular velocity sync
        // - Thresholds can be configured in USMNode Inspector (defaults: pos=0.1, rot=5, vel=0.1, angVel=10)
    }

    void OnDestroy()
    {
        DL.Log("Ball destroyed", "red");
    }

    void Update()
    {
        // Update debug information display
        if (floatingText != null && usmNode != null)
        {
            floatingText.Text = usmNode.NodeID;
        }
    }

    #endregion

    #region Collision System

    /// <summary>
    /// Play hit sound on collision with volume based on impact velocity
    /// </summary>
    void OnCollisionEnter2D(Collision2D collision)
    {
        // Only the live ball should play audio (not remote copies)
        bool isLiveBall = usmNode == null || (usmNode.NodeType == USMNode.NODETYPE.ACTIVE && usmNode.LiveNode);

        if (!isLiveBall || audioSource == null) return;

        float velocity = collision.relativeVelocity.magnitude;
        float volume = Mathf.Clamp01(velocity / 10f); // Normalize velocity to 0-1 range
        audioSource.volume = volume;

        audioSource.Play();
    }

    #endregion

    #region IUSMNetworkSync Implementation

    /// <summary>
    /// Called when network data is received for this ball.
    /// Ball doesn't have custom values to sync - USMNode automatically handles all physics sync.
    /// This method is required by IUSMNetworkSync interface but doesn't need to do anything.
    /// </summary>
    public void OnSyncFromNetwork(ref NetworkMessage message)
    {
        // No custom values to sync - USMNode handles all Rigidbody2D properties automatically
        // Position, velocity, rotation, and angular velocity are synced by USMNode
    }

    #endregion
}
