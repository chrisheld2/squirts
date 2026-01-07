using UnityEngine;

public class Fold : MonoBehaviour
{
    private Animator animator;

    void Start()
    {
        animator = GetComponent<Animator>();

    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.isTrigger) return;

        animator.Play("FoldDown");
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other.isTrigger) return;

        animator.Play("FoldUp");

    }
}
