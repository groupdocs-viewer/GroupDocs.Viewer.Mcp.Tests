# Use with Cursor

Connect the MCP server to [Cursor](https://cursor.com) so you can ask its Agent
to render document pages as images or inspect a document's view info.

## Prerequisites

- Cursor installed and updated (MCP support is in **Settings → Tools & MCP**).
- One of:
  - [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (for the `dnx` route — recommended), or
  - [Docker](https://www.docker.com/products/docker-desktop) (for the container route).

## Config file location

Cursor uses the **`mcpServers`** key (like Claude Desktop) — **not** `servers`
as in VS Code. Two scopes:

| Scope | Path |
|---|---|
| Global (all projects) | `~/.cursor/mcp.json` (macOS/Linux) · `%USERPROFILE%\.cursor\mcp.json` (Windows) |
| Project-only | `.cursor/mcp.json` in the workspace root |

Create the file if it doesn't exist.

## Option A — dnx (recommended)

```json
{
  "mcpServers": {
    "groupdocs-viewer": {
      "command": "dnx",
      "args": ["GroupDocs.Viewer.Mcp@26.7.0", "--yes"],
      "env": {
        "GROUPDOCS_MCP_STORAGE_PATH": "/Users/you/Documents"
      }
    }
  }
}
```

- Replace the storage path with an **absolute path** to the folder Cursor should
  operate on. On Windows use `"C:\\Users\\you\\Documents"` (double-escaped) or
  forward slashes.
- Omit `@26.7.0` to always pull the latest stable.
- Add `"GROUPDOCS_LICENSE_PATH": "…/GroupDocs.Total.lic"` to `env` to remove the
  evaluation watermark from rendered pages. `render_page` and `get_view_info`
  both work without a license — evaluation mode just watermarks the PNG output.

Copy-paste starter: [examples/cursor-mcp.json](../examples/cursor-mcp.json).

## Option B — Windows: full path to `dotnet.exe` (SSL / timeout workaround)

On Windows, Cursor launching `dnx` can fail with an **SSL / ~30 s timeout** on
the first package probe. Bypass `dnx` by running the already-cached tool DLL
directly with `dotnet.exe`:

```json
{
  "mcpServers": {
    "groupdocs-viewer": {
      "command": "C:\\Program Files\\dotnet\\dotnet.exe",
      "args": [
        "C:\\Users\\you\\.nuget\\packages\\groupdocs.viewer.mcp\\26.7.0\\tools\\net10.0\\any\\GroupDocs.Viewer.Mcp.dll"
      ],
      "env": {
        "GROUPDOCS_MCP_STORAGE_PATH": "C:\\Users\\you\\Documents"
      }
    }
  }
}
```

Populate the cache first by running `dnx GroupDocs.Viewer.Mcp@26.7.0 --yes` once
in a terminal, then point `args[0]` at the resulting
`…\.nuget\packages\groupdocs.viewer.mcp\<version>\tools\net10.0\any\GroupDocs.Viewer.Mcp.dll`.

## Option C — Docker

```json
{
  "mcpServers": {
    "groupdocs-viewer": {
      "command": "docker",
      "args": [
        "run", "--rm", "-i",
        "-v", "/Users/you/Documents:/data",
        "ghcr.io/groupdocs-viewer/viewer-net-mcp:26.7.0"
      ]
    }
  }
}
```

## Reload and verify

1. Save `mcp.json`.
2. **Settings → Tools & MCP** → find `groupdocs-viewer` → toggle it on (or hit
   the reload icon). A green dot means it connected.
3. Expand it — you should see `render_page` and `get_view_info`.

## Example prompts (Agent mode)

```
Render page 1 of report.pdf — show me the image.

How many pages does contract.docx have? What size is each page?

Show me page 5 of the slide deck quarterly.pptx.

Inspect /docs/legal-brief.pdf — what file type and page count?
```

The Agent will call `render_page` / `get_view_info` and compose its answer from
the results — `render_page` returns the PNG inline so Cursor can display it.

## Troubleshooting

| Symptom | Fix |
|---|---|
| Server greyed out / won't start on Windows | `dnx` SSL/timeout — use **Option B** (full `dotnet.exe` path + cached DLL). |
| Server not listed | JSON typo — Cursor silently drops unparseable entries. Validate with `jq . mcp.json`. Confirm the key is `mcpServers`, not `servers`. |
| Rendered pages carry a watermark | Expected in evaluation mode. Add `GROUPDOCS_LICENSE_PATH` to `env` to produce clean output. `get_view_info` is unaffected (read-only). |
| `DllNotFoundException: libgdiplus` (macOS/Linux) | Install native deps — `brew install mono-libgdiplus` (macOS) / `apt-get install libgdiplus libfontconfig1 ttf-mscorefonts-installer` (Linux), or use the Docker option. |

## Next steps

- [04 — Use with Claude Desktop](04-use-with-claude-desktop.md)
- [05 — Use with VS Code / Copilot](05-use-with-vscode-copilot.md)
