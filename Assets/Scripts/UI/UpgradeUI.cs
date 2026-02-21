using TMPro;
using UnityEngine;

public class UpgradeUI : MonoBehaviour
{
    public CarUpgradeSystem upgradeSystem;

    [Header("Engine UI")]
    public TMP_Text engineLevelText;
    public TMP_Text enginePriceText;

    [Header("Transmission UI")]
    public TMP_Text transmissionLevelText;
    public TMP_Text transmissionPriceText;

    //[Header("Brakes UI")]
    //public TMP_Text brakeLevelText;
    //public TMP_Text brakePriceText;

    public void RefreshUI()
    {
        // ===== ENGINE =====
        int engineLvl = upgradeSystem.GetCurrentEngineLevel();
        var nextEnginePrice = upgradeSystem.GetNextEnginePrice();
        engineLevelText.text = $"Lv. {engineLvl}";
        enginePriceText.text = nextEnginePrice.HasValue ? $"{nextEnginePrice.Value} Rp" : "MAX";

        // ===== TRANSMISSION =====
        int transLvl = upgradeSystem.GetCurrentTransmissionLevel();
        var nextTransPrice = upgradeSystem.GetNextTransmissionPrice();
        transmissionLevelText.text = $"Lv. {transLvl}";
        transmissionPriceText.text = nextTransPrice.HasValue ? $"{nextTransPrice.Value} Rp" : "MAX";

        /*
        // ===== BRAKES =====
        int brakeLvl = upgradeSystem.GetCurrentBrakeLevel();
        var nextBrakePrice = upgradeSystem.GetNextBrakePrice();
        brakeLevelText.text = $"Lv. {brakeLvl}";
        brakePriceText.text = nextBrakePrice.HasValue ? $"{nextBrakePrice.Value} Rp" : "MAX";
        */
    }
}
