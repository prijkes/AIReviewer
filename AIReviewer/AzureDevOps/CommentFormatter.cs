using AIReviewer.AzureDevOps.Models;
using AIReviewer.Review;
using AIReviewer.Utils;
using System.Text;

namespace AIReviewer.AzureDevOps;

/// <summary>
/// Formats review issues and state information into Azure DevOps comment markdown.
/// </summary>
public static class CommentFormatter
{
    /// <summary>
    /// Formats a review issue into a markdown comment for display on Azure DevOps.
    /// </summary>
    /// <param name="issue">The issue to format.</param>
    /// <returns>A markdown-formatted comment string.</returns>
    public static string FormatReviewIssue(ReviewIssue issue)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"🤖 AI Review — {issue.Category}/{issue.Severity}");
        builder.AppendLine();
        builder.AppendLine(issue.Rationale);
        builder.AppendLine();
        builder.AppendLine($"**Recommendation**: {issue.Recommendation}");

        if (!string.IsNullOrWhiteSpace(issue.FixExample))
        {
            builder.AppendLine();
            builder.AppendLine("```csharp");
            builder.AppendLine(issue.FixExample);
            builder.AppendLine("```");
        }

        builder.AppendLine();
        builder.AppendLine("_I'm a bot; reply here to discuss. Set `DRY_RUN=true` to preview without posting._");

        return builder.ToString();
    }

    /// <summary>
    /// Formats a re-triggered issue comment.
    /// </summary>
    /// <param name="issue">The re-triggered issue.</param>
    /// <returns>A markdown-formatted comment string for re-triggered issues.</returns>
    public static string FormatReTriggeredIssue(ReviewIssue issue)
    {
        return $"Re-triggered: {FormatReviewIssue(issue)}";
    }

    /// <summary>
    /// Formats the state thread content containing issue fingerprints.
    /// This creates a hidden JSON record of all active issues for tracking.
    /// </summary>
    /// <param name="result">The review plan result containing all issues.</param>
    /// <returns>A markdown-formatted state thread comment.</returns>
    public static string FormatStateThread(ReviewPlanResult result)
    {
        var state = new
        {
            fingerprints = result.Issues.Select(i => new
            {
                i.Fingerprint,
                i.FilePath,
                i.Line,
                i.Severity
            }).ToArray(),
            updatedAt = DateTimeOffset.UtcNow
        };

        return $"<!-- ai-state -->\n```\n{JsonHelpers.Serialize(state)}\n```";
    }
}
