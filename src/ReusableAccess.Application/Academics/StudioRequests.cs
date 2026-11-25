using MediatR;
using ReusableAccess.Application.Errors;
using ReusableAccess.Contracts;

namespace ReusableAccess.Application.Academics;

// The application knows only these read/write ports. Infrastructure owns transactions and expressions over EF.
public interface IStudioTemplateQueries
{
    Task<StudioBank> Bank(string? search, int page, CancellationToken ct);
    Task<CatalogRow[]> Courses(CancellationToken ct);
    Task<StudioVersionList> Versions(Guid templateId, int page, CancellationToken ct);
    Task<StudioTemplateDetails> Detail(Guid templateId, Guid? versionId, CancellationToken ct);
}

public interface IStudioTemplateCommands
{
    Task<Guid> Create(StudioCreateTemplate input, CancellationToken ct);
    Task<StudioVersionSaved> Save(Guid templateId, StudioSaveVersion input, CancellationToken ct);
    Task<bool> Archive(Guid templateId, StudioRevision input, CancellationToken ct);
}

public interface IStudioScheduleQueries
{
    Task<StudioTarget[]> Targets(Guid courseId, Guid? termId, CancellationToken ct);
    Task<StudioSource[]> Sources(Guid offeringId, Guid versionId, CancellationToken ct);
    Task<StudioSchedulePreview> Preview(Guid offeringId, StudioPlan input, CancellationToken ct);
    Task<StudioDraftResult> Draft(Guid offeringId, CancellationToken ct);
    Task<StudioReadSchedule> Read(Guid offeringId, CancellationToken ct);
}

public interface IStudioScheduleCommands
{
    Task<StudioDraftView> Save(Guid offeringId, StudioStoreDraft input, CancellationToken ct);
    Task<bool> Discard(Guid offeringId, StudioRevision input, CancellationToken ct);
    Task<StudioReceipt> Apply(Guid offeringId, StudioApplyDraft input, bool publish, CancellationToken ct);
}

public sealed record GetCourseTemplateBankQuery(string? Search, int Page) : IRequest<StudioBank>;
public sealed record GetTemplateCoursesQuery() : IRequest<CatalogRow[]>;
public sealed record GetCourseTemplateVersionsQuery(Guid TemplateId, int Page) : IRequest<StudioVersionList>;
public sealed record GetCourseTemplateVersionQuery(Guid TemplateId, Guid? VersionId) : IRequest<StudioTemplateDetails>;
public sealed record CreateCourseTemplateCommand(StudioCreateTemplate Input) : IRequest<Guid>, IConcurrencyConflictRequest;
public sealed record SaveCourseTemplateVersionCommand(Guid TemplateId, StudioSaveVersion Input) : IRequest<StudioVersionSaved>, IConcurrencyConflictRequest;
public sealed record ArchiveCourseTemplateCommand(Guid TemplateId, StudioRevision Input) : IRequest<bool>, IConcurrencyConflictRequest;

public sealed record GetTeachingScheduleTargetsQuery(Guid CourseId, Guid? TermId) : IRequest<StudioTarget[]>;
public sealed record GetScheduleOriginsQuery(Guid OfferingId, Guid VersionId) : IRequest<StudioSource[]>;
public sealed record PreviewOfferingScheduleQuery(Guid OfferingId, StudioPlan Input) : IRequest<StudioSchedulePreview>;
public sealed record GetOfferingScheduleDraftQuery(Guid OfferingId) : IRequest<StudioDraftResult>;
public sealed record GetOfferingScheduleReadModelQuery(Guid OfferingId) : IRequest<StudioReadSchedule>;
public sealed record SaveOfferingScheduleDraftCommand(Guid OfferingId, StudioStoreDraft Input) : IRequest<StudioDraftView>, IConcurrencyConflictRequest;
public sealed record DiscardOfferingScheduleDraftCommand(Guid OfferingId, StudioRevision Input) : IRequest<bool>, IConcurrencyConflictRequest;
public sealed record ApplyOfferingScheduleCommand(Guid OfferingId, StudioApplyDraft Input) : IRequest<StudioReceipt>, IConcurrencyConflictRequest;
public sealed record PublishScheduledOfferingCommand(Guid OfferingId, StudioApplyDraft Input) : IRequest<StudioReceipt>, IConcurrencyConflictRequest;

