namespace AIReviewer.Reviewer.Utils;

public static class Logging
{
    public static string HashSha256(string input)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(input);
        return Convert.ToHexString(sha.ComputeHash(bytes));
    }
}
