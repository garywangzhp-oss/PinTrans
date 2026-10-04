# HanBridge V1 frozen scope

## Product

- Windows 10 22H2 and Windows 11 x64 only.
- Personal use plus 3-5 internal testers.
- Built on Weasel, librime, and rime-ice.
- Typing quality target is the installed rime-ice behavior, not WeChat Input Method parity.
- No accounts, cloud sync, auto-update, telemetry, or cross-platform support.

## Input behavior

- Chinese remains candidate 1.
- Translation appears as candidate 2 with an `EN` label and `译首选` comment.
- The current top Chinese candidate is translated automatically.
- `Ctrl+Alt+Enter` translates the currently highlighted Chinese candidate.
- `Ctrl+Alt+P` translates the raw pinyin with model inference when Rime does not generate the desired Chinese phrase.
- Selecting the English candidate commits it directly using the current Rime composition span.
- English candidates must not enter the Rime user dictionary or influence Chinese frequency learning.
- Translation starts after a 250 ms pause, only when the composition contains at least two Han characters.
- Maximum source length is 200 Han characters.
- `Ctrl+Alt+E` fully toggles translation mode. Translation is enabled by default after a provider is configured.

## Never translate

- Text without Han characters.
- A single Han character without context.
- Pure numbers, punctuation, or emoji.
- Email addresses, URLs, or file paths.
- Obvious source code, shell commands, or secret-like strings.
- Password or secure fields when detected.
- Any text while translation mode is off.

## Providers

Only OpenAI-compatible Chat Completions is supported in V1.

- DeepSeek official:
  - Endpoint: `https://api.deepseek.com/chat/completions`
  - Model: `deepseek-flash`
- OpenCode Go:
  - Endpoint: `https://opencode.ai/zen/go/v1/chat/completions`
  - Model: `deepseek-v4.1-flash`
- Custom OpenAI-compatible endpoint and model.

Requests are non-streaming, have an 8 second timeout, and retry transient errors once. There is no cross-provider fallback. The system proxy is used by default and can be overridden.

## Safety and data

- API keys are stored with Windows DPAPI or Credential Manager.
- SQLite cache rows are encrypted with DPAPI.
- File IPC lives under `%LOCALAPPDATA%\HanBridge\ipc`.
- Local rotating logs contain no API keys, full input, or full model output.
- Zero telemetry.
- Best-effort password field detection; secure fields are not guaranteed to be identifiable.
- Default uninstall preserves user data and prompts separately for API key and cache deletion.

## Implementation

- C# / .NET 8.
- Always-running WinForms tray application.
- HKCU Run autostart.
- PowerShell installer and uninstaller.
- Private Git repository, MIT for HanBridge-owned code.
- Weasel 0.17.x and a pinned rime-ice revision.
- Lua filter plus F24 refresh. File IPC uses atomic temporary-file rename.

## Test matrix

- WeChat desktop.
- Microsoft Word.
- Chrome and Edge.
- VS Code.
- Feishu.
- Notepad.
- WPS Writer, WPS Spreadsheet cell editing, WPS Presentation text boxes.
- Foxmail, Outlook classic, and new Outlook.

## Acceptance criteria

- p99 added latency for normal Chinese typing is under 5 ms.
- 90 percent of translation candidates appear within 1.5 seconds.
- Human acceptance rate is at least 85 percent on a 100-sentence fixture set.
- Network failures never block Chinese input.
- Eight-hour run has no crashes and no sustained memory growth.
- Translation-off mode performs zero network requests.