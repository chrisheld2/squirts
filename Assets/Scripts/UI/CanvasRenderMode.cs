using UnityEngine;

public class CanvasRenderMode : MonoBehaviour
{
    private Canvas mainCanvas; // Canvas attached to the same GameObject

    void Awake()
    {
        mainCanvas = GetComponent<Canvas>(); // Get the Canvas component attached to this GameObject
    }

    // Public function to toggle the render mode
    public void RenderModeSet(int newRenderMode)
    {
        if (newRenderMode == (int)RenderMode.ScreenSpaceCamera)
        {
            mainCanvas.renderMode = RenderMode.ScreenSpaceCamera;
            mainCanvas.worldCamera = Camera.main; // Set the main camera
        }
        else if (newRenderMode == (int)RenderMode.ScreenSpaceOverlay)
        {
            mainCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        }
    }
}
