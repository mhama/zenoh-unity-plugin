# Query / Reply Bindings 実装計画

`00_bindings-generation-rules.md` に従い、Query / Reply / Queryable / Get
に関する低レベル Bindings クラスを実装する。

## 対象ネイティブ API 一覧

### Query 系 (`z_query_*`)

| Native API | シグネチャ | Binding 側マッピング |
|---|---|---|
| `z_internal_query_null` | `(z_owned_query_t*) → void` | ZOwnedQuery ctor (private) |
| `z_internal_query_check` | `(z_owned_query_t*) → bool` | `ZOwnedQuery.Check()` |
| `z_query_loan` | `(z_owned_query_t*) → z_loaned_query_t*` | `ZOwnedQuery.Loan()` |
| `z_query_loan_mut` | `(z_owned_query_t*) → z_loaned_query_t*` | `ZOwnedQuery.LoanMut()` |
| `z_query_drop` | `(z_moved_query_t*) → void` | `ZOwnedQuery.Dispose()` (private) |
| `z_query_clone` | `(z_owned_query_t* dst, z_loaned_query_t*) → void` | `ZOwnedQuery.CreateClone(ZLoanedQuery)` static |
| `z_query_take_from_loaned` | `(z_owned_query_t* dst, z_loaned_query_t*) → void` | `ZOwnedQuery.CreateTakeFromLoaned(ZLoanedQuery)` static |
| `z_query_keyexpr` | `(z_loaned_query_t*) → z_loaned_keyexpr_t*` | `ZLoanedQuery.KeyExpr()` |
| `z_query_parameters` | `(z_loaned_query_t*, z_view_string_t*) → void` | `ZLoanedQuery.Parameters()` ※後述 |
| `z_query_payload` | `(z_loaned_query_t*) → z_loaned_bytes_t*` | `ZLoanedQuery.Payload()` (nullable) |
| `z_query_payload_mut` | `(z_loaned_query_t*) → z_loaned_bytes_t*` | `ZLoanedQuery.PayloadMut()` (nullable) |
| `z_query_encoding` | `(z_loaned_query_t*) → z_loaned_encoding_t*` | `ZLoanedQuery.Encoding()` |
| `z_query_attachment` | `(z_loaned_query_t*) → z_loaned_bytes_t*` | `ZLoanedQuery.Attachment()` (nullable) |
| `z_query_attachment_mut` | `(z_loaned_query_t*) → z_loaned_bytes_t*` | `ZLoanedQuery.AttachmentMut()` (nullable) |
| `z_query_source_info` | `(z_loaned_query_t*) → z_source_info_t*` | `ZLoanedQuery.SourceInfo()` (nullable) |
| `z_query_accepts_replies` | `(z_loaned_query_t*) → z_reply_keyexpr_t` | `ZLoanedQuery.AcceptsReplies()` |
| `z_query_reply` | `(z_loaned_query_t*, z_loaned_keyexpr_t*, z_moved_bytes_t*, z_query_reply_options_t*) → z_result_t` | `ZLoanedQuery.Reply(...)` |
| `z_query_reply_err` | `(z_loaned_query_t*, z_moved_bytes_t*, z_query_reply_err_options_t*) → z_result_t` | `ZLoanedQuery.ReplyErr(...)` |
| `z_query_reply_del` | `(z_loaned_query_t*, z_loaned_keyexpr_t*, z_query_reply_del_options_t*) → z_result_t` | `ZLoanedQuery.ReplyDel(...)` |

### Reply 系 (`z_reply_*`)

| Native API | シグネチャ | Binding 側マッピング |
|---|---|---|
| `z_internal_reply_null` | `(z_owned_reply_t*) → void` | ZOwnedReply ctor (private) |
| `z_internal_reply_check` | `(z_owned_reply_t*) → bool` | `ZOwnedReply.Check()` |
| `z_reply_loan` | `(z_owned_reply_t*) → z_loaned_reply_t*` | `ZOwnedReply.Loan()` |
| `z_reply_loan_mut` | `(z_owned_reply_t*) → z_loaned_reply_t*` | `ZOwnedReply.LoanMut()` |
| `z_reply_drop` | `(z_moved_reply_t*) → void` | `ZOwnedReply.Dispose()` (private) |
| `z_reply_clone` | `(z_owned_reply_t* dst, z_loaned_reply_t*) → void` | `ZOwnedReply.CreateClone(ZLoanedReply)` static |
| `z_reply_take_from_loaned` | `(z_owned_reply_t* dst, z_loaned_reply_t*) → void` | `ZOwnedReply.CreateTakeFromLoaned(ZLoanedReply)` static |
| `z_reply_is_ok` | `(z_loaned_reply_t*) → bool` | `ZLoanedReply.IsOk()` |
| `z_reply_ok` | `(z_loaned_reply_t*) → z_loaned_sample_t*` | `ZLoanedReply.Ok()` (nullable) |
| `z_reply_ok_mut` | `(z_loaned_reply_t*) → z_loaned_sample_t*` | `ZLoanedReply.OkMut()` (nullable) |
| `z_reply_err` | `(z_loaned_reply_t*) → z_loaned_reply_err_t*` | `ZLoanedReply.Err()` (nullable) |
| `z_reply_err_mut` | `(z_loaned_reply_t*) → z_loaned_reply_err_t*` | `ZLoanedReply.ErrMut()` (nullable) |
| `z_reply_replier_id` | `(z_loaned_reply_t*, z_entity_global_id_t*) → bool` | `ZLoanedReply.ReplierId(out ...)` ※後述 |

