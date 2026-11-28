using Microsoft.EntityFrameworkCore;
using ReusableAccess.Application.Academics;
using ReusableAccess.Contracts;
using ReusableAccess.Domain.Academics;
using ReusableAccess.Infrastructure.Identity;

namespace ReusableAccess.Infrastructure.Academics;

internal sealed partial class LmsOfferings(LmsData data, LmsAccess access, StudioTemplates studioTemplates, StudioMaterializer materializer) : ILmsOfferings
{
    internal IQueryable<OfferingInfo> Infos() => from o in data.Read<Offering>()
        join c in data.Read<CatalogCourse>() on o.CourseId equals c.Id
        join t in data.Read<Term>() on o.TermId equals t.Id select new OfferingInfo { Offering = o, Course = c, Term = t };

    private async Task AuthorizeView(string view, CancellationToken ct)
    {
        switch (view)
        {
            case "admin": await access.AsEducation(LmsPermissions.Assign, ct); break;
            case "teacher": case "preview": await access.AsTeacher(LmsPermissions.Teach, ct); break;
            case "student": await access.AsStudent(LmsPermissions.Learn, ct); break;
            default: throw LmsError.Missing();
        }
    }
    internal bool CanJoin(OfferingInfo info, StudyInfo? s, Guid[] targets, Enrollment? own, DateTimeOffset now)
        => access.HasStudent(LmsPermissions.Enroll) && own is null && s?.Cohort.Active == true && info.Offering.Active && info.Course.Active &&
           AcademicRules.Eligible(info.Offering.Audience, info.Course.FacultyId, info.Offering.OwnerMajorId, s.Cohort.FacultyId, s.Cohort.MajorId,
               targets.Length == 0 || targets.Contains(s.Cohort.Id)) && AcademicRules.CanSee(now, info.Offering.PublishedUtc, info.Offering.VisibleUtc) &&
           !AcademicRules.IsClosed(info.Term, now) && AcademicRules.InWindow(now, info.Term.EnrollOpensUtc, info.Term.EnrollClosesUtc);

    public async Task<OfferingRow[]> ListAsync(OfferingFilter filter, CancellationToken ct)
    {
        if (filter.View == "preview") throw LmsError.Missing();
        await AuthorizeView(filter.View, ct);
        var search = LmsError.Text(filter.Search, 80, true);
        var query = Infos().Where(x => (filter.TermId == null || x.Term.Id == filter.TermId) &&
            (search == "" || x.Course.Title.Contains(search) || x.Offering.GroupCode.Contains(search)));
        var id = access.ActorId;
        var now = data.Now;
        var rows = await query.OrderByDescending(x => x.Term.StartsUtc).ThenBy(x => x.Course.Title).ThenBy(x => x.Offering.GroupCode).ToArrayAsync(ct);
        if (filter.View == "admin") rows = rows.Where(x => access.CanAdminOffering(LmsPermissions.Assign, x)).ToArray();
        if (filter.View == "teacher")
        {
            var taught = (await data.Read<CoTeacher>().Where(x => x.InstructorId == id && x.Active).Select(x => x.OfferingId).ToArrayAsync(ct)).ToHashSet();
            rows = rows.Where(x => x.Offering.LeadId == id || taught.Contains(x.Offering.Id)).ToArray();
        }
        var study = filter.View == "student" ? await access.ActiveStudy(ct) : null;
        var studentRows = filter.View == "student" ? await (from e in data.Read<Enrollment>() join s in data.Read<StudyRecord>() on e.StudyRecordId equals s.Id
                                                           where s.StudentId == id orderby (s.EndedUtc == null) descending, e.CreatedUtc descending select e).ToArrayAsync(ct) : [];
        var history = studentRows.ToLookup(x => x.OfferingId);
        var targets = (await data.Read<OfferingCohort>().ToArrayAsync(ct)).ToLookup(x => x.OfferingId, x => x.CohortId);
        var results = new List<(OfferingInfo Info, Guid[] Targets, Enrollment? Own)>();
        foreach (var r in rows)
        {
            var ids = targets[r.Offering.Id].ToArray();
            var enrollment = history[r.Offering.Id].FirstOrDefault();
            if (filter.CohortId.HasValue && ids.Length > 0 && !ids.Contains(filter.CohortId.Value)) continue;
            if (filter.View == "student")
            {
                if (filter.Mine && enrollment is null) continue;
                if (enrollment is null && (AcademicRules.IsClosed(r.Term, now) || study?.Cohort.Active != true || !r.Offering.Active || !r.Course.Active ||
                    !AcademicRules.CanSee(now, r.Offering.PublishedUtc, r.Offering.VisibleUtc) ||
                    !AcademicRules.Eligible(r.Offering.Audience, r.Course.FacultyId, r.Offering.OwnerMajorId, study.Cohort.FacultyId, study.Cohort.MajorId,
                        ids.Length == 0 || ids.Contains(study.Cohort.Id)))) continue;
            }
            results.Add((r, ids, enrollment));
            if (results.Count >= 500) break;
        }
        var leadIds = results.Select(x => x.Info.Offering.LeadId).Distinct().ToArray();
        var names = await data.Read<ApplicationUser>().Where(x => leadIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.DisplayName, ct);
        return results.Select(x => Row(x.Info, names.GetValueOrDefault(x.Info.Offering.LeadId, "—"), x.Targets, study, x.Own, now)).ToArray();
    }

