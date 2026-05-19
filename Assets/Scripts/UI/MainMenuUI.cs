using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuUI : MonoBehaviour
{
    [Header("Scene Names")]
    [Tooltip("Name of the scene to load for gameplay")]
    public string gameplaySceneName = "Gameplay";

    [Header("Player Input")]
    [SerializeField] private TMP_InputField playerNameInput;
    [SerializeField] private Button playButton;
    [SerializeField] private Button freeModeButton;

    private void Start()
    {
        UpdatePlayButtonState();

        if (playerNameInput != null)
            playerNameInput.onValueChanged.AddListener(OnNameChanged);
    }

    private void OnDestroy()
    {
        if (playerNameInput != null)
            playerNameInput.onValueChanged.RemoveListener(OnNameChanged);
    }

    private void OnNameChanged(string value)
    {
        UpdatePlayButtonState();
    }

    public void PlaySimulation()
    {
        SessionModeData.SetTaskMode();
        SessionPlayerData.SetPlayerName(GetPlayerName("Player"));
        SceneManager.LoadScene(gameplaySceneName);
    }

    public void PlayFreeMode()
    {
        SessionModeData.SetFreeMode();
        SessionPlayerData.SetPlayerName(GetPlayerName("Free Player"));
        SceneManager.LoadScene(gameplaySceneName);
    }

    private string GetPlayerName(string fallbackName)
    {
        if (playerNameInput != null && !string.IsNullOrWhiteSpace(playerNameInput.text))
            return playerNameInput.text.Trim();

        return fallbackName;
    }

    private void UpdatePlayButtonState()
    {
        bool hasText = playerNameInput != null && !string.IsNullOrWhiteSpace(playerNameInput.text);

        // Normal task mode needs a name because it records result data.
        if (playButton != null)
            playButton.interactable = hasText;

        // Free Mode does not record results, so the name can be optional.
        if (freeModeButton != null)
            freeModeButton.interactable = true;
    }

    public void QuitGame()
    {
        Application.Quit();
    }
}
