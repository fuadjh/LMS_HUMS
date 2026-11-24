using MediatR;
using ReusableAccess.Application.Errors;
using ReusableAccess.Contracts;

namespace ReusableAccess.Application.Academics;

public interface ILmsOperations
{
    Task<UploadTicket> StartAsync(UploadStart input, CancellationToken ct);
    Task<UploadStatusRow> UploadAsync(Guid id, CancellationToken ct);
    Task<LogPage<UploadStatusRow>> UploadsAsync(LogFilter filter, CancellationToken ct);
    Task<LogPage<SecurityRow>> SecurityAsync(LogFilter filter, CancellationToken ct);
    Task<SmsView> SmsAsync(CancellationToken ct);
    Task<bool> SaveSmsAsync(SmsEdit input, CancellationToken ct);
}

public sealed record StartLmsUploadCommand(UploadStart Input) : IRequest<UploadTicket>, IConcurrencyConflictRequest;
public sealed record GetLmsUploadQuery(Guid Id) : IRequest<UploadStatusRow>;
public sealed record GetUploadLogsQuery(LogFilter Filter) : IRequest<LogPage<UploadStatusRow>>;
public sealed record GetSecurityLogsQuery(LogFilter Filter) : IRequest<LogPage<SecurityRow>>;
public sealed record GetLmsSmsSettingsQuery() : IRequest<SmsView>;
public sealed record SaveLmsSmsSettingsCommand(SmsEdit Input) : IRequest<bool>, IConcurrencyConflictRequest;

public sealed class StartLmsUploadCommandHandler(ILmsOperations feature) : IRequestHandler<StartLmsUploadCommand, UploadTicket>
{
    public Task<UploadTicket> Handle(StartLmsUploadCommand request, CancellationToken ct)
        => feature.StartAsync(request.Input, ct);
}

public sealed class GetLmsUploadQueryHandler(ILmsOperations feature) : IRequestHandler<GetLmsUploadQuery, UploadStatusRow>
{
    public Task<UploadStatusRow> Handle(GetLmsUploadQuery request, CancellationToken ct)
        => feature.UploadAsync(request.Id, ct);
}

public sealed class GetUploadLogsQueryHandler(ILmsOperations feature) : IRequestHandler<GetUploadLogsQuery, LogPage<UploadStatusRow>>
{
    public Task<LogPage<UploadStatusRow>> Handle(GetUploadLogsQuery request, CancellationToken ct)
        => feature.UploadsAsync(request.Filter, ct);
}

public sealed class GetSecurityLogsQueryHandler(ILmsOperations feature) : IRequestHandler<GetSecurityLogsQuery, LogPage<SecurityRow>>
{
    public Task<LogPage<SecurityRow>> Handle(GetSecurityLogsQuery request, CancellationToken ct)
        => feature.SecurityAsync(request.Filter, ct);
}

public sealed class GetLmsSmsSettingsQueryHandler(ILmsOperations feature) : IRequestHandler<GetLmsSmsSettingsQuery, SmsView>
{
    public Task<SmsView> Handle(GetLmsSmsSettingsQuery request, CancellationToken ct)
        => feature.SmsAsync(ct);
}

public sealed class SaveLmsSmsSettingsCommandHandler(ILmsOperations feature) : IRequestHandler<SaveLmsSmsSettingsCommand, bool>
{
    public Task<bool> Handle(SaveLmsSmsSettingsCommand request, CancellationToken ct)
        => feature.SaveSmsAsync(request.Input, ct);
}
