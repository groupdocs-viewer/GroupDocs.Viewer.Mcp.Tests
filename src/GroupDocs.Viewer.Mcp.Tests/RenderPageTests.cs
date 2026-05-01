using GroupDocs.Viewer.Mcp.IntegrationTests.Fixtures;
using Xunit;
using Xunit.Abstractions;

namespace GroupDocs.Viewer.Mcp.IntegrationTests;

/// GroupDocs.Viewer.RenderPage produces output in evaluation mode (with
/// watermarks) so happy-path assertions work in both eval and licensed mode.
[Collection(McpServerCollection.Name)]
public class RenderPageTests
{
    private readonly McpServerFixture _fixture;
    private readonly ITestOutputHelper _output;

    public RenderPageTests(McpServerFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    [Fact]
    public async Task RenderPage_AuthoredPdfPage1_ReturnsTextAndImageContentBlocks()
    {
        var catalog = await ToolCatalog.LoadAsync(_fixture.Client);

        var response = await _fixture.Client.CallToolAsync(
            catalog.RenderPage.Name,
            new Dictionary<string, object?>
            {
                ["file"] = new Dictionary<string, object?> { ["filePath"] = SampleDocuments.AuthoredPdf },
                ["page"] = 1,
            });

        Assert.False(response.IsError ?? false,
            $"Tool reported an error: {ToolResponse.Text(response)}");

        // RenderPage is unique among GroupDocs MCP tools — it returns a
        // CallToolResult containing BOTH a TextContentBlock (saved file path)
        // AND an ImageContentBlock (PNG bytes inline). Verify both.
        var text = ToolResponse.Text(response);
        _output.WriteLine(text);
        Assert.Contains("authored_page1.png", text, StringComparison.OrdinalIgnoreCase);

        var image = ToolResponse.Image(response);
        Assert.NotNull(image);
        Assert.Equal("image/png", image!.MimeType);
        Assert.True(image.Data.Length > 0,
            "ImageContentBlock should carry non-empty PNG bytes.");

        // The saved file should also exist in storage.
        var savedPath = Path.Combine(_fixture.StoragePath, "authored_page1.png");
        Assert.True(File.Exists(savedPath),
            $"Expected rendered PNG at '{savedPath}'. Response body:\n{text}");
        Assert.True(new FileInfo(savedPath).Length > 0,
            "Rendered PNG file is empty.");
    }

    [Fact]
    public async Task RenderPage_MultiPagePdf_PageNSelection_ProducesDistinctPngs()
    {
        var catalog = await ToolCatalog.LoadAsync(_fixture.Client);

        // Render page 1 and page 3 of the synthetic 3-page PDF. The two pages
        // have different visible text in their /Contents streams, so the
        // rendered PNGs must differ in pixels. If the tool ignored the `page`
        // parameter (e.g. always rendered page 1), this assertion would fail.
        var page1Response = await _fixture.Client.CallToolAsync(
            catalog.RenderPage.Name,
            new Dictionary<string, object?>
            {
                ["file"] = new Dictionary<string, object?> { ["filePath"] = SampleDocuments.MultiPagePdf },
                ["page"] = 1,
            });
        Assert.False(page1Response.IsError ?? false,
            $"Render page 1 failed: {ToolResponse.Text(page1Response)}");

        var page3Response = await _fixture.Client.CallToolAsync(
            catalog.RenderPage.Name,
            new Dictionary<string, object?>
            {
                ["file"] = new Dictionary<string, object?> { ["filePath"] = SampleDocuments.MultiPagePdf },
                ["page"] = SampleDocuments.MultiPageCount,
            });
        Assert.False(page3Response.IsError ?? false,
            $"Render page {SampleDocuments.MultiPageCount} failed: {ToolResponse.Text(page3Response)}");

        // Saved-path text for the two pages must reflect the requested page numbers.
        var page1Text = ToolResponse.Text(page1Response);
        var page3Text = ToolResponse.Text(page3Response);
        Assert.Contains("multipage_page1.png", page1Text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"multipage_page{SampleDocuments.MultiPageCount}.png", page3Text, StringComparison.OrdinalIgnoreCase);

        // Both responses must carry an inline ImageContentBlock.
        var page1Image = ToolResponse.Image(page1Response);
        var page3Image = ToolResponse.Image(page3Response);
        Assert.NotNull(page1Image);
        Assert.NotNull(page3Image);

        // The two PNGs must differ — proves page-N selection is honored.
        var page1Bytes = page1Image!.Data.ToArray();
        var page3Bytes = page3Image!.Data.ToArray();
        Assert.True(page1Bytes.Length > 0, "Page 1 PNG is empty.");
        Assert.True(page3Bytes.Length > 0, $"Page {SampleDocuments.MultiPageCount} PNG is empty.");
        Assert.False(page1Bytes.SequenceEqual(page3Bytes),
            "Page 1 and page 3 must render to distinct PNG bytes (different visible text content).");

        // Saved files for both pages must exist.
        Assert.True(File.Exists(Path.Combine(_fixture.StoragePath, "multipage_page1.png")));
        Assert.True(File.Exists(Path.Combine(_fixture.StoragePath, $"multipage_page{SampleDocuments.MultiPageCount}.png")));
    }

    public static IEnumerable<object[]> RenderableSamples() => new[]
    {
        new object[] { SampleDocuments.SamplePdf,  1 },
        new object[] { SampleDocuments.SampleDocx, 1 },
        new object[] { SampleDocuments.SampleXlsx, 1 },
        new object[] { SampleDocuments.SamplePptx, 1 },
    };

    [Theory]
    [MemberData(nameof(RenderableSamples))]
    public async Task RenderPage_RealSample_ProducesExpectedOutputFile(string fileName, int page)
    {
        if (!File.Exists(Path.Combine(_fixture.StoragePath, fileName)))
        {
            _output.WriteLine($"Sample '{fileName}' not present in storage — skipping.");
            return;
        }

        var catalog = await ToolCatalog.LoadAsync(_fixture.Client);

        var response = await _fixture.Client.CallToolAsync(
            catalog.RenderPage.Name,
            new Dictionary<string, object?>
            {
                ["file"] = new Dictionary<string, object?> { ["filePath"] = fileName },
                ["page"] = page,
            });

        Assert.False(response.IsError ?? false,
            $"Render failed for '{fileName}' page {page}: {ToolResponse.Text(response)}");

        var stem = Path.GetFileNameWithoutExtension(fileName);
        var expectedOutput = $"{stem}_page{page}.png";
        var savedPath = Path.Combine(_fixture.StoragePath, expectedOutput);
        Assert.True(File.Exists(savedPath),
            $"Expected rendered PNG at '{savedPath}'.");
        Assert.True(new FileInfo(savedPath).Length > 0,
            "Rendered PNG file is empty.");

        var image = ToolResponse.Image(response);
        Assert.NotNull(image);
        Assert.Equal("image/png", image!.MimeType);
    }
}
