using System.Data;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using ReusableAccess.Application.Errors;
using ReusableAccess.Contracts;
using ReusableAccess.Domain.Academics;
using ReusableAccess.Domain.Entities;
using ReusableAccess.Infrastructure.Identity;
using ReusableAccess.Infrastructure.Persistence;

namespace ReusableAccess.Infrastructure.Academics;

internal static class LmsError
{
    internal static UseCaseException Invalid(string text) => new(400, text);
    internal static UseCaseException Denied() => new(403, "مجوز انجام این عملیات را ندارید.");
    internal static UseCaseException Missing() => new(404, "منبع در این محدوده پیدا نشد.");
    internal static UseCaseException Conflict(string text) => new(409, text);
    internal static string Text(string? value, int max, bool optional = false)
    {
        var text = value?.Trim() ?? "";
        if ((text.Length == 0 && !optional) || text.Length > max || text.Contains('\0'))
            throw Invalid($"مقدار الزامی خالی یا طول متن بیش از {max} کاراکتر است.");
        return text;
    }
    internal static string Code(string? value) => Text(value, 50).ToUpperInvariant();
    internal static Guid Id(Guid? value) => value is { } id && id != Guid.Empty
        ? id : throw Invalid("انتخاب این مورد الزامی است.");
    internal static DateTimeOffset Instant(DateTimeOffset value)
        => value.Year is >= 1900 and <= 2250 && value.Ticks % TimeSpan.TicksPerMinute == 0
            ? value.ToUniversalTime() : throw Invalid("تاریخ/ساعت معتبر تا دقت دقیقه وارد کنید.");
    internal static string Season(TermSeason season) => season switch
    {
        TermSeason.Mehr => "مهر", TermSeason.Bahman => "بهمن", TermSeason.Summer => "تابستان", _ => "نامشخص"
    };
}

internal sealed class LmsData(ApplicationDbContext db, TimeProvider time, IHttpContextAccessor http)
{
    internal ApplicationDbContext Db => db;
    internal IQueryable<T> Read<T>() where T : class => db.Set<T>().AsNoTracking();
    internal DateTimeOffset Now => time.GetUtcNow();
    internal async Task<T> Find<T>(Guid id, CancellationToken ct) where T : class
    {
        var value = await db.Set<T>().FindAsync([id], ct);
        return value ?? throw LmsError.Missing();
    }
    internal Task<IDbContextTransaction> Transaction(CancellationToken ct) => db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
    internal async Task Save(CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException e) when (e.GetBaseException() is SqlException { Number: 2601 or 2627 or 1205 })
        { throw LmsError.Conflict("رکورد تکراری یا تغییر هم‌زمان؛ صفحه را تازه‌سازی کنید."); }
    }
    internal void Resource(Guid id)
    {
        if (http.HttpContext is { } context) context.Items["lms-resource"] = id;
    }
    internal async Task<T> Finish<T>(IDbContextTransaction tx, T result, Guid? resource, CancellationToken ct)
    {
        await Save(ct);
        await tx.CommitAsync(ct);
        if (resource.HasValue) Resource(resource.Value);
        return result;
    }
    internal static void Open(Term term, DateTimeOffset now)
    {
        if (AcademicRules.IsClosed(term, now)) throw LmsError.Conflict("نیم‌سال بسته شده و فقط خواندنی است.");
    }
}

internal sealed class CohortInfo
{
    public Guid Id { get; init; }
    public Guid FacultyId { get; init; }
    public Guid MajorId { get; init; }
    public Guid ProgramId { get; init; }
    public string Label { get; init; } = "";
    public bool Active { get; init; }
}
internal sealed record StudyInfo(StudyRecord Record, CohortInfo Cohort);
internal sealed class OfferingInfo
{
    public Offering Offering { get; init; } = null!;
    public CatalogCourse Course { get; init; } = null!;
    public Term Term { get; init; } = null!;
}

