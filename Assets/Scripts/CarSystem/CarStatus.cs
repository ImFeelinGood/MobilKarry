using TMPro;
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

    [Header("Counters")]
    [SerializeField] private int damageTakenCount = 0;        // how many times damage happened
    [SerializeField] private float totalDamageTaken = 0f;     // optional: total damage amount
    [SerializeField] private TMP_Text damageTakenUI;          // drag TMP_Text here in Inspector

    public int DamageTakenCount => damageTakenCount;
    public float TotalDamageTaken => totalDamageTaken;

    [Header("Other")]
    public bool isBroken => currentHealth <= 0;
    public bool isOutOfFuel => currentFuel <= 0;

    private void Start()
    {
        // Optional auto-find (only if you want):
        // if (damageTakenUI == null)
        // {
        //     var go = GameObject.FindWithTag("DamageTakenUI");
        //     if (go != null) damageTakenUI = go.GetComponent<TMP_Text>();
        // }

        UpdateDamageTakenUI();
    }

    private void Update()
    {
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
        currentFuel = Mathf.Max(currentFuel - amount, 0);
    }

    public void Repair(float amount)
    {
        currentHealth = Mathf.Min(currentHealth + amount, maxHealth);
    }

    public void TakeDamage(float amount)
    {
        if (amount <= 0f) return;

        float before = currentHealth;
        currentHealth = Mathf.Max(currentHealth - amount, 0f);

        float actualDamage = before - currentHealth; // clamps if health hits 0
        if (actualDamage > 0f)
        {
            damageTakenCount++;
            totalDamageTaken += actualDamage;
            UpdateDamageTakenUI(); // <- update UI here
        }

        Debug.Log($"[CarStatus] Damage Taken: {actualDamage} | Health Now: {currentHealth} | Hits: {damageTakenCount}");
    }

    public void Refuel(float amount)
    {
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

    public void ResetCounters()
    {
        damageTakenCount = 0;
        totalDamageTaken = 0f;
        UpdateDamageTakenUI(); // <- update UI here too
    }

    private void UpdateDamageTakenUI()
    {
        if (damageTakenUI == null) return;

        // Pick one format:
        damageTakenUI.text = $"Damage Taken: {damageTakenCount}";
        // damageTakenUI.text = $"Hits: {damageTakenCount}\nTotal Damage: {totalDamageTaken:0}";
    }
}
