using System.Text;

namespace LaunchPad.Api.IntegrationTests;

/// <summary>
/// Upload fixtures that start with the right magic bytes. The API checks a file's header
/// against the type it claims (see FileSignatures), so a test that posts arbitrary bytes as
/// an image is now testing the rejection path whether it means to or not. The payload after
/// the header is free text, which keeps byte-for-byte round-trip assertions readable.
/// </summary>
public static class TestFiles
{
    private static byte[] With(ReadOnlySpan<byte> header, string payload) =>
        [.. header, .. Encoding.UTF8.GetBytes(payload)];

    public static byte[] Jpeg(string payload = "jpeg") => With([0xFF, 0xD8, 0xFF, 0xE0], payload);

    public static byte[] Png(string payload = "png") =>
        With([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A], payload);

    public static byte[] Pdf(string payload = "pdf") => With("%PDF-1.7\n"u8, payload);
}