    private OfferingRow Row(OfferingInfo info, string lead, Guid[] ids, StudyInfo? study, Enrollment? own, DateTimeOffset now)
    {
        var o = info.Offering; var c = info.Course; var t = info.Term;
        return new(o.Id, c.Id, t.Id, o.LeadId, o.OwnerMajorId, c.Title, o.GroupCode, $"{LmsError.Season(t.Season)} {t.Year}", lead,
            o.Audience, o.VisibleUtc, o.PublishedUtc, ids, o.Active, AcademicRules.IsClosed(t, now), own?.Id, own?.Status,
            CanJoin(info, study, ids, own, now), o.Active && own is not null && study?.Record.Id == own.StudyRecordId &&
            access.HasStudent(LmsPermissions.Withdraw) && AcademicRules.CheckExit(own.Status, false, t, now, null) == EnrollmentExitCheck.Change,
            t.DropClosesUtc);
    }

    public async Task<OfferingDetails> GetAsync(Guid offeringId, string view, bool includeContent, CancellationToken ct)
    {
        await AuthorizeView(view, ct);
        var info = await access.Offering(offeringId, ct);
        var now = data.Now;
        var closed = AcademicRules.IsClosed(info.Term, now);
        var teacher = view == "teacher";
        var admin = view == "admin";
        var preview = view == "preview";
        var isLead = (teacher || preview) && info.Offering.LeadId == access.ActorId;
        var study = view == "student" ? await access.ActiveStudy(ct) : null;
        var own = view == "student" ? await access.OwnEnrollment(offeringId, ct) : null;
        CoTeacher? co = null;
        if (admin && !access.CanAdminOffering(LmsPermissions.Assign, info)) throw LmsError.Missing();
        if ((teacher || preview) && !isLead) co = await access.CoAssignment(offeringId, ct) ?? throw LmsError.Missing();
        var visible = AcademicRules.CanSee(now, info.Offering.PublishedUtc, info.Offering.VisibleUtc);
        if (view == "student" && own is null && (!visible || closed || !await access.Eligible(info, study, ct))) throw LmsError.Missing();
        var lead = await data.Read<ApplicationUser>().Where(x => x.Id == info.Offering.LeadId).Select(x => x.DisplayName).SingleAsync(ct);
        var targets = await TargetIds(offeringId, ct);
        var chapters = includeContent && (teacher || preview && visible && info.Offering.Active ||
            visible && own?.Status == EnrollmentStatus.Active && info.Offering.Active && (closed || study?.Record.Id == own.StudyRecordId))
            ? await ChapterRows(offeringId, own, teacher, now, ct) : [];
        var coTeachers = admin || teacher ? await (from x in data.Read<CoTeacher>() join u in data.Read<ApplicationUser>() on x.InstructorId equals u.Id
                                                   where x.OfferingId == offeringId orderby u.DisplayName
                                                   select new CoTeacherRow(x.InstructorId, u.DisplayName, x.CanGrade, x.CanPublish, x.Active)).ToArrayAsync(ct) : [];
        var writable = !closed && info.Offering.Active;
        var hasChapters = teacher && await data.Read<Chapter>().AnyAsync(c => c.OfferingId == offeringId, ct);
        return new(Row(info, lead, targets, study, own, now), chapters, coTeachers,
            teacher && writable && isLead && access.HasTeacher(LmsPermissions.Content),
            teacher && writable && (isLead || co?.CanPublish == true) && access.HasTeacher(LmsPermissions.Publish),
            teacher && writable && (isLead || co?.CanGrade == true) && access.HasTeacher(LmsPermissions.Grade),
            teacher && writable && (isLead || co?.CanGrade == true) && access.HasTeacher(LmsPermissions.Release),
            isLead, preview || !writable || view == "student" && (own?.Status != EnrollmentStatus.Active || study?.Record.Id != own?.StudyRecordId), hasChapters);
    }

