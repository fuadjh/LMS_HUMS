using Microsoft.EntityFrameworkCore;
using ReusableAccess.Application.Academics;
using ReusableAccess.Contracts;
using ReusableAccess.Domain.Academics;
using ReusableAccess.Infrastructure.Identity;

namespace ReusableAccess.Infrastructure.Academics;

internal sealed class LmsCatalog(LmsData data, LmsAccess access) : ILmsCatalog
{
    public async Task<LmsHome> HomeAsync(CancellationToken ct)
    {
        var user = await access.Load(ct);
        var study = access.Student ? await access.ActiveStudy(ct) : null;
        var student = access.Student ? await data.Read<StudentProfile>().SingleOrDefaultAsync(x => x.Id == user.Id, ct) : null;
        var teacher = access.Teacher ? await data.Read<InstructorProfile>().SingleOrDefaultAsync(x => x.Id == user.Id, ct) : null;
        return new(user.Id, user.DisplayName, user.IsPlatformSuperAdmin, access.EducationAdmin, access.Teacher, access.Student,
            study?.Cohort.Label, teacher?.Classification, student?.AvatarId, teacher?.AvatarId, teacher?.CvUrl, access.Keys.ToArray());
    }
    public async Task<CatalogRow[]> ListAsync(string kind, CancellationToken ct)
    {
        var person = await access.Load(ct);
        if (kind == "terms" && (person.IsPlatformSuperAdmin || access.HasEducation(LmsPermissions.CatalogRead) || access.HasStudent(LmsPermissions.Learn) || access.HasTeacher(LmsPermissions.Teach)))
        {
            var terms = await data.Read<Term>().OrderByDescending(x => x.Year).ThenByDescending(x => x.Season).ToArrayAsync(ct);
            if (!person.IsPlatformSuperAdmin && !access.EducationAdmin) terms = terms.Where(x => x.Active || x.ClosedUtc != null).ToArray();
            return terms.Select(x => new CatalogRow(x.Id, $"{LmsError.Season(x.Season)} {x.Year}", Year: x.Year, Season: x.Season,
                Calendar: new(x.StartsUtc, x.EndsUtc, x.EnrollOpensUtc, x.EnrollClosesUtc, x.DropOpensUtc, x.DropClosesUtc), Active: x.Active,
                Closed: AcademicRules.IsClosed(x, data.Now))).ToArray();
        }
        if (kind == "cohorts" && !person.IsPlatformSuperAdmin && !access.EducationAdmin)
        {
            await access.AsStudent(LmsPermissions.Learn, ct);
            return (await access.ActiveStudy(ct)) is { } s ? [new(s.Cohort.Id, s.Cohort.Label)] : [];
        }
        await access.AsEducation(LmsPermissions.CatalogRead, ct);
        const string p = LmsPermissions.CatalogRead;
        switch (kind)
        {
            case "faculties":
                return (await data.Read<Faculty>().OrderBy(x => x.Title).ToArrayAsync(ct)).Where(x => access.AdminIn(p, x.Id, null, true))
                    .Select(x => new CatalogRow(x.Id, x.Title, x.Code, Active: x.Active)).ToArray();
            case "majors":
                return (await (from m in data.Read<Major>() join f in data.Read<Faculty>() on m.FacultyId equals f.Id
                              orderby f.Title, m.Title select new { m, f.Title }).ToArrayAsync(ct))
                    .Where(x => access.AdminIn(p, x.m.FacultyId, x.m.Id))
                    .Select(x => new CatalogRow(x.m.Id, x.m.Title, x.m.Code, x.m.FacultyId, Active: x.m.Active) with { RelatedId = x.m.FacultyId }).ToArray();
            case "degrees":
                return (await data.Read<Degree>().OrderBy(x => x.Title).ToArrayAsync(ct)).Select(x => new CatalogRow(x.Id, x.Title, x.Code, Active: x.Active)).ToArray();
            case "programs":
                return (await (from a in data.Read<AcademicProgram>() join m in data.Read<Major>() on a.MajorId equals m.Id
                               join d in data.Read<Degree>() on a.DegreeId equals d.Id
                               orderby m.Title, d.Title select new { a, m.FacultyId, Title = d.Title + " " + m.Title }).ToArrayAsync(ct))
                    .Where(x => access.AdminIn(p, x.FacultyId, x.a.MajorId)).Select(x => new CatalogRow(x.a.Id, x.Title, ParentId: x.a.MajorId, RelatedId: x.a.DegreeId, Active: x.a.Active)).ToArray();
            case "cohorts":
                var cohorts = await access.Cohorts().OrderBy(x => x.Label).ToArrayAsync(ct);
                var allowed = cohorts.Where(x => access.AdminIn(p, x.FacultyId, x.MajorId)).ToDictionary(x => x.Id);
                return (await data.Read<Cohort>().OrderByDescending(x => x.Year).ThenBy(x => x.Season).ToArrayAsync(ct))
                    .Where(x => allowed.ContainsKey(x.Id)).Select(x => new CatalogRow(x.Id, allowed[x.Id].Label, ParentId: x.ProgramId, Year: x.Year, Season: x.Season, Active: x.Active)).ToArray();
            case "courses":
                return (await data.Read<CatalogCourse>().OrderBy(x => x.Title).ToArrayAsync(ct)).Where(x => access.AdminIn(p, x.FacultyId, null, true))
                    .Select(x => new CatalogRow(x.Id, x.Title, x.Code, x.FacultyId, Active: x.Active)).ToArray();
            default: throw LmsError.Missing();
        }
    }