internal sealed class LmsAccess(LmsData data, IHttpContextAccessor accessor)
{
    private ApplicationUser? _actor;
    private Dictionary<string, HashSet<string>>? _keys;
    private AcademicGrant[]? _grants;
    private bool _student;
    private bool _teacher;
    internal static Guid? Subject(ClaimsPrincipal? principal) => Guid.TryParse(
        principal?.FindFirstValue("sub") ?? principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
    internal Guid ActorId => Subject(accessor.HttpContext?.User) ?? throw new UseCaseException(401, "ورود معتبر لازم است.");
    internal bool Student => _student;
    internal bool Teacher => _teacher;
    internal bool EducationAdmin => _grants?.Length > 0;
    internal IReadOnlyCollection<string> Keys => _actor?.IsPlatformSuperAdmin == true
        ? LmsPermissions.All.Keys.ToArray() : _keys?.Values.SelectMany(x => x).Distinct().ToArray() ?? [];

    internal Task<ApplicationUser> Reload(CancellationToken ct) { _actor = null; return Load(ct); }
    internal async Task<ApplicationUser> Load(CancellationToken ct)
    {
        if (_actor is not null) return _actor;
        var id = ActorId;
        var actor = await data.Read<ApplicationUser>().SingleOrDefaultAsync(x => x.Id == id && x.IsActive && !x.MustChangePassword, ct)
            ?? throw new UseCaseException(401, "حساب یا نشست آموزشی معتبر نیست.");
        _student = await data.Read<StudentProfile>().AnyAsync(x => x.Id == id && x.Active, ct);
        _teacher = await (from teacher in data.Read<InstructorProfile>()
                          join major in data.Read<Major>() on teacher.MajorId equals major.Id
                          join faculty in data.Read<Faculty>() on major.FacultyId equals faculty.Id
                          where teacher.Id == id && teacher.Active && major.Active && faculty.Active select teacher.Id).AnyAsync(ct);
        _grants = await (from g in data.Read<AcademicGrant>()
                         join faculty in data.Read<Faculty>() on g.FacultyId equals faculty.Id
                         where g.UserId == id && g.Active && faculty.Active && (g.MajorId == null ||
                            data.Read<Major>().Any(m => m.Id == g.MajorId && m.FacultyId == g.FacultyId && m.Active))
                         select g).ToArrayAsync(ct);
        var codes = new List<string>();
        if (_student) codes.Add(LmsPermissions.StudentRole);
        if (_teacher) codes.Add(LmsPermissions.TeacherRole);
        if (_grants.Any(x => x.MajorId is null)) codes.Add(LmsPermissions.FacultyRole);
        if (_grants.Any(x => x.MajorId is not null)) codes.Add(LmsPermissions.MajorRole);
        var rows = await (from r in data.Read<Role>()
                          join rp in data.Read<RolePermission>() on r.Id equals rp.RoleId
                          join p in data.Read<Permission>() on rp.PermissionId equals p.Id
                          where codes.Contains(r.Code) && p.Key.StartsWith("lms.")
                          select new { r.Code, p.Key }).ToArrayAsync(ct);
        _keys = rows.GroupBy(x => x.Code, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.Select(y => y.Key).ToHashSet(StringComparer.Ordinal), StringComparer.OrdinalIgnoreCase);
        _actor = actor;
        return actor;
    }
    internal bool RoleHas(string role, string key) => _keys is not null && _keys.TryGetValue(role, out var keys) && keys.Contains(key);
    internal bool HasStudent(string key) => _student && RoleHas(LmsPermissions.StudentRole, key);
    internal bool HasTeacher(string key) => _teacher && RoleHas(LmsPermissions.TeacherRole, key);
    internal async Task AsStudent(string key, CancellationToken ct)
    {
        await Load(ct);
        if (!HasStudent(key)) throw LmsError.Denied();
    }
    internal async Task AsTeacher(string key, CancellationToken ct)
    {
        await Load(ct);
        if (!HasTeacher(key)) throw LmsError.Denied();
    }
    internal async Task AsPlatform(CancellationToken ct)
    {
        if (!(await Load(ct)).IsPlatformSuperAdmin) throw LmsError.Denied();
    }
    internal bool HasEducation(string key) => RoleHas(LmsPermissions.FacultyRole, key) || RoleHas(LmsPermissions.MajorRole, key);
    internal async Task AsEducation(string key, CancellationToken ct)
    {
        var user = await Load(ct);
        if (!user.IsPlatformSuperAdmin && !RoleHas(LmsPermissions.FacultyRole, key) && !RoleHas(LmsPermissions.MajorRole, key))
            throw LmsError.Denied();
    }
    internal bool AdminIn(string key, Guid faculty, Guid? major, bool forRead = false)
        => _actor?.IsPlatformSuperAdmin == true || _grants!.Any(g => g.FacultyId == faculty &&
            (g.MajorId is null && RoleHas(LmsPermissions.FacultyRole, key) || g.MajorId is not null &&
            (g.MajorId == major || (major is null && forRead)) && RoleHas(LmsPermissions.MajorRole, key)));
    internal void DemandScope(string key, Guid faculty, Guid? major, bool forRead = false)
    {
        if (!AdminIn(key, faculty, major, forRead)) throw LmsError.Missing();
    }
    internal bool CanAdminOffering(string key, OfferingInfo info)
        => _actor?.IsPlatformSuperAdmin == true || (info.Offering.Audience != OfferingAudience.General &&
            AdminIn(key, info.Course.FacultyId, info.Offering.OwnerMajorId));
    internal async Task<OfferingInfo> Offering(Guid id, CancellationToken ct)
    {
        var row = await (from o in data.Read<Offering>()
                         join c in data.Read<CatalogCourse>() on o.CourseId equals c.Id
                         join t in data.Read<Term>() on o.TermId equals t.Id
                         where o.Id == id select new { o, c, t }).SingleOrDefaultAsync(ct) ?? throw LmsError.Missing();
        data.Resource(id);
        return new() { Offering = row.o, Course = row.c, Term = row.t };
    }
    internal IQueryable<CohortInfo> Cohorts() =>
        from c in data.Read<Cohort>()
        join p in data.Read<AcademicProgram>() on c.ProgramId equals p.Id
        join d in data.Read<Degree>() on p.DegreeId equals d.Id
        join m in data.Read<Major>() on p.MajorId equals m.Id
        join f in data.Read<Faculty>() on m.FacultyId equals f.Id
        select new CohortInfo
        {
            Id = c.Id, FacultyId = f.Id, MajorId = m.Id, ProgramId = p.Id,
            Label = d.Title + " " + m.Title + " · " + (c.Season == TermSeason.Mehr ? "مهر " : "بهمن ") + c.Year.ToString(),
            Active = c.Active && p.Active && d.Active && m.Active && f.Active
        };
    internal async Task<StudyInfo?> ActiveStudy(CancellationToken ct)
    {
        var actor = ActorId;
        var record = await data.Read<StudyRecord>().SingleOrDefaultAsync(x => x.StudentId == actor && x.EndedUtc == null, ct);
        if (record is null) return null;
        var cohort = await Cohorts().SingleAsync(x => x.Id == record.CohortId, ct);
        return new(record, cohort);
    }
    internal async Task<Enrollment?> OwnEnrollment(Guid offeringId, CancellationToken ct)
    {
        var user = ActorId;
        return await (from e in data.Read<Enrollment>()
                      join r in data.Read<StudyRecord>() on e.StudyRecordId equals r.Id
                      where e.OfferingId == offeringId && r.StudentId == user
                      orderby (r.EndedUtc == null) descending, e.CreatedUtc descending select e).FirstOrDefaultAsync(ct);
    }
    internal async Task<bool> Eligible(OfferingInfo i, StudyInfo? student, CancellationToken ct)
    {
        if (student is null || !student.Cohort.Active || !i.Course.Active || !i.Offering.Active) return false;
        var targets = await data.Read<OfferingCohort>().Where(x => x.OfferingId == i.Offering.Id).Select(x => x.CohortId).ToArrayAsync(ct);
        return AcademicRules.Eligible(i.Offering.Audience, i.Course.FacultyId, i.Offering.OwnerMajorId,
            student.Cohort.FacultyId, student.Cohort.MajorId, targets.Length == 0 || targets.Contains(student.Cohort.Id));
    }
    internal async Task<CoTeacher?> CoAssignment(Guid offeringId, CancellationToken ct)
        => await data.Read<CoTeacher>().SingleOrDefaultAsync(x => x.OfferingId == offeringId && x.InstructorId == ActorId && x.Active, ct);
    internal async Task AsTeaching(OfferingInfo info, string key, CancellationToken ct)
    {
        await AsTeacher(key, ct);
        if (info.Offering.LeadId == ActorId) return;
        var co = await CoAssignment(info.Offering.Id, ct);
        if (co is null) throw LmsError.Missing();
        var permitted = key switch
        {
            LmsPermissions.Teach => true,
            LmsPermissions.Grade or LmsPermissions.Release => co.CanGrade,
            LmsPermissions.Publish => co.CanPublish,
            _ => false // private template and authorship remain with the lead
        };
        if (!permitted) throw LmsError.Denied();
    }
    internal async Task<Enrollment> ActiveEnrollment(Guid offeringId, CancellationToken ct)
    {
        var e = await OwnEnrollment(offeringId, ct) ?? throw LmsError.Missing();
        var current = await ActiveStudy(ct);
        if (e.Status != EnrollmentStatus.Active || current?.Record.Id != e.StudyRecordId || current.Cohort.Active != true)
            throw LmsError.Denied();
        return e;
    }
}
