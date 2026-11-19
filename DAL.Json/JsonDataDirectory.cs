namespace DAL.Json;

public static class JsonDataDirectory
{
    public static string Default =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".gomoku");
}