    public async Task<Guid> SaveAsync(string kind, CatalogEdit input, CancellationToken ct)
    {
        await using var tx = await data.Transaction(ct);
        await access.AsEducation(LmsPermissions.CatalogWrite, ct);
        const string permission = LmsPermissions.CatalogWrite;
        var now = data.Now;
        Guid id;
        switch (kind)
        {
            case "faculties":
            {
                await access.AsPlatform(ct);
                var f = input.Id is { } value ? await data.Find<Faculty>(value, ct) : new Faculty();
                if (!input.Active && await data.Read<Major>().AnyAsync(x => x.FacultyId == f.Id && x.Active, ct))
                    throw LmsError.Conflict("ابتدا رشته‌های فعال این دانشکده را تعیین تکلیف کنید.");
                f.Code = LmsError.Code(input.Code); f.Title = LmsError.Text(input.Title, 180); f.Active = input.Active;
                id = f.Id; if (input.Id is null) data.Db.Add(f); break;
            }
            case "majors":
            {
                var f = await data.Find<Faculty>(LmsError.Id(input.ParentId), ct);
                var m = input.Id is { } value ? await data.Find<Major>(value, ct) : new Major() { FacultyId = f.Id };
                access.DemandScope(permission, m.FacultyId, input.Id);
                if (m.FacultyId != f.Id || input.Active && !f.Active) throw LmsError.Conflict("تغییر دانشکده یا فعال‌سازی در دانشکده بایگانی‌شده مجاز نیست.");
                if (!input.Active && (await data.Read<AcademicProgram>().AnyAsync(x => x.MajorId == m.Id && x.Active, ct) ||
                    await data.Read<InstructorProfile>().AnyAsync(x => x.MajorId == m.Id && x.Active, ct)))
                    throw LmsError.Conflict("رشته برنامه یا استاد فعال دارد.");
                m.Code = LmsError.Code(input.Code); m.Title = LmsError.Text(input.Title, 180); m.Active = input.Active;
                id = m.Id; if (input.Id is null) data.Db.Add(m); break;
            }
            case "degrees":
            {
                await access.AsPlatform(ct);
                var d = input.Id is { } value ? await data.Find<Degree>(value, ct) : new Degree();
                if (!input.Active && await data.Read<AcademicProgram>().AnyAsync(x => x.DegreeId == d.Id && x.Active, ct))
                    throw LmsError.Conflict("مقطع به برنامهٔ فعال متصل است.");
                d.Code = LmsError.Code(input.Code); d.Title = LmsError.Text(input.Title, 120); d.Active = input.Active;
                id = d.Id; if (input.Id is null) data.Db.Add(d); break;
            }
            case "programs":
            {
                var m = await data.Find<Major>(LmsError.Id(input.ParentId), ct);
                var d = await data.Find<Degree>(LmsError.Id(input.RelatedId), ct);
                access.DemandScope(permission, m.FacultyId, m.Id);
                var p = input.Id is { } value ? await data.Find<AcademicProgram>(value, ct) : new AcademicProgram() { MajorId = m.Id, DegreeId = d.Id };
                if (p.MajorId != m.Id || p.DegreeId != d.Id || input.Active && (!m.Active || !d.Active))
                    throw LmsError.Conflict("رشته و مقطع برنامهٔ موجود تغییر نمی‌کنند و باید فعال باشند.");
                if (!input.Active && await data.Read<Cohort>().AnyAsync(x => x.ProgramId == p.Id && x.Active, ct))
                    throw LmsError.Conflict("برنامه ورودی فعال دارد.");
                p.Active = input.Active; id = p.Id; if (input.Id is null) data.Db.Add(p); break;
            }
            case "cohorts":
            {
                var p = await data.Find<AcademicProgram>(LmsError.Id(input.ParentId), ct);
                var m = await data.Find<Major>(p.MajorId, ct);
                access.DemandScope(permission, m.FacultyId, m.Id);
                if (input.Year is < 1300 or > 1600 || input.Season is not (TermSeason.Mehr or TermSeason.Bahman))
                    throw LmsError.Invalid("ورودی فقط مهر یا بهمنِ یک سال شمسی است.");
                var c = input.Id is { } value ? await data.Find<Cohort>(value, ct) : new Cohort() { ProgramId = p.Id, Year = input.Year, Season = input.Season };
                if (c.ProgramId != p.Id || c.Year != input.Year || c.Season != input.Season || input.Active && !p.Active)
                    throw LmsError.Conflict("شناسهٔ تحصیلی ورودی تغییر نمی‌کند یا برنامه بایگانی شده است.");
                if (!input.Active && await data.Read<StudyRecord>().AnyAsync(x => x.CohortId == c.Id && x.EndedUtc == null, ct))
                    throw LmsError.Conflict("ورودی پروندهٔ فعال دارد.");
                c.Active = input.Active; id = c.Id; if (input.Id is null) data.Db.Add(c); break;
            }
            case "courses":
            {
                var f = await data.Find<Faculty>(LmsError.Id(input.ParentId), ct);
                access.DemandScope(permission, f.Id, null);
                var c = input.Id is { } value ? await data.Find<CatalogCourse>(value, ct) : new CatalogCourse() { FacultyId = f.Id };
                if (c.FacultyId != f.Id || input.Active && !f.Active) throw LmsError.Conflict("دانشکدهٔ درس تغییر نمی‌کند.");
                if (!input.Active && await (from o in data.Read<Offering>() join t in data.Read<Term>() on o.TermId equals t.Id
                          where o.CourseId == c.Id && o.Active && t.Active && t.ClosedUtc == null && t.EndsUtc > now select o.Id).AnyAsync(ct))
                    throw LmsError.Conflict("درس در نیم‌سال جاری ارائهٔ فعال دارد.");
                c.Code = LmsError.Code(input.Code); c.Title = LmsError.Text(input.Title, 180); c.Active = input.Active;
                id = c.Id; if (input.Id is null) data.Db.Add(c); break;
            }
            case "terms":
            {
                await access.AsPlatform(ct);
                if (input.Year is < 1300 or > 1600 || !Enum.IsDefined(input.Season)) throw LmsError.Invalid("سال یا فصل نیم‌سال نامعتبر است.");
                var term = input.Id is { } value ? await data.Find<Term>(value, ct) : new Term() { Year = input.Year, Season = input.Season };
                if (input.Id is not null && (term.ClosedUtc is not null || now >= term.EndsUtc))
                    throw LmsError.Conflict("نیم‌سال پایان‌یافته تغییر نمی‌کند.");
                if (term.Year != input.Year || term.Season != input.Season) throw LmsError.Conflict("شناسهٔ نیم‌سال تغییر نمی‌کند.");
                var c = input.Calendar ?? throw LmsError.Invalid("همهٔ تاریخ‌های نیم‌سال الزامی‌اند.");
                var start = LmsError.Instant(c.StartsUtc); var end = LmsError.Instant(c.EndsUtc);
                var eo = LmsError.Instant(c.EnrollOpensUtc); var ec = LmsError.Instant(c.EnrollClosesUtc);
                var drop = LmsError.Instant(c.DropOpensUtc); var until = LmsError.Instant(c.DropClosesUtc);
                if (start >= end || eo >= ec || ec > end || drop >= until || until > end || drop < eo)
                    throw LmsError.Invalid("ابتدا و انتهای نیم‌سال، اخذ و حذف‌واضافه را بررسی کنید.");
                var hasOffer = await data.Read<Offering>().AnyAsync(x => x.TermId == term.Id, ct);
                if (hasOffer && (term.StartsUtc != start || term.EndsUtc != end || !input.Active))
                    throw LmsError.Conflict("برای نیم‌سال دارای ارائه، فقط پنجره‌های اخذ و حذف‌واضافه یا بستن مجاز است.");
                if (await data.Read<Offering>().AnyAsync(x => x.TermId == term.Id && x.Active && x.VisibleUtc >= ec, ct))
                    throw LmsError.Conflict("بسته‌شدن اخذ باید بعد از زمان دیده‌شدن ارائه‌های فعال باشد.");
                term.StartsUtc = start; term.EndsUtc = end; term.EnrollOpensUtc = eo; term.EnrollClosesUtc = ec;
                term.DropOpensUtc = drop; term.DropClosesUtc = until; term.Active = input.Active;
                if (input.Close) term.ClosedUtc = now;
                id = term.Id; if (input.Id is null) data.Db.Add(term); break;
            }
            default: throw LmsError.Missing();
        }
        return await data.Finish(tx, id, id, ct);
    }

