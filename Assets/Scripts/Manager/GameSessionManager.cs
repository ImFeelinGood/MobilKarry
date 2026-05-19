using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Ezereal;

public enum SessionPlayMode
{
    TaskMode,
    FreeMode
}

public static class SessionModeData
{
    public static SessionPlayMode CurrentMode { get; private set; } = SessionPlayMode.TaskMode;
    public static bool IsFreeMode => CurrentMode == SessionPlayMode.FreeMode;

    public static void SetTaskMode()
    {
        CurrentMode = SessionPlayMode.TaskMode;
    }

    public static void SetFreeMode()
    {
        CurrentMode = SessionPlayMode.FreeMode;
    }
}

public class GameSessionManager : MonoBehaviour
{
    public static GameSessionManager Instance;

    [Header("Mode Settings")]
    [Tooltip("If true, the scene uses the mode selected from MainMenuUI. If false, it uses Editor Default Mode below.")]
    [SerializeField] private bool useMenuSelectedMode = true;

    [Tooltip("Used only when Use Menu Selected Mode is false. Useful when testing the Gameplay scene directly.")]
    [SerializeField] private SessionPlayMode editorDefaultMode = SessionPlayMode.TaskMode;

    [Tooltip("Hide the timer text while playing Free Mode.")]
    [SerializeField] private bool hideTimerUIInFreeMode = true;

    [Tooltip("Hide the goal/task text while playing Free Mode.")]
    [SerializeField] private bool hideGoalUIInFreeMode = true;

    [Tooltip("Disable CameraUsageUI so camera usage is not tracked in Free Mode.")]
    [SerializeField] private bool disableCameraUsageTrackingInFreeMode = true;

    [Tooltip("Disable passenger/task system in Free Mode.")]
    [SerializeField] private bool disablePassengerSystemInFreeMode = true;

    private bool stopTracking = false;
    private SessionPlayMode currentMode = SessionPlayMode.TaskMode;

    [Header("Result Settings")]
    [SerializeField] private string resultName;

    [Header("Goal Settings")]
    [SerializeField] private int targetCurrency = 50000;
    [SerializeField] private float levelDurationSeconds = 300f;
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    [Header("References")]
    [SerializeField] private PlayerPassengerSystem passengerSystem;
    [SerializeField] private CarStatus carStatus;
    [SerializeField] private CameraUsageUI cameraUsageUI;

    [Header("Gameplay UI")]
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private TMP_Text goalText;

    [Header("Finish Panel UI")]
    [SerializeField] private GameObject finishPanel;
    [SerializeField] private TMP_Text playerNameText;
    [SerializeField] private TMP_Text endReasonText;
    [SerializeField] private TMP_Text finalTimerText;
    [SerializeField] private TMP_Text finalCurrencyText;
    [SerializeField] private TMP_Text finalTripText;
    [SerializeField] private TMP_Text finalCrashText;
    [SerializeField] private TMP_Text finalCloseFreeCameraText;
    [SerializeField] private TMP_Text finalFarFreeCameraText;
    [SerializeField] private TMP_Text finalWheelCameraText;
    [SerializeField] private TMP_Text finalCockpitCameraText;
    [SerializeField] private TMP_Text finalLockedCameraText;
    [SerializeField] private TMP_Text finalTopDownCameraText;
    [SerializeField] private Button continueButton;
    [SerializeField] private Button backToMenuButton;

    private float elapsedTime = 0f;
    private bool sessionEnded = false;
    private bool hasUpgradedCar = false;
    private int upgradeCount = 0;
    private SessionResultData lastResult;

