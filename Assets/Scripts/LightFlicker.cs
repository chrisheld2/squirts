using UnityEngine;
using UnityEngine.Rendering.Universal;

[RequireComponent(typeof(Light2D))]
public class LightFlicker : MonoBehaviour
{
    [Header("Flicker Settings")]
    [SerializeField] private bool enableFlicker = true;
    [SerializeField] private float baseIntensity = 1f;
    [SerializeField] private float flickerIntensity = 0.3f;
    [SerializeField] private float flickerSpeed = 5f;
    [SerializeField] private AnimationCurve flickerCurve = AnimationCurve.Linear(0, 0, 1, 1);
    
    [Header("Random Variation")]
    [SerializeField] private bool useRandomVariation = true;
    [SerializeField] private float randomIntensity = 0.1f;
    [SerializeField] private float randomSpeed = 2f;
    
    [Header("Smooth Transitions")]
    [SerializeField] private bool useSmoothTransitions = true;
    [SerializeField] private float smoothingSpeed = 8f;

    private Light2D light2D;
    private float targetIntensity;
    private float currentIntensity;
    private float timeOffset;
    private float randomOffset;

    public bool EnableFlicker 
    { 
        get => enableFlicker; 
        set => enableFlicker = value; 
    }
    
    public float BaseIntensity 
    { 
        get => baseIntensity; 
        set => baseIntensity = Mathf.Max(0f, value); 
    }
    
    public float FlickerIntensity 
    { 
        get => flickerIntensity; 
        set => flickerIntensity = Mathf.Clamp01(value); 
    }
    
    public float FlickerSpeed 
    { 
        get => flickerSpeed; 
        set => flickerSpeed = Mathf.Max(0f, value); 
    }

    private void Awake()
    {
        light2D = GetComponent<Light2D>();
        timeOffset = Random.Range(0f, 1000f);
        randomOffset = Random.Range(0f, 1000f);
        currentIntensity = baseIntensity;
        targetIntensity = baseIntensity;
    }

    private void Start()
    {
        if (light2D != null)
        {
            baseIntensity = light2D.intensity;
        }
    }

    private void Update()
    {
        if (!enableFlicker || light2D == null)
        {
            return;
        }

        CalculateTargetIntensity();
        ApplyIntensity();
    }

    private void CalculateTargetIntensity()
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

        float mainFlicker = flickerValue * flickerIntensity;

        float randomVariation = 0f;
        if (useRandomVariation)
        {
            randomVariation = (Mathf.PerlinNoise((time + randomOffset) * randomSpeed, 0f) - 0.5f) * randomIntensity;
        }

        targetIntensity = baseIntensity + mainFlicker + randomVariation;
        targetIntensity = Mathf.Max(0f, targetIntensity);
    }

    private void ApplyIntensity()
    {
        if (useSmoothTransitions)
        {
            currentIntensity = Mathf.Lerp(currentIntensity, targetIntensity, smoothingSpeed * Time.deltaTime);
        }
        else
        {
            currentIntensity = targetIntensity;
        }

        light2D.intensity = currentIntensity;
    }

    public void SetFlickerSettings(float baseInt, float flickerInt, float speed)
    {
        baseIntensity = Mathf.Max(0f, baseInt);
        flickerIntensity = Mathf.Clamp01(flickerInt);
        flickerSpeed = Mathf.Max(0f, speed);
    }

    public void StartFlicker()
    {
        enableFlicker = true;
    }

    public void StopFlicker()
    {
        enableFlicker = false;
        if (useSmoothTransitions)
        {
            targetIntensity = baseIntensity;
        }
        else
        {
            light2D.intensity = baseIntensity;
        }
    }
}