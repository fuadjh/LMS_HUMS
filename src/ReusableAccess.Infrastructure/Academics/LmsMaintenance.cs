using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ReusableAccess.Contracts;
using ReusableAccess.Domain.Academics;
using ReusableAccess.Domain.Entities;
using ReusableAccess.Infrastructure.Identity;

namespace ReusableAccess.Infrastructure.Academics;

internal sealed class LmsMaintenance(IServiceScopeFactory provider, ILogger<LmsMaintenance> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(2));
        try
        {
            do
            {
                try { await Run(stoppingToken); }
                catch (Exception) when (!stoppingToken.IsCancellationRequested)
                { log.LogWarning("LMS maintenance did not finish; check SQL Server, migrations and storage health."); }
            } while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
    private async Task Run(CancellationToken ct)
    {
        await using var scope = provider.CreateAsyncScope();
        var data = scope.ServiceProvider.GetRequiredService<LmsData>();
        var calendar = scope.ServiceProvider.GetRequiredService<LmsCalendar>();
        var files = scope.ServiceProvider.GetRequiredService<LmsFiles>();
        var sms = scope.ServiceProvider.GetRequiredService<LmsSms>();
        var now = data.Now;
        await Cleanup(data, files, now, ct);
        var until = now.AddHours(24);
        var active = await (from o in data.Read<Offering>() join t in data.Read<Term>() on o.TermId equals t.Id
                            where o.Active && t.Active && t.ClosedUtc == null && t.EndsUtc > now && t.StartsUtc <= until
                            select o).ToArrayAsync(ct);
        var offerIds = active.Select(x => x.Id).ToArray();
        var available = active.Where(x => AcademicRules.CanSee(now, x.PublishedUtc, x.VisibleUtc)).Select(x => x.Id).ToArray();
        var teacher = (await calendar.Events(offerIds, now.AddTicks(1), until.AddTicks(1), false, ct)).ToLookup(x => x.OfferingId);
        var student = (await calendar.Events(available, now.AddTicks(1), until.AddTicks(1), true, ct)).ToLookup(x => x.OfferingId);
        var lead = active.ToDictionary(x => x.Id, x => x.LeadId);
        var cohorts = (await (from e in data.Read<Enrollment>() join s in data.Read<StudyRecord>() on e.StudyRecordId equals s.Id
                            join p in data.Read<StudentProfile>() on s.StudentId equals p.Id
                            join u in data.Read<ApplicationUser>() on p.Id equals u.Id
                            where available.Contains(e.OfferingId) && e.Status == EnrollmentStatus.Active && s.EndedUtc == null && p.Active && u.IsActive
                            select new { e.OfferingId, User = p.Id }).ToArrayAsync(ct)).ToLookup(x => x.OfferingId, x => x.User);
        var co = (await (from t in data.Read<CoTeacher>() join p in data.Read<InstructorProfile>() on t.InstructorId equals p.Id
                        join u in data.Read<ApplicationUser>() on p.Id equals u.Id
                        where offerIds.Contains(t.OfferingId) && t.Active && p.Active && u.IsActive select t).ToArrayAsync(ct))
            .ToLookup(x => x.OfferingId, x => x.InstructorId);
        var leadIds = lead.Values.Distinct().ToArray();
        var activeLeads = (await (from p in data.Read<InstructorProfile>() join u in data.Read<ApplicationUser>() on p.Id equals u.Id
                            where leadIds.Contains(p.Id) && p.Active && u.IsActive select p.Id).ToArrayAsync(ct)).ToHashSet();
        var roles = await (from role in data.Read<Role>() join rp in data.Read<RolePermission>() on role.Id equals rp.RoleId
                           join p in data.Read<Permission>() on rp.PermissionId equals p.Id where p.Key == LmsPermissions.Calendar select role.Code).ToArrayAsync(ct);
        var teacherEnabled = roles.Contains(LmsPermissions.TeacherRole, StringComparer.OrdinalIgnoreCase);
        var studentEnabled = roles.Contains(LmsPermissions.StudentRole, StringComparer.OrdinalIgnoreCase);
        var items = new Dictionary<(Guid User, string Kind, Guid Event, DateTimeOffset At), CalendarEntry>();
        void Add(Guid user, IEnumerable<CalendarEntry> events)
        {
            foreach (var e in events) items.TryAdd((user, e.Kind, e.EventId, e.AtUtc), e);
        }
        foreach (var o in active)
        {
            if (teacherEnabled)
            {
                if (activeLeads.Contains(lead[o.Id])) Add(lead[o.Id], teacher[o.Id]);
                foreach (var t in co[o.Id]) Add(t, teacher[o.Id]);
            }
            if (studentEnabled) foreach (var s in cohorts[o.Id]) Add(s, student[o.Id]);
        }
        await using (var tx = await data.Transaction(ct))
        {
            var existing = await data.Db.Set<Reminder>().Where(x => x.DueUtc > now && x.DueUtc <= until).ToArrayAsync(ct);
            foreach (var row in existing)
            {
                var key = (row.UserId, row.Kind, row.EventId, row.DueUtc);
                if (!items.Remove(key, out var change)) row.SmsState = "Superseded";
                else { row.Title = Truncate(change.Title); }
            }
            // Key is unique on SQL Server. Selection and claim are separate from network I/O.
            foreach (var (key, e) in items)
                data.Db.Add(new Reminder { UserId = key.User, OfferingId = e.OfferingId, EventId = e.EventId,
                    Kind = e.Kind, Title = Truncate(e.Title), DueUtc = e.AtUtc, CreatedUtc = now, SmsState = "Pending" });
            await data.Save(ct);
            await tx.CommitAsync(ct);
        }
        data.Db.ChangeTracker.Clear();
        var queued = await data.Read<Reminder>().Where(x => x.SmsState == "Pending" && x.DueUtc > now).OrderBy(x => x.DueUtc).Take(30).ToArrayAsync(ct);
        foreach (var n in queued)
        {
            // At most one attempt per reminder; a crash near the gateway leaves Unknown rather than a duplicate SMS.
            var won = await data.Db.Set<Reminder>().Where(x => x.Id == n.Id && x.SmsState == "Pending")
                .ExecuteUpdateAsync(q => q.SetProperty(x => x.SmsState, "Unknown"), ct);
            if (won != 1) continue;
            var outcome = await sms.Deliver(n, ct);
            await data.Db.Set<Reminder>().Where(x => x.Id == n.Id && x.SmsState == "Unknown")
                .ExecuteUpdateAsync(q => q.SetProperty(x => x.SmsState, outcome), ct);
        }
    }
    private static string Truncate(string s) => s.Length <= 180 ? s : s[..180];
    private static async Task Cleanup(LmsData data, LmsFiles files, DateTimeOffset now, CancellationToken ct)
    {
        var oldPending = now.AddMinutes(-30); var oldReceiving = now.AddHours(-6);
        var stale = data.Db.Set<UploadAttempt>().Where(x => x.State == UploadState.Pending && x.UpdatedUtc < oldPending ||
            x.State == UploadState.Receiving && x.UpdatedUtc < oldReceiving);
        var ids = await stale.Select(x => x.Id).Take(200).ToArrayAsync(ct);
        await stale.Where(x => ids.Contains(x.Id)).ExecuteUpdateAsync(u => u.SetProperty(x => x.State, UploadState.Incomplete)
            .SetProperty(x => x.Code, "not-confirmed").SetProperty(x => x.UpdatedUtc, now), ct);
        foreach (var id in ids) files.CleanTemp(id);
        // Staged data is a separate retention policy, never keyed by the expiry of a log.
        var oldest = now.AddDays(-1);
        Guid[] temporary;
        await using (var tx = await data.Transaction(ct))
        {
            var unused = data.Db.Set<Media>().Where(m => m.CreatedUtc < oldest && (m.Purpose == MediaPurpose.RosterExcel ||
                m.Purpose == MediaPurpose.Submission && !data.Read<SubmissionFile>().Any(f => f.MediaId == m.Id) ||
                (m.Purpose == MediaPurpose.CurriculumFile || m.Purpose == MediaPurpose.CurriculumCover) &&
                !data.Read<LearningMaterial>().Any(r => r.MediaId == m.Id || r.CoverId == m.Id) &&
                !data.Read<TemplateVersionMedia>().Any(r => r.MediaId == m.Id)));
            temporary = await unused.Select(x => x.Id).Take(100).ToArrayAsync(ct);
            await unused.Where(x => temporary.Contains(x.Id)).ExecuteDeleteAsync(ct);
            await tx.CommitAsync(ct);
        }
        foreach (var id in temporary) files.CleanStaged(id);
        var expiry = now.AddMonths(-1);
        await data.Db.Set<OperationalEvent>().Where(x => x.OccurredUtc < expiry).ExecuteDeleteAsync(ct);
        await data.Db.Set<SecurityAuditEntry>().Where(x => x.OccurredUtc < expiry).ExecuteDeleteAsync(ct);
        await data.Db.Set<UploadAttempt>().Where(x => x.CreatedUtc < expiry && x.State != UploadState.Receiving).ExecuteDeleteAsync(ct);
        await data.Db.Set<Reminder>().Where(x => x.SmsState == "Pending" && x.DueUtc <= now)
            .ExecuteUpdateAsync(u => u.SetProperty(x => x.SmsState, "Missed"), ct);
    }
}
