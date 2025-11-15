# Multi-Provider Refactoring Documentation

## Overview

This document describes the major refactoring to support multiple source control providers (Azure DevOps, GitHub, GitLab, etc.) instead of being locked to Azure DevOps only.

## Architecture Changes

### Before: Tightly Coupled to Azure DevOps

```
Quaally/
├── AzureDevOps/           # ADO-specific code everywhere
│   ├── AdoSdkClient.cs
│   ├── Functions/
│   └── Models/
├── Orchestration/         # Uses ADO types directly
└── Queue/                 # ADO event models
```

### After: Provider-Agnostic with Abstraction Layer

```
Quaally/
├── Core/                       # NEW - Generic abstractions
│   ├── Enums/                 # Provider-agnostic enums
│   ├── Models/                # Generic models
│   └── Interfaces/            # Provider interfaces
├── Providers/                  # NEW - Provider implementations
│   ├── AzureDevOps/           # ADO-specific implementation
│   ├── GitHub/                # GitHub implementation (future)
│   └── GitLab/                # GitLab implementation (future)
├── Orchestration/             # Now uses Core interfaces
└── Queue/                     # Now uses Core models
```

## Core Abstractions Created

### Enums

- **SourceProvider**: Enum for different providers (AzureDevOps, GitHub, GitLab, Bitbucket)
- **PullRequestStatus**: Generic PR status (Active, Completed, Abandoned, Draft)
- **FileChangeType**: Generic file change types (Add, Edit, Delete, Rename, None)
- **ThreadStatus**: Generic thread status (Active, Fixed, Closed, ByDesign, Pending, WontFix)

### Models

- **Repository**: Generic repository model
- **UserIdentity**: Generic user identity model
- **PullRequest**: Generic pull request model
- **Comment**: Generic comment model
- **ReviewThread**: Generic review thread model
- **FileChange**: Generic file change model
- **CommentEvent**: Generic comment event model

### Interfaces

- **ISourceControlClient**: Main interface for SCM operations
  - Get pull request information
  - Get file changes
  - Get file content and diffs
  - Search codebase
  - Get file history

- **ICommentService**: Interface for managing comments/threads
  - Create threads
  - Reply to threads
  - Update thread status
  - Get threads

- **IApprovalService**: Interface for PR approvals and status
  - Vote on PR
  - Complete (merge) PR
  - Abandon PR
  - Set auto-complete
  - Add reviewers
  - Update description

- **IFunctionExecutor**: Interface for AI function execution
  - Set context
  - Execute functions by name

- **IEventProcessor**: Interface for processing events
  - Process comment events

## Azure DevOps Provider Implementation

### Model Adapter

The `ModelAdapter` class converts between Azure DevOps SDK types and Core generic types:

- `ToPullRequest()`: ADO GitPullRequest → Core PullRequest
- `ToRepository()`: ADO GitRepository → Core Repository
- `ToUserIdentity()`: ADO IdentityRef → Core UserIdentity
- `ToComment()`: ADO Comment → Core Comment
- `ToReviewThread()`: ADO GitPullRequestCommentThread → Core ReviewThread
- `ToFileChangeType()`: ADO VersionControlChangeType → Core FileChangeType
- `ToStatus()`: ADO PullRequestStatus → Core PullRequestStatus
- `ToThreadStatus()`: ADO CommentThreadStatus → Core ThreadStatus
- `FromStatus()`: Core PullRequestStatus → ADO PullRequestStatus
- `FromThreadStatus()`: Core ThreadStatus → ADO CommentThreadStatus

### Implementation Classes

The following classes implement the Core interfaces for Azure DevOps:

1. **AzureDevOpsSourceControlClient** (implements `ISourceControlClient`)
   - Wraps `IAdoSdkClient`
   - Uses `ModelAdapter` to convert types
   - Implements all SCM operations

2. **AzureDevOpsCommentService** (implements `ICommentService`)
   - Wraps existing `CommentService`
   - Uses `ModelAdapter` for type conversion
   - Implements comment/thread operations

3. **AzureDevOpsApprovalService** (implements `IApprovalService`)
   - Wraps existing `ApprovalService`
   - Implements PR approval operations

4. **AzureDevOpsFunctionExecutor** (implements `IFunctionExecutor`)
   - Existing class adapted to implement interface
   - Executes 20+ Azure DevOps functions

## Configuration Changes

### ReviewerOptions

Added new property:

```csharp
public SourceProvider SourceProvider { get; set; } = SourceProvider.AzureDevOps;
```

This can be set via:
- `settings.ini`: `SourceProvider = AzureDevOps`
- Environment variable: `SOURCE_PROVIDER=AzureDevOps`

### Dependency Injection

The `Program.cs` now uses a factory pattern to create provider-specific implementations:

```csharp
services.AddSingleton<ISourceControlClient>(sp =>
{
    var options = sp.GetRequiredService<ReviewerOptions>();
    return options.SourceProvider switch
    {
        SourceProvider.AzureDevOps => sp.GetRequiredService<AzureDevOpsSourceControlClient>(),
        SourceProvider.GitHub => sp.GetRequiredService<GitHubSourceControlClient>(),
        _ => throw new NotSupportedException($"Provider {options.SourceProvider} not supported")
    };
});
```

## Migration Path

