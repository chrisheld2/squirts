using UnityEngine;

public class SmoothCameraZoom : MonoBehaviour
{
    private Camera camera;

    [SerializeField]
    private float zoomSpeed = 2f;
    [SerializeField]
    private float minZoom = 2f;
    [SerializeField]
    private float maxZoom = 10f;
    [SerializeField]
    private float smoothTime = 0.3f; // Smoothing time for lerping
    [SerializeField]
    private float targetZoom = 5f; // Initial orthographic size
    private float currentZoom; // Current zoom for smooth interpolation

    void Start()
    {
        camera = Camera.main;
        currentZoom = targetZoom;
        if (camera != null)
        {
            camera.orthographicSize = currentZoom;
        }
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
            targetZoom -= scrollInput * zoomSpeed;
            targetZoom = Mathf.Clamp(targetZoom, minZoom, maxZoom);
        }
    }

    void SmoothZoom()
    {
        // Smoothly interpolate current zoom towards target zoom
        currentZoom = Mathf.Lerp(currentZoom, targetZoom, Time.deltaTime * smoothTime);

        // Update camera orthographic size
        if (camera != null)
        {
            camera.orthographicSize = currentZoom;
        }
    }
}