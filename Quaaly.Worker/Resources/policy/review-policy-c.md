# C Code Review Policy

Policy for reviewing C code with emphasis on memory safety, portability, and avoiding undefined behavior.

## Memory Management

- Always **free** allocated memory - check for memory leaks
- Set pointers to **NULL** after freeing them
- Check return values of **malloc/calloc/realloc** - they can return NULL
- Use **calloc** for zero-initialized memory, or memset after malloc
- Avoid **memory leaks** - ensure all allocations have corresponding frees
- Don't **double-free** - freeing the same pointer twice is undefined behavior
- Don't access memory **after freeing** - it's undefined behavior

## Pointer Safety

- Always initialize pointers (to NULL if not immediately assigned)
- Check pointers for **NULL** before dereferencing
- Avoid **dangling pointers** - pointers to freed or out-of-scope memory
- Be careful with **pointer arithmetic** - easy to go out of bounds
- Don't return pointers to **local variables** - they'll be invalid
- Validate **array indices** to prevent buffer overflows
- Use `const` pointers when data shouldn't be modified

## Buffer Safety & String Handling

- Check **buffer sizes** before copying - prevent buffer overflows
- Use **strncpy** instead of strcpy, **snprintf** instead of sprintf
- Always **null-terminate** strings explicitly
- Validate **string lengths** before operations
- Be aware of **off-by-one errors** in buffer sizes (need room for null terminator)
- Use **strnlen** to safely check string length with a maximum
- Consider using safer alternatives like `strlcpy` if available

## Error Handling

- Always check **return values** - don't ignore errors
- Use **errno** for system call errors - check and reset appropriately
- Return **error codes** from functions that can fail
- Provide **meaningful return values** - distinguish different error conditions
- Document error conditions in function comments
- Set **errno** appropriately when functions fail

## Integer Operations

- Check for **integer overflow/underflow** before operations
- Be careful with **signed vs unsigned** comparisons
- Use appropriate **integer types** - int, size_t, ptrdiff_t, etc.
- Avoid **signed integer overflow** - it's undefined behavior
- Use **unsigned types** for bit operations
- Check for **division by zero** before dividing

## Function Design

- Keep functions **focused** on a single task
- Limit function **length** - easier to understand and test
- Use **const** for parameters that shouldn't be modified
- Validate **input parameters** at function entry
- Document **preconditions and postconditions**
- Use **static** for functions that don't need external linkage

## Macros & Preprocessor

- Use **parentheses** around macro parameters to avoid precedence issues
- Consider using **inline functions** instead of macros when possible
- Use **ALL_CAPS** for macro names
- Avoid **side effects** in macro arguments
- Use **#ifndef guards** in header files
- Be careful with **multi-statement macros** - wrap in do-while(0)

## Type Safety

- Use **typedef** for complex types to improve readability
- Avoid **type punning** through pointer casts
- Use proper **casting** - understand what you're converting
- Be explicit about **integer promotion** and conversions
- Don't mix **signed and unsigned** without careful consideration
- Use **struct** for related data instead of parallel arrays

## Standard Library

- Use standard library functions when available - they're well-tested
- Understand the **difference between** similar functions (e.g., strcpy vs strncpy)
- Check **man pages** for proper usage and edge cases
- Be aware of **platform differences** in standard library
- Prefer **standard C** over platform-specific extensions for portability

## Portability

- Use **fixed-width integer types** from stdint.h when size matters
- Don't assume **sizeof** for standard types - use sizeof() operator
- Be aware of **endianness** issues when reading/writing binary data
- Use **standard types** like size_t, ptrdiff_t appropriately
- Avoid **platform-specific** features without #ifdef guards
- Test on **multiple platforms** if targeting multiple systems

## Concurrency (if applicable)

- Protect shared data with **mutexes/locks**
- Avoid **data races** - synchronize access to shared variables
- Be careful with **volatile** - it's not sufficient for synchronization
- Use **atomic operations** or locks for shared counters
- Consider using **pthread** or C11 threads.h for threading
- Document **thread-safety** of functions

## Security

- Validate **ALL user input** - never trust external data
- Check **array bounds** before access - prevent buffer overruns
- Sanitize input before using in **system calls** or **exec** family
- Use **snprintf** to prevent format string vulnerabilities
- Be cautious with **format strings** - don't use user input as format
- Clear sensitive data from memory when done (consider memset_s)

## Code Quality

- Use **meaningful variable names** - avoid single letters except for loops
- Add **comments** for complex algorithms or non-obvious code
- Keep **indentation consistent** - use a consistent style guide
- Remove **dead code** - don't comment out code, use version control
- Avoid **magic numbers** - use named constants (#define or const)
- Group **related functionality** together

## Testing

- Write **unit tests** for functions
- Test **edge cases** - NULL pointers, empty strings, boundary values
- Test **error paths** - ensure error handling works correctly
- Use tools like **Valgrind** to detect memory errors
- Enable compiler warnings and fix them (**-Wall -Wextra**)
- Consider **static analysis tools** (Coverity, clang-tidy)

## Common Pitfalls

- **Using uninitialized variables** - always initialize
- **Buffer overflows** - check sizes before copying
- **Use-after-free** - don't access freed memory
- **Null pointer dereference** - check before dereferencing
- **Signed integer overflow** - undefined behavior
- **Improper array indexing** - off-by-one errors
- **Mixing malloc/free with other allocators** - be consistent
- **Forgetting to close files/sockets** - resource leaks
