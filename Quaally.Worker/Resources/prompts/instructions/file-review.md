Apply the policy rubric.

# CRITICAL - Understanding Git Unified Diff Format

Git unified diffs show changes between two versions of files. Understanding the format is essential for accurate line number mapping when commenting on code changes.

## Diff Structure Overview

A complete diff consists of:
1. **File headers** - Metadata about the files being compared
2. **Hunks** - Sections of changes, each with a hunk header and content lines

## 1. File Headers (IGNORE for line counting)

These lines provide metadata but are NOT part of the actual file content:

```diff
diff --git a/file.txt b/file.txt           # Comparison declaration
index abc1234..def5678 100644              # Git object hashes and mode
--- a/file.txt                             # Old file path
+++ b/file.txt                             # New file path
```

Special file operation indicators:
- `deleted file mode 100644` - File was deleted
- `new file mode 100644` - File was created
- `rename from/to` - File was renamed
- `copy from/to` - File was copied

**IMPORTANT**: Never count these header lines when calculating line numbers.

## 2. Hunk Headers (The `@@` Lines)

Format: `@@ -<old_start>,<old_count> +<new_start>,<new_count> @@ [optional context]`

Breaking it down:
- `@@` - Hunk header delimiter
- `-<old_start>,<old_count>` - Original file: starts at line `old_start`, spans `old_count` lines
- `+<new_start>,<new_count>` - New file: starts at line `new_start`, spans `new_count` lines
- Optional context after second `@@` is informational only

### Examples:

```diff
@@ -10,7 +10,9 @@ function example()
```
- Old file: 7 lines starting at line 10 (lines 10-16)
- New file: 9 lines starting at line 10 (lines 10-18)

```diff
@@ -1,115 +0,0 @@
```
- Old file: 115 lines starting at line 1 (entire file being deleted)
- New file: 0 lines at position 0 (file doesn't exist in new version)

```diff
@@ -0,0 +1,50 @@
```
- Old file: 0 lines (file doesn't exist in old version)
- New file: 50 lines starting at line 1 (new file being created)

## 3. Content Lines and Prefixes

Each content line in a hunk has a one-character prefix:

- **` ` (space)** - Context line (unchanged, exists in both versions)
- **`-`** - Deleted line (exists only in old version)
- **`+`** - Added line (exists only in new version)
- **`\`** - Meta indicator (e.g., `\ No newline at end of file`) - DO NOT COUNT

## 4. Line Number Calculation Rules

### For DELETED Files (old file reference)

Use the `-<old_start>,<old_count>` numbers:

```diff
@@ -1,5 +0,0 @@
-line one          <- Original line 1
-line two          <- Original line 2
-line three        <- Original line 3
-line four         <- Original line 4
-line five         <- Original line 5
```

**Calculation**: Start at `old_start` (1), increment for each `-` line.

### For NEW Files (new file reference)

Use the `+<new_start>,<new_count>` numbers:

```diff
@@ -0,0 +1,5 @@
+line one          <- New line 1
+line two          <- New line 2
+line three        <- New line 3
+line four         <- New line 4
+line five         <- New line 5
```

**Calculation**: Start at `new_start` (1), increment for each `+` line.

### For MODIFIED Files

Track TWO line counters simultaneously:

- **Old line counter**: Starts at `old_start`, increments for ` ` and `-` lines
- **New line counter**: Starts at `new_start`, increments for ` ` and `+` lines

```diff
@@ -10,7 +10,8 @@
 unchanged line    <- Old line 10, New line 10
 context line      <- Old line 11, New line 11
-deleted line      <- Old line 12 (NOT in new file)
 more context      <- Old line 13, New line 12
+added line        <- New line 13 (NOT in old file)
+another add       <- New line 14 (NOT in old file)
 final context     <- Old line 14, New line 15
```

**Step-by-step**:
1. ` unchanged line` - Old: 10, New: 10 (both increment)
2. ` context line` - Old: 11, New: 11 (both increment)
3. `-deleted line` - Old: 12 (old increments, new doesn't)
4. ` more context` - Old: 13, New: 12 (both increment)
5. `+added line` - New: 13 (new increments, old doesn't)
6. `+another add` - New: 14 (new increments, old doesn't)
7. ` final context` - Old: 14, New: 15 (both increment)

## 5. Multiple Hunks in One Diff

Files can have multiple separate change sections:

```diff
@@ -10,3 +10,4 @@
 context
-old line
+new line
 context
@@ -50,2 +51,3 @@
 context
+inserted line
 context
```

Each hunk is independent. Start counting fresh from each hunk header's line numbers.

## 6. Commenting on Changes - Practical Guide

### When commenting on a DELETED line:
- Reference the **old file** line number
- Use the `-<old_start>` position from hunk header
- Count through `-` and ` ` (space) lines only

### When commenting on an ADDED line:
- Reference the **new file** line number
- Use the `+<new_start>` position from hunk header
- Count through `+` and ` ` (space) lines only

### When commenting on a MODIFIED section:
- Reference the **new file** line number (where the code will be)
- Track the new file counter through ` ` and `+` lines

## 7. Common Pitfalls to Avoid

**DON'T** count header lines (`diff --git`, `---`, `+++`, `index`, etc.)
**DON'T** count the `@@` hunk header itself
**DON'T** count `\ No newline at end of file` lines
**DON'T** use diff line numbers - always calculate actual file line numbers
**DON'T** mix up `-` and `+` positions

**DO** start counting from the hunk header's start position
**DO** increment only for relevant line types (-, +, or space)
**DO** reset your counter for each new hunk
**DO** reference the appropriate version (old for deletions, new for additions)

## 8. Quick Reference Summary

| File Operation | Use Which Counter | Line Prefix to Count |
|----------------|-------------------|----------------------|
| Deleted file   | Old (`-X,Y`)      | `-` and ` ` (space)  |
| New file       | New (`+X,Y`)      | `+` and ` ` (space)  |
| Modified file  | Both (track separately) | `-` and ` ` for old, `+` and ` ` for new |
| Comment on deletion | Old line number | N/A |
| Comment on addition | New line number | N/A |
| Comment on modification | New line number | N/A |

**Final Rule**: Always report line numbers as they appear in the ACTUAL SOURCE FILE, not the position within the diff output.
