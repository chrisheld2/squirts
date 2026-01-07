using UnityEngine;

public class AudioListenerPositioner : MonoBehaviour
{
    public Camera mainCamera;

    private void LateUpdate()
    {
        Vector3 newPosition = mainCamera.transform.position;
        newPosition.z = 0f;
        transform.position = newPosition;
    }
}
