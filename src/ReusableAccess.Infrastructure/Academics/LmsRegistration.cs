using System.Data;
using MediatR;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ReusableAccess.Application.Academics;
using ReusableAccess.Application.Errors;
using ReusableAccess.Contracts;
using ReusableAccess.Domain.Academics;
using ReusableAccess.Domain.Entities;
using ReusableAccess.Infrastructure.Persistence;

namespace ReusableAccess.Infrastructure.Academics;

public static class LmsRegistration
{
    public static IServiceCollection AddUniversityLms(this IServiceCollection services)
    {
        services.AddScoped<LmsData>();
        services.AddScoped<LmsAccess>();
        services.AddScoped<LmsCatalog>();
        services.AddScoped<ILmsCatalog>(p => p.GetRequiredService<LmsCatalog>());
        services.AddScoped<LmsPeople>();
        services.AddScoped<ILmsPeople>(p => p.GetRequiredService<LmsPeople>());
        services.AddScoped<ILmsAdminTableQueries, AcademicAdminTables>();
        services.AddScoped<AcademicWorkbook>();
        services.AddScoped<IAcademicWorkbookQueries>(p => p.GetRequiredService<AcademicWorkbook>());
        services.AddScoped<IAcademicWorkbookCommands>(p => p.GetRequiredService<AcademicWorkbook>());
        services.AddSingleton<StudioFlags>();
        services.AddScoped<StudioTemplates>();
        services.AddScoped<StudioMaterializer>();
        services.AddScoped<StudioScheduling>();
        services.AddScoped<IStudioTemplateQueries>(p => p.GetRequiredService<StudioTemplates>());
        services.AddScoped<IStudioTemplateCommands>(p => p.GetRequiredService<StudioTemplates>());
        services.AddScoped<IStudioScheduleQueries>(p => p.GetRequiredService<StudioScheduling>());
        services.AddScoped<IStudioScheduleCommands>(p => p.GetRequiredService<StudioScheduling>());
        services.AddScoped<LmsOfferings>();
        services.AddScoped<ILmsOfferings>(p => p.GetRequiredService<LmsOfferings>());
        services.AddScoped<LmsEnrollments>();
        services.AddScoped<ILmsEnrollment>(p => p.GetRequiredService<LmsEnrollments>());
        services.AddScoped<LmsGrading>();
        services.AddScoped<ILmsGrading>(p => p.GetRequiredService<LmsGrading>());
        services.AddScoped<IAssignmentQueries, AssignmentQueries>();
        services.AddScoped<IAssignmentCommands, AssignmentCommands>();
        services.AddScoped<CourseDiscussionAccess>();
        services.AddScoped<ICourseDiscussionQueries, CourseDiscussionQueries>();
        services.AddScoped<ICourseDiscussionCommands, CourseDiscussionCommands>();
        services.AddScoped<LmsCalendar>();
        services.AddScoped<ILmsCalendar>(p => p.GetRequiredService<LmsCalendar>());
        services.AddScoped<ILmsOperations, LmsOperations>();
        services.AddScoped<LearningReadModel>();
        services.AddScoped<LearningAccess>();
        services.AddScoped<LearningValidation>();
        services.AddScoped<ILearningQueries, LearningQueries>();
        services.AddScoped<ILearningCommands, LearningCommands>();
        services.AddScoped<LmsFiles>();
        services.AddScoped<ILmsFileTransport>(p => p.GetRequiredService<LmsFiles>());
        services.AddScoped<LmsSms>();
        services.AddHttpClient("UniversitySms", c => c.Timeout = TimeSpan.FromSeconds(15))
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
        services.AddHostedService<LmsMaintenance>();
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LmsConflicts<,>));
        return services;
    }

    // Idempotent, but never re-add a permission that the platform administrator removed.
    public static async Task SeedUniversityLmsAsync(this IServiceProvider services, CancellationToken ct = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var permissions = await db.Set<Permission>().Where(x => x.Key.StartsWith("lms.")).ToDictionaryAsync(x => x.Key, ct);
        foreach (var (code, definition) in LmsPermissions.Baseline)
        {
            if (await db.Roles.AnyAsync(x => x.Code == code, ct)) continue;
            var role = new Role(code, definition.Title);
            foreach (var key in definition.Keys) role.AddPermission(permissions[key]);
            db.Roles.Add(role);
        }
        var degrees = new (string Code, string Name)[]
        {
            ("ASSOCIATE", "کاردانی"), ("BACHELOR", "کارشناسی"), ("BACHELOR_TOPUP", "کارشناسی ناپیوسته"),
            ("MASTER", "کارشناسی ارشد"), ("MASTER_INTEGRATED", "کارشناسی ارشد پیوسته"),
            ("PROFESSIONAL_DOCTORATE", "دکتری حرفه‌ای"), ("PHD", "دکتری تخصصی")
        };
        foreach (var d in degrees)
            if (!await db.Set<Degree>().AnyAsync(x => x.Code == d.Code, ct))
                db.Add(new Degree { Code = d.Code, Title = d.Name, Active = true });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }
}

internal sealed class LmsConflicts<T, R> : IPipelineBehavior<T, R> where T : notnull
{
    public async Task<R> Handle(T request, RequestHandlerDelegate<R> next, CancellationToken ct)
    {
        try { return await next(); }
        catch (Exception e) when (request is IConcurrencyConflictRequest && typeof(T).Namespace == typeof(GetLmsHomeQuery).Namespace &&
            (e is DbUpdateConcurrencyException || e.GetBaseException() is SqlException { Number: 1205 or 2601 or 2627 }))
        { throw LmsError.Conflict("درخواست هم‌زمان تغییر کرد؛ دادهٔ تازه را دریافت کنید."); }
    }
}
