# Query/Reply HighLevel Wrapper 実装計画

## 概要

Bindings 層の Query/Reply/Queryable/Get API に対応する HighLevel Wrapper を実装する。
既存の Subscriber / Publisher / SampleView の実装パターンに倣い、
`00_highlevel_wrapper_generation_rules.md` に準拠した設計とする。

## 対象 Bindings 型と HighLevel 型の対応

| Bindings 型 | HighLevel 型 | パターン | 説明 |
|---|---|---|---|
| `ZOwnedQueryable` | `Queryable` | A (delegation class) | クエリを受けるサーバ側リソース |
| `ZLoanedQuery` | `QueryView` | B (readonly struct) | コールバック内で受け取るクエリの借用ビュー |
| `ZLoanedReply` | `ReplyView` | B (readonly struct) | Get コールバック内で受け取る返信の借用ビュー |
| `ZLoanedReplyErr` | (ReplyView 内でアクセス) | -- | `ReplyView` のプロパティ経由で公開 |
| `ZQueryReplyOptions` | `QueryReplyOptions` | POCO | QueryView.Reply に渡すオプション |
| `ZQueryReplyErrOptions` | `QueryReplyErrOptions` | POCO | QueryView.ReplyErr に渡すオプション |
| `ZQueryReplyDelOptions` | `QueryReplyDelOptions` | POCO | QueryView.ReplyDel に渡すオプション |
| `ZGetOptions` | `GetOptions` | POCO | Session.Get に渡すオプション |
| `ZQueryableOptions` | `QueryableDeclareOptions` | POCO | Queryable.Declare に渡すオプション |
| `ZQueryConsolidation` | (GetOptions 内で利用) | -- | `GetOptions.Consolidation` プロパティとして公開 |

## 新規ファイル一覧

```
Runtime/HighLevel/
  Queryable.cs              # Queryable (Pattern A) + QueryableDeclareOptions
  QueryView.cs              # QueryView (Pattern B) + QueryReplyOptions / QueryReplyErrOptions / QueryReplyDelOptions
  ReplyView.cs              # ReplyView (Pattern B)
  GetOptions.cs             # GetOptions (POCO, Session.Get 用)
```

Session.cs に `Get` / `TryGet` メソッドを追加する (新規ファイルは不要)。

---

## 1. QueryView (Pattern B: readonly struct)

`ZLoanedQuery` のコールバック内借用ビュー。Queryable のコールバック `Action<QueryView>`
の引数として渡される。コールバック外での保持は禁止。

### 公開 API

```csharp
public readonly struct QueryView
{
    private readonly ZLoanedQuery loaned;

    internal QueryView(ZLoanedQuery loaned);

    // --- 読み取りアクセサ ---
    public string KeyExpr { get; }            // loaned.KeyExpr().AsString()
    public string Parameters { get; }         // loaned.Parameters()

    public int PayloadLength { get; }
    public byte[] PayloadAsBytes();           // loaned.Payload()?.ToByteArray()
    public int PayloadCopyTo(byte[] dst, int dstOffset = 0);
    public string PayloadAsString();          // loaned.Payload()?.ToStringUtf8()

    public string EncodingMime { get; }       // loaned.Encoding()?.AsString()

    public byte[] AttachmentAsBytes();        // loaned.Attachment()?.ToByteArray()

    // --- 返信メソッド ---

    /// <summary>
    /// クエリに対してペイロード付きで返信する。
    /// </summary>
    public void Reply(string keyExpr, byte[] payload, QueryReplyOptions options = null);
    public void Reply(string keyExpr, string payload, QueryReplyOptions options = null);
    public bool TryReply(string keyExpr, byte[] payload, QueryReplyOptions options, out ZResult result);
    public bool TryReply(string keyExpr, string payload, QueryReplyOptions options, out ZResult result);

    /// <summary>
    /// クエリに対してエラーを返信する。
    /// </summary>
    public void ReplyErr(byte[] payload, QueryReplyErrOptions options = null);
    public void ReplyErr(string payload, QueryReplyErrOptions options = null);
    public bool TryReplyErr(byte[] payload, QueryReplyErrOptions options, out ZResult result);
    public bool TryReplyErr(string payload, QueryReplyErrOptions options, out ZResult result);

    /// <summary>
    /// クエリに対して DELETE 返信を送る。
    /// </summary>
    public void ReplyDel(string keyExpr, QueryReplyDelOptions options = null);
    public bool TryReplyDel(string keyExpr, QueryReplyDelOptions options, out ZResult result);
}
```

