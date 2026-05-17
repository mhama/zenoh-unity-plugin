# HighLevel wrapper generation rules

This document defines the conventions for writing the HighLevel layer that sits
on top of the `Zenoh.Bindings` low-level wrappers. The HighLevel layer's job is
to deliver an ergonomic API surface for end-user code: terse type names,
exceptions on failure, direct `byte[]` / `string` payloads, and complete
hiding of the `Owned` / `Loaned` / `Take` machinery — while leaning entirely
on the Bindings layer for memory safety guarantees.

Companion document:
- `Assets/ZenohPackage/Runtime/Bindings/00_bindings-generation-rules.md` — rules
  for the layer below.

## Scope

- Source types are `Zenoh.Bindings.*` (`ZOwned*`, `ZLoaned*`, `Z*Options`, …).
- Target types live under the `Zenoh.HighLevel` namespace in
  `Assets/ZenohPackage/Runtime/HighLevel/`.
- HighLevel must not weaken Bindings invariants (use-after-free, double-drop,
  dangling loans, GCHandle leaks). When ergonomics require new behaviour that
  cannot be expressed safely on top of the existing Bindings surface, extend
  Bindings — do not work around it from HighLevel.

## Layer relationship

| Layer | Namespace | Responsibility | Failure mode |
|---|---|---|---|
| `Zenoh.Plugins` | `ZenohNative` P/Invoke | csbindgen output. Untouched by hand. | n/a |
| `Zenoh.Bindings` | `ZOwned* / ZLoaned* / Z*Options` | Memory-safe 1:1 projection. | Returns `ZResult`, never throws (except argument validation). |
| **`Zenoh.HighLevel`** | `Session / Publisher / Subscriber / KeyExpr / SampleView / …` | **Ergonomics.** Exceptions, POCO options, `byte[]`/`string` payloads, hide `Owned`/`Loaned`. | Throws `ZenohException`; parallel `Try*` returns `bool` + `out ZResult`. |
| `Zenoh.Legacy` | existing `Wrappers/` | Legacy compatibility only. Do not extend. | mixed. |

## asmdef and directory layout

```
Runtime/
  ZenohPluginForUnity.asmdef       (Bindings + Plugins)
  AssemblyInfo.cs                  [InternalsVisibleTo("ZenohPluginForUnity.HighLevel")]
  Bindings/                        Zenoh.Bindings, Zenoh.Bindings.Internal
  HighLevel/
    ZenohPluginForUnity.HighLevel.asmdef    references "ZenohPluginForUnity"
    *.cs                           Zenoh.HighLevel
    Extensions/*.cs                Zenoh.HighLevel (extension methods)
    Internal/*.cs                  Zenoh.HighLevel.Internal (HighLevel-only helpers)
```

- One public type per file, file named after the type.
- Extension methods grouped by extended target (e.g. `LoanedExtensions.cs`)
  under `Extensions/`.
- HighLevel-only utilities (string marshalling, factory helpers) live under
  `Internal/` and are `internal` visibility.

## Naming conventions

1. Drop the `Z` prefix and `Owned`/`Loaned` markers from public type names.
   - `ZOwnedSession` → `Session`
   - `ZOwnedPublisher` → `Publisher`
   - `ZLoanedSample` → `SampleView` (see "Pattern selection" below)
2. Hide ownership terminology from the public surface. Do not name parameters
   or members `movingXxx`, `ownedXxx`, `loanedXxx`.
3. Factory method names mirror the Zenoh C API verbs where possible:
   `Open` (Session), `Declare` (Publisher / Subscriber / Queryable), `Parse`
   (KeyExpr), `FromJsonString` / `FromFile` / `Default` (Config).
4. Internal accessors that hand a Bindings type to another HighLevel class use
   the `*Internal` suffix: `Session.LoanInternal() -> ZLoanedSession`.
5. POCO option classes use a noun-y name: `PublisherDeclareOptions`, with
   simple settable properties (`DefaultEncoding`, etc.). No fluent builders.

## Pattern selection

The HighLevel layer combines three patterns. Pick deliberately:

### Pattern A — delegation class (state-bearing types)

Use when the Bindings type carries persistent state (a native handle, a
declared resource, an installed closure) and must own its lifetime:

