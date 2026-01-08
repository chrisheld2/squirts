using UnityEngine;

[ExecuteAlways]
public class PixelBlock : MonoBehaviour
{
    [SerializeField] private Color startColor = Color.white;
    [SerializeField] private Color endColor = Color.white;

    private Random.State randomState;
    private Vector3 lastPosition;

    private void Start()
    {
        ApplyColor();
    }

    private void OnEnable()
    {
        lastPosition = transform.position;
    }

#if UNITY_EDITOR
    private void Update()
    {
        // In editor mode, refresh color if the block has been moved
        if (!Application.isPlaying && transform.position != lastPosition)
        {
            lastPosition = transform.position;
            ApplyColor();
        }
    }

    private void OnValidate()
    {
        // Refresh color when properties (startColor, endColor) are changed in the inspector
        if (gameObject.activeInHierarchy)
        {
            ApplyColor();
        }
    }
#endif

    public void ApplyColor()
    {
        SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            InitializeRandomState();
            float t = GetRandomValue();
            spriteRenderer.color = Color.Lerp(startColor, endColor, t);
        }
        else if (Application.isPlaying)
        {
            Debug.LogWarning($"PixelBlock script on {gameObject.name} requires a SpriteRenderer component.");
        }
    }

    #region Random Utilities

    private void InitializeRandomState()
    {
        int baseSeed = 1234;
        
        // Get the seed from USMGame
        USMGame usmGame = FindFirstObjectByType<USMGame>();
        if (usmGame != null)
        {
            baseSeed = usmGame.RandomSeed;
        }

        // Combine baseSeed with position for deterministic variety across blocks
        int finalSeed = baseSeed + (int)(transform.position.x * 1000) + (int)(transform.position.y * 100);
        
        // Save current global state to avoid affecting other editor/game logic
        Random.State oldState = Random.state;
        Random.InitState(finalSeed);
        randomState = Random.state;
        // Restore global state
        Random.state = oldState;
    }

    private void RestoreRandomState()
    {
        Random.state = randomState;
    }

    private float GetRandomValue()
    {
        // Store current global state to avoid affecting other scripts
        Random.State oldState = Random.state;
        
        RestoreRandomState();
        float result = Random.value;
        randomState = Random.state;
        
        // Restore global state
        Random.state = oldState;
        
        return result;
    }

    #endregion
}