    public bool IsFreeMode => currentMode == SessionPlayMode.FreeMode;
    public bool IsTaskMode => currentMode == SessionPlayMode.TaskMode;
    public float ElapsedTime => elapsedTime;
    public float RemainingTime => IsFreeMode ? 0f : Mathf.Max(0f, levelDurationSeconds - elapsedTime);
    public bool HasUpgradedCar => hasUpgradedCar;
    public int UpgradeCount => upgradeCount;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        currentMode = useMenuSelectedMode ? SessionModeData.CurrentMode : editorDefaultMode;
    }

    private void Start()
    {
        if (finishPanel != null)
            finishPanel.SetActive(false);

        Time.timeScale = 1f;

        if (IsFreeMode)
            StartFreeMode();
        else
            StartTaskMode();
    }

    private void Update()
    {
        if (sessionEnded || stopTracking || IsFreeMode) return;

        elapsedTime += Time.deltaTime;
        UpdateUI();

        bool currencyGoalReached = CurrencyManager.Instance != null &&
                                   CurrencyManager.Instance.GetRupiah() >= targetCurrency;

        if (hasUpgradedCar && currencyGoalReached)
        {
            EndSession("All Goals Reached");
            return;
        }

        if (elapsedTime >= levelDurationSeconds)
        {
            EndSession("Time Up");
        }
    }

    private void StartTaskMode()
    {
        stopTracking = false;
        sessionEnded = false;
        elapsedTime = 0f;
        UpdateUI();
    }

    private void StartFreeMode()
    {
        stopTracking = true;
        sessionEnded = false;
        elapsedTime = 0f;

        if (timerText != null)
        {
            if (hideTimerUIInFreeMode)
                timerText.gameObject.SetActive(false);
            else
                timerText.text = "Free Mode";
        }

        if (goalText != null)
        {
            if (hideGoalUIInFreeMode)
                goalText.gameObject.SetActive(false);
            else
                goalText.text = "Free Mode\nNo time limit\nNo task";
        }

        if (disableCameraUsageTrackingInFreeMode)
            SetBehaviourEnabled(cameraUsageUI, false);

        if (disablePassengerSystemInFreeMode)
            SetBehaviourEnabled(passengerSystem, false);
    }

    private void UpdateUI()
    {
        if (IsFreeMode) return;

        if (timerText != null)
            timerText.text = $"Time: {FormatTime(RemainingTime)}";

        if (goalText != null)
        {
            int currentCurrency = CurrencyManager.Instance != null ? CurrencyManager.Instance.GetRupiah() : 0;
            string upgradeStatus = hasUpgradedCar ? "Done" : "Not Yet";
            string currencyStatus = $"{currentCurrency:N0} / {targetCurrency:N0}";

            goalText.text =
                $"Goal 1 - Upgrade Car: {upgradeStatus}\n" +
                $"Goal 2 - Currency: Rp {currencyStatus}";
        }
    }

    public void NotifyCarUpgraded()
    {
        if (sessionEnded || IsFreeMode) return;

        upgradeCount++;
        hasUpgradedCar = true;
        UpdateUI();
    }

    public void EndSession(string endReason)
    {
        // Free Mode should never finish automatically and should never export results.
        if (sessionEnded || IsFreeMode) return;

        sessionEnded = true;

        lastResult = BuildResult(endReason);
        ExportResultToCsv(lastResult);
        ShowFinishPanel(lastResult);

        Time.timeScale = 0f;
    }

    private SessionResultData BuildResult(string endReason)
    {
        SessionResultData result = new SessionResultData
        {
            playerName = SessionPlayerData.PlayerName,
            endReason = endReason,
            elapsedTimeSeconds = elapsedTime,
            collectedCurrency = CurrencyManager.Instance != null ? CurrencyManager.Instance.GetRupiah() : 0,
            completedTrips = passengerSystem != null ? passengerSystem.CompletedTrips : 0,
            crashCount = carStatus != null ? carStatus.DamageTakenCount : 0,
        };

        if (cameraUsageUI != null)
        {
            result.closeFreeCameraTime = cameraUsageUI.GetCameraUsageFormattedAt(0);
            result.farFreeCameraTime = cameraUsageUI.GetCameraUsageFormattedAt(1);
            result.wheelCameraTime = cameraUsageUI.GetCameraUsageFormattedAt(2);
            result.cockpitCameraTime = cameraUsageUI.GetCameraUsageFormattedAt(3);
            result.lockedCameraTime = cameraUsageUI.GetCameraUsageFormattedAt(4);
            result.topDownCameraTime = cameraUsageUI.GetCameraUsageFormattedAt(5);
        }
        else
        {
            result.closeFreeCameraTime = "00:00";
            result.farFreeCameraTime = "00:00";
            result.wheelCameraTime = "00:00";
            result.cockpitCameraTime = "00:00";
            result.lockedCameraTime = "00:00";
            result.topDownCameraTime = "00:00";
        }

        return result;
    }

    private void ShowFinishPanel(SessionResultData result)
    {
        if (finishPanel != null)
            finishPanel.SetActive(true);

        if (playerNameText != null)
            playerNameText.text = $"Player: {result.playerName}";

        if (endReasonText != null)
            endReasonText.text = $"Result: {result.endReason}";

        if (finalTimerText != null)
            finalTimerText.text = $"Time: {FormatTime(result.elapsedTimeSeconds)}";

        if (finalCurrencyText != null)
            finalCurrencyText.text = $"Currency: Rp {result.collectedCurrency:N0}";

        if (finalTripText != null)
            finalTripText.text = $"Passenger Trips: {result.completedTrips}";

        if (finalCrashText != null)
            finalCrashText.text = $"Crashes: {result.crashCount}";

        if (finalCloseFreeCameraText != null)
            finalCloseFreeCameraText.text = $"Close Free Camera: {result.closeFreeCameraTime}";

        if (finalFarFreeCameraText != null)
            finalFarFreeCameraText.text = $"Far Free Camera: {result.farFreeCameraTime}";

        if (finalWheelCameraText != null)
            finalWheelCameraText.text = $"Wheel Camera: {result.wheelCameraTime}";

        if (finalCockpitCameraText != null)
            finalCockpitCameraText.text = $"Cockpit Camera: {result.cockpitCameraTime}";

        if (finalLockedCameraText != null)
            finalLockedCameraText.text = $"Locked Camera: {result.lockedCameraTime}";

        if (finalTopDownCameraText != null)
            finalTopDownCameraText.text = $"Top Down Camera: {result.topDownCameraTime}";
    }

    public void ContinueGame()
    {
        if (!sessionEnded) return;

        if (finishPanel != null)
            finishPanel.SetActive(false);

        stopTracking = true;
        Time.timeScale = 1f;

        if (timerText != null)
            timerText.text = "Time: Finished";

        if (goalText != null)
            goalText.text = "Goal: Completed";
    }

    public void BackToMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(mainMenuSceneName);
    }

    private void ExportResultToCsv(SessionResultData result)
    {
        if (IsFreeMode) return;

        string folder = Path.Combine(Application.persistentDataPath, "Results");
        if (!Directory.Exists(folder))
            Directory.CreateDirectory(folder);

        string safeResultName = MakeSafeFileName(resultName);
        string filePath = Path.Combine(folder, $"{safeResultName}.csv");

        const string header =
            "PlayerName,DateTime,EndReason,ElapsedTimeSeconds,ElapsedTimeFormatted,CollectedCurrency,CompletedTrips,CrashCount,CloseFreeCamera,FarFreeCamera,WheelCamera,CockpitCamera,LockedCamera,TopDownCamera";

        string newRow =
            Escape(result.playerName) + "," +
            Escape(System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")) + "," +
            Escape(result.endReason) + "," +
            result.elapsedTimeSeconds.ToString("0.00") + "," +
            Escape(FormatTime(result.elapsedTimeSeconds)) + "," +
            result.collectedCurrency + "," +
            result.completedTrips + "," +
            result.crashCount + "," +
            Escape(result.closeFreeCameraTime) + "," +
            Escape(result.farFreeCameraTime) + "," +
            Escape(result.wheelCameraTime) + "," +
            Escape(result.cockpitCameraTime) + "," +
            Escape(result.lockedCameraTime) + "," +
            Escape(result.topDownCameraTime);

        try
        {
            var outputLines = new System.Collections.Generic.List<string>();
            outputLines.Add(header);

            if (File.Exists(filePath))
            {
                string[] existingLines = File.ReadAllLines(filePath);

                for (int i = 1; i < existingLines.Length; i++)
                {
                    string line = existingLines[i];
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    string existingPlayerName = GetFirstCsvField(line);

                    if (!string.Equals(existingPlayerName, result.playerName, System.StringComparison.OrdinalIgnoreCase))
                    {
                        outputLines.Add(line);
                    }
                }
            }

            outputLines.Add(newRow);

            File.WriteAllLines(filePath, outputLines);
            Debug.Log("CSV saved to: " + filePath);
        }
        catch (IOException e)
        {
            Debug.LogError("Failed to save CSV. Make sure the file is not open in Excel or another program.\n" + e.Message);
        }
    }

    private static void SetBehaviourEnabled(UnityEngine.Object target, bool enabled)
    {
        Behaviour behaviour = target as Behaviour;
        if (behaviour != null)
            behaviour.enabled = enabled;
    }

    // C:/Users/dheda/AppData/LocalLow/DefaultCompany/MobilKarry/Results/

    private static string GetFirstCsvField(string line)
    {
        if (string.IsNullOrEmpty(line))
            return "";

        if (line[0] == '"')
        {
            int endQuote = line.IndexOf("\",");
            if (endQuote >= 0)
                return line.Substring(1, endQuote - 1).Replace("\"\"", "\"");

            if (line.Length >= 2 && line[line.Length - 1] == '"')
                return line.Substring(1, line.Length - 2).Replace("\"\"", "\"");
        }

        int commaIndex = line.IndexOf(',');
        if (commaIndex >= 0)
            return line.Substring(0, commaIndex);

        return line;
    }

    private static string Escape(string value)
    {
        if (string.IsNullOrEmpty(value)) return "\"\"";
        return $"\"{value.Replace("\"", "\"\"")}\"";
    }

    private static string MakeSafeFileName(string fileName)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
            fileName = fileName.Replace(c.ToString(), "_");

        return string.IsNullOrWhiteSpace(fileName) ? "SessionResult" : fileName;
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
