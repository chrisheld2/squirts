using UnityEngine;

public class Buoyancy2D : MonoBehaviour
{
    public float floatForce = 5f;         // upward force to simulate buoyancy
    public float waterDrag = 3f;          // linear drag in water
    public float waterAngularDrag = 2f;  // angular drag in water

    private Rigidbody2D rb;
    private bool inWater = false;

    private float originalLinearDrag;
    private float originalAngularDrag;
    private float waterSurfaceY;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        originalLinearDrag = rb.linearDamping;
        originalAngularDrag = rb.angularDamping;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Water"))
        {
            inWater = true;
            rb.linearDamping = waterDrag;
            rb.angularDamping = waterAngularDrag;

            waterSurfaceY = other.bounds.max.y;

            Debug.Log("Entered water");
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Water"))
        {
            inWater = false;
            rb.linearDamping = originalLinearDrag;
            rb.angularDamping = originalAngularDrag;

            Debug.Log("Exited water");
        }
    }

    void FixedUpdate()
    {
        if (inWater)
        {
            if (transform.position.y < waterSurfaceY)
            {
                float buoyantForce = floatForce * rb.mass;
                rb.AddForce(Vector2.up * buoyantForce);
            }
        }
    }
}