### ReplyErr 系 (`z_reply_err_*`)

| Native API | シグネチャ | Binding 側マッピング |
|---|---|---|
| `z_internal_reply_err_null` | `(z_owned_reply_err_t*) → void` | ZOwnedReplyErr ctor (private) |
| `z_internal_reply_err_check` | `(z_owned_reply_err_t*) → bool` | `ZOwnedReplyErr.Check()` |
| `z_reply_err_loan` | `(z_owned_reply_err_t*) → z_loaned_reply_err_t*` | `ZOwnedReplyErr.Loan()` |
| `z_reply_err_loan_mut` | `(z_owned_reply_err_t*) → z_loaned_reply_err_t*` | `ZOwnedReplyErr.LoanMut()` |
| `z_reply_err_drop` | `(z_moved_reply_err_t*) → void` | `ZOwnedReplyErr.Dispose()` (private) |
| `z_reply_err_clone` | `(z_owned_reply_err_t* dst, z_loaned_reply_err_t*) → void` | `ZOwnedReplyErr.CreateClone(ZLoanedReplyErr)` static |
| `z_reply_err_payload` | `(z_loaned_reply_err_t*) → z_loaned_bytes_t*` | `ZLoanedReplyErr.Payload()` |
| `z_reply_err_payload_mut` | `(z_loaned_reply_err_t*) → z_loaned_bytes_t*` | `ZLoanedReplyErr.PayloadMut()` |
| `z_reply_err_encoding` | `(z_loaned_reply_err_t*) → z_loaned_encoding_t*` | `ZLoanedReplyErr.Encoding()` |

### Queryable 系 (`z_queryable_*`)

| Native API | シグネチャ | Binding 側マッピング |
|---|---|---|
| `z_internal_queryable_null` | `(z_owned_queryable_t*) → void` | ZOwnedQueryable ctor (private) |
| `z_internal_queryable_check` | `(z_owned_queryable_t*) → bool` | `ZOwnedQueryable.Check()` |
| `z_queryable_loan` | `(z_owned_queryable_t*) → z_loaned_queryable_t*` | `ZOwnedQueryable.Loan()` |
| `z_queryable_drop` | `(z_moved_queryable_t*) → void` | `ZOwnedQueryable.Dispose()` (private) |
| `z_undeclare_queryable` | `(z_moved_queryable_t*) → z_result_t` | ※後述 |
| `z_declare_queryable` | `(z_loaned_session_t*, z_owned_queryable_t*, ...) → z_result_t` | `ZOwnedQueryable.Declare(...)` ※注 |
| `z_declare_background_queryable` | `(z_loaned_session_t*, ...) → z_result_t` | `ZLoanedSession.DeclareBackgroundQueryable(...)` |
| `z_queryable_id` | `(z_loaned_queryable_t*) → z_entity_global_id_t` | `ZLoanedQueryable.Id()` ※後述 |
| `z_queryable_keyexpr` | `(z_loaned_queryable_t*) → z_loaned_keyexpr_t*` | `ZLoanedQueryable.KeyExpr()` |

### Closure 系

| Native API | Binding 側マッピング |
|---|---|
| `z_closure_query` / `z_internal_closure_query_null` / `z_internal_closure_query_check` / `z_closure_query_loan` / `z_closure_query_loan_mut` / `z_closure_query_drop` / `z_closure_query_call` | `ZOwnedClosureQuery` + `ZLoanedClosureQuery` |
| `z_closure_reply` / `z_internal_closure_reply_null` / `z_internal_closure_reply_check` / `z_closure_reply_loan` / `z_closure_reply_loan_mut` / `z_closure_reply_drop` / `z_closure_reply_call` | `ZOwnedClosureReply` + `ZLoanedClosureReply` |

### Get 系 (`z_get`, options, consolidation)

| Native API | シグネチャ | Binding 側マッピング |
|---|---|---|
| `z_get` | `(z_loaned_session_t*, z_loaned_keyexpr_t*, byte*, z_moved_closure_reply_t*, z_get_options_t*) → z_result_t` | `ZLoanedSession.Get(...)` (Session.cs) |
| `z_get_options_default` | `(z_get_options_t*) → void` | `ZGetOptions.Default()` |
| `z_query_consolidation_default` | `() → z_query_consolidation_t` | `ZQueryConsolidation.Default()` |
| `z_query_consolidation_auto` | `() → z_query_consolidation_t` | `ZQueryConsolidation.Auto()` |
| `z_query_consolidation_latest` | `() → z_query_consolidation_t` | `ZQueryConsolidation.Latest()` |
| `z_query_consolidation_monotonic` | `() → z_query_consolidation_t` | `ZQueryConsolidation.Monotonic()` |
| `z_query_consolidation_none` | `() → z_query_consolidation_t` | `ZQueryConsolidation.None()` |
| `z_reply_keyexpr_default` | `() → z_reply_keyexpr_t` | (enum のデフォルト値、定数として扱う) |

