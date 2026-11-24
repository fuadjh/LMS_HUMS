using System.Buffers;
using System.IO.Compression;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using ReusableAccess.Application.Academics;
using ReusableAccess.Application.Errors;
using ReusableAccess.Contracts;
using ReusableAccess.Domain.Academics;
using ReusableAccess.Domain.Identity;

namespace ReusableAccess.Infrastructure.Academics;

internal sealed class LmsFiles(LmsData data, LmsAccess access, LmsEnrollments enrollments, LmsGrading grading,
    IConfiguration config, IHostEnvironment environment, IHttpContextAccessor http) : ILmsFileTransport
{
    private string Root
    {
        get
        {
            var set = config["Lms:PrivateFilesPath"];
            if (string.IsNullOrWhiteSpace(set) && !environment.IsDevelopment()) throw new UseCaseException(503, "محل خصوصی فایل پیکربندی نشده است.");
            if (!string.IsNullOrWhiteSpace(set) && !Path.IsPathFullyQualified(set)) throw new UseCaseException(503, "مسیر خصوصی باید مطلق باشد.");
            var path = Path.GetFullPath(string.IsNullOrWhiteSpace(set) ? Path.Combine(environment.ContentRootPath, "App_Data", "Lms") : set);
            var web = Path.Combine(environment.ContentRootPath, "wwwroot");
            var relative = Path.GetRelativePath(web, path);
            if (relative == "." || (!Path.IsPathRooted(relative) && relative != ".." && !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)))
                throw new UseCaseException(503, "مخزن فایل باید خارج از پوشه عمومی باشد.");
            return path;
        }
    }
    private string FilePath(Guid id) => Path.Combine(Root, id.ToString("N") + ".blob");
    private string TempPath(Guid id) => Path.Combine(Root, id.ToString("N") + ".part");
    internal Stream Open(Media m)
    {
        var path = FilePath(m.Id);
        if (!File.Exists(path)) throw LmsError.Missing();
        return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.Asynchronous);
    }
    internal static long Limit(MediaPurpose purpose) => purpose == MediaPurpose.RosterExcel ? UploadLimits.RosterBytes : UploadLimits.FileBytes;
    internal static bool Allowed(MediaPurpose purpose, string ext) => purpose switch
    {
        MediaPurpose.InstructorAvatar or MediaPurpose.StudentAvatar => ext is ".jpg" or ".jpeg" or ".png",
        MediaPurpose.LessonMaterial => ext is ".mp4" or ".pdf" or ".docx" or ".jpg" or ".jpeg" or ".png" or ".xlsx",
        MediaPurpose.Submission => ext is ".pdf" or ".docx" or ".jpg" or ".jpeg" or ".png" or ".mp4" or ".xlsx",
        MediaPurpose.RosterExcel => ext == ".xlsx", _ => false
    };
    internal async Task<UploadTicket> Start(UploadStart input, CancellationToken ct)
    {
        await using var tx = await data.Transaction(ct);
        await access.Load(ct);
        // Record only metadata; never trust browser content type or reported bytes for validation.
        var (name, ext) = Name(input.Name);
        var now = data.Now;
        var cutoff = now.AddHours(-1);
        var actorId = access.ActorId;
        if (await data.Read<UploadAttempt>().CountAsync(x => x.ActorId == actorId && x.CreatedUtc >= cutoff, ct) >= 80)
            throw new UseCaseException(429, "تعداد تلاش‌های ارسال در ساعت بیش از حد مجاز است.");
        var c = http.HttpContext;
        var attempt = new UploadAttempt { ActorId = access.ActorId, TargetId = input.TargetId, Purpose = input.Purpose, Name = name, Extension = ext,
            ReportedBytes = input.Bytes, CreatedUtc = now, UpdatedUtc = now, State = UploadState.Pending,
            Ip = c?.Connection.RemoteIpAddress?.ToString(), Correlation = OperationalAudit.SafeCorrelation(c?.TraceIdentifier) };
        try
        {
            await Authorize(input.Purpose, input.TargetId, ct);
            if (input.Bytes <= 0 || input.Bytes > Limit(input.Purpose)) { attempt.State = UploadState.Rejected; attempt.Code = "reported-size"; }
            else if (!Allowed(input.Purpose, ext)) { attempt.State = UploadState.Rejected; attempt.Code = "file-type"; }
            else _ = Root;
        }
        catch (UseCaseException e)
        {
            attempt.State = UploadState.Rejected;
            attempt.Code = e.StatusCode switch { 401 or 403 or 404 => "access", 409 => "state", 503 => "storage-unconfigured", _ => "invalid" };
        }
        data.Db.Add(attempt);
        return await data.Finish(tx, new UploadTicket(attempt.Id, attempt.State, attempt.Code, Limit(input.Purpose)), attempt.Id, ct);
    }

    private async Task Authorize(MediaPurpose purpose, Guid targetId, CancellationToken ct)
    {
        switch (purpose)
        {
            case MediaPurpose.InstructorAvatar:
                await access.AsTeacher(LmsPermissions.OwnAvatar, ct);
                if (targetId != access.ActorId) throw LmsError.Missing(); break;
            case MediaPurpose.StudentAvatar:
                await access.AsStudent(LmsPermissions.OwnAvatar, ct);
                if (targetId != access.ActorId) throw LmsError.Missing(); break;
            case MediaPurpose.LessonMaterial:
                var l = await enrollments.LessonInfo(targetId, ct);
                await access.AsTeaching(l.Info, LmsPermissions.LessonUpload, ct);
                await access.AsTeaching(l.Info, LmsPermissions.Content, ct);
                LmsData.Open(l.Info.Term, data.Now);
                LmsOfferings.RequireDraft(l.Chapter);
                if (!l.Info.Offering.Active || !l.Chapter.Active || !l.Lesson.Active) throw LmsError.Conflict("درسک یا ارائه غیرفعال است.");
                if (await data.Read<LessonFile>().CountAsync(x => x.LessonId == targetId, ct) >= 16)
                    throw LmsError.Conflict("حداکثر ۱۶ فایل برای یک درسک.");
                break;
            case MediaPurpose.Submission:
                var i = await grading.Find(targetId, ct);
                if (i.Assessment.QuizSpec is not null) throw LmsError.Invalid("آزمون پاسخ گزینه‌ای دارد و پیوست نمی‌پذیرد.");
                _ = await grading.CheckSubmission(i, data.Now, LmsPermissions.SubmissionUpload, ct);
                await access.AsStudent(LmsPermissions.Submit, ct);
                var cutoff = data.Now.AddHours(-8);
                if (await data.Read<Media>().CountAsync(x => x.OwnerId == access.ActorId && x.TargetId == targetId && x.Purpose == MediaPurpose.Submission && x.CreatedUtc >= cutoff &&
                        !data.Read<SubmissionFile>().Any(f => f.MediaId == x.Id), ct) >= 8)
                    throw LmsError.Conflict("حداکثر ۸ فایل انتخاب‌شده و ثبت‌نشده؛ ابتدا تلاش را ثبت کنید.");
                break;
            case MediaPurpose.RosterExcel:
                await access.AsEducation(LmsPermissions.Import, ct);
                var c = await access.Cohorts().SingleOrDefaultAsync(x => x.Id == targetId, ct);
                if (c is not null)
                {
                    if (!c.Active) throw LmsError.Missing();
                    access.DemandScope(LmsPermissions.Import, c.FacultyId, c.MajorId); break;
                }
                var m = await data.Read<Major>().SingleOrDefaultAsync(x => x.Id == targetId && x.Active, ct) ?? throw LmsError.Missing();
                access.DemandScope(LmsPermissions.Import, m.FacultyId, m.Id); break;
            default: throw LmsError.Invalid("مصرف فایل نامعتبر است.");
        }
    }
    public async Task<FileRow> ReceiveAsync(Guid trackingId, Stream source, long? contentLength, CancellationToken ct)
    {
        await access.Load(ct);
        var owner = access.ActorId;
        var attempt = await data.Read<UploadAttempt>().SingleOrDefaultAsync(x => x.Id == trackingId && x.ActorId == owner, ct) ?? throw LmsError.Missing();
        if (attempt.State == UploadState.Stored)
        {
            var existing = await data.Read<Media>().SingleOrDefaultAsync(x => x.Id == trackingId, ct)
                ?? throw LmsError.Conflict("فایل موقت منقضی شده است؛ دوباره ثبت کنید.");
            return new(existing.Id, existing.Name, existing.Bytes, existing.Extension);
        }
        var latest = data.Now;
        var min = latest.AddMinutes(-30);
        var receiveIp = http.HttpContext?.Connection.RemoteIpAddress?.ToString();
        var receiveTrace = OperationalAudit.SafeCorrelation(http.HttpContext?.TraceIdentifier);
        var taken = await data.Db.Set<UploadAttempt>().Where(x => x.Id == trackingId && x.State == UploadState.Pending && x.CreatedUtc >= min)
            .ExecuteUpdateAsync(q => q.SetProperty(x => x.State, UploadState.Receiving).SetProperty(x => x.UpdatedUtc, latest)
                .SetProperty(x => x.Ip, x => receiveIp ?? x.Ip).SetProperty(x => x.Correlation, x => receiveTrace ?? x.Correlation), ct);
        if (taken != 1) throw LmsError.Conflict("کد پیگیری منقضی یا قبلاً مصرف شده است.");
        data.Resource(trackingId);
        var received = 0L; string? temp = null;
        var errorCode = "transfer-error";
        try
        {
            if (contentLength is { } len && (len <= 0 || len > Limit(attempt.Purpose) || len != attempt.ReportedBytes))
            {
                errorCode = "length-header";
                throw new UseCaseException(413, "حجم بدنه با سقف یا حجم اعلامی سازگار نیست.");
            }
            errorCode = "access-or-state";
            await Authorize(attempt.Purpose, attempt.TargetId, ct);
            errorCode = "storage-error";
            Directory.CreateDirectory(Root);
            temp = TempPath(trackingId);
            var buffer = ArrayPool<byte>.Shared.Rent(65536);
            try
            {
                await using var f = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, FileOptions.Asynchronous);
                while (true)
                {
                    errorCode = "transfer-error";
                    var n = await source.ReadAsync(buffer.AsMemory(0, 65536), ct);
                    if (n == 0) break;
                    received += n;
                    if (received > Limit(attempt.Purpose)) { errorCode = "received-size"; throw new UseCaseException(413, "فایل بیش از حد مجاز دریافت شد."); }
                    errorCode = "storage-error";
                    await f.WriteAsync(buffer.AsMemory(0, n), ct);
                }
                await f.FlushAsync(ct);
            }
            finally { ArrayPool<byte>.Shared.Return(buffer, clearArray: true); }
            errorCode = "received-length";
            if (received != attempt.ReportedBytes) throw LmsError.Invalid("حجم واقعی و اعلامی یکسان نیست.");
            errorCode = "signature";
            if (!ValidSignature(temp, attempt.Extension)) throw LmsError.Invalid("ساختار فایل با نوع مجاز سازگار نیست.");
            errorCode = "commit-unconfirmed";
            // Re-check the resource after streaming (it could have closed/deactivated since registration).
            data.Db.ChangeTracker.Clear();
            await using var tx = await data.Transaction(ct);
            await access.Reload(ct);
            await Authorize(attempt.Purpose, attempt.TargetId, ct);
            var tracking = await data.Find<UploadAttempt>(trackingId, ct);
            if (tracking.State != UploadState.Receiving) throw LmsError.Conflict("این تلاش دیگر فعال نیست.");
            var media = new Media { Id = attempt.Id, Name = attempt.Name, Extension = attempt.Extension, Bytes = received,
                Purpose = attempt.Purpose, TargetId = attempt.TargetId, OwnerId = owner, CreatedUtc = data.Now };
            data.Db.Add(media);
            switch (attempt.Purpose)
            {
                case MediaPurpose.InstructorAvatar: (await data.Find<InstructorProfile>(owner, ct)).AvatarId = media.Id; break;
                case MediaPurpose.StudentAvatar: (await data.Find<StudentProfile>(owner, ct)).AvatarId = media.Id; break;
                case MediaPurpose.LessonMaterial: data.Db.Add(new LessonFile { LessonId = media.TargetId, MediaId = media.Id }); break;
            }
            tracking.UpdatedUtc = data.Now; tracking.State = UploadState.Stored; tracking.ReceivedBytes = received; tracking.Code = null;
            // Move once before committing DB references; an ambiguous crash can leave an orphan, not a false success.
            File.Move(temp, FilePath(media.Id)); temp = null;
            return await data.Finish(tx, new FileRow(media.Id, media.Name, media.Bytes, media.Extension), media.Id, ct);
        }
        catch (Exception e) when (e is UseCaseException or IOException or UnauthorizedAccessException or OperationCanceledException or DbUpdateException)
        {
            var state = e switch
            {
                OperationCanceledException => UploadState.Interrupted,
                IOException when errorCode == "transfer-error" => UploadState.Interrupted,
                UseCaseException => UploadState.Rejected,
                _ => UploadState.Incomplete
            };
            if (temp is not null) TryDelete(temp);
            await Fail(trackingId, state, e is OperationCanceledException ? "interrupted" : errorCode, received);
            if (e is UseCaseException) throw;
            if (e is OperationCanceledException && ct.IsCancellationRequested) throw;
            throw new UseCaseException(503, "ذخیرهٔ فایل تأیید نشده است؛ وضعیت را با کد پیگیری بررسی کنید.");
        }
    }
    private async Task Fail(Guid id, UploadState state, string reason, long bytes)
    {
        try
        {
            data.Db.ChangeTracker.Clear();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var now = data.Now;
            await data.Db.Set<UploadAttempt>().Where(x => x.Id == id && x.State == UploadState.Receiving)
                .ExecuteUpdateAsync(q => q.SetProperty(x => x.State, state).SetProperty(x => x.Code, reason)
                    .SetProperty(x => x.ReceivedBytes, bytes).SetProperty(x => x.UpdatedUtc, now), timeout.Token);
        }
        catch (Exception) { /* the maintenance job expires an unresolved attempt; no claimed proxy rejection */ }
    }

    public async Task<LmsDownload> DownloadAsync(Guid id, CancellationToken ct)
    {
        await access.Load(ct);
        data.Resource(id);
        var m = await data.Read<Media>().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw LmsError.Missing();
        var user = access.ActorId;
        switch (m.Purpose)
        {
            case MediaPurpose.StudentAvatar when m.OwnerId == user: await access.AsStudent(LmsPermissions.OwnAvatar, ct); break;
            case MediaPurpose.InstructorAvatar when m.OwnerId == user: await access.AsTeacher(LmsPermissions.OwnAvatar, ct); break;
            case MediaPurpose.Submission:
            {
                var links = await (from sf in data.Read<SubmissionFile>() join a in data.Read<Submission>() on sf.SubmissionId equals a.Id
                                   join e in data.Read<Enrollment>() on a.EnrollmentId equals e.Id
                                   join r in data.Read<StudyRecord>() on e.StudyRecordId equals r.Id
                                   where sf.MediaId == id select new { r.StudentId, e.OfferingId }).ToArrayAsync(ct);
                if (links.Any(x => x.StudentId == user) && access.HasStudent(LmsPermissions.Learn)) break;
                if (links.Length != 1) throw LmsError.Missing();
                await grading.GradeRead(await access.Offering(links[0].OfferingId, ct), ct);
                break;
            }
            case MediaPurpose.LessonMaterial:
            {
                var links = await data.Read<LessonFile>().Where(x => x.MediaId == id).Select(x => x.LessonId).ToArrayAsync(ct);
                var allowed = false; var now = data.Now;
                foreach (var lid in links)
                {
                    var lesson = await enrollments.LessonInfo(lid, ct);
                    if (access.HasTeacher(LmsPermissions.Teach) && (lesson.Info.Offering.LeadId == user || await access.CoAssignment(lesson.Chapter.OfferingId, ct) is not null))
                    { allowed = true; break; }
                    if (!access.HasStudent(LmsPermissions.Learn) || !LmsEnrollments.PublishedLesson(lesson, now)) continue;
                    var own = await access.OwnEnrollment(lesson.Chapter.OfferingId, ct);
                    if (own?.Status == EnrollmentStatus.Active && (AcademicRules.IsClosed(lesson.Info.Term, now) ||
                        (await access.ActiveStudy(ct))?.Record.Id == own.StudyRecordId)) { allowed = true; break; }
                }
                if (!allowed) throw LmsError.Missing();
                break;
            }
            default: throw LmsError.Missing();
        }
        return new(Open(m), Mime(m.Extension), m.Name, m.CreatedUtc);
    }
    private static string Mime(string ext) => ext switch
    {
        ".jpg" or ".jpeg" => "image/jpeg", ".png" => "image/png", ".mp4" => "video/mp4", ".pdf" => "application/pdf",
        ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", _ => "application/octet-stream"
    };
    private static (string Name, string Extension) Name(string? input)
    {
        var baseName = (input ?? "").Replace('\\', '/').Split('/').Last().Trim();
        var n = IdentityRules.NormalizeDigits(string.Concat(baseName.Where(c => !char.IsControl(c) &&
            c is not ('\u202A' or '\u202B' or '\u202C' or '\u202D' or '\u202E' or '\u2066' or '\u2067' or '\u2068' or '\u2069'))));
        n = Regex.Replace(n, @"(?:[0-9][\p{Cf}\s._-]*){9}[0-9]", "[شناسه‌پوشیده]", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
        var ext = Path.GetExtension(n).ToLowerInvariant();
        if (ext.Length > 10) ext = "";
        if (n.Length == 0) n = "فایل بی‌نام";
        if (n.Length > 180) n = n[..(180 - ext.Length)] + ext;
        return (n, ext);
    }
    private static bool ValidSignature(string path, string ext)
    {
        try
        {
            using var file = File.OpenRead(path);
            Span<byte> h = stackalloc byte[16];
            if (file.ReadAtLeast(h, 12, throwOnEndOfStream: false) < 12) return false;
            return ext switch
            {
                ".jpg" or ".jpeg" => h[0] == 0xFF && h[1] == 0xD8 && h[2] == 0xFF,
                ".png" => h.StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }),
                ".pdf" => h.StartsWith("%PDF-"u8),
                ".mp4" => h[4..].StartsWith("ftyp"u8),
                ".docx" or ".xlsx" => h[0] == 80 && h[1] == 75 && OfficeStructure(file, ext),
                _ => false
            };
        }
        catch (Exception e) when (e is IOException or InvalidDataException or OverflowException or NotSupportedException) { return false; }
    }
    private static bool OfficeStructure(Stream file, string ext)
    {
        file.Position = 0;
        using var z = new ZipArchive(file, ZipArchiveMode.Read, leaveOpen: true);
        return z.Entries.Count is > 0 and <= 4096 && z.Entries.Sum(x => x.Length) <= 1_000_000_000 &&
            z.GetEntry("[Content_Types].xml") is not null && z.GetEntry(ext == ".docx" ? "word/document.xml" : "xl/workbook.xml") is not null &&
            !z.Entries.Any(x => x.FullName.Contains("vbaProject", StringComparison.OrdinalIgnoreCase) ||
                x.FullName.Contains("activeX", StringComparison.OrdinalIgnoreCase) || x.FullName.Contains("../", StringComparison.Ordinal));
    }
    internal void CleanStaged(Guid id) { try { TryDelete(FilePath(id)); } catch (UseCaseException) { } }
    internal void CleanTemp(Guid id) { try { TryDelete(TempPath(id)); } catch (UseCaseException) { } }
    private static void TryDelete(string file) { try { File.Delete(file); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
}
