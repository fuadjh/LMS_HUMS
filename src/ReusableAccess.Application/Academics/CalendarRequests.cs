using MediatR;
using ReusableAccess.Application.Errors;
using ReusableAccess.Contracts;

namespace ReusableAccess.Application.Academics;

public interface ILmsCalendar
{
    Task<CalendarEntry[]> CalendarAsync(string view, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken ct);
    Task<NoticeRow[]> NoticesAsync(CancellationToken ct);
    Task<bool> ReadAsync(Guid noticeId, CancellationToken ct);
    Task<ChatRoom> ChatAsync(Guid? recipientId, DateTimeOffset? beforeUtc, CancellationToken ct);
    Task<Guid> SendAsync(ChatPost input, CancellationToken ct);
}

public sealed record GetLmsCalendarQuery(string View, DateTimeOffset FromUtc, DateTimeOffset ToUtc) : IRequest<CalendarEntry[]>;
public sealed record GetLmsNoticesQuery() : IRequest<NoticeRow[]>;
public sealed record ReadLmsNoticeCommand(Guid NoticeId) : IRequest<bool>, IConcurrencyConflictRequest;
public sealed record GetCohortChatQuery(Guid? RecipientId, DateTimeOffset? BeforeUtc) : IRequest<ChatRoom>;
public sealed record SendCohortMessageCommand(ChatPost Input) : IRequest<Guid>, IConcurrencyConflictRequest;

public sealed class GetLmsCalendarQueryHandler(ILmsCalendar feature) : IRequestHandler<GetLmsCalendarQuery, CalendarEntry[]>
{
    public Task<CalendarEntry[]> Handle(GetLmsCalendarQuery request, CancellationToken ct)
        => feature.CalendarAsync(request.View, request.FromUtc, request.ToUtc, ct);
}

public sealed class GetLmsNoticesQueryHandler(ILmsCalendar feature) : IRequestHandler<GetLmsNoticesQuery, NoticeRow[]>
{
    public Task<NoticeRow[]> Handle(GetLmsNoticesQuery request, CancellationToken ct)
        => feature.NoticesAsync(ct);
}

public sealed class ReadLmsNoticeCommandHandler(ILmsCalendar feature) : IRequestHandler<ReadLmsNoticeCommand, bool>
{
    public Task<bool> Handle(ReadLmsNoticeCommand request, CancellationToken ct)
        => feature.ReadAsync(request.NoticeId, ct);
}

public sealed class GetCohortChatQueryHandler(ILmsCalendar feature) : IRequestHandler<GetCohortChatQuery, ChatRoom>
{
    public Task<ChatRoom> Handle(GetCohortChatQuery request, CancellationToken ct)
        => feature.ChatAsync(request.RecipientId, request.BeforeUtc, ct);
}

public sealed class SendCohortMessageCommandHandler(ILmsCalendar feature) : IRequestHandler<SendCohortMessageCommand, Guid>
{
    public Task<Guid> Handle(SendCohortMessageCommand request, CancellationToken ct)
        => feature.SendAsync(request.Input, ct);
}