### CancellationToken 系 (`z_cancellation_token_*`)

| Native API | シグネチャ | Binding 側マッピング |
|---|---|---|
| `z_internal_cancellation_token_null` | `(z_owned_cancellation_token_t*) → void` | ZOwnedCancellationToken ctor (private) |
| `z_internal_cancellation_token_check` | `(z_owned_cancellation_token_t*) → bool` | `ZOwnedCancellationToken.Check()` |
| `z_cancellation_token_new` | `(z_owned_cancellation_token_t*) → z_result_t` | `ZOwnedCancellationToken.TryCreateNew(out ...)` |
| `z_cancellation_token_loan` | `(z_owned_cancellation_token_t*) → z_loaned_cancellation_token_t*` | `ZOwnedCancellationToken.Loan()` |
| `z_cancellation_token_loan_mut` | `(z_owned_cancellation_token_t*) → z_loaned_cancellation_token_t*` | `ZOwnedCancellationToken.LoanMut()` |
| `z_cancellation_token_clone` | `(z_owned_cancellation_token_t* dst, z_loaned_cancellation_token_t*) → void` | `ZOwnedCancellationToken.CreateClone(...)` |
| `z_cancellation_token_drop` | `(z_moved_cancellation_token_t*) → void` | `ZOwnedCancellationToken.Dispose()` (private) |
| `z_cancellation_token_cancel` | `(z_loaned_cancellation_token_t*) → z_result_t` | `ZLoanedCancellationToken.Cancel()` |
| `z_cancellation_token_is_cancelled` | `(z_loaned_cancellation_token_t*) → bool` | `ZLoanedCancellationToken.IsCancelled()` |

### Options 系

| Native API | Binding 側マッピング |
|---|---|
| `z_queryable_options_default` | `ZQueryableOptions.Default()` |
| `z_query_reply_options_default` | `ZQueryReplyOptions.Default()` |
| `z_query_reply_err_options_default` | `ZQueryReplyErrOptions.Default()` |
| `z_query_reply_del_options_default` | `ZQueryReplyDelOptions.Default()` |
| `z_get_options_default` | `ZGetOptions.Default()` |

---

## ファイル構成

`00_bindings-generation-rules.md` のファイル組織ルール（1 file per concept、
cross-concept methods は receiver のファイル）に従い、以下の 5 ファイルを新規作成し、
Session.cs に 2 メソッドを追加する。

### 1. `Bindings/Query.cs` — Query コンセプト

ルールドキュメント記載: 「`Query.cs` holds `ZOwnedQuery`, `ZLoanedQuery`, the three
`ZQueryReply*Options`, and the `ZReplyKeyExpr` enum.」

含める型:

```
ZReplyKeyExpr             enum (z_reply_keyexpr_t の再宣言)
ZQueryReplyOptions        sealed class, IDisposable
ZQueryReplyErrOptions     sealed class, IDisposable
ZQueryReplyDelOptions     sealed class, IDisposable
ZOwnedQuery               sealed class, IDisposable
ZLoanedQuery              sealed class
```

#### `ZReplyKeyExpr` enum

```csharp
public enum ZReplyKeyExpr : uint
{
    Any = 0,
    MatchingQuery = 1,
}
```

#### `ZCongestionControl` enum (z_congestion_control_t の再宣言)

```csharp
public enum ZCongestionControl : uint
{
    Block = 0,
    Drop = 1,
    BlockFirst = 2,
}
```

#### `ZPriority` enum (z_priority_t の再宣言)

```csharp
public enum ZPriority : uint
{
    RealTime = 1,
    InteractiveHigh = 2,
    InteractiveLow = 3,
    DataHigh = 4,
    Data = 5,
    DataLow = 6,
    Background = 7,
}
```

#### `ZLocality` enum (z_locality_t の再宣言)

```csharp
public enum ZLocality : uint
{
    Any = 0,
    SessionLocal = 1,
    Remote = 2,
}
```

これらの enum は複数の Options クラスで使われるため Query.cs に置く。
将来 Publisher 等でも使う場合はファイル移動を検討する。

#### `ZQueryReplyOptions` (IDisposable)

ネイティブ構造体 `z_query_reply_options_t` のラッパー。
`z_moved_encoding_t* encoding` と `z_moved_bytes_t* attachment` を持つため IDisposable。

- `internal z_query_reply_options_t Native;`
- `static Default()` — `z_query_reply_options_default`
- `SetEncoding(ZOwnedEncoding movingEncoding)` — encoding フィールドを adopt
- `SetEncoding(ZLoanedEncoding loanedEncoding)` — clone 経由の convenience overload
- `SetCongestionControl(ZCongestionControl value)` — 値コピー
- `SetPriority(ZPriority value)` — 値コピー
- `SetIsExpress(bool value)` — 値コピー
- `SetTimestamp(ZLoanedTimestamp timestamp)` — `timestamp.NativePointer` をコピー
- `SetSourceInfo(ZLoanedSourceInfo sourceInfo)` — `sourceInfo.NativePointer` をコピー
- `SetAttachment(ZOwnedBytes movingAttachment)` — attachment フィールドを adopt
- `Dispose()` — ReleaseEncoding + ReleaseAttachment
- ファイナライザなし (OwnedStorage adopt のため)

