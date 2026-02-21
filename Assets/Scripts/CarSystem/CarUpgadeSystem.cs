using UnityEngine;

public class CarUpgradeSystem : MonoBehaviour
{
    public CarUpgrade upgrades = new CarUpgrade();
    private Ezereal.EzerealCarController car;

    private void Awake()
    {
        car = GetComponent<Ezereal.EzerealCarController>();
        ApplyUpgrades();
    }

    // ==========================
    // UPGRADE FUNCTIONS
    // ==========================
    public void UpgradeEngine()
    {
        if (upgrades.engineLevel < upgrades.engineSpeedBonus.Length - 1)
        {
            int price = upgrades.engineUpgradePrices[upgrades.engineLevel];
            if (CurrencyManager.Instance.SpendRupiah(price))
            {
                upgrades.engineLevel++;
                ApplyUpgrades();
            }
        }
    }

    public void UpgradeTransmission()
    {
        if (upgrades.transmissionLevel < upgrades.transmissionPowerBonus.Length - 1)
        {
            int price = upgrades.transmissionUpgradePrices[upgrades.transmissionLevel];
            if (CurrencyManager.Instance.SpendRupiah(price))
            {
                upgrades.transmissionLevel++;
                ApplyUpgrades();
            }
        }
    }

    /*
    public void UpgradeBrakes()
    {
        if (upgrades.brakeLevel < upgrades.brakePowerBonus.Length - 1)
        {
            int price = upgrades.brakeUpgradePrices[upgrades.brakeLevel];
            if (CurrencyManager.Instance.SpendRupiah(price))
            {
                upgrades.brakeLevel++;
                ApplyUpgrades();
            }
        }
    }
    */

    private void ApplyUpgrades()
    {
        car.maxForwardSpeed = car.defaultmaxForwardSpeed + upgrades.engineSpeedBonus[upgrades.engineLevel];
        car.horsePower = car.defaulthorsePower + upgrades.transmissionPowerBonus[upgrades.transmissionLevel];
        //car.brakePower = 2000f + upgrades.brakePowerBonus[upgrades.brakeLevel];
    }

    // ==========================
    // INFO METHODS FOR UI
    // ==========================
    public int GetCurrentEngineLevel() => upgrades.engineLevel;
    public int GetCurrentTransmissionLevel() => upgrades.transmissionLevel;
    //public int GetCurrentBrakeLevel() => upgrades.brakeLevel;

    public int? GetNextEnginePrice()
    {
        if (upgrades.engineLevel < upgrades.engineUpgradePrices.Length)
            return upgrades.engineUpgradePrices[upgrades.engineLevel];
        return null; // Max level
    }

    public int? GetNextTransmissionPrice()
    {
        if (upgrades.transmissionLevel < upgrades.transmissionUpgradePrices.Length)
            return upgrades.transmissionUpgradePrices[upgrades.transmissionLevel];
        return null; // Max level
    }

    /*
    public int? GetNextBrakePrice()
    {
        if (upgrades.brakeLevel < upgrades.brakeUpgradePrices.Length)
            return upgrades.brakeUpgradePrices[upgrades.brakeLevel];
        return null; // Max level
    }
    */
}
