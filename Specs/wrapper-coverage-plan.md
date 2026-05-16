# Zenoh C# Wrapper — Full API Coverage Plan

目的: **低レベルラッパー** を新規ディレクトリ `Assets/ZenohPackage/Runtime/Bindings/` に作り、Zenoh C API（`ZenohNative.g.cs`、現状 715 関数）をほぼ網羅する。共有メモリ (SHM) は本計画では対象外（後回し）。zenoh-c のバージョン上げに際してできるだけ機械的に追従できることを副目的とする。

## 0. アーキテクチャ前提（2 層構成）

ラッパーは 2 層に分ける。**本計画は低レベルラッパーが対象**。

| 層 | ディレクトリ | namespace | 責務 |
|---|---|---|---|
| FFI バインディング | `Assets/ZenohPackage/Runtime/Plugins/` (`ZenohNative.g.cs` 他) | `Zenoh.Plugins` | csbindgen が生成する生の P/Invoke。手で触らない |
| **低レベルラッパー**（本計画） | `Assets/ZenohPackage/Runtime/Bindings/` （新規） | `Zenoh.Bindings` | C API を 1:1 に近い粒度で C# から呼ぶ層。最優先課題は **Rust 由来のメモリ問題をほぼ起こさないこと**（use-after-free / double-free / dangling loan / コールバック中の GC / 二重 Drop）。戻り値は `ZResult`、例外は引数バリデーション以外では投げない。エルゴノミクスは追わない |
| 高レベルラッパー | `Assets/ZenohPackage/Runtime/HighLevel/` （将来） | `Zenoh.HighLevel` | API ごとに使いやすさを最適化する層。例外、async/UniTask、Try 系、ストリーミング、Unity 統合（NativeArray 等）はここで扱う。低レベルラッパーの上に薄く乗せる。**本計画では設計しない** |
| （既存）`Wrappers/` | `Assets/ZenohPackage/Runtime/Wrappers/` | `Zenoh` | 低レベル/高レベルの責務が混在している。**今回は触らずそのまま残す**（既存サンプル・アプリ互換性のため）。新ディレクトリと型名が衝突しないよう namespace で隔離する |

呼称ルール: 本ドキュメント内で「ラッパー」と単独で書かない。常に「低レベルラッパー」「高レベルラッパー」と区別する。

namespace ルール:
- 既存 `Wrappers/` の `namespace Zenoh` は維持
- 新 `Bindings/` は `namespace Zenoh.Bindings` で隔離。型名は既存と同名（`Session`, `Publisher`, ...）でも namespace で区別されるので衝突しない
- `using Zenoh;` と `using Zenoh.Bindings;` を同時に書くと曖昧になるため、利用側はどちらかに揃える
- 将来 `Wrappers/` を廃止する判断が下りた段階で `Zenoh` 直下を低レベル層に再割り当てするかは別議論

---

## 1. 現状サマリ

### 既存 `Wrappers/`（`namespace Zenoh`、低レベル/高レベル責務が混在）

本計画では **触らずそのまま残す**。良い参考にはなる（特に GCHandle まわりの `Subscriber`、Encoding プリセット）。低レベルラッパーで作り直すときに引き継ぐ/見直すポイントを併記する。

| 既存ファイル | カバー範囲 | 低レベル層で作り直す際の差分 |
|---|---|---|
| `Session` / `SessionRef` | `z_open`, `z_close`, `z_session_loan`, `zc_config_from_str` の最小経路 | `Open` 内の `Debug.Log` / `try-catch` を撤去。config 構築は別クラス（`Config`）に切り出して呼び出し側に寄せる |
| `KeyExpr` / `KeyExprRef` | `z_keyexpr_from_str`, `_loan`, `_drop`, view 文字列化 | ctor の `throw new Exception` をやめ static factory + `ZResult` に |
| `Publisher` + `PublisherOptions` + `PublisherPutOptions` | `z_declare_publisher`, `z_publisher_put` の bytes/string バリアント | `Put(string)` の暗黙 `Bytes` 生成・暗黙 `throw` を排除。低レベル層は `Put(BytesRef, options) -> ZResult` のみ提供 |
| `Subscriber` + `ClosureSample` | `z_declare_subscriber` の push 型コールバック経路のみ | GCHandle + `MonoPInvokeCallback` パターンは流用。`Dispose` 二重呼び出し安全性を強化、コールバック内 throw を握りつぶす規約を文書化 |
| `Bytes` / `BytesRef` | `z_bytes_copy_from_str/buf`, `z_bytes_to_slice` 経由のバイト取得 | ctor からの `throw` を排除、factory + `ZResult`。`NativeArray`/`Span` 連携は高レベル層へ |
| `Encoding` | 全 MIME プリセット getter + clone/drop。`encoding.With(schema)` 系は未対応 | ほぼそのまま移植。schema バリアント（`with_schema_*`）を追加 |
| `Sample`（`SampleRef` のみ） | `payload`, `keyexpr` 取得 | フィールドを網羅（encoding/kind/timestamp/source_info/attachment/express） |
| `ZResult` | result code の薄いラップ | 維持。低レベル層の戻り値ポリシーの中心 |

