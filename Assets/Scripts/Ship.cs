using UnityEngine;

/// <summary>
/// Ship controller for networked ship gameplay using USM pattern
/// </summary>
[RequireComponent(typeof(USMNode))]
[RequireComponent(typeof(Rigidbody2D))]
public class Ship : MonoBehaviour, IUSMNetworkSync, IBroadcast
{
    #region Serialized Fields - Inspector Configuration
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 500f;
    [SerializeField] private float maxSpeed = 150f;

    [Header("Passenger Settings")]
    [SerializeField] private float passengerVelocityMultiplier = 1.2f;

    [Header("Ship State")]
    [SerializeField] private bool piloted = false;
    [SerializeField] private int health = 100;
    [SerializeField] private int healthMax = 100;

    #endregion

    #region Component References

    private Rigidbody2D rb;
    private USMNode usmNode;
    private GameObject currentPilot;
    private Rigidbody2D currentPilotRb;
    private Vector2 pilotLocalOffset;

    private ShipLever shipLever;
    private GameObject shipConsole;
    private ShipConsole shipConsoleScript;

    private string cachedInstanceID;
    private Collider2D shipCollider;
    private int playerLayer;

    // Passenger tracking for players standing on ship (not piloting)
    // Dictionary maps Rigidbody2D to cached Player script for performance
    private System.Collections.Generic.Dictionary<Rigidbody2D, Player> passengersOnShip = new System.Collections.Generic.Dictionary<Rigidbody2D, Player>();

    // High-friction physics material for better passenger grip
    private PhysicsMaterial2D highFrictionMaterial;

    #endregion

    #region Movement State

    private float horizontalInput;
    private float verticalInput;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        usmNode = GetComponent<USMNode>();

        shipLever = transform.Find("Ship Lever")?.GetComponent<ShipLever>();
        shipConsole = transform.Find("Console")?.gameObject;
        shipConsoleScript = shipConsole?.GetComponent<ShipConsole>();

        cachedInstanceID = this.gameObject.GetInstanceID().ToString();

        // Cache collider and layer information
        shipCollider = GetComponent<Collider2D>();
        playerLayer = LayerMask.NameToLayer("Player");

        // Create and apply high-friction physics material to prevent passengers from sliding
        highFrictionMaterial = new PhysicsMaterial2D("ShipHighFriction")
        {
            friction = 1.0f,      // Maximum friction
            bounciness = 0f       // No bounce
        };

