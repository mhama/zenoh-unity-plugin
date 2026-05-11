## Zenoh Plugin Project for Unity

This is experimental Zenoh Plugin for Unity.
This plugin utilizes zenoh-c with csbindgen generated binding code. So all features can be used (ideally).

Currently, use of this plugin is not so easy because there's no wrapper classes. You need to fiddle with unsafe types and the ownership idea of Rust language...

Zenoh version: 1.9.0

## Platforms

Confirmed on Unity 2022.3.x

Build target platform:
* windows x86-64
* MacOS Apple Silicon
* Android arm64

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

### ZenohSimplePubSubSample scene / ZenohSimplePubSubTest script

This sample will publish strings on "myhome/kitchen/temp" key and also subscribe the same key.
You can subscribe the key or publish into the key using different zenoh implementations.

### ZenohCameraReceiverSample scene / ZenohCameraReceiverTest script

This sample will subscribe jpeg sequence on "rpi/camera/image_jpeg" key and reflect the images on the specified renderer.

This project will publish camera images on Raspberry Pi using zenoh-cpp
https://github.com/mhama/zenoh-rpi-camera-publisher-sample

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