    internal Task<Guid[]> TargetIds(Guid id, CancellationToken ct)
        => data.Read<OfferingCohort>().Where(x => x.OfferingId == id).Select(x => x.CohortId).ToArrayAsync(ct);
    private async Task<ChapterRow[]> ChapterRows(Guid id, Enrollment? own, bool teacher, DateTimeOffset now, CancellationToken ct)
    {
        var chapters = await data.Read<Chapter>().Where(x => x.OfferingId == id && !x.Retired && (teacher ? !data.Read<Chapter>().Any(h => h.PendingBaseId == x.Id && !h.Retired) : x.PendingBaseId == null && x.Active && x.PublishedUtc != null))
            .OrderBy(x => x.Position).ToArrayAsync(ct);
        var chapterIds = chapters.Select(x => x.Id).ToArray();
        var lessons = await data.Read<Lesson>().Where(x => chapterIds.Contains(x.ChapterId) && (teacher || x.Active))
            .OrderBy(x => x.Position).ToArrayAsync(ct);
        var lessonIds = lessons.Select(x => x.Id).ToArray();
        var files = (await (from l in data.Read<LessonFile>() join m in data.Read<Media>() on l.MediaId equals m.Id
                            where lessonIds.Contains(l.LessonId)
                            orderby l.Position == null, l.Position, l.Id
                            select new { l.LessonId, File = new FileRow(m.Id, m.Name, m.Bytes, m.Extension) }).ToArrayAsync(ct))
            .ToLookup(x => x.LessonId, x => x.File);
        var done = own is null ? new HashSet<Guid>() : (await data.Read<LessonProgress>().Where(x => x.EnrollmentId == own.Id && x.ClearedUtc == null)
            .Select(x => x.LessonId).ToArrayAsync(ct)).ToHashSet();
        var byChapter = lessons.ToLookup(l => l.ChapterId);
        return chapters.Select(c => new ChapterRow(c.Id, c.Title, c.Position, c.PublishedUtc, c.Active,
            byChapter[c.Id].Select(l =>
            {
                var allowed = teacher || AcademicRules.CanReadLesson(now, c, l);
                return new LessonRow(l.Id, l.Title, l.Position, AcademicRules.Later(c.VisibleFromUtc, l.VisibleFromUtc), allowed ? l.Body : null,
                    allowed, done.Contains(l.StableLessonId ?? l.Id), l.Active, allowed ? files[l.Id].ToArray() : [], allowed ? l.Summary : null,
                    AcademicRules.Earlier(c.VisibleUntilUtc, l.VisibleUntilUtc), allowed ? l.CoverMediaId : null,
                    teacher ? l.TemplateItemKey : null, teacher && l.NeedsAvailabilityReview,
                    teacher ? l.VisibleFromUtc : null, teacher ? l.VisibleUntilUtc : null);
            }).ToArray(), c.VisibleFromUtc, c.VisibleUntilUtc, teacher ? c.TemplateItemKey : null, teacher || AcademicRules.CanReadChapter(now, c) ? c.Summary : null)).ToArray();
    }