- `Session`, `Publisher`, `Subscriber`, `Config`, `KeyExpr`, `Queryable`,
  `Querier`, `LivelinessToken`, `MatchingListener`, etc.

Required structure:

```csharp
public sealed class Foo : IDisposable
{
    private ZOwnedFoo owned;

    private Foo(ZOwnedFoo owned) { this.owned = owned; }

    public static Foo Declare(...)                                     // throws
    public static bool TryDeclare(..., out Foo foo, out ZResult result)

    public void DoThing(...)                                           // throws
    public bool TryDoThing(..., out ZResult result)

    internal ZLoanedFoo LoanInternal() { ThrowIfDisposed(); return owned.Loan(); }

    private void ThrowIfDisposed() { if (owned == null) throw new ObjectDisposedException(nameof(Foo)); }
    public void Dispose() { owned?.Dispose(); owned = null; }
}
```

- Constructor is `private`. Construction is always through a named factory.
- The factory has two variants: one that throws (`Declare` / `Open` / `Parse`)
  and a `Try*` variant that takes `out` and returns `bool`.
- `Dispose` is idempotent. After Dispose, any public method must throw
  `ObjectDisposedException`.
- `LoanInternal()` is the only sanctioned way for sibling HighLevel classes to
  reach the underlying `ZLoaned*`. Never expose `ZOwned*` from HighLevel.

### Pattern B — `readonly struct` facade (short-lived borrowed views)

Use when the value exists only for the duration of a callback or expression
and should not be retained:

- `SampleView` (wraps `ZLoanedSample` inside subscriber/queryable callbacks).
- Future: `ReplyView`, `QueryView`.

Structure:

```csharp
public readonly struct SampleView
{
    private readonly ZLoanedSample loaned;
    internal SampleView(ZLoanedSample loaned) { this.loaned = loaned; }

    public string KeyExpr => loaned.KeyExpr().AsString();
    public byte[] PayloadAsBytes() => loaned.Payload()?.ToByteArray();
    public int PayloadCopyTo(byte[] dst, int dstOffset = 0) => ...;
}
```

- `internal` constructor: only HighLevel can mint instances.
- All members must be safe to call while the underlying `ZLoaned*` is alive.
- Document that the view must not be stored outside the callback. If a caller
  needs to hold data past the callback, expose a `Snapshot()` method that
  copies into managed memory.

### Pattern C — extension methods (values returned by Bindings)

Use when adding small accessor / conversion helpers on Bindings loaned types
that callers may legitimately receive directly:

- `ZLoanedBytes.ToByteArray()`, `CopyTo(byte[], int)`, `ToStringUtf8()`
- `ZLoanedKeyExpr.AsString()`
- `ZLoanedEncoding.AsString()`

Rules:

- Live under `Extensions/`. Class name groups related extensions.
- Implementation delegates to `Internal/BindingsInterop.cs`, which is the one
  place that touches `ZenohNative.*` via `InternalsVisibleTo`.
- Extension methods must not allocate `IDisposable` resources without a clear
  release path. If lifetime cannot be expressed as a single synchronous call,
  use Pattern A instead.

### Anti-patterns (do not use)

- Inheriting from `ZOwned*`. Bindings classes are `sealed`; do not unseal them
  to support HighLevel. Use composition (Pattern A).
- `partial` extension of `ZOwned*` from HighLevel. The namespace cannot be
  shared across asmdefs and this would erode the 2-layer split.
- Re-exporting Bindings options (`Z*Options`) as the HighLevel API surface.
  Wrap them in a HighLevel POCO and translate at the call site.

## Failure and exception policy

1. Throwing factories / methods are the main API. Failures map to
   `ZenohException` carrying the original `ZResult`.
2. Every throwing factory / method has a `Try*` counterpart that returns
   `bool` and `out ZResult`. The HighLevel implementation calls into the same
   core code path; the throwing variant is the one that wraps `Try*` (or vice
   versa), not a separate translation.
3. Argument validation throws `ArgumentNullException` / `ArgumentOutOfRangeException`
   directly. These represent programmer bugs and never propagate as
   `ZenohException`.
4. `ObjectDisposedException` is thrown for use-after-Dispose, matching .NET
   convention.
5. Reserve `Try*` paths for `ZResult` codes that are expected in normal flow
   (`Z_CHANNEL_NODATA`, `Z_CHANNEL_DISCONNECTED`). Do not use `Try*` to mask
   programmer errors — those should still throw.
