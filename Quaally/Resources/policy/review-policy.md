# General Code Review Policy

This is the general policy applied when language-specific policies are not available.

## Core Principles

1. **Security**: Ensure code is free from vulnerabilities
2. **Correctness**: Code should work as intended
3. **Performance**: Avoid unnecessary performance bottlenecks
4. **Readability**: Code should be clear and maintainable
5. **Testability**: Code should be easy to test

## Security

- Validate all user inputs
- Avoid hardcoded credentials
- Use secure communication protocols
- Implement proper authentication and authorization
- Sanitize data before use in queries or commands

## Error Handling

- Handle errors gracefully
- Provide meaningful error messages
- Log errors appropriately
- Don't expose sensitive information in error messages

## Code Quality

- Follow consistent naming conventions
- Keep functions/methods focused and small
- Avoid code duplication
- Add comments for complex logic
- Remove dead/commented code

## Performance

- Avoid unnecessary computations
- Use appropriate data structures
- Consider algorithmic complexity
- Minimize resource usage

## Testing

- Write testable code
- Consider edge cases
- Ensure proper test coverage
