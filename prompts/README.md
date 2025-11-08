# Prompt Templates

This directory contains externalized prompt templates for AIReviewer. Prompts are stored in markdown files for easy editing without recompilation.

## Directory Structure

```
prompts/
├── system/           # Language-specific base system prompts
│   ├── base-csharp.md
│   ├── base-cpp.md
│   ├── base-c.md
│   ├── base-cli.md
│   └── base-unknown.md
├── instructions/     # Task-specific instruction prompts
│   ├── file-review.md
│   └── metadata-review.md
└── language/         # Response language directives
    ├── english.md
    └── japanese.md
```

## Prompt Components

### System Prompts (`system/`)

Define the AI's role and expertise for different programming languages:

- **base-csharp.md** - C#/.NET code review expertise
- **base-cpp.md** - C++ code review expertise  
- **base-c.md** - C code review expertise
- **base-cli.md** - C++/CLI code review expertise
- **base-unknown.md** - Generic code review (fallback)

These prompts set the foundation for how the AI approaches code reviews based on the detected programming language.

### Instruction Prompts (`instructions/`)

Define what actions the AI should take:

- **file-review.md** - Instructions for reviewing file diffs
- **metadata-review.md** - Instructions for reviewing PR metadata (title, description, commits)

### Language Prompts (`language/`)

Specify the response language for reviews:

- **english.md** - Directive to provide feedback in English
- **japanese.md** - Directive to provide feedback in Japanese

## Editing Prompts

### Benefits of External Prompts

✅ **No Recompilation** - Edit prompts and restart the application  
✅ **Version Control** - Track changes to prompts separately from code  
✅ **Easy A/B Testing** - Swap prompt files to test variations  
✅ **Team Collaboration** - Non-developers can refine prompts  
✅ **Rapid Iteration** - Quickly improve review quality

### Best Practices

1. **Keep prompts focused** - Each file should have a single, clear purpose
2. **Be specific** - Provide concrete guidance rather than vague instructions
3. **Test changes** - Use dry run mode to verify prompt modifications
4. **Version control** - Commit prompt changes with descriptive messages
5. **Document rationale** - Add comments explaining why specific phrasing was chosen

### Example: Modifying a System Prompt

To adjust the C# review expertise:

```bash
# Edit the prompt file
nano prompts/system/base-csharp.md

# Make your changes, e.g., add emphasis on async/await patterns
# Save the file

# Restart AIReviewer (no recompilation needed)
dotnet run --project AIReviewer/AIReviewer.csproj
```

## Configuration

The prompts base path is configured in `settings.ini`:

```ini
[Review]
PromptsBasePath = ./prompts
```

This can be overridden with the `REVIEW_PROMPTSBASEPATH` environment variable if needed.

## How Prompts Are Combined

For file reviews, prompts are assembled in this order:

1. **System Prompt** = `system/<language>.md` + Policy + `language/<lang>.md`
2. **User Prompt** = File path + Diff + `instructions/file-review.md`

For metadata reviews:

1. **System Prompt** = `system/base-unknown.md` + Policy + `instructions/metadata-review.md` + `language/<lang>.md`  
2. **User Prompt** = PR title + description + commits

## Advanced Customization

### Adding a New Programming Language

1. Create `prompts/system/base-<language>.md` with appropriate expertise
2. Update `PromptLoader.GetSystemPromptFileName()` to recognize the new language
3. Update `ProgrammingLanguageDetector` to detect the language from file extensions

### Adding Response Languages

1. Create `prompts/language/<language>.md` with the language directive
2. Update `PromptLoader.LoadLanguageInstructionAsync()` to map the language code
3. Update `LanguageDetector` to detect the language from PR descriptions

## Troubleshooting

**Problem**: Prompts not loading  
**Solution**: Check that `PromptsBasePath` in `settings.ini` points to the correct directory

**Problem**: Reviews don't reflect prompt changes  
**Solution**: Ensure you restarted the application after editing prompt files

**Problem**: File not found errors  
**Solution**: Verify all expected prompt files exist in their respective subdirectories

## Related Documentation

- [settings.ini](../AIReviewer/settings.ini) - Configuration options
- [PromptLoader.cs](../AIReviewer/AI/PromptLoader.cs) - Prompt loading implementation
- [PromptBuilder.cs](../AIReviewer/AI/PromptBuilder.cs) - Prompt assembly logic
