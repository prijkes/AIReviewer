# Pipeline Environment Variables

This document describes the **dynamic environment variables** that must be set when running AIReviewer. These values change per execution and should **NOT** be in `settings.ini`.

## Overview

AIReviewer requires two types of configuration:

1. **Static Settings** → Configured in `settings.ini`
   - Default AI model parameters
   - Review behavior settings
   - File processing limits
   - These rarely change

2. **Dynamic Variables** → Set via environment variables (THIS DOCUMENT)
   - Azure DevOps credentials
   - Pull request information
   - Repository paths
   - API keys
   - These change per execution

## Configuration Loading Order

```
1. settings.ini          → Read first (static defaults)
2. Environment Variables → Override + add dynamic values
3. Final Configuration   → Merged result used for review
```

---

## Required Environment Variables

These variables **MUST** be set every time AIReviewer runs.

### Azure DevOps Connection

#### `ADO_COLLECTION_URL`

**Type:** `string` (URL)  
**Required:** Yes  
**Example:** `https://dev.azure.com/myorganization`

The base URL of your Azure DevOps organization.

**In Azure Pipelines:**
```yaml
variables:
  - name: ADO_COLLECTION_URL
    value: $(System.CollectionUri)
```

**Local Development:**
```bash
export ADO_COLLECTION_URL="https://dev.azure.com/MyOrg"
```

**How to find:**
- Navigate to your Azure DevOps organization
- The URL in your browser is your collection URL
- Format: `https://dev.azure.com/{organization-name}`

---

#### `ADO_PROJECT`

**Type:** `string`  
**Required:** Yes  
**Example:** `MyProject`

Azure DevOps project containing the repository.

**In Azure Pipelines:**
```yaml
variables:
  - name: ADO_PROJECT
    value: $(System.TeamProject)
```

**Local Development:**
```bash
export ADO_PROJECT="MyProject"
```

---

#### `ADO_REPO_NAME`

**Type:** `string`  
**Required:** Yes (unless `ADO_REPO_ID` is used)  
**Example:** `MyRepository`

Name of the repository to review.

**In Azure Pipelines:**
```yaml
variables:
  - name: ADO_REPO_NAME
    value: $(Build.Repository.Name)
```

**Local Development:**
```bash
export ADO_REPO_NAME="MyRepo"
```

**Note:** Use either `ADO_REPO_NAME` or `ADO_REPO_ID`, not both.

---

#### `ADO_REPO_ID`

**Type:** `string` (GUID)  
**Required:** Yes (unless `ADO_REPO_NAME` is used)  
**Example:** `a1b2c3d4-e5f6-7890-1234-567890abcdef`

Unique identifier of the repository.

**In Azure Pipelines:**
```yaml
variables:
  - name: ADO_REPO_ID
    value: $(Build.Repository.ID)
```

**Local Development:**
```bash
export ADO_REPO_ID="a1b2c3d4-e5f6-7890-1234-567890abcdef"
```

**When to use:** If you have multiple repositories with the same name.

---

#### `ADO_ACCESS_TOKEN`

**Type:** `string` (Personal Access Token)  
**Required:** Yes  
**Security:** **SENSITIVE - Never commit to source control**

Authentication token for Azure DevOps API.

**Required Permissions:**
- Code (Read)
- Code (Status)  
- Pull Request Threads (Read & Write)

**In Azure Pipelines:**
```yaml
variables:
  - name: ADO_ACCESS_TOKEN
    value: $(System.AccessToken)
```

**Local Development:**
```bash
export ADO_ACCESS_TOKEN="your_pat_token_here"
```

**How to create a PAT:**
1. Go to Azure DevOps User Settings
2. Personal Access Tokens → New Token
3. Select required scopes (see above)
4. Copy token (shown only once!)

---

#### `ADO_PR_ID`

**Type:** `integer`  
**Required:** No (can be auto-detected)  
**Example:** `123`

Pull request ID to review.

**In Azure Pipelines:**
```yaml
variables:
  - name: ADO_PR_ID
    value: $(System.PullRequest.PullRequestId)
```

**Local Development:**
```bash
export ADO_PR_ID=123
```

**Auto-detection:** If not set, AIReviewer attempts to infer from `BUILD_SOURCE_VERSION`.

---

#### `BUILD_SOURCE_VERSION`

**Type:** `string` (commit SHA)  
**Required:** No  
**Example:** `a1b2c3d4e5f67890123456789abcdef01234567`

Used to infer PR ID when `ADO_PR_ID` is not set.

**In Azure Pipelines:**
```yaml
variables:
  - name: BUILD_SOURCE_VERSION
    value: $(Build.SourceVersion)
```

