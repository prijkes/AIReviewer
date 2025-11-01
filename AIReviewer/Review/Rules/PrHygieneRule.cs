namespace AIReviewer.Reviewer.Review.Rules;

public static class PrHygieneRule
{
    public static bool NeedsAttention(string title, string description, IReadOnlyList<string> commits)
    {
        return string.IsNullOrWhiteSpace(title) ||
               string.IsNullOrWhiteSpace(description) ||
               commits.All(string.IsNullOrWhiteSpace);
    }
}
