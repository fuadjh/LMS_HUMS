using MediatR;
using ReusableAccess.Application.Errors;
using ReusableAccess.Contracts;

namespace ReusableAccess.Application.Academics;

public interface ILmsOfferings
{
    Task<OfferingRow[]> ListAsync(OfferingFilter filter, CancellationToken ct);
    Task<OfferingDetails> GetAsync(Guid offeringId, string view, CancellationToken ct);
    Task<Guid> SaveAsync(OfferingEdit input, CancellationToken ct);
    Task<Guid> CoTeachAsync(Guid offeringId, CoTeacherEdit input, CancellationToken ct);
    Task<bool> ScheduleAsync(Guid offeringId, ScheduleEdit input, CancellationToken ct);
    Task<bool> PublishAsync(Guid offeringId, CancellationToken ct);
    Task<Guid> SaveSectionAsync(Guid offeringId, string kind, SectionEdit input, CancellationToken ct);
    Task<bool> PublishChapterAsync(Guid offeringId, Guid chapterId, CancellationToken ct);
    Task<TemplateRow[]> TemplatesAsync(CancellationToken ct);
    Task<Guid> SaveTemplateAsync(Guid offeringId, TemplateSave input, CancellationToken ct);
    Task<bool> ApplyTemplateAsync(Guid offeringId, TemplateApply input, CancellationToken ct);
}

public sealed record GetOfferingsQuery(OfferingFilter Filter) : IRequest<OfferingRow[]>;
public sealed record GetOfferingQuery(Guid OfferingId, string View) : IRequest<OfferingDetails>;
public sealed record SaveOfferingCommand(OfferingEdit Input) : IRequest<Guid>, IConcurrencyConflictRequest;
public sealed record SetCoTeacherCommand(Guid OfferingId, CoTeacherEdit Input) : IRequest<Guid>, IConcurrencyConflictRequest;
public sealed record ScheduleOfferingCommand(Guid OfferingId, ScheduleEdit Input) : IRequest<bool>, IConcurrencyConflictRequest;
public sealed record PublishOfferingCommand(Guid OfferingId) : IRequest<bool>, IConcurrencyConflictRequest;
public sealed record SaveSectionCommand(Guid OfferingId, string Kind, SectionEdit Input) : IRequest<Guid>, IConcurrencyConflictRequest;
public sealed record PublishChapterCommand(Guid OfferingId, Guid ChapterId) : IRequest<bool>, IConcurrencyConflictRequest;
public sealed record GetTemplatesQuery() : IRequest<TemplateRow[]>;
public sealed record SaveTemplateCommand(Guid OfferingId, TemplateSave Input) : IRequest<Guid>, IConcurrencyConflictRequest;
public sealed record ApplyTemplateCommand(Guid OfferingId, TemplateApply Input) : IRequest<bool>, IConcurrencyConflictRequest;

public sealed class GetOfferingsQueryHandler(ILmsOfferings feature) : IRequestHandler<GetOfferingsQuery, OfferingRow[]>
{
    public Task<OfferingRow[]> Handle(GetOfferingsQuery request, CancellationToken ct)
        => feature.ListAsync(request.Filter, ct);
}

public sealed class GetOfferingQueryHandler(ILmsOfferings feature) : IRequestHandler<GetOfferingQuery, OfferingDetails>
{
    public Task<OfferingDetails> Handle(GetOfferingQuery request, CancellationToken ct)
        => feature.GetAsync(request.OfferingId, request.View, ct);
}

public sealed class SaveOfferingCommandHandler(ILmsOfferings feature) : IRequestHandler<SaveOfferingCommand, Guid>
{
    public Task<Guid> Handle(SaveOfferingCommand request, CancellationToken ct)
        => feature.SaveAsync(request.Input, ct);
}

public sealed class SetCoTeacherCommandHandler(ILmsOfferings feature) : IRequestHandler<SetCoTeacherCommand, Guid>
{
    public Task<Guid> Handle(SetCoTeacherCommand request, CancellationToken ct)
        => feature.CoTeachAsync(request.OfferingId, request.Input, ct);
}

public sealed class ScheduleOfferingCommandHandler(ILmsOfferings feature) : IRequestHandler<ScheduleOfferingCommand, bool>
{
    public Task<bool> Handle(ScheduleOfferingCommand request, CancellationToken ct)
        => feature.ScheduleAsync(request.OfferingId, request.Input, ct);
}

public sealed class PublishOfferingCommandHandler(ILmsOfferings feature) : IRequestHandler<PublishOfferingCommand, bool>
{
    public Task<bool> Handle(PublishOfferingCommand request, CancellationToken ct)
        => feature.PublishAsync(request.OfferingId, ct);
}

public sealed class SaveSectionCommandHandler(ILmsOfferings feature) : IRequestHandler<SaveSectionCommand, Guid>
{
    public Task<Guid> Handle(SaveSectionCommand request, CancellationToken ct)
        => feature.SaveSectionAsync(request.OfferingId, request.Kind, request.Input, ct);
}

public sealed class PublishChapterCommandHandler(ILmsOfferings feature) : IRequestHandler<PublishChapterCommand, bool>
{
    public Task<bool> Handle(PublishChapterCommand request, CancellationToken ct)
        => feature.PublishChapterAsync(request.OfferingId, request.ChapterId, ct);
}

public sealed class GetTemplatesQueryHandler(ILmsOfferings feature) : IRequestHandler<GetTemplatesQuery, TemplateRow[]>
{
    public Task<TemplateRow[]> Handle(GetTemplatesQuery request, CancellationToken ct)
        => feature.TemplatesAsync(ct);
}

public sealed class SaveTemplateCommandHandler(ILmsOfferings feature) : IRequestHandler<SaveTemplateCommand, Guid>
{
    public Task<Guid> Handle(SaveTemplateCommand request, CancellationToken ct)
        => feature.SaveTemplateAsync(request.OfferingId, request.Input, ct);
}

public sealed class ApplyTemplateCommandHandler(ILmsOfferings feature) : IRequestHandler<ApplyTemplateCommand, bool>
{
    public Task<bool> Handle(ApplyTemplateCommand request, CancellationToken ct)
        => feature.ApplyTemplateAsync(request.OfferingId, request.Input, ct);
}
