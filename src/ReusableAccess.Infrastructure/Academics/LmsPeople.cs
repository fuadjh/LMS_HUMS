using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ReusableAccess.Application.Academics;
using ReusableAccess.Application.Errors;
using ReusableAccess.Application.Users;
using ReusableAccess.Contracts;
using ReusableAccess.Domain.Academics;
using ReusableAccess.Domain.Identity;
using ReusableAccess.Infrastructure.Identity;

namespace ReusableAccess.Infrastructure.Academics;

internal sealed class LmsPeople(LmsData data, LmsAccess access, UserManager<ApplicationUser> users, LmsFiles files) : ILmsPeople
{
    internal static bool IsStudent(string kind) => kind switch
    {
        "student" => true, "teacher" => false, _ => throw LmsError.Missing()
    };
    internal static string National(string? input)
    {
        var value = IdentityRules.NormalizeDigits(input);
        if (!IdentityRules.IsValidNationalId(value)) throw LmsError.Invalid("کد ملی باید ۱۰ رقم با رقم کنترل معتبر باشد.");
        return value;
    }

    private async Task<(Guid Faculty, Guid Major)> Scope(string key, bool student, Guid id, bool mustBeActive, CancellationToken ct)
    {
        if (student)
        {
            var c = await access.Cohorts().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw LmsError.Missing();
            access.DemandScope(key, c.FacultyId, c.MajorId);
            if (mustBeActive && !c.Active) throw LmsError.Conflict("ورودی یا برنامه بایگانی شده است.");
            return (c.FacultyId, c.MajorId);
        }
        var m = await data.Read<Major>().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw LmsError.Missing();
        access.DemandScope(key, m.FacultyId, m.Id);
        if (mustBeActive && (!m.Active || !await data.Read<Faculty>().AnyAsync(x => x.Id == m.FacultyId && x.Active, ct)))
            throw LmsError.Conflict("رشته یا دانشکده بایگانی شده است.");
        return (m.FacultyId, m.Id);
    }

    private async Task<StudyRecord?> LatestStudy(Guid user, CancellationToken ct)
        => await data.Db.Set<StudyRecord>().Where(x => x.StudentId == user).OrderBy(x => x.EndedUtc != null)
            .ThenByDescending(x => x.OpenedUtc).FirstOrDefaultAsync(ct);

    public async Task<ProbeResult> ProbeAsync(string kind, ProbeInput input, CancellationToken ct)
    {
        var actor = await access.Load(ct);
        await access.AsEducation(LmsPermissions.PeopleRead, ct);
        var student = IsStudent(kind);
        _ = await Scope(LmsPermissions.PeopleRead, student, input.ScopeId, false, ct);
        var national = National(input.NationalId);
        var user = await data.Read<ApplicationUser>().SingleOrDefaultAsync(x => x.NationalId == national, ct);
        if (user is null) return new(false, false, false, false, true, null);
        data.Resource(user.Id);
        if (user.IsPlatformSuperAdmin && !actor.IsPlatformSuperAdmin) throw LmsError.Missing();
        var teacher = await data.Read<InstructorProfile>().SingleOrDefaultAsync(x => x.Id == user.Id, ct);
        var learner = await data.Read<StudentProfile>().AnyAsync(x => x.Id == user.Id, ct);
        var scope = student ? (await LatestStudy(user.Id, ct))?.CohortId : teacher?.MajorId;
        var canEdit = true;
        if (scope is { } scopeId)
        {
            try { await Scope(LmsPermissions.PeopleWrite, student, scopeId, false, ct); }
            catch (UseCaseException e) when (e.StatusCode == 404) { canEdit = false; }
        }
        return new(true, user.IsActive, teacher is not null, learner, canEdit,
            actor.IsPlatformSuperAdmin || scope is not null && canEdit ? user.DisplayName : null);
    }

    public async Task<PeopleRow[]> ListAsync(string kind, Guid? scopeId, string? search, CancellationToken ct)
        => (await Page(kind, scopeId, search, null, ct)).Rows;

