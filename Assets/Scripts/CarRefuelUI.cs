using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class CarRefuelUI : MonoBehaviour
{
    [Header("References")]
    public CarStatus carStatus;
    public TMP_Text priceText10;
    public TMP_Text priceText20;
    public TMP_Text priceText50;
    public TMP_Text priceTextMax;

    [Header("Settings")]
    public int costPerPercent = 1000; // Rp per 1% of fuel

    private void Start()
    {
        UpdateAllPrices();
    }

    public void UpdateAllPrices()
    {
        priceText10.text = GetPriceText(10);
        priceText20.text = GetPriceText(20);
        priceText50.text = GetPriceText(50);
        priceTextMax.text = GetMaxRefuelPriceText();
    }

    private string GetPriceText(int percent)
    {
        if (carStatus == null) return "";

        float missingPercent = 100f - carStatus.GetFuelPercentage();
        float actualRefuel = Mathf.Min(percent, missingPercent);
        int price = Mathf.RoundToInt(actualRefuel * costPerPercent);
        return "Rp" + price.ToString("N0");
    }

    private string GetMaxRefuelPriceText()
    {
        float missingPercent = 100f - carStatus.GetFuelPercentage();
        int price = Mathf.RoundToInt(missingPercent * costPerPercent);
        return "Rp" + price.ToString("N0");
    }

    public void RefuelPercent(int percent)
    {
        if (carStatus == null) return;

        float missingPercent = 100f - carStatus.GetFuelPercentage();
        float actualRefuel = Mathf.Min(percent, missingPercent);
        int price = Mathf.RoundToInt(actualRefuel * costPerPercent);

        if (CurrencyManager.Instance.SpendRupiah(price))
        {
            float fuelToAdd = (actualRefuel / 100f) * carStatus.maxFuel;
            carStatus.Refuel(fuelToAdd);
            UpdateAllPrices();
        }
        else
        {
            Debug.Log("Not enough money!");
        }
    }

    public void RefuelMax()
    {
        float missingPercent = 100f - carStatus.GetFuelPercentage();
        RefuelPercent(Mathf.RoundToInt(missingPercent)); // Will use same logic
    }
}
