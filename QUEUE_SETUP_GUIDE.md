# Quaally Queue-Based Setup Guide

This guide will help you set up the queue-based, conversational AI reviewer from scratch.

## Prerequisites

- Azure subscription with access to:
  - Azure Service Bus
  - Azure OpenAI or Azure AI Foundry
- Azure DevOps project with a repository
- Personal Access Token (PAT) with:
  - Code: Read & Write
  - Pull Request Threads: Read & Write
- .NET 8.0 SDK installed

## Step 1: Create Azure Service Bus

### 1.1 Create Service Bus Namespace

1. Go to [Azure Portal](https://portal.azure.com)
2. Click "Create a resource"
3. Search for "Service Bus"
4. Click "Create"
5. Fill in:
   - **Subscription**: Your subscription
   - **Resource Group**: Create new or use existing
   - **Namespace name**: `Quaally-sb` (or your choice)
   - **Location**: Choose closest to your Azure DevOps
   - **Pricing tier**: **Basic** (sufficient for this use case)
6. Click "Review + Create" → "Create"
7. Wait for deployment to complete

### 1.2 Create Queue

1. Navigate to your Service Bus namespace
2. Click "Queues" in the left menu
3. Click "+ Queue"
4. Configure:
   - **Name**: `Quaally-events`
   - **Max delivery count**: 10 (allows retries)
   - **Message time to live**: 14 days (default)
   - **Lock duration**: 5 minutes
   - Leave other settings as default
5. Click "Create"

### 1.3 Get Connection String

1. In your Service Bus namespace, click "Shared access policies"
2. Click "+ Add"
3. Configure:
   - **Policy name**: `QuaallyPolicy`
   - **Permissions**: Check both "Send" and "Listen"
4. Click "Create"
5. Click on the newly created policy
6. Copy the **Primary Connection String**
7. Save it securely - you'll need it for the .env file

## Step 2: Configure Azure DevOps Webhook

### 2.1 Create Service Hook

1. Go to your Azure DevOps project
2. Click "Project Settings" (bottom left)
3. Under "General", click "Service hooks"
4. Click "+ Create subscription"
5. Select "Azure Service Bus" and click "Next"

### 2.2 Configure Trigger

1. Select trigger: **Pull request commented on**
2. Configure filters (optional):
   - **Repository**: Select your repository (or leave blank for all)
   - **Pull request target branch**: Leave blank for all branches
   - **Commented by**: Leave blank to catch all comments
3. Click "Next"

### 2.3 Configure Action

1. Fill in:
   - **Service Bus namespace**: Your namespace name (e.g., `Quaally-sb`)
   - **Service Bus SAS Connection String**: Paste the connection string from Step 1.3
   - **Queue name**: `Quaally-events`
   - **Resource details to send**: **All**
   - **Messages to send**: **All**
   - **Resource version**: Leave as default
   
2. **IMPORTANT - Content Type Configuration:**
   - After Azure DevOps creates the Service Hook, it may use DataContract binary serialization by default
   - This causes messages to have invalid characters like `@\u0006string`
   - The application will detect and handle this, but for best performance, messages should be in JSON format
   - To ensure JSON format: After creating the Service Hook, edit it and verify the content type is set to `application/json`

3. Click "Test" to verify the connection works
4. If test succeeds, click "Finish"

### 2.4 Verify Setup

The service hook is now active! Every time someone comments on a PR, an event will be sent to your queue.

## Step 3: Configure Quaally Application

### 3.1 Create .env File

Create a `.env` file in the `Quaally` directory:

```ini
# Azure DevOps Configuration
ADO_COLLECTION_URL=https://dev.azure.com/your-organization
ADO_PROJECT=YourProjectName
ADO_REPO_ID=your-repo-guid-here
ADO_ACCESS_TOKEN=your-pat-token-here
LOCAL_REPO_PATH=/path/to/local/repo

# Azure AI Foundry / OpenAI
AI_FOUNDRY_ENDPOINT=https://your-foundry.openai.azure.com/
AI_FOUNDRY_API_KEY=your-api-key-here

# Azure Service Bus (NEW!)
ServiceBusConnectionString=Endpoint=sb://Quaally-sb.servicebus.windows.net/;SharedAccessKeyName=QuaallyPolicy;SharedAccessKey=your-key-here
```

**Notes:**
- `ADO_REPO_ID`: Find this in Azure DevOps → Repos → (Your Repo) → ... → "Copy clone URL". The GUID is in the URL.
- `LOCAL_REPO_PATH`: Should point to a local clone of your repository. Use forward slashes even on Windows (e.g., `E:/Projects/MyRepo`)
- `ServiceBusConnectionString`: The connection string from Step 1.3

### 3.2 Update settings.ini

The `settings.ini` file should already have the [Queue] section from the refactoring. Verify it looks like this:

```ini
[AI]
Deployment = gpt-4o
Temperature = 0.2
MaxTokens = 4000

[FunctionCalling]
Enabled = true
MaxCalls = 10

[Review]
DryRun = false
OnlyReviewIfRequiredReviewer = false
Scope = all
WarnBudget = 10
PolicyPath = policy/review-policy.md
PromptsBasePath = prompts

[Files]
MaxFilesToReview = 20
MaxIssuesPerFile = 5
MaxFileBytes = 200KB
MaxDiffBytes = 1MB
MaxPromptDiffBytes = 16KB
MaxCommitMessagesToReview = 5

[Language]
JapaneseDetectionThreshold = 0.3

[Queue]
QueueName = Quaally-events
BotDisplayName = Quaally
MaxConcurrentCalls = 5
MaxWaitTimeSeconds = 30
```

**Important Settings:**
- `QueueName`: Must match the queue you created in Azure
- `BotDisplayName`: The name users will @mention (case-insensitive)
- `MaxConcurrentCalls`: Number of messages to process concurrently (5 is good for most cases)

## Step 4: Run the Application

### 4.1 Build

```bash
cd Quaally
dotnet build
```

Expected output: Build succeeded with 5 warning(s)

### 4.2 Run

```bash
dotnet run --env .env
```

Expected output:
```
info: Quaally.Queue.QueueProcessorHostedService[0]
      Queue Processor starting...
info: Quaally.Queue.QueueProcessorHostedService[0]
      Service Bus processor initialized for queue: Quaally-events with 5 max concurrent calls
info: Quaally.Queue.QueueProcessorHostedService[0]
      Queue Processor started successfully. Listening on queue: Quaally-events
```

**The application is now running and listening for PR comments!**

## Step 5: Test the Integration

### 5.1 Create a Test PR

1. Create a simple PR in Azure DevOps
2. Add any file changes

### 5.2 Mention the Bot

Add a comment on the PR:
```
@Quaally please review this PR
```

### 5.3 Watch the Logs

In your running application, you should see:
```
info: Quaally.Queue.QueueProcessorHostedService[0]
      Processing message abc-123-def (Delivery count: 1)
info: Quaally.Orchestration.AiOrchestrator[0]
      Processing comment event for PR 123 in repo MyRepo
info: Quaally.Orchestration.AiOrchestrator[0]
      User request: please review this PR
info: Quaally.AzureDevOps.Functions.AzureDevOpsFunctionExecutor[0]
      Executing function: get_pr_files
info: Quaally.AzureDevOps.Functions.AzureDevOpsFunctionExecutor[0]
      Executing function: get_full_file_content
...
info: Quaally.Orchestration.AiOrchestrator[0]
      Successfully replied to comment in thread 456
```

### 5.4 Check Azure DevOps

The bot should reply to your comment with its review!

## Usage Examples

### Example 1: General Review
```
@Quaally please review this PR for code quality and best practices
```

### Example 2: Specific File Review
```
@Quaally can you check the changes in Program.cs?
```

### Example 3: Security Review
```
@Quaally please review this PR for security issues
```

### Example 4: Approve if Good
```
@Quaally if everything looks good, please approve this PR
```

### Example 5: Continue Conversation
When the bot creates a comment thread:
```
User: (fixes the issue)
User (in thread): "@Quaally I've fixed this, please review and close if OK"
Bot: (reviews the fix and closes the thread if satisfied)
```

### Example 6: Ask Questions
```
@Quaally can you explain what this function does in MyClass.cs?
```

## Available Functions

The AI can autonomously call these functions to accomplish tasks:

### Thread & Comment Management
- `create_pr_comment_thread` - Add inline code comments
- `reply_to_thread` - Continue conversations
- `update_thread_status` - Mark threads as fixed/closed
- `get_thread_conversation` - View full conversation history

### PR Status & Approval
- `approve_pull_request` - Approve/reject with different vote levels
- `complete_pull_request` - Merge the PR
- `abandon_pull_request` - Close without merging
- `set_auto_complete` - Enable auto-merge when policies pass

### PR Management
- `add_reviewer` - Add reviewers to the PR
- `update_pr_description` - Improve PR descriptions
- `add_pr_label` - Tag the PR (placeholder)

### PR Information
- `get_pr_files` - List changed files
- `get_pr_commits` - List commits in the PR
- `get_commit_details` - Get commit information
- `get_pr_work_items` - View linked work items

### Code Analysis
- `get_full_file_content` - Read complete file contents
- `get_file_at_commit` - View file at specific version
- `search_codebase` - Find code patterns/classes/methods
- `get_related_files` - Find files using same namespace
- `get_file_history` - View commit history for a file

## Troubleshooting

### Issue: Bot doesn't respond

**Check:**
1. Is the application running?
2. Check application logs for errors
3. Verify Service Bus connection string is correct
4. Check Azure Service Bus queue - are messages arriving?
   - Go to Azure Portal → Your Service Bus → Queues → Quaally-events
   - Look at "Active message count"

### Issue: "ServiceBusConnectionString environment variable is not set"

**Solution:** Make sure your .env file is in the correct location and contains the ServiceBusConnectionString.

### Issue: Bot responds but says "Error: ..."

**Check:**
1. Verify ADO_ACCESS_TOKEN has correct permissions
2. Check AI_FOUNDRY_ENDPOINT and AI_FOUNDRY_API_KEY are correct
3. Ensure LOCAL_REPO_PATH points to a valid git repository
4. Review application logs for specific error messages

### Issue: Messages stuck in queue

**Solution:**
1. Check application logs for errors processing messages
2. Look at dead-letter queue in Azure Portal
3. Messages that fail 3 times are automatically dead-lettered
4. Fix the issue, then manually resubmit from dead-letter queue

### Issue: Bot mentions not detected

**Check:**
1. Verify `BotDisplayName` in settings.ini matches what you're typing
2. @mentions are case-insensitive but spelling must match
3. Try: `@Quaally` or `@<Quaally>` (XML-style)

### Issue: Deserialization error with invalid characters like `@\u0006string`

**Symptoms:**
- Log shows: "Message is not in JSON format"
- Message body contains: `@\u0006string\b3http://schemas.microsoft.com/2003/10/Serialization/`
- Error: "Failed to parse message as JSON"

**Root Cause:**
Azure DevOps Service Hook is sending messages in DataContract binary serialization format instead of JSON.

**Solution:**
1. The application will automatically detect and extract JSON from binary payloads (as of the latest update)
2. However, for optimal performance, reconfigure the Service Hook:
   - Go to Azure DevOps → Project Settings → Service Hooks
   - Find your Quaally subscription and click "..."  → Edit
   - Ensure the content type is set to `application/json`
   - If the option isn't available, delete and recreate the Service Hook following Step 2.3

**Note:** The fix in `QueueProcessorHostedService.cs` handles this gracefully by:
1. First attempting to deserialize as JSON using `ToObjectFromJson<T>()`
2. If that fails, extracting the JSON portion from the binary payload
3. Logging a warning to alert you to fix the configuration

## Monitoring

### Azure Service Bus Metrics

Monitor your queue in Azure Portal:
- **Active Messages**: Should be near 0 when application is running
- **Dead-Letter Messages**: Should stay at 0 (investigate if increasing)
- **Incoming Messages**: Shows webhook activity
- **Outgoing Messages**: Shows successful processing

### Application Logs

Key log messages to watch:
- `Queue Processor started successfully` - Application initialized
- `Processing message` - Message received from queue
- `Executing function: X` - AI calling a function
- `Successfully replied to comment` - Response posted

### Cost Considerations

**Azure Service Bus (Basic tier):**
- ~$0.05 per million operations
- Typical usage: <100 messages/day = negligible cost

**Azure OpenAI:**
- Depends on token usage
- Estimated: $0.01-0.10 per PR review (depends on PR size)
- Budget: ~$10-30/month for moderate usage

## Advanced Configuration

### Custom Bot Name

Change the display name in settings.ini:
```ini
[Queue]
BotDisplayName = CodeReviewBot  # Users will use @CodeReviewBot
```

### Adjust Concurrency

For high-traffic scenarios:
```ini
[Queue]
MaxConcurrentCalls = 10  # Process more messages simultaneously
```

**Note:** Higher concurrency = more OpenAI API calls = higher costs

### Different Queue Per Environment

Development:
```ini
[Queue]
QueueName = Quaally-events-dev
```

Production:
```ini
[Queue]
QueueName = Quaally-events-prod
```

## Deployment Options

### Option 1: Run Locally
```bash
dotnet run --env .env
```

### Option 2: Azure Container Instance

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY Quaally/Quaally.csproj .
RUN dotnet restore
COPY Quaally/ .
RUN dotnet publish -c Release -o /app

FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "Quaally.dll"]
```

### Option 3: Azure App Service

Deploy as a worker process (no HTTP endpoints needed).

### Option 4: Self-Hosted Server

Run as a systemd service on Linux or Windows Service.

## Next Steps

1. ✅ **Complete this setup** - Get the basic system working
2. 📝 **Customize prompts** - Edit files in `Quaally/Resources/prompts/`
3. 🔒 **Add policies** - Update `Quaally/Resources/policy/` for your standards
4. 🧪 **Test thoroughly** - Try different scenarios and edge cases
5. 📊 **Monitor usage** - Watch costs and adjust concurrency if needed
6. 🚀 **Roll out gradually** - Start with one repo, expand as confidence grows

## Support

For issues or questions:
1. Check application logs for error details
2. Verify all configuration settings
3. Test with simple scenarios first
4. Review the main README.md for architectural details

Happy reviewing! 🎉
