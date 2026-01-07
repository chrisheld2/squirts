using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class SpriteFlicker : MonoBehaviour
{
    [Header("Color Settings")]
    [SerializeField] private Color baseColor = Color.white;
    [SerializeField, Range(0f, 1f)] private float transparency = 1f;

    [Header("Size Settings")]
    [SerializeField, Range(0.1f, 10f)] private float width = 1f;
    [SerializeField, Range(0.1f, 10f)] private float height = 1f;

    [Header("Flicker Settings")]
    [SerializeField] private bool enableFlicker = true;
    [SerializeField, Range(0f, 20f)] private float flickerSpeed = 5f;
    [SerializeField, Range(0f, 1f)] private float flickerRange = 0.3f;
    [SerializeField] private AnimationCurve flickerCurve = AnimationCurve.Linear(0, 0, 1, 1);

    [Header("Random Variation")]
    [SerializeField] private bool useRandomVariation = true;
    [SerializeField, Range(0f, 0.5f)] private float randomIntensity = 0.1f;
    [SerializeField, Range(0f, 10f)] private float randomSpeed = 2f;

    [Header("Smooth Transitions")]
    [SerializeField] private bool useSmoothTransitions = true;
    [SerializeField, Range(0f, 20f)] private float smoothingSpeed = 8f;

    private SpriteRenderer spriteRenderer;
    private float targetBrightness;
    private float currentBrightness;
    private float timeOffset;
    private float randomOffset;

    public bool EnableFlicker
    {
        get => enableFlicker;
        set => enableFlicker = value;
    }

    public Color BaseColor
    {
        get => baseColor;
        set
        {
            baseColor = value;
            ApplyColor();
        }
    }

    public float Transparency
    {
        get => transparency;
        set
        {
            transparency = Mathf.Clamp01(value);
            ApplyColor();
        }
    }

    public float FlickerSpeed
    {
        get => flickerSpeed;
        set => flickerSpeed = Mathf.Max(0f, value);
    }

    public float FlickerRange
    {
        get => flickerRange;
        set => flickerRange = Mathf.Clamp01(value);
    }

    public float Width
    {
        get => width;
        set
        {
            width = Mathf.Max(0.1f, value);
            ApplySize();
        }
    }

    public float Height
    {
        get => height;
        set
        {
            height = Mathf.Max(0.1f, value);
            ApplySize();
        }
    }

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        timeOffset = Random.Range(0f, 1000f);
        randomOffset = Random.Range(0f, 1000f);
        currentBrightness = 1f;
        targetBrightness = 1f;
    }

    private void Start()
    {
        if (spriteRenderer != null)
        {
            baseColor = spriteRenderer.color;
            transparency = baseColor.a;
        }
        ApplySize();
    }

    private void Update()
    {
        if (!enableFlicker || spriteRenderer == null)
        {
            return;
        }

        CalculateTargetBrightness();
        ApplyColor();
    }

    private void CalculateTargetBrightness()
    {
        float time = Time.time + timeOffset;
        float flickerValue = 0f;

        if (flickerCurve != null && flickerCurve.keys.Length > 0)
        {
            float curveTime = Mathf.Repeat(time * flickerSpeed, flickerCurve.keys[flickerCurve.keys.Length - 1].time);
            flickerValue = flickerCurve.Evaluate(curveTime);
        }
        else
        {
            flickerValue = Mathf.PerlinNoise(time * flickerSpeed, 0f);
        }

        // Convert flickerValue (0-1) to range centered around 1.0
        float mainFlicker = (flickerValue - 0.5f) * 2f * flickerRange;

        float randomVariation = 0f;
        if (useRandomVariation)
        {
            randomVariation = (Mathf.PerlinNoise((time + randomOffset) * randomSpeed, 0f) - 0.5f) * randomIntensity;
        }

        targetBrightness = 1f + mainFlicker + randomVariation;
        targetBrightness = Mathf.Clamp(targetBrightness, 0f, 2f);
    }

    private void ApplyColor()
    {
        if (spriteRenderer == null)
            return;

        if (useSmoothTransitions && enableFlicker)
        {
            currentBrightness = Mathf.Lerp(currentBrightness, targetBrightness, smoothingSpeed * Time.deltaTime);
        }
        else if (enableFlicker)
        {
            currentBrightness = targetBrightness;
        }
        else
        {
            currentBrightness = 1f;
        }

        // Apply brightness to color while preserving hue
        Color flickeredColor = baseColor * currentBrightness;
        flickeredColor.a = transparency;

        spriteRenderer.color = flickeredColor;
    }

    private void ApplySize()
    {
        if (spriteRenderer == null)
            return;

        transform.localScale = new Vector3(width, height, 1f);
    }

    public void SetFlickerSettings(Color color, float speed, float range, float alpha)
    {
        baseColor = color;
        flickerSpeed = Mathf.Max(0f, speed);
        flickerRange = Mathf.Clamp01(range);
        transparency = Mathf.Clamp01(alpha);
        ApplyColor();
    }

    public void StartFlicker()
    {
        enableFlicker = true;
    }

    public void StopFlicker()
    {
        enableFlicker = false;
        currentBrightness = 1f;
        ApplyColor();
    }

    // Allows setting color and size in editor and seeing immediate results
    private void OnValidate()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        if (spriteRenderer != null && !Application.isPlaying)
        {
            Color previewColor = baseColor;
            previewColor.a = transparency;
            spriteRenderer.color = previewColor;
            transform.localScale = new Vector3(width, height, 1f);
        }
    }
}
