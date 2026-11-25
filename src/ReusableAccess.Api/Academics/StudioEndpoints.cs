using MediatR;
using Microsoft.AspNetCore.Mvc;
using ReusableAccess.Application.Academics;
using ReusableAccess.Contracts;

namespace ReusableAccess.Api.Academics;

internal sealed class LmsReadOnlyPost { }

internal static class StudioEndpoints
{
    internal static RouteGroupBuilder MapLmsStudio(this RouteGroupBuilder r)
    {
        r.MapGet("/template-bank", (string? search, int? page, ISender s, CancellationToken ct) =>
            Send(s, new GetCourseTemplateBankQuery(search, page ?? 1), ct)).WithName("lms.studio.bank");
        r.MapGet("/template-bank/courses", (ISender s, CancellationToken ct) =>
            Send(s, new GetTemplateCoursesQuery(), ct)).WithName("lms.studio.courses");
        r.MapGet("/template-bank/{id:guid}", (Guid id, Guid? versionId, ISender s, CancellationToken ct) =>
            Send(s, new GetCourseTemplateVersionQuery(id, versionId), ct)).WithName("lms.studio.template");
        r.MapGet("/template-bank/{id:guid}/versions", (Guid id, int? page, ISender s, CancellationToken ct) =>
            Send(s, new GetCourseTemplateVersionsQuery(id, page ?? 1), ct)).WithName("lms.studio.versions");
        r.MapPost("/template-bank", (StudioCreateTemplate body, ISender s, CancellationToken ct) =>
            Send(s, new CreateCourseTemplateCommand(body), ct)).WithName("lms.studio.template.create");
        r.MapPost("/template-bank/{id:guid}/versions", (Guid id, StudioSaveVersion body, ISender s, CancellationToken ct) =>
            Send(s, new SaveCourseTemplateVersionCommand(id, body), ct))
            .WithMetadata(new RequestSizeLimitAttribute(4_200_000)).WithName("lms.studio.template.release");
        r.MapPost("/template-bank/{id:guid}/archive", (Guid id, StudioRevision body, ISender s, CancellationToken ct) =>
            Send(s, new ArchiveCourseTemplateCommand(id, body), ct)).WithName("lms.studio.template.archive");

        r.MapGet("/teaching/schedule-targets", (Guid courseId, Guid? termId, ISender s, CancellationToken ct) =>
            Send(s, new GetTeachingScheduleTargetsQuery(courseId, termId), ct)).WithName("lms.studio.targets");
        r.MapGet("/offerings/{id:guid}/schedule-origins", (Guid id, Guid versionId, ISender s, CancellationToken ct) =>
            Send(s, new GetScheduleOriginsQuery(id, versionId), ct)).WithName("lms.studio.origins");
        r.MapPost("/offerings/{id:guid}/schedule-preview", (Guid id, StudioPlan body, ISender s, CancellationToken ct) =>
            Send(s, new PreviewOfferingScheduleQuery(id, body), ct))
            .WithMetadata(new RequestSizeLimitAttribute(200_000), new LmsReadOnlyPost()).WithName("lms.studio.schedule.preview");
        r.MapGet("/offerings/{id:guid}/schedule-draft", (Guid id, ISender s, CancellationToken ct) =>
            Send(s, new GetOfferingScheduleDraftQuery(id), ct)).WithName("lms.studio.schedule.draft");
        r.MapGet("/offerings/{id:guid}/schedule-view", (Guid id, ISender s, CancellationToken ct) =>
            Send(s, new GetOfferingScheduleReadModelQuery(id), ct)).WithName("lms.studio.schedule.read");
        r.MapPost("/offerings/{id:guid}/schedule-draft", (Guid id, StudioStoreDraft body, ISender s, CancellationToken ct) =>
            Send(s, new SaveOfferingScheduleDraftCommand(id, body), ct))
            .WithMetadata(new RequestSizeLimitAttribute(200_000)).WithName("lms.studio.schedule.save");
        r.MapPost("/offerings/{id:guid}/schedule-draft/discard", (Guid id, StudioRevision body, ISender s, CancellationToken ct) =>
            Send(s, new DiscardOfferingScheduleDraftCommand(id, body), ct)).WithName("lms.studio.schedule.discard");
        r.MapPost("/offerings/{id:guid}/schedule-draft/apply", (Guid id, StudioApplyDraft body, ISender s, CancellationToken ct) =>
            Send(s, new ApplyOfferingScheduleCommand(id, body), ct)).WithName("lms.studio.schedule.apply");
        r.MapPost("/offerings/{id:guid}/schedule-draft/publish", (Guid id, StudioApplyDraft body, ISender s, CancellationToken ct) =>
            Send(s, new PublishScheduledOfferingCommand(id, body), ct)).WithName("lms.studio.schedule.publish");
        return r;
    }

    private static async Task<IResult> Send<T>(ISender mediator, IRequest<T> request, CancellationToken ct)
        => Results.Ok(new { data = await mediator.Send(request, ct) });
}
