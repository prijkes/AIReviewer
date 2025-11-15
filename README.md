# Quaally - Conversational AI Code Reviewer

**QUallity Assurance AnLLYzer** - An AI-powered, conversational code review assistant for pull requests. Quaally operates as a queue-based service that responds to @mentions in PR comments, providing intelligent code reviews with autonomous function calling capabilities.

Supports **Azure DevOps** with extensible architecture for GitHub, GitLab, and other providers.

## 🎯 Key Features

- **💬 Conversational Interface**: @mention the bot in any PR comment to interact naturally
- **🤖 Autonomous Function Calling**: AI can call 20+ functions to manage PRs, review code, and more
- **🔄 Multi-Turn Conversations**: Maintains context across multiple exchanges in thread discussions
- **⚡ Event-Driven**: Triggered by Azure Service Bus queue for scalable, reliable processing
- **🎭 Context-Aware**: Remembers thread history for intelligent follow-ups
- **🛠️ Full PR Management**: Can approve, merge, create threads, and manage the entire PR lifecycle
- **🔌 Provider-Agnostic**: Clean architecture separating business logic from provider specifics
- **📦 Modular Design**: Easy to add new providers through interface implementation
- **⚙️ Configurable**: Flexible configuration system with settings file and environment variables

## 🚀 Quick Start

### Prerequisites

- Azure subscription (for Service Bus & Azure OpenAI)
- Azure DevOps project with repository
- .NET 8.0 SDK
- Personal Access Token with Code & PR permissions

### Step 1: Setup Azure Service Bus

See **[QUEUE_SETUP_GUIDE.md](QUEUE_SETUP_GUIDE.md)** for detailed instructions.

**Quick version:**
1. Create Azure Service Bus namespace (Basic tier)
2. Create queue named `Quaally-events`
3. Get connection string
4. Configure Azure DevOps webhook (Pull request commented on → Service Bus)

### Step 2: Configure Application

Create `.env` file in `Quaally.Worker/` directory:

```ini
# Azure DevOps
ADO_COLLECTION_URL=https://dev.azure.com/your-org
ADO_PROJECT=YourProject
ADO_REPO_ID=your-repo-guid
ADO_ACCESS_TOKEN=your-pat-token
LOCAL_REPO_PATH=/path/to/local/repo/clone

# Azure AI
AI_FOUNDRY_ENDPOINT=https://your-endpoint.openai.azure.com/
AI_FOUNDRY_API_KEY=your-api-key

# Azure Service Bus
ServiceBusConnectionString=Endpoint=sb://...
```

Update `Quaally.Worker/settings.ini`:

```ini
[Queue]
QueueName = Quaally-events
BotDisplayName = Quaally
MaxConcurrentCalls = 5
MaxWaitTimeSeconds = 30
```

### Step 3: Run the Application

```bash
cd Quaally.Worker
dotnet build
dotnet run --env .env
```

Expected output:
```
info: Queue Processor started successfully. Listening on queue: Quaally-events
```

### Step 4: Test It!

1. Create a PR in Azure DevOps
2. Add a comment: `@Quaally please review this PR`
3. Watch the bot respond with a review!

## 💡 Usage Examples

### Code Review
```
@Quaally please review this PR for code quality and security issues
```

The AI will:
1. Get list of changed files
2. Read file contents
3. Analyze the code
4. Create inline comment threads for issues found
5. Reply with summary

### Specific File Review
```
@Quaally can you review the changes in Program.cs?
```

### Explain Code
```
@Quaally please explain what the ProcessMessage method does
```

### Approve PR
```
@Quaally if everything looks good, please approve this PR
```

### Continue Conversation
```
User: @Quaally review this
Bot: (creates thread on line 42)
User: (fixes code)
User in thread: @Quaally I've fixed this, please check
Bot: (reviews fix, closes thread if satisfied)
```

### Search Codebase
```
@Quaally can you find all usages of the IPaymentService interface?
```

## 🛠️ Available Functions

The AI can autonomously call these 20 functions:

