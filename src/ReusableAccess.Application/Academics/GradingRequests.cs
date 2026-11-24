using MediatR;
using ReusableAccess.Application.Errors;
using ReusableAccess.Contracts;

namespace ReusableAccess.Application.Academics;

public interface ILmsGrading
{
    Task<Guid> SubmitAsync(Guid assessmentId, AttemptInput input, CancellationToken ct);
    Task<AttemptRow[]> WorkAsync(Guid assessmentId, string view, CancellationToken ct);
    Task<Guid> GradeAsync(Guid submissionId, GradeEdit input, CancellationToken ct);
    Task<Guid> FinalAsync(Guid enrollmentId, GradeEdit input, CancellationToken ct);
    Task<GradeRow[]> FinalsAsync(Guid enrollmentId, CancellationToken ct);
}

public sealed record SubmitAssessmentCommand(Guid AssessmentId, AttemptInput Input) : IRequest<Guid>, IConcurrencyConflictRequest;
public sealed record GetAssessmentWorkQuery(Guid AssessmentId, string View) : IRequest<AttemptRow[]>;
public sealed record SaveAssessmentGradeCommand(Guid SubmissionId, GradeEdit Input) : IRequest<Guid>, IConcurrencyConflictRequest;
public sealed record SaveFinalGradeCommand(Guid EnrollmentId, GradeEdit Input) : IRequest<Guid>, IConcurrencyConflictRequest;
public sealed record GetFinalGradesQuery(Guid EnrollmentId) : IRequest<GradeRow[]>;

public sealed class SubmitAssessmentCommandHandler(ILmsGrading feature) : IRequestHandler<SubmitAssessmentCommand, Guid>
{
    public Task<Guid> Handle(SubmitAssessmentCommand request, CancellationToken ct)
        => feature.SubmitAsync(request.AssessmentId, request.Input, ct);
}

public sealed class GetAssessmentWorkQueryHandler(ILmsGrading feature) : IRequestHandler<GetAssessmentWorkQuery, AttemptRow[]>
{
    public Task<AttemptRow[]> Handle(GetAssessmentWorkQuery request, CancellationToken ct)
        => feature.WorkAsync(request.AssessmentId, request.View, ct);
}

public sealed class SaveAssessmentGradeCommandHandler(ILmsGrading feature) : IRequestHandler<SaveAssessmentGradeCommand, Guid>
{
    public Task<Guid> Handle(SaveAssessmentGradeCommand request, CancellationToken ct)
        => feature.GradeAsync(request.SubmissionId, request.Input, ct);
}

public sealed class SaveFinalGradeCommandHandler(ILmsGrading feature) : IRequestHandler<SaveFinalGradeCommand, Guid>
{
    public Task<Guid> Handle(SaveFinalGradeCommand request, CancellationToken ct)
        => feature.FinalAsync(request.EnrollmentId, request.Input, ct);
}

public sealed class GetFinalGradesQueryHandler(ILmsGrading feature) : IRequestHandler<GetFinalGradesQuery, GradeRow[]>
{
    public Task<GradeRow[]> Handle(GetFinalGradesQuery request, CancellationToken ct)
        => feature.FinalsAsync(request.EnrollmentId, ct);
}
