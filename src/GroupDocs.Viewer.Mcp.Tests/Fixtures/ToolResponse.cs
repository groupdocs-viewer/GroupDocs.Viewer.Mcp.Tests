using System.Text.Json;
using ModelContextProtocol.Protocol;

namespace GroupDocs.Viewer.Mcp.IntegrationTests.Fixtures;

/// Helpers for reading `CallToolResult` contents. GroupDocs.Viewer.Mcp has two
/// shapes: GetViewInfo returns a single TextContentBlock (JSON), RenderPage
/// returns a TextContentBlock plus an ImageContentBlock with the rendered PNG.
internal static class ToolResponse
{
    public static string Text(CallToolResult result)
    {
        var block = result.Content
            .OfType<TextContentBlock>()
            .FirstOrDefault()
            ?? throw new InvalidOperationException(
                "Tool response contained no TextContentBlock.");

        return block.Text ?? string.Empty;
    }

    public static JsonElement Json(CallToolResult result)
    {
        var text = Text(result);
        try
        {
            return JsonDocument.Parse(text).RootElement.Clone();
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                $"Tool response was not valid JSON. Body:\n{text}", ex);
        }
    }

    /// Extract the first ImageContentBlock from the response. RenderPage uses
    /// this to return the rendered PNG inline.
    public static ImageContentBlock? Image(CallToolResult result) =>
        result.Content.OfType<ImageContentBlock>().FirstOrDefault();
}