パターン: `ZPublisherPutOptions` と同一 (moved フィールドの adopt 部分)。

#### `ZQueryReplyErrOptions` (IDisposable)

ネイティブ構造体 `z_query_reply_err_options_t` のラッパー。
`z_moved_encoding_t* encoding` のみ。

- `internal z_query_reply_err_options_t Native;`
- `static Default()` — `z_query_reply_err_options_default`
- `SetEncoding(ZOwnedEncoding movingEncoding)` — encoding を adopt
- `SetEncoding(ZLoanedEncoding loanedEncoding)` — convenience overload
- `Dispose()` — ReleaseEncoding

#### `ZQueryReplyDelOptions` (IDisposable)

ネイティブ構造体 `z_query_reply_del_options_t` のラッパー。
`z_moved_bytes_t* attachment` を持つため IDisposable。

- `internal z_query_reply_del_options_t Native;`
- `static Default()` — `z_query_reply_del_options_default`
- `SetCongestionControl(ZCongestionControl value)` — 値コピー
- `SetPriority(ZPriority value)` — 値コピー
- `SetIsExpress(bool value)` — 値コピー
- `SetTimestamp(ZLoanedTimestamp timestamp)` — `timestamp.NativePointer` をコピー
- `SetSourceInfo(ZLoanedSourceInfo sourceInfo)` — `sourceInfo.NativePointer` をコピー
- `SetAttachment(ZOwnedBytes movingAttachment)` — attachment を adopt
- `Dispose()` — ReleaseAttachment

##### ※3 `timestamp` / `source_info` ポインタフィールドの取り扱い

`z_timestamp_t*` と `z_source_info_t*` は moved ポインタではなく、呼び出し側が所有する
データへの参照ポインタである。public API では raw pointer を露出せず、既存の
`ZLoanedTimestamp` / `ZLoanedSourceInfo` を受け取り、内部で `NativePointer` を
native struct に書き込む。Options ラッパーはポインタをコピーするのみで、指し先の
ライフタイム管理は呼び出し側の責務。`using` スコープ内でネイティブ呼び出しが完了する
典型的な使用パターンでは問題にならない。

#### `ZOwnedQuery` (IDisposable)

ライフサイクルパターン（alloc + null + dispose + take + loan）は `ZOwnedSample` に基づくが、
以下の点が異なる:
- コンストラクタは **private** (Query はユーザーが直接 new するものではなく、callback 内の
  loaned query から clone/take で取得するため)
- **静的ファクトリ** `CreateClone` / `CreateTakeFromLoaned` を持つ

- private ctor: `NativeStorage.Alloc<z_owned_query_t>()` + `z_internal_query_null`
- `Loan()` → `ZLoanedQuery` via `z_query_loan`
- `LoanMut()` → `ZLoanedQuery` via `z_query_loan_mut`
- `Check()` → `bool` via `z_internal_query_check`
- `internal Take()` → `OwnedStorage<z_owned_query_t>`
- `static CreateClone(ZLoanedQuery source)` → `ZOwnedQuery` via `z_query_clone`
- `static CreateTakeFromLoaned(ZLoanedQuery source)` → `ZOwnedQuery` via `z_query_take_from_loaned`
- `Dispose()` — `z_query_drop` + `NativeStorage.Free`
- finalizer あり

#### `ZLoanedQuery`

パターン: `ZLoanedSample` と同一（owner は `object` 型、loaned query はコールバック経由で
ClosureState からも、owned からも借用されるため）。

- `internal z_loaned_query_t* NativePointer`
- `KeyExpr()` → `ZLoanedKeyExpr`
- `Parameters()` → `string` (※1)
- `Payload()` → `ZLoanedBytes` (nullable: ポインタが null の場合 null)
- `PayloadMut()` → `ZLoanedBytes` (nullable)
- `Encoding()` → `ZLoanedEncoding`
- `Attachment()` → `ZLoanedBytes` (nullable)
- `AttachmentMut()` → `ZLoanedBytes` (nullable)
- `SourceInfo()` → `ZLoanedSourceInfo` (nullable, 既存 Sample.cs:175 の定義を使用)
- `AcceptsReplies()` → `ZReplyKeyExpr`
- `Reply(ZLoanedKeyExpr keyExpr, ZOwnedBytes movingPayload, ZQueryReplyOptions options)` → `ZResult`
- `ReplyErr(ZOwnedBytes movingPayload, ZQueryReplyErrOptions options)` → `ZResult`
- `ReplyDel(ZLoanedKeyExpr keyExpr, ZQueryReplyDelOptions options)` → `ZResult`

##### ※1 `Parameters()` の実装方針

`z_query_parameters` は `z_view_string_t*` を出力パラメータとして受け取る。
`z_view_string_t` は query が所有するメモリへのビューであり、スタック上に確保してよい。

