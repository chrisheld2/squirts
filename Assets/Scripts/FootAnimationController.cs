using UnityEngine;

public class FootAnimationController : MonoBehaviour
{
    private Animation animation;
    private bool isTouchingObject = false;

    void Start()
    {
        animation = GetComponent<Animation>();
    }

    void Update()
    {
        // if (!isTouchingObject)
        // {

        //     animation.enabled = true;
        // }
        // else
        // {
        //     animation.enabled = false;
        // }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        // Check if the player is touching a specific object (optional)
        // For example, check tag:
        // if (collision.gameObject.CompareTag("Obstacle"))
        // {
        isTouchingObject = true;
        // }
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        // Reset when no longer touching the object
        isTouchingObject = false;
    }
}
