using UnityEngine;

public class RandomDecoration : MonoBehaviour
{
    [Header("RANDOM DEACTIVATION")]
    [SerializeField][Range(0, 100)] private float deactivationChance = 50f;

    public void Start()
    {
        float randomValue = Random.Range(0f, 100f);

        if (randomValue <= deactivationChance)
        {
            gameObject.SetActive(false);
        }
    }
}