### 未カバー（主要 — SHM除く）
- セッション情報: `z_info_*`（zid/peers/routers/transports/links）, `z_scout`, `z_hello_*`
- Get / Reply: `z_get`, `z_reply_*`, `z_reply_err_*`, `z_query_*`, `z_queryable`
- Querier（zenoh 1.x で追加された pre-declared get）: `z_querier_*`
- Liveliness: `z_liveliness_*`（token, subscriber, get）
- Matching listener: `z_publisher_*_matching_listener`, `z_querier_*_matching_listener`
- Channel ハンドラ: `z_fifo_handler_*`, `z_ring_handler_*`（subscriber/queryable/get の pull モデル）
- Advanced publisher/subscriber（`ze_advanced_*`）: cache, history, recovery, sample miss
- Serializer / Deserializer (`ze_serialize*`, `ze_deserialize*`)
- Closures（残り）: `z_closure_query`, `z_closure_reply`, `z_closure_hello`, `z_closure_zid`, `z_closure_matching_status`, `z_closure_link*`, `z_closure_transport*`, `ze_closure_miss`, `zc_closure_log`
- Bytes 拡張: writer, reader, slice iterator, 各種数値型 to/from
- Config: file ロード, 個別キーの set/get
- Encoding schema (`with_schema_*`)
- Slice / String / StringArray のフル CRUD
- 同期プリミティブ: `z_mutex`, `z_condvar`, `z_task`, `z_cancellation_token`, `z_clock`, `z_time`, `z_sleep`
- Init/logging: `zc_init_log_from_env_or`, `zc_log_set_callback`
- Transport listener: `z_transport_events_listener_*`, `z_link_events_listener_*`

---

## 2. 機械的に変換できる部分 / 判断が要る部分

ここを明確に切り分けることで、バージョン上げ時に「機械変換ゾーン」だけ再生成し、「判断ゾーン」は人がレビューする運用にする。

### 2-A. 機械的に変換できる（=規則化してテンプレートに落とせる）

すべての Zenoh 「Owned 型」は同じ寿命パターンに従う:
- `z_owned_X_t` を `AllocHGlobal` で確保
- `z_X_loan(owned_ptr) -> z_loaned_X_t*`
- `z_X_drop((z_moved_X_t*)owned_ptr)`
- `(z_moved_X_t*)` への cast で move semantics を表現

→ 以下は **テンプレ展開可能**:

1. **Owned ラッパークラスの骨格**
   - private `z_owned_X_t* nativePtr`, `bool disposed`
   - ctor で `AllocHGlobal`
   - `XRef Loan()` で `z_X_loan` を呼び `XRef` を返す
   - `Dispose(true/false)` で `z_X_drop` → `FreeHGlobal`
   - `internal NativePointer` 公開
   - `Move()`（オーナーシップ移譲、`Encoding` と同パターン）
2. **Ref（loaned）クラスの骨格**
   - `private readonly z_loaned_X_t* nativePtr`
   - `internal NativePointer` 公開
3. **Options クラスの骨格**
   - 対応する `z_X_options_t` 構造体 + `z_X_options_default(out)` を呼ぶ `ApplyTo(z_X_options_t*)`
4. **Encoding の MIME プリセット**（53 個）
   - すでに 1-to-1 ラップ済み、追加分も機械変換
