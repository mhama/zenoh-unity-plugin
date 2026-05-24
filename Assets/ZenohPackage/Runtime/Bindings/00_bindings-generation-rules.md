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
- Public Binding types use a `Z` prefix to avoid collisions with BCL types,
  legacy wrappers, and future high-level wrappers.
- `ZenohNative.*` function names may remain inside method bodies, but public
  Binding method names must not expose raw `z_*`, `zc_*`, or `ze_*` names.

## File organization

- Files directly under `Bindings/` are the mechanical projection of
  `ZenohNative.g.cs` and must follow the rules in this document. A reviewer
  should be able to predict the file's contents from the corresponding native
  declarations alone.
- One file per native concept. Group everything that pertains to a single
  native concept (e.g. owned + loaned wrapper, its options structs, redeclared
  enums, receiver-less helper classes) into the same file. Examples:
  - `Query.cs` holds `ZOwnedQuery`, `ZLoanedQuery`, the three
    `ZQueryReply*Options`, and the `ZReplyKeyExpr` enum.
  - `Get.cs` holds `ZGetOptions`, `ZQueryConsolidation`, and the
    `ZQueryTarget` / `ZConsolidationMode` enums.
  - Cross-concept methods belong to the receiver's file. `z_get` is an
    operation on `z_loaned_session_t`, so the `ZLoanedSession.Get(...)`
    instance method lives in `Session.cs`, not in `Get.cs`.
- Do not split a single class with `partial` to separate concepts that already
  live within the mechanical layer. `partial` is reserved for separating the
  mechanical layer from `Handwritten/` (see below).
- `Bindings/Handwritten/` contains hand-written, non-mechanical bindings that
  do not derive from a one-to-one mapping of native declarations. These files
  must be reviewed independently from the mechanical layer and must call out
  the non-mechanical lifetime contract in a file-level comment.
  - Use `partial` on the mechanical class in `Bindings/` plus a matching
    `partial` in `Handwritten/` to attach handwritten members without
    cluttering the mechanical file.
  - Current example: `Handwritten/Bytes.Pinned.cs` extends `ZOwnedBytes` with
    `TryCreateFromManagedBuf`, which pins a managed `byte[]` via `GCHandle`
    and hands the raw pointer to `z_bytes_from_buf` with a managed-side
    deleter. The lifetime contract (pin until native drop, AOT-safe deleter
    delegate) is fundamentally different from the mechanical
    Owned/Loaned/Take scaffolding in `Bytes.cs`.
- Tests for both layers live in
  `Assets/ZenohPackage/Tests/EditMode/Bindings*Tests.cs` and are not subject
  to these rules.

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
     - `z_config_default(z_owned_config_t*)` =>
       `ZOwnedConfig.TryCreateDefault(out ZOwnedConfig value)`
4. If the receiver is an existing owned value, expose an instance method on
   `ZOwned<Type>`.
   - `z_<type>_loan` => `Loan()`
   - `z_<type>_loan_mut` => `LoanMut()`
   - `z_internal_<type>_check` => `Check()`
   - Do not use `Check()` as a universal managed liveness predicate. Native
     semantics may be type-specific; for example, `z_owned_bytes_t` created by
     `z_bytes_empty()` can be loaned and dropped even though
     `z_internal_bytes_check()` returns false.
   - `Check()` must throw `ObjectDisposedException` when the wrapper has been
     disposed or moved.
5. If the receiver is a loaned value, expose an instance method on
   `ZLoaned<Type>`.
   - Remove `<type>_` and PascalCase the suffix.
   - Keep standard predicate names:
     - `is_empty` => `IsEmpty()`
   - Keep common native abbreviations when they are part of the native API:
     - `len` => `Len()`
   - For native functions that write into a `z_view_string_t*` output
     parameter, return a managed `string`. Stack-allocate the
     `z_view_string_t`, call the native function, then convert via
     `z_view_string_loan` → `z_string_data` / `z_string_len` →
     `Encoding.UTF8.GetString`. The view does not own memory and needs no
     disposal.
   - Examples:
     - `z_bytes_len(z_loaned_bytes_t*)` => `ZLoanedBytes.Len()`
     - `z_sample_payload(z_loaned_sample_t*)` => `ZLoanedSample.Payload()`