6. `ZResult.ThrowIfErr(string message = null)` is the canonical extension for
   converting `ZResult` to `ZenohException`. Use it at the boundary; do not
   write ad-hoc `if (!result.IsOk) throw new ZenohException(...)` blocks.

## Options handling

- HighLevel POCO option classes carry simple properties. They are not
  `IDisposable` and do not hold native resources. Example:
  ```csharp
  public sealed class PublisherDeclareOptions
  {
      public EncodingPreset? DefaultEncoding { get; set; }
  }
  ```
- Inside the HighLevel method that consumes the POCO:
  1. Construct the Bindings `Z*Options` via `Default()`.
  2. For each set HighLevel property, build the required owned value and call
     the corresponding `Set*` on the Bindings options. The Bindings setter
     takes ownership via `Take()`; the owned value's lifetime now belongs to
     the options object.
  3. Use a `try / finally { options.Dispose(); }` block around the native call
     so the options' adopted owned values are dropped on every path.
- Never expose Bindings options to HighLevel callers. They are an
  implementation detail of the HighLevel method.

## Closure / callback handling

For Bindings types that take a `ZOwnedClosure*` (currently `ZOwnedSubscriber`,
and the future `Queryable`, `Querier`, `MatchingListener`, `LivelinessSubscriber`,
`Scout`, log):

1. Inside the HighLevel `Declare`, create a fresh `ZOwnedClosure*` locally.
2. Allocate the user-supplied `Action<…>` once, wrap it in a `ZClosure*Call`
   delegate that constructs the appropriate Pattern-B view and invokes the
   user action.
3. Call `closure.Set(adapter, null, IntPtr.Zero)`.
4. Pass the closure to `ZOwned*.Declare(...)`. Bindings will `Take()` the
   closure inside the call; after this point the HighLevel reference is
   inert.
5. Wrap the whole thing in `try { … } catch { closure.Dispose(); owned.Dispose(); throw; }`
   so a failure before `Declare` cleans up. After `Declare` returns
   (success or graceful failure), `closure.Dispose()` is a no-op because the
   closure has been moved.
6. **Do not** wrap the user-supplied callback in your own try/catch. The
   Bindings `DispatchCall` already logs and swallows exceptions to prevent
   propagation across the FFI boundary.
7. **Do not** retain the closure or the adapter delegate in a HighLevel field
   after `Declare`. The GCHandle inside Bindings keeps the delegate alive.

## Lifetime and Dispose ordering

- Derived resources must be disposed before the resource they depend on:
  - `Publisher` / `Subscriber` / `Queryable` → before their `Session`.
  - `Session` → after all of its derived resources.
- `KeyExpr` can be disposed immediately after the `Declare` that consumed it.
  Zenoh copies the key expression internally; the loaned reference is not
  retained.
- Finalizers exist on `ZOwned*` for safety. HighLevel classes rely on this and
  do not add their own finalizers (they hold managed references only).

## Memory efficiency

### Zero-copy publish

`Publisher.Put(byte[] payload, …)` routes through
`BytesFactory.FromBuffer(byte[])` → `ZOwnedBytes.TryCreateFromManagedBuf`,
which pins the managed array via `GCHandle` and registers a deleter that
unpins when Zenoh drops the bytes. This avoids the managed→native copy that
`ZOwnedBytes.TryCreateCopyFromBuf` performs.

Rules:

- Whenever the HighLevel API can take a `byte[]` directly, use the
  pin-based path. Reserve `TryCreateCopyFromBuf` for cases where the caller
  hands a `ReadOnlySpan<byte>` or other unpinnable source.
- Do **not** add GCHandle / pin lifecycle code into HighLevel. Hand-written
  factories that involve GCHandle live in Bindings (see `Bytes.Pinned.cs`
  pattern in `00_bindings-generation-rules.md`).

### Low-copy receive

`SampleView.PayloadCopyTo(byte[], int)` uses `z_bytes_get_contiguous_view`
to copy the payload directly into the caller's buffer (one native→managed
copy), falling back to `z_bytes_reader_read` if the bytes are not contiguous.

Guidance for callbacks:

- Use `sample.PayloadLength` to size a fresh buffer, then `PayloadCopyTo` —
  one allocation + one copy per frame.