    internal async Task<LmsTableSlice<PeopleRow>> Page(string kind, Guid? scopeId, string? search, LmsTablePage? page, CancellationToken ct)
    {
        var actor = await access.Load(ct);
        const string permission = LmsPermissions.PeopleRead;
        await access.AsEducation(permission, ct);
        var student = IsStudent(kind);
        var q = LmsError.Text(search, 80, optional: true);
        if (scopeId is { } s) await Scope(permission, student, s, false, ct);
        var scope = access.EducationScopes(permission);
        var faculties = scope.Faculties; var majors = scope.Majors; var platform = actor.IsPlatformSuperAdmin;
        IQueryable<PeopleRow> rows;
        if (!student)
        {
            rows = from p in data.Read<InstructorProfile>() join m in data.Read<Major>() on p.MajorId equals m.Id
                   join u in data.Read<ApplicationUser>() on p.Id equals u.Id
                   where (platform || !u.IsPlatformSuperAdmin && (faculties.Contains(m.FacultyId) || majors.Contains(m.Id))) &&
                         (scopeId == null || p.MajorId == scopeId) && (q == "" || u.DisplayName.Contains(q) || p.Classification.Contains(q))
                   orderby u.DisplayName, p.Id
                   select new PeopleRow(p.Id, u.DisplayName, u.IsActive, p.Active, p.MajorId, null, m.Title, null, p.Classification, p.CvUrl);
        }
        else
        {
            rows = from r in data.Read<StudyRecord>() join p in data.Read<StudentProfile>() on r.StudentId equals p.Id
                   join c in access.Cohorts() on r.CohortId equals c.Id join u in data.Read<ApplicationUser>() on r.StudentId equals u.Id
                   where (platform || !u.IsPlatformSuperAdmin && (faculties.Contains(c.FacultyId) || majors.Contains(c.MajorId))) &&
                         (scopeId == null || r.CohortId == scopeId) && (q == "" || u.DisplayName.Contains(q) || r.Number.Contains(q)) &&
                         r.Id == data.Read<StudyRecord>().Where(old => old.StudentId == r.StudentId)
                              .OrderByDescending(old => old.EndedUtc == null).ThenByDescending(old => old.OpenedUtc).ThenByDescending(old => old.Id)
                              .Select(old => old.Id).FirstOrDefault()
                   orderby u.DisplayName, p.Id
                   select new PeopleRow(p.Id, u.DisplayName, u.IsActive, p.Active, c.MajorId, c.Id, c.Label, r.Number, null, null);
        }
        if (page is null)
        {
            var first = await rows.Take(500).ToArrayAsync(ct);
            return new(first, first.Length, 1, 500);
        }
        var (actual, size, total) = await AcademicPaging.Bounds(rows, page, ct);
        return new(await rows.Skip((actual - 1) * size).Take(size).ToArrayAsync(ct), total, actual, size);
    }

    public async Task<StudyRow[]> HistoryAsync(Guid studentId, CancellationToken ct)
    {
        await access.AsEducation(LmsPermissions.PeopleRead, ct);
        var rows = await (from s in data.Read<StudyRecord>() join c in access.Cohorts() on s.CohortId equals c.Id
                          where s.StudentId == studentId orderby s.OpenedUtc descending select new { s, c }).ToArrayAsync(ct);
        rows = rows.Where(x => access.AdminIn(LmsPermissions.PeopleRead, x.c.FacultyId, x.c.MajorId)).ToArray();
        if (rows.Length == 0) throw LmsError.Missing();
        return rows.Select(x => new StudyRow(x.s.Id, x.c.Id, x.c.Label, x.s.Number, x.s.OpenedUtc, x.s.EndedUtc)).ToArray();
    }

    public Task<Guid> SaveAsync(string kind, ProfileEdit input, CancellationToken ct)
        => SaveCore(kind, input, import: false, ct);

