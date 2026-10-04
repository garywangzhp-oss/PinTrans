# Manual test matrix

Run each row after installing HanBridge with a configured provider.

| ID | Application | Test | Expected | Result |
|---|---|---|---|---|
| APP-01 | WeChat desktop | Type a two-character Chinese phrase and wait | Chinese candidate remains first; English candidate appears second | Pass |
| APP-02 | Microsoft Word | Type a sentence and choose English candidate | English commits in place with no extra newline | Pass |
| APP-03 | Chrome | Type in a normal form field | Candidate 2 appears; selecting it commits English | Pass |
| APP-04 | Edge | Type in a normal form field | Candidate 2 appears; selecting it commits English | Pass |
| APP-05 | VS Code | Type Chinese in a string and use `Ctrl+Alt+E` | VS Code Explorer shortcut still works; HanBridge toggle is separate | Pass |
| APP-06 | Feishu | Type and commit both candidates | Both output paths work | Pass |
| APP-07 | Notepad | Type and commit both candidates | Both output paths work | Pass |
| APP-08 | WPS Writer | Type a paragraph and commit English | English commits without cursor jump | Pass |
| APP-09 | WPS Spreadsheet | Edit a cell and commit English | English remains inside the active cell | Pass |
| APP-10 | WPS Presentation | Edit a text box and commit English | English remains inside the text box | Pass |
| APP-11 | Foxmail | Compose a message and commit English | English commits in the composition area | Pass |
| APP-12 | Outlook classic | Compose and commit both candidates | Both output paths work | Pass |
| APP-13 | New Outlook | Compose in a WebView field | Candidate 2 works like the Edge case | Pass |
| APP-14 | Betterbird | Compose a message and wait for candidate 2 | Candidate 2 appears; selecting it commits English | Pass |
| SAFE-01 | Password field | Type Chinese with translation enabled | Best-effort detection suppresses network request; otherwise toggle off before typing | Pending |
| SAFE-02 | Translation off | Type and wait | No model request; Chinese input remains responsive | Pass |
| PERF-01 | Any text editor | Type continuously for 10 minutes | No input lag or candidate flicker | Pending |
| PERF-02 | Any text editor | Leave HanBridge running for 8 hours | No crash and no sustained memory growth | Pending |
| FAIL-01 | Any app | Disconnect network, type Chinese | Chinese input remains responsive; translation candidate is absent | Pass (automated) |
| FAIL-02 | Any app | Use an invalid API key | Chinese input remains responsive; tray shows configuration error | Pass (automated) |