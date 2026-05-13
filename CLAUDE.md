# Android Autonomous Dev Environment

This template provides a complete autonomous Android development environment.
Claude Code can build, deploy, test, and patch Android apps without human
intervention.

## Supported toolchains

| Language | Build tool | Target |
|----------|-----------|--------|
| C# MAUI/Blazor | `dotnet build -f net9.0-android` | Android 15 (API 35) |
| Java | `./gradlew assembleDebug` | Android 15 (API 35) |
| Kotlin | `./gradlew assembleDebug` | Android 15 (API 35) |

## Project structure

```
.
├── flake.nix                   # Nix devshell (dotnet 9 + JDK 17 + Android SDK API 35)
├── scripts/
│   ├── setup-dotnet-maui.sh   # One-time MAUI workload install (run once)
│   ├── create-avd.sh          # Create dev35 AVD (run once)
│   ├── start-emulator.sh      # Boot headless emulator + wait for boot
│   ├── stop-emulator.sh       # Stop running emulator
│   └── run-tests.sh           # Start Appium + run tests + stop Appium
├── tests/appium/
│   ├── SmokeTest.cs           # App-launch smoke test (extend with task-specific tests)
│   ├── AppiumSmokeTest.csproj
│   └── appium-config.json
└── CLAUDE.md                  # This file
```

## Environment setup (one-time)

```bash
# 1. Enter the devshell
nix develop

# 2. Install MAUI Android workloads (C# only — creates $HOME/.dotnet-maui)
scripts/setup-dotnet-maui.sh

# 3. Create the AVD (inside the FHS shell)
android-env -c 'scripts/create-avd.sh'
```

After these three steps, every subsequent `nix develop` is ready to use.

## Autonomous workflow

### Build

```bash
# C# MAUI — must pass MSBuild properties to locate Android SDK and AAPT2
dotnet build Manta.csproj -f net9.0-android -c Debug \
  -p:TargetFrameworks=net9.0-android \
  -p:Aapt2ToolPath="$ANDROID_HOME/build-tools/35.0.0" \
  -p:AndroidSdkDirectory="$ANDROID_HOME" \
  -p:JavaSdkDirectory="$JAVA_HOME"

# Java or Kotlin (Gradle)
android-env -c './gradlew assembleDebug'
```

### Deploy

```bash
# Install APK onto the running emulator
android-env -c 'adb install -r <path-to-apk>'
```

### Emulator lifecycle

```bash
# Start (headless, waits for sys.boot_completed=1, timeout 300s)
android-env -c 'scripts/start-emulator.sh'

# Stop
android-env -c 'scripts/stop-emulator.sh'

# Check if running
android-env -c 'adb devices'
```

### Run tests

```bash
# Appium smoke test (app launches, does not crash)
APP_PACKAGE=com.retoswap APP_ACTIVITY=crc6462e1985d7b8dbfee.MainActivity scripts/run-tests.sh
```

### Full autonomous cycle

```bash
# 1. Build (EmbedAssembliesIntoApk=true required — see "No assemblies found" gotcha)
dotnet build Manta.csproj -f net9.0-android -c Debug \
  -p:TargetFrameworks=net9.0-android \
  -p:EmbedAssembliesIntoApk=true \
  -p:Aapt2ToolPath="$ANDROID_HOME/build-tools/35.0.0" \
  -p:AndroidSdkDirectory="$ANDROID_HOME" \
  -p:JavaSdkDirectory="$JAVA_HOME"

# 2. Start emulator
android-env -c 'scripts/start-emulator.sh'

# 3. Install
android-env -c 'adb install -r bin/Debug/net9.0-android/com.retoswap-Signed.apk'

# 4. Run tests
APP_PACKAGE=com.retoswap APP_ACTIVITY=crc6462e1985d7b8dbfee.MainActivity scripts/run-tests.sh

# 5. Stop emulator
android-env -c 'scripts/stop-emulator.sh'
```

## KVM

KVM acceleration is required for practical emulator performance.

- **Check access:** `ls -la /dev/kvm`
- **NixOS prerequisite:** `virtualisation.libvirtd.enable = true` or equivalent
- **User group:** user must be in the `kvm` group
- Without KVM: emulator falls back to software rendering — very slow

## MAUI-specific notes

### DOTNET_ROOT override

The shellHook detects `$HOME/.dotnet-maui` and overrides `DOTNET_ROOT` automatically.
If DOTNET_ROOT is not overridden, MAUI workloads are not available.

Check:
```bash
echo $DOTNET_ROOT          # should be $HOME/.dotnet-maui
dotnet workload list       # should show maui-android
```

Rebuild if needed (e.g. after flake update):
```bash
scripts/setup-dotnet-maui.sh
```

### APK location (MAUI)

```
bin/Debug/net9.0-android/com.retoswap-Signed.apk
bin/Release/net9.0-android/com.retoswap-Signed.apk
```

## Appium UIAutomator2 session config

```csharp
options.PlatformName = "Android";
options.AutomationName = "UIAutomator2";
options.AddAdditionalAppiumOption("appPackage", "<package>");
options.AddAdditionalAppiumOption("appActivity", "<activity>");
```

Appium 2.x server: base-path is `/` (not `/wd/hub`).
Appium log: `/tmp/appium.log`

## Claude Code permissions required

The `.claude/settings.json` in this template grants permissions for:

