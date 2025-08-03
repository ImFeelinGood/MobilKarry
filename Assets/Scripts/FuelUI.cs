using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class FuelUI : MonoBehaviour
{
    [Header("References")]
    public CarStatus carStatus;
    public Slider fuelSlider;
    public TMP_Text fuelPercentageText;

    private void Start()
    {
        if (GameModeManager.Instance.currentMode == GameMode.Arcade)
        {
            gameObject.SetActive(false);
        }
    }

    private void Update()
    {
        if (carStatus == null) return;

        float fuelPercent = carStatus.currentFuel / carStatus.maxFuel;
        fuelSlider.value = fuelPercent;

        fuelPercentageText.text = Mathf.RoundToInt(fuelPercent * 100f) + "%";
    }
}