    public async Task<Guid> SaveAsync(OfferingEdit input, CancellationToken ct)
    {
        await using var tx = await data.Transaction(ct);
        var (course, term, ids, group) = await Prepare(input, ct);
        var o = input.Id is { } id ? await data.Find<Offering>(id, ct) : new Offering { CourseId = course.Id, TermId = term.Id };
        if (input.Id.HasValue)
        {
            var old = await access.Offering(o.Id, ct);
            if (!access.CanAdminOffering(LmsPermissions.Assign, old)) throw LmsError.Missing();
            if (o.CourseId != course.Id || o.TermId != term.Id) throw LmsError.Conflict("درس و نیم‌سال ارائهٔ موجود تغییر نمی‌کنند.");
        }
        var existing = await data.Db.Set<OfferingCohort>().Where(x => x.OfferingId == o.Id).ToArrayAsync(ct);
        if (o.PublishedUtc.HasValue && (o.LeadId != input.LeadId || o.GroupCode != group || o.OwnerMajorId != input.OwnerMajorId ||
            o.Audience != input.Audience || !ids.ToHashSet().SetEquals(existing.Select(x => x.CohortId))))
            throw LmsError.Conflict("مخاطب، گروه و استاد مسئولِ ارائهٔ منتشرشده ثابت‌اند.");
        if (o.LeadId != input.LeadId && (await data.Read<Chapter>().AnyAsync(x => x.OfferingId == o.Id, ct) ||
            await data.Read<Assessment>().AnyAsync(x => x.OfferingId == o.Id, ct)))
            throw LmsError.Conflict("پس از ساخت محتوا یا تکلیف، تغییر مالکیت ارائه مجاز نیست.");
        o.LeadId = input.LeadId; o.OwnerMajorId = input.OwnerMajorId; o.GroupCode = group; o.Audience = input.Audience; o.Active = input.Active; o.Revision = Guid.NewGuid();
        if (input.Id is null) data.Db.Add(o);
        data.Db.RemoveRange(existing.Where(x => !ids.Contains(x.CohortId)));
        data.Db.AddRange(ids.Except(existing.Select(x => x.CohortId)).Select(c => new OfferingCohort { OfferingId = o.Id, CohortId = c }));
        return await data.Finish(tx, o.Id, o.Id, ct);
    }