### Thread & Comment Management
- `create_pr_comment_thread` - Create inline comments on code
- `reply_to_thread` - Continue conversations in threads
- `update_thread_status` - Mark threads as fixed/closed/active
- `get_thread_conversation` - View full thread history

### PR Status & Approval
- `approve_pull_request` - Vote on PR (-10 to +10)
- `complete_pull_request` - Merge the PR
- `abandon_pull_request` - Close without merging
- `set_auto_complete` - Enable auto-merge

### PR Management
- `add_reviewer` - Add required/optional reviewers
- `update_pr_description` - Improve PR descriptions
- `add_pr_label` - Tag PRs (placeholder)

### PR Information
- `get_pr_files` - List changed files
- `get_pr_diff` - Get file diffs
- `get_commit_details` - Inspect commits
- `get_pr_commits` - List all commits
- `get_pr_work_items` - View linked work items

### Code Analysis
- `get_full_file_content` - Read complete files
- `get_file_at_commit` - View file at specific version
- `search_codebase` - Find code patterns/classes/methods
- `get_related_files` - Discover related files
- `get_file_history` - View commit history

## 📚 Documentation

### Setup & Configuration
- **[QUEUE_SETUP_GUIDE.md](QUEUE_SETUP_GUIDE.md)** - Complete setup instructions
- **[ARCHITECTURE.md](ARCHITECTURE.md)** - System architecture and design
- **[FUNCTION_CALLING.md](FUNCTION_CALLING.md)** - Function calling internals
- **[LANGUAGE_SPECIFIC_POLICIES.md](LANGUAGE_SPECIFIC_POLICIES.md)** - Code review policies

## ⚙️ Configuration

Quaally uses a layered configuration system:

1. **settings.ini** - Static defaults with inline documentation
2. **Environment variables** - Dynamic values and secrets
3. **.env file** - Local development (gitignored)

### Key Settings

**settings.ini:**
```ini
[AI]
Deployment = gpt-4o
Temperature = 0.2
MaxTokens = 4000

[FunctionCalling]
Enabled = true
MaxConversationIterations = 10

[Queue]
QueueName = Quaally-events
BotDisplayName = Quaally
MaxConcurrentCalls = 5
```

**Environment (.env):**
```ini
# Source Control Provider (defaults to AzureDevOps)
SOURCE_PROVIDER=AzureDevOps

# Azure DevOps Configuration
ServiceBusConnectionString=Endpoint=sb://...
AI_FOUNDRY_ENDPOINT=https://...
AI_FOUNDRY_API_KEY=...
ADO_ACCESS_TOKEN=...
```

## 🏗️ Architecture

```
Azure DevOps PR Comment (@Quaally)
    ↓
Azure Service Bus Queue
    ↓
QueueProcessorHostedService
    ↓
AiOrchestrator (builds context + calls AI)
    ↓
AzureDevOpsFunctionExecutor (executes functions)
    ↓
Azure DevOps API (posts results)
```

### Project Structure

```
Quaally.sln
├── Quaally.Core/              # Domain models, interfaces, enums
├── Quaally.Data/              # Data access and telemetry
├── Quaally.Infrastructure/    # External integrations and utilities
├── Quaally.Worker/           # Application entry point
└── Quaally.Tests/            # Unit tests
```

See [ARCHITECTURE.md](ARCHITECTURE.md) for detailed diagrams and explanations.

## 📊 Monitoring

### Azure Portal
- Monitor queue depth (should be near 0)
- Watch for dead-letter messages (should stay at 0)
- Track message processing metrics

### Application Logs
```
info: Processing message abc-123 (Delivery count: 1)
info: Executing function: get_pr_files
info: Executing function: get_full_file_content
info: Successfully replied to comment in thread 456
```

## 💰 Cost Management

### Azure Service Bus
- Basic tier: ~$0.05 per million operations
- Typical usage: <100 messages/day = negligible cost

### Azure OpenAI
- Varies by model and usage
- Estimated: $0.01-0.10 per PR review
- Budget: ~$10-30/month for moderate usage

### Optimization Tips
1. Use smaller models (gpt-4o-mini vs gpt-4o)
2. Adjust `MaxTokens` in settings
3. Monitor token usage in logs

