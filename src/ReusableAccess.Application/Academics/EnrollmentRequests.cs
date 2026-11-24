using MediatR;
using ReusableAccess.Application.Errors;
using ReusableAccess.Contracts;

namespace ReusableAccess.Application.Academics;

public interface ILmsEnrollment
{
    Task<EnrollmentChange> JoinAsync(Guid offeringId, CancellationToken ct);
    Task<EnrollmentChange> WithdrawAsync(Guid enrollmentId, CancellationToken ct);
    Task<EnrollmentChange> RemoveAsync(Guid enrollmentId, RemoveEnrollmentInput input, CancellationToken ct);
    Task<RosterRow[]> RosterAsync(Guid offeringId, string view, CancellationToken ct);
    Task<bool> ProgressAsync(Guid lessonId, CancellationToken ct);
    Task<TranscriptRow[]> TranscriptAsync(CancellationToken ct);
    Task<EnrollmentHistoryRow[]> HistoryAsync(Guid enrollmentId, string view, CancellationToken ct);
}

public sealed record JoinOfferingCommand(Guid OfferingId) : IRequest<EnrollmentChange>, IConcurrencyConflictRequest;
public sealed record WithdrawMyEnrollmentCommand(Guid EnrollmentId) : IRequest<EnrollmentChange>, IConcurrencyConflictRequest;
public sealed record RemoveEnrollmentAsAdminCommand(Guid EnrollmentId, RemoveEnrollmentInput Input) : IRequest<EnrollmentChange>, IConcurrencyConflictRequest;
public sealed record GetOfferingRosterQuery(Guid OfferingId, string View) : IRequest<RosterRow[]>;
public sealed record MarkLessonDoneCommand(Guid LessonId) : IRequest<bool>, IConcurrencyConflictRequest;
public sealed record GetTranscriptQuery() : IRequest<TranscriptRow[]>;

public sealed class JoinOfferingCommandHandler(ILmsEnrollment feature) : IRequestHandler<JoinOfferingCommand, EnrollmentChange>
{
    public Task<EnrollmentChange> Handle(JoinOfferingCommand request, CancellationToken ct)
        => feature.JoinAsync(request.OfferingId, ct);
}

public sealed class WithdrawMyEnrollmentCommandHandler(ILmsEnrollment feature) : IRequestHandler<WithdrawMyEnrollmentCommand, EnrollmentChange>
{
    public Task<EnrollmentChange> Handle(WithdrawMyEnrollmentCommand request, CancellationToken ct)
        => feature.WithdrawAsync(request.EnrollmentId, ct);
}

public sealed class RemoveEnrollmentAsAdminCommandHandler(ILmsEnrollment feature) : IRequestHandler<RemoveEnrollmentAsAdminCommand, EnrollmentChange>
{
    public Task<EnrollmentChange> Handle(RemoveEnrollmentAsAdminCommand request, CancellationToken ct)
        => feature.RemoveAsync(request.EnrollmentId, request.Input, ct);
}

public sealed class GetOfferingRosterQueryHandler(ILmsEnrollment feature) : IRequestHandler<GetOfferingRosterQuery, RosterRow[]>
{
    public Task<RosterRow[]> Handle(GetOfferingRosterQuery request, CancellationToken ct)
        => feature.RosterAsync(request.OfferingId, request.View, ct);
}

public sealed class MarkLessonDoneCommandHandler(ILmsEnrollment feature) : IRequestHandler<MarkLessonDoneCommand, bool>
{
    public Task<bool> Handle(MarkLessonDoneCommand request, CancellationToken ct)
        => feature.ProgressAsync(request.LessonId, ct);
}

public sealed class GetTranscriptQueryHandler(ILmsEnrollment feature) : IRequestHandler<GetTranscriptQuery, TranscriptRow[]>
{
    public Task<TranscriptRow[]> Handle(GetTranscriptQuery request, CancellationToken ct)
        => feature.TranscriptAsync(ct);
}

public sealed record GetEnrollmentHistoryQuery(Guid EnrollmentId, string View) : IRequest<EnrollmentHistoryRow[]>;
public sealed class GetEnrollmentHistoryQueryHandler(ILmsEnrollment feature) : IRequestHandler<GetEnrollmentHistoryQuery, EnrollmentHistoryRow[]>
{
    public Task<EnrollmentHistoryRow[]> Handle(GetEnrollmentHistoryQuery request, CancellationToken ct)
        => feature.HistoryAsync(request.EnrollmentId, request.View, ct);
}
