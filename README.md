## Zenoh Plugin Project for Unity

This is experimental Zenoh Plugin for Unity.
This plugin utilizes zenoh-c with csbindgen generated binding code.

Zenoh version: 1.9.0

### Architecture: 3-layer wrapper design

This plugin provides three layers of abstraction. Users typically write code against the **HighLevel** layer.

| Layer | Namespace | Role |
|---|---|---|
| **Plugins** | `ZenohNative` (P/Invoke) | csbindgen auto-generated native bindings. Not hand-edited. |
| **Bindings** | `Zenoh.Bindings` (`ZOwned*` / `ZLoaned*` / `Z*Options`) | Memory-safe 1:1 C# projection of zenoh-c. Preserves native ownership semantics (`Owned`/`Loaned`/`Take`). Returns `ZResult`, never throws. |
| **HighLevel** | `Zenoh.HighLevel` (`Session` / `Publisher` / `Subscriber` / `Queryable` / …) | Ergonomic API for end-users. Exceptions on failure (`ZenohException`), `byte[]`/`string` payloads, POCO option classes. Hides the `Owned`/`Loaned`/`Take` machinery. |

#### Covered use cases (HighLevel)

- **Session**: Open / Close
- **Pub/Sub**: Publisher (Put / PutZeroCopy), Subscriber (callback-based)
- **Query/Reply**: Queryable (Reply / ReplyErr / ReplyDel), Session.Get (with CancellationToken support)
- **Key Expression**: Parse / TryParse
- **Config**: Default / FromJsonString / FromFile

Features not yet covered by HighLevel (e.g. Querier, Liveliness, SharedMemory) are also not yet implemented in the Bindings layer. They will be added as needed.

#### AI-assisted code generation

Each layer has a generation rules document that defines naming conventions, ownership patterns, error handling, and file layout:

- `Runtime/Bindings/00_bindings-generation-rules.md` — rules for projecting `ZenohNative.g.cs` into Bindings classes
- `Runtime/HighLevel/00_highlevel_wrapper_generation_rules.md` — rules for wrapping Bindings types into the HighLevel API

These rule documents are designed so that an AI coding assistant can generate new wrappers mechanically. Given a rule document and a target Bindings type, the assistant can produce a conformant HighLevel wrapper with the correct ownership handling, Dispose patterns, and API shape. Implementation plans (e.g. `Specs/query-reply-highlevel-plan.md`) reference these rules and serve as input for AI-driven implementation.

## Platforms

Confirmed on Unity 2022.3.x

Build target platform:
* windows x86-64
* MacOS Apple Silicon
* Android arm64
* iOS (experimental)

## Note for Android

* set Player Settings as follows.
  * enable `IL2CPP` scripting backend and select `arm64` architecture.
  * set internet access as `Required`

## How to install this package

Input this url to the Package Manager's 'Add package from Git URL...' menu.

```
https://github.com/mhama/zenoh-unity-plugin.git?path=Assets/ZenohPackage
```

## About samples

Sample scenes are located under `Assets/ZenohSampleScenes/`.

### HighLevel API samples (`HighLevelWrapperTests/`)

- **SimplePubSubHighLevelTest** — Publish/Subscribe using the HighLevel API
- **QueryableReplyHighLevelTest** — Queryable (server side) that replies to queries
- **QueryGetHighLevelTest** — Session.Get (client side) that sends queries and receives replies
- **CameraPubSubHighLevelSample** — Camera image Pub/Sub using the HighLevel API

### Bindings API samples (`BindingTests/`)

- **QueryableReplyBindingTest / QueryGetBindingTest** — Query/Reply using the Bindings layer directly

### Legacy wrapper samples (`LegacyWrapperTests/`)

- **SimplePubSubTest** — Pub/Sub using legacy wrappers (not recommended for new code)
- **CameraPublisherTest / CameraReceiverTest** — Camera image Pub/Sub using legacy wrappers

