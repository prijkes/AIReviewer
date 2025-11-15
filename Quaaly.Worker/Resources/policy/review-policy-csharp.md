# C#/.NET Code Review Policy

Policy for reviewing C# and .NET code, including best practices, framework-specific guidelines, and common pitfalls.

## .NET Framework & Language Features

- Use **async/await** properly - avoid `async void` except for event handlers
- Prefer **LINQ** for collections but watch out for performance implications with large datasets
- Use **nullable reference types** (C# 8+) and handle nullability properly
- Leverage **pattern matching** for cleaner conditional logic
- Use **records** for immutable data transfer objects
- Prefer **expression-bodied members** when they improve readability

## Memory Management & Performance

- **Dispose resources properly** - use `using` statements or `IDisposable`
- Avoid unnecessary **boxing/unboxing** of value types
- Use **string interpolation** (`$""`) or `StringBuilder` instead of string concatenation in loops
- Consider `Span<T>` and `Memory<T>` for high-performance scenarios
- Use **object pooling** for frequently allocated objects
- Avoid **capturing variables** in closures unnecessarily

## LINQ & Collections

- Don't call `Count()` when you can use `Any()`
- Use `ToList()` or `ToArray()` only when necessary - be aware of multiple enumeration
- Avoid modifying collections while iterating
- Use `HashSet<T>` or `Dictionary<TKey, TValue>` for O(1) lookups
- Consider using **immutable collections** when appropriate

## Async/Await Best Practices

- Always use `ConfigureAwait(false)` in library code
- Don't block on async code with `.Result` or `.Wait()` - this can cause deadlocks
- Use `ValueTask<T>` for frequently called async methods that often complete synchronously
- Avoid `async void` methods except for event handlers
- Use `CancellationToken` for long-running or I/O-bound operations

## Exception Handling

- Catch **specific exceptions**, not just `Exception`
- Don't use exceptions for control flow
- Use `throw;` to rethrow exceptions, not `throw ex;`
- Add meaningful context to exceptions using `InnerException`
- Consider using **Result types** instead of exceptions for expected failures

## Security

- **Never trust user input** - always validate and sanitize
- Use **parameterized queries** or ORM (Entity Framework) to prevent SQL injection
- Don't log sensitive information (passwords, tokens, PII)
- Use **cryptographically secure random** (`RandomNumberGenerator`) for security purposes
- Validate file paths to prevent **path traversal attacks**
- Use **SecureString** for sensitive data in memory when appropriate

## Dependency Injection & IoC

- Register services with appropriate **lifetimes** (Singleton, Scoped, Transient)
- Avoid **service locator anti-pattern** - prefer constructor injection
- Don't inject `IServiceProvider` directly
- Be careful with **captive dependencies** (e.g., Scoped in Singleton)

## Entity Framework / Database

- Use **AsNoTracking()** for read-only queries
- Avoid **N+1 query problems** - use `Include()` or projections
- Don't execute queries in loops - batch operations when possible
- Use **raw SQL** sparingly and always with parameters
- Consider using **compiled queries** for frequently executed queries

## Testing

- Follow **AAA pattern** (Arrange, Act, Assert)
- Use meaningful test names that describe the scenario
- Mock external dependencies
- Test edge cases and error paths
- Use `xUnit`, `NUnit`, or `MSTest` with appropriate assertions

## Code Style & Standards

- Follow **PascalCase** for public members, **camelCase** for private fields
- Use **expression-bodied members** when they improve readability
- Prefer **explicit types** over `var` when type isn't obvious
- Add **XML documentation comments** for public APIs
- Keep methods focused and under 20-30 lines when possible
- Use **readonly** for fields that don't change after initialization

## Common Anti-Patterns to Avoid

- **God classes** - classes that do too much
- **Primitive obsession** - over-using primitives instead of domain objects
- **Feature envy** - methods that use more of another class than their own
- **Circular dependencies** between classes
- **Static mutable state** - leads to threading issues
- **Leaky abstractions** - implementation details leaking through interfaces
