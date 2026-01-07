using UnityEngine;

public class Fog : MonoBehaviour
{
    private const int numRays = 36;
    private RaycastHit2D[] hits;
    private RaycastResult[] raycastResults;

    public float range = 2;
    public LayerMask obstacleMask;

    struct RaycastResult
    {
        public Vector2 angle;
        public float range;
    }

    void Start()
    {
        PrecalculateAngles();
    }

    void Update()
    {
        CheckFogCoverage();
    }

    private void PrecalculateAngles()
    {
        float angleIncrement = 360f / numRays;

        raycastResults = new RaycastResult[numRays];
        for (int i = 0; i < numRays; i++)
        {
            float angle = i * angleIncrement;
            float radianAngle = angle * Mathf.Deg2Rad; // Convert angle to radians

            // Calculate the direction in 2D space (x and y)
            raycastResults[i].angle = new Vector2(Mathf.Cos(radianAngle), Mathf.Sin(radianAngle));

        }
    }


    private void OnDrawGizmos()
    {
        for (int i = 0; i < numRays; i++)
        {
            Gizmos.color = raycastResults[i].range < range ? Color.green : Color.yellow;
            Vector3 rayStart = transform.position + (Vector3)raycastResults[i].angle / 2;
            Gizmos.DrawRay(rayStart, raycastResults[i].angle * raycastResults[i].range);
        }

    }

    private void CheckFogCoverage()
    {
        for (int i = 0; i < numRays; i++)
        {
            var angle = raycastResults[i].angle;
            Vector2 rayStart = (Vector2)transform.position + angle / 2;
            hits = Physics2D.RaycastAll(rayStart, angle, range);

            bool hitWall = false;
            int j = 0;
            Debug.Log(hits.Length);
            foreach (var hit in hits)
            {
                j++;
                // if (hit.collider == null) break;

                if (hit.collider.tag == "Fog")
                {


                    hit.collider.gameObject.SetActive(false);


                    if (hitWall)
                        break;

                }




                if (hit.collider.tag == "BlocksLight")
                {
                    raycastResults[i].range = hit.distance;
                    hitWall = true;

                    // continue;

                    break;
                }
                else
                {
                    raycastResults[i].range = range;
                }

            }


        }

    }
}
