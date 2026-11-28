using ReusableAccess.Contracts;
using ReusableAccess.Web.Components.Layout;
using ReusableAccess.Web.Components.Lms;

namespace ReusableAccess.Web.Academics;

internal static class LmsLayoutRoutes
{
    internal static Type For(Type type, IReadOnlyDictionary<string, object?> values)
    {
        if (type.Namespace != typeof(LmsDashboard).Namespace) return typeof(MainLayout);
        if (type == typeof(LmsDashboard)) return Mode(Value(values, "Mode"));
        if (type == typeof(LmsOffering) || type == typeof(LmsOfferingList) || type == typeof(LmsCalendarPage) || type == typeof(LmsStudy))
            return Mode(Value(values, "View"));
        if (type == typeof(LmsAdminCatalog) || type == typeof(LmsAdminCourses) || type == typeof(LmsAdminTeachers) || type == typeof(LmsAdminStudents) || type == typeof(LmsAdminRoles) || type == typeof(LmsAdminPeople) || type == typeof(LmsAdminGrants) || type == typeof(LmsOperationsPage))
            return typeof(LmsAdminLayout);
        return type == typeof(LmsChat) || type == typeof(LmsTranscript) ? typeof(LmsStudentLayout) : typeof(LmsInstructorLayout);
    }

    private static string? Value(IReadOnlyDictionary<string, object?> values, string key) => values.TryGetValue(key, out var value) ? value as string : null;
    private static Type Mode(string? value) => value switch
    {
        "admin" => typeof(LmsAdminLayout), "teacher" or "preview" => typeof(LmsInstructorLayout),
        "student" => typeof(LmsStudentLayout), _ => typeof(LmsEntryLayout)
    };

    internal static bool Allowed(LmsHome home, string mode) => mode switch
    {
        "admin" => home.PlatformAdmin || home.EducationAdmin, "teacher" => home.Teacher,
        "student" => home.Student, _ => false
    };
    internal static IReadOnlyList<string> Available(LmsHome home) =>
        new[] { "admin", "teacher", "student" }.Where(mode => Allowed(home, mode)).ToArray();
    internal static string Label(string mode) => mode switch
    {
        "admin" => "مدیر آموزش", "teacher" => "مدرس", "student" => "دانشجو", _ => "سامانهٔ یادگیری"
    };
}