### 実装ノート

- `Reply` の `keyExpr` 引数は `string` で受け取り、内部で `ZOwnedKeyExpr.TryCreateFromStr` を
  使って一時的な `ZOwnedKeyExpr` を作成・`Loan` し、呼び出し後に `Dispose` する。
  - **keyExpr パース失敗時**: throwing 版は `ZenohException` を throw (`result.ThrowIfErr(...)` パターン)、
    Try 版は `out ZResult` にパース失敗コードを返す。keyExpr パース失敗と Reply ネイティブコール失敗は
    同じ `ZResult` に統合されるため、呼び出し側はどちらの原因かを区別できない点に注意。
- `payload` は `BytesFactory.FromBuffer` / `BytesFactory.FromString` で `ZOwnedBytes` に変換する。
- Bindings の `ZQueryReplyOptions` 等は IDisposable を実装しているため `try / finally { Dispose() }` で管理する。
- `options` が `null` の場合はデフォルトオプションを使用する。

### Reply 内部実装フロー (概要)

```
Reply(string keyExpr, byte[] payload, options):
  1. KeyExpr ke = KeyExpr.Parse(keyExpr)  -- 一時的 (using で管理する内部実装)
     または ZOwnedKeyExpr.TryCreateFromStr で直接作成
  2. ZOwnedBytes bytes = BytesFactory.FromBuffer(payload)
  3. ZQueryReplyOptions nativeOpts = ZQueryReplyOptions.Default()
     options から各プロパティを nativeOpts に転写
  4. try { loaned.Reply(ke.Loan(), bytes, nativeOpts).ThrowIfErr(...) }
     finally { nativeOpts.Dispose(); ke.Dispose(); }
```

---

## 2. QueryReplyOptions / QueryReplyErrOptions / QueryReplyDelOptions (POCO)

QueryView の Reply メソッドに渡す POCO オプション。Bindings の `Z*Options` は
HighLevel 内部でのみ使用し、公開しない。

### QueryReplyOptions

```csharp
public sealed class QueryReplyOptions
{
    public EncodingPreset? Encoding { get; set; }
    public ZCongestionControl? CongestionControl { get; set; }
    public ZPriority? Priority { get; set; }
    public bool? IsExpress { get; set; }
    public byte[] Attachment { get; set; }
}
```

### QueryReplyErrOptions

```csharp
public sealed class QueryReplyErrOptions
{
    public EncodingPreset? Encoding { get; set; }
}
```

### QueryReplyDelOptions

```csharp
public sealed class QueryReplyDelOptions
{
    public ZCongestionControl? CongestionControl { get; set; }
    public ZPriority? Priority { get; set; }
    public bool? IsExpress { get; set; }
    public byte[] Attachment { get; set; }
}
```

### 実装ノート

- `Timestamp` / `SourceInfo` は UNSAFE な借用ポインタ操作を伴うため、
  初期実装では公開しない。必要になった時点で安全な API を設計する。
- Attachment は `byte[]` で受け取り、内部で `BytesFactory.FromBuffer` を使う。

---

## 3. Queryable (Pattern A: delegation class)

`ZOwnedQueryable` をラップし、Subscriber と同じパターンで実装する。

### 公開 API