    internal async Task<(CatalogCourse Course, Term Term, Guid[] Cohorts, string Group)> Prepare(OfferingEdit input, CancellationToken ct)
    {
        await access.AsEducation(LmsPermissions.Assign, ct);
        if (!Enum.IsDefined(input.Audience) || input.CohortIds is null || input.CohortIds.Length > 60)
            throw LmsError.Invalid("نوع مخاطب یا تعداد ورودی‌ها معتبر نیست.");
        var actor = await access.Load(ct);
        if (input.Audience == OfferingAudience.General && !actor.IsPlatformSuperAdmin) throw LmsError.Denied();
        if ((input.Audience == OfferingAudience.Specialized) != input.OwnerMajorId.HasValue)
            throw LmsError.Invalid("رشته فقط برای ارائهٔ تخصصی الزامی است.");
        var course = await data.Read<CatalogCourse>().SingleOrDefaultAsync(x => x.Id == input.CourseId, ct) ?? throw LmsError.Missing();
        var term = await data.Read<Term>().SingleOrDefaultAsync(x => x.Id == input.TermId, ct) ?? throw LmsError.Missing();
        if (input.Id is null && !course.MajorId.HasValue) throw LmsError.Invalid("رشتهٔ درس قدیمی را ابتدا در بخش درس‌ها مشخص کنید.");
        if (input.Id is null && input.Audience == OfferingAudience.Specialized && input.OwnerMajorId != course.MajorId)
            throw LmsError.Invalid("رشتهٔ ارائهٔ تخصصی باید همان رشتهٔ درس باشد.");
        access.DemandScope(LmsPermissions.Assign, course.FacultyId, input.OwnerMajorId);
        LmsData.Open(term, data.Now);
        if (!course.Active || !await data.Read<Faculty>().AnyAsync(x => x.Id == course.FacultyId && x.Active, ct))
            throw LmsError.Conflict("درس یا دانشکده غیرفعال است.");
        if (input.OwnerMajorId is { } owner && !await data.Read<Major>().AnyAsync(x => x.Id == owner && x.FacultyId == course.FacultyId && x.Active, ct))
            throw LmsError.Invalid("رشته به دانشکدهٔ درس تعلق ندارد یا فعال نیست.");
        await ValidateInstructor(input.LeadId, course.FacultyId, input.OwnerMajorId, actor.IsPlatformSuperAdmin, ct);
        var ids = input.CohortIds.Distinct().ToArray();
        var cohorts = await access.Cohorts().Where(x => ids.Contains(x.Id)).ToArrayAsync(ct);
        if (cohorts.Length != ids.Length || cohorts.Any(x => !x.Active || !AcademicRules.Eligible(input.Audience,
                course.FacultyId, input.OwnerMajorId, x.FacultyId, x.MajorId, true)))
            throw LmsError.Invalid("ورودی خارج از مخاطب یا غیرفعال است.");
        var group = LmsError.Code(input.GroupCode);
        if (await data.Read<Offering>().AnyAsync(x => x.Id != input.Id && x.CourseId == course.Id && x.TermId == term.Id && x.GroupCode == group, ct))
            throw LmsError.Conflict("کد گروه برای این درس و نیم‌سال تکراری است.");
        return (course, term, ids, group);
    }

    public async Task<LmsTableSlice<OfferingRow>> AdminPage(OfferingFilter filter, LmsTablePage page, CancellationToken ct)
    {
        await access.AsEducation(LmsPermissions.Assign, ct);
        var actor = await access.Load(ct);
        var scope = access.EducationScopes(LmsPermissions.Assign);
        var faculties = scope.Faculties; var majors = scope.Majors;
        var q = LmsError.Text(filter.Search, 80, true);
        var query = Infos().Where(x => (filter.TermId == null || x.Term.Id == filter.TermId) &&
            (q == "" || x.Course.Title.Contains(q) || x.Course.Code.Contains(q) || x.Offering.GroupCode.Contains(q)) &&
            (filter.CohortId == null || !data.Read<OfferingCohort>().Any(c => c.OfferingId == x.Offering.Id) ||
             data.Read<OfferingCohort>().Any(c => c.OfferingId == x.Offering.Id && c.CohortId == filter.CohortId)));
        if (!actor.IsPlatformSuperAdmin)
            query = query.Where(x => x.Offering.Audience != OfferingAudience.General && (faculties.Contains(x.Course.FacultyId) ||
                 x.Offering.OwnerMajorId != null && majors.Contains(x.Offering.OwnerMajorId.Value)));
        var (actual, size, total) = await AcademicPaging.Bounds(query, page, ct);
        var rows = await query.OrderByDescending(x => x.Term.StartsUtc).ThenBy(x => x.Course.Title).ThenBy(x => x.Offering.GroupCode).ThenBy(x => x.Offering.Id)
            .Skip((actual - 1) * size).Take(size).ToArrayAsync(ct);
        var ids = rows.Select(x => x.Offering.Id).ToArray();
        var targets = (await data.Read<OfferingCohort>().Where(x => ids.Contains(x.OfferingId)).ToArrayAsync(ct)).ToLookup(x => x.OfferingId, x => x.CohortId);
        var leadIds = rows.Select(x => x.Offering.LeadId).Distinct().ToArray();
        var names = await data.Read<ApplicationUser>().Where(x => leadIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.DisplayName, ct);
        return new(rows.Select(x => Row(x, names.GetValueOrDefault(x.Offering.LeadId, "—"), targets[x.Offering.Id].ToArray(), null, null, data.Now)).ToArray(), total, actual, size);
    }