```csharp
public string Parameters()
{
    z_view_string_t viewStr;
    ZenohNative.z_query_parameters(NativePointer, &viewStr);
    z_loaned_string_t* loaned = ZenohNative.z_view_string_loan(&viewStr);
    byte* data = ZenohNative.z_string_data(loaned);       // ZenohNative.g.cs:4544
    nuint len = ZenohNative.z_string_len(loaned);          // ZenohNative.g.cs:4538
    return System.Text.Encoding.UTF8.GetString(data, (int)len);
}
```

`z_view_string_t` 自体は native memory を所有しないため Dispose 不要。
低レベルラッパーでは managed `string` への変換コストを許容する。

---

### 2. `Bindings/Reply.cs` — Reply コンセプト

含める型:

```
ZOwnedReply               sealed class, IDisposable
ZLoanedReply              sealed class
ZOwnedReplyErr            sealed class, IDisposable
ZLoanedReplyErr           sealed class
```

#### `ZOwnedReply` (IDisposable)

ライフサイクルパターン（alloc + null + dispose + take + loan）は `ZOwnedSample` に基づくが、
以下の点が異なる:
- コンストラクタは **private** + 静的ファクトリ `CreateClone` / `CreateTakeFromLoaned` を持つ
  (Reply はコールバック内の loaned reply から clone/take で取得する用途が主)
- fifo/ring handler の recv 先として使う場合は public ctor が必要になるが、handler 実装は
  本計画のスコープ外のため初回は private ctor で実装する

- private ctor: `NativeStorage.Alloc<z_owned_reply_t>()` + `z_internal_reply_null`
- `Loan()` → `ZLoanedReply` via `z_reply_loan`
- `LoanMut()` → `ZLoanedReply` via `z_reply_loan_mut`
- `Check()` → `bool` via `z_internal_reply_check`
- `internal Take()` → `OwnedStorage<z_owned_reply_t>`
- `static CreateClone(ZLoanedReply source)` → `ZOwnedReply` via `z_reply_clone`
- `static CreateTakeFromLoaned(ZLoanedReply source)` → `ZOwnedReply` via `z_reply_take_from_loaned`
- `internal NativePointer` プロパティ (fifo/ring handler の recv で書き込み先として使用)
- `Dispose()` — `z_reply_drop` + `NativeStorage.Free`
- finalizer あり

#### `ZLoanedReply`

owner は `object` 型（ZOwnedReply または ClosureState いずれからも借用）。

- `internal z_loaned_reply_t* NativePointer`
- `IsOk()` → `bool` via `z_reply_is_ok`
- `Ok()` → `ZLoanedSample` (nullable: ポインタが null の場合) via `z_reply_ok`
- `OkMut()` → `ZLoanedSample` (nullable) via `z_reply_ok_mut`
- `Err()` → `ZLoanedReplyErr` (nullable) via `z_reply_err`
- `ErrMut()` → `ZLoanedReplyErr` (nullable) via `z_reply_err_mut`
- `ReplierId(out z_entity_global_id_t id)` → `bool` via `z_reply_replier_id` (※2)

##### ※2 `ReplierId` の z_entity_global_id_t 取り扱い

`z_entity_global_id_t` は値型構造体で、ポインタ経由の借用ではないため rule 12
の対象外。現時点ではネイティブ構造体をそのまま `out` パラメータとして返す。
将来 `ZEntityGlobalId` ラッパーが必要になったら追加する。

#### `ZOwnedReplyErr` (IDisposable)

ライフサイクルパターンは `ZOwnedSample` に基づくが、コンストラクタは **private**、
静的ファクトリは `CreateClone` のみ。Take() は不要（後述）。

- private ctor: `NativeStorage.Alloc<z_owned_reply_err_t>()` + `z_internal_reply_err_null`
- `Loan()` → `ZLoanedReplyErr` via `z_reply_err_loan`
- `LoanMut()` → `ZLoanedReplyErr` via `z_reply_err_loan_mut`
- `Check()` → `bool` via `z_internal_reply_err_check`
- `static CreateClone(ZLoanedReplyErr source)` → `ZOwnedReplyErr` via `z_reply_err_clone`
- `Dispose()` — `z_reply_err_drop` + `NativeStorage.Free`
- finalizer あり

Take() は不要 — `z_moved_reply_err_t*` を消費する zenoh-c API が z_reply_err_drop
以外に存在しない (rule 7 の条件を満たさない)。

#### `ZLoanedReplyErr`

owner は `object` 型。

- `internal z_loaned_reply_err_t* NativePointer`
- `Payload()` → `ZLoanedBytes` via `z_reply_err_payload`
- `PayloadMut()` → `ZLoanedBytes` via `z_reply_err_payload_mut`
- `Encoding()` → `ZLoanedEncoding` via `z_reply_err_encoding`

---

### 3. `Bindings/Queryable.cs` — Queryable コンセプト

ClosureSample → Subscriber.cs のパターンに倣い、ClosureQuery もこのファイルに含める。

含める型:

```
ZClosureQueryCall         delegate
ZClosureQueryDrop         delegate
ZOwnedClosureQuery        sealed class, IDisposable
ZLoanedClosureQuery       sealed class
ZQueryableOptions         sealed class (IDisposable 不要)
ZOwnedQueryable           sealed class, IDisposable
ZLoanedQueryable          sealed class
```

#### Closure delegates