5. **Bytes の数値型 from/to**（`u8/u16/.../bool/f32/f64/slice/string/pair/iter`）
   - `z_bytes_copy_from_*` / `z_bytes_to_*` の各 primitive を 1 メソッドずつ生やす
6. **Encoding schema バリアント**
   - 全プリセット getter に `WithSchema(string)` を生やす（`z_encoding_set_schema` を移動先で呼ぶ）
7. **Drop / Loan / LoanMut**
   - すべての owned 型に対して機械的に追加

実装方針: `Bindings/` 配下に `*.Generated.cs` を partial として生やし、機械変換できる部分はそこに置く。手書き部分は `<Name>.cs` に置き、partial で合流させる。バージョン上げ時は `*.Generated.cs` を再生成する。再生成スクリプトは別途 `Specs/codegen-plan.md` で検討（§7）。

### 2-B. 判断が要る（=手書きで残す）

| 項目 | 判断ポイント |
|---|---|
| **コールバック→C#デリゲート橋渡し** | `GCHandle`, `MonoPInvokeCallback`, `static` ディスパッチ。型ごとに微妙に違うため手書き。`Subscriber` 既存実装をベースに `Queryable`, `Get(reply)`, `LivelinessSubscriber`, `MatchingListener`, `LinkEventsListener`, `TransportEventsListener`, `Scout(hello)`, `Log` で同パターンを展開 |
| **チャンネルハンドラ (`fifo`/`ring`)** | push クロージャと相互排他。`Subscriber.WithFifo(capacity)` 的なファクトリで、ハンドラを保持しつつ `try_recv`/`recv` を Ref クラスに生やす |
| **`Session.Open`** | config の構築フロー（文字列／ファイル／ビルダ）と `z_open` のオプション組み合わせ、エラー処理 |
| **`Get` API** | reply クロージャ または ring/fifo ハンドラのどちらでも受け取れるよう 2 経路。Payload / Attachment / Encoding / Target / Consolidation 等オプションが多い |
| **Queryable** | `z_query_reply / reply_err / reply_del` の 3 経路 + selector パース + parameters |
| **Advanced pub/sub** | cache 設定、history、recovery 戦略、miss listener が交錯。プリセット風 API（`AdvancedPublisher.WithCache(...).WithSampleMiss(...)`）を検討 |
| **`Bytes` ↔ `Span<T>` / `ReadOnlyMemory<byte>` / `NativeArray<byte>`** | Unity 統合。Burst / NativeArray 連携はゼロコピー設計が必要 |
| **エラーマッピング** | 低レベル層は `ZResult` を返すで確定（§0）。`Z_CHANNEL_NODATA` 等の「期待されるエラー」を例外にしないため。`ZenohException` 化や `bool TryRecv(out ...)` 形式は高レベル層で API ごとに判断 |
| **AOT/iOS の `__Internal`** | 既に対応済みだが、コールバック追加のたびに `[MonoPInvokeCallback]` を貼る必要があり、レビュー必須 |

---

## 3. クラス設計の指針（既存パターンの整理）

既存実装で **採用** されているパターン:
- `Owned`（`IDisposable`） / `Ref`（borrow） の 2 クラスペア
- `Marshal.AllocHGlobal(sizeof(z_owned_X_t))` でハンドル領域を確保 → `z_X_drop` → `FreeHGlobal`
- finalizer は `Dispose(false)` 経由
- `internal NativePointer` で同一 namespace から低レベルアクセス可
- Options クラスは `ApplyTo(z_X_options_t*)` を持つ

**ルールとして固定したい点** (バージョン上げの差分が読みやすくなる):