    internal async Task ValidateInstructor(Guid id, Guid faculty, Guid? major, bool platform, CancellationToken ct)
    {
        var candidate = await (from t in data.Read<InstructorProfile>() join m in data.Read<Major>() on t.MajorId equals m.Id
                               join u in data.Read<ApplicationUser>() on t.Id equals u.Id
                               join f in data.Read<Faculty>() on m.FacultyId equals f.Id
                               where t.Id == id && t.Active && u.IsActive && m.Active && f.Active select m).SingleOrDefaultAsync(ct);
        if (candidate is null || !platform && (candidate.FacultyId != faculty || major is not null && candidate.Id != major))
            throw LmsError.Invalid("استاد فعال و مجاز برای این قلمرو انتخاب کنید.");
    }
    public async Task<Guid> CoTeachAsync(Guid offeringId, CoTeacherEdit input, CancellationToken ct)
    {
        await using var tx = await data.Transaction(ct);
        var actor = await access.Load(ct);
        await access.AsEducation(LmsPermissions.Assign, ct);
        var i = await access.Offering(offeringId, ct);
        if (!access.CanAdminOffering(LmsPermissions.Assign, i)) throw LmsError.Missing();
        LmsData.Open(i.Term, data.Now);
        if (input.InstructorId == i.Offering.LeadId) throw LmsError.Invalid("استاد مسئول از پیش در ارائه است.");
        var row = await data.Db.Set<CoTeacher>().SingleOrDefaultAsync(x => x.OfferingId == offeringId && x.InstructorId == input.InstructorId, ct);
        if (input.Active) await ValidateInstructor(input.InstructorId, i.Course.FacultyId, i.Offering.OwnerMajorId, actor.IsPlatformSuperAdmin, ct);
        if (row is null)
        {
            if (!input.Active) throw LmsError.Missing();
            row = new() { OfferingId = offeringId, InstructorId = input.InstructorId }; data.Db.Add(row);
        }
        row.CanGrade = input.CanGrade; row.CanPublish = input.CanPublish; row.Active = input.Active;
        return await data.Finish(tx, row.Id, offeringId, ct);
    }
    internal async Task<OfferingInfo> Editable(Guid id, string permission, CancellationToken ct)
    {
        var info = await access.Offering(id, ct);
        await access.AsTeaching(info, permission, ct);
        LmsData.Open(info.Term, data.Now);
        if (!info.Offering.Active) throw LmsError.Conflict("ارائه بایگانی شده است.");
        return info;
    }
    public async Task<bool> ScheduleAsync(Guid offeringId, ScheduleEdit input, CancellationToken ct)
    {
        await using var tx = await data.Transaction(ct);
        var info = await Editable(offeringId, LmsPermissions.Content, ct);
        var when = LmsError.Instant(input.VisibleUtc);
        if (when >= info.Term.EnrollClosesUtc || (info.Offering.PublishedUtc.HasValue && info.Offering.VisibleUtc <= data.Now && info.Offering.VisibleUtc != when))
            throw LmsError.Conflict("تاریخ دیده‌شدن باید قبل از پایان اخذ و پیش از دیده‌شدن ارائه تنظیم شود.");
        var o = await data.Find<Offering>(offeringId, ct);
        o.VisibleUtc = when; o.Revision = Guid.NewGuid();
        return await data.Finish(tx, true, o.Id, ct);
    }
    public async Task<bool> PublishAsync(Guid offeringId, CancellationToken ct)
    {
        await using var tx = await data.Transaction(ct);
        var info = await Editable(offeringId, LmsPermissions.Publish, ct);
        var now = data.Now;
        if (info.Offering.VisibleUtc is not { } v || v >= info.Term.EnrollClosesUtc ||
            (info.Offering.PublishedUtc is null && now >= info.Term.EnrollClosesUtc))
            throw LmsError.Conflict("انتشار اولیه به تاریخ نمایش و مهلت اخذ معتبر نیاز دارد.");
        await ValidateInstructor(info.Offering.LeadId, info.Course.FacultyId, info.Offering.OwnerMajorId, true, ct);
        var o = await data.Find<Offering>(offeringId, ct);
        if (o.PublishedUtc is null) { o.PublishedUtc = now; o.Revision = Guid.NewGuid(); }
        return await data.Finish(tx, true, o.Id, ct);
    }
    public async Task<Guid> SaveSectionAsync(Guid offeringId, string kind, SectionEdit input, CancellationToken ct)
    {
        await using var tx = await data.Transaction(ct);
        var info = await Editable(offeringId, LmsPermissions.Content, ct);
        var opens = input.OpensUtc is { } start ? LmsError.Instant(start) : (DateTimeOffset?)null;
        var closes = input.ClosesUtc is { } end ? LmsError.Instant(end) : (DateTimeOffset?)null;
        GuardWindow(info.Term, opens, closes);
        Guid result;
        if (kind == "chapter")
        {
            var chapter = input.Id is { } id ? await FindChapter(offeringId, id, ct) : new Chapter { OfferingId = offeringId };
            RequireDraft(chapter);
            var children = await data.Read<Lesson>().Where(l => l.ChapterId == chapter.Id && l.Active).ToArrayAsync(ct);
            foreach (var lesson in children) GuardChildWindow(opens, closes, lesson.VisibleFromUtc, lesson.VisibleUntilUtc);
            chapter.Title = LmsError.Text(input.Title, 180);
            chapter.Position = Position(input.Position);
            chapter.Active = input.Active;
            chapter.VisibleFromUtc = opens;
            chapter.VisibleUntilUtc = closes;
            if (input.Id is null) data.Db.Add(chapter);
            result = chapter.Id;
        }
        else if (kind is "lesson" or "legacy-availability")
        {
            var chapter = await FindChapter(offeringId, LmsError.Id(input.ParentId), ct);
            var lesson = input.Id is { } id ? await data.Find<Lesson>(id, ct) : new Lesson { ChapterId = chapter.Id };
            if (lesson.ChapterId != chapter.Id) throw LmsError.Missing();
            GuardChildWindow(chapter.VisibleFromUtc, chapter.VisibleUntilUtc, opens, closes);
            if (kind == "legacy-availability")
            {
                if (input.Id is null || !lesson.NeedsAvailabilityReview || !lesson.Active || chapter.PublishedUtc is null || chapter.Retired)
                    throw LmsError.Conflict("این درسک منتظر بازبینی دسترسی نیست.");
            }
            else
            {
                RequireDraft(chapter);
                lesson.Title = LmsError.Text(input.Title, 180);
                lesson.Position = Position(input.Position);
                lesson.Body = LmsError.Text(input.Body, 32000, true);
                lesson.Active = input.Active;
            }
            lesson.VisibleFromUtc = opens;
            lesson.VisibleUntilUtc = closes;
            lesson.NeedsAvailabilityReview = false;
            if (input.Id is null) data.Db.Add(lesson);
            result = lesson.Id;
        }
        else throw LmsError.Missing();
        (await data.Find<Offering>(offeringId, ct)).Revision = Guid.NewGuid();
        return await data.Finish(tx, result, result, ct);
    }