**Note:** Automatically provided by Azure Pipelines.

---

### Azure AI Configuration

#### `AI_FOUNDRY_ENDPOINT`

**Type:** `string` (URL)  
**Required:** Yes  
**Example:** `https://myendpoint.openai.azure.com/`

Azure OpenAI resource endpoint URL.

**In Azure Pipelines:**
```yaml
variables:
  - name: AI_FOUNDRY_ENDPOINT
    value: $(AI_ENDPOINT)  # Set as pipeline variable
```

**Local Development:**
```bash
export AI_FOUNDRY_ENDPOINT="https://myendpoint.openai.azure.com/"
```

**How to find:**
1. Azure Portal → Your OpenAI Resource
2. Keys and Endpoint section
3. Copy the Endpoint URL

---

#### `AI_FOUNDRY_API_KEY`

**Type:** `string`  
**Required:** Yes  
**Security:** **SENSITIVE - Never commit to source control**

API key for Azure OpenAI authentication.

**In Azure Pipelines:**
```yaml
variables:
  - name: AI_FOUNDRY_API_KEY
    value: $(AI_API_KEY)  # Set as secret pipeline variable
```

**Local Development:**
```bash
export AI_FOUNDRY_API_KEY="your_api_key_here"
```

**How to find:**
1. Azure Portal → Your OpenAI Resource
2. Keys and Endpoint section
3. Copy KEY 1 or KEY 2

---

### Local Repository

#### `LOCAL_REPO_PATH`

**Type:** `string` (filesystem path)  
**Required:** Yes  
**Example:** `/home/user/myrepo` or `C:\projects\myrepo`

Path to local git repository for file operations.

**Why required:**
- Avoids Azure DevOps API rate limits
- Enables fast file operations
- Required for codebase search
- Needed for diff generation

**In Azure Pipelines:**
```yaml
variables:
  - name: LOCAL_REPO_PATH
    value: $(Build.SourcesDirectory)
```

**Local Development:**
```bash
export LOCAL_REPO_PATH="/path/to/your/repo"
```

**Requirements:**
- Must be a valid git repository
- Must contain `.git` directory
- Must have PR commits available

---

## Optional Environment Variables

These can override values from `settings.ini`.

### Override Static Settings

Any setting in `settings.ini` can be overridden via environment variable using this naming pattern:

**INI Format** → **Environment Variable**
- `[AI] Deployment` → `AI_DEPLOYMENT`
- `[AI] Temperature` → `AI_TEMPERATURE`
- `[FunctionCalling] Enabled` → `FUNCTIONCALLING_ENABLED`
- `[Review] DryRun` → `REVIEW_DRYRUN`
- `[Files] MaxFilesToReview` → `FILES_MAXFILESTOREVIEW`

**Pattern:**
```
[Section] Key  →  SECTION_KEY
```

**Examples:**
```bash
# Override AI model
export AI_DEPLOYMENT="gpt-4"

# Override file limit
export FILES_MAXFILESTOREVIEW=100

# Enable function calling
export FUNCTIONCALLING_ENABLED=true

# Set dry run mode
export REVIEW_DRYRUN=true
```

---

## Complete Examples

### Azure Pipeline YAML

```yaml
trigger:
  - main

pr:
  - main

pool:
  vmImage: 'ubuntu-latest'

variables:
  # Azure DevOps (auto-provided)
  - name: ADO_COLLECTION_URL
    value: $(System.CollectionUri)
  - name: ADO_PROJECT
    value: $(System.TeamProject)
  - name: ADO_REPO_ID
    value: $(Build.Repository.ID)
  - name: ADO_PR_ID
    value: $(System.PullRequest.PullRequestId)
  - name: ADO_ACCESS_TOKEN
    value: $(System.AccessToken)
  
  # Local repo (auto-provided)
  - name: LOCAL_REPO_PATH
    value: $(Build.SourcesDirectory)
  
  # AI settings (set as pipeline variables - KEEP THESE SECRET!)
  - name: AI_FOUNDRY_ENDPOINT
    value: $(AI_ENDPOINT)
  - name: AI_FOUNDRY_API_KEY
    value: $(AI_API_KEY)
  
  # Optional: Override static settings
  - name: FILES_MAXFILESTOREVIEW
    value: 100

steps:
  - script: |
      cd $(Build.SourcesDirectory)/AIReviewer
      dotnet run
    displayName: 'Run AI Reviewer'
```

### Local Development (.env file)

Create a `.env` file (gitignored):