6. If the native function initializes an options struct named `z_*_options_t`,
   expose a static default factory on the options class.
   - If the native return type is `void`, use `Default()`.
   - If the native return type is `z_result_t`, use
     `TryCreateDefault(out Z*Options options)`.
   - Keep `InitDefault()` private unless generated implementation code needs
     broader access.
   - Store the native struct in `internal z_<X>_options_t Native;`.
   - If the native options struct contains `z_moved_*` fields, expose
     `Set<FieldName>(ZOwned<FieldType> moving<FieldName>)` methods to set them.
   - If the native options struct contains non-moved pointer fields (e.g.
     `z_timestamp_t*`, `z_source_info_t*`), expose
     `Set<FieldName>(ZLoaned<FieldType> <fieldName>)` methods that copy
     `NativePointer` into the native field. The caller is responsible for
     keeping the pointed-to data alive until the native call completes.
   - Options classes with `z_moved_*` fields must implement `IDisposable`
     because they adopt the moved wrappers' native storage.
   - Options classes that adopt `OwnedStorage<T>` must not define finalizers.
     `OwnedStorage<T>` has its own finalizer safety net, and finalizer ordering
     is not reliable enough for options finalizers to call native drop safely.
   - Example:
     - `z_open_options_default(z_open_options_t*)` =>
       `ZOpenOptions.Default()`
7. If the native function consumes an owned value through a `z_moved_*` pointer,
   expose the operation as a method whose parameter is the owned wrapper type.
   - Implement `Take()` on an owned wrapper only when at least one zenoh-c API
     that consumes that owned type actually exists.
   - For function arguments, the binding method must call `Take()`, keep the
     returned storage alive with `using` until the native call returns, and pass
     `(z_moved_<type>_t*)storage.Pointer`.
   - For options struct fields typed `z_moved_*`, the options setter must be
     named `Set<FieldName>(ZOwned<FieldType> moving<FieldName>)`, call
     `Take()`, store the returned `OwnedStorage<T>`, and write
     `storage.Pointer` into the native field.
   - Name all consumed parameters with a `moving` prefix.
   - Later use of the source owned wrapper must throw
     `ObjectDisposedException`.
   - Example:
     - `z_publisher_put(..., z_moved_bytes_t*, ...)` =>
       `ZLoanedPublisher.Put(ZOwnedBytes movingPayload, ZPublisherPutOptions options)`
8. Native `*_drop` functions are not public Binding APIs.
   - They are used by `Dispose()`.
9. Native `z_internal_*_null` functions are not public Binding APIs.
   - They are used only to initialize wrapper storage to the gravestone state.
10. Receiver-less native functions should be grouped by concept in static
    classes.
    - Constant loan getters go in `Z<Concept>Constants`.
    - Utility functions go in `Z<Concept>Utils`.
11. Native enums are internal in `Zenoh.Plugins`. If a public Binding method
    uses a native enum concept in its signature (parameter or return type),
    redeclare it as a public Binding enum.
12. Native APIs that return direct `z_<type>_t*` pointers, such as timestamp or
    source_info accessors, must still be wrapped before public exposure.
    - Use `ZLoaned<Type>` naming when the returned pointer is borrowed from the
      receiver.
    - Do not expose the raw pointer from public Binding APIs.
13. Output structs are returned with `Try*` methods using `out`.
    - Example:
      - `z_publisher_get_matching_status(...)` =>
        `ZLoanedPublisher.TryGetMatchingStatus(out ZMatchingStatus status)`
14. If a native equality function is exposed, implement `IEquatable<T>`,
    `Equals(object)`, and `GetHashCode()`.

## Factory and failure handling

`ZResult` exposes `IsOk`, `IsError`, and `IsChannelStatus`. Positive channel
statuses such as `Z_CHANNEL_NODATA` are not errors.

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
- Do not add throwing convenience factories such as `Default()` for owned
  values when the native constructor returns `z_result_t`.

For `Create*` methods wrapping native `void` constructors:

- Allocate the owned wrapper internally.
- Call the native constructor.
- Return the valid owned wrapper.

Public constructors for owned wrappers should be avoided when the native type
must be constructed by a specific native constructor. Use a private constructor
for allocation plus `z_internal_*_null`.

For owned wrappers (`ZOwned*`):

- Allocate native storage with
  `Zenoh.Bindings.Internal.NativeStorage.Alloc<T>()` in the constructor.
- Initialize to gravestone state via `z_internal_<type>_null`.
- Free storage with `Zenoh.Bindings.Internal.NativeStorage.Free(ptr)` in
  `Dispose()` only when storage is still owned, meaning it has not been
  `Take()`-d.

## Ownership and lifetime rules

