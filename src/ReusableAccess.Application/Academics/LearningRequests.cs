using MediatR;
using ReusableAccess.Application.Errors;
using ReusableAccess.Contracts;

namespace ReusableAccess.Application.Academics;

public interface ILearningQueries
{
    Task<LearningTeacherView> Teacher(Guid offering, CancellationToken ct);
    Task<LearningCurriculum> Curriculum(Guid offering, string view, CancellationToken ct);
    Task<LearningLessonView> Lesson(Guid offering, string view, Guid lesson, CancellationToken ct);
    Task<LearningReadMaterial> Material(Guid offering, string view, Guid lesson, Guid key, CancellationToken ct);
}

public interface ILearningCommands
{
    Task<LearningSaved> Save(Guid offering, LearningEdit input, CancellationToken ct);
    Task<LearningPublished> Publish(Guid offering, LearningPublish input, CancellationToken ct);
    Task<bool> Complete(Guid offering, Guid lesson, bool completed, CancellationToken ct);
}

public sealed record GetLearningTeacherQuery(Guid Offering) : IRequest<LearningTeacherView>;
public sealed record GetLearningCurriculumQuery(Guid Offering, string View) : IRequest<LearningCurriculum>;
public sealed record GetLearningLessonQuery(Guid Offering, string View, Guid Lesson) : IRequest<LearningLessonView>;
public sealed record GetLearningMaterialQuery(Guid Offering, string View, Guid Lesson, Guid Key) : IRequest<LearningReadMaterial>;
public sealed record SaveLearningCommand(Guid Offering, LearningEdit Input) : IRequest<LearningSaved>, IConcurrencyConflictRequest;
public sealed record PublishLearningCommand(Guid Offering, LearningPublish Input) : IRequest<LearningPublished>, IConcurrencyConflictRequest;
public sealed record CompleteLearningLessonCommand(Guid Offering, Guid Lesson, bool Completed) : IRequest<bool>, IConcurrencyConflictRequest;

public sealed class LearningQueryHandlers(ILearningQueries queries) :
    IRequestHandler<GetLearningTeacherQuery, LearningTeacherView>, IRequestHandler<GetLearningCurriculumQuery, LearningCurriculum>,
    IRequestHandler<GetLearningLessonQuery, LearningLessonView>, IRequestHandler<GetLearningMaterialQuery, LearningReadMaterial>
{
    public Task<LearningTeacherView> Handle(GetLearningTeacherQuery r, CancellationToken ct) => queries.Teacher(r.Offering, ct);
    public Task<LearningCurriculum> Handle(GetLearningCurriculumQuery r, CancellationToken ct) => queries.Curriculum(r.Offering, r.View, ct);
    public Task<LearningLessonView> Handle(GetLearningLessonQuery r, CancellationToken ct) => queries.Lesson(r.Offering, r.View, r.Lesson, ct);
    public Task<LearningReadMaterial> Handle(GetLearningMaterialQuery r, CancellationToken ct) => queries.Material(r.Offering, r.View, r.Lesson, r.Key, ct);
}

public sealed class LearningCommandHandlers(ILearningCommands commands) : IRequestHandler<SaveLearningCommand, LearningSaved>,
    IRequestHandler<PublishLearningCommand, LearningPublished>, IRequestHandler<CompleteLearningLessonCommand, bool>
{
    public Task<LearningSaved> Handle(SaveLearningCommand r, CancellationToken ct) => commands.Save(r.Offering, r.Input, ct);
    public Task<LearningPublished> Handle(PublishLearningCommand r, CancellationToken ct) => commands.Publish(r.Offering, r.Input, ct);
    public Task<bool> Handle(CompleteLearningLessonCommand r, CancellationToken ct) => commands.Complete(r.Offering, r.Lesson, r.Completed, ct);
}