    private async Task<Guid> SaveCore(string kind, ProfileEdit input, bool import, CancellationToken ct)
    {
        await using var tx = await data.Transaction(ct);
        var actor = await access.Load(ct);
        const string permission = LmsPermissions.PeopleWrite;
        await access.AsEducation(permission, ct);
        var student = IsStudent(kind);
        var scopeId = input.ScopeId;
        _ = await Scope(permission, student, scopeId, input.Active, ct);
        if (import) _ = await Scope(LmsPermissions.Import, student, scopeId, true, ct);
        var national = National(input.NationalId);
        var user = await data.Db.Users.SingleOrDefaultAsync(x => x.NationalId == national, ct);
        if (user?.IsPlatformSuperAdmin == true && !actor.IsPlatformSuperAdmin) throw LmsError.Missing();
        if (user is not null && import) CheckExistingAccount(user, input);
        if (user is null)
        {
            if (!input.Active) throw LmsError.Invalid("ابتدا پروفایل جدید را فعال ثبت کنید.");
            var profile = NewIdentity(input, national, import);
            user = new ApplicationUser
            {
                Id = Guid.NewGuid(), NationalId = national, UserName = national, IsActive = true,
                FirstName = profile.FirstName.Trim(), LastName = profile.LastName.Trim(),
                DisplayName = $"{profile.FirstName.Trim()} {profile.LastName.Trim()}",
                OrganizationalCode = profile.OrganizationalCode.Trim(), Email = profile.Email.Trim(),
                PhoneNumber = IdentityRules.NormalizeMobile(profile.Mobile), LockoutEnabled = true, MustChangePassword = true
            };
            var result = await users.CreateAsync(user, profile.Password);
            if (!result.Succeeded) throw LmsError.Invalid("حساب ساخته نشد؛ یکتایی شناسه و ایمیل و قواعد گذرواژه را بررسی کنید.");
        }

        if (student)
        {
            var profile = await data.Db.Set<StudentProfile>().FindAsync([user.Id], ct);
            // به‌جای تغییر سابقه، انتقالْ پروندهٔ قبلی را می‌بندد و پروندهٔ تازه می‌سازد.
            var previous = profile is null ? null : await LatestStudy(user.Id, ct);
            if (previous is not null) _ = await Scope(permission, true, previous.CohortId, false, ct);
            if (import && profile is not null) throw LmsError.Conflict("پروفایل از قبل وجود دارد؛ ویرایش یا انتقال فقط دستی است.");
            if (profile is null)
            {
                if (!input.Active) throw LmsError.Invalid("پروفایل تازه باید فعال باشد.");
                profile = new StudentProfile { Id = user.Id, Active = true }; data.Db.Add(profile);
            }
            profile.Active = input.Active;
            var active = previous is { EndedUtc: null };
            if (input.Active)
            {
                var number = LmsError.Text(IdentityRules.NormalizeDigits(input.StudentNumber), 50);
                if (active && previous!.CohortId == scopeId && previous.Number == number)
                    return await data.Finish(tx, user.Id, user.Id, ct);
                if (active)
                {
                    previous!.EndedUtc = data.Now;
                    await data.Save(ct); // unique active record; still inside the same transaction
                }
                data.Db.Add(new StudyRecord { StudentId = user.Id, CohortId = scopeId, Number = number, OpenedUtc = data.Now });
            }
            else if (active) previous!.EndedUtc = data.Now;
        }
        else
        {
            var profile = await data.Db.Set<InstructorProfile>().FindAsync([user.Id], ct);
            if (profile is not null) _ = await Scope(permission, false, profile.MajorId, false, ct);
            if (import && profile is not null) throw LmsError.Conflict("پروفایل از قبل وجود دارد؛ از ویرایش دستی استفاده کنید.");
            if (profile is null)
            {
                if (!input.Active) throw LmsError.Invalid("پروفایل تازه باید فعال باشد.");
                profile = new InstructorProfile { Id = user.Id }; data.Db.Add(profile);
            }
            profile.Active = input.Active; profile.MajorId = scopeId;
            profile.Classification = LmsError.Text(input.Classification, 100, true);
            profile.CvUrl = Cv(input.CvUrl);
        }
        return await data.Finish(tx, user.Id, user.Id, ct);
    }

    public async Task<bool> SetActiveAsync(string kind, Guid userId, bool active, CancellationToken ct)
    {
        await using var tx = await data.Transaction(ct);
        var actor = await access.Load(ct);
        await access.AsEducation(LmsPermissions.PeopleWrite, ct);
        var u = await data.Find<ApplicationUser>(userId, ct);
        if (u.IsPlatformSuperAdmin && !actor.IsPlatformSuperAdmin) throw LmsError.Missing();
        if (IsStudent(kind))
        {
            if (active) throw LmsError.Conflict("برای فعال‌سازی دانشجو، پرونده و ورودی را انتخاب کنید.");
            var profile = await data.Find<StudentProfile>(userId, ct);
            var old = await LatestStudy(userId, ct) ?? throw LmsError.Missing();
            _ = await Scope(LmsPermissions.PeopleWrite, true, old.CohortId, false, ct);
            profile.Active = false;
            old.EndedUtc ??= data.Now;
        }
        else
        {
            var profile = await data.Find<InstructorProfile>(userId, ct);
            _ = await Scope(LmsPermissions.PeopleWrite, false, profile.MajorId, active, ct);
            profile.Active = active;
        }
        return await data.Finish(tx, true, userId, ct);
    }

