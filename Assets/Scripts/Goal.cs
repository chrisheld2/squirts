using System;
using UnityEngine;

public class Goal : MonoBehaviour
{
    private BoxCollider2D boxCollider;
    private ParticleSystem fireworks;
    private AudioSource audioSource;
    private TMPro.TextMeshPro floatingTextWin;

    [SerializeField]
    public int score = 0;

    void Start()
    {
        fireworks = transform.Find("Shotgun")?.GetComponent<ParticleSystem>();
        audioSource = GetComponent<AudioSource>();
        floatingTextWin = transform.Find("FloatingText")?.GetComponent<TMPro.TextMeshPro>();
    }

    void Update()
    {
        // Delay every 333 milliseconds
        if (Time.frameCount % 20 == 0) return;

        if (floatingTextWin != null)
            floatingTextWin.text = score.ToString();
    }

    void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.name == "Ball")
        {
            ScoreEffects();

            // Check if this is the live ball using USM
            USMNode ballNode = collision.GetComponent<USMNode>();
            if (ballNode != null && ballNode.LiveNode)
            {
                score++;
                // TODO: Implement goal scoring logic
                // Soccer.Instance.OnGoalScored(score, collision.gameObject);
            }
        }
    }
    void OnDrawGizmos()
    {

        if (boxCollider == null)
            TryGetComponent(out boxCollider);


        Gizmos.color = new Color(1f, 0.5f, 0f, .10f);
        Gizmos.DrawCube(transform.position + (Vector3)boxCollider.offset, boxCollider.size);


    }


    public void ScoreEffects()
    {
        if (fireworks != null)
        {
            if (fireworks.isPlaying)
                fireworks.Stop();
            
            fireworks.Play();
        }
        
        if (audioSource != null)
            audioSource.Play();
    }



}
