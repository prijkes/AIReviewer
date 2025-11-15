# Quaally Architecture - Queue-Based Conversational System

## Project Structure

The solution uses a **layered architecture** with clear separation of concerns:

```
Quaally.sln
├── Quaally.Core/              # Domain models, interfaces, enums
│   ├── Models/                # Core domain models (PullRequest, FileChange, etc.)
│   ├── Interfaces/            # Service interfaces (ISourceControlClient, etc.)
│   └── Enums/                 # Enumerations (SourceProvider, FileChangeType, etc.)
│
├── Quaally.Data/              # Data access and telemetry
│   └── ReviewMetrics.cs       # Metrics tracking and logging
│
├── Quaally.Infrastructure/    # External integrations and utilities
│   ├── AI/                    # AI client implementations
│   ├── AzureDevOps/          # Azure DevOps SDK integration
│   ├── Providers/            # Source provider implementations
│   ├── Diff/                 # Diff processing
│   ├── Utils/                # Utilities (logging, settings, etc.)
│   ├── Policy/               # Review policy loading
│   ├── Options/              # Configuration options
│   └── Review/               # Review planning and execution
│
├── Quaally.Worker/           # Application entry point
│   ├── Program.cs            # Main application host
│   ├── ReviewerHostedService.cs
│   ├── Queue/                # Queue processing
│   └── Orchestration/        # AI orchestration
│
└── Quaally.Tests/            # Unit tests
    ├── AI/
    ├── Diff/
    ├── Options/
    └── Utils/
```

### Layer Dependencies

```
Quaally.Worker
    ↓ depends on
Quaally.Infrastructure
    ↓ depends on
Quaally.Core
    ↑
Quaally.Data
    ↓ depends on
Quaally.Core

Quaally.Tests → references all projects
```

**Design Principles:**
- **Core**: No external dependencies, pure domain logic
- **Data**: Minimal dependencies, focuses on metrics and persistence
- **Infrastructure**: Implements Core interfaces, contains all external integrations
- **Worker**: Composition root, hosts the application

## Overview

Quaally is a **queue-based, conversational AI assistant** with autonomous function calling capabilities for pull request code reviews.

## High-Level Architecture

```
┌─────────────────────────────────────────────────────────────────────┐
│                         Azure DevOps                                 │
│                                                                      │
│  Pull Request ──► User Comments ──► @Quaally please review      │
│                         │                                            │
│                         │ (Webhook Trigger)                          │
│                         ▼                                            │
│                  Service Hook                                        │
│                  (PR Comment Event)                                  │
└─────────────────────────┬───────────────────────────────────────────┘
                          │
                          ▼
┌─────────────────────────────────────────────────────────────────────┐
│                    Azure Service Bus                                 │
│                                                                      │
│  Queue: Quaally-events                                           │
│  ├─ Message 1: PR #123 comment                                     │
│  ├─ Message 2: PR #456 comment                                     │
│  └─ Message 3: PR #789 comment                                     │
│                                                                      │
└─────────────────────────┬───────────────────────────────────────────┘
                          │
                          │ (Pull Messages)
                          ▼
┌─────────────────────────────────────────────────────────────────────┐
│                    Quaally Application                            │
│                                                                      │
│  ┌────────────────────────────────────────────────────────────┐   │
│  │         QueueProcessorHostedService                        │   │
│  │  - Pulls messages from Azure Service Bus                   │   │
│  │  - Routes events by type                                    │   │
│  │  - Handles retries & dead-lettering                        │   │
│  └────────────────────┬───────────────────────────────────────┘   │
│                       │                                             │
│                       ▼                                             │
│  ┌────────────────────────────────────────────────────────────┐   │
│  │              AiOrchestrator                                │   │
│  │  1. Detects @mentions (MentionDetector)                    │   │
│  │  2. Builds PR context + thread history                     │   │
│  │  3. Calls Azure OpenAI with 20 function definitions        │   │
│  │  4. Executes functions via AzureDevOpsFunctionExecutor     │   │
│  │  5. Continues conversation until complete                  │   │
│  │  6. Posts final response to PR thread                      │   │
│  └────────────────────┬───────────────────────────────────────┘   │
│                       │                                             │
│                       ▼                                             │
│  ┌────────────────────────────────────────────────────────────┐   │
│  │      AzureDevOpsFunctionExecutor                           │   │
│  │  Executes 20 functions:                                    │   │
│  │  ├─ create_pr_comment_thread                              │   │
│  │  ├─ reply_to_thread                                       │   │
│  │  ├─ update_thread_status                                  │   │
│  │  ├─ approve_pull_request                                  │   │
│  │  ├─ get_pr_files                                          │   │
│  │  ├─ get_full_file_content                                 │   │
│  │  ├─ search_codebase                                       │   │
│  │  └─ ... (13 more functions)                               │   │
│  └────────────────────┬───────────────────────────────────────┘   │
│                       │                                             │
│                       ▼                                             │
│  ┌────────────────────────────────────────────────────────────┐   │
│  │         Azure DevOps SDK / Git API                         │   │
│  │  - Manages PR threads and comments                         │   │
│  │  - Handles approvals and merges                            │   │
│  │  - Retrieves file contents and diffs                       │   │
│  └────────────────────────────────────────────────────────────┘   │
│                                                                      │
└──────────────────────────────────────────────────────────────────────┘
                          │
                          │ (Posts Comments)
                          ▼
┌──────────────────────────────────────────────────────────────────────┐
│                       Azure DevOps                                    │
│                                                                       │
│  Pull Request ◄── Bot Reply ◄── "I've reviewed the code..."         │
│                                                                       │
└───────────────────────────────────────────────────────────────────────┘
```