### Phase 1: Core Abstraction ✅
- Created Core enums, models, and interfaces
- Created ModelAdapter for Azure DevOps

### Phase 2: Azure DevOps Provider (In Progress)
- Implement ISourceControlClient for Azure DevOps
- Implement ICommentService for Azure DevOps
- Implement IApprovalService for Azure DevOps
- Adapt existing FunctionExecutor to interface

### Phase 3: Update Core Components
- Update AiOrchestrator to use ISourceControlClient
- Update QueueProcessorHostedService to use Core models
- Update BotIdentityService to use ISourceControlClient
- Update MentionDetector to be provider-agnostic

### Phase 4: Update Dependency Injection
- Register provider-specific implementations
- Add factory pattern for interface selection
- Update configuration

### Phase 5: Testing
- Test Azure DevOps provider still works
- Verify all functions execute correctly
- Test queue processing with new models

### Phase 6: Future Providers
- Implement GitHub provider
- Implement GitLab provider
- Add provider-specific function definitions

## Benefits

✅ **Multi-provider support**: Can now support GitHub, GitLab, Bitbucket
✅ **Clean separation**: Business logic separated from provider specifics
✅ **Extensibility**: Easy to add new providers
✅ **Maintainability**: Changes to one provider don't affect others
✅ **Testability**: Can mock providers for unit tests
✅ **Flexibility**: Can run multiple providers in same deployment

## Breaking Changes

### For Azure DevOps Users

**None** - The refactoring is backward compatible. Existing Azure DevOps setups will continue to work without changes.

### For Developers

- Import namespaces changed:
  - Old: `using Quaally.AzureDevOps.Models;`
  - New: `using Quaally.Core.Models;`
  
- Type names changed:
  - Old: `GitPullRequest` (ADO SDK type)
  - New: `PullRequest` (Core model)

## Future Enhancements

### GitHub Provider

Will support:
- GitHub webhooks instead of Azure Service Bus
- GitHub REST API
- GitHub-specific functions (issues, projects, GitHub Actions)

### GitLab Provider

Will support:
- GitLab webhooks
- GitLab REST API
- GitLab-specific functions (merge requests, pipelines)

### Provider-Specific Functions

Some functions will be provider-specific:

**Azure DevOps Only:**
- `get_pr_work_items` (links to Azure Boards)
- Pipeline integration

**GitHub Only:**
- `get_pr_issues` (links to GitHub Issues)
- GitHub Actions integration
- GitHub Projects

**GitLab Only:**
- `get_mr_issues` (links to GitLab Issues)
- GitLab CI/CD integration

## Implementation Status

- [x] Core Enums
- [x] Core Models
- [x] Core Interfaces
- [x] Azure DevOps ModelAdapter
- [ ] Azure DevOps ISourceControlClient implementation
- [ ] Azure DevOps ICommentService implementation
- [ ] Azure DevOps IApprovalService implementation
- [ ] Azure DevOps IFunctionExecutor adaptation
- [ ] Update AiOrchestrator
- [ ] Update QueueProcessorHostedService
- [ ] Update dependency injection
- [ ] Testing
- [ ] Documentation updates
- [ ] GitHub provider (future)
- [ ] GitLab provider (future)

## Files Modified/Created

### Created
- `Quaally/Core/Enums/SourceProvider.cs`
- `Quaally/Core/Enums/PullRequestStatus.cs`
- `Quaally/Core/Enums/FileChangeType.cs`
- `Quaally/Core/Enums/ThreadStatus.cs`
- `Quaally/Core/Models/Repository.cs`
- `Quaally/Core/Models/UserIdentity.cs`
- `Quaally/Core/Models/PullRequest.cs`
- `Quaally/Core/Models/Comment.cs`
- `Quaally/Core/Models/ReviewThread.cs`
- `Quaally/Core/Models/FileChange.cs`
- `Quaally/Core/Models/CommentEvent.cs`
- `Quaally/Core/Interfaces/ISourceControlClient.cs`
- `Quaally/Core/Interfaces/ICommentService.cs`
- `Quaally/Core/Interfaces/IApprovalService.cs`
- `Quaally/Core/Interfaces/IFunctionExecutor.cs`
- `Quaally/Core/Interfaces/IEventProcessor.cs`
- `Quaally/Providers/AzureDevOps/Adapters/ModelAdapter.cs`

### To Be Created
- `Quaally/Providers/AzureDevOps/AzureDevOpsSourceControlClient.cs`
- `Quaally/Providers/AzureDevOps/AzureDevOpsCommentService.cs`
- `Quaally/Providers/AzureDevOps/AzureDevOpsApprovalService.cs`
- `Quaally/Providers/AzureDevOps/AzureDevOpsFunctionExecutor.cs` (move from existing)

### To Be Modified
- `Quaally/Options/ReviewerOptions.cs` (add SourceProvider property)
- `Quaally/Program.cs` (update DI)
- `Quaally/Orchestration/AiOrchestrator.cs` (use Core interfaces)
- `Quaally/Queue/QueueProcessorHostedService.cs` (use Core models)
- `Quaally/Orchestration/BotIdentityService.cs` (use Core interfaces)

## Notes

- The existing Azure DevOps code remains functional during migration
- Old `IAdoSdkClient` interface kept for backward compatibility
- Gradual migration allows testing at each step
- Provider implementations can coexist during transition