- `ZOwned*` owns native memory and implements `IDisposable`.
- `ZLoaned*` does not own native memory and stores a reference to its owner to
  keep the owner alive for the loan's managed lifetime.
- All native heap allocations and frees go through
  `Zenoh.Bindings.Internal.NativeStorage`. Direct use of
  `Marshal.AllocHGlobal` / `Marshal.FreeHGlobal` is prohibited outside this
  utility.
- Ownership of allocated storage is expressed by
  `Zenoh.Bindings.Internal.OwnedStorage<T>`.
- `Take()` is the single consume primitive on `ZOwned*` types.
  - It is implemented only for owned types that are consumed by a real zenoh-c
    API.
  - It moves both the native value and its storage ownership to the caller.
  - It returns `OwnedStorage<T>` holding the native pointer.
  - After `Take()`, the wrapper is fully inert: `moved = true`,
    `nativePtr = null`, `Dispose()` becomes a no-op for storage, and all public
    operations throw `ObjectDisposedException`.
- `MoveToNative()` and `MoveStorageToNative()` are not Binding APIs. Use
  `Take()` instead.
- For direct `z_moved_*` function arguments, binding methods must keep the
  returned storage alive with `using` until the native call returns.
- For `z_moved_*` fields inside options structs, the options wrapper adopts the
  returned `OwnedStorage<T>` and disposes it from the options `Dispose()`.
- Binding methods must not automatically dispose options; options lifetime is
  the caller's responsibility, typically via `using`.
- Options that adopt `OwnedStorage<T>` must always be released by `using` or an
  explicit `Dispose()` call. If the caller forgets, the managed object and
  storage may eventually be reclaimed by finalizers, but the native drop
  function is not called for adopted values, so native resources can leak.
- Options wrappers that adopt `OwnedStorage<T>` must not have finalizers, to
  avoid finalizer-order races between options native drop and
  `OwnedStorage<T>` storage release.
- Replacing an adopted options field must release the previously adopted
  storage before storing the new one.
- When native consumes a moved field inside an options struct, that field's
  storage is written back to the gravestone state. If the same options instance
  is reused for another `Put`, `Declare`, or similar native call, every moved
  field that should be sent again must be set again with the corresponding
  `Set*` method.
- `Dispose()` must be idempotent.
- `Dispose()` must call the native drop function only when the value is valid
  and has not been moved.
- Loaned values must not expose native pointers publicly.
- Loaned wrappers store a typed owner reference when there is only one possible
  owner type, and `object owner` when the loan may originate from either owned
  or loaned wrappers.
- Manually disposing an owned wrapper invalidates outstanding loans. The owner
  reference only prevents managed GC collection; it cannot prevent native
  use-after-free after explicit `Dispose()`.

## Testing

Every binding change must come with tests. The mechanical conversion is
small, but the boundary between managed lifetime and native lifetime is
where bugs hide, so reviewers should be able to point at a test for any
public Binding API. New PRs without tests will be sent back.

Required coverage:

- Each public `ZOwned*` factory (`Create*`, `TryCreate*`) gets at least one
  unit test asserting `IsOk` (or the documented failure mode) plus a
  successful `Loan()` / `Check()` call on the produced wrapper.
- Each options class gets at least one unit test exercising every `Set*`
  method that adopts a moved field, asserting that the source `ZOwned*`
  becomes inert (`ObjectDisposedException` from `Check()` / `Loan()`) and
  that a subsequent explicit `Dispose()` on the source does not throw.
- Owned wrappers with native operations that change observable state
  (e.g. `Reply`, `Get`, `Put`, `Declare`) need a round-trip integration
  test under `Tests/EditMode/Bindings*ZenohdIntegrationTests.cs` that runs
  against a local `zenohd`, mirroring the existing pub/sub fixture.
- Re-declared public enums and small value wrappers (e.g.
  `ZQueryConsolidation`) need a unit test verifying that the binding-side
  factories agree with the native ones on every supported variant.

Unit-style tests go in
`Assets/ZenohPackage/Tests/EditMode/BindingsBasicTests.cs`. Integration
tests that require a router go in
`Assets/ZenohPackage/Tests/EditMode/BindingsZenohdIntegrationTests.cs`
under the `ZenohIntegration` NUnit category, so they can be filtered when
no `zenohd` is available.

Tests in `Handwritten/`'s scope (non-mechanical bindings) carry the same
testing requirement; the lifetime contract is precisely what makes those
files non-mechanical, and an unverified lifetime contract is a latent
use-after-free.
