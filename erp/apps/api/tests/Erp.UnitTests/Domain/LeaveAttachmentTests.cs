using Erp.Core.Aggregates.Leave;
using FluentAssertions;

namespace Erp.UnitTests.Domain;

public class LeaveAttachmentTests
{
    [Theory]
    [InlineData("application/pdf", new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34 })] // "%PDF-1.4"
    [InlineData("image/jpeg", new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0, 0, 0, 0 })]
    [InlineData("image/png", new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A })]
    public void Recognizes_the_real_signature_for_each_allowed_type(string contentType, byte[] header)
    {
        LeaveAttachment.MatchesSignature(contentType, header).Should().BeTrue();
    }

    [Fact]
    public void Rejects_a_declared_type_the_bytes_do_not_back_up()
    {
        // An HTML file's actual bytes, wearing a spoofed "application/pdf" Content-Type header —
        // exactly what a client-supplied header alone would never catch.
        var htmlBytes = "<html><body>"u8.ToArray();

        LeaveAttachment.MatchesSignature("application/pdf", htmlBytes).Should().BeFalse();
    }

    [Fact]
    public void Rejects_an_unrecognized_content_type_outright()
    {
        LeaveAttachment.MatchesSignature("application/octet-stream", [0x25, 0x50, 0x44, 0x46]).Should().BeFalse();
    }

    [Fact]
    public void Rejects_a_header_shorter_than_the_signature()
    {
        LeaveAttachment.MatchesSignature("application/pdf", [0x25, 0x50]).Should().BeFalse();
    }
}
