# Acceptance testing

V1 uses a 100-row CSV fixture. Fill `tests/fixtures/acceptance-template.csv` with real user-provided sentences after the application is running with a valid model provider.

Columns:

- `id`: stable row number.
- `category`: one of the predefined categories.
- `source`: the exact Chinese text submitted by Rime.
- `expected_reference`: optional human reference translation.
- `candidate_seen`: `true` when the English candidate appeared.
- `latency_ms`: elapsed time from request to candidate render.
- `human_accept`: `true` when the translation can be committed without editing.
- `notes`: optional failure details.

Evaluate:

```powershell
.\tools\Evaluate-Acceptance.ps1
```

The script requires all 100 sources to be filled and applies the frozen thresholds: candidate availability >= 90%, candidate latency <= 1500 ms >= 90%, and human acceptance >= 85%.