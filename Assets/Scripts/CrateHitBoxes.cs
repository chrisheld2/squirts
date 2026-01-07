using UnityEngine;
using TMPro;
using System;

public class CrateHitBoxes : MonoBehaviour, IUSMNetworkSync
{
    #region Fields

    private FloatingText floatingText;
    private FloatingText floatingTextNodeID;
    private UnifiedSyncMatrix usm;
    private USMNode usmNode = null;
    private bool correctGameMode = false;
    private SpriteRenderer spriteRenderer;
    private int _initialLife;

    #endregion

    #region Inspector Configuration

    [SerializeField, Range(0, 99)]
    private int _life = 3;

    [SerializeField]
    private bool showNodeID = true;

    #endregion

    #region Unity Lifecycle

    [Obsolete]
    void Awake()
    {
        usm = FindObjectOfType<UnifiedSyncMatrix>();
        usmNode = GetComponent<USMNode>();
        spriteRenderer = GetComponent<SpriteRenderer>();

        usmNode.OnInit += (string nodeID) =>
        {
            UpdateFloatingTextNodeID();

        };

        Transform floatingTextTransform = transform.Find("FloatingText");
        floatingText = floatingTextTransform.GetComponent<FloatingText>();

        Transform floatingTextNodeIDTransform = transform.Find("FloatingText NodeID");
        floatingTextNodeID = floatingTextNodeIDTransform.GetComponent<FloatingText>();

        if (floatingTextNodeID != null && floatingTextNodeIDTransform != null)
        {
            floatingTextNodeIDTransform.gameObject.SetActive(showNodeID);
        }


    }
    void Start()
    {
        // Store initial life value for color scaling
        _initialLife = _life;

        // Sync both position (Position) and life (Float1 converted to int) for initial network sync
        // This ensures remote clients get the correct spawn position
        // Use SyncPhysical to properly set Position field, with zero velocity and life as Float1
        Vector2 position = transform.parent != null ? (Vector2)transform.localPosition : (Vector2)transform.position;
        usmNode.SyncPhysical(position, Vector2.zero, _life);
        UpdateFloatingText();
        UpdateFloatingTextNodeID();
        UpdateSpriteColor();

        correctGameMode = usmNode.LiveNode || usm.GameMode == UnifiedSyncMatrix.GAMEMODE.SINGLEPLAYER;


    }
    void Update()
    {
        UpdateFloatingText();
    }
    #endregion

    #region Private Methods

    private void UpdateFloatingText()
    {
        floatingText.Text = _life.ToString();
    }

    private void UpdateFloatingTextNodeID()
    {
        if (floatingTextNodeID != null && usmNode != null)
        {
            floatingTextNodeID.Text = usmNode.NodeID.ToString();
        }
    }

    private void UpdateSpriteColor()
    {
        if (spriteRenderer == null) return;

        // Interpolate color from red (low life) to white (full life)
        // Scale based on initial life value
        float normalizedLife = _initialLife > 0 ? Mathf.Clamp01((float)_life / _initialLife) : 0f;
        spriteRenderer.color = Color.Lerp(Color.red, Color.white, normalizedLife);
    }

    private void UpdateLife(int newLife)
    {
        _life = newLife;
        UpdateFloatingText();
        UpdateSpriteColor();

        if (_life <= 0)
        {
            Destroy(gameObject);
        }
    }

    #endregion

    #region IUSMNetworkSync Implementation

    public void OnSyncFromNetwork(ref NetworkMessage message)
    {
        // Note: Position is handled automatically by USMNode.ApplyDefaultNetworkSync()
        // We don't need to manually apply message.Position here

        // Sync life (Float1, converted from int)
        if (message.Float1.HasValue)
        {
            UpdateLife((int)message.Float1.Value);
        }
    }

    #endregion

    #region Collision Handling
    private void OnParticleCollision(GameObject collision)
    {
        // if (!correctGameMode) return;
        if (!usmNode.LiveNode && usm.GameMode != UnifiedSyncMatrix.GAMEMODE.SINGLEPLAYER) return;

        if (collision.CompareTag("Damage"))
        {
            HandleDamage();
        }
    }

    void OnCollisionEnter2D(Collision2D other)
    {
        if (Time.frameCount % 260 != 0) return;

        if (!correctGameMode) return;
        // Handles both regular collisions and 2D particle collisions
        // For particles: Enable Collision module with Mode: 2D, Type: World, Send Collision Messages: True
        // NOTE: Requires Rigidbody2D component on this GameObject for particle collisions to work!
        Debug.Log($"[CrateHitBoxes] OnCollisionEnter2D called! Collider: {other.collider.name}, Tag: {other.collider.tag}, Layer: {LayerMask.LayerToName(other.collider.gameObject.layer)}");

        if (other.collider.CompareTag("Damage"))
        {
            Debug.Log("[CrateHitBoxes] Damage tag detected! Handling damage...");
            HandleDamage();
        }
    }

    private void HandleDamage()
    {
        int newLife = _life - 1;
        // Sync position and life together using SyncPhysical
        Vector2 position = transform.parent != null ? (Vector2)transform.localPosition : (Vector2)transform.position;
        usmNode.SyncPhysical(position, Vector2.zero, true, newLife);
        UpdateLife(newLife);
    }

    #endregion

}