using UnityEngine;

/// <summary>
/// Player controller handling movement, climbing, jumping, aiming, and networking synchronization
/// </summary>
public class Player : MonoBehaviour, IUSMNetworkSync
{
    #region Serialized Fields - Inspector Configuration

    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float aimSpeed = 15f;
    [SerializeField] private float jumpForce = 1f;

    [Header("Collision Detection")]
    [SerializeField] private bool isPiloting = false;
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private LayerMask climbLayer;
    [SerializeField] private float groundCheckRadius = 0.2f;
    [SerializeField] private Vector3 groundCheckOffset = new Vector3(0f, 0f, 0f);
    [SerializeField] private Vector3 climbableCheckOffset = new Vector3(0f, 0f, 0f);
    [SerializeField] private float climbableCheckHorizontalSize = 0.2f;
    [SerializeField] private float wallCheckVerticalSize = 0.2f;
    [SerializeField] private float downwardForce = 1f;

    [Header("Player State")]
    [SerializeField] private int health = 100;
    [SerializeField] private int healthMax = 100;

    [Header("Flare System")]
    [SerializeField] public int flareMax = 3;

    [Header("Bomb System")]
    [SerializeField] public int bombMax = 3;

    [Header("Audio")]
    [SerializeField] private AudioClip footstepsDirtClip;

    #endregion

    #region Component References

    private CameraSway cameraSwayScript;
    private AudioSource audioSource;
    private Rigidbody2D rb;
    private Transform aimTransform;
    private Transform handTransform;
    private Animator anim;
    private Animator animBackPack;
    private Animator animHelmet;
    private SpriteRenderer spriteRenderer;
    private Vector2 startingPosition;
    private Camera mainCamera; // Cached camera reference for performance
    private USMNode usmNode; // Reference to USM node for control validation
    private FloatingText floatingText; // Reference to floating text for displaying NodeID
    private UnifiedSyncMatrix usm; // Reference to UnifiedSyncMatrix for networked object spawning

    #endregion

    #region Movement and Physics State

    private bool isFeetOnTheGround;
    private bool isTouchingClimbable;
    private bool jumpRequest;
    private bool isJumping;
    private bool isClimbing;
    private float horizontalDirection;
    private float verticalDirection;

    // Platform movement tracking
    [HideInInspector] public Vector2 platformVelocity = Vector2.zero;

    // Network sync tracking for change detection
    private float lastSyncedAimAngle;
    private Vector2 lastSyncedPosition;
    private const float AIM_ANGLE_THRESHOLD = 1f; // Only sync if aim changes by more than 1 degree
    private const float POSITION_THRESHOLD = 0.01f; // Only sync if position changes by this amount

    // Remote player aim interpolation
    private float targetRemoteAimAngle; // Target angle received from network
    private bool hasReceivedRemoteAimAngle; // Track if we've received network data

    #endregion

    #region Animation

    private int currentAnimationState;

    // Animation state hashes
    private static readonly int ANIM_PLAYER_IDLE = Animator.StringToHash("Player_Idle");
    private static readonly int ANIM_PLAYER_RUN = Animator.StringToHash("Player_Run");
    private static readonly int ANIM_PLAYER_JUMP = Animator.StringToHash("Player_Jump");
    private static readonly int ANIM_PLAYER_CLIMB = Animator.StringToHash("Player_Climb");

    #endregion

    #region Flare System

    private int currentFlareCount = 0;
    private const string ACTION_FLARE = "Flare";

    #endregion

    #region Bomb System

    private int currentBombCount = 0;
    private const string ACTION_BOMB = "Bomb";

    #endregion

    #region Crate System

    private const string ACTION_SPAWN_CRATE = "SpawnCrate";
    private const float CRATE_SPAWN_DISTANCE = 1.5f;

    #endregion

    #region Ball System

    private const string ACTION_LAUNCH_BALL = "LaunchBall";

    #endregion

    #region Unity Lifecycle

