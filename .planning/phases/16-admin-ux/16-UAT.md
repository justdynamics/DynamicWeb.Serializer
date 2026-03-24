---
status: diagnosed
phase: 16-admin-ux
source: [16-01-SUMMARY.md, 16-02-SUMMARY.md, 16-03-SUMMARY.md, 16-04-SUMMARY.md]
started: 2026-03-24T14:30:00Z
updated: 2026-03-24T15:40:00Z
---

## Current Test

[testing complete]

## Tests

### 1. API Command Rename
expected: POST /Admin/Api/SerializerSerialize returns status "ok". POST /Admin/Api/ContentSyncSerialize returns "Unknown command".
result: pass

### 2. Per-Run Log Files
expected: After serialization, a timestamped log file like Serialize_2026-03-24_142815.log appears in the Log directory with a JSON summary header between === SERIALIZER SUMMARY === markers, followed by timestamped text lines.
result: pass

### 3. Config Backward Compatibility
expected: DW instance loads Serializer.config.json if present. If only ContentSync.config.json exists, it falls back to that. Config error messages mention both file names.
result: pass

### 4. Admin Tree Node Location
expected: In DW admin, navigate to Settings. The "Serialize" node appears under Settings > Database (not under Settings > Content). Clicking it opens the settings screen.
result: pass

### 5. Settings Screen Title
expected: The settings screen header reads "Serialize Settings" (not "Content Sync Settings" or "Sync Settings").
result: pass

### 6. Log Viewer Screen
expected: Under Settings > Database > Serialize, there is a "Log Viewer" sub-node. Clicking it shows a screen with a dropdown of log files (most recent first), per-provider summary, advice text, and raw log output.
result: issue
reported: "the 2 logs are visible but selecting a file does not update the details"
severity: major

### 7. Log Viewer File Selection
expected: The log viewer dropdown lists all per-run log files. Selecting a different file updates the displayed summary, advice, and raw log content.
result: pass

### 8. Import to Database Action on Zip
expected: In DW admin asset management (Files), navigate to a .zip file inside the configured output directory. The file detail page shows an "Import to database" action. Clicking it shows a dry-run confirmation with per-table breakdown before executing.
result: issue
reported: "The zip file has an action menu, but clicking import the dialog shows: Zip file not found: /Files/System/Serializer/Download/Serializer_Home Machines_2026-03-24.zip"
severity: major

### 9. Import to Database Gating
expected: The "Import to database" action does NOT appear on non-.zip files, and does NOT appear on .zip files outside the configured output directory.
result: pass

## Summary

total: 9
passed: 7
issues: 2
pending: 0
skipped: 0
blocked: 0

## Gaps

- truth: "Log viewer dropdown selection updates displayed summary, advice, and raw log content"
  status: failed
  reason: "User reported: the 2 logs are visible but selecting a file does not update the details"
  severity: major
  test: 6
  root_cause: "LogViewerScreen.CreateLogFileSelect() returns Select without .WithReloadOnChange() — dropdown is passive, never triggers postback"
  artifacts:
    - path: "src/DynamicWeb.Serializer/AdminUI/Screens/LogViewerScreen.cs"
      issue: "CreateLogFileSelect() missing .WithReloadOnChange() on Select"
  missing:
    - "Add .WithReloadOnChange() to the Select in CreateLogFileSelect()"
  debug_session: ""

- truth: "Clicking Import to database on a zip shows dry-run confirmation with per-table breakdown"
  status: failed
  reason: "User reported: The zip file has an action menu, but clicking import the dialog shows: Zip file not found: /Files/System/Serializer/Download/Serializer_Home Machines_2026-03-24.zip"
  severity: major
  test: 8
  root_cause: "Doubled Files segment in path: DW virtual path /Files/System/... combined with filesRoot that already points to the Files directory produces Files/Files/System/..."
  artifacts:
    - path: "src/DynamicWeb.Serializer/AdminUI/Models/DeserializeFromZipModel.cs"
      issue: "Path.Combine(filesRoot, filePath.TrimStart('/')) doubles the Files segment"
    - path: "src/DynamicWeb.Serializer/AdminUI/Commands/DeserializeFromZipCommand.cs"
      issue: "Same doubled Files segment in path construction"
  missing:
    - "Use webRoot (parent of filesRoot) as base for Path.Combine, so /Files/... virtual paths resolve correctly"
  debug_session: ""
