# Decision record

Date: 2026-10-04

1. V1 is a private prototype for the owner and 3-5 testers.
2. Build on Weasel, librime, and rime-ice; do not build a new engine.
3. Use candidate 1 for Chinese and candidate 2 for the English translation.
4. Translate the top candidate after a 250 ms pause and a minimum of two Han characters.
5. Do not read cursor context in V1.
6. Use one OpenAI-compatible Chat Completions request path.
7. Presets: DeepSeek `deepseek-flash` and OpenCode Go `deepseek-v4.1-flash`.
8. Use Windows DPAPI for credentials and per-row cache encryption.
9. Store IPC files under user-private `%LOCALAPPDATA%`.
10. Use C# / .NET 8, WinForms, and a persistent tray process.
11. Use Lua filter plus F24 refresh; the C# bridge owns model calls.
12. Require Weasel and rime-ice as prerequisites; no bundled engine in V1.
13. Use a PowerShell installer with backup, managed markers, and idempotent merges.
14. Use `Ctrl+Alt+E`, not the conflicting `Ctrl+Shift+E`.
15. Pin tested Weasel and rime-ice versions and reject unsafe automatic configuration.
16. Do not add auto-update, telemetry, custom prompts, or multiple writing styles in V1.