    /// <summary>
    /// Initialize component references and subscribe to events
    /// </summary>
    private void Awake()
    {
        // Get core components
        rb = GetComponent<Rigidbody2D>();
        audioSource = GetComponent<AudioSource>();

        // Find and cache sprite components
        var spriteTransform = transform.Find("Sprite");
        spriteTransform.TryGetComponent(out spriteRenderer);
        spriteTransform.TryGetComponent(out anim);

        // Find optional accessory animators
        var backPackMount = transform.Find("BackPackMount");
        animBackPack = backPackMount != null && backPackMount.TryGetComponent<Animator>(out var backPackAnim) ? backPackAnim : null;

        var helmetMount = transform.Find("HelmetMount");
        if (helmetMount != null && helmetMount.TryGetComponent<Animator>(out var helmetAnim))
        {
            animHelmet = helmetAnim;
        }

        // Find aiming-related transforms
        aimTransform = transform.Find("CENTER/AIM");
        handTransform = transform.Find("CENTER/AIM/HAND");

        // Cache main camera reference for performance (avoid Camera.main lookups)
        mainCamera = Camera.main;

        // Find camera sway script
        var mainCameraGO = GameObject.Find("Main Camera");
        if (mainCameraGO != null && mainCameraGO.TryGetComponent<CameraSway>(out var cameraSway))
            cameraSwayScript = cameraSway;

        // Get USMNode component early to ensure it's ready for network sync
        usmNode = GetComponent<USMNode>();

        // Get FloatingText component for displaying NodeID
        var floatingTextObj = transform.Find("FloatingText");
        if (floatingTextObj != null && floatingTextObj.TryGetComponent(out floatingText))
        {
            floatingText.Visible = true;
        }

        // Find UnifiedSyncMatrix in the scene for networked object spawning
        usm = FindAnyObjectByType<UnifiedSyncMatrix>();

        // Subscribe to flare events
        Flare.flareFinished += OnFlareFinished;

        // Subscribe to bomb events
        Bomb.bombExploded += OnBombExploded;
    }

    private void Start()
    {
        startingPosition = transform.position;

        // Initialize network sync tracking (use local space if parented, world space otherwise)
        bool useLocalSpace = transform.parent != null;
        lastSyncedPosition = useLocalSpace ? (Vector2)transform.localPosition : (Vector2)transform.position;
        lastSyncedAimAngle = aimTransform != null ? aimTransform.eulerAngles.z : 0f;
    }

    /// <summary>
    /// Clean up event subscriptions
    /// </summary>
    private void OnDestroy()
    {
        Flare.flareFinished -= OnFlareFinished;
        Bomb.bombExploded -= OnBombExploded;
    }

    /// <summary>
    /// Handle input, visual updates, and debug information per frame
    /// </summary>
    private void Update()
    {
        // Update debug information display
        UpdateDebugDisplay();

        if (isPiloting) return;

        // Determine if this is a remote player copy
        bool isRemotePlayer = usmNode != null && (usmNode.NodeType != USMNode.NODETYPE.ACTIVE || !usmNode.LiveNode);

        // Update visual elements every frame
        Aim();
        FlipPlayer();
        AnimationLogic();

        // Only handle input for local/live players
        // Network sync is now handled automatically by USMNode
        if (!isRemotePlayer)
        {
            HandlePlayerInput();
        }
    }

    /// <summary>
    /// Handle physics-based movement and collision detection
    /// </summary>
    private void FixedUpdate()
    {
        if (isPiloting) return;

        // Determine if this is a remote player copy
        bool isRemotePlayer = usmNode != null && (usmNode.NodeType != USMNode.NODETYPE.ACTIVE || !usmNode.LiveNode);

        if (isRemotePlayer)
        {
            // Remote players: Position is handled automatically by USMNode.ApplyDefaultNetworkSync()
            // No custom physics processing needed for remote players
            return;
        }

        // Local/live players: Process physics normally
        UpdateCollisionState();
        HandleMovement();
        HandleJumping();
        UpdateGravity();
        ApplyGroundStickiness();

        // Sync position, velocity, and aim angle together
        SyncPlayerState();
    }

    #endregion

    #region Event Handlers

    /// <summary>
    /// Handle flare finished event
    /// </summary>
    private void OnFlareFinished()
    {
        currentFlareCount--;
        if (currentFlareCount < 0) currentFlareCount = 0;
    }

    /// <summary>
    /// Handle bomb exploded event
    /// </summary>
    private void OnBombExploded()
    {
        currentBombCount--;
        if (currentBombCount < 0) currentBombCount = 0;
    }

    #endregion

    #region Input Handling

