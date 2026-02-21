using UnityEngine;
using TMPro;

public class CurrencyUI : MonoBehaviour
{
    [SerializeField] private TMP_Text rupiahText;

    private void Start()
    {
        // Initialize text at start
        if (CurrencyManager.Instance != null)
        {
            UpdateRupiahText(CurrencyManager.Instance.GetRupiah());

            // Subscribe to currency change event
            CurrencyManager.Instance.onCurrencyChanged.AddListener(UpdateRupiahText);
        }
    }

    public void UpdateRupiahText(int amount)
    {
        rupiahText.text = FormatRupiah(amount);
    }

    private string FormatRupiah(int amount)
    {
        return "Rp" + amount.ToString("N0"); // e.g., Rp125,000
    }
}