```csharp
public sealed class QueryableDeclareOptions
{
    public bool? Complete { get; set; }
    public ZLocality? AllowedOrigin { get; set; }
}

public sealed class Queryable : IDisposable
{
    private ZOwnedQueryable owned;

    private Queryable(ZOwnedQueryable owned);

    public static Queryable Declare(
        Session session,
        KeyExpr keyExpr,
        Action<QueryView> onQuery,
        QueryableDeclareOptions options = null);

    public static bool TryDeclare(
        Session session,
        KeyExpr keyExpr,
        Action<QueryView> onQuery,
        QueryableDeclareOptions options,
        out Queryable queryable,
        out ZResult result);

    public void Dispose();
}
```

### 内部実装フロー

Subscriber.TryDeclareCore のパターンに準拠:

```
TryDeclareCore(session, keyExpr, onQuery, options, out queryable, out result):
  1. ArgumentNull チェック
  2. ZOwnedClosureQuery closure = new()
  3. ZOwnedQueryable owned = new()
  4. try:
     a. ZClosureQueryCall adapter = (loanedQuery, _) => onQuery(new QueryView(loanedQuery))
     b. closure.Set(adapter, null, IntPtr.Zero)
     c. ZQueryableOptions nativeOpts = ZQueryableOptions.Default()
        options?.Complete  -> nativeOpts.SetComplete(...)
        options?.AllowedOrigin -> nativeOpts.SetAllowedOrigin(...)
     d. result = owned.Declare(session.LoanInternal(), keyExpr.LoanInternal(), closure, nativeOpts)
     e. if (!result.IsOk) { owned.Dispose(); return false; }
     f. queryable = new Queryable(owned); return true;
  5. catch: closure.Dispose(); owned.Dispose(); throw;
```

### コールバックのスレッドモデル

- Zenoh の内部スレッドからコールバックが呼ばれる。
- HighLevel はメインスレッドへのマーシャリングを行わない
  (generation rules の Threading セクションに準拠)。
- XML doc で注意事項を明記する。

---

## 4. ReplyView (Pattern B: readonly struct)

`ZLoanedReply` のコールバック内借用ビュー。`Session.Get` のコールバック
`Action<ReplyView>` の引数として渡される。

### 公開 API

```csharp
public readonly struct ReplyView
{
    private readonly ZLoanedReply loaned;

    internal ReplyView(ZLoanedReply loaned);

    /// <summary>
    /// 返信が成功 (Ok) かどうか。
    /// </summary>
    public bool IsOk { get; }                // loaned.IsOk()

    // --- 成功時のアクセサ (IsOk == true のとき有効) ---
    public string KeyExpr { get; }           // loaned.Ok()?.KeyExpr().AsString()

    public ZSampleKind Kind { get; }         // loaned.Ok()?.Kind() ?? default

    public int PayloadLength { get; }
    public byte[] PayloadAsBytes();          // loaned.Ok()?.Payload()?.ToByteArray()
    public int PayloadCopyTo(byte[] dst, int dstOffset = 0);
    public string PayloadAsString();         // loaned.Ok()?.Payload()?.ToStringUtf8()

    public string EncodingMime { get; }      // loaned.Ok()?.Encoding()?.AsString()

    public byte[] AttachmentAsBytes();       // loaned.Ok()?.Attachment()?.ToByteArray()

    // --- エラー時のアクセサ (IsOk == false のとき有効) ---
    public byte[] ErrorPayloadAsBytes();     // loaned.Err()?.Payload()?.ToByteArray()
    public string ErrorPayloadAsString();    // loaned.Err()?.Payload()?.ToStringUtf8()
    public string ErrorEncodingMime { get; } // loaned.Err()?.Encoding()?.AsString()
}
```

### 実装ノート

- 成功時のアクセサは `loaned.Ok()` で `ZLoanedSample` を取得し、
  `SampleView` と同等のアクセサロジックを直接実装する
  (`SampleView` は `readonly struct` なのでそのまま利用することも検討可能だが、
  直接アクセスする方がシンプル)。
- `IsOk == false` のとき成功側アクセサは `null` / `0` / `default` を返す。
- `IsOk == true` のときエラー側アクセサは `null` を返す。
- `loaned.Ok()` / `loaned.Err()` が返す `ZLoanedSample` / `ZLoanedReplyErr` は
  コールバック中のみ有効。