    /// <summary>
    /// Process player input for movement and actions
    /// Only processes input if USMNode type is ACTIVE and LiveNode is true (not a guest copy)
    /// </summary>
    private void HandlePlayerInput()
    {
        // Only allow input control for ACTIVE nodes that are live (not guest copies)
        if (usmNode == null || usmNode.NodeType != USMNode.NODETYPE.ACTIVE || !usmNode.LiveNode)
        {
            horizontalDirection = 0f;
            verticalDirection = 0f;
            return;
        }

        horizontalDirection = Input.GetAxis("Horizontal");
        verticalDirection = Input.GetAxis("Vertical");

        if (Input.GetButtonDown("Jump") && isFeetOnTheGround && !isJumping)
        {
            jumpRequest = true;
        }

        if (Input.GetKeyDown(KeyCode.F))
        {
            Action_Event(ACTION_FLARE, null);
        }

        if (Input.GetKeyDown(KeyCode.C))
        {
            Action_Event(ACTION_SPAWN_CRATE, null);
        }

        if (Input.GetKeyDown(KeyCode.B))
        {
            Action_Event(ACTION_LAUNCH_BALL, null);
        }

        if (Input.GetKeyDown(KeyCode.G))
        {
            Action_Event(ACTION_BOMB, null);
        }
    }

    #endregion

    #region Movement System

    /// <summary>
    /// Update collision detection states
    /// </summary>
    private void UpdateCollisionState()
    {
        isFeetOnTheGround = Physics2D.OverlapCircle(transform.position + groundCheckOffset, groundCheckRadius, groundLayer);
        isTouchingClimbable = Physics2D.OverlapBox(transform.position, new Vector2(climbableCheckHorizontalSize, wallCheckVerticalSize), 0, climbLayer);
    }

    /// <summary>
    /// Handle horizontal and vertical movement
    /// </summary>
    private void HandleMovement()
    {
        // Handle horizontal movement
        if (!isJumping)
        {
            // Add platform velocity to player's own movement for proper platform support
            rb.linearVelocityX = (horizontalDirection * moveSpeed) + platformVelocity.x;
        }

        // Handle vertical movement (climbing)
        if (isTouchingClimbable && verticalDirection != 0)
        {
            isClimbing = true;
            rb.linearVelocityY = verticalDirection * moveSpeed;
        }
        else
        {
            isClimbing = false;
        }

        // Reset platform velocity after applying it (ship will set it again next frame if still on platform)
        platformVelocity = Vector2.zero;
    }

    /// <summary>
    /// Handle jumping mechanics
    /// </summary>
    private void HandleJumping()
    {
        // Reset jumping state when grounded
        if (isJumping && (isFeetOnTheGround || isTouchingClimbable))
        {
            isJumping = false;
        }

        // Execute jump if requested
        if (jumpRequest && (isFeetOnTheGround || isTouchingClimbable))
        {
            jumpRequest = false;
            isJumping = true;
            rb.linearVelocityY = jumpForce;
        }
    }

    /// <summary>
    /// Update gravity scale based on climbing state
    /// </summary>
    private void UpdateGravity()
    {
        rb.gravityScale = (isJumping && isTouchingClimbable) ? 0 : 1;
    }

    /// <summary>
    /// Apply downward force to help player stick to ground while moving
    /// </summary>
    private void ApplyGroundStickiness()
    {
        if (!isJumping && horizontalDirection != 0 && !isTouchingClimbable)
        {
            rb.AddForce(Vector2.down * downwardForce);
        }
    }

    #endregion

    #region Debug System

    /// <summary>
    /// Update debug display with current player state
    /// </summary>
    private void UpdateDebugDisplay()
    {
        // Update floating text with NodeID
        if (floatingText != null && usmNode != null)
        {
            floatingText.Text = usmNode.NodeID;
        }
    }

    #endregion

    #region Action System

    /// <summary>
    /// Handle player actions like flare throwing, crate spawning, and ball launching
    /// </summary>
    /// <param name="value">Action identifier</param>
    public void Action_Event(string name, string value)
    {
        switch (name)
        {
            case ACTION_FLARE:
                ThrowFlare();
                break;

            case ACTION_SPAWN_CRATE:
                SpawnCrate();
                break;

            case ACTION_LAUNCH_BALL:
                LaunchBall();
                break;

            case ACTION_BOMB:
                ThrowBomb();
                break;
        }
    }