    private static string? Cv(string? value)
    {
        var text = LmsError.Text(value, 600, true);
        if (text.Length == 0) return null;
        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(uri.UserInfo))
            throw LmsError.Invalid("پیوند رزومه باید یک نشانی HTTPS معتبر باشد.");
        return uri.AbsoluteUri.Length <= 600 ? uri.AbsoluteUri : throw LmsError.Invalid("پیوند رزومه طولانی است.");
    }
    private static CreateUserInput NewIdentity(ProfileEdit input, string national, bool import)
    {
        var p = new CreateUserInput
        {
            NationalId = national, FirstName = input.FirstName ?? "", LastName = input.LastName ?? "",
            OrganizationalCode = input.OrganizationCode ?? "", Email = input.Email ?? "", Mobile = input.Mobile ?? "",
            Password = import ? "Aa!9" + Convert.ToHexString(RandomNumberGenerator.GetBytes(24)) : input.TemporaryPassword ?? ""
        };
        UserProfileValidation.Profile(p);
        return p;
    }
    private static void CheckExistingAccount(ApplicationUser u, ProfileEdit row)
    {
        if (Different(row.FirstName, u.FirstName) || Different(row.LastName, u.LastName) ||
            Different(row.OrganizationCode, u.OrganizationalCode) || Different(row.Email, u.Email) ||
            (!string.IsNullOrWhiteSpace(row.Mobile) && IdentityRules.NormalizeMobile(row.Mobile) != u.PhoneNumber))
            throw LmsError.Conflict("مشخصات هویتی ردیف با حساب موجود هم‌خوان نیست؛ هیچ مقداری بازنویسی نشد.");
    }
    private static bool Different(string? supplied, string? actual)
        => !string.IsNullOrWhiteSpace(supplied) && !string.Equals(supplied.Trim(), actual, StringComparison.OrdinalIgnoreCase);

    public async Task<ImportRow[]> ImportAsync(string kind, Guid mediaId, Guid scopeId, bool apply, CancellationToken ct)
    {
        await access.AsEducation(LmsPermissions.Import, ct);
        var student = IsStudent(kind);
        _ = await Scope(LmsPermissions.Import, student, scopeId, true, ct);
        if (apply) _ = await Scope(LmsPermissions.PeopleWrite, student, scopeId, true, ct);
        var media = await data.Read<Media>().SingleOrDefaultAsync(x => x.Id == mediaId && x.OwnerId == access.ActorId &&
            x.TargetId == scopeId && x.Purpose == MediaPurpose.RosterExcel, ct) ?? throw LmsError.Missing();
        await using var stream = files.Open(media);
        var entries = RosterWorkbook.Read(stream, student, scopeId);
        var duplicates = entries.GroupBy(x => IdentityRules.NormalizeDigits(x.Input.NationalId)).Where(x => x.Count() > 1)
            .Select(x => x.Key).ToHashSet();
        var output = new List<ImportRow>();
        foreach (var row in entries)
        {
            ct.ThrowIfCancellationRequested();
            var number = IdentityRules.NormalizeDigits(row.Input.NationalId);
            var mask = number.Length == 10 && number.All(char.IsAsciiDigit) ? "•••••••" + number[^3..] : "—";
            try
            {
                if (row.Error is not null) throw LmsError.Invalid(row.Error);
                _ = National(number);
                if (duplicates.Contains(number)) throw LmsError.Invalid("کد ملی در فایل تکراری است.");
                var u = await data.Read<ApplicationUser>().SingleOrDefaultAsync(x => x.NationalId == number, ct);
                if (u is not null)
                {
                    if (u.IsPlatformSuperAdmin && !(await access.Load(ct)).IsPlatformSuperAdmin) throw LmsError.Missing();
                    CheckExistingAccount(u, row.Input);
                    var already = student ? await data.Read<StudentProfile>().AnyAsync(x => x.Id == u.Id, ct)
                        : await data.Read<InstructorProfile>().AnyAsync(x => x.Id == u.Id, ct);
                    if (already) throw LmsError.Conflict("پروفایل موجود است؛ فایل آن را تغییر نمی‌دهد.");
                }
                else _ = NewIdentity(row.Input, number, true);
                if (student) _ = LmsError.Text(IdentityRules.NormalizeDigits(row.Input.StudentNumber), 50);
                else { _ = Cv(row.Input.CvUrl); _ = LmsError.Text(row.Input.Classification, 100, true); }
                if (apply) _ = await SaveCore(kind, row.Input, true, ct);
                output.Add(new(row.Line, mask, apply ? "ثبت شد" : "آماده", u is null ? "حساب تازه؛ رمز اولیه را ادمین کل با گردش فعلی تنظیم می‌کند." : "فقط پروفایل افزوده می‌شود."));
            }
            catch (UseCaseException e) { output.Add(new(row.Line, mask, "رد شد", e.Message)); }
            catch (DbUpdateException) { output.Add(new(row.Line, mask, "رد شد", "تداخل ثبت داده؛ فهرست را تازه‌سازی کنید.")); }
            finally { data.Db.ChangeTracker.Clear(); }
        }
        data.Resource(scopeId);
        return output.ToArray();
    }
}