## Component Details

### 1. QueueProcessorHostedService

**Responsibility:** Message processing from Azure Service Bus

**Key Features:**
- Continuously polls Azure Service Bus queue
- Processes up to 5 messages concurrently (configurable)
- Handles message completion, abandonment, and dead-lettering
- Routes events by type to appropriate handlers

**Message Flow:**
```
Message arrives → Deserialize → Route by event type → Process → Complete/Abandon
```

**Error Handling:**
- Retries up to 3 times (configurable in Azure)
- Auto dead-letters after max retries
- Logs all processing errors

### 2. AiOrchestrator

**Responsibility:** Orchestrate AI conversations with function calling

**Workflow:**
```
1. Receive PR comment event
2. Check if bot was @mentioned
3. Extract user request from mention
4. Build conversation context:
   ├─ PR metadata (title, desc, author, branches, status)
   ├─ Thread history (previous conversation)
   └─ User request
5. Call Azure OpenAI with:
   ├─ System message (PR context + instructions)
   ├─ Conversation history
   ├─ User message
   └─ 20 function definitions
6. Process AI response:
   ├─ If function calls → Execute functions → Loop back to step 5
   └─ If final response → Post to PR thread
```

**Function Calling Loop:**
- Max 10 iterations to prevent infinite loops
- Each iteration can call multiple functions
- Function results feed back into next AI call
- Continues until AI provides final text response

### 3. MentionDetector

**Responsibility:** Detect when bot is mentioned in comments

**Detection Patterns:**
- `@BotName` - Standard mention
- `@<BotName>` - XML-style mention (Azure DevOps format)
- Case-insensitive matching
- Extracts text after mention as user request

### 4. BotIdentityService

**Responsibility:** Identify the bot's own user ID

**Purpose:**
- Distinguishes bot's comments from human comments in thread history
- Caches identity to avoid repeated API calls
- Used for conversation history formatting

### 5. AzureDevOpsFunctionExecutor

**Responsibility:** Execute functions called by the AI

**Function Categories:**

#### Thread & Comment Management (4 functions)
- `create_pr_comment_thread` - Create inline code comments
- `reply_to_thread` - Add replies to existing threads
- `update_thread_status` - Change thread status (active/fixed/closed)
- `get_thread_conversation` - Retrieve full thread history

#### PR Status & Approval (4 functions)
- `approve_pull_request` - Vote on PR (-10 to +10)
- `complete_pull_request` - Merge the PR
- `abandon_pull_request` - Close without merging
- `set_auto_complete` - Enable/disable auto-merge

#### PR Management (3 functions)
- `add_reviewer` - Add reviewers (required/optional)
- `update_pr_description` - Update PR description
- `add_pr_label` - Tag the PR (placeholder)

#### PR Information (5 functions)
- `get_pr_files` - List all changed files
- `get_pr_diff` - Get diff for specific file (placeholder)
- `get_commit_details` - Get commit information
- `get_pr_commits` - List all commits in PR
- `get_pr_work_items` - Get linked work items

#### Code Analysis (5 functions)
- `get_full_file_content` - Read complete file
- `get_file_at_commit` - Read file at specific version
- `search_codebase` - Search for code patterns
- `get_related_files` - Find related files
- `get_file_history` - Get file commit history

### 6. ReviewContextRetriever

**Responsibility:** Retrieve code and repository context

**Capabilities:**
- Read files from local git repository (fast)
- Search codebase with regex patterns
- Find namespace relationships
- Retrieve commit history
- Provides context for AI code analysis

## Data Flow

### Typical Conversation Flow

