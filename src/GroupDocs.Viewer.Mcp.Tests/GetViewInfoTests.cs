using GroupDocs.Viewer.Mcp.IntegrationTests.Fixtures;
using Xunit;
using Xunit.Abstractions;

namespace GroupDocs.Viewer.Mcp.IntegrationTests;

[Collection(McpServerCollection.Name)]
public class GetViewInfoTests
{
    private readonly McpServerFixture _fixture;
    private readonly ITestOutputHelper _output;

    public GetViewInfoTests(McpServerFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    [Fact]
    public async Task GetViewInfo_AuthoredPdf_ReturnsFileTypeAndOnePage()
    {
        var catalog = await ToolCatalog.LoadAsync(_fixture.Client);

        var response = await _fixture.Client.CallToolAsync(
            catalog.ViewInfo.Name,
            new Dictionary<string, object?>
            {
                ["file"] = new Dictionary<string, object?> { ["filePath"] = SampleDocuments.AuthoredPdf },
            });

        Assert.False(response.IsError ?? false,
            $"Tool reported an error: {ToolResponse.Text(response)}");

        var json = ToolResponse.Json(response);
        _output.WriteLine(json.ToString());

        Assert.Equal(SampleDocuments.AuthoredPdf, json.GetProperty("fileName").GetString());
        Assert.Equal(1, json.GetProperty("pageCount").GetInt32());
        var pages = json.GetProperty("pages");
        Assert.Equal(1, pages.GetArrayLength());
        var page1 = pages[0];
        Assert.Equal(1, page1.GetProperty("number").GetInt32());
        Assert.True(page1.GetProperty("width").GetDouble() > 0,
            "Expected non-zero page width for synthetic PDF (MediaBox 612x792).");
        Assert.True(page1.GetProperty("height").GetDouble() > 0,
            "Expected non-zero page height for synthetic PDF.");
    }

    [Fact]
    public async Task GetViewInfo_MultiPagePdf_ReportsMultiplePagesWithDimensions()
    {
        // The synthetic fixture (SampleDocuments.MultiPagePdf) is a valid PDF
        // with SampleDocuments.MultiPageCount (3) pages. GroupDocs.Viewer's
        // EVALUATION mode, however, limits the number of pages it processes —
        // unlicensed CI typically reports 2 pages, not 3. So this test asserts
        // multi-page DETECTION (>= 2, which distinguishes it from the 1-page
        // AuthoredPdf) up to the fixture's true page count, rather than an
        // exact count the eval mode won't yield. With a license the engine
        // reports the full 3. See Pitfall #3 in the clone-to-new-product.md
        // prompt (Viewer eval-mode per-page cap).
        var catalog = await ToolCatalog.LoadAsync(_fixture.Client);

        var response = await _fixture.Client.CallToolAsync(
            catalog.ViewInfo.Name,
            new Dictionary<string, object?>
            {
                ["file"] = new Dictionary<string, object?> { ["filePath"] = SampleDocuments.MultiPagePdf },
            });

        Assert.False(response.IsError ?? false,
            $"Tool reported an error: {ToolResponse.Text(response)}");

        var json = ToolResponse.Json(response);
        _output.WriteLine(json.ToString());

        Assert.Equal(SampleDocuments.MultiPagePdf, json.GetProperty("fileName").GetString());

        var pageCount = json.GetProperty("pageCount").GetInt32();
        Assert.True(pageCount >= 2 && pageCount <= SampleDocuments.MultiPageCount,
            $"Expected the multi-page fixture to report between 2 and " +
            $"{SampleDocuments.MultiPageCount} pages (eval mode caps it; licensed mode " +
            $"reports the full {SampleDocuments.MultiPageCount}). Got {pageCount}.");

        var pages = json.GetProperty("pages");
        Assert.Equal(pageCount, pages.GetArrayLength());
        for (var i = 0; i < pages.GetArrayLength(); i++)
        {
            var page = pages[i];
            Assert.Equal(i + 1, page.GetProperty("number").GetInt32());
            Assert.True(page.GetProperty("width").GetDouble() > 0,
                $"Expected non-zero width for page {i + 1}.");
            Assert.True(page.GetProperty("height").GetDouble() > 0,
                $"Expected non-zero height for page {i + 1}.");
        }
    }

    public static IEnumerable<object[]> RealSampleData() => new[]
    {
        new object[] { SampleDocuments.SamplePdf  },
        new object[] { SampleDocuments.SampleDocx },
        new object[] { SampleDocuments.SampleXlsx },
        new object[] { SampleDocuments.SamplePptx },
    };

    [Theory]
    [MemberData(nameof(RealSampleData))]
    public async Task GetViewInfo_RealSample_ReturnsValidInfo(string fileName)
    {
        if (!File.Exists(Path.Combine(_fixture.StoragePath, fileName)))
        {
            _output.WriteLine($"Sample '{fileName}' not present in storage — skipping.");
            return;
        }

        var catalog = await ToolCatalog.LoadAsync(_fixture.Client);

        var response = await _fixture.Client.CallToolAsync(
            catalog.ViewInfo.Name,
            new Dictionary<string, object?>
            {
                ["file"] = new Dictionary<string, object?> { ["filePath"] = fileName },
            });

        Assert.False(response.IsError ?? false,
            $"Tool reported an error for '{fileName}': {ToolResponse.Text(response)}");

        // Larger documents (XLSX, PPTX) may exceed the tool's output budget;
        // verify by substring rather than full JsonDocument.Parse.
        var body = ToolResponse.Text(response);
        _output.WriteLine(body);

        Assert.Contains("\"fileType\"", body);
        Assert.Contains("\"pageCount\"", body);
        Assert.Contains("\"pages\"", body);
        Assert.Contains(fileName, body);
    }
}
