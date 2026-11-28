using Microsoft.EntityFrameworkCore;
using ReusableAccess.Application.Academics;
using ReusableAccess.Application.Errors;
using ReusableAccess.Contracts;
using ReusableAccess.Domain.Academics;
using ReusableAccess.Infrastructure.Identity;

namespace ReusableAccess.Infrastructure.Academics;

internal sealed class AcademicWorkbook(LmsData data, LmsAccess access, LmsFiles files, LmsCatalog catalog, LmsOfferings offerings)
    : IAcademicWorkbookQueries, IAcademicWorkbookCommands
{
    internal static readonly string[] CourseColumns = ["course_code", "title"];
    internal static readonly string[] OfferingColumns = ["course_code", "group_code", "instructor_code"];
    public Task<AcademicWorkbookRow[]> Preview(string kind, AcademicWorkbookInput input, CancellationToken ct) => Process(kind, input, false, ct);
    public Task<AcademicWorkbookRow[]> Apply(string kind, AcademicWorkbookInput input, CancellationToken ct) => Process(kind, input, true, ct);

    private async Task<AcademicWorkbookRow[]> Process(string kind, AcademicWorkbookInput input, bool apply, CancellationToken ct)
    {
        if (kind is not ("courses" or "offerings")) throw LmsError.Missing();
        bool course = kind == "courses";
        if (course == input.TermId.HasValue) throw LmsError.Invalid(course ? "ورود درس به نیم‌سال نیاز ندارد." : "نیم‌سال الزامی است.");
        await access.AsEducation(LmsPermissions.Import, ct);
        var major = await catalog.CourseMajor(input.MajorId, LmsPermissions.Import, true, ct);
        var write = course ? LmsPermissions.CatalogWrite : LmsPermissions.Assign;
        await access.AsEducation(write, ct);
        access.DemandScope(write, major.FacultyId, major.Id);
        var asset = await data.Read<Media>().SingleOrDefaultAsync(m => m.Id == input.MediaId && m.OwnerId == access.ActorId &&
             m.TargetId == major.Id && m.Purpose == MediaPurpose.RosterExcel, ct) ?? throw LmsError.Missing();
        if (asset.Bytes is <= 0 or > UploadLimits.RosterBytes) throw LmsError.Invalid("حجم فایل XLSX مجاز نیست.");
        await using var stream = files.Open(asset);
        var rows = RosterWorkbook.ReadRaw(stream, course ? CourseColumns : OfferingColumns, course ? [0] : [0, 1, 2], true);
        var result = new List<AcademicWorkbookRow>(rows.Length);
        var seen = new HashSet<(string Course, string Group)>();
        foreach (var row in rows)
        {
            ct.ThrowIfCancellationRequested();
            var v = row.Values;
            var label = course ? v[0] : string.Join(" · ", v.Take(2));
            if (label.Length > 105) label = label[..105];
            try
            {
                if (row.Error is not null) throw LmsError.Invalid(row.Error);
                var code = LmsError.Code(v[0]);
                if (course)
                {
                    var payload = new CatalogEdit(null, major.Id, null, code, LmsError.Text(v[1], 180));
                    if (!seen.Add((code, "")) || await data.Read<CatalogCourse>().AnyAsync(x => x.MajorId == major.Id && x.Code == code, ct))
                        throw LmsError.Conflict("کد درس در این رشته تکراری است.");
                    if (apply) _ = await catalog.SaveAsync("courses", payload, ct);
                }
                else
                {
                    var group = LmsError.Code(v[1]);
                    if (!seen.Add((code, group))) throw LmsError.Conflict("درس و گروه در فایل تکرار شده است.");
                    var lesson = await data.Read<CatalogCourse>().SingleOrDefaultAsync(c => c.Active && c.MajorId == major.Id && c.Code == code, ct)
                         ?? throw LmsError.Invalid("کد درس فعال در این رشته یافت نشد.");
                    var instructorCode = LmsError.Text(v[2], 100);
                    var teachers = await (from profile in data.Read<InstructorProfile>() join user in data.Read<ApplicationUser>() on profile.Id equals user.Id
                                    where profile.Active && user.IsActive && profile.MajorId == major.Id && user.OrganizationalCode == instructorCode
                                    select profile.Id).Take(2).ToArrayAsync(ct);
                    if (teachers.Length != 1) throw LmsError.Invalid("استاد فعال و یکتا با این کد سازمانی در رشته یافت نشد.");
                    var payload = new OfferingEdit(null, lesson.Id, input.TermId!.Value, teachers[0], major.Id, group, OfferingAudience.Specialized, []);
                    if (apply) _ = await offerings.SaveAsync(payload, ct);
                    else _ = await offerings.Prepare(payload, ct);
                }
                result.Add(new(row.Line, label, apply ? "ثبت شد" : "آماده"));
            }
            catch (UseCaseException e) when (e.StatusCode is 400 or 404 or 409)
            { result.Add(new(row.Line, label, "رد شد", e.Message)); }
            finally { if (apply) data.Db.ChangeTracker.Clear(); }
        }
        return result.ToArray();
    }
}
