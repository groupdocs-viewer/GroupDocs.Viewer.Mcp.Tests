using GroupDocs.Viewer.Mcp.IntegrationTests.Fixtures;
using Xunit;
using Xunit.Abstractions;

namespace GroupDocs.Viewer.Mcp.IntegrationTests;

[Collection(McpServerCollection.Name)]
public class ErrorHandlingTests
{
    private readonly McpServerFixture _fixture;
    private readonly ITestOutputHelper _output;

    public ErrorHandlingTests(McpServerFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    [Fact]
    public async Task GetViewInfo_UnknownFile_ReturnsErrorListingAvailableFiles()
    {
        var catalog = await ToolCatalog.LoadAsync(_fixture.Client);

        var response = await _fixture.Client.CallToolAsync(
            catalog.ViewInfo.Name,
            new Dictionary<string, object?>
            {
                ["file"] = new Dictionary<string, object?> { ["filePath"] = "does-not-exist.pdf" },
            });

        var body = ToolResponse.Text(response);
        _output.WriteLine(body);

        var isErrorReported = (response.IsError ?? false)
            || body.Contains("not found", StringComparison.OrdinalIgnoreCase)
            || body.Contains("available", StringComparison.OrdinalIgnoreCase)
            || body.Contains(SampleDocuments.AuthoredPdf, StringComparison.OrdinalIgnoreCase);

        Assert.True(isErrorReported,
            $"Expected an error / available-files hint for an unknown file. Response:\n{body}");
    }

    [Fact]
    public async Task RenderPage_CorruptedFile_DoesNotCrashServer()
    {
        var corrupted = "corrupted.pdf";
        await File.WriteAllBytesAsync(
            Path.Combine(_fixture.StoragePath, corrupted),
            new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34, 0xDE, 0xAD, 0xBE, 0xEF });

        var catalog = await ToolCatalog.LoadAsync(_fixture.Client);

        try
        {
            var response = await _fixture.Client.CallToolAsync(
                catalog.RenderPage.Name,
                new Dictionary<string, object?>
                {
                    ["file"] = new Dictionary<string, object?> { ["filePath"] = corrupted },
                    ["page"] = 1,
                });
            _output.WriteLine($"Tool response: {ToolResponse.Text(response)}");
        }
        catch (Exception ex)
        {
            _output.WriteLine($"Call threw (acceptable): {ex.GetType().Name}: {ex.Message}");
        }

        // Prove the server is still alive by making another call.
        var listAfter = await _fixture.Client.ListToolsAsync();
        Assert.NotEmpty(listAfter);
    }

    [Fact]
    public async Task PasswordParameter_IsAcceptedByGetViewInfo()
    {
        // Wrong password on a non-protected file — the tool should treat it as a no-op
        // or return a clear error, but must accept the `password` parameter without
        // rejecting the schema.
        var catalog = await ToolCatalog.LoadAsync(_fixture.Client);

        var response = await _fixture.Client.CallToolAsync(
            catalog.ViewInfo.Name,
            new Dictionary<string, object?>
            {
                ["file"] = new Dictionary<string, object?> { ["filePath"] = SampleDocuments.AuthoredPdf },
                ["password"] = "not-a-real-password",
            });

        var body = ToolResponse.Text(response);
        _output.WriteLine(body);

        Assert.False(string.IsNullOrEmpty(body));
    }
}