### 代替案: OkSample / ErrorPayload に分離

`ReplyView` に `OkSample` プロパティ (`SampleView?`) を持たせ、
成功時は `SampleView` を返す設計も考えられる。

```csharp
public SampleView? OkSample { get; }  // IsOk ? new SampleView(loaned.Ok()) : null
```

**この代替案を推奨する。** 理由:
- `SampleView` の既存実装をそのまま再利用できる。
- 成功/エラーの分岐が型で明確になる。
- 将来 `SampleView` にプロパティを追加したとき自動的に `ReplyView` にも反映される。

推奨する最終 API:

```csharp
public readonly struct ReplyView
{
    internal ReplyView(ZLoanedReply loaned);

    public bool IsOk { get; }

    /// <summary>
    /// 成功時の SampleView。IsOk == false のとき null。
    /// コールバック外で保持禁止。
    /// </summary>
    public SampleView? OkSample { get; }

    // --- エラー時のアクセサ ---
    public byte[] ErrorPayloadAsBytes();
    public string ErrorPayloadAsString();
    public string ErrorEncodingMime { get; }
}
```

---

## 5. Session.Get / Session.TryGet (既存 Session クラスへの追加)

### 公開 API

```csharp
// GetOptions.cs (独立ファイル)
// Session.Get で使用するオプション。Session.cs や ReplyView.cs ではなく
// 独立ファイルに配置し、発見容易性を確保する。
public sealed class GetOptions
{
    public ZQueryTarget? Target { get; set; }
    public ZConsolidationMode? Consolidation { get; set; }
    public byte[] Payload { get; set; }
    public EncodingPreset? Encoding { get; set; }
    public ZCongestionControl? CongestionControl { get; set; }
    public bool? IsExpress { get; set; }
    public ZLocality? AllowedDestination { get; set; }
    public ZReplyKeyExpr? AcceptReplies { get; set; }
    public ZPriority? Priority { get; set; }
    public byte[] Attachment { get; set; }
    public ulong? TimeoutMs { get; set; }
}

// Session クラスに追加するメソッド
public void Get(
    KeyExpr keyExpr,
    Action<ReplyView> onReply,
    string parameters = null,
    GetOptions options = null,
    CancellationToken cancellationToken = default);

public void Get(
    string keyExprString,
    Action<ReplyView> onReply,
    string parameters = null,
    GetOptions options = null,
    CancellationToken cancellationToken = default);

public bool TryGet(
    KeyExpr keyExpr,
    Action<ReplyView> onReply,
    string parameters,
    GetOptions options,
    CancellationToken cancellationToken,
    out ZResult result);
```

### 内部実装フロー

```
Get(keyExpr, onReply, parameters, options, cancellationToken):
  1. ArgumentNull チェック (keyExpr, onReply)
  2. ThrowIfDisposed()
  3. ZOwnedClosureReply closure = new()
  4. CancellationToken 準備 (cancellationToken != default の場合):
     a. ZOwnedCancellationToken.TryCreateNew(out var zenohToken)
     b. var zenohTokenClone = ZOwnedCancellationToken.CreateClone(zenohToken.Loan())
     c. CancellationTokenRegistration reg = cancellationToken.Register(
            () => zenohTokenClone.Loan().Cancel())
  5. try:
     a. ZClosureReplyCall adapter = (loanedReply, _) => onReply(new ReplyView(loanedReply))
     b. // ZClosureReplyDrop は Action<IntPtr> 型。ラムダキャプチャ経由で
        // マネージドオブジェクトを参照する (IntPtr context は不使用)。
        // Set 内部の GCHandle が ClosureState → drop delegate → キャプチャ変数を
        // ルートとして保持するため、Zenoh が closure を drop するまで GC されない。
        ZClosureReplyDrop drop = (IntPtr _) => { reg.Dispose(); zenohTokenClone?.Dispose(); }
     c. closure.Set(adapter, drop, IntPtr.Zero)
     d. byte[] parametersBytes = parameters == null
          ? Utf8Helper.EncodeNullTerminated("")
          : Utf8Helper.EncodeNullTerminated(parameters)
     e. ZGetOptions nativeOpts = ZGetOptions.Default()
        try:
          options の各プロパティを nativeOpts に転写
          if (zenohToken != null):
            nativeOpts.SetCancellationToken(zenohToken)  // moves ownership
          result = owned.Loan().Get(keyExpr.LoanInternal(), parametersBytes, closure, nativeOpts)
          result.ThrowIfErr("Session.Get failed")
        finally:
          nativeOpts.Dispose()
  6. catch:
       // SetCancellationToken 前に例外が発生した場合、zenohToken は未 move なので
       // ここで cleanup する。SetCancellationToken 後の場合は nativeOpts.Dispose() が
       // ReleaseCancellationToken() 経由で zenohToken を解放済み。
       reg.Dispose()                    // CancellationTokenRegistration の解除
       zenohTokenClone?.Dispose()       // Clone の解放
       zenohToken?.Dispose()            // original が未 move なら解放 (move 済みなら no-op)
       closure.Dispose()
       throw;
```

