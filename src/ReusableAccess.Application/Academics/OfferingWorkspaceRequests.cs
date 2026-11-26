using MediatR;
using ReusableAccess.Application.Errors;
using ReusableAccess.Contracts;

namespace ReusableAccess.Application.Academics;

public interface IAssignmentQueries
{
    Task<AssignmentPanel> ListAsync(Guid offeringId, string view, CancellationToken ct);
    Task<AssessmentRow> DetailAsync(Guid offeringId, Guid assessmentId, string view, CancellationToken ct);
    Task<AssignmentQueueRow[]> QueueAsync(CancellationToken ct);
    Task<AssignmentTemplateRow[]> TemplatesAsync(string? search, int page, CancellationToken ct);
}

public interface IAssignmentCommands
{
    Task<Guid> SaveAsync(Guid offeringId, AssessmentEdit input, CancellationToken ct);
    Task<bool> PublishAsync(Guid offeringId, Guid assessmentId, PublishAssessmentInput input, CancellationToken ct);
    Task<Guid> ExtendAsync(Guid offeringId, Guid assessmentId, ExtendAssignmentInput input, CancellationToken ct);
    Task<Guid> SaveTemplateAsync(AssignmentTemplateInput input, CancellationToken ct);
}

public interface ICourseDiscussionQueries
{
    Task<CourseDiscussionPage> ListAsync(Guid offeringId, string view, int page, CancellationToken ct);
}

public interface ICourseDiscussionCommands
{
    Task<Guid> PostAsync(Guid offeringId, string view, CourseDiscussionPost input, CancellationToken ct);
}

public sealed record GetOfferingAssignmentsQuery(Guid OfferingId, string View) : IRequest<AssignmentPanel>;
public sealed record GetOfferingAssignmentQuery(Guid OfferingId, Guid AssessmentId, string View) : IRequest<AssessmentRow>;
public sealed record GetAssignmentQueueQuery() : IRequest<AssignmentQueueRow[]>;
public sealed record GetAssignmentTemplatesQuery(string? Search, int Page) : IRequest<AssignmentTemplateRow[]>;
public sealed record SaveOfferingAssignmentCommand(Guid OfferingId, AssessmentEdit Input) : IRequest<Guid>, IConcurrencyConflictRequest;
public sealed record PublishOfferingAssignmentCommand(Guid OfferingId, Guid AssessmentId, PublishAssessmentInput Input) : IRequest<bool>, IConcurrencyConflictRequest;
public sealed record ExtendOfferingAssignmentCommand(Guid OfferingId, Guid AssessmentId, ExtendAssignmentInput Input) : IRequest<Guid>, IConcurrencyConflictRequest;
public sealed record SaveAssignmentTemplateCommand(AssignmentTemplateInput Input) : IRequest<Guid>, IConcurrencyConflictRequest;
public sealed record GetOfferingDiscussionQuery(Guid OfferingId, string View, int Page) : IRequest<CourseDiscussionPage>;
public sealed record PostOfferingDiscussionCommand(Guid OfferingId, string View, CourseDiscussionPost Input) : IRequest<Guid>, IConcurrencyConflictRequest;

public sealed class GetOfferingAssignmentsHandler(IAssignmentQueries feature) : IRequestHandler<GetOfferingAssignmentsQuery, AssignmentPanel>
{
    public Task<AssignmentPanel> Handle(GetOfferingAssignmentsQuery request, CancellationToken ct)
        => feature.ListAsync(request.OfferingId, request.View, ct);
}
public sealed class GetOfferingAssignmentHandler(IAssignmentQueries feature) : IRequestHandler<GetOfferingAssignmentQuery, AssessmentRow>
{
    public Task<AssessmentRow> Handle(GetOfferingAssignmentQuery request, CancellationToken ct)
        => feature.DetailAsync(request.OfferingId, request.AssessmentId, request.View, ct);
}
public sealed class GetAssignmentTemplatesHandler(IAssignmentQueries feature) : IRequestHandler<GetAssignmentTemplatesQuery, AssignmentTemplateRow[]>
{
    public Task<AssignmentTemplateRow[]> Handle(GetAssignmentTemplatesQuery request, CancellationToken ct)
        => feature.TemplatesAsync(request.Search, request.Page, ct);
}
public sealed class SaveOfferingAssignmentHandler(IAssignmentCommands feature) : IRequestHandler<SaveOfferingAssignmentCommand, Guid>
{
    public Task<Guid> Handle(SaveOfferingAssignmentCommand request, CancellationToken ct)
        => feature.SaveAsync(request.OfferingId, request.Input, ct);
}
public sealed class PublishOfferingAssignmentHandler(IAssignmentCommands feature) : IRequestHandler<PublishOfferingAssignmentCommand, bool>
{
    public Task<bool> Handle(PublishOfferingAssignmentCommand request, CancellationToken ct)
        => feature.PublishAsync(request.OfferingId, request.AssessmentId, request.Input, ct);
}
public sealed class ExtendOfferingAssignmentHandler(IAssignmentCommands feature) : IRequestHandler<ExtendOfferingAssignmentCommand, Guid>
{
    public Task<Guid> Handle(ExtendOfferingAssignmentCommand request, CancellationToken ct)
        => feature.ExtendAsync(request.OfferingId, request.AssessmentId, request.Input, ct);
}
public sealed class SaveAssignmentTemplateHandler(IAssignmentCommands feature) : IRequestHandler<SaveAssignmentTemplateCommand, Guid>
{
    public Task<Guid> Handle(SaveAssignmentTemplateCommand request, CancellationToken ct)
        => feature.SaveTemplateAsync(request.Input, ct);
}
public sealed class GetOfferingDiscussionHandler(ICourseDiscussionQueries feature) : IRequestHandler<GetOfferingDiscussionQuery, CourseDiscussionPage>
{
    public Task<CourseDiscussionPage> Handle(GetOfferingDiscussionQuery request, CancellationToken ct)
        => feature.ListAsync(request.OfferingId, request.View, request.Page, ct);
}
public sealed class PostOfferingDiscussionHandler(ICourseDiscussionCommands feature) : IRequestHandler<PostOfferingDiscussionCommand, Guid>
{
    public Task<Guid> Handle(PostOfferingDiscussionCommand request, CancellationToken ct)
        => feature.PostAsync(request.OfferingId, request.View, request.Input, ct);
}

public sealed class GetAssignmentQueueHandler(IAssignmentQueries feature) : IRequestHandler<GetAssignmentQueueQuery, AssignmentQueueRow[]>
{
    public Task<AssignmentQueueRow[]> Handle(GetAssignmentQueueQuery request, CancellationToken ct) => feature.QueueAsync(ct);
}
