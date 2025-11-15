# C++/CLI Code Review Policy

Policy for reviewing C++/CLI code with emphasis on interop between native C++ and managed .NET, proper resource management in mixed environments.

## Mixed Environment Basics

- Understand the difference between **native** and **managed** code
- Use **gcnew** for managed objects, **new** for native objects
- Managed types use **^** (hat) handles, native types use ***** (pointers)
- Be aware of **garbage collection** - it runs on managed heap only
- Native resources need **manual cleanup**, managed resources have finalizers

## Resource Management

- Implement **IDisposable** for managed classes that hold native resources
- Use **RAII** for native resources (destructors/finalizers)
- Implement both **destructor (~)** and **finalizer (!)** when wrapping native resources
- Call **delete** on native objects explicitly - GC won't clean them up
- Use **using** statement or try/finally for deterministic cleanup of IDisposable
- Be careful with **mixed object graphs** - native objects holding managed references

## Marshaling & Interop

- Use **marshal_as** for simple type conversions between native and managed
- Use **pin_ptr** to pin managed objects when passing to native code
- Be aware of **string conversion** - System::String^ vs std::string vs char*
- Use **IntPtr** to pass native pointers through managed code
- Understand **blittable types** - they don't need marshaling
- Use **GCHandle** to prevent GC from moving pinned objects

## Memory Safety

- Don't let **native pointers** point to managed objects - they can move during GC
- Use **pin_ptr** when you must get native pointer to managed memory
- Don't store **pin_ptr** - only use in local scope
- Be careful with **lifetime** - managed objects can be collected
- Native objects **won't be** automatically collected by GC
- Use **GC::KeepAlive** to prevent premature collection

## Exception Handling

- **Managed exceptions** (System::Exception^) propagate through managed code
- **Native exceptions** (C++ exceptions) don't cross managed boundaries well
- Catch and convert native exceptions to managed exceptions at boundaries
- Use **try-catch** blocks appropriately for both managed and native exceptions
- Don't let native exceptions escape to managed callers
- Implement proper **finally** blocks for cleanup

## Threading

- Use **.NET threading** (System::Threading) for managed code
- Use **native threading** (std::thread, Windows threads) for native code
- Be aware of **GC thread suspension** - can affect performance
- Synchronize access to shared resources between native and managed threads
- Use appropriate **locks** - Monitor for managed, mutex for native
- Be careful with **thread-local storage** in mixed environments

## Performance Considerations

- **Minimize transitions** between native and managed code - they have overhead
- Consider **batching operations** to reduce interop calls
- Be aware of **marshaling costs** - especially for strings and arrays
- Use **native code** for performance-critical sections
- Use **managed code** for convenient APIs and framework features
- Profile to identify **interop bottlenecks**

## Type Conversions

- Convert **System::String^ to std::string** properly (marshal_as or manual)
- Convert **arrays** between native and managed carefully
- Use **List<T>^ to std::vector** conversions when needed
- Be explicit about **numeric type conversions**
- Handle **nullptr vs NULL** correctly in both environments
- Use appropriate **casts** for each environment (safe_cast vs static_cast)

## Best Practices

- Keep **native and managed code** separated when possible
- Use **C++/CLI as a bridge** between pure native and pure managed
- Minimize the **surface area** of C++/CLI code
- Document which parts are **native vs managed**
- Use **ref class** for managed types, **class** for native types
- Use **value class** for lightweight managed value types

## Common Patterns

- **Wrapper pattern** - wrap native C++ classes with managed types
- **PIMPL in reverse** - hide managed implementation from native code
- **Adapter pattern** - adapt native interfaces to managed ones
- Use **interior_ptr** to get stable references within managed objects
- Implement proper **copy semantics** for ref classes if needed
- Use **initonly** for const-like behavior in managed classes

## Security

- Validate **all data** crossing native/managed boundary
- Don't trust pointers from **unmanaged code**
- Be careful with **buffer operations** across boundaries
- Use **SecureString** for sensitive data in managed code
- Sanitize **strings** before passing to native code
- Check for **buffer overruns** in marshaling code

## Garbage Collection Awareness

- Understand **generational GC** behavior
- Don't rely on **deterministic finalization** - use IDisposable
- Call **GC::Collect** sparingly if at all
- Use **WeakReference** for cache-like scenarios
- Implement **finalizers** correctly - they run on finalizer thread
- Suppress finalization with **GC::SuppressFinalize** after cleanup

## CLI-Specific Features

- Use **property** syntax for getters/setters in ref classes
- Use **event** keyword for managed events
- Leverage **.NET collections** when appropriate
- Use **operator overloading** carefully in managed types
- Implement **IEnumerable** for collection-like types
- Use **generic types** (<T>) where appropriate

## Debugging & Diagnostics

- Use **mixed-mode debugging** in Visual Studio
- Enable **managed debugging assistants** (MDAs)
- Use **.NET profilers** for managed code issues
- Use **native profilers** for native code issues
- Check for **memory leaks** in both native and managed code
- Monitor **GC pressure** and collections

## Common Pitfalls

- **Forgetting to free native resources** - GC won't do it
- **Passing managed references to native code** without pinning
- **Storing pin_ptr** beyond local scope
- **Mixing new/delete with gcnew** - undefined behavior
- **Throwing native exceptions across managed boundaries**
- **Not implementing IDisposable** for native resource wrappers
- **Incorrect finalizer implementation** - must be safe for concurrent execution
