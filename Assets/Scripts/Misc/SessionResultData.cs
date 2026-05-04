[System.Serializable]
public class SessionResultData
{
    public string playerName;
    public string endReason;
    public float elapsedTimeSeconds;
    public int collectedCurrency;
    public int completedTrips;
    public int crashCount;
    public float totalCameraUsageSeconds;

    public string closeFreeCameraTime;
    public string farFreeCameraTime;
    public string wheelCameraTime;
    public string cockpitCameraTime;
    public string lockedCameraTime;
    public string topDownCameraTime;
}