- Avoid shared buffers + locks between the receive thread and the main
  thread. Per-frame allocations transfer ownership cleanly to the main
  thread via closure capture.
- Use `PayloadAsBytes()` only when you genuinely want a freshly allocated
  exact-sized array. It is equivalent to allocate + `PayloadCopyTo`.

### Per-frame overhead

- Hoist `WaitForSeconds` instances out of coroutine loops.
- For Publisher hot loops, set `DefaultEncoding` on the declare options
  rather than building a `ZOwnedEncoding` per `Put`.

## When to extend Bindings vs HighLevel

| Symptom | Resolution |
|---|---|
| Need a new native API surfaced (e.g. `z_keyexpr_as_view_string`) | Use it from HighLevel via `Internal/BindingsInterop.cs` + `InternalsVisibleTo`. Do not add a public method on `ZLoaned*` in Bindings unless every Bindings consumer needs it. |
| Need GCHandle pin / deleter / closure lifecycle handling | Add to Bindings as a `<Name>.Pinned.cs` (or similar) partial. HighLevel cannot manage these safely on its own. |
| Need to consume a Bindings type's `internal` member from HighLevel | `InternalsVisibleTo` is already in place; just call. Do not promote the member to `public`. |
| Want a different ownership semantic for the same native API | This belongs in HighLevel, not Bindings. The Bindings projection is 1:1 with native. |
| Want to combine multiple native calls into one ergonomic call | HighLevel only. |

## Threading

- Bindings sample / reply / query callbacks fire on Zenoh's internal thread,
  not Unity's main thread. They must not touch Unity APIs (`Texture2D`,
  `GameObject`, etc.) directly.
- HighLevel does **not** marshal user callbacks to the main thread on the
  user's behalf. Each receiver is responsible for capturing
  `SynchronizationContext.Current` and posting Unity-touching work itself.
  Document this on every callback-accepting HighLevel API.
- HighLevel state on `Pattern A` classes is touched only from the user thread
  (Dispose, instance method calls). Concurrent use from multiple threads is
  not supported unless the class's documentation says otherwise.

## Documentation conventions inside HighLevel

- Public types and members get an XML doc comment when behaviour is not
  obvious from the name. Lifetime hazards (Pattern B "do not retain"), pin
  semantics, and threading constraints are mandatory to document.
- Avoid restating Bindings invariants in HighLevel comments; link instead.

## Checklist when adding a new HighLevel type

1. [ ] Decide pattern (A / B / C) based on lifetime and state.
2. [ ] Type name drops `Z` prefix and ownership markers.
3. [ ] One file per type under `Runtime/HighLevel/` (no nested types unless
       they are private POCO option helpers).
4. [ ] Factory methods: throwing variant + `Try*` variant.
5. [ ] All `ZResult` returns are either consumed locally via `ThrowIfErr` or
       propagated via `Try*`.
6. [ ] Bindings options consumed in `try / finally { Dispose() }`.
7. [ ] Callbacks: closure created locally, `Set` once, passed to Bindings
       `Declare`, never retained in a HighLevel field afterwards.
8. [ ] Dispose: idempotent, sets owned reference to `null`, every public
       method has `ThrowIfDisposed` (Pattern A).
9. [ ] Internal accessor (`LoanInternal`) provided only if sibling HighLevel
       classes need it.
10. [ ] `byte[]` payload ingress uses the pin path
        (`ZOwnedBytes.TryCreateFromManagedBuf`).
11. [ ] Sample / received-bytes path uses `PayloadCopyTo` or the contiguous-view
        helper rather than `z_bytes_to_slice` round-trips.
12. [ ] Sample under `Assets/ZenohSampleScenes/` updated or added if the new
        type is reachable from end-user code paths.

## Open questions (for future revisions)

- async / UniTask wrappers (`Session.GetAsync` etc.) — not addressed here.
- `SampleView.ToSnapshot()` policy for retaining payloads beyond the callback.
- Whether to introduce HighLevel handler types
  (`FifoSubscriber` / `RingSubscriber`) once Bindings handles them.
- Unity-specific zero-copy ingress from `NativeArray<byte>`. Needs a
  Bindings factory analogous to `TryCreateFromManagedBuf` operating on a
  pinned `NativeArray` pointer.
