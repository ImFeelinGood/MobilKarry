using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance;

    [Header("Input")]
    public InputActionAsset inputActions;
    private InputAction pauseAction;

    [Header("Arcade Timer UI")]
    public TMP_Text arcadeTimerText;

    [Header("Result UI")]
    public GameObject resultScreen;
    public TMP_Text finalScoreText;

    [Header("Pause Menu")]
    public GameObject pauseMenuUI;

    private bool isPaused = false;

    private void Start()
    {
        if (GameModeManager.Instance.currentMode == GameMode.Simulation && arcadeTimerText != null)
            arcadeTimerText.gameObject.SetActive(false);

        pauseAction = inputActions.FindActionMap("Car").FindAction("Pause");
        pauseAction.performed += ctx => TogglePause();
        pauseAction.Enable();
    }

    private void OnDestroy()
    {
        if (pauseAction != null)
            pauseAction.performed -= ctx => TogglePause();
    }

    private void Awake()
    {
        Instance = this;
    }

    public void ShowArcadeResult(int score)
    {
        resultScreen.SetActive(true);
        finalScoreText.text = $"Rp. {score:N0}";
        Time.timeScale = 0f;
    }

    public void HideArcadeResult()
    {
        resultScreen.SetActive(false);
    }

    public void UpdateArcadeTimer(float timeRemaining)
    {
        if (arcadeTimerText == null) return;

        int minutes = Mathf.FloorToInt(timeRemaining / 60f);
        int seconds = Mathf.FloorToInt(timeRemaining % 60f);
        arcadeTimerText.text = $"{minutes:00}:{seconds:00}";
        arcadeTimerText.color = timeRemaining < 10f ? Color.red : Color.white;
    }

    public void ReturnToMainMenu()
    {
        SceneManager.LoadScene("Menu");
        Time.timeScale = 1f;
    }

    public void TogglePause()
    {
        if (isPaused)
            ResumeGame();
        else
            PauseGame();
    }

    public void PauseGame()
    {
        if (pauseMenuUI != null)
            pauseMenuUI.SetActive(true);

        Time.timeScale = 0f;
        isPaused = true;
    }

    public void ResumeGame()
    {
        if (pauseMenuUI != null)
            pauseMenuUI.SetActive(false);

        Time.timeScale = 1f;
        isPaused = false;
    }
}
