using UnityEngine;

/// <summary>
/// Attach this to the ExplosionWithSparks prefab to debug particle collision issues
/// </summary>
public class ParticleCollisionDebugger : MonoBehaviour
{
    void Start()
    {
        // Get all particle systems on this object and children
        ParticleSystem[] particleSystems = GetComponentsInChildren<ParticleSystem>();

        Debug.Log($"[ParticleDebugger] Found {particleSystems.Length} particle systems on {gameObject.name}");

        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem ps = particleSystems[i];
            var collision = ps.collision;
            var main = ps.main;

            Debug.Log($"[ParticleDebugger] ParticleSystem #{i}: {ps.gameObject.name}");
            Debug.Log($"  - Tag: {ps.gameObject.tag}, Layer: {ps.gameObject.layer} ({LayerMask.LayerToName(ps.gameObject.layer)})");
            Debug.Log($"  - Max Particles: {main.maxParticles}, Start Lifetime: {main.startLifetime.constant}");
            Debug.Log($"  - Collision Enabled: {collision.enabled}");

            if (collision.enabled)
            {
                Debug.Log($"  - Collision Type: {collision.type}");
                Debug.Log($"  - Collision Mode: {collision.mode}");
                Debug.Log($"  - Send Collision Messages: {collision.sendCollisionMessages}");
                Debug.Log($"  - Collides With Layers: {collision.collidesWith.value}");
                Debug.Log($"  - Dampen: {collision.dampenMultiplier}");
                Debug.Log($"  - Bounce: {collision.bounceMultiplier}");
            }
        }
    }
}
