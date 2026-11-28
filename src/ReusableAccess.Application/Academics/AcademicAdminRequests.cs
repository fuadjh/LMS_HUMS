using MediatR;
using ReusableAccess.Application.Errors;
using ReusableAccess.Contracts;

namespace ReusableAccess.Application.Academics;

public interface ILmsAdminTableQueries
{
    Task<LmsTableSlice<OfferingRow>> Offerings(OfferingFilter filter, LmsTablePage page, CancellationToken ct);
    Task<LmsTableSlice<PeopleRow>> People(string kind, Guid? scope, string? search, LmsTablePage page, CancellationToken ct);
}
public interface IAcademicWorkbookQueries
{
    Task<AcademicWorkbookRow[]> Preview(string kind, AcademicWorkbookInput input, CancellationToken ct);
}
public interface IAcademicWorkbookCommands
{
    Task<AcademicWorkbookRow[]> Apply(string kind, AcademicWorkbookInput input, CancellationToken ct);
}
public sealed record ReadAdminOfferingsQuery(OfferingFilter Filter, LmsTablePage Page) : IRequest<LmsTableSlice<OfferingRow>>;
public sealed record ReadAdminPeopleQuery(string Kind, Guid? Scope, string? Search, LmsTablePage Page) : IRequest<LmsTableSlice<PeopleRow>>;
public sealed record PreviewAcademicWorkbookQuery(string Kind, AcademicWorkbookInput Input) : IRequest<AcademicWorkbookRow[]>;
public sealed record ApplyAcademicWorkbookCommand(string Kind, AcademicWorkbookInput Input) : IRequest<AcademicWorkbookRow[]>, IConcurrencyConflictRequest;

public sealed class AcademicAdminQueryHandlers(ILmsAdminTableQueries tables, IAcademicWorkbookQueries workbook) :
    IRequestHandler<ReadAdminOfferingsQuery, LmsTableSlice<OfferingRow>>, IRequestHandler<ReadAdminPeopleQuery, LmsTableSlice<PeopleRow>>,
    IRequestHandler<PreviewAcademicWorkbookQuery, AcademicWorkbookRow[]>
{
    public Task<LmsTableSlice<OfferingRow>> Handle(ReadAdminOfferingsQuery r, CancellationToken ct) => tables.Offerings(r.Filter, r.Page, ct);
    public Task<LmsTableSlice<PeopleRow>> Handle(ReadAdminPeopleQuery r, CancellationToken ct) => tables.People(r.Kind, r.Scope, r.Search, r.Page, ct);
    public Task<AcademicWorkbookRow[]> Handle(PreviewAcademicWorkbookQuery r, CancellationToken ct) => workbook.Preview(r.Kind, r.Input, ct);
}
public sealed class ApplyAcademicWorkbookHandler(IAcademicWorkbookCommands workbook) : IRequestHandler<ApplyAcademicWorkbookCommand, AcademicWorkbookRow[]>
{
    public Task<AcademicWorkbookRow[]> Handle(ApplyAcademicWorkbookCommand r, CancellationToken ct) => workbook.Apply(r.Kind, r.Input, ct);
}
