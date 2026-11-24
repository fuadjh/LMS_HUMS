using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ReusableAccess.Application.Academics;
using ReusableAccess.Contracts;
using ReusableAccess.Domain.Academics;
using ReusableAccess.Infrastructure.Identity;

namespace ReusableAccess.Infrastructure.Academics;

internal sealed partial class LmsOfferings(LmsData data, LmsAccess access) : ILmsOfferings
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

    public async Task<OfferingDetails> GetAsync(Guid offeringId, string view, CancellationToken ct)
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
        var chapters = teacher || (preview && visible) || (visible && own?.Status == EnrollmentStatus.Active && info.Offering.Active && (closed || study?.Record.Id == own.StudyRecordId))
            ? await ChapterRows(offeringId, own, teacher, isLead && teacher, now, ct) : [];
        var sessions = teacher ? await data.Read<LessonSession>().Where(x => x.OfferingId == offeringId).OrderBy(x => x.OpensUtc)
            .Select(x => new SessionRow(x.Id, x.Title, x.OpensUtc, x.Active)).ToArrayAsync(ct) : [];
        var coTeachers = admin || teacher ? await (from x in data.Read<CoTeacher>() join u in data.Read<ApplicationUser>() on x.InstructorId equals u.Id
                                                   where x.OfferingId == offeringId orderby u.DisplayName
                                                   select new CoTeacherRow(x.InstructorId, u.DisplayName, x.CanGrade, x.CanPublish, x.Active)).ToArrayAsync(ct) : [];
        var writable = !closed && info.Offering.Active;
        return new(Row(info, lead, targets, study, own, now), chapters, sessions, coTeachers,
            teacher && writable && isLead && access.HasTeacher(LmsPermissions.Content),
            teacher && writable && (isLead || co?.CanPublish == true) && access.HasTeacher(LmsPermissions.Publish),
            teacher && writable && (isLead || co?.CanGrade == true) && access.HasTeacher(LmsPermissions.Grade),
            teacher && writable && (isLead || co?.CanGrade == true) && access.HasTeacher(LmsPermissions.Release),
            isLead, preview || !writable || view == "student" && (own?.Status != EnrollmentStatus.Active || study?.Record.Id != own?.StudyRecordId));
    }

    internal Task<Guid[]> TargetIds(Guid id, CancellationToken ct)
        => data.Read<OfferingCohort>().Where(x => x.OfferingId == id).Select(x => x.CohortId).ToArrayAsync(ct);
    private async Task<ChapterRow[]> ChapterRows(Guid id, Enrollment? own, bool teacher, bool includeQuizKey, DateTimeOffset now, CancellationToken ct)
    {
        var chapters = await data.Read<Chapter>().Where(x => x.OfferingId == id && (teacher || x.Active && x.PublishedUtc != null)).OrderBy(x => x.Position).ToArrayAsync(ct);
        var chapterIds = chapters.Select(x => x.Id).ToArray();
        var sessions = await data.Read<LessonSession>().Where(x => x.OfferingId == id && (teacher || x.Active)).ToDictionaryAsync(x => x.Id, ct);
        var lessons = await data.Read<Lesson>().Where(x => chapterIds.Contains(x.ChapterId) && (teacher || x.Active)).OrderBy(x => x.Position).ToArrayAsync(ct);
        var lessonIds = lessons.Select(x => x.Id).ToArray();
        var files = (await (from l in data.Read<LessonFile>() join m in data.Read<Media>() on l.MediaId equals m.Id where lessonIds.Contains(l.LessonId)
                            select new { l.LessonId, File = new FileRow(m.Id, m.Name, m.Bytes, m.Extension) }).ToArrayAsync(ct)).ToLookup(x => x.LessonId, x => x.File);
        var done = own is null ? new HashSet<Guid>() : (await data.Read<LessonProgress>().Where(x => x.EnrollmentId == own.Id).Select(x => x.LessonId).ToArrayAsync(ct)).ToHashSet();
        var assessments = await data.Read<Assessment>().Where(x => chapterIds.Contains(x.ChapterId) && (teacher || x.Active && x.DueUtc != null && x.CutoffUtc != null)).OrderBy(x => x.DueUtc).ToArrayAsync(ct);
        return chapters.Select(c => new ChapterRow(c.Id, c.Title, c.Position, c.PublishedUtc, c.Active,
            lessons.Where(l => l.ChapterId == c.Id).Select(l =>
            {
                DateTimeOffset? opens = l.SessionId is { } sid ? sessions.GetValueOrDefault(sid)?.OpensUtc : null;
                var allowed = teacher || AcademicRules.CanReadLesson(now, c.PublishedUtc, opens);
                return new LessonRow(l.Id, l.Title, l.Position, l.SessionId, opens, allowed ? l.Body : null, allowed, done.Contains(l.Id), l.Active,
                    allowed ? files[l.Id].ToArray() : []);
            }).ToArray(), assessments.Where(a => a.ChapterId == c.Id).Select(a => new AssessmentRow(a.Id, c.Id, a.Title, a.Prompt, a.Rubric,
                a.DueUtc, a.CutoffUtc, a.MaxAttempts, a.MaxScore, a.Active, QuizRows(a, includeQuizKey))).ToArray())).ToArray();
    }

    public async Task<Guid> SaveAsync(OfferingEdit input, CancellationToken ct)
    {
        await using var tx = await data.Transaction(ct);
        await access.AsEducation(LmsPermissions.Assign, ct);
        if (!Enum.IsDefined(input.Audience) || input.CohortIds is null || input.CohortIds.Length > 60)
            throw LmsError.Invalid("نوع مخاطب یا تعداد ورودی‌ها معتبر نیست.");
        var actor = await access.Load(ct);
        if (input.Audience == OfferingAudience.General && !actor.IsPlatformSuperAdmin) throw LmsError.Denied();
        if ((input.Audience == OfferingAudience.Specialized) != input.OwnerMajorId.HasValue)
            throw LmsError.Invalid("رشته فقط برای ارائهٔ تخصصی الزامی است.");
        var course = await data.Find<CatalogCourse>(input.CourseId, ct);
        var term = await data.Find<Term>(input.TermId, ct);
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
        if (o.LeadId != input.LeadId && await data.Read<Chapter>().AnyAsync(x => x.OfferingId == o.Id, ct))
            throw LmsError.Conflict("پس از ساخت محتوا، تغییر مالکیت ارائه مجاز نیست.");
        o.LeadId = input.LeadId; o.OwnerMajorId = input.OwnerMajorId; o.GroupCode = group; o.Audience = input.Audience; o.Active = input.Active; o.Revision = Guid.NewGuid();
        if (input.Id is null) data.Db.Add(o);
        data.Db.RemoveRange(existing.Where(x => !ids.Contains(x.CohortId)));
        data.Db.AddRange(ids.Except(existing.Select(x => x.CohortId)).Select(c => new OfferingCohort { OfferingId = o.Id, CohortId = c }));
        return await data.Finish(tx, o.Id, o.Id, ct);
    }

    private async Task ValidateInstructor(Guid id, Guid faculty, Guid? major, bool platform, CancellationToken ct)
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
    internal static bool Publishable(Assessment a, Term t) => a.DueUtc is not null && a.CutoffUtc is not null && a.DueUtc <= a.CutoffUtc && a.CutoffUtc <= t.EndsUtc;

    public async Task<Guid> SaveSectionAsync(Guid offeringId, string kind, SectionEdit input, CancellationToken ct)
    {
        await using var tx = await data.Transaction(ct);
        var i = await Editable(offeringId, LmsPermissions.Content, ct);
        var title = LmsError.Text(input.Title, 180);
        if (input.Position is < 1 or > 1000) throw LmsError.Invalid("ترتیب باید بین ۱ تا ۱۰۰۰ باشد.");
        Guid result;
        switch (kind)
        {
            case "chapter":
            {
                var c = input.Id is { } id ? await data.Find<Chapter>(id, ct) : new Chapter { OfferingId = offeringId };
                if (c.OfferingId != offeringId) throw LmsError.Missing();
                RequireDraft(c);
                c.Title = title; c.Position = input.Position; c.Active = input.Active;
                if (input.Id is null) data.Db.Add(c);
                result = c.Id; break;
            }
            case "session":
            {
                var s = input.Id is { } id ? await data.Find<LessonSession>(id, ct) : new LessonSession { OfferingId = offeringId };
                if (s.OfferingId != offeringId) throw LmsError.Missing();
                var when = LmsError.Instant(input.OpensUtc ?? throw LmsError.Invalid("زمان بازشدن جلسه الزامی است."));
                if (when < i.Term.StartsUtc || when >= i.Term.EndsUtc) throw LmsError.Invalid("زمان جلسه باید داخل نیم‌سال باشد.");
                var linked = await (from l in data.Read<Lesson>() join c in data.Read<Chapter>() on l.ChapterId equals c.Id
                                    where l.SessionId == s.Id && c.PublishedUtc != null && c.Active && l.Active select l.Id).AnyAsync(ct);
                if (linked && (!input.Active || s.OpensUtc <= data.Now && s.OpensUtc != when))
                    throw LmsError.Conflict("جلسهٔ منتشرشده قابل بایگانی نیست و پس از بازشدن زمان آن ثابت می‌ماند.");
                s.Title = title; s.OpensUtc = when; s.Active = input.Active;
                if (input.Id is null) data.Db.Add(s);
                result = s.Id; break;
            }
            case "lesson":
            {
                var c = await FindChapter(offeringId, LmsError.Id(input.ParentId), ct);
                RequireDraft(c);
                if (input.SessionId is { } s && !await data.Read<LessonSession>().AnyAsync(x => x.Id == s && x.OfferingId == offeringId && x.Active, ct))
                    throw LmsError.Invalid("جلسه باید فعال و متعلق به همین ارائه باشد.");
                var l = input.Id is { } id ? await data.Find<Lesson>(id, ct) : new Lesson { ChapterId = c.Id };
                if (l.ChapterId != c.Id) throw LmsError.Missing();
                l.Title = title; l.Position = input.Position; l.Body = LmsError.Text(input.Body, 32000, true); l.SessionId = input.SessionId; l.Active = input.Active;
                if (input.Id is null) data.Db.Add(l);
                result = l.Id; break;
            }
            case "assessment":
            {
                var c = await FindChapter(offeringId, LmsError.Id(input.ParentId), ct);
                RequireDraft(c);
                var a = input.Id is { } id ? await data.Find<Assessment>(id, ct) : new Assessment { ChapterId = c.Id };
                if (a.ChapterId != c.Id) throw LmsError.Missing();
                var due = LmsError.Instant(input.DueUtc ?? throw LmsError.Invalid("موعد عادی تکلیف الزامی است."));
                var cutoff = LmsError.Instant(input.CutoffUtc ?? due);
                if (due < i.Term.StartsUtc || due >= i.Term.EndsUtc || cutoff < due || cutoff > i.Term.EndsUtc ||
                    input.MaxAttempts is < 1 or > 20 || input.MaxScore is <= 0 or > 1000 || decimal.Round(input.MaxScore, 2) != input.MaxScore)
                    throw LmsError.Invalid("موعد، تعداد تلاش یا سقف نمره معتبر نیست.");
                a.Title = title; a.Prompt = LmsError.Text(input.Body, 16000); a.Rubric = LmsError.Text(input.Rubric, 8000, true);
                a.QuizSpec = ValidateQuiz(input.Quiz);
                a.DueUtc = due; a.CutoffUtc = cutoff; a.MaxAttempts = input.MaxAttempts; a.MaxScore = input.MaxScore; a.Active = input.Active;
                if (input.Id is null) data.Db.Add(a);
                result = a.Id; break;
            }
            default: throw LmsError.Missing();
        }
        return await data.Finish(tx, result, result, ct);
    }
    internal static QuizQuestionRow[]? QuizRows(Assessment a, bool includeQuizKey)
    {
        if (a.QuizSpec is null) return null;
        var questions = JsonSerializer.Deserialize<QuizQuestionRow[]>(a.QuizSpec) ?? throw LmsError.Conflict("ساختار آزمون قابل خواندن نیست.");
        return includeQuizKey ? questions : questions.Select(q => q with { CorrectIndex = null }).ToArray();
    }
    private static string? ValidateQuiz(QuizQuestionRow[]? questions)
    {
        if (questions is null || questions.Length == 0) return null;
        if (questions.Length > 30) throw LmsError.Invalid("آزمون حداکثر ۳۰ سؤال دارد.");
        var normalized = new List<QuizQuestionRow>();
        foreach (var q in questions)
        {
            if (q is null || q.Choices is null || q.Choices.Length != 4 || q.CorrectIndex is null or < 0 or > 3)
                throw LmsError.Invalid("هر سؤال باید چهار گزینه و یک پاسخ درست داشته باشد.");
            var options = q.Choices.Select(x => LmsError.Text(x, 500)).ToArray();
            if (options.Distinct().Count() != 4) throw LmsError.Invalid("گزینه‌های یک سؤال باید متفاوت باشند.");
            normalized.Add(new(LmsError.Text(q.Prompt, 1000), options, q.CorrectIndex));
        }
        return JsonSerializer.Serialize(normalized);
    }
    internal async Task<Chapter> FindChapter(Guid offeringId, Guid chapterId, CancellationToken ct)
        => await data.Db.Set<Chapter>().SingleOrDefaultAsync(x => x.Id == chapterId && x.OfferingId == offeringId, ct) ?? throw LmsError.Missing();
    internal static void RequireDraft(Chapter c)
    {
        if (c.PublishedUtc is not null) throw LmsError.Conflict("فصل منتشرشده ثابت است؛ فصل تازه‌ای بسازید.");
    }
    public async Task<bool> PublishChapterAsync(Guid offeringId, Guid chapterId, CancellationToken ct)
    {
        await using var tx = await data.Transaction(ct);
        var info = await Editable(offeringId, LmsPermissions.Publish, ct);
        var c = await FindChapter(offeringId, chapterId, ct);
        if (c.PublishedUtc is not null) return true;
        if (!c.Active) throw LmsError.Conflict("ابتدا فصل را فعال کنید.");
        var lessons = await data.Read<Lesson>().Where(x => x.ChapterId == c.Id && x.Active).ToArrayAsync(ct);
        var scheduled = (await data.Read<LessonSession>().Where(x => x.OfferingId == offeringId && x.Active).Select(x => x.Id).ToArrayAsync(ct)).ToHashSet();
        if (lessons.Length == 0 || lessons.Any(x => x.SessionId is null || !scheduled.Contains(x.SessionId.Value)))
            throw LmsError.Conflict("فصل باید درسک فعال با جلسهٔ زمان‌بندی‌شده داشته باشد.");
        var assignments = await data.Read<Assessment>().Where(x => x.ChapterId == c.Id && x.Active).ToArrayAsync(ct);
        if (assignments.Any(x => !Publishable(x, info.Term) || x.CutoffUtc <= data.Now))
            throw LmsError.Conflict("مهلت تکلیف‌های فصل را پیش از انتشار تنظیم کنید.");
        c.PublishedUtc = data.Now;
        return await data.Finish(tx, true, c.Id, ct);
    }
}
