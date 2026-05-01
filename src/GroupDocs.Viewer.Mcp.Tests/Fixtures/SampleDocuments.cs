using System.Text;

namespace GroupDocs.Viewer.Mcp.IntegrationTests.Fixtures;

/// Builds small, self-contained PDFs on disk so tests don't require committed
/// binary files. Each synthetic PDF embeds /Contents streams with visible text
/// so that GroupDocs.Viewer's rendering path produces non-trivial PNG output
/// (Step 9 sanity check from the cloning prompt — fixture must exercise what
/// the tool actually processes).
internal static class SampleDocuments
{
    /// Single-page PDF for basic GetViewInfo / RenderPage assertions.
    public const string AuthoredPdf = "authored.pdf";

    /// Multi-page PDF (3 pages, each with distinct visible text) for
    /// GetViewInfo page-count + per-page-dimensions assertions and for
    /// RenderPage page-N selection assertions (page 1 vs page 3 must
    /// produce different PNG bytes).
    public const string MultiPagePdf = "multipage.pdf";
    public const int MultiPageCount = 3;

    public const string KnownTitle = "Integration Test Document";
    public const string KnownAuthor = "Integration Test Author";

    public const string SamplePdf = "sample.pdf";
    public const string SampleDocx = "sample.docx";
    public const string SampleXlsx = "sample.xlsx";
    public const string SamplePptx = "sample.pptx";
    public const string SamplePng = "sample.png";

    public static IReadOnlyList<string> RealSamples { get; } = new[]
    {
        SamplePdf, SampleDocx, SampleXlsx, SamplePptx, SamplePng,
    };

    public static void WriteAll(string directory)
    {
        Directory.CreateDirectory(directory);

        // Single-page synthetic PDF (legacy fixture for basic assertions).
        File.WriteAllBytes(Path.Combine(directory, AuthoredPdf), BuildSyntheticPdf(
            KnownTitle, KnownAuthor,
            new[]
            {
                new[]
                {
                    "Integration Test Document",
                    "Section A: Sample Content",
                    "Generated for Viewer rendering tests",
                    "Page 1 of 1"
                }
            }));

        // Multi-page synthetic PDF (3 pages with distinct content).
        File.WriteAllBytes(Path.Combine(directory, MultiPagePdf), BuildSyntheticPdf(
            $"{KnownTitle} (Multi-Page)", KnownAuthor,
            new[]
            {
                new[]
                {
                    "Page One: Introduction",
                    "This is the first page of three.",
                    "Distinct content for page-1 rendering tests."
                },
                new[]
                {
                    "Page Two: Body Content",
                    "Different visible text for the middle page.",
                    "Used to verify page-N selection works correctly."
                },
                new[]
                {
                    "Page Three: Conclusion",
                    "Final page with unique terminal content.",
                    "Page-1 vs page-3 PNGs must differ in pixels."
                },
            }));
    }

    public static void CopyRealSamples(string targetDirectory, string? sourceDirectory)
    {
        if (string.IsNullOrEmpty(sourceDirectory) || !Directory.Exists(sourceDirectory))
            return;

        Directory.CreateDirectory(targetDirectory);
        foreach (var name in RealSamples)
        {
            var src = Path.Combine(sourceDirectory, name);
            if (File.Exists(src))
                File.Copy(src, Path.Combine(targetDirectory, name), overwrite: true);
        }
    }

    public static string? ResolveSourceSampleDocs()
    {
        var env = Environment.GetEnvironmentVariable("GROUPDOCS_MCP_SAMPLE_DOCS");
        if (!string.IsNullOrEmpty(env) && Directory.Exists(env))
            return env;

        var staged = Path.Combine(AppContext.BaseDirectory, "sample-docs");
        if (Directory.Exists(staged))
            return staged;

        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 10 && !string.IsNullOrEmpty(dir); i++)
        {
            var candidate = Path.Combine(dir, "sample-docs");
            if (Directory.Exists(candidate))
                return candidate;
            dir = Path.GetDirectoryName(dir);
        }

        return null;
    }

    /// Minimal PDF 1.4 with an Info dictionary AND one /Contents stream per
    /// page. Object offsets are computed at build time so the xref table is
    /// byte-accurate. Object layout:
    ///   1: Catalog
    ///   2: Pages (parent — references all page objects in /Kids)
    ///   3: Info
    ///   4: Font (single Helvetica, shared by all pages)
    ///   5..(4+N): Page objects (one per page, all reference Font 4 0 R)
    ///   (5+N)..(4+2N): Content streams (one per page, with text drawn via BT…Tj…ET)
    private static byte[] BuildSyntheticPdf(string title, string author, string[][] pageTexts)
    {
        if (pageTexts.Length == 0)
            throw new ArgumentException("pageTexts must contain at least one page.", nameof(pageTexts));

        var pageCount = pageTexts.Length;

        // Pre-compute each page's content-stream bytes so we know the /Length
        // for the dict header before we write the object body.
        var streams = pageTexts.Select(textLines =>
        {
            var sb = new StringBuilder();
            var y = 720;
            for (var i = 0; i < textLines.Length; i++)
            {
                var size = i == 0 ? 24 : 14;
                sb.Append($"BT /F1 {size} Tf 72 {y} Td ({EscapePdfString(textLines[i])}) Tj ET\n");
                y -= i == 0 ? 36 : 22;
            }
            return sb.ToString().TrimEnd('\n');
        }).ToList();
        var streamLengths = streams.Select(s => Encoding.ASCII.GetByteCount(s)).ToList();

        var body = new StringBuilder();
        var offsets = new List<int>();

        void WriteObj(string obj)
        {
            offsets.Add(body.Length);
            body.Append(obj);
        }

        body.Append("%PDF-1.4\n%\xE2\xE3\xCF\xD3\n");

        WriteObj("1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");

        var kids = string.Join(" ", Enumerable.Range(0, pageCount).Select(i => $"{5 + i} 0 R"));
        WriteObj($"2 0 obj\n<< /Type /Pages /Kids [{kids}] /Count {pageCount} >>\nendobj\n");

        WriteObj($"3 0 obj\n<< /Title ({EscapePdfString(title)}) /Author ({EscapePdfString(author)}) /Creator (GroupDocs.Viewer.Mcp integration tests) >>\nendobj\n");
        WriteObj("4 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>\nendobj\n");

        // Page objects (5..4+N).
        for (var i = 0; i < pageCount; i++)
        {
            var contentObjNum = 5 + pageCount + i;
            WriteObj($"{5 + i} 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 4 0 R >> >> /Contents {contentObjNum} 0 R >>\nendobj\n");
        }

        // Content streams (5+N..4+2N).
        for (var i = 0; i < pageCount; i++)
        {
            WriteObj($"{5 + pageCount + i} 0 obj\n<< /Length {streamLengths[i]} >>\nstream\n{streams[i]}\nendstream\nendobj\n");
        }

        var totalObjects = 4 + pageCount * 2;
        var xrefOffset = body.Length;
        body.Append($"xref\n0 {totalObjects + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets)
            body.Append($"{offset:D10} 00000 n \n");

        body.Append($"trailer\n<< /Size {totalObjects + 1} /Root 1 0 R /Info 3 0 R >>\n");
        body.Append($"startxref\n{xrefOffset}\n%%EOF");

        return Encoding.ASCII.GetBytes(body.ToString());
    }

    private static string EscapePdfString(string s) =>
        s.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
}
