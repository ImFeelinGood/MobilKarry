using UnityEngine;

public class CarStatus : MonoBehaviour
{
    [Header("Fuel Settings")]
    public float maxFuel = 100f;
    public float currentFuel = 100f;
    public float fuelConsumptionPerSecond = 0.1f;

    [Header("Health Settings")]
    public float maxHealth = 100f;
    public float currentHealth = 100f;

    [Header("Visual Effects")]
    public ParticleSystem lowHealthEffect;
    public bool effectTriggered = false;

    [Header("Other")]
    public bool isBroken => currentHealth <= 0;
    public bool isOutOfFuel => currentFuel <= 0;

    private void Update()
    {
        if (GameModeManager.Instance.currentMode != GameMode.Simulation) return;

        float healthPercent = GetHealthPercentage();

        if (healthPercent <= 30f)
        {
            if (!effectTriggered && lowHealthEffect != null)
            {
                lowHealthEffect.Play();
                effectTriggered = true;
            }
        }
        else
        {
            if (effectTriggered && lowHealthEffect != null && lowHealthEffect.isPlaying)
            {
                lowHealthEffect.Stop();
                effectTriggered = false;
            }
        }
    }

    public void ConsumeFuel(float amount)
    {
        if (GameModeManager.Instance.currentMode != GameMode.Simulation) return;
        currentFuel = Mathf.Max(currentFuel - amount, 0);
    }

    public void Repair(float amount)
    {
        if (GameModeManager.Instance.currentMode != GameMode.Simulation) return;
        currentHealth = Mathf.Min(currentHealth + amount, maxHealth);
    }

    public void TakeDamage(float amount)
    {
        if (GameModeManager.Instance.currentMode != GameMode.Simulation) return;

        currentHealth = Mathf.Max(currentHealth - amount, 0);
        Debug.Log($"[CarStatus] Damage Taken: {amount} | Health Now: {currentHealth}");
    }

    public void Refuel(float amount)
    {
        if (GameModeManager.Instance.currentMode != GameMode.Simulation) return;
        currentFuel = Mathf.Min(currentFuel + amount, maxFuel);
    }

    public float GetFuelPercentage()
    {
        return (currentFuel / maxFuel) * 100f;
    }

    public float GetHealthPercentage()
    {
        return (currentHealth / maxHealth) * 100f;
    }

}
