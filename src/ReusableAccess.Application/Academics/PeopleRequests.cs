using MediatR;
using ReusableAccess.Application.Errors;
using ReusableAccess.Contracts;

namespace ReusableAccess.Application.Academics;

public interface ILmsPeople
{
    Task<PeopleRow[]> ListAsync(string kind, Guid? scopeId, string? search, CancellationToken ct);
    Task<ProbeResult> ProbeAsync(string kind, ProbeInput input, CancellationToken ct);
    Task<Guid> SaveAsync(string kind, ProfileEdit input, CancellationToken ct);
    Task<bool> SetActiveAsync(string kind, Guid userId, bool active, CancellationToken ct);
    Task<StudyRow[]> HistoryAsync(Guid studentId, CancellationToken ct);
    Task<ImportRow[]> ImportAsync(string kind, Guid mediaId, Guid scopeId, bool apply, CancellationToken ct);
}

public sealed record GetPeopleQuery(string Kind, Guid? ScopeId, string? Search) : IRequest<PeopleRow[]>;
public sealed record ProbeProfileQuery(string Kind, ProbeInput Input) : IRequest<ProbeResult>;
public sealed record SaveProfileCommand(string Kind, ProfileEdit Input) : IRequest<Guid>, IConcurrencyConflictRequest;
public sealed record SetProfileActiveCommand(string Kind, Guid UserId, bool Active) : IRequest<bool>, IConcurrencyConflictRequest;
public sealed record GetAcademicHistoryQuery(Guid StudentId) : IRequest<StudyRow[]>;
public sealed record ImportProfilesCommand(string Kind, Guid MediaId, Guid ScopeId, bool Apply) : IRequest<ImportRow[]>, IConcurrencyConflictRequest;

public sealed class GetPeopleQueryHandler(ILmsPeople feature) : IRequestHandler<GetPeopleQuery, PeopleRow[]>
{
    public Task<PeopleRow[]> Handle(GetPeopleQuery request, CancellationToken ct)
        => feature.ListAsync(request.Kind, request.ScopeId, request.Search, ct);
}

public sealed class ProbeProfileQueryHandler(ILmsPeople feature) : IRequestHandler<ProbeProfileQuery, ProbeResult>
{
    public Task<ProbeResult> Handle(ProbeProfileQuery request, CancellationToken ct)
        => feature.ProbeAsync(request.Kind, request.Input, ct);
}

public sealed class SaveProfileCommandHandler(ILmsPeople feature) : IRequestHandler<SaveProfileCommand, Guid>
{
    public Task<Guid> Handle(SaveProfileCommand request, CancellationToken ct)
        => feature.SaveAsync(request.Kind, request.Input, ct);
}

public sealed class SetProfileActiveCommandHandler(ILmsPeople feature) : IRequestHandler<SetProfileActiveCommand, bool>
{
    public Task<bool> Handle(SetProfileActiveCommand request, CancellationToken ct)
        => feature.SetActiveAsync(request.Kind, request.UserId, request.Active, ct);
}

public sealed class GetAcademicHistoryQueryHandler(ILmsPeople feature) : IRequestHandler<GetAcademicHistoryQuery, StudyRow[]>
{
    public Task<StudyRow[]> Handle(GetAcademicHistoryQuery request, CancellationToken ct)
        => feature.HistoryAsync(request.StudentId, ct);
}

public sealed class ImportProfilesCommandHandler(ILmsPeople feature) : IRequestHandler<ImportProfilesCommand, ImportRow[]>
{
    public Task<ImportRow[]> Handle(ImportProfilesCommand request, CancellationToken ct)
        => feature.ImportAsync(request.Kind, request.MediaId, request.ScopeId, request.Apply, ct);
}
