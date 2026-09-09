using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using FluentAssertions;
using LaunchPad.Infrastructure.Candidates;
using Microsoft.Extensions.Logging.Abstractions;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;
using Xunit;

namespace LaunchPad.Api.IntegrationTests;

/// <summary>
/// Exercises ResumeTextExtractor directly — same placement reasoning as
/// CompositeNotificationPublisherTests: the class lives in Infrastructure, and
/// LaunchPad.Application.Tests deliberately references only LaunchPad.Application.
///
/// Fixtures are generated in-process rather than committed as binaries. A checked-in PDF is an
/// opaque blob nobody can review or adjust, and building them here means the assertions and the
/// input that produced them stay visibly in sync.
/// </summary>
public class ResumeTextExtractorTests
{
    private readonly ResumeTextExtractor _sut = new(NullLogger<ResumeTextExtractor>.Instance);

    private const string Pdf = "application/pdf";
    private const string Docx = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    // A4 in PostScript points. Given as dimensions rather than via PdfPig's PageSize enum
    // because DocumentFormat.OpenXml.Wordprocessing exports a PageSize type of its own, and
    // both namespaces are in scope here.
    private const double A4WidthPoints = 595;
    private const double A4HeightPoints = 842;

    // --- Fixture builders ---------------------------------------------------

