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

    private void Start()
    {
        // button disabled at start
        UpdatePlayButtonState();

        // listen when input changes
        playerNameInput.onValueChanged.AddListener(OnNameChanged);
    }

    private void OnDestroy()
    {
        playerNameInput.onValueChanged.RemoveListener(OnNameChanged);
    }

    private void OnNameChanged(string value)
    {
        UpdatePlayButtonState();
    }

    public void PlaySimulation()
    {
        string playerName = "Player";

        if (playerNameInput != null && !string.IsNullOrWhiteSpace(playerNameInput.text))
            playerName = playerNameInput.text.Trim();

        SessionPlayerData.SetPlayerName(playerName);
        SceneManager.LoadScene(gameplaySceneName);
    }

    private void UpdatePlayButtonState()
    {
        bool hasText = !string.IsNullOrWhiteSpace(playerNameInput.text);
        playButton.interactable = hasText;
    }

    public void QuitGame()
    {
        Application.Quit();
    }
}