1. 1 ファイル 1 主要型。`Publisher.cs` の中に `PublisherOptions`, `PublisherPutOptions` を入れる現状方針を踏襲。
2. クラス名は C 側型名の prefix を落として PascalCase: `z_owned_publisher_t` → `Publisher`、`ze_owned_advanced_publisher_t` → `AdvancedPublisher`。
3. `Ref` クラスは `<Name>Ref` を採用（`KeyExprRef`, `SampleRef` に倣う）。loan_mut が必要な型のみ `<Name>MutRef`。
4. クロージャ系は `<Target>Callback` の C# `delegate` + `<Target>Closure` の内部クラスにする。`SampleReceivedCallback` + `ClosureSample` を踏襲。
5. オプションは `<Action>Options`。put なら `PublisherPutOptions`、delete なら `PublisherDeleteOptions`、declare なら `PublisherOptions`。
6. **戻り値ポリシー（低レベル層）**: 失敗は常に `ZResult` を返す。結果オブジェクトが必要なものは `out` 引数で取る（`ZResult TryXxx(out T result)` 型）。例外は引数バリデーション（`ArgumentNullException` 等、呼び出し側の明らかなバグ）に限定。`Z_CHANNEL_NODATA` のような期待されるエラーも `ZResult` で返す。
7. ハンドラ受け渡しは GCHandle + `static` `[MonoPInvokeCallback]` を必ず使う（IL2CPP/iOS で必須）。コールバック内では throw しない（ネイティブ側に伝播できずクラッシュするため、try-catch でログ＋無視を規約化）。
8. **ctor で失敗しうる型は static factory + `ZResult` 返しを採用**。`new KeyExpr("...")` のような失敗 throw する ctor は低レベル層では作らず、`KeyExpr.TryCreate(string, out KeyExpr) -> ZResult` 形式にする。素の `new Foo()`（空ハンドル確保のみ、失敗しない）は OK。
9. 既存 `Wrappers/` の型と名前が衝突しても、namespace が異なる（`Zenoh.Bindings` vs `Zenoh`）ので問題なし。同じ概念は同じ型名にして対応関係を保つ。

---

## 4. フェーズ分けロードマップ

優先度はサンプル動作とユーザ要望ベース。各フェーズは独立に PR 化可能。

### Phase 0: 足回り整備（`Bindings/` 開設）
- [ ] `Specs/wrapper-coverage-plan.md`（本ドキュメント）を確定
- [ ] `Assets/ZenohPackage/Runtime/Bindings/` 作成、`namespace Zenoh.Bindings` 採用
- [ ] 既存 `Wrappers/` の `ZResult` を移植（型は再宣言。既存版はそのまま残す）
- [ ] `Session`, `KeyExpr`, `Bytes`, `Encoding`, `Sample`, `Publisher`, `Subscriber` を §1 の差分方針に従い低レベル化して `Bindings/` に新規実装。ctor 失敗→ static factory + `ZResult`、`throw` 排除
- [ ] `Config` クラス新設（`z_config_default` / `zc_config_from_str` / `zc_config_from_file` をラップ。`Session.Open` から切り出す）
- [ ] `Sample` を payload / encoding / kind / timestamp / source_info / attachment / express を取れる API に拡充
- [ ] 既存サンプル（`RawSamples/`, `ZenohSampleScenes/`）はそのまま動くこと（`Zenoh.Bindings` を使う新サンプルは Phase 0 末尾に 1 つだけ追加）

### Phase 1: Query 系（Get/Queryable）— **既存ユーザ需要が高い**
- [ ] `Reply` / `ReplyRef`, `ReplyErr` / `ReplyErrRef`
- [ ] `Query` / `QueryRef`（queryable に渡される側）
- [ ] `Queryable` + `QueryReceivedCallback` + `ClosureQuery`
- [ ] `Session.Get(KeyExpr, ...)` の reply クロージャ版
- [ ] `Session.Get(...)` の `FifoHandlerReply` / `RingHandlerReply` 版
- [ ] `Query.Reply / ReplyErr / ReplyDel`
- [ ] `Querier`（zenoh 1.x 新機能）

### Phase 2: Liveliness
- [ ] `LivelinessToken`
- [ ] `Session.LivelinessDeclareToken / Get / DeclareSubscriber`
- [ ] バックグラウンド購読版

### Phase 3: Matching listener
- [ ] `MatchingStatus` + `MatchingStatusCallback` + `ClosureMatchingStatus`
- [ ] `Publisher.DeclareMatchingListener / DeclareBackgroundMatchingListener`
- [ ] `Querier.DeclareMatchingListener / ...`

### Phase 4: Subscriber の Pull モデル
- [ ] `FifoHandlerSample` / `RingHandlerSample`
- [ ] `Session.DeclareSubscriber(...).WithFifo(capacity)` 的なファクトリ
- [ ] `recv / try_recv` を Ref に生やす

### Phase 5: Bytes 拡張 + Serializer
- [ ] `BytesWriter`, `BytesReader`, `BytesSliceIterator`
- [ ] 各 primitive の to/from（u8/u16/.../bool/f64/string/pair/iter）
- [ ] `Serializer` / `Deserializer`（`ze_serialize*`）

