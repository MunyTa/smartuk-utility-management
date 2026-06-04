using UkManagement.Web.Domain;

namespace UkManagement.Web.Services;

public sealed record IngestionResult(bool Accepted, string Message, ReadingQuality? Quality = null)
{
    public static IngestionResult Rejected(string message) => new(false, message);

    public static IngestionResult Stored(ReadingQuality quality, string message) => new(true, message, quality);
}