        if (shipCollider != null)
        {
            shipCollider.sharedMaterial = highFrictionMaterial;
        }
    }

    private void Update()
    {
        // Only process input if this is the live node and it's being piloted
        if (!usmNode.LiveNode || !piloted) return;

        HandlePlayerInput();
    }

    private void FixedUpdate()
    {
        // Only process movement if this is the live ship and it's being piloted
        if (usmNode.LiveNode && piloted)
        {
            HandleMovement();
        }

        // Always keep pilot synchronized with ship position (regardless of who's piloting)
        if (currentPilot != null && currentPilotRb != null)
        {
            // Calculate world position based on ship's position and rotation
            Vector2 worldOffset = transform.TransformVector(pilotLocalOffset);
            currentPilotRb.MovePosition((Vector2)transform.position + worldOffset);
        }

        // Always apply ship's velocity to all passengers (regardless of who's piloting)
        // Cache velocity once to avoid multiple property accesses
        Vector2 shipVelocity = rb.linearVelocity;
        Vector2 platformVel = shipVelocity * passengerVelocityMultiplier;

        foreach (var passenger in passengersOnShip)
        {
            Rigidbody2D passengerRb = passenger.Key;
            Player playerScript = passenger.Value;

            if (passengerRb != null && playerScript != null)
            {
                // Set the platform velocity on the player script with multiplier
                // The multiplier compensates for physics timing and helps prevent sliding
                playerScript.platformVelocity = platformVel;

                // Additionally, apply a small friction force to help stick passengers to the ship
                // This compensates for any velocity difference between ship and passenger
                Vector2 velocityDifference = shipVelocity - passengerRb.linearVelocity;
                if (velocityDifference.magnitude > 0.1f)
                {
                    // Apply a force proportional to the velocity difference
                    passengerRb.AddForce(velocityDifference * 2f, ForceMode2D.Force);
                }
            }
        }
    }

    #endregion

    #region Input Handling

    /// <summary>
    /// Process player input for ship movement
    /// </summary>
    private void HandlePlayerInput()
    {
        horizontalInput = Input.GetAxis("Horizontal");
        verticalInput = Input.GetAxis("Vertical");

    }

    #endregion

    #region Movement System

    /// <summary>
    /// Handle ship movement using physics-based forces
    /// </summary>
    private void HandleMovement()
    {
        // Create movement vector from input
        Vector2 inputDirection = new Vector2(horizontalInput, verticalInput);

        // Normalize to prevent faster diagonal movement
        if (inputDirection.magnitude > 1f)
        {
            inputDirection.Normalize();
        }

        // Apply force based on input
        if (inputDirection.magnitude > 0.1f)
        {
            rb.AddForce(inputDirection * moveSpeed, ForceMode2D.Impulse);
        }

        // Clamp to max speed by limiting velocity magnitude
        if (rb.linearVelocity.magnitude > maxSpeed)
        {
            rb.linearVelocity = rb.linearVelocity.normalized * maxSpeed;
        }

    }

    #endregion

    #region Piloting System

    /// <summary>
    /// Toggle piloting mode on/off
    /// </summary>
    public void TogglePiloting()
    {
        piloted = !piloted;
    }

    /// <summary>
    /// Set piloting state directly
    /// </summary>
    public void SetPiloted(bool isPiloted)
    {
        piloted = isPiloted;
    }

    #endregion

    #region Passenger System

    /// <summary>
    /// Detect when a player collides with the ship to add them as a passenger
    /// </summary>
    private void OnCollisionEnter2D(Collision2D collision)
    {
        // Check if the colliding object is a player
        if (collision.gameObject.CompareTag("Player"))
        {
            // Make sure it's not the pilot before adding as passenger
            if (collision.gameObject == currentPilot) return;

            Rigidbody2D passengerRb = collision.gameObject.GetComponent<Rigidbody2D>();

            // Make sure has a rigidbody and not already in dictionary
            if (passengerRb != null && !passengersOnShip.ContainsKey(passengerRb))
            {
                // Cache the Player component for performance
                Player playerScript = collision.gameObject.GetComponent<Player>();
                if (playerScript != null)
                {
                    passengersOnShip.Add(passengerRb, playerScript);
                }
            }
        }
    }

    /// <summary>
    /// Detect when a player leaves the ship to remove them as a passenger
    /// </summary>
    private void OnCollisionExit2D(Collision2D collision)
    {
        // Check if the leaving object is a player
        if (collision.gameObject.CompareTag("Player"))
        {
            Rigidbody2D passengerRb = collision.gameObject.GetComponent<Rigidbody2D>();

            if (passengerRb != null && passengersOnShip.ContainsKey(passengerRb))
            {
                passengersOnShip.Remove(passengerRb);
            }
        }
    }

    #endregion

    #region IUSMNetworkSync Implementation

    /// <summary>
    /// Receive and apply state updates from the network
    /// Called automatically by USMNode when network data arrives
    /// </summary>
    /// <param name="message">Network message containing state data</param>
    public void OnSyncFromNetwork(ref NetworkMessage message)
    {
        // Sync health
        if (message.Int1.HasValue)
            health = message.Int1.Value;

        // Sync piloted state
        if (message.Bool1.HasValue)
        {
            piloted = message.Bool1.Value;

            this.BroadcastMessage("Broadcast", new InteractivePromptModel
            {
                gameobjectID = cachedInstanceID,
                BroadcastID = "ship",
                Value = piloted.ToString(),
                playerInteracted = null
            }, SendMessageOptions.DontRequireReceiver);

            if (shipConsoleScript != null && !usmNode.LiveNode)
                shipConsoleScript.ToggleInteractionPrompt(!piloted);
        }

        // Ship Flood Lights
        if (message.Bool2.HasValue)
        {
            bool floodLightsOn = message.Bool2.Value;

            this.BroadcastMessage("Broadcast", new InteractivePromptModel
            {
                gameobjectID = cachedInstanceID,
                BroadcastID = "floodlights",
                Value = floodLightsOn.ToString(),
                playerInteracted = null
            }, SendMessageOptions.DontRequireReceiver);
        }
    }

    public void Broadcast(InteractivePromptModel interactivePromptModel)
    {
        bool sendUpdate = false;

        switch (interactivePromptModel.BroadcastID)
        {
            case "ship":

                bool powerOn = bool.Parse(interactivePromptModel.Value);

                var pilotGameObject = interactivePromptModel.playerInteracted;
                if (pilotGameObject == null) return;

                // Note: USM handles ownership automatically based on which client initiates the interaction
                // No manual ownership swapping needed with USM pattern

                var pilotScript = pilotGameObject.GetComponent<Player>();
                var pilotRigidbody = pilotGameObject.GetComponent<Rigidbody2D>();

                if (pilotScript != null)
                {
                    // Tell player script to enter/exit piloting mode FIRST
                    pilotScript.Pilot(powerOn);

                    if (pilotRigidbody != null)
                    {
                        if (powerOn)
                        {
                            // Store references to the pilot
                            currentPilot = pilotGameObject;
                            currentPilotRb = pilotRigidbody;

                            // Store the local offset from ship to pilot at the time of attachment
                            pilotLocalOffset = transform.InverseTransformVector(pilotGameObject.transform.position - transform.position);

                            // Make pilot kinematic so it doesn't interact with physics
                            pilotRigidbody.bodyType = RigidbodyType2D.Kinematic;
                            pilotRigidbody.gravityScale = 0f;
                        }
                        else
                        {
                            // Clear pilot references
                            currentPilot = null;
                            currentPilotRb = null;

                            // Restore pilot to dynamic physics
                            pilotRigidbody.bodyType = RigidbodyType2D.Dynamic;
                            pilotRigidbody.gravityScale = 1f;
                        }
                    }
                }

                piloted = powerOn;

                // Handle ship physics based on piloting state
                if (powerOn)
                {
                    // Make ship dynamic and able to move when piloted
                    rb.bodyType = RigidbodyType2D.Dynamic;
                    rb.gravityScale = 0f;

                    // Change ship layer to exclude Player layer collisions when powered on
                    if (shipCollider != null)
                    {
                        // Set to a layer that doesn't collide with Player
                        // This prevents the ship from colliding with the pilot
                        Physics2D.IgnoreLayerCollision(gameObject.layer, playerLayer, true);
                    }
                }
                else
                {
                    // Make ship kinematic when not piloted so it doesn't fall or move
                    rb.linearVelocity = Vector2.zero;
                    rb.bodyType = RigidbodyType2D.Kinematic;
                    rb.gravityScale = 0f;

                    // Restore collision with Player layer when powered off
                    if (shipCollider != null)
                    {
                        Physics2D.IgnoreLayerCollision(gameObject.layer, playerLayer, false);
                    }
                }

                sendUpdate = true;

                break;

            case "floodlights":

                sendUpdate = true;

                break;
        }

        if (sendUpdate)
        {
            if (interactivePromptModel.gameobjectID == cachedInstanceID) return;

            // Send state update to network via USM
            bool floodLightsOn = shipLever != null && shipLever.GetState();
            usmNode.SyncToNetwork(health, piloted, floodLightsOn);
        }


    }

    #endregion
}
