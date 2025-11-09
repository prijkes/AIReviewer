# AIReviewer

An AI-powered code review bot for Azure DevOps pull requests. AIReviewer automatically analyzes code changes, identifies issues, and provides actionable feedback using Azure OpenAI.

## What Does AIReviewer Do?

AIReviewer integrates with your Azure DevOps pull request workflow to provide automated code reviews. It:

- **Analyzes Code Changes**: Reviews file diffs in pull requests against your custom policy guidelines
- **Reviews PR Metadata**: Checks PR titles, descriptions, and commit messages for completeness and quality
- **Posts Actionable Comments**: Creates threaded comments on specific lines with clear recommendations
- **Provides Approvals**: Automatically approves or waits based on error/warning thresholds
- **Tracks Issues**: Maintains issue fingerprints across PR iterations to avoid duplicate comments
- **Supports Custom Policies**: Uses markdown policy files to define your team's review standards
- **Multi-Language Support**: Automatically detects and responds in English or Japanese based on PR description

## Prerequisites

- .NET 8.0 SDK
- Azure DevOps organization with a repository
- Azure OpenAI or AI Foundry endpoint with API access
- Azure DevOps Personal Access Token (PAT) with code review permissions

## Quick Start

### Running Locally

1. **Clone the repository**
   ```bash
   git clone <your-repo-url>
   cd AIReviewer
   ```

2. **Set required environment variables**
   ```bash
   # Azure DevOps Configuration
   export ADO_COLLECTION_URL="https://dev.azure.com/YourOrg"
   export ADO_PROJECT="YourProject"
   export ADO_REPO_NAME="YourRepo"  # or use ADO_REPO_ID
   export ADO_PR_ID="123"
   export ADO_ACCESS_TOKEN="your-pat-token"

   # Azure AI Configuration
   export AI_FOUNDRY_ENDPOINT="https://your-endpoint.openai.azure.com"
   export AI_FOUNDRY_API_KEY="your-api-key"
   export AI_FOUNDRY_DEPLOYMENT="gpt-4o"  # or your model deployment name

   # Policy File
   export POLICY_PATH="./policy/review-policy.md"
   ```

3. **Create a policy file** (e.g., `policy/review-policy.md`)
   ```markdown
   # Code Review Policy

   ## Security
   - Check for SQL injection vulnerabilities
   - Verify proper authentication/authorization
   - Ensure sensitive data is not logged

   ## Code Quality
   - Follow naming conventions
   - Maintain proper error handling
   - Add unit tests for new functionality
   ```

4. **Run the reviewer**
   ```bash
   dotnet run --project AIReviewer/AIReviewer.csproj
   ```

### Command Line Options

AIReviewer supports command-line arguments for specifying custom configuration file paths using the official Microsoft `System.CommandLine` library.

```bash
# View help
dotnet run --project AIReviewer/AIReviewer.csproj -- --help

# Use default settings.ini and no .env file
dotnet run --project AIReviewer/AIReviewer.csproj

# Specify custom settings.ini path
dotnet run --project AIReviewer/AIReviewer.csproj -- --settings /path/to/settings.ini

# Specify custom .env file path
dotnet run --project AIReviewer/AIReviewer.csproj -- --env /path/to/.env

# Specify both custom settings and env file
dotnet run --project AIReviewer/AIReviewer.csproj -- --settings config/prod.ini --env config/.env.prod
```

**Available Options:**
- `-s, --settings <path>`: Path to settings.ini file (default: `settings.ini`)
- `-e, --env <path>`: Path to .env file to load (optional)
- `-h, --help`: Show help message and exit

**Use Cases:**
- **Multiple Environments**: Use different settings files for dev/staging/production
- **Shared Configurations**: Point to a centralized settings file on a network drive
- **Testing**: Use test-specific settings and environment variables
- **CI/CD**: Load environment-specific configurations based on build parameters

**Examples:**
```bash
# Development environment
dotnet run -- -s config/dev.ini -e config/.env.dev

# Production environment  
dotnet run -- -s config/prod.ini -e config/.env.prod

# Testing with custom settings only
dotnet run -- --settings test-settings.ini

# Load .env from a different directory
dotnet run -- --env /secure/credentials/.env
```

### Running in Azure Pipelines

Add the provided `azure-pipeline.yaml` to your repository and configure the required pipeline variables (see Configuration section below).

