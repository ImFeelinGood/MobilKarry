using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class FinishMenuUI : MonoBehaviour
{
    [Header("Texts")]
    [SerializeField] private TMP_Text playerNameText;
    [SerializeField] private TMP_Text endReasonText;
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private TMP_Text currencyText;
    [SerializeField] private TMP_Text tripText;
    [SerializeField] private TMP_Text crashText;
    [SerializeField] private TMP_Text closeFreeCameraText;
    [SerializeField] private TMP_Text farFreeCameraText;
    [SerializeField] private TMP_Text wheelCameraText;
    [SerializeField] private TMP_Text cockpitCameraText;
    [SerializeField] private TMP_Text lockedCameraText;
    [SerializeField] private TMP_Text topDownCameraText;

    [Header("Scene Names")]
    [SerializeField] private string mainMenuSceneName = "Menu";

    private void Start()
    {
        var result = FinishMenuData.LastResult;
        if (result == null) return;

        if (playerNameText != null)
            playerNameText.text = $"Player: {result.playerName}";

        if (endReasonText != null)
            endReasonText.text = $"Result: {result.endReason}";

        if (timerText != null)
            timerText.text = $"Time: {FormatTime(result.elapsedTimeSeconds)}";

        if (currencyText != null)
            currencyText.text = $"Currency: Rp {result.collectedCurrency:N0}";

        if (tripText != null)
            tripText.text = $"Passenger Trips: {result.completedTrips}";

        if (crashText != null)
            crashText.text = $"Crashes: {result.crashCount}";

        if (closeFreeCameraText != null)
            closeFreeCameraText.text = $"Close Free Camera: {result.closeFreeCameraTime}";

        if (farFreeCameraText != null)
            farFreeCameraText.text = $"Far Free Camera: {result.farFreeCameraTime}";

        if (wheelCameraText != null)
            wheelCameraText.text = $"Wheel Camera: {result.wheelCameraTime}";

        if (cockpitCameraText != null)
            cockpitCameraText.text = $"Cockpit Camera: {result.cockpitCameraTime}";

        if (lockedCameraText != null)
            lockedCameraText.text = $"Locked Camera: {result.lockedCameraTime}";

        if (topDownCameraText != null)
            topDownCameraText.text = $"Top Down Camera: {result.topDownCameraTime}";
    }

    public void BackToMainMenu()
    {
        SceneManager.LoadScene(mainMenuSceneName);
    }

    private static string FormatTime(float seconds)
    {
        if (seconds < 0f) seconds = 0f;
        int total = Mathf.FloorToInt(seconds);

        int h = total / 3600;
        int m = (total % 3600) / 60;
        int s = total % 60;

        return (h > 0) ? $"{h:00}:{m:00}:{s:00}" : $"{m:00}:{s:00}";
    }
}