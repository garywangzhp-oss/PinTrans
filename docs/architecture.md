# Architecture

```text
Rime / Weasel candidate pipeline
  │
  ├─ hanbridge_filter.lua
  │    ├─ writes request.json atomically
  │    ├─ reads response.txt when a translation is ready
  │    └─ yields:
  │         1. original Chinese candidate
  │         2. English candidate
  │
  └─ hanbridge_refresh.lua
       └─ handles F24 and refreshes the composition

HanBridge.exe
  ├─ polls %LOCALAPPDATA%\HanBridge\ipc\request.json
  ├─ debounces for 250 ms and cancels superseded requests
  ├─ validates language and safety guards
  ├─ checks encrypted SQLite cache
  ├─ calls the active OpenAI-compatible provider
  ├─ writes response.txt atomically
  └─ sends F24 to refresh Rime candidates
```

## Components

- `HanBridge.Core` contains settings, DPAPI secret storage, model providers, cache, language guards, and file IPC.
- `HanBridge.App` is the persistent WinForms tray application and settings UI.
- `rime/lua` contains the Rime filter and F24 processor.
- `installer` contains build, install, and uninstall scripts.

## Privacy boundaries

- API keys are protected with Windows DPAPI in `secrets.dat`.
- Cache rows are encrypted with DPAPI.
- Request and response files exist only in the user-private IPC directory and are deleted by the Lua filter.
- Logs contain provider/model/status but not source text, translation text, or API keys.
- There is no telemetry endpoint or relay server.