    public void Pilot(bool piloting)
    {
        isPiloting = piloting;

        // Force immediate sync for critical state change
        if (usmNode != null)
        {
            usmNode.ForceNextSync();
        }

        if (isPiloting)
        {
            // Disable all physics when piloting
            rb.gravityScale = 0f;
            rb.linearVelocity = Vector2.zero;
        }
        else
        {
            // Re-enable gravity when exiting piloting mode
            rb.gravityScale = 1f;
        }
    }
    /// <summary>
    /// Throw a flare if available
    /// </summary>
    private void ThrowFlare()
    {
        if (currentFlareCount >= flareMax) return;

        GameObject flare = usm.InstantiateNetworkedObject("Flare", handTransform.position, null, forceIsLive: true);

        if (flare != null)
        {
            if (flare.TryGetComponent(out Rigidbody2D flareRb))
                flareRb.linearVelocity = handTransform.rotation * Vector2.up * 12f;

            currentFlareCount++;
        }

    }

    /// <summary>
    /// Spawn a crate at a short distance from the aim transform
    /// </summary>
    private void SpawnCrate()
    {
        if (usm == null || aimTransform == null) return;

        // Calculate spawn position based on aim direction
        Vector2 aimDirection = (handTransform.position - transform.position).normalized;
        Vector3 spawnPosition = handTransform.position + handTransform.rotation * Vector2.up * 3f + (Vector3)(aimDirection * CRATE_SPAWN_DISTANCE);

        GameObject crate = usm.InstantiateNetworkedObject("CrateHitBoxes", spawnPosition, Folders.LEVEL, forceIsLive: true);
    }

    /// <summary>
    /// Launch a ball from the hand position with velocity
    /// </summary>
    private void LaunchBall()
    {
        if (usm == null || handTransform == null) return;

        GameObject ball = usm.InstantiateNetworkedObject("Ball", handTransform.position, null, forceIsLive: true);

        if (ball != null)
        {
            if (ball.TryGetComponent(out Rigidbody2D ballRb))
            {
                // Launch ball in the direction the player is aiming
                ballRb.linearVelocity = handTransform.rotation * Vector2.up * 15f;
            }
        }
    }

    /// <summary>
    /// Throw a bomb if available
    /// </summary>
    private void ThrowBomb()
    {
        if (currentBombCount >= bombMax) return;

        GameObject bomb = usm.InstantiateNetworkedObject("Bomb", handTransform.position + handTransform.rotation * Vector2.up * 3f, null, forceIsLive: true);

        if (bomb != null)
        {
            if (bomb.TryGetComponent(out Rigidbody2D bombRb))
                bombRb.linearVelocity = handTransform.rotation * Vector2.up * 20f;

            currentBombCount++;
        }
    }

    #endregion

    #region Animation System

    /// <summary>
    /// Handle animation state changes and audio based on player state
    /// </summary>
    private void AnimationLogic()
    {
        (int animationState, float animSpeed) = DetermineAnimationState();

        ChangeAnimationState(animationState);
        SyncAnimationSpeed(animSpeed);
        HandleFootstepAudio(animationState, animSpeed);
    }

    /// <summary>
    /// Determine the appropriate animation state and speed based on player conditions
    /// </summary>
    /// <returns>Tuple containing animation state hash and animation speed</returns>
    private (int state, float speed) DetermineAnimationState()
    {
        if (isClimbing)
            return (ANIM_PLAYER_CLIMB, Mathf.Abs(rb.linearVelocityY));

        if (isFeetOnTheGround)
        {
            return horizontalDirection != 0
                ? (ANIM_PLAYER_RUN, Mathf.Abs(rb.linearVelocityX))
                : (ANIM_PLAYER_IDLE, 1f);
        }

        return rb.linearVelocityY > 0
            ? (ANIM_PLAYER_JUMP, 1f)
            : (ANIM_PLAYER_IDLE, 1f);
    }

    /// <summary>
    /// Change animation state if different from current
    /// </summary>
    /// <param name="newState">New animation state hash to apply</param>
    private void ChangeAnimationState(int newState)
    {
        if (newState == currentAnimationState) return;

        anim.Play(newState);
        currentAnimationState = newState;
    }

