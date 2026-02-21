using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuUI : MonoBehaviour
{
    [Header("Scene Names")]
    [Tooltip("Name of the scene to load for gameplay")]
    public string gameplaySceneName = "Gameplay";

    public void PlaySimulation()
    {
        SceneManager.LoadScene(gameplaySceneName);
    }

    public void ShowLeaderboard()
    {
        LeaderboardManager.Instance.ShowLeaderboard();
    }

    public void QuitGame()
    {
        Application.Quit();
    }
}
