
using UnityEngine;

public class BlockEnd : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            Debug.Log("Player has entered the block end trigger.");
        }
    }
}
