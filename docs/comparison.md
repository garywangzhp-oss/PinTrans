# PinTrans compared with related open-source projects

PinTrans is a local, Windows-first prototype in this repository. It is not a fork of Weasel or of the projects below, and it keeps Weasel, librime, and rime-ice as external prerequisites.

| Capability | PinTrans | RimeTranslate | TypeAnything / Chatou | Transput | AIME |
|---|---|---|---|---|---|
| Platform | Windows x64 | Windows x64 | Windows x64 | macOS | macOS 26+ |
| Integration | Rime Lua plugin + C# tray bridge | Rime Lua filter + Go Ollama bridge | Forked Weasel TSF + librime processor | Rime/Squirrel-based macOS input method | Native librime/InputMethodKit app |
| Automatic candidate translation | Yes, candidate 2 | Yes, candidate 2 | No; translates after Enter/F8 | Yes, via shortcut | Yes, via manual AI action |
| Highlighted-candidate translation | Yes, `Ctrl+Alt+Enter` | No | No | No | AI action works on selected/last text |
| Raw-pinyin fallback | Yes, `Ctrl+Alt+P` | No | No | No | No |
| Output choice | Chinese or English candidate | Chinese or Ollama translation candidate | Original or translated text | Enter or Ctrl+Enter | Result replaces input after manual action |
| Text replacement method | Native Rime candidate commit | Native Rime candidate commit | Clipboard + backspace / SendInput | Native input method | Native candidate replacement |
| Provider model | DeepSeek, OpenCode Go, custom OpenAI-compatible | Local Ollama | OpenAI-compatible | Configurable LLM | Apple on-device or OpenAI-compatible |
| OpenCode Go support | Yes, mode-aware and JSON-constrained | No | No | No | No |
| Pinyin ambiguity policy | Prefer completed-state meaning for `yi + verb` | N/A | Prompt/style dependent | N/A | N/A |
| API key protection | Windows DPAPI | No cloud key required | Local config; upstream versions use plaintext | Project-dependent | Project-dependent |
| Translation cache | SQLite, encrypted per row | Model-level only | Varies | Varies | Varies |
| Stale-response protection | Request IDs, mode matching, persistent per-composition results | Source matching | Replacement based | Varies | Varies |
| Password-field guard | Best-effort Win32 edit-style detection | N/A | Not documented | N/A | Platform-dependent |
| Telemetry | None | None | None documented | None documented | None documented |
| Update/account/sync | None in V1 | None | None | Installer only | Preview project |

## Key product differences

1. PinTrans is a companion plugin, not a replacement input engine. It depends on Weasel and rime-ice and does not patch Weasel binaries.
2. PinTrans supports three translation paths: automatic top candidate, explicitly highlighted Chinese candidate, and raw-pinyin inference when Rime has no suitable Chinese candidate.
3. PinTrans never replaces committed text with clipboard or simulated backspaces. English is committed through the normal Rime candidate path.
4. OpenCode Go responses are constrained to a JSON translation field. This prevents model reasoning or explanations from leaking into the candidate.
5. Translation results are persistent for the current composition and are protected by request and mode matching.
6. PinTrans has explicit failure handling: timeout, one retry, rate limit, consecutive-failure pause, no cross-provider fallback, and no telemetry.

## Shared foundations and limitations

- Weasel provides Windows TSF input.
- librime and rime-ice provide Chinese candidate generation.
- Weasel 0.17.x and a pinned rime-ice revision are required.
- PinTrans V1 is Windows x64 only.
- Typing quality is rime-ice quality, not WeChat Input Method parity.
- Cloud translation latency depends on the provider; first-use translation can take 1-4 seconds.
- Raw-pinyin inference is probabilistic and can choose the wrong interpretation for ambiguous input.
- PinTrans is currently a private local repository and has no public release, account system, cloud sync, auto-update, or mobile frontend.