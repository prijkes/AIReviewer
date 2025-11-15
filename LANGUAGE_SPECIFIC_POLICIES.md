# Language-Specific Policies

Quaaly now supports language-specific review policies and prompts based on file type. This allows for more accurate and relevant code reviews tailored to each programming language's best practices.

## Overview

When reviewing code, Quaaly:
1. **Detects the programming language** based on file extension
2. **Loads language-specific policies** (if available) from the `policy/` directory
3. **Uses language-specific prompts** that emphasize relevant best practices
4. **Falls back to general policy** if no language-specific policy exists

## Supported Languages

| Language | File Extensions | Policy File | Display Name |
|----------|----------------|-------------|--------------|
| C#/.NET  | `.cs` | `review-policy-csharp.md` | C#/.NET |
| C++      | `.cpp`, `.cxx`, `.cc`, `.hpp`, `.hxx`, `.h` | `review-policy-cpp.md` | C++ |
| C        | `.c` | `review-policy-c.md` | C |
| C++/CLI  | `.cli` | `review-policy-cli.md` | C++/CLI |
| General  | All others | `review-policy.md` | Unknown |

## How It Works

### 1. Language Detection

The system automatically detects the programming language based on file extension:

```csharp
var programmingLanguage = ProgrammingLanguageDetector.DetectLanguage(filePath);
// Example: "Program.cs" → ProgrammingLanguage.CSharp
// Example: "main.cpp" → ProgrammingLanguage.Cpp
```

### 2. Policy Loading

The `PolicyLoader` attempts to load a language-specific policy file:

```
Base policy path: ./policy/review-policy.md

Language-specific paths:
- C#: ./policy/review-policy-csharp.md
- C++: ./policy/review-policy-cpp.md
- C: ./policy/review-policy-c.md
- C++/CLI: ./policy/review-policy-cli.md
```

If a language-specific policy doesn't exist, it falls back to the general policy.

### 3. Language-Specific Prompts

Each language has a tailored system prompt that emphasizes relevant concerns:

**C#/.NET Prompt:**
```
You are an expert C#/.NET code reviewer bot...
Focus on .NET best practices, LINQ usage, async/await patterns, 
memory management, and framework-specific guidelines.
```

**C++ Prompt:**
```
You are an expert C++ code reviewer bot...
Focus on memory safety (RAII, smart pointers), modern C++ standards,
performance optimization, undefined behavior prevention...
```

**C Prompt:**
```
You are an expert C code reviewer bot...
Focus on memory safety, buffer overflows, null pointer dereferences,
proper error handling, and undefined behavior...
```

**C++/CLI Prompt:**
```
You are an expert C++/CLI code reviewer bot...
Focus on interop between native C++ and managed .NET code,
proper resource management in mixed environments, marshaling...
```

## Creating Language-Specific Policies

### Policy File Naming Convention

Language-specific policy files follow this pattern:
```
{base-filename}-{language-suffix}.md
```

Examples:
- `review-policy-csharp.md`
- `review-policy-cpp.md`
- `review-policy-c.md`
- `review-policy-cli.md`

### Policy File Structure

Each policy file should be written in Markdown and include:

1. **Language/Framework Features**: Specific to that language
2. **Memory Management**: How the language handles memory
3. **Error Handling**: Best practices for exceptions/errors
4. **Performance**: Language-specific performance considerations
5. **Security**: Common security vulnerabilities
6. **Best Practices**: Coding standards and conventions
7. **Common Pitfalls**: Language-specific anti-patterns

### Example Policy File

```markdown
# C++ Code Review Policy

## Memory Management & RAII
- Use RAII for all resources
- Prefer smart pointers over raw pointers
- Use std::make_unique and std::make_shared

## Modern C++ Features
- Use auto for complex types
- Leverage range-based for loops
- Use lambda expressions for callbacks

## Common Pitfalls to Avoid
- Returning references to local variables
- Slicing objects
- Raw pointer ownership
```

## Configuration

### Default Configuration

The base policy path is configured in `settings.ini`:

```ini
[Review]
PolicyPath = ./policy/review-policy.md
```

This path is used as the base for loading both general and language-specific policies.

### Environment Variables

You can override the policy path using environment variables:

```bash
export ReviewerOptions__PolicyPath="./custom-policy/review-policy.md"
```

## Adding Support for New Languages

To add support for a new programming language:

### 1. Update ProgrammingLanguageDetector

Add the language to the enum in `Quaaly/Utils/ProgrammingLanguageDetector.cs`:

```csharp
public enum ProgrammingLanguage
{
    CSharp,
    Cpp,
    C,
    Cli,
    Python,  // New language
    Unknown
}
```

Update the `DetectLanguage` method to recognize the file extensions:

