using UnityEngine;

public class RandomDecoration : MonoBehaviour
{
    [Header("RANDOM DEACTIVATION")]
    [SerializeField][Range(0, 100)] private float deactivationChance = 50f;

    private void Awake()
    {
        if (Random.value * 100f <= deactivationChance)
        {
            gameObject.SetActive(false);
        }
    }
}