**例外パスの ownership 整理:**
- `zenohToken` は `nativeOpts.SetCancellationToken()` で move される。move 後は
  `nativeOpts.Dispose()` → `ReleaseCancellationToken()` が解放を担当。
- move **前** に例外が発生した場合、`zenohToken` は呼び出し側が Dispose する責務を持つ。
  `ZOwnedCancellationToken.Dispose()` は冪等であるため、move 済みかどうかに関わらず
  catch ブロックで安全に呼べる。
- `zenohTokenClone` と `reg` は closure の drop コールバックで cleanup される想定だが、
  Get 呼び出し前に例外が発生した場合は closure が Zenoh に渡されないため
  drop コールバックは呼ばれない。catch ブロックで明示的に cleanup する。

### CancellationToken の内部動作

- `System.Threading.CancellationToken` を受け取り、Zenoh ネイティブの
  `z_cancellation_token` に橋渡しする。
- `ZOwnedCancellationToken` を生成し、Clone を作成してから original を
  `ZGetOptions.SetCancellationToken` で move する。Clone 側を
  `CancellationTokenRegistration` のコールバックで Cancel に使う。
- Closure の drop コールバック (`ZClosureReplyDrop`: `Action<IntPtr>` 型) で
  `CancellationTokenRegistration` と Clone の cleanup を行う。
  drop コールバックはラムダキャプチャ経由でマネージドオブジェクトを参照し、
  `IntPtr context` パラメータは使用しない (`IntPtr.Zero` を渡す)。
  `Set` 内部の `GCHandle` が `ClosureState` → drop delegate → キャプチャ変数を
  ルートとして保持するため、Zenoh が closure を drop するまで GC されない。
- `CancellationToken.None` (default) の場合は zenohToken 生成をスキップし、
  drop コールバックも不要なので `null` を渡す。
- **例外パスの cleanup**: Get 呼び出し前に例外が発生した場合、closure が Zenoh に
  渡されないため drop コールバックは呼ばれない。catch ブロックで `reg.Dispose()` /
  `zenohTokenClone?.Dispose()` / `zenohToken?.Dispose()` を明示的に行う。

### GetOptions POCO -> ZGetOptions 変換の詳細

```
options?.Target           -> nativeOpts.SetTarget(...)
options?.Consolidation    -> nativeOpts.SetConsolidation(
                               ZQueryConsolidation に変換)
options?.Payload          -> nativeOpts.SetPayload(BytesFactory.FromBuffer(...))
options?.Encoding         -> nativeOpts.SetEncoding(preset.ToLoanedEncoding())
options?.CongestionControl -> nativeOpts.SetCongestionControl(...)
options?.IsExpress        -> nativeOpts.SetIsExpress(...)
options?.AllowedDestination -> nativeOpts.SetAllowedDestination(...)
options?.AcceptReplies    -> nativeOpts.SetAcceptReplies(...)
options?.Priority         -> nativeOpts.SetPriority(...)
options?.Attachment       -> nativeOpts.SetAttachment(BytesFactory.FromBuffer(...))
options?.TimeoutMs        -> nativeOpts.SetTimeoutMs(...)
```

