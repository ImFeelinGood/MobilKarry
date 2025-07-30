using UnityEngine;
using UnityEngine.Events;

public class CurrencyManager : MonoBehaviour
{
    public static CurrencyManager Instance;

    [Header("Currency Settings")]
    public int currentRupiah = 0;

    [Header("Events")]
    public UnityEvent<int> onCurrencyChanged; // Optional for UI updates

    private void Awake()
    {
        if (Instance != null && Instance != this)
            Destroy(this);
        else
            Instance = this;
    }

    public void AddRupiah(int amount)
    {
        currentRupiah += Mathf.Max(0, amount);
        onCurrencyChanged?.Invoke(currentRupiah);
    }

    public bool SpendRupiah(int amount)
    {
        if (currentRupiah >= amount)
        {
            currentRupiah -= amount;
            onCurrencyChanged?.Invoke(currentRupiah);
            return true;
        }

        Debug.Log("Not enough Rupiah!");
        return false;
    }

    public int GetRupiah()
    {
        return currentRupiah;
    }
}
