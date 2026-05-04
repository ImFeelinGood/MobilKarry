public static class FinishMenuData
{
    public static SessionResultData LastResult { get; private set; }

    public static void SetResult(SessionResultData result)
    {
        LastResult = result;
    }
}