```csharp
return extension switch
{
    ".cs" => ProgrammingLanguage.CSharp,
    ".cpp" or ".cxx" or ".cc" => ProgrammingLanguage.Cpp,
    ".py" => ProgrammingLanguage.Python,  // New
    // ...
};
```

Update the helper methods:

```csharp
public static string GetDisplayName(ProgrammingLanguage language)
{
    return language switch
    {
        ProgrammingLanguage.Python => "Python",  // New
        // ...
    };
}

public static string GetPolicySuffix(ProgrammingLanguage language)
{
    return language switch
    {
        ProgrammingLanguage.Python => "python",  // New
        // ...
    };
}
```

### 2. Add Language-Specific Prompt

Update `PromptBuilder.cs` to add a language-specific prompt:

```csharp
private static readonly Dictionary<ProgrammingLanguageDetector.ProgrammingLanguage, string> LanguagePrompts = new()
{
    [ProgrammingLanguageDetector.ProgrammingLanguage.Python] = """
        You are an expert Python code reviewer bot...
        Focus on PEP 8, type hints, list comprehensions, context managers...
        """,
    // ...
};
```

### 3. Create Policy File

Create a new policy file: `policy/review-policy-python.md`

### 4. Add Unit Tests

Add tests to `ProgrammingLanguageDetectorTests.cs`:

```csharp
[Theory]
[InlineData("script.py", ProgrammingLanguageDetector.ProgrammingLanguage.Python)]
public void DetectLanguage_PythonFiles_ReturnsPython(string filePath, ...)
{
    // Test implementation
}
```

## Examples

### Mixed Codebase

For a project with multiple languages:

```
src/
  ├── Services/
  │   ├── UserService.cs      → Uses C# policy
  │   └── DatabaseService.cs  → Uses C# policy
  ├── Native/
  │   ├── Core.cpp            → Uses C++ policy
  │   ├── Core.h              → Uses C++ policy
  │   └── Utils.c             → Uses C policy
  └── Interop/
      └── Wrapper.cli         → Uses C++/CLI policy
```

Each file is reviewed with the appropriate language-specific policy and prompt.

### Logging

The system logs language detection for each file:

```
[Debug] File src/Services/UserService.cs detected as C#/.NET
[Info] Loaded language-specific policy file ./policy/review-policy-csharp.md for C#/.NET (chars: 3542)

[Debug] File src/Native/Core.cpp detected as C++
[Info] Loaded language-specific policy file ./policy/review-policy-cpp.md for C++ (chars: 4123)
```

### Fallback Behavior

If a language-specific policy doesn't exist:

```
[Debug] File script.py detected as Unknown
[Debug] Language-specific policy not found at ./policy/review-policy-general.md, falling back to general policy
[Info] Loaded general policy file ./policy/review-policy.md (chars: 1234)
```

## Best Practices

1. **Keep policies focused**: Each language policy should emphasize that language's unique concerns
2. **Avoid duplication**: Common guidelines go in the general policy
3. **Update regularly**: Keep policies current with language evolution (e.g., new C++ standards)
4. **Be specific**: Provide concrete examples of good and bad patterns
5. **Prioritize**: Focus on the most common and impactful issues

## Benefits

- **Higher accuracy**: Language-specific prompts help the AI identify relevant issues
- **Better context**: Policies tailored to each language's ecosystem
- **Reduced noise**: Less generic advice, more actionable feedback
- **Easier maintenance**: Separate policies are easier to update independently
- **Flexibility**: Easy to customize per language without affecting others

## Troubleshooting

### Policy not loading

Check that:
1. The policy file exists in the `policy/` directory
2. The file naming follows the convention: `review-policy-{suffix}.md`
3. The `PolicyPath` setting points to the correct base path
4. File permissions allow reading the policy files

### Wrong language detected

Verify:
1. The file extension is correct
2. The extension is mapped in `ProgrammingLanguageDetector.DetectLanguage()`
3. Check the logs for language detection messages

### AI giving generic advice

Ensure:
1. The language-specific policy file exists and is loaded (check logs)
2. The policy content is specific enough to that language
3. The language-specific prompt in `PromptBuilder` is being used

## Performance Considerations

- **Policy caching**: Policies are cached after first load to improve performance
- **Parallel reviews**: Files are reviewed in parallel, each with its own policy
- **Minimal overhead**: Language detection is a simple file extension check

## Future Enhancements

Potential improvements for language-specific policies:

1. Support for more languages (JavaScript, TypeScript, Python, Java, Go, etc.)
2. Content-based language detection for ambiguous extensions (e.g., .h files)
3. Project-specific policy overrides
4. Per-language configuration options (e.g., different severity thresholds)
5. Language-specific issue templates