## Configuration

AIReviewer uses a self-documenting configuration system:

1. **settings.ini** - Static defaults with detailed inline documentation (version controlled)
2. **Environment variables** - Override defaults + provide dynamic values (highest priority)
3. **.env file** - Local development overrides (gitignored)

### Quick Start Configuration

**Step 1:** Review and customize `AIReviewer/settings.ini`
```ini
[AI]
Deployment = o4-mini    # Change to gpt-4 for better quality
Temperature = 0.2       # Keep low for consistent reviews
MaxTokens = 2000        # Increase for more detailed feedback

[Review]
DryRun = false         # Set to true for testing
WarnBudget = 3         # Adjust based on your standards

# ... see settings.ini for all options with detailed explanations
```

**Step 2:** Set required environment variables (copy from `.env.example`):
```bash
# Azure DevOps
ADO_COLLECTION_URL=https://dev.azure.com/MyOrg
ADO_PROJECT=MyProject
ADO_REPO_NAME=MyRepo
ADO_ACCESS_TOKEN=your-pat-token

# Azure AI
AI_FOUNDRY_ENDPOINT=https://your-endpoint.openai.azure.com
AI_FOUNDRY_API_KEY=your-api-key

# Local Repository
LOCAL_REPO_PATH=/path/to/your/repo
```

### 📖 Configuration Documentation

- **`AIReviewer/settings.ini`** - All static settings with detailed inline comments
  - Every setting explained right where it's configured
  - Includes default values, valid ranges, cost impacts, and guidelines
  - Edit without recompiling - just restart the application

- **[PIPELINE.md](PIPELINE.md)** - Environment variable reference
  - Required variables for Azure DevOps and AI
  - Azure Pipeline integration examples
  - Security best practices
  - Override patterns for settings.ini values

- **[.env.example](AIReviewer/.env.example)** - Local development template
  - Copy to `.env` for local development
  - Gitignored to protect secrets

### Key Configuration Benefits

✅ **Self-Documenting** - All settings explained in settings.ini  
✅ **No Recompilation** - Edit settings.ini and restart  
✅ **Secure** - Secrets only in environment variables  
✅ **Flexible** - Override any setting via environment  
✅ **Version Controlled** - Track default changes in git

## Policy Files

Policy files are written in markdown and define your review standards. The AI uses these as guidelines when reviewing code.

### Example Policy Structure

```markdown
# Code Review Policy

## Security
- Validate all user inputs
- Use parameterized queries to prevent SQL injection
- Never log sensitive information (passwords, tokens, PII)
- Ensure proper authentication and authorization checks

## C# Best Practices
- Follow Microsoft naming conventions
- Use `async`/`await` for I/O operations
- Dispose of `IDisposable` resources properly
- Avoid catching generic `Exception` unless re-throwing

## Testing
- Add unit tests for new functionality
- Maintain test coverage above 80%
- Mock external dependencies in tests

## Performance
- Avoid N+1 queries
- Use appropriate collection types
- Cache expensive operations when possible

## Code Quality
- Keep methods under 50 lines
- Limit class complexity
- Remove dead code and commented-out code
- Add XML documentation for public APIs
```

**Tips for Writing Policies:**
- Be specific and actionable
- Use markdown formatting (headers, lists, code blocks)
- Focus on your team's actual priorities
- Include code examples where helpful
- Organize by category (Security, Performance, etc.)

## Language Detection

AIReviewer automatically detects the primary language of your PR based on the PR description and provides review feedback in that language.

### Supported Languages

- **English (en)**: Default language
- **Japanese (ja)**: Detected when PR description contains significant Japanese characters (Hiragana, Katakana, Kanji)

### How Detection Works

1. The system analyzes the PR description text
2. Counts Japanese characters (Hiragana, Katakana, Kanji, Fullwidth forms)
3. If more than 30% of non-whitespace characters are Japanese, reviews are conducted in Japanese
4. Otherwise, reviews default to English

### Example

**PR with English description:**
```
Fix authentication bug in user login flow

This PR addresses an issue where users were unable to log in...
```
→ Reviews provided in **English**

**PR with Japanese description:**
```
ユーザーログインフローの認証バグを修正

このPRでは、ユーザーがログインできなかった問題に対処します...
```
→ Reviews provided in **Japanese** (日本語)

The detected language applies to:
- Code review comments
- PR metadata review feedback
- All issue titles, rationales, and recommendations

