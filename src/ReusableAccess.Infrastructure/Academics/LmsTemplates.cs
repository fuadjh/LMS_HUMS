using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ReusableAccess.Contracts;
using ReusableAccess.Domain.Academics;

namespace ReusableAccess.Infrastructure.Academics;

internal sealed partial class LmsOfferings
{
    private sealed record SavedLesson(string Title, string Body, int Position, Guid[] MediaIds);
    private sealed record SavedAssessment(string Title, string Prompt, string Rubric, int MaxAttempts, decimal MaxScore, QuizQuestionRow[]? Quiz);
    private sealed record SavedChapter(string Title, int Position, SavedLesson[] Lessons, SavedAssessment[] Assessments);
    private sealed record SavedCourse(int Format, SavedChapter[] Chapters);

    public async Task<TemplateRow[]> TemplatesAsync(CancellationToken ct)
    {
        await access.AsTeacher(LmsPermissions.Template, ct);
        return await (from t in data.Read<CourseTemplate>() join v in data.Read<TemplateVersion>() on t.Id equals v.TemplateId
                      where t.OwnerId == access.ActorId && t.Active orderby v.CreatedUtc descending
                      select new TemplateRow(t.Id, t.CourseId, v.Id, v.Number, t.Title, v.CreatedUtc)).Take(500).ToArrayAsync(ct);
    }
    public async Task<Guid> SaveTemplateAsync(Guid offeringId, TemplateSave input, CancellationToken ct)
    {
        await using var tx = await data.Transaction(ct);
        var i = await access.Offering(offeringId, ct);
        await access.AsTeaching(i, LmsPermissions.Template, ct); // read-only source may be from a closed term
        var title = LmsError.Text(input.Title, 180);
        var template = input.TemplateId is { } id ? await data.Find<CourseTemplate>(id, ct)
            : new CourseTemplate { OwnerId = access.ActorId, CourseId = i.Course.Id, Title = title, Active = true };
        if (template.OwnerId != access.ActorId || template.CourseId != i.Course.Id || !template.Active) throw LmsError.Missing();
        var content = await ChapterRows(offeringId, null, true, true, data.Now, ct);
        if (!content.Any(x => x.Active)) throw LmsError.Conflict("ارائه فصل فعال برای ذخیره ندارد.");
        var snapshot = JsonSerializer.Serialize(new SavedCourse(1, content.Where(c => c.Active).Select(c => new SavedChapter(c.Title, c.Position,
            c.Lessons.Where(l => l.Active).Select(l => new SavedLesson(l.Title, l.Body ?? "", l.Position, l.Files.Select(f => f.Id).ToArray())).ToArray(),
            c.Assessments.Where(a => a.Active).Select(a => new SavedAssessment(a.Title, a.Prompt ?? "", a.Rubric ?? "", a.MaxAttempts, a.MaxScore, a.Quiz)).ToArray())).ToArray()));
        if (snapshot.Length > 4_000_000) throw LmsError.Conflict("طول محتوای قالب بیش از حد مجاز است.");
        if (input.TemplateId is null) data.Db.Add(template);
        var number = (await data.Read<TemplateVersion>().Where(x => x.TemplateId == template.Id).MaxAsync(x => (int?)x.Number, ct) ?? 0) + 1;
        var version = new TemplateVersion { TemplateId = template.Id, Number = number, CreatedUtc = data.Now, Snapshot = snapshot };
        data.Db.Add(version);
        return await data.Finish(tx, version.Id, version.Id, ct);
    }
    public async Task<bool> ApplyTemplateAsync(Guid offeringId, TemplateApply input, CancellationToken ct)
    {
        await using var tx = await data.Transaction(ct);
        var info = await Editable(offeringId, LmsPermissions.Template, ct);
        await access.AsTeacher(LmsPermissions.Content, ct);
        var version = await (from t in data.Read<CourseTemplate>() join v in data.Read<TemplateVersion>() on t.Id equals v.TemplateId
                             where t.Active && t.OwnerId == access.ActorId && t.CourseId == info.Course.Id && v.Id == input.VersionId select v).SingleOrDefaultAsync(ct)
             ?? throw LmsError.Missing();
        if (info.Offering.PublishedUtc is not null || await data.Read<Chapter>().AnyAsync(x => x.OfferingId == offeringId, ct))
            throw LmsError.Conflict("استفاده از قالب فقط برای ارائهٔ خالی و منتشرنشده مجاز است.");
        var draft = JsonSerializer.Deserialize<SavedCourse>(version.Snapshot);
        if (draft?.Format != 1) throw LmsError.Conflict("نسخهٔ قالب قابل خواندن نیست.");
        foreach (var ch in draft.Chapters)
        {
            var c = new Chapter { OfferingId = offeringId, Title = ch.Title, Position = ch.Position, Active = true };
            data.Db.Add(c);
            foreach (var l in ch.Lessons)
            {
                var row = new Lesson { ChapterId = c.Id, Title = l.Title, Body = l.Body, Position = l.Position, Active = true };
                data.Db.Add(row);
                data.Db.AddRange(l.MediaIds.Distinct().Select(m => new LessonFile { LessonId = row.Id, MediaId = m }));
            }
            data.Db.AddRange(ch.Assessments.Select(a => new Assessment
            { ChapterId = c.Id, Title = a.Title, Prompt = a.Prompt, Rubric = a.Rubric, MaxAttempts = a.MaxAttempts, MaxScore = a.MaxScore, QuizSpec = a.Quiz is null ? null : JsonSerializer.Serialize(a.Quiz), Active = true }));
        }
        return await data.Finish(tx, true, offeringId, ct);
    }
}
