using System;
using UnityEngine;

public class Flare : MonoBehaviour, IUSMNetworkSync
{
    #region Fields

    public static event System.Action flareFinished;

    private USMNode usmNode;
    private UnityEngine.Rendering.Universal.Light2D light2D;
    private Rigidbody2D rb;
    private Collider2D collider2D;
    private AudioSource audioSource;
    private FloatingText floatingText;

    // Network synchronized variables
    private float life = 0;

    // Cached calculations for performance
    private float intensityMultiplier; // Pre-calculated: startIntensity / startingLife
    private float lastDisplayedLife = -1f; // For floating text optimization

    #endregion

    #region Inspector Configuration

    [SerializeField]
    private float startIntensity = 4.0f;

    public float startingLife = 5;

    [Tooltip("How long the flare takes to fade from full brightness to zero (in seconds)")]
    public float fadeDuration = 25;

    #endregion

    #region Unity Lifecycle

    void Awake()
    {
        usmNode = GetComponent<USMNode>();
        rb = GetComponent<Rigidbody2D>();
        collider2D = GetComponent<Collider2D>();
        light2D = GetComponentInChildren<UnityEngine.Rendering.Universal.Light2D>();
        audioSource = GetComponent<AudioSource>();

        Transform floatingTextTransform = transform.Find("FloatingText");
        if (floatingTextTransform != null)
        {
            floatingText = floatingTextTransform.GetComponent<FloatingText>();
        }

        startIntensity = light2D.intensity;

        // Pre-calculate intensity multiplier for performance
        if (startingLife > 0)
        {
            intensityMultiplier = startIntensity / startingLife;
        }

        usmNode.SetAlwaysUseWorldSpace(true);
    }

    void Start()
    {
        // Initialize state on both live and remote copies

        // Only live nodes play audio and send initial sync
        if (usmNode != null && usmNode.LiveNode)
        {
            rb.bodyType = RigidbodyType2D.Dynamic;
            life = startingLife;
            collider2D.enabled = true;

            PlayFlareAudio();

            usmNode.SyncPhysical(transform.position, rb.linearVelocity, life);
        }
    }

    void Update()
    {
        // Only live nodes update life timer
        if (!usmNode.LiveNode) return;

        life -= Time.deltaTime / fadeDuration;

        if (life <= 0)
        {
            life = 0;

            flareFinished?.Invoke();
            // Only sync on destruction - USMNode handles automatic syncing during normal updates
            SyncFlareState();
            Destroy(gameObject);

            return;
        }

        // Update light intensity based on remaining life
        UpdateLightIntensity();

        // Update floating text
        UpdateFloatingText();
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        // Only live nodes handle collision logic
        if (!usmNode.LiveNode) return;

        int layer = collision.gameObject.layer;
        int solidLayer = LayerMask.NameToLayer("Solid");
        int moveableLayer = LayerMask.NameToLayer("MoveableObjects");

        // Skip collision with other flares (use layer check instead of name comparison)
        if (layer == gameObject.layer)
            return;

        if (collision.gameObject.tag == "Block" || layer == solidLayer || layer == moveableLayer)
        {
            MakeSticky(ref collision);
            // Force immediate sync when state changes
            if (usmNode != null)
            {
                usmNode.ForceNextSync();
            }
        }
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Updates the light intensity based on remaining life.
    /// Uses pre-calculated multiplier for performance.
    /// </summary>
    private void UpdateLightIntensity()
    {
        light2D.intensity = life * intensityMultiplier;
    }

    /// <summary>
    /// Syncs the flare state to the network.
    /// Sends: position (Physical Position), velocity (Physical Velocity), and life (Float1)
    /// </summary>
    private void SyncFlareState()
    {
        // Only sync if this is a live node (not a guest copy)
        if (!usmNode.LiveNode)
            return;

        // Sync position, velocity, and life using Physical properties
        Vector2 position = transform.position;
        Vector2 velocity = rb.linearVelocity;

        if (life > 0)
            usmNode.SyncPhysical(position, velocity, life);
        else
            usmNode.SyncPhysical(position, velocity, true, 0);
    }

    private void UpdateFloatingText()
    {
        if (floatingText != null)
        {
            // Only update if the displayed value actually changed (rounded to 2 decimals)
            float roundedLife = (float)Math.Round(life, 2);
            if (roundedLife != lastDisplayedLife)
            {
                lastDisplayedLife = roundedLife;
                floatingText.Text = roundedLife.ToString();
            }
        }
    }

    private void MakeSticky(ref Collision2D collision)
    {
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0;
        collider2D.enabled = false;

        transform.SetParent(collision.transform);
    }

    private void PlayFlareAudio()
    {
        if (audioSource != null)
        {
            audioSource.Play();
        }
    }

    #endregion

    #region IUSMNetworkSync Implementation

    /// <summary>
    /// Receives network updates from other clients.
    /// This is called on remote copies only.
    /// Position and velocity are handled automatically by USMNode.
    /// Custom values: Float1 = life
    /// </summary>
    public void OnSyncFromNetwork(ref NetworkMessage message)
    {
        // Only remote copies process incoming sync data
        if (usmNode.LiveNode) return;

        // Receive life value from Float1
        if (message.Float1.HasValue)
        {
            life = message.Float1.Value;

            // Update light intensity using shared method
            UpdateLightIntensity();

            // Update floating text display
            UpdateFloatingText();

            // If life has reached 0, destroy the remote copy
            if (life <= 0)
            {
                flareFinished?.Invoke();
                Destroy(gameObject);
            }
        }
    }

    #endregion
}

