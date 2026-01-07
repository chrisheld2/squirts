
using UnityEngine;
using UnityEngine.Rendering.Universal;

public class Gemstack : MonoBehaviour
{
    [SerializeField] private Sprite[] gemstackSprites = new Sprite[3];

    void Start()
    {
        // Generate a random color
        Color randomColor = new Color(Random.value, Random.value, Random.value);

        // Find the Light2D component and set its color
        Light2D childLight = GetComponentInChildren<Light2D>();
        if (childLight != null)
        {
            childLight.color = randomColor;
        }
        else
        {
            Debug.LogWarning("No Light2D component found in children of Gemstack.");
        }

        // Find the SpriteRenderer and set its color and sprite
        SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            // Set the sprite color
            spriteRenderer.color = randomColor;

            // If sprites are assigned in inspector, use them
            if (gemstackSprites != null && gemstackSprites.Length > 0)
            {
                // Filter out null entries
                Sprite[] validSprites = System.Array.FindAll(gemstackSprites, s => s != null);

                if (validSprites.Length > 0)
                {
                    // Randomize the sprite from the array
                    int randomIndex = Random.Range(0, validSprites.Length);
                    spriteRenderer.sprite = validSprites[randomIndex];
                }
                else
                {
                    Debug.LogWarning("No valid Gemstack sprites assigned in the inspector.");
                }
            }
            else
            {
                Debug.LogWarning("Gemstack sprites array is not assigned. Please assign sprites in the inspector.");
            }
        }
        else
        {
            Debug.LogWarning("No SpriteRenderer component found on Gemstack.");
        }
    }
}
