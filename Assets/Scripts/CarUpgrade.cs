[System.Serializable]
public class CarUpgrade
{
    public int engineLevel = 0;
    public int transmissionLevel = 0;
    //public int brakeLevel = 0;

    public float[] engineSpeedBonus = { 0f, 20f, 40f, 70f };
    public float[] transmissionPowerBonus = { 0f, 30f, 70f, 100f };
    //public float[] brakePowerBonus = { 0f, 500f, 1000f, 1500f };

    public int[] engineUpgradePrices = { 100, 200, 400 };
    public int[] transmissionUpgradePrices = { 150, 300, 600 };
    //public int[] brakeUpgradePrices = { 120, 240, 480 };
}