internal sealed class StudioTemplateQueryHandlers(IStudioTemplateQueries reads) :
    IRequestHandler<GetCourseTemplateBankQuery, StudioBank>, IRequestHandler<GetTemplateCoursesQuery, CatalogRow[]>,
    IRequestHandler<GetCourseTemplateVersionsQuery, StudioVersionList>, IRequestHandler<GetCourseTemplateVersionQuery, StudioTemplateDetails>
{
    public Task<StudioBank> Handle(GetCourseTemplateBankQuery q, CancellationToken ct) => reads.Bank(q.Search, q.Page, ct);
    public Task<CatalogRow[]> Handle(GetTemplateCoursesQuery q, CancellationToken ct) => reads.Courses(ct);
    public Task<StudioVersionList> Handle(GetCourseTemplateVersionsQuery q, CancellationToken ct) => reads.Versions(q.TemplateId, q.Page, ct);
    public Task<StudioTemplateDetails> Handle(GetCourseTemplateVersionQuery q, CancellationToken ct) => reads.Detail(q.TemplateId, q.VersionId, ct);
}

internal sealed class StudioTemplateCommandHandlers(IStudioTemplateCommands writes) :
    IRequestHandler<CreateCourseTemplateCommand, Guid>, IRequestHandler<SaveCourseTemplateVersionCommand, StudioVersionSaved>,
    IRequestHandler<ArchiveCourseTemplateCommand, bool>
{
    public Task<Guid> Handle(CreateCourseTemplateCommand c, CancellationToken ct) => writes.Create(c.Input, ct);
    public Task<StudioVersionSaved> Handle(SaveCourseTemplateVersionCommand c, CancellationToken ct) => writes.Save(c.TemplateId, c.Input, ct);
    public Task<bool> Handle(ArchiveCourseTemplateCommand c, CancellationToken ct) => writes.Archive(c.TemplateId, c.Input, ct);
}

internal sealed class StudioScheduleQueryHandlers(IStudioScheduleQueries reads) :
    IRequestHandler<GetTeachingScheduleTargetsQuery, StudioTarget[]>, IRequestHandler<GetScheduleOriginsQuery, StudioSource[]>,
    IRequestHandler<PreviewOfferingScheduleQuery, StudioSchedulePreview>, IRequestHandler<GetOfferingScheduleDraftQuery, StudioDraftResult>,
    IRequestHandler<GetOfferingScheduleReadModelQuery, StudioReadSchedule>
{
    public Task<StudioTarget[]> Handle(GetTeachingScheduleTargetsQuery q, CancellationToken ct) => reads.Targets(q.CourseId, q.TermId, ct);
    public Task<StudioSource[]> Handle(GetScheduleOriginsQuery q, CancellationToken ct) => reads.Sources(q.OfferingId, q.VersionId, ct);
    public Task<StudioSchedulePreview> Handle(PreviewOfferingScheduleQuery q, CancellationToken ct) => reads.Preview(q.OfferingId, q.Input, ct);
    public Task<StudioDraftResult> Handle(GetOfferingScheduleDraftQuery q, CancellationToken ct) => reads.Draft(q.OfferingId, ct);
    public Task<StudioReadSchedule> Handle(GetOfferingScheduleReadModelQuery q, CancellationToken ct) => reads.Read(q.OfferingId, ct);
}

internal sealed class StudioScheduleCommandHandlers(IStudioScheduleCommands writes) :
    IRequestHandler<SaveOfferingScheduleDraftCommand, StudioDraftView>, IRequestHandler<DiscardOfferingScheduleDraftCommand, bool>,
    IRequestHandler<ApplyOfferingScheduleCommand, StudioReceipt>, IRequestHandler<PublishScheduledOfferingCommand, StudioReceipt>
{
    public Task<StudioDraftView> Handle(SaveOfferingScheduleDraftCommand c, CancellationToken ct) => writes.Save(c.OfferingId, c.Input, ct);
    public Task<bool> Handle(DiscardOfferingScheduleDraftCommand c, CancellationToken ct) => writes.Discard(c.OfferingId, c.Input, ct);
    public Task<StudioReceipt> Handle(ApplyOfferingScheduleCommand c, CancellationToken ct) => writes.Apply(c.OfferingId, c.Input, false, ct);
    public Task<StudioReceipt> Handle(PublishScheduledOfferingCommand c, CancellationToken ct) => writes.Apply(c.OfferingId, c.Input, true, ct);
}