---

## 6. ファイル配置と既存ファイルへの変更

### 新規ファイル

| ファイル | 内容 |
|---|---|
| `Runtime/HighLevel/Queryable.cs` | `Queryable` class + `QueryableDeclareOptions` class |
| `Runtime/HighLevel/QueryView.cs` | `QueryView` struct + `QueryReplyOptions` / `QueryReplyErrOptions` / `QueryReplyDelOptions` |
| `Runtime/HighLevel/ReplyView.cs` | `ReplyView` struct |
| `Runtime/HighLevel/GetOptions.cs` | `GetOptions` class (Session.Get 用 POCO) |

### 変更ファイル

| ファイル | 変更内容 |
|---|---|
| `Runtime/HighLevel/Session.cs` | `Get` / `TryGet` メソッド追加 |

### サンプルシーン (実装後に追加)

| ファイル | 内容 |
|---|---|
| `Assets/ZenohSampleScenes/HighLevelTests/QueryableReplyHighLevelTest.cs` | Queryable 側の HighLevel サンプル |
| `Assets/ZenohSampleScenes/HighLevelTests/QueryGetHighLevelTest.cs` | Get 側の HighLevel サンプル |

---

## 7. 想定されるユーザコード例

### Queryable 側 (サーバ)

```csharp
using Zenoh.HighLevel;

// セッション & Queryable のセットアップ
using Session session = Session.Open(configJson);
using KeyExpr ke = KeyExpr.Parse("demo/query");
using Queryable queryable = Queryable.Declare(session, ke, query =>
{
    Debug.Log($"Query received: {query.KeyExpr}, params={query.Parameters}");

    string responsePayload = query.PayloadAsString();
    query.Reply(query.KeyExpr, $"Hello! You said: {responsePayload}");
});
```

### Queryable 側 (TryDeclare)

```csharp
using Zenoh.HighLevel;

using Session session = Session.Open(configJson);
using KeyExpr ke = KeyExpr.Parse("demo/query");

if (Queryable.TryDeclare(session, ke, query =>
{
    query.Reply(query.KeyExpr, "pong");
}, null, out var queryable, out var result))
{
    using (queryable)
    {
        // queryable を使用
    }
}
else
{
    Debug.LogError($"Queryable declare failed: {result}");
}
```

### Get 側 (クライアント)

```csharp
using Zenoh.HighLevel;
using System.Threading;

using Session session = Session.Open(configJson);
using KeyExpr ke = KeyExpr.Parse("demo/query");

// CancellationTokenSource でキャンセル可能にする
using var cts = new CancellationTokenSource();

session.Get(ke, reply =>
{
    if (reply.IsOk)
    {
        SampleView sample = reply.OkSample.Value;
        Debug.Log($"Reply: {sample.PayloadAsString()}");
    }
    else
    {
        Debug.LogWarning($"Reply error: {reply.ErrorPayloadAsString()}");
    }
},
parameters: "key=value",
options: new GetOptions { TimeoutMs = 5000 },
cancellationToken: cts.Token);

// 必要に応じてキャンセル
// cts.Cancel();
```

---

## 8. ユニットテスト計画

既存の `HighLevelBasicTests.cs` / `HighLevelZenohdIntegrationTests.cs` のパターンに準拠する。

### 8.1 BasicTests (HighLevelBasicTests.cs に追加)

zenohd 不要。null チェック・例外パスのみ検証。

| テストメソッド | 検証内容 |
|---|---|
| `Queryable_Declare_NullSessionThrowsArgumentNull` | `Queryable.Declare(null, keyExpr, _ => {})` → `ArgumentNullException` |
| `Queryable_Declare_NullCallbackThrowsArgumentNull` | `Queryable.Declare(session, keyExpr, null)` → `ArgumentNullException` (session は実在不要なため Bindings 側到達前に throw されることを確認。ただし Session が null 不可の場合は上のテストで代用) |
| `GetOptions_DefaultPropertyValues` | `new GetOptions()` の全プロパティが null / default であること |
| `QueryReplyOptions_DefaultPropertyValues` | `new QueryReplyOptions()` の全プロパティが null / default であること |

