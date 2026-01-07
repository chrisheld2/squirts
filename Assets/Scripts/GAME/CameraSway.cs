using UnityEngine;
using UnityEngine.Rendering.Universal;

public class CameraSway : MonoBehaviour
{
    #region Variables

    [Header("Target")]
    public Transform target; // Public variable to assign the target GameObject in the inspector
    public Vector3 playerAim;
    public Vector3 offset = new Vector3();

    [Header("Sway")]
    public float smoothTime = 11f; // Adjust for desired smoothness
    private Vector3 velocity; // Used for SmoothDamp




    #endregion

    #region Unity Methods


    void Update()
    {
        if (target == null) return;

        Vector3 targetPos = target.position + playerAim + offset;
        Vector3 cameraPos = transform.position;

        // SmoothDamp towards target position with smoothTime
        cameraPos = Vector3.SmoothDamp(cameraPos, targetPos, ref velocity, smoothTime);

        // Only update X and Y for 2D camera (optional)
        // cameraPos.z = transform.position.z; // Maintain current Z position

        transform.position = cameraPos;
    }

    #endregion

}
