using Erp.Core.Aggregates.Leave;

namespace Erp.UnitTests;

/// <summary>
/// A stand-in doctor's note. Sick leave will not construct without one, so every Sick fixture
/// in the suite needs an attachment whether or not the test is about attachments.
/// </summary>
internal static class TestAttachments
{
    internal static LeaveAttachment DoctorsNote() =>
        LeaveAttachment.Create("2026/08/note", "surat-dokter.pdf", "application/pdf", 1024);
}
