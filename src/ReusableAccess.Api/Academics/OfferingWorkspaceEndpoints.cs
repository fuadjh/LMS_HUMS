using MediatR;
using Microsoft.AspNetCore.Mvc;
using ReusableAccess.Application.Academics;
using ReusableAccess.Contracts;

namespace ReusableAccess.Api.Academics;

internal static class OfferingWorkspaceEndpoints
{
    internal static void MapOfferingWorkspace(this RouteGroupBuilder r)
    {
        r.MapGet("/offerings/{id:guid}/assignments", (Guid id, string view, ISender s, CancellationToken ct) =>
            Send(s, new GetOfferingAssignmentsQuery(id, view), ct)).WithName("lms.assignment.list");
        r.MapGet("/offerings/{id:guid}/assignments/{assessment:guid}", (Guid id, Guid assessment, string view, ISender s, CancellationToken ct) =>
            Send(s, new GetOfferingAssignmentQuery(id, assessment, view), ct)).WithName("lms.assignment.detail");
        r.MapPost("/offerings/{id:guid}/assignments", (Guid id, AssessmentEdit body, ISender s, CancellationToken ct) =>
            Send(s, new SaveOfferingAssignmentCommand(id, body), ct))
            .WithMetadata(new RequestSizeLimitAttribute(750_000)).WithName("lms.assignment.save");
        r.MapPost("/offerings/{id:guid}/assignments/{assessment:guid}/publish", (Guid id, Guid assessment, PublishAssessmentInput body, ISender s, CancellationToken ct) =>
            Send(s, new PublishOfferingAssignmentCommand(id, assessment, body), ct)).WithName("lms.assignment.publish");
        r.MapPost("/offerings/{id:guid}/assignments/{assessment:guid}/extend", (Guid id, Guid assessment, ExtendAssignmentInput body, ISender s, CancellationToken ct) =>
            Send(s, new ExtendOfferingAssignmentCommand(id, assessment, body), ct)).WithName("lms.assignment.extend");
        r.MapGet("/teaching/assignment-queue", (ISender s, CancellationToken ct) =>
            Send(s, new GetAssignmentQueueQuery(), ct)).WithName("lms.assignment.queue");
        r.MapGet("/assignment-templates", (string? search, int? page, ISender s, CancellationToken ct) =>
            Send(s, new GetAssignmentTemplatesQuery(search, page ?? 1), ct)).WithName("lms.assignment.template.read");
        r.MapPost("/assignment-templates", (AssignmentTemplateInput body, ISender s, CancellationToken ct) =>
            Send(s, new SaveAssignmentTemplateCommand(body), ct))
            .WithMetadata(new RequestSizeLimitAttribute(160_000)).WithName("lms.assignment.template.save");
        r.MapGet("/offerings/{id:guid}/discussion", (Guid id, string view, int? page, ISender s, CancellationToken ct) =>
            Send(s, new GetOfferingDiscussionQuery(id, view, page ?? 1), ct)).WithName("lms.course-discussion.read");
        r.MapPost("/offerings/{id:guid}/discussion", (Guid id, string view, CourseDiscussionPost body, ISender s, CancellationToken ct) =>
            Send(s, new PostOfferingDiscussionCommand(id, view, body), ct))
            .WithMetadata(new RequestSizeLimitAttribute(16_000)).WithName("lms.course-discussion.post");
    }

    private static async Task<IResult> Send<T>(ISender mediator, IRequest<T> request, CancellationToken ct)
        => Results.Ok(new { data = await mediator.Send(request, ct) });
}