    private static byte[] BuildPdf(params string[][] pagesOfLines)
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);

        foreach (var lines in pagesOfLines)
        {
            var page = builder.AddPage(A4WidthPoints, A4HeightPoints);
            var y = 750;
            foreach (var line in lines)
            {
                page.AddText(line, 12, new PdfPoint(30, y), font);
                y -= 20;
            }
        }

        return builder.Build();
    }

    private static byte[] BuildEmptyPdf()
    {
        // A page with no text operators at all — what a scanned resume looks like to a parser
        // that does no OCR. The real thing carries an embedded image; for extraction purposes
        // the two are the same, since neither yields characters.
        var builder = new PdfDocumentBuilder();
        builder.AddPage(A4WidthPoints, A4HeightPoints);
        return builder.Build();
    }

    private static byte[] BuildDocx(IEnumerable<string> paragraphs, string[][]? tableRows = null)
    {
        using var stream = new MemoryStream();
        using (var document = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var body = new Body();
            foreach (var text in paragraphs)
            {
                body.Append(new Paragraph(new Run(new Text(text))));
            }

            if (tableRows is not null)
            {
                var table = new Table();
                foreach (var row in tableRows)
                {
                    var tableRow = new TableRow();
                    foreach (var cell in row)
                    {
                        tableRow.Append(new TableCell(new Paragraph(new Run(new Text(cell)))));
                    }
                    table.Append(tableRow);
                }
                body.Append(table);
            }

            document.AddMainDocumentPart().Document = new Document(body);
        }

        return stream.ToArray();
    }

    private Task<string?> ExtractAsync(byte[] bytes, string contentType) =>
        _sut.ExtractTextAsync(new MemoryStream(bytes), contentType);

    // --- PDF ----------------------------------------------------------------

    [Fact]
    public async Task Pdf_ExtractsTheText()
    {
        var pdf = BuildPdf([
            "Jordan Avery",
            "Senior Data Engineer",
            "Built ingestion pipelines in Python and Kubernetes",
        ]);

        var text = await ExtractAsync(pdf, Pdf);

        text.Should().NotBeNull();
        text.Should().ContainAll("Jordan", "Engineer", "Python", "Kubernetes");
    }

    [Fact]
    public async Task Pdf_ExtractsEveryPage_NotJustTheFirst()
    {
        // A two-page resume is normal, and losing page two silently would cost the candidate
        // every skill listed there.
        var pdf = BuildPdf(
            ["Jordan Avery", "Experience section continues overleaf"],
            ["Certifications", "Azure Solutions Architect and Terraform Associate"]);

        var text = await ExtractAsync(pdf, Pdf);

        text.Should().ContainAll("Jordan", "Certifications", "Terraform");
    }

    [Fact]
    public async Task Pdf_WithNoExtractableText_ReturnsNull()
    {
        // The scanned-resume case. Null is what makes the run fail loudly; returning an empty
        // string would tell the candidate their resume contained no skills.
        (await ExtractAsync(BuildEmptyPdf(), Pdf)).Should().BeNull();
    }

    [Fact]
    public async Task Pdf_WithOnlyAScrapOfText_ReturnsNull()
    {
        // A scan often yields a few stray glyphs from a logo. Too little to parse is the same
        // outcome as none at all.
        (await ExtractAsync(BuildPdf(["Hi"]), Pdf)).Should().BeNull();
    }

    [Fact]
    public async Task Pdf_ThatIsTruncated_ReturnsNullRatherThanThrowing()
    {
        var valid = BuildPdf(["Jordan Avery", "Senior Data Engineer", "Python and Kubernetes"]);
        var truncated = valid.Take(valid.Length / 3).ToArray();

        var act = async () => await ExtractAsync(truncated, Pdf);

        await act.Should().NotThrowAsync();
        (await ExtractAsync(truncated, Pdf)).Should().BeNull();
    }

    // --- DOCX ---------------------------------------------------------------

    [Fact]
    public async Task Docx_ExtractsParagraphs_SeparatedNotConcatenated()
    {
        var docx = BuildDocx([
            "Jordan Avery",
            "Senior Data Engineer",
            "Built ingestion pipelines in Python",
        ]);

        var text = await ExtractAsync(docx, Docx);

        text.Should().NotBeNull();
        // Body.InnerText would run these together as "Jordan AverySenior Data Engineer",
        // fusing the last word of each line onto the first of the next.
        text.Should().Contain("Jordan Avery\nSenior Data Engineer");
    }

    [Fact]
    public async Task Docx_ExtractsTextInsideTables()
    {
        // Skills matrices are routinely laid out as tables; missing them would drop exactly the
        // content this feature is trying to read.
        var docx = BuildDocx(
            ["Jordan Avery", "Senior Data Engineer with pipeline experience"],
            [["Language", "Level"], ["Python", "Expert"], ["Kubernetes", "Working"]]);

        var text = await ExtractAsync(docx, Docx);

        text.Should().ContainAll("Python", "Expert", "Kubernetes");
    }

    // --- Format identification ----------------------------------------------

    [Fact]
    public async Task DeclaredContentTypeIsIgnored_TheBytesDecide()
    {
        // The declared type comes from an HTTP client, and this file is about to be parsed and
        // sent to a language model. A DOCX mislabelled as PDF must still parse correctly.
        var docx = BuildDocx(["Jordan Avery", "Senior Data Engineer", "Python and Kubernetes"]);

        (await ExtractAsync(docx, Pdf)).Should().Contain("Kubernetes");
    }

    [Fact]
    public async Task UnrecognizedFileSignature_ReturnsNull()
    {
        // A PNG, renamed and declared as a PDF.
        byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, .. new byte[64]];

        (await ExtractAsync(png, Pdf)).Should().BeNull();
    }

    [Fact]
    public async Task EmptyFile_ReturnsNull() =>
        (await ExtractAsync([], Pdf)).Should().BeNull();

    [Fact]
    public async Task FileTooShortToIdentify_ReturnsNull() =>
        (await ExtractAsync([0x25, 0x50], Pdf)).Should().BeNull();

    // --- Normalization ------------------------------------------------------

    [Fact]
    public async Task CollapsesRaggedWhitespace_ButKeepsParagraphBreaks()
    {
        var docx = BuildDocx([
            "Jordan     Avery",
            string.Empty,
            string.Empty,
            "Senior\tData      Engineer",
            "Built pipelines in Python and Kubernetes",
        ]);

        var text = await ExtractAsync(docx, Docx);

        text.Should().NotBeNull();
        text.Should().Contain("Jordan Avery", "runs of spaces collapse to one");
        text.Should().Contain("Senior Data Engineer", "tabs are horizontal whitespace too");
        text.Should().NotContain("\n\n\n", "paragraph breaks survive, but not stacks of them");
        text.Should().NotStartWith("\n").And.NotEndWith("\n");
    }

    // --- Stream handling ----------------------------------------------------

    [Fact]
    public async Task ReadsANonSeekableStream()
    {
        // This is how the file arrives: a blob download hands back a forward-only stream, and
        // both parsers need to seek. Buffering is what makes that work — regressing it would
        // break only in Azure, never in these tests, unless this one exists.
        var pdf = BuildPdf(["Jordan Avery", "Senior Data Engineer", "Python and Kubernetes"]);

        var text = await _sut.ExtractTextAsync(new ForwardOnlyStream(pdf), Pdf);

        text.Should().Contain("Kubernetes");
    }

    [Fact]
    public async Task Cancellation_Propagates_RatherThanBeingSwallowedAsAFailedParse()
    {
        // The catch-all that turns parse errors into null must not also swallow cancellation —
        // a cancelled run should stop, not record itself as an unreadable resume.
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var act = async () => await _sut.ExtractTextAsync(
            new MemoryStream(BuildPdf(["Jordan Avery"])), Pdf, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    /// <summary>A read-only, forward-only stream — what Azure Blob's DownloadStreaming returns.</summary>
    private sealed class ForwardOnlyStream(byte[] data) : Stream
    {
        private readonly MemoryStream _inner = new(data);

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing) _inner.Dispose();
            base.Dispose(disposing);
        }
    }
}