## Understanding Review Results

### Issue Severities

- **Error**: Critical issues that must be fixed (blocks approval)
- **Warning**: Issues that should be addressed but don't block approval (up to `WARN_BUDGET`)

### Issue Categories

Issues are automatically categorized:
- `Security`: Security vulnerabilities or concerns
- `Performance`: Performance anti-patterns
- `CodeQuality`: Code maintainability and readability
- `BestPractice`: Framework/language best practices
- `Testing`: Test-related issues

### Approval Logic

The bot automatically sets its vote based on review results:
- **Approve (10)**: No errors AND warnings ≤ `WARN_BUDGET`
- **Wait for Author (0)**: Has errors OR warnings > `WARN_BUDGET`

## Cost Management

AIReviewer makes API calls to Azure OpenAI, which incurs costs. Use these strategies to manage expenses:

### 1. Adjust Review Limits
```bash
# Review fewer files per PR
export MAX_FILES_TO_REVIEW=25

# Report fewer issues per file
export MAX_ISSUES_PER_FILE=3

# Reduce diff size sent to AI
export MAX_PROMPT_DIFF_BYTES=4000
```

### 2. Use Smaller Models
```bash
# Use a more cost-effective model
export AI_FOUNDRY_DEPLOYMENT="gpt-35-turbo"
```

### 3. Enable Dry Run for Testing
```bash
# Test configuration without posting to Azure DevOps
export DRY_RUN=true
```

### 4. Monitor Token Usage

The bot logs token usage for each file review:
```
Token usage - Input: 1500, Output: 300, Total: 1800
```

Use these logs to:
- Identify expensive reviews
- Optimize your policy file
- Adjust `MAX_PROMPT_DIFF_BYTES` to reduce input tokens

## Troubleshooting

### Common Issues

**"Policy file not found"**
- Ensure `POLICY_PATH` points to an existing markdown file
- Use absolute path or path relative to working directory

**"Failed to authenticate with Azure DevOps"**
- Verify `ADO_ACCESS_TOKEN` has Code (Read & Write) permissions
- Check token hasn't expired
- Ensure token has access to the specified project/repository

**"AI review failed"**
- Verify `AI_FOUNDRY_ENDPOINT` and `AI_FOUNDRY_API_KEY` are correct
- Check that `AI_FOUNDRY_DEPLOYMENT` matches your Azure OpenAI deployment name
- Ensure your API key has sufficient quota

**"Too many files to review"**
- Increase `MAX_FILES_TO_REVIEW` if you need to review more files
- Or split large PRs into smaller, focused changes

**"Comments not appearing in PR"**
- Check `DRY_RUN` is set to `false`
- Verify the bot's PAT has permissions to post comments
- Review logs for any API errors

### Debug Logging

For more detailed logs, review the console output. The bot logs:
- Files being reviewed (with sizes)
- Token usage per file
- Issues found
- When limits are exceeded
- Approval decisions

## Azure Pipeline Integration

### Required Pipeline Variables

Configure these as pipeline variables (can be secret):

| Variable | Type | Description |
|----------|------|-------------|
| `AIREVIEWER_REPO_URL` | String | Git URL to clone AIReviewer from |
| `ENV_FILE_CONTENT` | Secret | Contents of .env file with AI credentials |

### Pipeline Environment Variables

These are automatically set by Azure Pipelines:
- `System.AccessToken`: Automatically injected as `ADO_ACCESS_TOKEN`
- `System.CollectionUri`: Used for `ADO_COLLECTION_URL`
- `System.TeamProject`: Used for `ADO_PROJECT`
- `Build.Repository.ID`: Used for `ADO_REPO_ID`
- `System.PullRequest.PullRequestId`: Used for `ADO_PR_ID`

### Example .env File Content

Store in `ENV_FILE_CONTENT` pipeline variable:
```
AI_FOUNDRY_ENDPOINT=https://your-endpoint.openai.azure.com
AI_FOUNDRY_API_KEY=your-api-key-here
AI_FOUNDRY_DEPLOYMENT=gpt-4o
POLICY_PATH=./policy/review-policy.md
MAX_FILES_TO_REVIEW=50
MAX_ISSUES_PER_FILE=5
```

## License

[Your License Here]

## Support

For issues, questions, or contributions, please [open an issue](your-repo-url/issues) or contact your team.
