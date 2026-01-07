using UnityEngine;

public class DoorClose : MonoBehaviour
{
    public float lerpSpeed = 5f; // Speed of the rotation interpolation

    private void Update()
    {
        // Get the current rotation of the door
        Vector3 currentRotation = transform.rotation.eulerAngles;

        // Lerp the Z rotation towards zero
        float newZRotation = Mathf.LerpAngle(currentRotation.z, 0f, Time.deltaTime * lerpSpeed);

        // Set the new rotation while keeping the X and Y the same
        transform.rotation = Quaternion.Euler(currentRotation.x, currentRotation.y, newZRotation);
    }
}