```csharp
public delegate void ZClosureQueryCall(ZLoanedQuery query, IntPtr context);
public delegate void ZClosureQueryDrop(IntPtr context);
```

#### `ZOwnedClosureQuery` (IDisposable)

パターン: `ZOwnedClosureSample` と完全に同一。

- static readonly `NativeCall` / `NativeDrop` (MonoPInvokeCallback)
- `Set(ZClosureQueryCall call, ZClosureQueryDrop drop, IntPtr context)`
- `Loan()` / `LoanMut()` / `Check()`
- `internal Take()` → `OwnedStorage<z_owned_closure_query_t>`
- DispatchCall: `z_loaned_query_t*` → `new ZLoanedQuery(ptr, state)` を作り callback 呼出
- DispatchDrop: GCHandle 解放 + drop delegate 呼出

#### `ZLoanedClosureQuery`

- `Call(ZLoanedQuery query)` via `z_closure_query_call`

#### `ZQueryableOptions`

ネイティブ構造体 `z_queryable_options_t` のラッパー。
`z_moved_*` フィールドなし → IDisposable 不要。

- `internal z_queryable_options_t Native;`
- `static Default()` — `z_queryable_options_default`
- `SetComplete(bool value)` — 値コピー
- `SetAllowedOrigin(z_locality_t value)` — 値コピー

#### `ZOwnedQueryable` (IDisposable)

パターン: `ZOwnedSubscriber` と同一。

- public ctor: `NativeStorage.Alloc<z_owned_queryable_t>()` + `z_internal_queryable_null`
- `Declare(ZLoanedSession session, ZLoanedKeyExpr keyExpr, ZOwnedClosureQuery movingCallback, ZQueryableOptions options)` → `ZResult`
  - z_declare_queryable の第 1 引数は session だが、出力先が owned_queryable であるため
    既存 Subscriber パターンに倣い ZOwnedQueryable のインスタンスメソッドにする
- `Loan()` → `ZLoanedQueryable` via `z_queryable_loan`
- `Check()` → `bool` via `z_internal_queryable_check`
- `Dispose()` — `z_queryable_drop` + `NativeStorage.Free`
- finalizer あり

`z_undeclare_queryable` について: `z_queryable_drop` と等価だが z_result_t を返す。
Dispose() では drop を使う。undeclare の結果を知りたい場合のために
`Undeclare()` メソッドを追加することも考えられるが、既存 Subscriber に `Undeclare`
がないため初回実装では見送り、必要になった段階で追加する。

#### `ZLoanedQueryable`

- `internal z_loaned_queryable_t* NativePointer`
- `KeyExpr()` → `ZLoanedKeyExpr` via `z_queryable_keyexpr`
- `Id()` → `z_entity_global_id_t` via `z_queryable_id` (※2 と同様、値型そのまま返す)

---

### 4. `Bindings/CancellationToken.cs` — CancellationToken コンセプト

CancellationToken は `z_get_options_t`, `z_liveliness_get_options_t`,
`z_querier_get_options_t` など複数の Options で使われる独立したネイティブ概念であるため、
「one file per native concept」に従い独立ファイルとする。

含める型:

```
ZOwnedCancellationToken   sealed class, IDisposable
ZLoanedCancellationToken  sealed class
```

#### `ZOwnedCancellationToken` (IDisposable)

ライフサイクルパターンは `ZOwnedSample` に基づくが、コンストラクタは **private**、
静的ファクトリ `TryCreateNew` / `CreateClone` を持つ。

- private ctor: `NativeStorage.Alloc<z_owned_cancellation_token_t>()` + `z_internal_cancellation_token_null`
- `static TryCreateNew(out ZOwnedCancellationToken value)` → `ZResult` via `z_cancellation_token_new`
- `static CreateClone(ZLoanedCancellationToken source)` → `ZOwnedCancellationToken` via `z_cancellation_token_clone`
- `Loan()` → `ZLoanedCancellationToken` via `z_cancellation_token_loan`
- `LoanMut()` → `ZLoanedCancellationToken` via `z_cancellation_token_loan_mut`
- `Check()` → `bool` via `z_internal_cancellation_token_check`
- `internal Take()` → `OwnedStorage<z_owned_cancellation_token_t>`
- `Dispose()` — `z_cancellation_token_drop` + `NativeStorage.Free`
- finalizer あり

#### `ZLoanedCancellationToken`

owner は `ZOwnedCancellationToken`。

- `internal z_loaned_cancellation_token_t* NativePointer`
- `Cancel()` → `ZResult` via `z_cancellation_token_cancel`
- `IsCancelled()` → `bool` via `z_cancellation_token_is_cancelled`

---

### 5. `Bindings/Get.cs` — Get コンセプト

ルールドキュメント記載: 「`Get.cs` holds `ZGetOptions`, `ZQueryConsolidation`, and the
`ZQueryTarget` / `ZConsolidationMode` enums.」

ClosureReply もこのファイルに含める（z_get が ClosureReply の消費先）。

含める型:

