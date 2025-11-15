namespace Quaally.AzureDevOps.Models;

public sealed record ThreadFingerprint(
    string Fingerprint,
    string FilePath,
    int? Line,
    string IssueId,
    int Iteration);
