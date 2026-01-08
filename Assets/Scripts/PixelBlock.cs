using UnityEngine;

public class PixelBlock : MonoBehaviour
{
    [SerializeField] private Color startColor = Color.white;
    [SerializeField] private Color endColor = Color.white;

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
        int baseSeed = 1234;
        
        // Get the seed from USMGame
        USMGame usmGame = FindFirstObjectByType<USMGame>();
        if (usmGame != null)
        {
            baseSeed = usmGame.RandomSeed;
        }

        // Combine baseSeed with position for deterministic variety across blocks
        int finalSeed = baseSeed + (int)(transform.position.x * 1000) + (int)(transform.position.y * 100);
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
