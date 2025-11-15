# OpenAI Function Calling for Context Retrieval

## Overview

The AI Reviewer now supports **OpenAI function calling**, allowing the AI to retrieve additional context during code reviews. When enabled, the AI can intelligently request more information about the codebase to provide more informed and accurate reviews.

## How It Works

### Standard Review Flow (Without Function Calling)
```
User → AI: Review this diff
AI → User: Here are the issues I found (based only on the diff)
```

### Enhanced Review Flow (With Function Calling)
```
User → AI: Review this diff that changes SomeMethod()
AI → System: "I need to see the full file to understand the context"
System → AI: [Full file content]
AI → System: "Let me search for where this method is called"
System → AI: [Search results showing usage]
AI → User: "Issue: This change breaks the contract because..."
```

## Available Functions

When function calling is enabled, the AI has access to these tools:

### 1. `get_full_file_content`
Gets the complete content of a file from the target branch.

**Use Case:** Understanding the full context of a file beyond what's in the diff.

**Example:** When reviewing a change to a method, the AI can see the entire class to understand dependencies and invariants.

### 2. `get_file_at_commit`
Gets the content of a file at a specific commit or branch.

**Use Case:** Comparing versions or understanding historical context.

**Example:** Checking how a function looked before changes were made.

### 3. `search_codebase`
Searches the entire codebase for files containing specific text or patterns.

**Use Case:** Finding where functions, classes, or interfaces are defined or used.

**Example:** Locating all callers of a modified method to assess impact.

### 4. `get_related_files`
Finds files related to a given file through imports, namespaces, or references.

**Use Case:** Understanding the impact of changes on related code.

**Example:** Finding all files in the same namespace or that import the changed file.

### 5. `get_file_history`
Gets the commit history for a specific file.

**Use Case:** Understanding how a file has evolved over time.

**Example:** Seeing who has worked on a file and what changes were made recently.

## Configuration

### Enabling Function Calling

Add this environment variable (or .env file entry):

```bash
ENABLE_FUNCTION_CALLING=true
```

Or set it in your configuration:

```csharp
{
  "EnableFunctionCalling": true
}
```

### Complete Configuration Example

```bash
# Azure DevOps Configuration
ADO_COLLECTION_URL=https://dev.azure.com/yourorg
ADO_PROJECT=YourProject
ADO_REPO_NAME=YourRepo
ADO_ACCESS_TOKEN=your_pat_token

# Azure OpenAI Configuration
AI_FOUNDRY_ENDPOINT=https://your-endpoint.openai.azure.com/
AI_FOUNDRY_DEPLOYMENT=gpt-4
AI_FOUNDRY_API_KEY=your_api_key

# Enable Function Calling
ENABLE_FUNCTION_CALLING=true
```

## Benefits

### ✅ Improved Review Quality
- The AI can see the full context, not just the diff
- Better understanding of how changes affect the overall system
- More accurate detection of breaking changes

### ✅ Deeper Analysis
- Can verify assumptions by looking at related code
- Understands dependencies and relationships
- Identifies non-obvious issues

### ✅ Example Scenarios

**Scenario 1: Method Signature Change**
```csharp
// Diff shows only:
- public void Process(string data)
+ public void Process(string data, bool validate)
```

Without function calling:
> "Parameter added to method"

With function calling:
> "This change breaks 15 callers in the codebase. Recommend: Add overload instead of modifying signature."

**Scenario 2: Interface Implementation**
```csharp
// Diff shows a class implementing IService
public class MyService : IService
```

Without function calling:
> "Class implements IService"

With function calling (searches for IService):
> "This implementation is missing the required Dispose() method defined in IService"

## Trade-offs

### Costs
- **Increased API Costs**: Each function call adds tokens and requires additional API requests
- **Higher Latency**: Reviews take longer (2-5 API round trips vs 1)
- **Rate Limits**: May hit rate limits faster with frequent use

### Typical Review Metrics

| Metric | Without Functions | With Functions |
|--------|------------------|----------------|
| API Calls | 1 per file | 1-5 per file |
| Review Time | 2-5 seconds | 5-15 seconds |
| Token Usage | ~1,000 tokens | ~3,000-8,000 tokens |
| Cost per Review | $0.01 | $0.03-$0.08 |

## Safeguards

The implementation includes several safeguards:

1. **Maximum Function Calls**: Limited to 5 calls per review to prevent infinite loops
2. **Error Handling**: Function errors don't crash the review
3. **Timeouts**: Network timeouts prevent hangs
4. **Token Limits**: Same max token limits apply
5. **Optional Feature**: Disabled by default, opt-in only

## Implementation Details

### Architecture

```
ReviewPlanner
  ├─> Sets PR context in ReviewContextRetriever
  └─> Calls AzureFoundryAiClient.ReviewAsync()
       ├─> Adds function definitions to chat options
       ├─> Sends initial review request
       └─> HandleFunctionCallsAsync() loop:
            ├─> AI requests function call
            ├─> ExecuteFunctionAsync() calls ReviewContextRetriever
            ├─> AdoSdkClient fetches data from Azure DevOps
            ├─> Returns result to AI
            └─> AI continues review with new context
```

### Key Classes

- **`ReviewContextRetriever`**: Executes function calls, wraps AdoSdkClient
- **`FunctionDefinitions`**: Defines available functions for OpenAI
- **`AzureFoundryAiClient`**: Handles function calling protocol
- **`AdoSdkClient`**: Extended with new methods for file operations

## Logging

When function calling is enabled, you'll see logs like:

```
[11:30:45] AI requested function calls (iteration 1/5)
[11:30:45] Executing function: get_full_file_content with args: {"filePath":"src/Services/MyService.cs"}
[11:30:46] Retrieved 5432 bytes for src/Services/MyService.cs
[11:30:46] Function get_full_file_content returned 5432 chars
[11:30:47] AI reviewed MyService.cs: 3 issues found in 2341ms
[11:30:47] Token usage - Input: 4521, Output: 892, Total: 5413
```

## Troubleshooting

### Function calling not working?

1. **Check configuration**: Ensure `ENABLE_FUNCTION_CALLING=true`
2. **Verify model**: Some models don't support function calling (use GPT-4 or later)
3. **Check logs**: Look for "AI requested function calls" messages
4. **API errors**: Ensure your Azure DevOps token has read permissions

### High costs?

1. **Disable for simple PRs**: Only enable for complex reviews
2. **Reduce max tokens**: Lower `AI_MAX_TOKENS` to reduce context size
3. **Use cheaper model**: Try GPT-3.5-turbo for simple reviews

### Slow reviews?

1. **Expected behavior**: Function calling adds latency
2. **Network issues**: Check Azure DevOps connectivity
3. **Large codebases**: Searches might take time

## Future Enhancements

Potential future improvements:

- [ ] Caching of frequently accessed files
- [ ] Parallel function execution
- [ ] More granular function control (enable/disable specific functions)
- [ ] Semantic code search using embeddings
- [ ] AST-based code analysis functions
- [ ] Pull request comparison functions

## References

- [OpenAI Function Calling Documentation](https://platform.openai.com/docs/guides/function-calling)
- [Azure OpenAI Function Calling](https://learn.microsoft.com/en-us/azure/ai-services/openai/how-to/function-calling)

## Contributing

When adding new functions:

1. Add method to `ReviewContextRetriever`
2. Add corresponding method to `AdoSdkClient` if needed
3. Define function in `FunctionDefinitions`
4. Add switch case in `AzureFoundryAiClient.ExecuteFunctionAsync()`
5. Update this documentation

## License

Same as the main Quaally project.
