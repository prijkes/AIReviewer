Apply the policy rubric. Report up to 5 actionable issues. Leave summary empty.

**CRITICAL - Line Number Mapping for Unified Diffs:**

When reading a unified diff, you must correctly map diff content to actual file line numbers:

1. **Ignore diff header lines** - These are NOT part of the file content:
   - Lines starting with `diff --git`
   - Lines starting with `deleted file mode`, `new file mode`, `index`
   - Lines starting with `---` or `+++`
   
2. **Find the hunk header** - Lines starting with `@@`:
   - Format: `@@ -<old_start>,<old_count> +<new_start>,<new_count> @@`
   - Example: `@@ -1,115 +0,0 @@` means original file starts at line 1 with 115 lines
   - For **deleted files**: Use the numbers after `-` (old file position)
   - For **modified/new files**: Use the numbers after `+` (new file position)

3. **Count line numbers from the hunk header**:
   - The FIRST content line after `@@` is the starting line number from the hunk header
   - Each subsequent line with `-` (deleted files) or ` ` (context) or `+` (new files) increments the count
   - Lines with `\\ No newline at end of file` do NOT count
   
4. **Example for a deleted file**:
   ```
   @@ -1,115 +0,0 @@
   -# azure-pipelines.yml     <- This is ORIGINAL line 1
   -trigger: none             <- This is ORIGINAL line 2  
   -pr:                       <- This is ORIGINAL line 3
   ```

5. **Report line numbers as they appear in the ACTUAL SOURCE FILE**, not the diff line position.

**For deleted files specifically**: Count from the `@@ -X,Y` starting line number (X) and map each `-` line sequentially.
