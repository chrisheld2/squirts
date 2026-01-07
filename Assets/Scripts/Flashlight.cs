using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public class FlashLight : MonoBehaviour
{

    [SerializeField]
    public bool GUILog = false;

    [SerializeField]
    public float range = 1;

    [SerializeField]
    public Color color = Color.white;

    [SerializeField]
    public float intensity = 1f;

    [SerializeField]
    public bool volumeIntensityEnabled = false;
    [SerializeField]
    public float volumeIntensity = .01f;

    [SerializeField]
    public float outerRadius = 1;
    [SerializeField]
    public float innerRadius = 1;

    private float timeSinceLastUpdate = 0f;
    private const float updateInterval = 0.25f;

    public LayerMask obstacleMask;

    private bool lightHitSomething = false;
    private RaycastHit2D hitObject;
    private UnityEngine.Rendering.Universal.Light2D light2D = null;

    // Cached values for performance
    private readonly Color debugColorHit = Color.red;
    private readonly Color debugColorMiss = Color.green;
    private Transform lightSourceTransform;

    void Start()
    {
        lightSourceTransform = transform.Find("Light Source");

        lightSourceTransform.gameObject.TryGetComponent(out light2D);

        light2D.color = color;
        light2D.intensity = intensity;
        light2D.volumeIntensity = volumeIntensity;
        light2D.pointLightInnerAngle = innerRadius;
        light2D.pointLightOuterRadius = outerRadius;
    }

    void Update()
    {
        timeSinceLastUpdate += Time.deltaTime;
        if (timeSinceLastUpdate < updateInterval)
            return;

        timeSinceLastUpdate = 0f;

        var transformDirection = transform.TransformDirection(Vector2.up);

        // Perform single raycast instead of RaycastAll for better performance
        hitObject = Physics2D.Raycast(transform.position, transformDirection, range, obstacleMask);
        lightHitSomething = hitObject.collider != null;

        // Determine light distance (based on raycast hit or max range)
        float hitDistance = lightHitSomething ? hitObject.distance : range;

        // Debug visualization
        if (GUILog)
        {
            Color debugColor = lightHitSomething ? debugColorHit : debugColorMiss;
            Debug.DrawRay(transform.position, transformDirection * hitDistance, debugColor);
        }

        // Update light state based on distance
        if (hitDistance < 0.5f)
        {
            light2D.enabled = false;
        }
        else
        {
            light2D.enabled = true;
            light2D.pointLightOuterRadius = hitDistance + 1f;
        }
    }

}