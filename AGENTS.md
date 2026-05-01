# AGENTS.md — Guide for AI coding agents

Brief orientation for AI coding agents (Claude Code, Copilot, Cursor, Aider, Amp, Codex) working in this repository.

## What this repo is

**Integration tests** for the [`GroupDocs.Viewer.Mcp`](https://www.nuget.org/packages/GroupDocs.Viewer.Mcp) NuGet package — an MCP server that exposes GroupDocs.Viewer for .NET as AI-callable tools.

This repo is **not** the server itself. The server lives at [groupdocs-viewer/GroupDocs.Viewer.Mcp](https://github.com/groupdocs-viewer/GroupDocs.Viewer.Mcp). This repo:

1. Consumes only the **published** NuGet artifact (no project references).
2. Launches the server via `dnx`, connects as an MCP stdio client, and exercises every advertised tool.
3. Doubles as a copy-pasteable set of example configs and how-to guides for all deployment channels (NuGet, Docker, MCP registry, Claude Desktop, VS Code).

## Folder layout

```
src/GroupDocs.Viewer.Mcp.Tests/
  Fixtures/
    McpServerFixture.cs          ← launches dnx child process, wires stdio MCP client
    SampleDocuments.cs           ← builds a synthetic PDF with /Contents stream at runtime
    ToolCatalog.cs               ← keyword-based tool name resolution (render, viewinfo)
    ToolResponse.cs              ← CallToolResult text/JSON/image extraction
    CommandResolver.cs           ← cross-platform dnx.cmd resolution on Windows
    PackageVersion.cs            ← pulls version from env / assembly metadata / default
  ToolDiscoveryTests.cs          ← handshake, tools/list, schema validation
  RenderPageTests.cs             ← synthetic + real-sample rendering; verifies BOTH text and image content blocks
  GetViewInfoTests.cs            ← file type, page count, per-page dimensions for synthetic + real samples
  ErrorHandlingTests.cs          ← unknown file, corrupted bytes, password parameter
  GroupDocs.Viewer.Mcp.Tests.csproj
.github/workflows/integration.yml  ← matrix × 3 OS, nightly cron, release-smoke dispatch
changelog/                         ← one MD file per change (NNN-slug.md)
how-to/                            ← user-facing guides for every deployment channel
examples/                          ← claude-desktop.json, vscode-mcp.json, docker-compose.yml
sample-docs/                       ← drop real fixture files here; copied to test output
Directory.Build.props              ← McpPackageVersion property (overridable)
global.json                        ← pinned to .NET 10.0.100
```

## What gets tested

| Area | Covered by |
|---|---|
| Package installs and starts via `dnx` | `McpServerFixture` |
| MCP handshake, server info, version | `ToolDiscoveryTests` |
| `RenderPage` — synthetic PDF + real-sample theory; verifies both `TextContentBlock` and `ImageContentBlock` in the response | `RenderPageTests` |
| `GetViewInfo` — file type, page count, per-page dimensions for synthetic + real samples | `GetViewInfoTests` |
| Unknown / corrupted files, password parameter | `ErrorHandlingTests` |

## Commands you can run

```bash
# Restore + build
dotnet restore
dotnet build -c Release

# Run all tests against the default package version
dotnet test -c Release

# Run against a specific published version
dotnet test -c Release -p:McpPackageVersion=26.5.0
# or
MCP_PACKAGE_VERSION=26.5.0 dotnet test -c Release

# Unlock licensed-mode tests (drops watermarks)
GROUPDOCS_LICENSE_PATH=/path/to/GroupDocs.Total.lic dotnet test -c Release

# Run just the discovery suite (fastest — no tool invocations)
dotnet test -c Release --filter "FullyQualifiedName~ToolDiscovery"
```

## Key design decisions

1. **Keyword-based tool resolution.** `ToolCatalog.RenderPage` resolves by `"render"`, `ToolCatalog.ViewInfo` by `"viewinfo"`. The MCP C# SDK currently uses the method name verbatim (PascalCase: `RenderPage`, `GetViewInfo`) — keyword resolution keeps tests robust against future renames / casing convention changes.

2. **Synthetic PDF with real content.** `SampleDocuments.cs` builds a minimal PDF with both an Info dict (for the metadata-based assertions) AND a `/Contents` stream drawing visible text via `BT /F1 24 Tf … (text) Tj ET` operators. This guarantees `RenderPage` produces non-trivial PNG output and `GetViewInfo` reports a real page count + dimensions.

3. **`RenderPage` returns `CallToolResult` directly.** Unlike `GetViewInfo` (which returns a JSON string wrapped in a `TextContentBlock` server-side), `RenderPage` builds a `CallToolResult` with **two** content blocks: a `TextContentBlock` (saved file path) and an `ImageContentBlock` (PNG bytes inline). Tests assert on both — see `RenderPageTests.RenderPage_AuthoredPdfPage1_ReturnsTextAndImageContentBlocks`.

4. **Evaluation-mode handling.** `GroupDocs.Viewer` produces watermarked output in eval mode, never throws. Tests assert non-error responses + output-file existence in both modes — they don't branch on `GROUPDOCS_LICENSE_PATH` for happy-path assertions.

5. **No project references to the server.** The csproj only references `ModelContextProtocol` 1.1.0. If the server source breaks in the sibling repo, these tests still pass — they validate the shipped NuGet artifact.

## House rules

1. **Changelog entries required** — any PR that changes behaviour adds `changelog/NNN-slug.md` (schema in `changelog/README.md`).
2. **How-to guides track deployment reality** — if the main repo publishes a new channel (e.g. new Docker registry), add a guide under `how-to/` *and* update `README.md`.
3. **Version bumps flow through `Directory.Build.props`** — `<McpPackageVersion>` is the single source of truth for "what version are we testing." CI overrides it via env var / workflow input.
4. **Tests must not require the main repo's source.** If a test needs a server-side change, file an issue there — don't work around it here.
5. **Target framework is `net10.0` only** — required by `dnx` and the MCP SDK.

## Release smoke hook

The main repo's `publish_prod.yml` should fire a `repository_dispatch` with `event_type=nuget-published` after `dotnet nuget push` succeeds. The workflow in `.github/workflows/integration.yml` consumes `client_payload.package_version` and runs the matrix against the just-published version.

## What NOT to change

- Do not add a `ProjectReference` to the main repo's `GroupDocs.Viewer.Mcp.csproj`. This repo exists to test the shipped NuGet, not the source.
- Do not hardcode tool names as string literals (`"render"`). Use `ToolCatalog.RenderPage.Name` / `ToolCatalog.ViewInfo.Name`.
- Do not commit real license files or binary fixtures with unclear provenance. License goes through the `GROUPDOCS_LICENSE` CI secret; fixtures in `sample-docs/` must be self-authored or CC0/Apache-2.0.
- Do not assume `RenderPage` returns a string — it returns a `CallToolResult` with both text and image content blocks. Tests must use `ToolResponse.Image(result)` to access the PNG.
