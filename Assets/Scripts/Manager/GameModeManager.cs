using UnityEngine;
using System;

public enum GameMode
{
    Simulation,
    Arcade
}

public class GameModeManager : MonoBehaviour
{
    public static GameModeManager Instance { get; private set; }

    public GameMode currentMode { get; private set; }

    [Header("Arcade Mode Settings")]
    public float arcadeStartTime = 60f; // seconds
    public float timePerDropOff = 10f;

    private float arcadeTimer;
    private bool timerRunning = false;

    public event Action<float> OnTimerUpdate;
    public event Action OnArcadeTimeOut;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        if (currentMode == GameMode.Arcade && timerRunning)
        {
            arcadeTimer -= Time.deltaTime;

            OnTimerUpdate?.Invoke(arcadeTimer);

            if (UIManager.Instance != null)
            {
                UIManager.Instance.UpdateArcadeTimer(arcadeTimer);
            }

            if (arcadeTimer <= 0)
            {
                arcadeTimer = 0;
                timerRunning = false;
                OnArcadeTimeOut?.Invoke();
            }
        }
    }

    public void SetGameMode(GameMode mode)
    {
        currentMode = mode;

        if (mode == GameMode.Arcade)
        {
            arcadeTimer = arcadeStartTime;
            timerRunning = true;
        }
        else
        {
            timerRunning = false;
        }
    }

    public void AddArcadeTime(float seconds)
    {
        if (currentMode == GameMode.Arcade)
        {
            arcadeTimer += seconds;
        }
    }

    public float GetRemainingArcadeTime() => arcadeTimer;
}
