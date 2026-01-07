using UnityEngine;

public class RopeGenerator : MonoBehaviour
{
    [SerializeField] private GameObject ropeSegmentPrefab;
    [SerializeField] private int segmentCount = 10;
    [SerializeField] private float segmentSpacing = 0.2f;

    // Add a reference to the ball's Rigidbody2D
    [SerializeField] private Rigidbody2D parentRigidBody;

    private void Start()
    {
        GenerateRope();
    }

    public void GenerateRope()
    {
        // Destroy existing rope segments
        foreach (Transform child in transform)
        {
            Destroy(child.gameObject);
        }

        GameObject previousSegment = null;
        for (int i = 0; i < segmentCount; i++)
        {
            // Create each rope segment
            GameObject currentSegment = Instantiate(
                ropeSegmentPrefab,
                transform.position + Vector3.down * (i * segmentSpacing),
                Quaternion.identity,
                transform);

            currentSegment.TryGetComponent(out HingeJoint2D hingeJoint);

            if (hingeJoint != null)
            {
                if (previousSegment == null)
                {
                    // First segment connects to the ball
                    hingeJoint.connectedBody = parentRigidBody;
                }
                else
                {
                    // Other segments connect to the previous segment
                    if (previousSegment.TryGetComponent(out Rigidbody2D previousRb))
                        hingeJoint.connectedBody = previousRb;
                }
            }

            previousSegment = currentSegment;
        }
    }

    public int SegmentCount
    {
        get => segmentCount;
        set
        {
            segmentCount = Mathf.Clamp(value, 1, 100);
            GenerateRope();
        }
    }

    public void ResetRope()
    {
        GenerateRope();
    }
}
