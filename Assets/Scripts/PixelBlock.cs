using UnityEngine;

public class PixelBlock : MonoBehaviour
{
    [SerializeField] private Color startColor = Color.white;
    [SerializeField] private Color endColor = Color.white;
    [SerializeField] private int randomSeed = 1234;

    private Random.State randomState;

    private void Start()
    {
        SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            InitializeRandomState();
            float t = GetRandomValue();
            spriteRenderer.color = Color.Lerp(startColor, endColor, t);
        }
        else
        {
            Debug.LogWarning($"PixelBlock script on {gameObject.name} requires a SpriteRenderer component.");
        }
    }

    #region Random Utilities

    private void InitializeRandomState()
    {
        // Combine randomSeed with position for deterministic variety across blocks
        int finalSeed = randomSeed + (int)(transform.position.x * 1000) + (int)(transform.position.y * 100);
        Random.InitState(finalSeed);
        randomState = Random.state;
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