## 🔒 Security

- **Credentials**: Stored in environment variables (never in code)
- **Authentication**: Azure DefaultAzureCredential for OpenAI, PAT for Azure DevOps
- **Permissions**: Bot acts only with configured PAT permissions (principle of least privilege)
- **Data**: All communication over HTTPS
- **Sandboxing**: Function execution is controlled and logged

## 🧪 Troubleshooting

### Bot Doesn't Respond

**Check:**
1. Is application running? (`dotnet run`)
2. Are messages arriving in queue? (Azure Portal → Service Bus → Queue)
3. Check application logs for errors
4. Verify Service Bus connection string

### "ServiceBusConnectionString not set"

- Ensure `.env` file exists in `Quaally.Worker/` directory
- Verify `ServiceBusConnectionString` is in `.env`
- Try running with `dotnet run --env .env`

### Bot Mentions Not Detected

- Verify `BotDisplayName` in `settings.ini` matches your @mention
- Try both `@Quaally` and `@<Quaally>`
- Check application logs for "Bot not mentioned" messages

### Messages in Dead-Letter Queue

1. Check application logs for processing errors
2. Fix the underlying issue
3. Resubmit from dead-letter queue or wait for new events

## 🌐 Multi-Provider Support

### Supported Providers

- ✅ **Azure DevOps** - Full support (all 20+ functions)

### Planned Providers

- 🔄 **GitHub** - In development
- 🔄 **GitLab** - Planned
- 🔄 **Bitbucket** - Planned

### Provider Architecture

Quaally uses a provider-agnostic architecture:

```
┌─────────────────────────────────┐
│   Core Interfaces               │  ← Provider-agnostic
│   (ISourceControlClient, etc.)  │
└──────────┬──────────────────────┘
           │ Implemented by
           ▼
┌─────────────────────────────────┐
│   Provider Implementation       │  ← Provider-specific
│   (AzureDevOps, GitHub, etc.)   │
└─────────────────────────────────┘
```

### Adding a New Provider

To add support for a new provider:

1. Implement `ISourceControlClient` interface
2. Implement `ICommentService` interface
3. Implement `IApprovalService` interface
4. Register in `Program.cs` dependency injection

### Selecting a Provider

**Default (Azure DevOps):**
```bash
dotnet run --env .env
```

**Explicit Selection:**
```bash
# Via environment variable
export SOURCE_PROVIDER=AzureDevOps
dotnet run --env .env

# Or in settings.ini:
[Provider]
SourceProvider = AzureDevOps
```

## 🚀 Deployment Options

### Option 1: Run Locally (Development)
```bash
dotnet run --env .env
```

### Option 2: Azure Container Instance
```bash
docker build -t quaally .
docker run -e ServiceBusConnectionString="..." quaally
```

### Option 3: Azure App Service
Deploy as a worker process (no HTTP endpoints needed)

### Option 4: Self-Hosted Server
Run as systemd service (Linux) or Windows Service

## 📈 Roadmap

### Future Features
- Persistent conversation memory across sessions
- Build/test status integration
- Code owners awareness
- Multi-repository support
- Analytics dashboard
- Additional provider support (GitHub, GitLab, Bitbucket)
- Multi-provider deployments (run multiple providers simultaneously)

### Potential Additions
- Slack/Teams notifications
- Scheduled PR reviews
- Custom review templates
- Machine learning feedback loop

## 🤝 Contributing

Contributions are welcome! Please:

1. Check existing issues or create a new one
2. Fork the repository
3. Create a feature branch
4. Make your changes with tests
5. Submit a pull request

## 📄 License

[Your License Here]

## 💬 Support

- **Documentation**: See [QUEUE_SETUP_GUIDE.md](QUEUE_SETUP_GUIDE.md) and [ARCHITECTURE.md](ARCHITECTURE.md)
- **Issues**: [Open an issue](your-repo-url/issues)
- **Questions**: [Discussions](your-repo-url/discussions)

---

**Happy Reviewing!** 🎉

Built with ❤️ using Azure OpenAI, Azure Service Bus, and .NET 8