```
ZQueryTarget              enum (z_query_target_t の再宣言)
ZConsolidationMode        enum (z_consolidation_mode_t の再宣言)
ZQueryConsolidation       readonly struct
ZClosureReplyCall         delegate
ZClosureReplyDrop         delegate
ZOwnedClosureReply        sealed class, IDisposable
ZLoanedClosureReply       sealed class
ZGetOptions               sealed class, IDisposable
```

#### `ZQueryTarget` enum

```csharp
public enum ZQueryTarget : uint
{
    BestMatching = 0,
    All = 1,
    AllComplete = 2,
}
```

#### `ZConsolidationMode` enum

```csharp
public enum ZConsolidationMode : int
{
    Auto = -1,
    None = 0,
    Monotonic = 1,
    Latest = 2,
}
```

#### `ZQueryConsolidation` struct

```csharp
public readonly struct ZQueryConsolidation
{
    private readonly z_query_consolidation_t native;

    internal ZQueryConsolidation(z_query_consolidation_t native) { this.native = native; }
    internal z_query_consolidation_t Native => native;

    public ZConsolidationMode Mode => (ZConsolidationMode)native.mode;

    public static ZQueryConsolidation Default() => new(ZenohNative.z_query_consolidation_default());
    public static ZQueryConsolidation Auto() => new(ZenohNative.z_query_consolidation_auto());
    public static ZQueryConsolidation Latest() => new(ZenohNative.z_query_consolidation_latest());
    public static ZQueryConsolidation Monotonic() => new(ZenohNative.z_query_consolidation_monotonic());
    public static ZQueryConsolidation None() => new(ZenohNative.z_query_consolidation_none());
}
```

#### Closure delegates

```csharp
public delegate void ZClosureReplyCall(ZLoanedReply reply, IntPtr context);
public delegate void ZClosureReplyDrop(IntPtr context);
```

#### `ZOwnedClosureReply` (IDisposable)

パターン: `ZOwnedClosureSample` と完全に同一。
DispatchCall 内で `new ZLoanedReply(ptr, state)` を生成して callback へ渡す。

#### `ZLoanedClosureReply`

- `Call(ZLoanedReply reply)` via `z_closure_reply_call`

#### `ZGetOptions` (IDisposable)

ネイティブ構造体 `z_get_options_t` のラッパー。
`z_moved_bytes_t* payload`, `z_moved_encoding_t* encoding`,
`z_moved_bytes_t* attachment`, `z_moved_cancellation_token_t* cancellation_token`
を持つため IDisposable。

- `internal z_get_options_t Native;`
- `static Default()` — `z_get_options_default`
- `SetTarget(ZQueryTarget target)` — 値コピー
- `SetConsolidation(ZQueryConsolidation consolidation)` — 値コピー
- `SetPayload(ZOwnedBytes movingPayload)` — payload を adopt
- `SetEncoding(ZOwnedEncoding movingEncoding)` — encoding を adopt
- `SetEncoding(ZLoanedEncoding loanedEncoding)` — convenience overload
- `SetCongestionControl(ZCongestionControl value)` — 値コピー
- `SetIsExpress(bool value)` — 値コピー
- `SetAllowedDestination(ZLocality value)` — 値コピー
- `SetAcceptReplies(ZReplyKeyExpr value)` — 値コピー
- `SetPriority(ZPriority value)` — 値コピー
- `SetSourceInfo(ZLoanedSourceInfo sourceInfo)` — `sourceInfo.NativePointer` をコピー (※3)
- `SetAttachment(ZOwnedBytes movingAttachment)` — attachment を adopt
- `SetTimeoutMs(ulong timeoutMs)` — 値コピー
- `SetCancellationToken(ZOwnedCancellationToken movingCancellationToken)` — cancellation_token を adopt
- `Dispose()` — ReleasePayload + ReleaseEncoding + ReleaseAttachment + ReleaseCancellationToken
- ファイナライザなし

---

### 6. `Session.cs` への追加 — cross-concept メソッド

`z_get` と `z_declare_background_queryable` は receiver が `z_loaned_session_t*`
であるため `ZLoanedSession` に追加する。

#### `ZLoanedSession.Get(...)`

```csharp
public ZResult Get(
    ZLoanedKeyExpr keyExpr,
    byte[] nullTerminatedParameters,   // null-terminated UTF-8
    ZOwnedClosureReply movingCallback,
    ZGetOptions options)
```

- `movingCallback.Take()` で storage を取得し `using` で保持
- `z_get(NativePointer, keyExpr.NativePointer, parametersPtr, (z_moved_closure_reply_t*)storage.Pointer, &options.Native)`

#### `ZLoanedSession.DeclareBackgroundQueryable(...)`

```csharp
public ZResult DeclareBackgroundQueryable(
    ZLoanedKeyExpr keyExpr,
    ZOwnedClosureQuery movingCallback,
    ZQueryableOptions options)
```

---

## 実装順序

依存関係を考慮し、以下の順序で実装する。

