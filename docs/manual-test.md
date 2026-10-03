# Manual test matrix

Run each row after installing HanBridge with a configured provider.

| ID | Application | Test | Expected | Result |
|---|---|---|---|---|
| APP-01 | WeChat desktop | Type a two-character Chinese phrase and wait | Chinese candidate remains first; English candidate appears second | Pending |
| APP-02 | Microsoft Word | Type a sentence and choose English candidate | English commits in place with no extra newline | Pending |
| APP-03 | Chrome | Type in a normal form field | Candidate 2 appears; selecting it commits English | Pending |
| APP-04 | Edge | Type in a normal form field | Candidate 2 appears; selecting it commits English | Pending |
| APP-05 | VS Code | Type Chinese in a string and use `Ctrl+Shift+E` | VS Code Explorer shortcut still works; HanBridge toggle is separate | Pending |
| APP-06 | Feishu | Type and commit both candidates | Both output paths work | Pending |
| APP-07 | Notepad | Type and commit both candidates | Both output paths work | Pending |
| APP-08 | WPS Writer | Type a paragraph and commit English | English commits without cursor jump | Pending |
| APP-09 | WPS Spreadsheet | Edit a cell and commit English | English remains inside the active cell | Pending |
| APP-10 | WPS Presentation | Edit a text box and commit English | English remains inside the text box | Pending |
| APP-11 | Foxmail | Compose a message and commit English | English commits in the composition area | Pending |
| APP-12 | Outlook classic | Compose and commit both candidates | Both output paths work | Pending |
| APP-13 | New Outlook | Compose in a WebView field | Candidate 2 works like the Edge case | Pending |
| SAFE-01 | Password field | Type Chinese with translation enabled | Best-effort detection suppresses network request; otherwise toggle off before typing | Pending |
| SAFE-02 | Translation off | Type and wait | No request/response files and no network activity | Pending |
| PERF-01 | Any text editor | Type continuously for 10 minutes | No input lag or candidate flicker | Pending |
| PERF-02 | Any text editor | Leave HanBridge running for 8 hours | No crash and no sustained memory growth | Pending |
| FAIL-01 | Any app | Disconnect network, type Chinese | Chinese input remains responsive; translation candidate is absent | Pending |
| FAIL-02 | Any app | Use an invalid API key | Chinese input remains responsive; tray shows configuration error | Pending |