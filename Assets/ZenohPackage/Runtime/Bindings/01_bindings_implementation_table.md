# Bindings Implementation Table

`ZenohNative.g.cs` のネイティブ型に対する Bindings 層の実装状況。
SHM (Shared Memory) は対象外。

凡例: ✅ 実装済み / ❌ 未実装 / — 該当なし

## Owned / Loaned ラッパー

### セッション・基盤

| ネイティブ型 | Bindings クラス | ファイル | 状態 |
|---|---|---|---|
| `z_owned_session_t` / `z_loaned_session_t` | `ZOwnedSession` / `ZLoanedSession` | Session.cs | ✅ |
| `z_owned_config_t` / `z_loaned_config_t` | `ZOwnedConfig` / `ZLoanedConfig` | Config.cs | ✅ |

### データ型

| ネイティブ型 | Bindings クラス | ファイル | 状態 |
|---|---|---|---|
| `z_owned_bytes_t` / `z_loaned_bytes_t` | `ZOwnedBytes` / `ZLoanedBytes` | Bytes.cs | ✅ |
| `z_owned_encoding_t` / `z_loaned_encoding_t` | `ZOwnedEncoding` / `ZLoanedEncoding` | Encoding.cs | ✅ |
| `z_owned_keyexpr_t` / `z_loaned_keyexpr_t` | `ZOwnedKeyExpr` / `ZLoanedKeyExpr` | KeyExpr.cs | ✅ |
| `z_owned_sample_t` / `z_loaned_sample_t` | `ZOwnedSample` / `ZLoanedSample` | Sample.cs | ✅ |
| `z_owned_bytes_writer_t` / `z_loaned_bytes_writer_t` | — | — | ❌ |
| `z_owned_string_t` / `z_loaned_string_t` | — | — | ❌ |
| `z_owned_slice_t` / `z_loaned_slice_t` | — | — | ❌ |
| `z_owned_string_array_t` / `z_loaned_string_array_t` | — | — | ❌ |

### Pub/Sub

| ネイティブ型 | Bindings クラス | ファイル | 状態 |
|---|---|---|---|
| `z_owned_publisher_t` / `z_loaned_publisher_t` | `ZOwnedPublisher` / `ZLoanedPublisher` | Publisher.cs | ✅ |
| `z_owned_subscriber_t` / `z_loaned_subscriber_t` | `ZOwnedSubscriber` / `ZLoanedSubscriber` | Subscriber.cs | ✅ |

### Query / Reply

| ネイティブ型 | Bindings クラス | ファイル | 状態 |
|---|---|---|---|
| `z_owned_query_t` / `z_loaned_query_t` | `ZOwnedQuery` / `ZLoanedQuery` | Query.cs | ✅ |
| `z_owned_reply_t` / `z_loaned_reply_t` | `ZOwnedReply` / `ZLoanedReply` | Reply.cs | ✅ |
| `z_owned_reply_err_t` / `z_loaned_reply_err_t` | `ZOwnedReplyErr` / `ZLoanedReplyErr` | Reply.cs | ✅ |
| `z_owned_queryable_t` / `z_loaned_queryable_t` | `ZOwnedQueryable` / `ZLoanedQueryable` | Queryable.cs | ✅ |
| `z_owned_querier_t` / `z_loaned_querier_t` | — | — | ❌ |

### Closure (コールバック)

| ネイティブ型 | Bindings クラス | ファイル | 状態 |
|---|---|---|---|
| `z_owned_closure_sample_t` | `ZOwnedClosureSample` / `ZLoanedClosureSample` | Subscriber.cs | ✅ |
| `z_owned_closure_query_t` | `ZOwnedClosureQuery` / `ZLoanedClosureQuery` | Queryable.cs | ✅ |
| `z_owned_closure_reply_t` | `ZOwnedClosureReply` / `ZLoanedClosureReply` | Get.cs | ✅ |
| `z_owned_closure_zid_t` | — | — | ❌ |
| `z_owned_closure_hello_t` | — | — | ❌ |
| `z_owned_closure_matching_status_t` | — | — | ❌ |
| `z_owned_closure_transport_event_t` | — | — | ❌ |
| `z_owned_closure_link_event_t` | — | — | ❌ |
| `z_owned_closure_link_t` | — | — | ❌ |
| `z_owned_closure_transport_t` | — | — | ❌ |

### Handler (Pull モデル)

| ネイティブ型 | Bindings クラス | ファイル | 状態 |
|---|---|---|---|
| `z_owned_fifo_handler_sample_t` | — | — | ❌ |
| `z_owned_ring_handler_sample_t` | — | — | ❌ |
| `z_owned_fifo_handler_query_t` | — | — | ❌ |
| `z_owned_ring_handler_query_t` | — | — | ❌ |
| `z_owned_fifo_handler_reply_t` | — | — | ❌ |
| `z_owned_ring_handler_reply_t` | — | — | ❌ |

### 同期・キャンセル

| ネイティブ型 | Bindings クラス | ファイル | 状態 |
|---|---|---|---|
| `z_owned_cancellation_token_t` / `z_loaned_cancellation_token_t` | `ZOwnedCancellationToken` / `ZLoanedCancellationToken` | CancellationToken.cs | ✅ |
| `z_owned_mutex_t` / `z_loaned_mutex_t` | — | — | ❌ |
| `z_owned_condvar_t` / `z_loaned_condvar_t` | — | — | ❌ |

### Liveliness