    public async Task<ScopeGrantRow[]> GrantsAsync(CancellationToken ct)
    {
        await access.AsPlatform(ct);
        return await (from g in data.Read<AcademicGrant>() join u in data.Read<ApplicationUser>() on g.UserId equals u.Id
                      join f in data.Read<Faculty>() on g.FacultyId equals f.Id
                      join m in data.Read<Major>() on g.MajorId equals (Guid?)m.Id into majors
                      from m in majors.DefaultIfEmpty()
                      orderby u.DisplayName, f.Title, m.Title
                      select new ScopeGrantRow(g.Id, g.UserId, u.DisplayName, g.FacultyId, g.MajorId,
                          m == null ? f.Title : f.Title + " · " + m.Title, g.Active)).Take(500).ToArrayAsync(ct);
    }
    public async Task<Guid> GrantAsync(ScopeGrantEdit input, CancellationToken ct)
    {
        await using var tx = await data.Transaction(ct);
        await access.AsPlatform(ct);
        var faculty = await data.Find<Faculty>(input.FacultyId, ct);
        var user = await data.Find<ApplicationUser>(input.UserId, ct);
        if (input.Active && (!faculty.Active || !user.IsActive)) throw LmsError.Conflict("حساب و دانشکده باید فعال باشند.");
        if (input.MajorId is { } mid)
        {
            var m = await data.Find<Major>(mid, ct);
            if (m.FacultyId != faculty.Id || input.Active && !m.Active) throw LmsError.Invalid("رشته در دانشکدهٔ انتخاب‌شده فعال نیست.");
        }
        var g = await data.Db.Set<AcademicGrant>().SingleOrDefaultAsync(x => x.UserId == input.UserId && x.FacultyId == input.FacultyId && x.MajorId == input.MajorId, ct);
        if (g is null) { g = new() { UserId = input.UserId, FacultyId = input.FacultyId, MajorId = input.MajorId }; data.Db.Add(g); }
        g.Active = input.Active;
        return await data.Finish(tx, g.Id, g.Id, ct);
    }
}
