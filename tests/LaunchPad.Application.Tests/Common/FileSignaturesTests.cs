using FluentAssertions;
using LaunchPad.Application.Common;
using Xunit;

namespace LaunchPad.Application.Tests.Common;

/// <summary>
/// The case worth pinning down is the mismatch: a file whose name and declared type say one
/// thing while its bytes say another. Uploads are served back to other people, so that gap is
/// the whole reason these checks exist.
/// </summary>
public class FileSignaturesTests
{
    private static readonly byte[] Pdf = [.. "%PDF-1.7"u8];
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00];
    private static readonly byte[] ZipOrOfficeXml = [0x50, 0x4B, 0x03, 0x04];
    private static readonly byte[] WindowsExecutable = [0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00];
    private static readonly byte[] Html = [.. "<script>alert(1)</script>"u8];

    [Fact]
    public void RealFilesMatchTheirExtension()
    {
        FileSignatures.MatchesExtension(Pdf, ".pdf").Should().BeTrue();
        FileSignatures.MatchesExtension(Png, ".png").Should().BeTrue();
        FileSignatures.MatchesExtension(ZipOrOfficeXml, ".docx").Should().BeTrue();
        FileSignatures.MatchesExtension([.. "id,name"u8], ".csv").Should().BeTrue();
    }

    [Fact]
    public void AnExecutableRenamedToPdfIsRejected()
    {
        FileSignatures.MatchesExtension(WindowsExecutable, ".pdf").Should().BeFalse();
        FileSignatures.MatchesExtension(WindowsExecutable, ".txt").Should().BeFalse("an executable is not plain text");
        FileSignatures.IsAllowedDeliverableFileName("payload.exe").Should().BeFalse();
    }

    [Fact]
    public void HtmlSentAsAPdfIsRejected()
    {
        FileSignatures.MatchesExtension(Html, ".pdf").Should().BeFalse();
    }

    [Fact]
    public void ImageUploadsAreCheckedAgainstTheDeclaredContentType()
    {
        FileSignatures.MatchesImageContentType(Png, "image/png").Should().BeTrue();
        FileSignatures.MatchesImageContentType(Html, "image/png").Should().BeFalse();
        FileSignatures.MatchesImageContentType(Png, "image/webp").Should().BeFalse();
        FileSignatures.MatchesImageContentType(Png, "image/svg+xml")
            .Should().BeFalse("SVG can carry script and is not an accepted image upload");
    }

    [Fact]
    public void AnUnknownExtensionIsNeverAllowed()
    {
        FileSignatures.MatchesExtension(Pdf, ".exe").Should().BeFalse();
        FileSignatures.IsAllowedDeliverableFileName("notes").Should().BeFalse();
    }
}