### 8.2 Integration Tests (HighLevelZenohdIntegrationTests.cs に追加)

zenohd を起動して実際のクエリ/レプライ往復を検証。
既存の `ManualResetEventSlim` + タイムアウトパターンを踏襲。

| テストメソッド | 検証内容 |
|---|---|
| `Queryable_ReplyString_DeliveredToGet` | Queryable が受けたクエリに文字列で Reply → Get 側で `ReplyView.IsOk == true`、`OkSample.PayloadAsString()` がペイロードと一致 |
| `Queryable_ReplyBytes_DeliveredToGet` | byte[] ペイロードで Reply → Get 側で `PayloadAsBytes()` が一致 |
| `Queryable_ReplyErr_DeliveredToGet` | `QueryView.ReplyErr` → Get 側で `ReplyView.IsOk == false`、`ErrorPayloadAsString()` が一致 |
| `Queryable_ReplyDel_DeliveredToGet` | `QueryView.ReplyDel` → Get 側で `OkSample.Kind == ZSampleKind.Delete` |
| `Queryable_TryDeclare_WithValidArgsReturnsTrue` | `Queryable.TryDeclare` が `true` を返し、queryable が non-null |
| `Session_Get_WithParameters_QueryReceivesParameters` | Get に `parameters: "key=value"` を渡す → Queryable 側で `QueryView.Parameters == "key=value"` |
| `Session_Get_WithPayload_QueryReceivesPayload` | `GetOptions { Payload = ... }` → Queryable 側で `QueryView.PayloadAsBytes()` が一致 |
| `Session_Get_WithEncoding_QueryReceivesEncoding` | `QueryReplyOptions { Encoding = TextPlain }` → Get 側で `OkSample.EncodingMime` が `"text/plain"` を含む |
| `Session_Get_WithAttachment_QueryReceivesAttachment` | `QueryReplyOptions { Attachment = ... }` → Get 側で `OkSample.AttachmentAsBytes()` が一致 |
| `Session_TryGet_ReturnsTrueOnSuccess` | `TryGet` が `true` を返し、コールバックが呼ばれる |
| `Session_Get_CancellationToken_CancelsGet` | `CancellationTokenSource.Cancel()` 後にクエリが中断されること (タイムアウトより早く完了することを検証) |

### 8.3 テストヘルパー (既存ヘルパーに追加)

```csharp
private static Queryable DeclareQueryable(
    Session session, string keyExprString, Action<QueryView> onQuery)
{
    using (KeyExpr keyExpr = KeyExpr.Parse(keyExprString))
    {
        return Queryable.Declare(session, keyExpr, onQuery);
    }
}
```

### 8.4 テスト実装パターン (代表例)

```csharp
[Test]
public void Queryable_ReplyString_DeliveredToGet()
{
    const string keyExpr = "unity/test/highlevel/queryreply/string";
    const string replyPayload = "hello from queryable";

    using (Session qSession = OpenClientSession())
    using (Session gSession = OpenClientSession())
    using (ManualResetEventSlim received = new ManualResetEventSlim(false))
    {
        bool receivedIsOk = false;
        string receivedPayload = null;

        Queryable queryable = DeclareQueryable(qSession, keyExpr, query =>
        {
            query.Reply(query.KeyExpr, replyPayload);
        });

        try
        {
            using (KeyExpr ke = KeyExpr.Parse(keyExpr))
            {
                gSession.Get(ke, reply =>
                {
                    receivedIsOk = reply.IsOk;
                    if (reply.IsOk)
                        receivedPayload = reply.OkSample.Value.PayloadAsString();
                    received.Set();
                });
            }

            Assert.That(received.Wait(5000), Is.True,
                "No reply received within 5 seconds.");
            Assert.That(receivedIsOk, Is.True);
            Assert.That(receivedPayload, Is.EqualTo(replyPayload));
        }
        finally
        {
            queryable.Dispose();
        }
    }
}
```