    internal static int Position(int value) => value is >= 1 and <= 1000 ? value
        : throw LmsError.Invalid("ترتیب باید بین ۱ تا ۱۰۰۰ باشد.");

    internal static void GuardWindow(Term term, DateTimeOffset? opens, DateTimeOffset? closes)
    {
        if (opens < term.StartsUtc || opens >= term.EndsUtc || closes <= term.StartsUtc || closes > term.EndsUtc || closes <= opens)
            throw LmsError.Invalid("بازهٔ نمایش باید معتبر و در محدودهٔ نیم‌سال باشد.");
    }

    internal static void GuardChildWindow(DateTimeOffset? parentStart, DateTimeOffset? parentEnd, DateTimeOffset? start, DateTimeOffset? end)
    {
        if (start < parentStart || end > parentEnd ||
            AcademicRules.Earlier(parentEnd, end) <= AcademicRules.Later(parentStart, start))
            throw LmsError.Invalid("بازهٔ نمایش درسک باید در بازهٔ فصل قرار بگیرد.");
    }

    internal async Task<Chapter> FindChapter(Guid offeringId, Guid chapterId, CancellationToken ct)
        => await data.Db.Set<Chapter>().SingleOrDefaultAsync(x => x.Id == chapterId && x.OfferingId == offeringId, ct) ?? throw LmsError.Missing();
    internal static void RequireDraft(Chapter c)
    {
        if (c.PublishedUtc is not null || c.WorkspaceManaged || c.Retired) throw LmsError.Conflict("تغییر این فصل فقط از نمای نسخه‌بندی‌شدهٔ محتوا ممکن است.");
    }
    public async Task<bool> PublishChapterAsync(Guid offeringId, Guid chapterId, CancellationToken ct)
    {
        await using var tx = await data.Transaction(ct);
        var info = await Editable(offeringId, LmsPermissions.Publish, ct);
        var c = await FindChapter(offeringId, chapterId, ct);
        if (c.PublishedUtc is not null) return true;
        RequireDraft(c);
        if (!c.Active) throw LmsError.Conflict("ابتدا فصل را فعال کنید.");
        var lessons = await data.Read<Lesson>().Where(x => x.ChapterId == c.Id && x.Active).ToArrayAsync(ct);
        if (lessons.Length == 0 || lessons.Any(l => l.NeedsAvailabilityReview))
            throw LmsError.Conflict("فصل به درسک فعال و بازبینی‌شده نیاز دارد؛ ایجاد جلسه الزامی نیست.");
        GuardWindow(info.Term, c.VisibleFromUtc, c.VisibleUntilUtc);
        var files = data.Read<LessonFile>();
        var emptyIds = lessons.Where(l => string.IsNullOrWhiteSpace(l.Body)).Select(l => l.Id).ToArray();
        if (emptyIds.Length != 0 && await files.Where(f => emptyIds.Contains(f.LessonId)).Select(f => f.LessonId).Distinct().CountAsync(ct) != emptyIds.Length)
            throw LmsError.Conflict("هر درسک فعال باید متن یا پیوست داشته باشد.");
        foreach (var lesson in lessons)
        {
            GuardWindow(info.Term, lesson.VisibleFromUtc, lesson.VisibleUntilUtc);
            GuardChildWindow(c.VisibleFromUtc, c.VisibleUntilUtc, lesson.VisibleFromUtc, lesson.VisibleUntilUtc);
        }
        c.PublishedUtc = data.Now;
        (await data.Find<Offering>(offeringId, ct)).Revision = Guid.NewGuid();
        return await data.Finish(tx, true, c.Id, ct);
    }
}
