using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class LeaderboardManager : MonoBehaviour
{
    public static LeaderboardManager Instance { get; private set; }

    private const string leaderboardKey = "LeaderboardScores";
    private const int maxEntries = 10;

    [Header("UI")]
    public GameObject leaderboardScreen;
    public TMP_Text leaderboardText;

    private List<int> scores = new List<int>();

    public void SetupUI(GameObject screen, TMP_Text text)
    {
        leaderboardScreen = screen;
        leaderboardText = text;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject); // ✅ Stays across scenes
    }

    public void ShowLeaderboard()
    {
        leaderboardScreen.SetActive(true);
        UpdateLeaderboardText();
    }

    public void HideLeaderboard()
    {
        leaderboardScreen.SetActive(false);
    }

    public void UpdateLeaderboardText()
    {
        var scores = GetTopScores();
        leaderboardText.text = "";

        for (int i = 0; i < scores.Count; i++)
        {
            leaderboardText.text += $"{i + 1}. Rp. {scores[i]:N0}\n";
        }
    }

    public void SubmitScore(int score)
    {
        scores.Add(score);
        scores.Sort((a, b) => b.CompareTo(a)); // Descending
        if (scores.Count > maxEntries)
            scores.RemoveAt(scores.Count - 1);

        SaveScores();
    }

    public List<int> GetTopScores()
    {
        return new List<int>(scores);
    }

    private void SaveScores()
    {
        string saveString = string.Join(",", scores);
        PlayerPrefs.SetString(leaderboardKey, saveString);
        PlayerPrefs.Save();
    }

    private void LoadScores()
    {
        scores.Clear();
        string saved = PlayerPrefs.GetString(leaderboardKey, "");
        if (!string.IsNullOrEmpty(saved))
        {
            string[] split = saved.Split(',');
            foreach (string s in split)
            {
                if (int.TryParse(s, out int value))
                    scores.Add(value);
            }
        }
    }
}
