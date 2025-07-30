using UnityEngine;

public class CarDamageTest : MonoBehaviour
{
    [Header("Damage Settings")]
    public float damageMultiplier = 2f;
    public float impactThreshold = 5f;
    public float damageCooldown = 0.5f; // seconds

    public CarStatus carStatus;

    private float lastDamageTime = -999f;

    private void OnCollisionEnter(Collision collision)
    {
        float impactForce = collision.relativeVelocity.magnitude;

        // Prevent multiple hits too quickly
        if (Time.time - lastDamageTime < damageCooldown)
            return;

        if (impactForce > impactThreshold && carStatus != null)
        {
            float damage = impactForce * damageMultiplier;
            carStatus.TakeDamage(damage);
            lastDamageTime = Time.time;
        }
    }
}
