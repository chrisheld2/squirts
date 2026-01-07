using System;
using UnityEngine;

public class Bomb : MonoBehaviour, IUSMNetworkSync
{
    #region Fields

    public static event System.Action bombExploded;

    private USMNode usmNode;
    private Rigidbody2D rb;
    private AudioSource audioSource;
    private FloatingText floatingText;

    // Sync tracking
    private bool hasExploded = false;

    #endregion

    #region Inspector Configuration

    [SerializeField] private float life = 5;
    [SerializeField] private float fadeMultiplier = 25;
    [SerializeField] private GameObject explosionPrefab;

    #endregion

    #region Unity Lifecycle

    void Awake()
    {
        usmNode = GetComponent<USMNode>();
        rb = GetComponent<Rigidbody2D>();
        audioSource = GetComponent<AudioSource>();

        Transform floatingTextTransform = transform.Find("FloatingText");
        if (floatingTextTransform != null)
        {
            floatingText = floatingTextTransform.GetComponent<FloatingText>();
        }

        usmNode.SetAlwaysUseWorldSpace(true);
    }
    void OnDestroy()
    {
        // Only spawn explosion during gameplay, not when scene is unloading
        if (gameObject.scene.isLoaded && Application.isPlaying)
        {
            SpawnExplosion();
        }
    }

    void Start()
    {
        // Initialize state on both live and remote copies

        // Only live nodes play audio and send initial sync
        if (usmNode != null && usmNode.LiveNode)
        {
            PlayBombAudio();

            usmNode.SyncPhysical(transform.position, rb.linearVelocity, life);
        }
    }

    void Update()
    {
        // Only live nodes update life timer
        if (!usmNode.LiveNode) return;

        life -= Time.deltaTime / fadeMultiplier;

        if (life <= 0 && !hasExploded)
        {
            life = 0;
            hasExploded = true;

            bombExploded?.Invoke();
            SyncBombState();

            // Delay destruction slightly to ensure network sync completes
            Destroy(gameObject);

            return;
        }

        // Update floating text
        UpdateFloatingText();

        // Sync life value with change detection (position/velocity handled automatically by USMNode)
        SyncBombState();
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Syncs the bomb state to the network.
    /// Sends: position (Physical Position), velocity (Physical Velocity), and life (Float1)
    /// Uses change detection to reduce network traffic.
    /// </summary>
    private void SyncBombState()
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

    private void SpawnExplosion()
    {
        // Use assigned prefab if available, otherwise load from Resources
        // This ensures both editor-placed and network-instantiated bombs work
        GameObject prefab = explosionPrefab;
        if (prefab == null)
        {
            prefab = Resources.Load<GameObject>("ExplosionWithSparks");
        }

        if (prefab == null)
        {
            Debug.LogWarning("Explosion prefab not found!");
            return;
        }

        GameObject explosion = Instantiate(prefab, transform.position, Quaternion.identity);

        // Auto-destroy after particle system finishes
        ParticleSystem ps = explosion.GetComponent<ParticleSystem>();
        if (ps != null)
        {
            Destroy(explosion, ps.main.duration + ps.main.startLifetime.constantMax);
        }
        else
        {
            // Fallback: destroy after 5 seconds if no particle system found
            Destroy(explosion, 5f);
        }
    }

    private void UpdateFloatingText()
    {
        if (floatingText != null)
        {
            floatingText.Text = Math.Round(life, 2).ToString();
        }
    }


    private void PlayBombAudio()
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

            // Update floating text display
            UpdateFloatingText();

            // If life has reached 0, spawn explosion and destroy the remote copy
            if (life <= 0 && !hasExploded)
            {
                hasExploded = true;
                bombExploded?.Invoke();
                SpawnExplosion();
                Destroy(gameObject);
            }
        }
    }

    #endregion
}