    /// <summary>
    /// Sync animation speed across all animators
    /// </summary>
    /// <param name="animSpeed">Animation speed to apply</param>
    private void SyncAnimationSpeed(float animSpeed)
    {
        anim.speed = animSpeed;
        if (animBackPack != null) animBackPack.speed = animSpeed;
        if (animHelmet != null) animHelmet.speed = animSpeed;
    }

    /// <summary>
    /// Handle footstep audio based on animation state and speed
    /// </summary>
    /// <param name="animationState">Current animation state hash</param>
    /// <param name="animSpeed">Current animation speed</param>
    private void HandleFootstepAudio(int animationState, float animSpeed)
    {
        if (footstepsDirtClip == null || audioSource == null) return;

        bool shouldPlayFootsteps = animationState == ANIM_PLAYER_RUN;

        if (shouldPlayFootsteps)
        {
            if (!audioSource.isPlaying)
            {
                audioSource.clip = footstepsDirtClip;
                audioSource.Play();
            }
            audioSource.pitch = animSpeed / 3f;
        }
        else if (audioSource.isPlaying && audioSource.clip == footstepsDirtClip)
        {
            audioSource.Stop();
        }
    }

    #endregion

    #region Aiming System

    /// <summary>
    /// Handle player aiming for flashlight and weapon direction
    /// Note: Network sync is handled in SyncPlayerState() during FixedUpdate, not here
    /// </summary>
    private void Aim()
    {
        float targetAngle = CalculateAimAngle();
        ApplyAimRotation(targetAngle);
        // Aim angle is synced with position in SyncPlayerState() - not synced separately here
    }

    /// <summary>
    /// Calculate the target aim angle based on player input or network data
    /// Only processes mouse input if USMNode type is ACTIVE and LiveNode is true (not a guest copy)
    /// For remote players, interpolates towards the networked target angle
    /// </summary>
    /// <returns>Target angle in degrees</returns>
    private float CalculateAimAngle()
    {
        // Only allow mouse aiming for ACTIVE nodes that are live (not guest copies)
        if (usmNode == null || usmNode.NodeType != USMNode.NODETYPE.ACTIVE || !usmNode.LiveNode)
        {
            // Remote players: interpolate towards target angle received from network
            if (hasReceivedRemoteAimAngle)
            {
                return targetRemoteAimAngle;
            }

            // Return current angle if no network data received yet
            return aimTransform.eulerAngles.z;
        }

        // Use cached camera reference instead of Camera.main (expensive lookup)
        var mouseWorldPos = mainCamera.ScreenToWorldPoint(Input.mousePosition);
        mouseWorldPos.z = 0f;

        Vector2 aimDirection = (mouseWorldPos - transform.position).normalized;
        float angle = Mathf.Atan2(aimDirection.y, aimDirection.x) * Mathf.Rad2Deg - 90f;

        if (cameraSwayScript != null)
            cameraSwayScript.playerAim = aimDirection;

        return angle;


    }

    /// <summary>
    /// Apply smooth rotation to the aim transform
    /// </summary>
    /// <param name="targetAngle">Target angle to rotate towards</param>
    private void ApplyAimRotation(float targetAngle)
    {
        var targetRotation = Quaternion.Euler(0, 0, targetAngle);
        aimTransform.rotation = Quaternion.Lerp(aimTransform.rotation, targetRotation, Time.deltaTime * aimSpeed);
    }

    /// <summary>
    /// Sync all player state including position, velocity, and aim angle
    /// Uses USMNode.SyncPhysical to send complete network messages without clearing position
    /// Called from FixedUpdate to batch all network data together and avoid race conditions
    /// Only syncs when position or aim angle changes significantly (respects networkInterval timing)
    /// </summary>
    private void SyncPlayerState()
    {
        if (usmNode == null || rb == null || aimTransform == null)
            return;

        // Use local space position if the transform has a parent, otherwise use world space
        // This matches USMNode's internal UseLocalSpace logic
        bool useLocalSpace = transform.parent != null;
        Vector2 currentPosition = useLocalSpace ? (Vector2)transform.localPosition : (Vector2)transform.position;
        float currentAimAngle = aimTransform.eulerAngles.z;

        // Check if position has changed enough to warrant a sync
        float positionDistance = Vector2.Distance(currentPosition, lastSyncedPosition);
        bool positionChanged = positionDistance > POSITION_THRESHOLD;

        // Check if aim angle has changed enough to warrant a sync
        float angleDifference = Mathf.Abs(Mathf.DeltaAngle(currentAimAngle, lastSyncedAimAngle));
        bool aimAngleChanged = angleDifference > AIM_ANGLE_THRESHOLD;

        // Only attempt sync if position or aim angle changed significantly
        // SyncPhysical internally handles:
        // - networkInterval timing (won't send too frequently)
        // - firstSync flag (ensures Name is sent on first sync)
        if (positionChanged || aimAngleChanged)
        {
            // Send position (local or world based on parent), velocity, and aim angle together
            usmNode.SyncPhysical(
                currentPosition,
                rb.linearVelocity,
                currentAimAngle  // Float1: aim angle
            );

            // Update last synced values
            // Note: SyncPhysical may not actually send if networkInterval hasn't elapsed,
            // but we update these values to avoid redundant checks
            lastSyncedPosition = currentPosition;
            lastSyncedAimAngle = currentAimAngle;
        }
    }

