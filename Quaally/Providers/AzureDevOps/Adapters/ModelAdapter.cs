using Quaally.Core.Enums;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.Services.WebApi;
using AdoComment = Microsoft.TeamFoundation.SourceControl.WebApi.Comment;
using AdoPullRequest = Microsoft.TeamFoundation.SourceControl.WebApi.GitPullRequest;
using AdoRepository = Microsoft.TeamFoundation.SourceControl.WebApi.GitRepository;
using CoreComment = Quaally.Core.Models.Comment;
using CorePullRequest = Quaally.Core.Models.PullRequest;
using CoreRepository = Quaally.Core.Models.Repository;
using CoreUserIdentity = Quaally.Core.Models.UserIdentity;
using CoreReviewThread = Quaally.Core.Models.ReviewThread;
using CorePullRequestStatus = Quaally.Core.Enums.PullRequestStatus;
using AdoPullRequestStatus = Microsoft.TeamFoundation.SourceControl.WebApi.PullRequestStatus;

namespace Quaally.Providers.AzureDevOps.Adapters;

/// <summary>
/// Adapts Azure DevOps SDK models to generic Core models.
/// </summary>
public static class ModelAdapter
{
    /// <summary>
    /// Converts Azure DevOps PullRequest to Core PullRequest.
    /// </summary>
    public static CorePullRequest ToPullRequest(AdoPullRequest adoPr)
    {
        return new CorePullRequest
        {
            Id = adoPr.PullRequestId,
            Title = adoPr.Title ?? string.Empty,
            Description = adoPr.Description,
            Status = ToStatus(adoPr.Status),
            SourceBranch = adoPr.SourceRefName?.Replace("refs/heads/", "") ?? string.Empty,
            TargetBranch = adoPr.TargetRefName?.Replace("refs/heads/", "") ?? string.Empty,
            CreatedBy = ToUserIdentity(adoPr.CreatedBy),
            CreatedDate = adoPr.CreationDate,
            Repository = ToRepository(adoPr.Repository),
            LastSourceCommitId = adoPr.LastMergeSourceCommit?.CommitId,
            LastTargetCommitId = adoPr.LastMergeTargetCommit?.CommitId,
            Url = adoPr.Url,
            IsDraft = adoPr.IsDraft ?? false
        };
    }

    /// <summary>
    /// Converts Azure DevOps Repository to Core Repository.
    /// </summary>
    public static CoreRepository ToRepository(AdoRepository adoRepo)
    {
        return new CoreRepository
        {
            Id = adoRepo.Id.ToString(),
            Name = adoRepo.Name ?? string.Empty,
            DefaultBranch = adoRepo.DefaultBranch?.Replace("refs/heads/", ""),
            RemoteUrl = adoRepo.RemoteUrl,
            Project = adoRepo.ProjectReference?.Name
        };
    }

    /// <summary>
    /// Converts Azure DevOps IdentityRef to Core UserIdentity.
    /// </summary>
    public static CoreUserIdentity ToUserIdentity(IdentityRef? identity)
    {
        if (identity == null)
        {
            return new CoreUserIdentity
            {
                Id = "unknown",
                DisplayName = "Unknown User"
            };
        }

        return new CoreUserIdentity
        {
            Id = identity.Id ?? identity.UniqueName ?? "unknown",
            DisplayName = identity.DisplayName ?? identity.UniqueName ?? "Unknown",
            Email = identity.UniqueName?.Contains('@') == true ? identity.UniqueName : null,
            UniqueName = identity.UniqueName,
            AvatarUrl = identity.ImageUrl
        };
    }

    /// <summary>
    /// Converts Azure DevOps Comment to Core Comment.
    /// </summary>
    public static CoreComment ToComment(AdoComment adoComment)
    {
        return new CoreComment
        {
            Id = adoComment.Id,
            Content = adoComment.Content ?? string.Empty,
            Author = ToUserIdentity(adoComment.Author),
            PublishedDate = adoComment.PublishedDate,
            ParentCommentId = adoComment.ParentCommentId > 0 ? adoComment.ParentCommentId : null,
            IsEdited = adoComment.IsDeleted == true, // ADO doesn't have explicit IsEdited
            LastUpdatedDate = adoComment.LastUpdatedDate
        };
    }

