using UnityEngine;

public class ArcadeModeHandler : MonoBehaviour
{
    private void Start()
    {
        if (GameModeManager.Instance.currentMode == GameMode.Arcade)
            CurrencyManager.Instance.currentRupiah = 0;
    }

    private void OnEnable()
    {
        GameModeManager.Instance.OnArcadeTimeOut += HandleArcadeEnd;
    }

    private void OnDisable()
    {
        GameModeManager.Instance.OnArcadeTimeOut -= HandleArcadeEnd;
    }

    void HandleArcadeEnd()
    {
        int finalScore = CurrencyManager.Instance.currentRupiah;
        //LeaderboardManager.Instance.SubmitScore(finalScore);
        UIManager.Instance.ShowArcadeResult(finalScore);
    }
}
