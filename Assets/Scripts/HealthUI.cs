using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class HealthUI : MonoBehaviour
{
    [Header("References")]
    public CarStatus carStatus;
    public Slider healthSlider;
    public TMP_Text healthText;

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

        float healthPercent = carStatus.currentHealth / carStatus.maxHealth;
        healthSlider.value = healthPercent;

        healthText.text = Mathf.RoundToInt(healthPercent * 100f) + "%";
    }
}
