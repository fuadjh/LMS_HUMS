using MediatR;
using ReusableAccess.Application.Errors;
using ReusableAccess.Contracts;

namespace ReusableAccess.Application.Academics;

public interface ILmsCatalog
{
    Task<LmsHome> HomeAsync(CancellationToken ct);
    Task<CatalogRow[]> ListAsync(string kind, CancellationToken ct);
    Task<Guid> SaveAsync(string kind, CatalogEdit input, CancellationToken ct);
    Task<ScopeGrantRow[]> GrantsAsync(CancellationToken ct);
    Task<Guid> GrantAsync(ScopeGrantEdit input, CancellationToken ct);
}

public sealed record GetLmsHomeQuery() : IRequest<LmsHome>;
public sealed record GetCatalogQuery(string Kind) : IRequest<CatalogRow[]>;
public sealed record SaveCatalogCommand(string Kind, CatalogEdit Input) : IRequest<Guid>, IConcurrencyConflictRequest;
public sealed record GetAcademicGrantsQuery() : IRequest<ScopeGrantRow[]>;
public sealed record SetAcademicGrantCommand(ScopeGrantEdit Input) : IRequest<Guid>, IConcurrencyConflictRequest;

public sealed class GetLmsHomeQueryHandler(ILmsCatalog feature) : IRequestHandler<GetLmsHomeQuery, LmsHome>
{
    public Task<LmsHome> Handle(GetLmsHomeQuery request, CancellationToken ct)
        => feature.HomeAsync(ct);
}

public sealed class GetCatalogQueryHandler(ILmsCatalog feature) : IRequestHandler<GetCatalogQuery, CatalogRow[]>
{
    public Task<CatalogRow[]> Handle(GetCatalogQuery request, CancellationToken ct)
        => feature.ListAsync(request.Kind, ct);
}

public sealed class SaveCatalogCommandHandler(ILmsCatalog feature) : IRequestHandler<SaveCatalogCommand, Guid>
{
    public Task<Guid> Handle(SaveCatalogCommand request, CancellationToken ct)
        => feature.SaveAsync(request.Kind, request.Input, ct);
}

public sealed class GetAcademicGrantsQueryHandler(ILmsCatalog feature) : IRequestHandler<GetAcademicGrantsQuery, ScopeGrantRow[]>
{
    public Task<ScopeGrantRow[]> Handle(GetAcademicGrantsQuery request, CancellationToken ct)
        => feature.GrantsAsync(ct);
}

public sealed class SetAcademicGrantCommandHandler(ILmsCatalog feature) : IRequestHandler<SetAcademicGrantCommand, Guid>
{
    public Task<Guid> Handle(SetAcademicGrantCommand request, CancellationToken ct)
        => feature.GrantAsync(request.Input, ct);
}