    /// <summary>
    /// Flip the player sprite based on aim direction
    /// </summary>
    private void FlipPlayer()
    {
        float aimAngle = aimTransform.eulerAngles.z;
        Vector3 scale = transform.localScale;

        scale.x = aimAngle > 180 ? 1 : -1;
        transform.localScale = scale;
    }

    #endregion

    #region Particle Collision System
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Damage"))
        {
            takeDamage();

        }
    }
    private void OnParticleCollision(GameObject collision)
    {
        if (collision.CompareTag("Damage"))
        {
            takeDamage();
        }

    }

    #endregion

    #region IUSMNetworkSync Implementation

    /// <summary>
    /// Receives network updates for Player-specific values.
    /// USMNode.ApplyDefaultNetworkSync() handles position (Vector2_1) automatically before this is called.
    /// This method only handles custom Player values: health, aim angle, piloting state.
    /// </summary>
    public void OnSyncFromNetwork(ref NetworkMessage message)
    {
        // Early return if critical components aren't initialized yet
        if (usmNode == null || rb == null || aimTransform == null)
        {
            return;
        }

        // Determine if this is a remote player copy
        bool isRemotePlayer = usmNode.NodeType != USMNode.NODETYPE.ACTIVE || !usmNode.LiveNode;

        // Sync health (Int1)
        if (message.Int1.HasValue)
        {
            health = message.Int1.Value;
        }

        // Sync aim angle (Float1)
        if (message.Float1.HasValue)
        {
            // Only store aim angle for remote players (local player controls their own aim)
            if (isRemotePlayer)
            {
                targetRemoteAimAngle = message.Float1.Value;
                hasReceivedRemoteAimAngle = true;
                // The actual interpolation happens in Aim() -> CalculateAimAngle() -> ApplyAimRotation()
            }
        }

        // Sync piloting state (Bool1)
        if (message.Bool1.HasValue)
        {
            bool newPilotingState = message.Bool1.Value;

            // Only apply state change if it actually changed
            if (isPiloting != newPilotingState)
            {
                isPiloting = newPilotingState;

                // Apply physics changes for piloting state
                if (isPiloting)
                {
                    rb.gravityScale = 0f;
                    rb.linearVelocity = Vector2.zero;
                }
                else
                {
                    rb.gravityScale = 1f;
                }
            }
        }

        // Note: Position (Vector2_1) is handled automatically by USMNode.ApplyDefaultNetworkSync()
        // No custom position interpolation needed here since USMNode handles it
    }

    #endregion

    private void takeDamage()
    {
        // Only take damage and sync if this is a live node (not a guest copy)
        if (usmNode == null || !usmNode.LiveNode)
            return;

        health -= 10;

        if (health <= 0)
        {
            health = healthMax;
            transform.position = startingPosition;
        }

        // Force immediate sync for critical state changes (health/death)
        if (usmNode != null)
        {
            usmNode.ForceNextSync();
        }
    }

    #region Debug Visualization

    /// <summary>
    /// Draw debug gizmos for collision detection areas
    /// </summary>
    private void OnDrawGizmos()
    {
        // Ground detection area
        Gizmos.color = Color.hotPink;
        Gizmos.DrawWireSphere(transform.position + groundCheckOffset, groundCheckRadius);

        // Climbable detection area
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(transform.position + climbableCheckOffset,
                          new Vector3(climbableCheckHorizontalSize, wallCheckVerticalSize, 0));
    }

    #endregion
}
