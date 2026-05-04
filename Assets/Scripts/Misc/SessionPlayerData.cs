public static class SessionPlayerData
{
    public static string PlayerName { get; private set; } = "Player";

    public static void SetPlayerName(string value)
    {
        PlayerName = string.IsNullOrWhiteSpace(value) ? "Player" : value.Trim();
    }
}