```bash
# Azure DevOps
ADO_COLLECTION_URL=https://dev.azure.com/myorg
ADO_PROJECT=MyProject
ADO_REPO_NAME=MyRepository
ADO_PR_ID=123
ADO_ACCESS_TOKEN=your_pat_token_here

# Azure AI
AI_FOUNDRY_ENDPOINT=https://myendpoint.openai.azure.com/
AI_FOUNDRY_API_KEY=your_api_key_here

# Local repository
LOCAL_REPO_PATH=/home/user/projects/myrepo

# Optional: Override settings.ini values
REVIEW_DRYRUN=true
AI_DEPLOYMENT=gpt-4
FILES_MAXFILESTOREVIEW=100
```

Then run:
```bash
# Load .env file (if using direnv or similar)
dotnet run --project AIReviewer/AIReviewer.csproj
```

---

## Security Best Practices

### Never Commit Secrets

**❌ DON'T:**
- Commit API keys to git
- Put tokens in settings.ini
- Store credentials in code

**✅ DO:**
- Use environment variables
- Use Azure Pipeline secret variables
- Use .env files (gitignored) for local dev

### Pipeline Secret Variables

In Azure DevOps:

1. **Pipeline Variables:**
   - Go to Pipelines → Your Pipeline → Edit
   - Variables → New variable
   - Check "Keep this value secret"
   - Set `AI_FOUNDRY_API_KEY`, `AI_FOUNDRY_ENDPOINT`, etc.

2. **Variable Groups:**
   - Library → Variable Groups
   - Create group for AI settings
   - Mark sensitive values as secret
   - Link group to pipeline

---

## Troubleshooting

### "Configuration failed validation"

**Cause:** Required environment variable is missing

**Solution:** Ensure all required variables are set:
```bash
# Check if variables are set
echo $ADO_COLLECTION_URL
echo $ADO_PROJECT
echo $ADO_ACCESS_TOKEN
echo $AI_FOUNDRY_ENDPOINT
echo $AI_FOUNDRY_API_KEY
echo $LOCAL_REPO_PATH
```

### "settings.ini not found"

**Cause:** Running from wrong directory

**Solution:** 
```bash
# settings.ini must be in same directory as the executable
cd AIReviewer  # or wherever settings.ini is located
dotnet run
```

### "Access denied to Azure DevOps"

**Cause:** Invalid or expired `ADO_ACCESS_TOKEN`

**Solutions:**
- Verify token hasn't expired
- Check token has required permissions:
  - Code (Read)
  - Code (Status)
  - Pull Request Threads (Read & Write)
- Generate new token if needed

### "Invalid API key"

**Cause:** Wrong `AI_FOUNDRY_API_KEY`

**Solutions:**
- Verify key is correct (Azure Portal → Keys and Endpoint)
- Check for extra spaces or quotes in the key
- Regenerate key if needed

---

## Integration with CI/CD

### Azure Pipelines

AIReviewer is designed to run in Azure Pipelines as a PR validation step:

```yaml
trigger: none  # Only run on PR

pr:
  branches:
    include:
      - main
      - develop

steps:
  - task: UseDotNet@2
    inputs:
      version: '8.x'
  
  - script: |
      dotnet run --project AIReviewer/AIReviewer.csproj
    env:
      ADO_ACCESS_TOKEN: $(System.AccessToken)
      AI_FOUNDRY_ENDPOINT: $(AI_ENDPOINT)
      AI_FOUNDRY_API_KEY: $(AI_API_KEY)
    displayName: 'Run AI Code Review'
```

### GitHub Actions

While designed for Azure DevOps, you could adapt for GitHub:

```yaml
name: AI Review
on: [pull_request]

jobs:
  review:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      
      - uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '8.0.x'
      
      - name: Run AI Review
        env:
          ADO_COLLECTION_URL: ${{ secrets.ADO_URL }}
          ADO_PROJECT: ${{ secrets.ADO_PROJECT }}
          # ... other variables
        run: |
          cd AIReviewer
          dotnet run
```

---

## Summary

**Static settings** (in `settings.ini`):
- AI model parameters
- Review behavior
- File limits
- Rarely change

**Dynamic variables** (environment variables):
- Credentials and API keys
- PR information
- Repository paths
- Change per execution

**Always set these via environment variables:**
- `ADO_COLLECTION_URL`
- `ADO_PROJECT`
- `ADO_REPO_NAME` or `ADO_REPO_ID`
- `ADO_ACCESS_TOKEN`
- `AI_FOUNDRY_ENDPOINT`
- `AI_FOUNDRY_API_KEY`
- `LOCAL_REPO_PATH`

**Never put in settings.ini:**
- API keys
- Access tokens
- Sensitive credentials
- PR-specific information
