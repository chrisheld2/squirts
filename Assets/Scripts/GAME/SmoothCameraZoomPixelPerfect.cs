using UnityEngine;
using UnityEngine.Rendering.Universal;

public class SmoothCameraZoomPixelPerfect : MonoBehaviour
{
    private Camera camera;
    private PixelPerfectCamera pixelPerfectCamera;

    [SerializeField]
    private float zoomSpeed = 2f;
    [SerializeField]
    private float minZoom = 32f;
    [SerializeField]
    private float maxZoom = 10f;
    [SerializeField]
    private float smoothTime = 0.3f; // Smoothing time for lerping
    [SerializeField]
    private float targetPPU = 16f; // Initial Zoom level
    private float currentPPU; // Current PPU for smooth interpolation

    void Start()
    {
        camera = Camera.main;
        camera.TryGetComponent(out pixelPerfectCamera);
        currentPPU = targetPPU;

    }

    void Update()
    {
        HandleZoomInput();
        SmoothZoom();
    }

    void HandleZoomInput()
    {
        float scrollInput = Input.GetAxis("Mouse ScrollWheel");
        if (scrollInput != 0)
        {
            targetPPU += scrollInput * zoomSpeed;
            targetPPU = Mathf.Clamp(targetPPU, minZoom, maxZoom);
        }
    }

    void SmoothZoom()
    {
        // Smoothly interpolate current PPU towards target PPU
        currentPPU = Mathf.Lerp(currentPPU, targetPPU, Time.deltaTime * smoothTime);

        // Update pixel perfect camera with rounded integer value
        pixelPerfectCamera.assetsPPU = Mathf.RoundToInt(currentPPU);
    }
}