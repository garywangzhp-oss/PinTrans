# PinTrans

PinTrans is a Windows-first bilingual AI input method built on Weasel, librime, and rime-ice.

```text
type Chinese pinyin
  -> candidate 1: Chinese
  -> candidate 2: English translation
  -> choose either candidate normally
```

Additional shortcuts:

- `Ctrl+Alt+E`: enable or disable translation.
- `Ctrl+Alt+Enter`: translate the currently highlighted Chinese candidate.
- `Ctrl+Alt+P`: infer and translate the raw pinyin when Rime does not generate the desired Chinese phrase.

PinTrans uses an OpenAI-compatible Chat Completions API. DeepSeek and OpenCode Go presets are included.

## Quick start for users

1. Open [Releases](https://github.com/garywangzhp-oss/PinTrans/releases/latest).
2. Download `PinTrans-win-x64.zip`.
3. Extract the ZIP.
4. Open PowerShell in the extracted directory and run:

```powershell
powershell -ExecutionPolicy Bypass -File .\installer\Quick-Install.ps1
```

The installer:

- verifies Windows x64;
- installs Weasel 0.17.4 through winget if needed;
- installs rime-ice into `%APPDATA%\Rime` if needed;
- deploys the PinTrans Lua plugin;
- installs and starts the PinTrans tray application;
- registers current-user autostart;
- triggers Weasel deployment.

Windows may ask for administrator permission when installing Weasel.

After installation:

1. Open the PinTrans tray icon.
2. Select **设置**.
3. Choose DeepSeek, OpenCode Go, or a custom OpenAI-compatible endpoint.
4. Enter your API key.
5. Click **测试连接**, then **保存**.

## Quick start from source

Requirements: Windows 10 22H2 / Windows 11 x64, Git, and PowerShell.

```powershell
git clone https://github.com/garywangzhp-oss/PinTrans.git
cd PinTrans
powershell -ExecutionPolicy Bypass -File .\installer\Quick-Install.ps1
```

The script installs the .NET 8 SDK if a local `PinTrans.exe` is not already available.

## Provider presets

- DeepSeek:
  - Endpoint: `https://api.deepseek.com/chat/completions`
  - Model: `deepseek-flash`
- OpenCode Go:
  - Endpoint: `https://opencode.ai/zen/go/v1/chat/completions`
  - Model presets: `deepseek-v4.1-flash`, `glm-5.3-flash`, `mimo-v2.6-flash`, `kimi-k3`, `longcat-2.0`
- Custom: any OpenAI-compatible Chat Completions endpoint.

OpenCode Go responses are constrained to JSON and only the `translation` field is accepted. This prevents model explanations from leaking into the candidate.

## Build

```powershell
.\installer\Build-PinTrans.ps1
```

The self-contained Windows x64 executable is written to:

```text
artifacts\publish\win-x64\PinTrans.exe
```

## Install from source build

```powershell
.\installer\Install-PinTrans.ps1
```

The installer backs up `rime_ice.custom.yaml`, inserts one idempotent managed patch block, deploys the Lua plugin, installs the tray application, and registers autostart. If a conflicting user patch already exists, installation stops rather than overwriting it.

## Uninstall

```powershell
.\installer\Uninstall-PinTrans.ps1
```

User Rime data is preserved by default. Use `-RemoveApiKeys`, `-RemoveCache`, or `-RemoveAllData` for explicit data removal.

## Privacy and security

- API keys are protected with Windows DPAPI.
- SQLite cache rows are encrypted with DPAPI.
- IPC files live in a user-private directory.
- Logs do not contain API keys or full input/output text.
- There is no telemetry endpoint or relay server.
- Translating in a password field is suppressed on a best-effort basis.
- When translation is off, no model request is sent.

## Current limitations

- Windows 10/11 x64 only.
- Requires Weasel 0.17.x and rime-ice.
- Chinese typing quality is rime-ice quality, not WeChat Input Method parity.
- First-use cloud translation usually takes 1-4 seconds.
- Raw-pinyin inference is probabilistic for ambiguous input.
- No account system, cloud sync, auto-update, or mobile frontend.

## Development

```powershell
dotnet test HanBridge.sln -c Release
dotnet build HanBridge.sln -c Release
```

Architecture and scope:

- `docs\architecture.md`
- `docs\v1-scope.md`
- `docs\comparison.md`

## Internal compatibility name

Internal .NET namespaces, configuration keys, cache paths, and the data directory still use `HanBridge`. This preserves existing settings and translation cache across the PinTrans display-name migration. Users normally do not need to interact with these identifiers.