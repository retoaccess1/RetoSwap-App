{
  description = "Autonomous Android dev environment — C# MAUI, Java, Kotlin + headless emulator + Appium";

  inputs = {
    nixpkgs.url = "github:NixOS/nixpkgs/nixos-unstable";
    flake-utils.url = "github:numtide/flake-utils";
    android-nixpkgs = {
      url = "github:tadfisher/android-nixpkgs";
      inputs.nixpkgs.follows = "nixpkgs";
    };
  };

  outputs =
    {
      nixpkgs,
      flake-utils,
      android-nixpkgs,
      ...
    }:
    flake-utils.lib.eachDefaultSystem (
      system:
      let
        pkgs = import nixpkgs {
          inherit system;
          config = {
            android_sdk.accept_license = true;
            allowUnfree = true;
          };
        };

        # Android SDK — API 35 (Android 15), x86_64 for KVM acceleration
        # google_apis image required for Google Maps / GMS APIs.
        # Fallback if unavailable: system-images-android-35-default-x86-64
        androidSdk = android-nixpkgs.sdk.${system} (
          sdkPkgs: with sdkPkgs; [
            cmdline-tools-latest
            build-tools-35-0-0
            platform-tools
            platforms-android-35
            emulator
            system-images-android-35-google-apis-x86-64
          ]
        );

        # FHS environment — wraps the Android SDK so Gradle and the emulator
        # can find shared libraries at their expected FHS paths.
        # Entry point: `android-env -c '<cmd>'` or just `android-env` for a shell.
        # DO NOT use androidEnv.env (exec bwrap breaks --command).
        androidEnv = pkgs.buildFHSEnv {
          name = "android-env";
          targetPkgs =
            _:
            (with pkgs; [
              androidSdk
              jdk17
              gradle
              git
              which
              file
              curl
              # Emulator display / audio stubs (headless)
              libGL
              libpulseaudio
              alsa-lib
              # X11 stubs for emulator (even in headless mode)
              libx11
              libxext
              libxi
              libxrender
              libxtst
              zlib
            ]);
          multiPkgs = _: (with pkgs; [ zlib ]);

          profile = ''
            export ANDROID_HOME="${androidSdk}/share/android-sdk"
            export ANDROID_SDK_ROOT="$ANDROID_HOME"
            export JAVA_HOME="${pkgs.jdk17}"
            export PATH="$ANDROID_HOME/cmdline-tools/latest/bin:$ANDROID_HOME/platform-tools:$ANDROID_HOME/emulator:$JAVA_HOME/bin:$PATH"
            export ANDROID_EMULATOR_USE_SYSTEM_LIBS=1
            export ANDROID_AVD_HOME="''${ANDROID_AVD_HOME:-''${HOME}/.android/avd}"
          '';
          runScript = "bash";
        };

      in
      {
        # Primary devshell — all three toolchains in one shell.
        #
        # IMPORTANT: dotnet MAUI Android workloads cannot be installed into
        # the read-only Nix store. Run scripts/setup-dotnet-maui.sh once after
        # first `nix develop` to create a mutable copy at $HOME/.dotnet-maui.
        # The shellHook detects the mutable copy and overrides DOTNET_ROOT.
        #
        # Autonomous command pattern:
        #   dotnet build -f net9.0-android   ← runs in mkShell (mutable DOTNET_ROOT)
        #   android-env -c 'adb install ...' ← runs inside FHS
        devShells.default = pkgs.mkShell {
          buildInputs = with pkgs; [
            androidEnv
            # Java / Kotlin / Gradle
            jdk17
            kotlin
            gradle
            # C# / .NET (base SDK — workloads installed by setup-dotnet-maui.sh)
            dotnet-sdk_9
            # Node.js + Appium (installed on first nix develop)
            nodejs_22
            # Utilities
            android-tools # standalone adb/fastboot outside FHS
            git
            jq
            # NixOS: patchelf + upx for MAUI bundled native binaries.
            # patchelf rewrites ELF interpreters; upx decompresses UPX-packed
            # binaries (e.g. binutils/bin/as) before patchelf can process them.
            patchelf
            upx
          ];

          # Exposed so setup-dotnet-maui.sh can read the current SDK store path.
          DOTNET_SDK_NIX_ROOT = "${pkgs.dotnet-sdk_9}/share/dotnet";
          # Real glibc interpreter path for patchelf of bundled MAUI native binaries.
          # On NixOS, /lib64/ld-linux-x86-64.so.2 is a stub — MAUI's llc/ld binaries
          # must be patched to use the actual interpreter.
          GLIBC_INTERP = "${pkgs.glibc}/lib/ld-linux-x86-64.so.2";

          shellHook = ''
            # ── Android SDK + JDK paths ───────────────────────────────────────
            # These are also set inside android-env's FHS profile, but dotnet
            # build runs in the outer mkShell and needs them directly.
            export ANDROID_HOME="${androidSdk}/share/android-sdk"
            export ANDROID_SDK_ROOT="$ANDROID_HOME"
            export JAVA_HOME="${pkgs.jdk17}"
            export PATH="$ANDROID_HOME/platform-tools:$JAVA_HOME/bin:$PATH"

            # ── MAUI escape hatch ────────────────────────────────────────────
            # Detect mutable dotnet copy with installed MAUI workloads.
            # The .nix-sdk-source file contains the Nix store path that was
            # used to create the mutable copy. If it matches the current SDK,
            # override DOTNET_ROOT to use the mutable copy (which has MAUI).
            _nix_sdk="${pkgs.dotnet-sdk_9}/share/dotnet"
            if [ -f "$HOME/.dotnet-maui/.nix-sdk-source" ]; then
              _stored=$(cat "$HOME/.dotnet-maui/.nix-sdk-source")
              if [ "$_stored" = "$_nix_sdk" ]; then
                export DOTNET_ROOT="$HOME/.dotnet-maui"
                export PATH="$DOTNET_ROOT:$PATH"
              else
                echo "WARNING: dotnet-sdk_9 version changed in flake."
                echo "  Stored SDK: $_stored"
                echo "  Current:    $_nix_sdk"
                echo "  Run: scripts/setup-dotnet-maui.sh  to rebuild the mutable copy."
              fi
            else
              echo "NOTE: MAUI Android workloads not installed."
              echo "  Run: scripts/setup-dotnet-maui.sh"
            fi

            # ── Appium install guard ─────────────────────────────────────────
            # Install Appium 2.x into $PWD/.appium on first nix develop.
            # Idempotent — skipped when already installed.
            # npm install --prefix uses node_modules/ (not lib/node_modules/).
            if [ ! -f "$PWD/.appium/node_modules/appium/package.json" ]; then
              echo "Installing Appium 2.x (one-time setup)..."
              npm install --prefix "$PWD/.appium" appium@latest 2>&1
              "$PWD/.appium/node_modules/.bin/appium" driver install uiautomator2 2>&1
              echo "Appium installed."
            fi

            # Add Appium to PATH
            export PATH="$PWD/.appium/node_modules/.bin:$PATH"

            echo ""
            echo "Android autonomous dev environment ready."
            echo "  Android SDK : ${androidSdk}/share/android-sdk (via android-env)"
            echo "  Java        : $(java -version 2>&1 | head -1)"
            echo "  Kotlin      : $(kotlinc -version 2>&1)"
            echo "  dotnet      : $(dotnet --version 2>/dev/null || echo 'not ready — run setup-dotnet-maui.sh')"
            echo "  Node        : $(node --version)"
            echo ""
            echo "Quick start:"
            echo "  scripts/setup-dotnet-maui.sh           # once — installs MAUI workloads"
            echo "  scripts/create-avd.sh                  # once — creates dev35 AVD"
            echo "  scripts/start-emulator.sh              # start headless emulator"
            echo "  dotnet build App.csproj -f net9.0-android -p:TargetFrameworks=net9.0-android -p:Aapt2ToolPath=\"\$ANDROID_HOME/build-tools/35.0.0\" -p:AndroidSdkDirectory=\"\$ANDROID_HOME\" -p:JavaSdkDirectory=\"\$JAVA_HOME\"  # build MAUI app"
            echo "  android-env -c 'adb install ...'       # install APK"
            echo "  scripts/run-tests.sh                   # run Appium tests"
            echo "  scripts/stop-emulator.sh               # stop emulator"
          '';
        };
      }
    );
}
