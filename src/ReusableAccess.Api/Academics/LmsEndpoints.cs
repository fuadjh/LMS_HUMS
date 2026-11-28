using MediatR;
using Microsoft.AspNetCore.Mvc;
using ReusableAccess.Application.Academics;
using ReusableAccess.Contracts;
using ReusableAccess.Domain.Academics;
using ReusableAccess.Infrastructure.Academics;

namespace ReusableAccess.Api.Academics;

public static class LmsEndpoints
{
    private static async Task<IResult> Send<T>(ISender sender, IRequest<T> request, CancellationToken ct)
        => Results.Ok(new { data = await sender.Send(request, ct) });

    public static IEndpointRouteBuilder MapUniversityLms(this IEndpointRouteBuilder app)
    {
        var r = app.MapGroup("/api/lms").RequireAuthorization("AccessApi");
        r.MapGet("/me", (ISender s, CancellationToken ct) => Send(s, new GetLmsHomeQuery(), ct)).WithName("lms.me");
        r.MapGet("/catalog/{kind}", (string kind, ISender s, CancellationToken ct) => Send(s, new GetCatalogQuery(kind), ct)).WithName("lms.catalog.read");
        r.MapPost("/catalog/{kind}", (string kind, CatalogEdit input, ISender s, CancellationToken ct) => Send(s, new SaveCatalogCommand(kind, input), ct)).WithName("lms.catalog.write");
        r.MapGet("/grants", (ISender s, CancellationToken ct) => Send(s, new GetAcademicGrantsQuery(), ct)).RequireAuthorization("PlatformAdmin").WithName("lms.grants.read");
        r.MapPost("/grants", (ScopeGrantEdit input, ISender s, CancellationToken ct) => Send(s, new SetAcademicGrantCommand(input), ct)).RequireAuthorization("PlatformAdmin").WithName("lms.grants.write");

        r.MapGet("/people/{kind}", (string kind, Guid? scopeId, string? search, ISender s, CancellationToken ct) => Send(s, new GetPeopleQuery(kind, scopeId, search), ct)).WithName("lms.people.read");
        r.MapPost("/people/{kind}/probe", (string kind, ProbeInput input, ISender s, CancellationToken ct) => Send(s, new ProbeProfileQuery(kind, input), ct)).WithName("lms.people.probe").RequireRateLimiting("lms-sensitive");
        r.MapPost("/people/{kind}", (string kind, ProfileEdit input, ISender s, CancellationToken ct) => Send(s, new SaveProfileCommand(kind, input), ct)).WithName("lms.people.write");
        r.MapPost("/people/{kind}/{id:guid}/active/{active:bool}", (string kind, Guid id, bool active, ISender s, CancellationToken ct) => Send(s, new SetProfileActiveCommand(kind, id, active), ct)).WithName("lms.people.active");
        r.MapGet("/study/{id:guid}", (Guid id, ISender s, CancellationToken ct) => Send(s, new GetAcademicHistoryQuery(id), ct)).WithName("lms.people.history");
        r.MapPost("/import/{kind}/{mediaId:guid}/{scopeId:guid}/{apply:bool}", (string kind, Guid mediaId, Guid scopeId, bool apply, ISender s, CancellationToken ct) =>
            Send(s, new ImportProfilesCommand(kind, mediaId, scopeId, apply), ct)).WithName("lms.people.import").RequireRateLimiting("lms-sensitive");

        r.MapGet("/admin-tables/offerings", (Guid? termId, Guid? cohortId, string? search, int? page, int? size, ISender s, CancellationToken ct) =>
            Send(s, new ReadAdminOfferingsQuery(new("admin", termId, cohortId, false, search), new(page ?? 1, size ?? LmsTablePage.DefaultSize)), ct)).WithName("lms.offering.table");
        r.MapGet("/admin-tables/people/{kind}", (string kind, Guid? scopeId, string? search, int? page, int? size, ISender s, CancellationToken ct) =>
            Send(s, new ReadAdminPeopleQuery(kind, scopeId, search, new(page ?? 1, size ?? LmsTablePage.DefaultSize)), ct)).WithName("lms.people.table");
        r.MapGet("/academic-import/{kind}/preview", (string kind, Guid mediaId, Guid majorId, Guid? termId, ISender s, CancellationToken ct) =>
            Send(s, new PreviewAcademicWorkbookQuery(kind, new(mediaId, majorId, termId)), ct)).WithName("lms.academic-import.preview").RequireRateLimiting("lms-sensitive");
        r.MapPost("/academic-import/{kind}", (string kind, AcademicWorkbookInput input, ISender s, CancellationToken ct) =>
            Send(s, new ApplyAcademicWorkbookCommand(kind, input), ct)).WithName("lms.academic-import.apply").RequireRateLimiting("lms-sensitive");
        r.MapGet("/offerings", (string view, Guid? termId, Guid? cohortId, bool? mine, string? search, ISender s, CancellationToken ct) =>
            Send(s, new GetOfferingsQuery(new(view, termId, cohortId, mine == true, search)), ct)).WithName("lms.offerings.read");
        r.MapGet("/offerings/{id:guid}", (Guid id, string view, bool? includeContent, ISender s, CancellationToken ct) =>
            Send(s, new GetOfferingQuery(id, view, includeContent ?? true), ct)).WithName("lms.offering.read");
        r.MapPost("/offerings", (OfferingEdit input, ISender s, CancellationToken ct) => Send(s, new SaveOfferingCommand(input), ct)).WithName("lms.offering.assign");
        r.MapPost("/offerings/{id:guid}/co-teachers", (Guid id, CoTeacherEdit input, ISender s, CancellationToken ct) => Send(s, new SetCoTeacherCommand(id, input), ct)).WithName("lms.offering.co-teacher");
        r.MapPost("/offerings/{id:guid}/schedule", (Guid id, ScheduleEdit input, ISender s, CancellationToken ct) => Send(s, new ScheduleOfferingCommand(id, input), ct)).WithName("lms.offering.schedule");
        r.MapPost("/offerings/{id:guid}/publish", (Guid id, ISender s, CancellationToken ct) => Send(s, new PublishOfferingCommand(id), ct)).WithName("lms.offering.publish");
        r.MapPost("/offerings/{id:guid}/content/{kind}", (Guid id, string kind, SectionEdit input, ISender s, CancellationToken ct) => Send(s, new SaveSectionCommand(id, kind, input), ct)).WithName("lms.content.write");
        r.MapPost("/offerings/{id:guid}/chapters/{chapter:guid}/publish", (Guid id, Guid chapter, ISender s, CancellationToken ct) => Send(s, new PublishChapterCommand(id, chapter), ct)).WithName("lms.chapter.publish");
        r.MapGet("/templates", (ISender s, CancellationToken ct) => Send(s, new GetTemplatesQuery(), ct)).WithName("lms.template.read");
        r.MapPost("/offerings/{id:guid}/templates/save", (Guid id, TemplateSave input, ISender s, CancellationToken ct) => Send(s, new SaveTemplateCommand(id, input), ct)).WithName("lms.template.save");
        r.MapPost("/offerings/{id:guid}/templates/apply", (Guid id, TemplateApply input, ISender s, CancellationToken ct) => Send(s, new ApplyTemplateCommand(id, input), ct)).WithName("lms.template.apply");

        r.MapPost("/offerings/{id:guid}/join", (Guid id, ISender s, CancellationToken ct) => Send(s, new JoinOfferingCommand(id), ct)).WithName("lms.enrollment.join");
        r.MapPost("/enrollments/{id:guid}/withdraw", (Guid id, ISender s, CancellationToken ct) => Send(s, new WithdrawMyEnrollmentCommand(id), ct)).WithName("lms.enrollment.withdraw");
        r.MapPost("/enrollments/{id:guid}/remove", (Guid id, RemoveEnrollmentInput input, ISender s, CancellationToken ct) => Send(s, new RemoveEnrollmentAsAdminCommand(id, input), ct)).WithName("lms.enrollment.remove");
        r.MapGet("/offerings/{id:guid}/roster", (Guid id, string view, ISender s, CancellationToken ct) => Send(s, new GetOfferingRosterQuery(id, view), ct)).WithName("lms.enrollment.roster");
        r.MapPost("/lessons/{id:guid}/done", (Guid id, ISender s, CancellationToken ct) => Send(s, new MarkLessonDoneCommand(id), ct)).WithName("lms.lesson.done");
        r.MapGet("/enrollments/{id:guid}/history", (Guid id, string view, ISender s, CancellationToken ct) => Send(s, new GetEnrollmentHistoryQuery(id, view), ct)).WithName("lms.enrollment.history");
        r.MapGet("/transcript", (ISender s, CancellationToken ct) => Send(s, new GetTranscriptQuery(), ct)).WithName("lms.transcript.read");

        r.MapGet("/assessments/{id:guid}/work", (Guid id, string view, ISender s, CancellationToken ct) => Send(s, new GetAssessmentWorkQuery(id, view), ct)).WithName("lms.grading.work");
        r.MapPost("/assessments/{id:guid}/work", (Guid id, AttemptInput input, ISender s, CancellationToken ct) => Send(s, new SubmitAssessmentCommand(id, input), ct)).WithName("lms.assessment.submit");
        r.MapPost("/submissions/{id:guid}/grade", (Guid id, GradeEdit input, ISender s, CancellationToken ct) => Send(s, new SaveAssessmentGradeCommand(id, input), ct)).WithName("lms.grading.assessment");
        r.MapGet("/enrollments/{id:guid}/grade", (Guid id, ISender s, CancellationToken ct) => Send(s, new GetFinalGradesQuery(id), ct)).WithName("lms.grading.final-history");
        r.MapPost("/enrollments/{id:guid}/grade", (Guid id, GradeEdit input, ISender s, CancellationToken ct) => Send(s, new SaveFinalGradeCommand(id, input), ct)).WithName("lms.grading.final");

        r.MapGet("/calendar", (string view, DateTimeOffset fromUtc, DateTimeOffset toUtc, ISender s, CancellationToken ct) =>
            Send(s, new GetLmsCalendarQuery(view, fromUtc, toUtc), ct)).WithName("lms.calendar.read");
        r.MapGet("/notices", (ISender s, CancellationToken ct) => Send(s, new GetLmsNoticesQuery(), ct)).WithName("lms.notice.read");
        r.MapPost("/notices/{id:guid}/read", (Guid id, ISender s, CancellationToken ct) => Send(s, new ReadLmsNoticeCommand(id), ct)).WithName("lms.notice.acknowledge");
        r.MapGet("/chat", (Guid? recipientId, DateTimeOffset? beforeUtc, ISender s, CancellationToken ct) => Send(s, new GetCohortChatQuery(recipientId, beforeUtc), ct)).WithName("lms.chat.read");
        r.MapPost("/chat", (ChatPost input, ISender s, CancellationToken ct) => Send(s, new SendCohortMessageCommand(input), ct)).WithName("lms.chat.send");

        r.MapPost("/uploads", async (UploadStart input, ISender s, HttpContext context, CancellationToken ct) =>
        {
            var ticket = await s.Send(new StartLmsUploadCommand(input), ct);
            if (ticket.State == UploadState.Rejected) OperationalAudit.Outcome(context, "rejected");
            return Results.Ok(new { data = ticket });
        }).WithName("lms.upload.start").RequireRateLimiting("lms-sensitive");
        r.MapGet("/uploads/{id:guid}", (Guid id, ISender s, CancellationToken ct) => Send(s, new GetLmsUploadQuery(id), ct)).WithName("lms.upload.status");
        r.MapPut("/uploads/{id:guid}", async (Guid id, HttpContext c, ILmsFileTransport files, CancellationToken ct) =>
        {
            if (!string.Equals(c.Request.ContentType, "application/octet-stream", StringComparison.OrdinalIgnoreCase))
                return Results.Problem(statusCode: 415, title: "نوع بدنه باید application/octet-stream باشد.");
            return Results.Ok(new { data = await files.ReceiveAsync(id, c.Request.Body, c.Request.ContentLength, ct) });
        }).WithMetadata(new RequestSizeLimitAttribute(UploadLimits.FileBytes)).WithName("lms.upload.transfer");
        r.MapGet("/files/{id:guid}", async (Guid id, HttpContext c, ILmsFileTransport files, CancellationToken ct) =>
        {
            var file = await files.DownloadAsync(id, ct);
            // Inline only recognized image/video; document downloads are attachments, not scriptable pages.
            var inline = file.ContentType is "image/png" or "image/jpeg" or "image/webp" or "image/gif" or
                "video/mp4" or "video/webm" or "audio/mpeg" or "audio/wav" or "audio/ogg";
            return Results.File(file.Body, file.ContentType, inline ? null : file.Name,
                lastModified: file.CreatedUtc, enableRangeProcessing: true);
        }).WithName("lms.file.read");
        r.MapPost("/logs/uploads", (LogFilter input, ISender s, CancellationToken ct) => Send(s, new GetUploadLogsQuery(input), ct))
            .RequireAuthorization("PlatformAdmin").WithName("lms.logs.uploads");
        r.MapPost("/logs/security", (LogFilter input, ISender s, CancellationToken ct) => Send(s, new GetSecurityLogsQuery(input), ct))
            .RequireAuthorization("PlatformAdmin").WithName("lms.logs.security");
        r.MapGet("/sms", (ISender s, CancellationToken ct) => Send(s, new GetLmsSmsSettingsQuery(), ct))
            .RequireAuthorization("PlatformAdmin").WithName("lms.sms.read");
        r.MapPost("/sms", (SmsEdit input, ISender s, CancellationToken ct) => Send(s, new SaveLmsSmsSettingsCommand(input), ct))
            .RequireAuthorization("PlatformAdmin").WithName("lms.sms.write");
        var learning = r.MapGroup("/offerings/{id:guid}/learning");
        learning.MapGet("/teacher", (Guid id, ISender s, CancellationToken ct) => Send(s, new GetLearningTeacherQuery(id), ct)).WithName("lms.learning.teacher");
        learning.MapGet("/curriculum", (Guid id, string view, ISender s, CancellationToken ct) => Send(s, new GetLearningCurriculumQuery(id, view), ct)).WithName("lms.learning.curriculum");
        learning.MapGet("/lessons/{lesson:guid}", (Guid id, Guid lesson, string view, ISender s, CancellationToken ct) => Send(s, new GetLearningLessonQuery(id, view, lesson), ct)).WithName("lms.learning.lesson");
        learning.MapGet("/lessons/{lesson:guid}/materials/{key:guid}", (Guid id, Guid lesson, Guid key, string view, ISender s, CancellationToken ct) => Send(s, new GetLearningMaterialQuery(id, view, lesson, key), ct)).WithName("lms.learning.material");
        learning.MapPost("/draft", (Guid id, LearningEdit edit, ISender s, CancellationToken ct) => Send(s, new SaveLearningCommand(id, edit), ct)).WithName("lms.learning.draft");
        learning.MapPost("/publish", (Guid id, LearningPublish edit, ISender s, CancellationToken ct) => Send(s, new PublishLearningCommand(id, edit), ct)).WithName("lms.learning.publish");
        learning.MapPost("/lessons/{lesson:guid}/completion", (Guid id, Guid lesson, LearningCompletion edit, ISender s, CancellationToken ct) =>
            Send(s, new CompleteLearningLessonCommand(id, lesson, edit.Completed), ct)).WithName("lms.learning.completion");
        r.MapLmsStudio();
        r.MapOfferingWorkspace();
        return app;
    }
}