| Step | ファイル | 理由 |
|------|---------|------|
| 1 | `CancellationToken.cs` | 他の型への依存なし。ZGetOptions が adopt する |
| 2 | `Reply.cs` | 他の型への依存が少ない。ZLoanedReply は ClosureReply コールバック内で使われる |
| 3 | `Get.cs` | ZQueryConsolidation / enums + ClosureReply + ZGetOptions。Reply.cs, CancellationToken.cs に依存 |
| 4 | `Query.cs` | ZQueryReply*Options + ZOwnedQuery / ZLoanedQuery。Get.cs の enum に依存 |
| 5 | `Queryable.cs` | ClosureQuery + ZOwnedQueryable。Query.cs に依存 (callback で ZLoanedQuery を渡す) |
| 6 | `Session.cs` 追加 | Get.cs と Queryable.cs の型を使うため最後 |

---

## テスト計画

`00_bindings-generation-rules.md` のテスト要件に従う。

### ユニットテスト (`BindingsBasicTests.cs`)

- **ZReplyKeyExpr enum**: `z_reply_keyexpr_default()` の戻り値と
  `ZReplyKeyExpr.MatchingQuery` が一致することを検証
- **ZQueryTarget enum**: `z_query_target_default()` の戻り値と
  `ZQueryTarget.BestMatching` が一致することを検証
- **ZConsolidationMode enum**: `ZQueryConsolidation.Default().Mode` が
  `ZConsolidationMode.Auto` であることを検証。各ファクトリ
  (Auto/Latest/Monotonic/None) が正しい Mode 値を返すことを検証
- **ZQueryReplyOptions**: `SetEncoding` / `SetAttachment` 後にソースの ZOwned* が
  inert (ObjectDisposedException) になること、Dispose が 2 回呼べること
- **ZQueryReplyErrOptions**: 同上 (encoding のみ)
- **ZQueryReplyDelOptions**: 同上 (attachment のみ)
- **ZGetOptions**: 全セッター (`SetPayload` / `SetEncoding` / `SetAttachment` /
  `SetConsolidation` / `SetTarget` / `SetTimeoutMs` / `SetCongestionControl` /
  `SetIsExpress` / `SetAllowedDestination` / `SetAcceptReplies` / `SetPriority` /
  `SetCancellationToken`) の基本動作
- **ZOwnedCancellationToken**: `TryCreateNew` → `Loan()` → `Cancel()` /
  `IsCancelled()` のライフサイクル検証
- **ZQueryableOptions**: `Default()` で生成し Dispose 不要であることを検証
- **ZOwnedClosureQuery**: managed callback を Set し、Take 後にソースが inert に
  なることを検証 (既存 `ZOwnedClosureSample` テスト BindingsBasicTests.cs:144 と同パターン)
- **ZOwnedClosureReply**: 同上
- **ZOwnedReplyErr**: `CreateClone` → `Loan()` → `Payload()` / `Encoding()` のチェーン
  (ReplyErr は z_get 経由でしか取得できないため、単体テストは限定的になる可能性あり)

### 統合テスト (`BindingsZenohdIntegrationTests.cs`)

- **Query/Reply ラウンドトリップ**:
  1. Session を開く
  2. Queryable を declare し、コールバック内で `query.Reply(...)` を呼ぶ
  3. 同じ Session (または別 Session) から `Get(...)` を実行
  4. ClosureReply コールバックで受け取った Reply が `IsOk() == true` で、
     payload が送信データと一致することを検証
- **ReplyErr ラウンドトリップ**: コールバック内で `query.ReplyErr(...)` を呼び、
  Get 側で `reply.IsOk() == false` + `reply.Err().Payload()` を検証
- **ReplyDel ラウンドトリップ**: コールバック内で `query.ReplyDel(...)` を呼び、
  Get 側で `reply.Ok().Kind() == ZSampleKind.Delete` を検証
- **Query フィールド検証**: Queryable コールバック内で `query.KeyExpr()`,
  `query.Parameters()`, `query.Payload()`, `query.Encoding()`, `query.Attachment()`
  の値が Get 側で設定した値と一致することを検証
- **CreateClone / CreateTakeFromLoaned**: コールバック内で受け取った loaned query を
  clone して owned にし、コールバック外でも使えることを検証

---

## 未決事項・メモ

1. **`z_view_string_t` の汎用化**: `Parameters()` で使う `z_view_string_t` →
   managed string 変換は `KeyExpr` の `z_keyexpr_as_view_string` でも同じパターンが
   必要になる。ヘルパーメソッド（例: `ZViewStringUtils.ToManagedString`）を
   `Internal/` に置くことを検討。
2. **`z_entity_global_id_t` ラッパー**: 現時点ではネイティブ構造体を直接返す。
   公開 API として不適切な場合は `ZEntityGlobalId` readonly struct を導入する。
3. **z_undeclare_queryable**: 初回実装では見送り。`z_queryable_drop` のネイティブ
   ドキュメントに「This is equivalent to calling `z_undeclare_queryable()` and
   discarding its return value.」とあり（ZenohNative.g.cs:224）、`Dispose()` で
   `z_queryable_drop` を呼ぶため機能的に代用できる。違いは `z_undeclare_queryable` が
   `z_result_t` を返す点のみ。既存の `ZOwnedSubscriber` にも `Undeclare` がないため
   一貫性を優先し、undeclare の成否確認が必要になった段階で追加する。
4. **z_get_with_parameters_substr**: `z_get` の byte*/len バリアント。初回は
   null-terminated `z_get` のみ公開し、必要に応じて追加する。