| ネイティブ型 | Bindings クラス | ファイル | 状態 |
|---|---|---|---|
| `z_owned_liveliness_token_t` | — | — | ❌ |

### Matching Listener

| ネイティブ型 | Bindings クラス | ファイル | 状態 |
|---|---|---|---|
| `z_owned_matching_listener_t` | — | — | ❌ |

### ネットワーク・ディスカバリ

| ネイティブ型 | Bindings クラス | ファイル | 状態 |
|---|---|---|---|
| `z_owned_hello_t` / `z_loaned_hello_t` | — | — | ❌ |
| `z_owned_transport_t` / `z_loaned_transport_t` | — | — | ❌ |
| `z_owned_link_t` / `z_loaned_link_t` | — | — | ❌ |
| `z_owned_transport_events_listener_t` | — | — | ❌ |
| `z_owned_link_events_listener_t` | — | — | ❌ |

### Advanced (ze_ prefix)

| ネイティブ型 | Bindings クラス | ファイル | 状態 |
|---|---|---|---|
| `ze_owned_advanced_publisher_t` | — | — | ❌ |
| `ze_owned_advanced_subscriber_t` | — | — | ❌ |
| `ze_owned_publication_cache_t` | — | — | ❌ |
| `ze_owned_querying_subscriber_t` | — | — | ❌ |
| `ze_owned_serializer_t` | — | — | ❌ |
| `ze_owned_sample_miss_listener_t` | — | — | ❌ |

---

## Options 構造体

| ネイティブ型 | Bindings クラス | ファイル | 状態 |
|---|---|---|---|
| `z_open_options_t` | `ZOpenOptions` | Session.cs | ✅ |
| `z_close_options_t` | `ZCloseOptions` | Session.cs | ✅ |
| `z_publisher_options_t` | `ZPublisherOptions` | Publisher.cs | ✅ |
| `z_publisher_put_options_t` | `ZPublisherPutOptions` | Publisher.cs | ✅ |
| `z_publisher_delete_options_t` | — | — | ❌ |
| `z_subscriber_options_t` | `ZSubscriberOptions` | Subscriber.cs | ✅ |
| `z_get_options_t` | `ZGetOptions` | Get.cs | ✅ |
| `z_queryable_options_t` | `ZQueryableOptions` | Queryable.cs | ✅ |
| `z_query_reply_options_t` | `ZQueryReplyOptions` | Query.cs | ✅ |
| `z_query_reply_err_options_t` | `ZQueryReplyErrOptions` | Query.cs | ✅ |
| `z_query_reply_del_options_t` | `ZQueryReplyDelOptions` | Query.cs | ✅ |
| `z_put_options_t` | — | — | ❌ |
| `z_delete_options_t` | — | — | ❌ |
| `z_querier_options_t` | — | — | ❌ |
| `z_querier_get_options_t` | — | — | ❌ |
| `z_scout_options_t` | — | — | ❌ |
| `z_liveliness_token_options_t` | — | — | ❌ |
| `z_liveliness_subscriber_options_t` | — | — | ❌ |
| `z_liveliness_get_options_t` | — | — | ❌ |
| `ze_advanced_publisher_options_t` | — | — | ❌ |
| `ze_advanced_subscriber_options_t` | — | — | ❌ |
| `ze_publication_cache_options_t` | — | — | ❌ |
| `ze_querying_subscriber_options_t` | — | — | ❌ |

---

## メタデータ・値型ラッパー

| ネイティブ型 | Bindings クラス | ファイル | 状態 |
|---|---|---|---|
| `z_timestamp_t` | `ZLoanedTimestamp` | Sample.cs | ✅ |
| `z_source_info_t` | `ZLoanedSourceInfo` | Sample.cs | ✅ |
| `z_matching_status_t` | `ZMatchingStatus` | Publisher.cs | ✅ |
| `z_query_consolidation_t` | `ZQueryConsolidation` | Get.cs | ✅ |
| `z_entity_global_id_t` | — (internal 経由で使用) | — | ❌ |

---

## 再宣言 Enum

| ネイティブ enum | Bindings enum | ファイル | 状態 |
|---|---|---|---|
| `z_sample_kind_t` | `ZSampleKind` | Sample.cs | ✅ |
| `z_consolidation_mode_t` | `ZConsolidationMode` | Get.cs | ✅ |
| `z_query_target_t` | `ZQueryTarget` | Get.cs | ✅ |
| `z_reply_keyexpr_t` | `ZReplyKeyExpr` | Query.cs | ✅ |
| `z_congestion_control_t` | `ZCongestionControl` | Query.cs | ✅ |
| `z_priority_t` | `ZPriority` | Query.cs | ✅ |
| `z_locality_t` | `ZLocality` | Query.cs | ✅ |
| `z_whatami_t` | — | — | ❌ |
| `z_reliability_t` | — | — | ❌ |
| `z_keyexpr_intersection_level_t` | — | — | ❌ |
| `zc_log_severity_t` | — | — | ❌ |

---

## Handwritten 拡張

| 対象クラス | 拡張 | ファイル | 状態 |
|---|---|---|---|
| `ZOwnedBytes` (partial) | `TryCreateFromManagedBuf` | Handwritten/Bytes.Pinned.cs | ✅ |

---

## ロギング (zc_ prefix)

| ネイティブ型 | Bindings クラス | ファイル | 状態 |
|---|---|---|---|
| `zc_owned_closure_log_t` | — | — | ❌ |
| `zc_owned_concurrent_close_handle_t` | — | — | ❌ |