### 8.5 CancellationToken テストパターン

```csharp
[Test]
public void Session_Get_CancellationToken_CancelsGet()
{
    const string keyExpr = "unity/test/highlevel/get/cancel";

    using (Session qSession = OpenClientSession())
    using (Session gSession = OpenClientSession())
    using (ManualResetEventSlim received = new ManualResetEventSlim(false))
    using (CancellationTokenSource cts = new CancellationTokenSource())
    {
        // Queryable を宣言するが Reply しない → Get はタイムアウトまで待つ
        Queryable queryable = DeclareQueryable(qSession, keyExpr, query => { });

        try
        {
            Stopwatch sw = Stopwatch.StartNew();

            using (KeyExpr ke = KeyExpr.Parse(keyExpr))
            {
                gSession.Get(ke, reply =>
                {
                    received.Set();
                },
                options: new GetOptions { TimeoutMs = 30000 },
                cancellationToken: cts.Token);
            }

            // 即座にキャンセル
            cts.Cancel();

            // タイムアウト (30s) より大幅に早く完了することを検証
            // (closure の drop が呼ばれて received.Set() される可能性があるため Wait する)
            Thread.Sleep(2000);
            Assert.That(sw.ElapsedMilliseconds, Is.LessThan(10000),
                "Get should have been cancelled well before the 30s timeout.");
        }
        finally
        {
            queryable.Dispose();
        }
    }
}
```

---

## 9. 実装順序

1. **ReplyView** — `readonly struct`。依存が少ないため最初に実装。
2. **QueryView** — `readonly struct`。Reply メソッドが Bindings の Query.Reply を呼ぶ。
3. **Queryable** — Subscriber パターンのほぼコピー。QueryView をコールバックに渡す。
4. **Session.Get / TryGet** — Session.cs への追加。ReplyView をコールバックに渡す。
5. **BasicTests** — null チェック・POCO デフォルト値テスト (zenohd 不要)。
6. **Integration Tests** — zenohd 起動して query/reply 往復テスト。
7. **サンプルシーン** — BindingTests の HighLevel 版。

各ステップ完了ごとにコンパイル確認を行う。

---

## 10. generation rules チェックリスト

以下のチェック項目は全て対応する:

- [x] パターン選択: Queryable = A, QueryView / ReplyView = B
- [x] 型名: Z prefix / Owned / Loaned 除去
- [x] 1 ファイル 1 public type (POCO は同居可)
- [x] ファクトリメソッド: throwing + Try* バリアント
- [x] ZResult は ThrowIfErr / Try* で処理
- [x] Bindings options は try / finally { Dispose() }
- [x] Closure: ローカル作成、Set 1回、Declare に渡す、フィールドに保持しない
- [x] Dispose: 冪等、owned = null、ThrowIfDisposed
- [x] LoanInternal: 不要 (Queryable は sibling から loan される用途なし)
- [x] byte[] payload ingress: copy path by default (BytesFactory.FromBuffer → TryCreateCopyFromBuf)
- [x] 受信 payload: PayloadCopyTo / ToByteArray
- [x] Threading: コールバックはメインスレッドマーシャリングなし、doc に明記

---

## 11. スコープ外 (将来検討)

- `Querier` (declared querier) — `z_declare_querier` の HighLevel ラッパー。
  Session.Get とは別にステートフルなクエリ発行者として宣言するパターン。
  Bindings 側に `ZOwnedQuerier` が未実装のため、今回は対象外。
- async / UniTask ラッパー (`Session.GetAsync`)
- `QueryView.ReplyWithKeyExpr(KeyExpr keyExpr, ...)` — HighLevel `KeyExpr` を直接受け取るオーバーロード
- `SourceInfo` / `Timestamp` の安全な HighLevel 公開
- `QueryView.ReplyZeroCopy` / `Session.GetZeroCopy` 等の zero-copy バリアント
  (呼び出し元がバッファの不変性を保証できるケース向け)
