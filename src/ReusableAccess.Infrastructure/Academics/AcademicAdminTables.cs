using Microsoft.EntityFrameworkCore;
using ReusableAccess.Application.Academics;
using ReusableAccess.Contracts;

namespace ReusableAccess.Infrastructure.Academics;

internal static class AcademicPaging
{
    internal static async Task<(int Page, int Size, int Total)> Bounds<T>(IQueryable<T> query, LmsTablePage input, CancellationToken ct)
    {
        if (input.Page < 1 || !LmsTablePage.Sizes.Contains(input.Size)) throw LmsError.Invalid("صفحه یا تعداد ردیف معتبر نیست.");
        var count = await query.CountAsync(ct);
        var last = Math.Max(1, (int)((count + (long)input.Size - 1) / input.Size));
        return (Math.Min(input.Page, last), input.Size, count);
    }
}

internal sealed class AcademicAdminTables(LmsOfferings offerings, LmsPeople people) : ILmsAdminTableQueries
{
    public Task<LmsTableSlice<OfferingRow>> Offerings(OfferingFilter filter, LmsTablePage page, CancellationToken ct) => offerings.AdminPage(filter, page, ct);
    public Task<LmsTableSlice<PeopleRow>> People(string kind, Guid? scope, string? search, LmsTablePage page, CancellationToken ct) =>
        people.Page(kind, scope, search, page, ct);
}