```
User: "@Quaally please review this PR"
  ↓
Queue: PR comment event
  ↓
Orchestrator: 
  - Detects mention
  - Extracts: "please review this PR"
  - Builds context
  ↓
AI (Iteration 1):
  - Function call: get_pr_files
  ↓
Executor: Returns ["Program.cs", "README.md"]
  ↓
AI (Iteration 2):
  - Function call: get_full_file_content(Program.cs)
  ↓
Executor: Returns file content
  ↓
AI (Iteration 3):
  - Function call: get_full_file_content(README.md)
  ↓
Executor: Returns file content
  ↓
AI (Iteration 4):
  - Function call: create_pr_comment_thread
    (file: Program.cs, line: 42, comment: "Consider using...")
  ↓
Executor: Creates thread, returns thread ID
  ↓
AI (Iteration 5):
  - Final response: "I've reviewed your PR and found..."
  ↓
Orchestrator: Posts response to original thread
  ↓
User sees: Bot reply in PR with inline comments
```

## Configuration

### Environment Variables (.env)
```
ADO_COLLECTION_URL          - Azure DevOps org URL
ADO_PROJECT                 - Project name
ADO_REPO_ID                 - Repository GUID
ADO_ACCESS_TOKEN            - PAT with Code & PR permissions
AI_FOUNDRY_ENDPOINT         - Azure OpenAI endpoint
AI_FOUNDRY_API_KEY          - Azure OpenAI API key
LOCAL_REPO_PATH             - Path to local git clone
ServiceBusConnectionString  - Azure Service Bus connection
```

### Settings (settings.ini)
```ini
[AI]
Deployment = gpt-4o          # Model to use
Temperature = 0.2            # Lower = more deterministic
MaxTokens = 4000             # Max response length

[FunctionCalling]
Enabled = true               # Enable function calling
MaxCalls = 10                # Max function calls per review

[Queue]
QueueName = Quaally-events      # Azure Service Bus queue
BotDisplayName = Quaally        # Name for @mentions
MaxConcurrentCalls = 5             # Concurrent message processing
MaxWaitTimeSeconds = 30            # Max wait for messages
```

## Scalability

### Horizontal Scaling
- Multiple instances can process from same queue
- Azure Service Bus handles message distribution
- Each instance processes up to MaxConcurrentCalls messages

### Vertical Scaling
- Increase MaxConcurrentCalls for more throughput
- Requires more CPU/memory per instance
- Higher OpenAI API costs

### Cost Optimization
- Use Basic tier Service Bus (~$0.05/million operations)
- Monitor OpenAI token usage
- Adjust concurrency based on load

## Security

### Authentication
- Azure DevOps: PAT with minimal required permissions
- Azure OpenAI: API key authentication
- Service Bus: SAS connection string

### Data Protection
- All communication over HTTPS
- Credentials in .env file (not in source control)
- Local repository path must be secure

### Access Control
- Bot can only act with PAT permissions
- Function execution is sandboxed
- No direct code execution from AI

## Error Handling

### Message Processing Errors
1. Log error details
2. Abandon message (returns to queue)
3. Retry up to 3 times
4. Dead-letter if max retries exceeded

### Function Execution Errors
1. Catch and log exception
2. Return error message to AI
3. AI adapts or reports error to user
4. Conversation continues

### Network Failures
1. Polly retry policies (3 retries with backoff)
2. Circuit breaker for repeated failures
3. Detailed logging for troubleshooting

## Monitoring

### Key Metrics
- **Queue depth**: Should be near 0 when running
- **Dead-letter count**: Should stay at 0
- **Processing time**: Avg time per message
- **Function call frequency**: Which functions used most
- **Error rate**: Failed message percentage

### Logging
- All function executions logged
- Message processing lifecycle tracked
- Errors include stack traces
- Debug logs for AI iterations

## Extension Points

### Adding New Functions
1. Define function in `AzureDevOpsFunctionDefinitions`
2. Create parameter model in `Functions/Parameters/`
3. Implement execution in `AzureDevOpsFunctionExecutor`
4. Add case to switch statement
5. Test with @mentions

### Custom Event Types
1. Add event model in `Queue/Models/`
2. Add case in `RouteEventAsync`
3. Implement handler method
4. Update Service Hook filters

### Alternative AI Providers
1. Implement `ChatClient` interface
2. Update service registration in `Program.cs`
3. Ensure function calling compatibility

## Future Enhancements

### Planned Features
- Persistent conversation memory across sessions
- Build/test status integration
- Code owners awareness
- Multi-repository support
- Analytics dashboard

### Potential Additions
- Slack/Teams notifications
- Scheduled PR reviews
- Custom review templates
- Integration with external tools
- Machine learning feedback loop

## References

- [Azure Service Bus Documentation](https://learn.microsoft.com/en-us/azure/service-bus-messaging/)
- [Azure DevOps Service Hooks](https://learn.microsoft.com/en-us/azure/devops/service-hooks/)
- [OpenAI Function Calling](https://platform.openai.com/docs/guides/function-calling)
- [Azure DevOps REST API](https://learn.microsoft.com/en-us/rest/api/azure/devops/)
