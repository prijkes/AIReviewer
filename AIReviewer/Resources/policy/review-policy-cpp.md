# C++ Code Review Policy

Policy for reviewing modern C++ code (C++11/14/17/20) with emphasis on memory safety, performance, and best practices.

## Memory Management & RAII

- Use **RAII** (Resource Acquisition Is Initialization) for all resources
- Prefer **smart pointers** (`std::unique_ptr`, `std::shared_ptr`) over raw pointers
- Use `std::make_unique` and `std::make_shared` for creating smart pointers
- Avoid manual `new`/`delete` - let smart pointers handle it
- Use **std::optional** for optional values instead of pointers
- Implement proper **copy/move constructors** and assignment operators (Rule of Five)
- Follow the **Rule of Zero** when possible - let compiler generate special members

## Modern C++ Features

- Use **auto** for complex types but keep code readable
- Leverage **range-based for loops** instead of iterator loops
- Use **lambda expressions** for callbacks and algorithms
- Prefer **constexpr** for compile-time constants and functions
- Use **structured bindings** (C++17) for tuple-like returns
- Leverage **if/switch init statements** (C++17) to limit scope
- Use **std::string_view** for non-owning string references

## STL & Algorithms

- Prefer **STL algorithms** over manual loops (`std::find`, `std::transform`, etc.)
- Use **std::vector** as default container, choose others when needed
- Reserve capacity for `std::vector` when size is known
- Use **emplace** methods instead of push for in-place construction
- Prefer **range-based operations** from `<algorithm>`
- Use **std::array** for fixed-size arrays instead of C-style arrays

## Error Handling

- Use **exceptions** for exceptional cases, not for control flow
- Prefer **RAII** to ensure cleanup happens even with exceptions
- Make destructors `noexcept` - they should never throw
- Use `std::error_code` or `std::optional` for expected failures
- Add `noexcept` specifier where appropriate for better optimization
- Consider **std::expected** (C++23) for error handling without exceptions

## Threading & Concurrency

- Use **std::thread**, **std::async**, or thread pools, not raw threads
- Protect shared data with **std::mutex** and **std::lock_guard**
- Use **std::atomic** for lock-free programming when appropriate
- Avoid **deadlocks** - acquire locks in consistent order
- Prefer **std::shared_mutex** for reader-writer scenarios
- Use **std::condition_variable** for thread synchronization
- Consider **std::jthread** (C++20) for automatic joining

## Performance

- Pass large objects by **const reference** or **rvalue reference**
- Use **move semantics** to avoid unnecessary copies
- Mark functions `const` when they don't modify state
- Use **inline** for small, frequently called functions
- Avoid unnecessary **virtual** functions - they prevent inlining
- Consider **constexpr** for compile-time computation
- Profile before optimizing - don't guess
- Use **std::string_view** to avoid string copies

## Safety & Correctness

- Initialize all variables - use **uniform initialization** `{}`
- Avoid **undefined behavior** - buffer overflows, dangling pointers, etc.
- Don't use **C-style casts** - use `static_cast`, `dynamic_cast`, etc.
- Validate **array/vector indices** - use `.at()` for bounds checking in debug
- Check **pointer validity** before dereferencing
- Use `nullptr` instead of `NULL` or `0`
- Avoid **signed integer overflow** - it's undefined behavior

## Code Organization

- Use **namespaces** to avoid naming conflicts
- Prefer **forward declarations** to reduce compile-time dependencies
- Keep header files **self-contained** with include guards or `#pragma once`
- Use **const** wherever possible for better const-correctness
- Limit **#include** in headers - prefer forward declarations
- Use **anonymous namespaces** for internal linkage (instead of `static`)

## Modern C++ Style

- Use **= default** and **= delete** for special member functions
- Mark single-argument constructors **explicit** to prevent implicit conversions
- Use **override** keyword for virtual function overrides
- Use **final** to prevent further inheritance/overriding
- Prefer **enum class** (scoped enums) over plain enums
- Use **using** for type aliases instead of `typedef`

## Common Pitfalls to Avoid

- **Returning references to local variables** - causes dangling references
- **Slicing objects** - passing derived objects by value to base type
- **Multiple inheritance** - creates complexity, prefer composition
- **Raw pointer ownership** - unclear who owns the resource
- **Mixing malloc/free with new/delete** - undefined behavior
- **Modifying strings while iterating** - can invalidate iterators
- **Thread-unsafe static initialization** - use `std::call_once` or C++11 magic statics

## Testing

- Write **unit tests** for all non-trivial functions
- Test **edge cases** - empty containers, null pointers, boundary values
- Use testing frameworks like **Google Test** or **Catch2**
- Check for **memory leaks** using tools like Valgrind or AddressSanitizer
- Enable and fix all **compiler warnings** (`-Wall -Wextra -pedantic`)

## Documentation

- Document **public APIs** with clear comments
- Explain **non-obvious code** with inline comments
- Document **thread-safety** guarantees
- Specify **ownership semantics** for pointers
- Note any **platform-specific code** or dependencies
