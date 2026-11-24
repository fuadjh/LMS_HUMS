using ReusableAccess.Contracts;

namespace ReusableAccess.Application.Academics;

public interface ILmsFileTransport
{
    Task<FileRow> ReceiveAsync(Guid trackingId, Stream source, long? contentLength, CancellationToken ct);
    Task<LmsDownload> DownloadAsync(Guid id, CancellationToken ct);
}

public sealed record LmsDownload(Stream Body, string ContentType, string Name, DateTimeOffset CreatedUtc);
