# ZenohNative.g.cs to Bindings generation rules

This document defines the mechanical naming and ownership rules for converting
`Assets/ZenohPackage/Runtime/Plugins/*/ZenohNative.g.cs` APIs into
`Assets/ZenohPackage/Runtime/Bindings` classes.

The Bindings layer is a memory-safe, low-level C# projection of the native API.
It must not become the convenience Wrappers layer. Do not add shortcut workflows
that combine multiple native concepts unless the native API itself has that
shape.

## Scope

- Source APIs are native declarations in `ZenohNative.g.cs`.
- Target APIs live under the `Zenoh.Bindings` namespace.
- Bindings classes should preserve native ownership boundaries:
  - `z_owned_*` => `ZOwned*`
  - `z_loaned_*` => `ZLoaned*`
  - options structs => `Z*Options`
  - status/value structs => `Z*`
- `ZenohNative.*` function names may remain inside method bodies, but public
  Binding method names must not expose raw `z_*`, `zc_*`, or `ze_*` names.

## Mechanical naming rules

1. Drop the native prefix `z_`, `zc_`, or `ze_`.
2. Determine the receiver from the native signature, not from the function name
   alone.
3. If the first argument is an uninitialized `z_owned_<type>*` output location,
   expose the function as a static factory on `ZOwned<Type>`.
   - If the native return type is `void`, use `Create<Suffix>()`.
   - If the native return type is `z_result_t`, use
     `TryCreate<Suffix>(..., out ZOwned<Type> value)`.
   - `<Suffix>` is the remaining function name after removing `<type>_`,
     converted to PascalCase. Do not remove words based on interpretation.
   - Examples:
     - `z_bytes_empty(z_owned_bytes_t*)` => `ZOwnedBytes.CreateEmpty()`
     - `z_bytes_copy_from_buf(z_owned_bytes_t*, ...)` =>
       `ZOwnedBytes.TryCreateCopyFromBuf(..., out ZOwnedBytes value)`
     - `z_bytes_copy_from_str(z_owned_bytes_t*, ...)` =>
       `ZOwnedBytes.TryCreateCopyFromStr(..., out ZOwnedBytes value)`
4. If the receiver is an existing owned value, expose an instance method on
   `ZOwned<Type>`.
   - `z_<type>_loan` => `Loan()`
   - `z_<type>_loan_mut` => `LoanMut()`
   - `z_internal_<type>_check` => `Check()`
5. If the receiver is a loaned value, expose an instance method on
   `ZLoaned<Type>`.
   - Remove `<type>_` and PascalCase the suffix.
   - Keep standard predicate names:
     - `is_empty` => `IsEmpty()`
   - Keep common native abbreviations when they are part of the native API:
     - `len` => `Len()`
   - Examples:
     - `z_bytes_len(z_loaned_bytes_t*)` => `ZLoanedBytes.Len()`
     - `z_sample_payload(z_loaned_sample_t*)` => `ZLoanedSample.Payload()`
6. If the native function initializes an options struct named `z_*_options_t`,
   expose a static `Default()` factory on the options class.
   - Keep an internal or public `InitDefault()` only when needed by generated
     implementation code.
   - Example:
     - `z_open_options_default(z_open_options_t*)` =>
       `ZOpenOptions.Default()`
7. If the native function consumes an owned value through a `z_moved_*` pointer,
   expose the operation as a method whose parameter is the owned wrapper type.
   - The method must call `MoveToNative()` and make later use of that owned
     wrapper throw `ObjectDisposedException`.
   - Example:
     - `z_publisher_put(..., z_moved_bytes_t*, ...)` =>
       `ZLoanedPublisher.Put(ZOwnedBytes payload, ZPublisherPutOptions options)`
8. Native `*_drop` functions are not public Binding APIs.
   - They are used by `Dispose()`.
9. Native `z_internal_*_null` functions are not public Binding APIs.
   - They are used only to initialize wrapper storage to the gravestone state.

## Factory and failure handling

For `TryCreate*` methods:

- Allocate the owned wrapper internally.
- Initialize its native storage to the gravestone state before calling native
  construction.
- On success:
  - return the native `ZResult`
  - set `out value` to the valid owned wrapper
- On error:
  - dispose the allocated wrapper
  - set `out value = null`
  - return the native `ZResult`

For `Create*` methods wrapping native `void` constructors:

- Allocate the owned wrapper internally.
- Call the native constructor.
- Return the valid owned wrapper.

Public constructors for owned wrappers should be avoided when the native type
must be constructed by a specific native constructor. Use a private constructor
for allocation plus `z_internal_*_null`.

## Ownership and lifetime rules

- `ZOwned*` owns native memory and implements `IDisposable`.
- `ZLoaned*` does not own native memory and stores a reference to its owner to
  keep the owner alive for the loan's managed lifetime.
- `MoveToNative()` is internal only.
  - It marks the owned wrapper as moved.
  - After moving, public operations on the wrapper must throw
    `ObjectDisposedException`.
- `Dispose()` must be idempotent.
- `Dispose()` must call the native drop function only when the value is valid
  and has not been moved.
- Loaned values must not expose native pointers publicly.