    /// <summary>
    /// Converts Azure DevOps CommentThread to Core ReviewThread.
    /// </summary>
    public static CoreReviewThread ToReviewThread(GitPullRequestCommentThread adoThread)
    {
        return new CoreReviewThread
        {
            Id = adoThread.Id,
            Status = ToThreadStatus(adoThread.Status),
            Comments = adoThread.Comments?.Select(ToComment).ToList() ?? new List<CoreComment>(),
            FilePath = adoThread.ThreadContext?.FilePath,
            LineStart = adoThread.ThreadContext?.RightFileStart?.Line,
            LineEnd = adoThread.ThreadContext?.RightFileEnd?.Line,
            CreatedDate = adoThread.PublishedDate,
            LastUpdatedDate = adoThread.LastUpdatedDate
        };
    }

    /// <summary>
    /// Converts Azure DevOps VersionControlChangeType to Core FileChangeType.
    /// </summary>
    public static FileChangeType ToFileChangeType(VersionControlChangeType adoChangeType)
    {
        // ADO uses flags, so check for specific values
        if (adoChangeType.HasFlag(VersionControlChangeType.Add))
            return FileChangeType.Add;
        if (adoChangeType.HasFlag(VersionControlChangeType.Delete))
            return FileChangeType.Delete;
        if (adoChangeType.HasFlag(VersionControlChangeType.Rename))
            return FileChangeType.Rename;
        if (adoChangeType.HasFlag(VersionControlChangeType.Edit))
            return FileChangeType.Edit;
        
        return FileChangeType.None;
    }

    /// <summary>
    /// Converts Azure DevOps PullRequestStatus to Core PullRequestStatus.
    /// </summary>
    public static CorePullRequestStatus ToStatus(AdoPullRequestStatus adoStatus)
    {
        return adoStatus switch
        {
            AdoPullRequestStatus.Active => CorePullRequestStatus.Active,
            AdoPullRequestStatus.Completed => CorePullRequestStatus.Completed,
            AdoPullRequestStatus.Abandoned => CorePullRequestStatus.Abandoned,
            _ => CorePullRequestStatus.Active
        };
    }

    /// <summary>
    /// Converts Core PullRequestStatus to Azure DevOps PullRequestStatus.
    /// </summary>
    public static AdoPullRequestStatus FromStatus(CorePullRequestStatus status)
    {
        return status switch
        {
            CorePullRequestStatus.Active => AdoPullRequestStatus.Active,
            CorePullRequestStatus.Completed => AdoPullRequestStatus.Completed,
            CorePullRequestStatus.Abandoned => AdoPullRequestStatus.Abandoned,
            CorePullRequestStatus.Draft => AdoPullRequestStatus.Active,
            _ => AdoPullRequestStatus.Active
        };
    }

    /// <summary>
    /// Converts Azure DevOps CommentThreadStatus to Core ThreadStatus.
    /// </summary>
    public static ThreadStatus ToThreadStatus(CommentThreadStatus? adoStatus)
    {
        return adoStatus switch
        {
            CommentThreadStatus.Active => ThreadStatus.Active,
            CommentThreadStatus.Fixed => ThreadStatus.Fixed,
            CommentThreadStatus.Closed => ThreadStatus.Closed,
            CommentThreadStatus.ByDesign => ThreadStatus.ByDesign,
            CommentThreadStatus.Pending => ThreadStatus.Pending,
            CommentThreadStatus.WontFix => ThreadStatus.WontFix,
            _ => ThreadStatus.Active
        };
    }

    /// <summary>
    /// Converts Core ThreadStatus to Azure DevOps CommentThreadStatus.
    /// </summary>
    public static CommentThreadStatus FromThreadStatus(ThreadStatus status)
    {
        return status switch
        {
            ThreadStatus.Active => CommentThreadStatus.Active,
            ThreadStatus.Fixed => CommentThreadStatus.Fixed,
            ThreadStatus.Closed => CommentThreadStatus.Closed,
            ThreadStatus.ByDesign => CommentThreadStatus.ByDesign,
            ThreadStatus.Pending => CommentThreadStatus.Pending,
            ThreadStatus.WontFix => CommentThreadStatus.WontFix,
            _ => CommentThreadStatus.Active
        };
    }
}
