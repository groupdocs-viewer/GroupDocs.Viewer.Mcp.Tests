---
id: 001
date: 2026-05-01
version: 26.5.0
type: feature
---

# Initial integration-tests suite for GroupDocs.Viewer.Mcp

## What changed
- Test repo bootstrapped: launches the published `GroupDocs.Viewer.Mcp@26.5.0` NuGet via `dnx`, wires an MCP stdio client, and exercises every advertised tool.
- Four test classes:
  - `ToolDiscoveryTests` — server info advertises `GroupDocs.Viewer.Mcp`, tool list contains exactly `RenderPage` and `GetViewInfo`, every tool has a description + input schema.
  - `RenderPageTests` — `RenderPage` produces a non-empty PNG for the synthetic source PDF (verifies BOTH the `TextContentBlock` saved-path AND the `ImageContentBlock` PNG bytes inline), plus theory coverage across real samples (DOCX, XLSX, PPTX, PDF).
  - `GetViewInfoTests` — `GetViewInfo` returns the expected file type, page count, and per-page dimensions for the synthetic source PDF (1 page, ~612×792), plus theory coverage across real samples.
  - `ErrorHandlingTests` — unknown filename returns a clear error, corrupted file does not crash the server, `password` parameter is accepted without schema rejection.
- Synthetic PDF (`authored.pdf`) generated at test startup with both Info-dict metadata AND a `/Contents` stream drawing visible text (`BT /F1 24 Tf … Tj ET`) — guarantees Viewer's rendering path produces non-trivial PNG output.
- Five real samples shipped under `sample-docs/` (sample.docx, .xlsx, .pptx, .pdf, .png) auto-copied to test output and exercised via theories on both tools.
- How-to guides under `how-to/` cover NuGet install, Docker, MCP registry verification, Claude Desktop, VS Code / GitHub Copilot, and running the test suite locally.
- `examples/` ships `claude-desktop.json`, `vscode-mcp.json`, `docker-compose.yml` pinned to `26.5.0`.

## Why
Closes the loop on the published `GroupDocs.Viewer.Mcp` NuGet artifact — every release is exercised end-to-end against live nuget.org so packaging or dnx-shim regressions surface immediately rather than at user install time.

`RenderPage` is the first tool in the GroupDocs MCP family to return a `CallToolResult` directly with an inline `ImageContentBlock`. The integration tests assert on both content blocks (text + image) so any regression in the inline-image path fails CI immediately.

## Migration / impact
First release — no migration required.
