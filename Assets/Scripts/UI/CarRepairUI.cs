using UnityEngine;
using TMPro;

public class CarRepairUI : MonoBehaviour
{
    [Header("References")]
    public CarStatus carStatus;
    public TMP_Text priceText10;
    public TMP_Text priceText20;
    public TMP_Text priceText50;
    public TMP_Text priceTextMax;

    [Header("Settings")]
    public int costPerPercent = 2000; // Cost per 1% of health

    private void OnEnable()
    {
        UpdateAllPrices();
    }

    public void UpdateAllPrices()
    {
        priceText10.text = GetPriceText(10);
        priceText20.text = GetPriceText(20);
        priceText50.text = GetPriceText(50);
        priceTextMax.text = GetMaxRepairPriceText();
    }

    private string GetPriceText(int percent)
    {
        if (carStatus == null) return "";

        float missingPercent = 100f - carStatus.GetHealthPercentage();
        float actualRepair = Mathf.Min(percent, missingPercent);
        int price = Mathf.RoundToInt(actualRepair * costPerPercent);
        return "Rp" + price.ToString("N0");
    }

    private string GetMaxRepairPriceText()
    {
        float missingPercent = 100f - carStatus.GetHealthPercentage();
        int price = Mathf.RoundToInt(missingPercent * costPerPercent);
        return "Rp" + price.ToString("N0");
    }

    public void RepairPercent(int percent)
    {
        if (carStatus == null) return;

        float missingPercent = 100f - carStatus.GetHealthPercentage();
        float actualRepair = Mathf.Min(percent, missingPercent);
        int price = Mathf.RoundToInt(actualRepair * costPerPercent);

        if (CurrencyManager.Instance.SpendRupiah(price))
        {
            float healthToAdd = (actualRepair / 100f) * carStatus.maxHealth;
            carStatus.Repair(healthToAdd);
            UpdateAllPrices();
        }
        else
        {
            Debug.Log("Not enough money to repair!");
        }
    }

    public void RepairMax()
    {
        float missingPercent = 100f - carStatus.GetHealthPercentage();
        RepairPercent(Mathf.RoundToInt(missingPercent));
    }
}
