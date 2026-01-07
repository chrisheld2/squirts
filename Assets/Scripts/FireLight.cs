using UnityEngine;
using UnityEngine.Rendering.Universal;

public class FireLight : MonoBehaviour
{
    private Light2D light2D;
    private float baseIntensity;
    private float randomOffset;

    [SerializeField] private float fluctuationAmount = 2;
    [SerializeField] private float fluctuationSpeed = 8;
    [SerializeField] private float minIntensity = .5f;
    [SerializeField] private float maxIntensity = 1.5f;

    void Awake()
    {
        light2D = GetComponent<Light2D>();
        if (light2D == null) return;

        baseIntensity = light2D.intensity;
        randomOffset = Random.Range(0f, 1000f);
    }

    void Update()
    {
        if (light2D == null) return;

        // Map Perlin noise from [0, 1] to [-1, 1] to allow for dimming and brightening
        float fluctuation = (Mathf.PerlinNoise(Time.time * fluctuationSpeed, randomOffset) * 2f - 1f) * fluctuationAmount;
        float newIntensity = baseIntensity + fluctuation;
        light2D.intensity = newIntensity;
    }
}