### Phase 6: Scout / Info / Hello
- [ ] `Hello` / `HelloRef`
- [ ] `Session.Scout(...)`（クロージャ版 + handler 版）
- [ ] `Session.Info`（zid/peers/routers/transports/links）

### Phase 7: Advanced pub/sub（`ze_advanced_*`）
- [ ] `AdvancedPublisher`, `AdvancedSubscriber`
- [ ] `PublicationCache`, `QueryingSubscriber`
- [ ] Sample miss listener

### Phase 8: 残りのインフラ / Transport
- [ ] `Mutex`, `Condvar`, `Task`, `CancellationToken`, `Clock`, `Time`
- [ ] Logging (`zc_log_set_callback`)
- [ ] Transport / Link events listener
- [ ] `String`, `StringArray`, `Slice` の owned 版（現状は内部で生のまま使用）

### Phase 9 以降（後回し）: SHM
- 共有メモリは要件・ABI が複雑なため当面スコープ外。Phase 8 までで「ほぼ全機能」を満たす想定。

---

## 5. テスト戦略（最小限）

- `Assets/ZenohSampleScenes/` に Phase ごとの動作確認シーンを追加（`RawSamples/SimplePubSubTestRaw.cs` 相当を Wrapper 版で）
- 同一プロセス内で publisher/subscriber、querier/queryable のループバック確認
- 寿命: `Dispose` を二重呼び出ししてクラッシュしないこと、GC 中のコールバック発火でクラッシュしないこと（GCHandle）
- iOS は AOT 起因のクラッシュが出やすいため、各 Phase の終わりに iOS 実機 1 回はビルド確認

---

## 6. バージョン上げ手順（zenoh-c の minor up のとき）

1. `csbindgen` 再実行で `ZenohNative.g.cs` を更新（既存パイプラインがある前提）
2. `git diff` で新規 owned 型 / 関数を列挙
3. 新規 owned 型 → §2-A のテンプレに従い `Bindings/<Name>.Generated.cs` を生成
4. 既存 `Bindings/` の partial に差分が出ていないか確認:
   - `_options_default` の引数増減 → コンパイルエラーで検知
   - 既存関数のシグネチャ変更 → 同上
5. 判断が必要な変更（コールバックの追加、handler の追加、選好ポリシー変更）は本ドキュメントの §2-B に追記し、レビュー
6. サンプルシーンで主要 4 経路（pub/sub/get/queryable）を実機確認

---

## 7. 自動生成スクリプトの方針（任意・将来）

別ファイル `Specs/codegen-plan.md` で詳細詰める想定。要点だけ:

- 入力: `ZenohNative.g.cs`（または zenoh-c の `zenoh.h`）
- パース: regex で `z_owned_X_t` / `z_loaned_X_t` / `z_X_drop` / `z_X_loan` の triple をマッチ
- 出力: `Bindings/Generated/<Name>.Generated.cs`（partial class、ctor + Dispose + Loan + NativePointer のみ）
- options 構造体は手で書く（フィールドごとに意味が変わるため自動化メリット薄）
- まずは Phase 1 を手書きで進め、3 型くらい書いた時点で共通骨格を抽出 → 生成器化、という順番が現実的

---

## 8. オープン課題

低レベル層（本計画）に関するもの:
- iOS の `__Internal` リンクで Advanced 機能（`ze_advanced_*`）が使えるか（zenoh-c のビルドフラグ依存）
- コールバック中の例外取り扱い規約の徹底方法（lint / コードレビュー / 静的解析）
- `Wrappers/` の最終的な扱い（廃止するか、`Zenoh.Bindings` を `Zenoh` に昇格させるか）は別計画で

高レベル層（本計画外、将来の参考）:
- `ZenohException` 階層の設計、どの API で例外化するか
- `Bytes` のゼロコピー設計: Unity の `NativeArray<byte>` / `Span<byte>` ベースで `z_bytes_from_buf` の deleter コールバックをどう実装するか
- Async / UniTask 統合: `Session.GetAsync(...)` を提供するか
- ストリーミング受信（`IAsyncEnumerable<SampleRef>` 等）

---

最終更新: 2026-05-14