## How to make DLL / ZenohNative.g.cs for each platforms

binding code can be generated with [a branch of zenoh-c-cs](https://github.com/mhama/zenoh-c-cs/tree/20260511-csbindgen-1.9.0) project. This project is a fork of zenoh-c with capability to output csharp binding code using [csbindgen](https://github.com/Cysharp/csbindgen).

### clone repo

```sh
git clone https://github.com/mhama/zenoh-c-cs
git checkout 20260511-csbindgen-1.9.0
```

### For MacOS (Apple Silicon) (non-cross-compiling)

This is the case for building on MacOS (for MacOS).

```sh
# remove./build folder if exist
mkdir -p ./build && cd ./build
cmake -DZENOHC_BUILD_WITH_UNSTABLE_API=true -DZENOHC_BUILD_WITH_SHARED_MEMORY=true ..  -GNinja
cmake --build . --config Release
```

use target/release/libzenohc.dylib, target/ZenohNative.g.cs

### For Windows (cross-compiling on Mac OS)

prepare

```sh
brew install mingw-w64
rustup target add x86_64-pc-windows-gnu
```

build

```sh
# remove./build folder if exist
mkdir -p ./build && cd ./build
cmake -DCMAKE_TOOLCHAIN_FILE="../ci/toolchains/TC-x86_64-pc-windows-gnu.cmake" -DZENOHC_BUILD_WITH_SHARED_MEMORY=ON -DZENOHC_BUILD_WITH_UNSTABLE_API=ON ..
cmake --build . --config Release
```

use target/x86_64-pc-windows-gnu/release/zenohc.dll, target/ZenohNative.g.cs

### For Android arm64 (cross-compiling on Mac OS)

prepare
install Android SDK and NDK.

```sh
rustup target add aarch64-linux-android
```

modify ci/toolchains/TC-aarch64-linux-android.cmake
```cmake
set(CMAKE_ANDROID_NDK /PATH/TO/ANDROID/NDK) # fix path
```

build

```sh
# remove./build folder if exist
mkdir -p ./build && cd ./build
cmake -DCMAKE_TOOLCHAIN_FILE="../ci/toolchains/TC-aarch64-linux-android.cmake" -DZENOHC_BUILD_WITH_UNSTABLE_API=ON ..
cmake --build . --config Release
```

(Notice that shared memory apis are not supported on Android platform.)

use target/aarch64-linux-android/release/libzenohc.so, target/ZenohNative.g.cs

#### if there's error on building `ring` or get `failed to find tool "aarch64-linux-android-clang"`

I experienced an error here at building `ring` package. Here's the log:

```
warning: ring@0.17.13: Compiler family detection failed due to error: ToolNotFound: failed to find tool "aarch64-linux-android-clang": No such file or directory (os error 2)
warning: ring@0.17.13: Compiler family detection failed due to error: ToolNotFound: failed to find tool "aarch64-linux-android-clang": No such file or directory (os error 2)
error: failed to run custom build command for ring v0.17.13
```

Or if you get this error

```
error occurred in cc-rs: failed to find tool "aarch64-linux-android-clang": No such file or directory (os error 2)
```

Here's the workaround:

```sh
android_ndk=/YOUR-NDK-PATH/toolchains/llvm/prebuilt/darwin-x86_64/bin
export CC_aarch64_linux_android=$android_ndk/aarch64-linux-android21-clang
export AR_aarch64_linux_android=$android_ndk/llvm-ar
export CARGO_TARGET_AARCH64_LINUX_ANDROID_LINKER=$android_ndk/aarch64-linux-android21-clang
```

Then build again.

### For iOS (cross-compiling on Mac OS) (experimental)

prepare

```sh
rustup target add aarch64-apple-ios
```

build

```sh
cargo build --release --no-default-features --features=transport_tcp,transport_tls,unstable --target=aarch64-apple-ios
```

use target/aarch64-apple-ios/release/libzenohc.a, target/ZenohNative.g.cs