- `dotnet` — build and test C# projects
- `gradlew` — build Java/Kotlin projects
- `adb` — deploy and interact with emulator
- `android-env` — enter FHS environment for SDK tools
- `emulator` — managed via `android-env -c '...'`
- `avdmanager` — managed via `android-env -c '...'`
- `appium` — managed via `scripts/run-tests.sh`
- `scripts/` — all helper scripts

## Common issues

**Emulator boot timeout (300s)**
- Cold swiftshader_indirect can take 3-5 min. Increase `BOOT_TIMEOUT`:
  `BOOT_TIMEOUT=600 android-env -c 'scripts/start-emulator.sh'`

**DOTNET_ROOT not overridden after setup**
- Exit and re-enter `nix develop` — the shellHook sets it on entry.

**Appium can't find device**
- Verify emulator is running: `android-env -c 'adb devices'`
- Verify APK is installed: `android-env -c 'adb shell pm list packages | grep <package>'`

**`google_apis` system image unavailable**
- Edit `flake.nix`: replace `system-images-android-35-google-apis-x86-64`
  with `system-images-android-35-default-x86-64`
- Edit `scripts/create-avd.sh`: replace `google_apis` with `default` in `SYSTEM_IMAGE`

**XA3006: Could not compile native assembly — llc/mono-aot-cross fails on NixOS**
- Root cause: MAUI bundles pre-compiled ELF binaries (`llc`, `ld`, `mono-aot-cross`, …)
  with `/lib64/ld-linux-x86-64.so.2` hard-coded as the ELF interpreter. On NixOS
  that path is a stub. Additionally, several binutils wrapper scripts use `#!/bin/bash`
  which also does not exist on NixOS.
- Fix: `setup-dotnet-maui.sh` patches both issues automatically:
  1. `patchelf --set-interpreter $GLIBC_INTERP` on all ELF executables in packs
  2. Rewrites `#!/bin/bash`/`#!/bin/sh` shebangs to the real bash path
  If you skipped the script or it predates this fix, re-run `scripts/setup-dotnet-maui.sh`.
- If `nix-ld` is enabled in your NixOS config, the ELF interpreter issue does not occur
  (but the shebang fix is still needed).

**XamlC error: Failed to resolve assembly — ZXing.Net.Maui.Controls**
- Root cause: The `ZXing.Net.MAUI` NuGet package uses all-caps `MAUI` in its DLL
  filename (`ZXing.Net.MAUI.Controls.dll`) but XAML namespaces reference it as
  `ZXing.Net.Maui.Controls` (mixed case). Linux file systems are case-sensitive.
- Fix (one-time, per version): create a symlink in the NuGet cache:
  ```bash
  VER=0.6.0
  DIR="$HOME/.nuget/packages/zxing.net.maui.controls/$VER/lib/net9.0-android35.0"
  ln -sf ZXing.Net.MAUI.Controls.dll "$DIR/ZXing.Net.Maui.Controls.dll"
  ```
  Adjust `VER` to match the version pinned in your `.csproj`.

**`-f net9.0-android` flag alone does not restrict restore**
- Use `-p:TargetFrameworks=net9.0-android` at the MSBuild property level to prevent
  restore from attempting iOS/Windows target frameworks (which lack Linux SDKs):
  ```bash
  dotnet build App.csproj -f net9.0-android \
    -p:TargetFrameworks=net9.0-android \
    -p:Aapt2ToolPath="$ANDROID_HOME/build-tools/35.0.0" \
    -p:AndroidSdkDirectory="$ANDROID_HOME" \
    -p:JavaSdkDirectory="$JAVA_HOME"
  ```

**MAUI activity class is CRC-based — `.MainActivity` will fail**
- MAUI generates a native wrapper activity class with a CRC-based namespace such as
  `crc6462e1985d7b8dbfee.MainActivity` rather than the package-relative `.MainActivity`.
- Discover the correct activity name from the APK before running tests:
  ```bash
  aapt dump badging <path-to-apk> | grep launchable-activity
  # Example: launchable-activity: name='crc6462e1985d7b8dbfee.MainActivity'
  ```
- Pass the full class name as `APP_ACTIVITY`:
  ```bash
  APP_PACKAGE=com.example.myapp APP_ACTIVITY=crc6462e1985d7b8dbfee.MainActivity \
    scripts/run-tests.sh
  ```

**MAUI Debug APK crash: "No assemblies found" (Fast Deployment mode)**
- Root cause: MAUI Debug builds default to Fast Deployment where .NET assemblies
  are NOT bundled inside the APK. The monodroid runtime looks for them in a
  side-loaded directory that doesn't exist when you install with bare `adb install`.
- Symptom: app crashes immediately after launch with logcat:
  ```
  F monodroid: No assemblies found in '.../files/.__override__/x86_64'
  F monodroid: ALL entries in APK named `lib/x86_64/` MUST be STORED.
  ```
- Fix: rebuild with `EmbedAssembliesIntoApk=true` (keep Debug config — Release
  triggers AOT compilation which fails on NixOS without additional patching):
  ```bash
  dotnet build App.csproj -c Debug -f net9.0-android \
    -p:TargetFrameworks=net9.0-android \
    -p:EmbedAssembliesIntoApk=true \
    -p:Aapt2ToolPath="$ANDROID_HOME/build-tools/35.0.0" \
    -p:AndroidSdkDirectory="$ANDROID_HOME" \
    -p:JavaSdkDirectory="$JAVA_HOME"
  ```
  This embeds assemblies as `lib/x86_64/lib_*.dll.so` files in the APK so
  the monodroid runtime can find them without a side-loaded directory.
