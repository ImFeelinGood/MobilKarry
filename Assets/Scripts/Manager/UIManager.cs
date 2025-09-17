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

    [Header("Pause Menu")]
    public GameObject pauseMenuUI;

    private bool isPaused = false;

    private void Start()
    {
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
