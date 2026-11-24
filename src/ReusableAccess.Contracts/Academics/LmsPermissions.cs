namespace ReusableAccess.Contracts;

// The existing role/permission editor is authoritative. An educational grant
// establishes resource scope; it never grants platform/legacy permissions.
public static class LmsPermissions
{
    public const string StudentRole = "LMS_STUDENT", TeacherRole = "LMS_INSTRUCTOR",
        FacultyRole = "LMS_FACULTY_ADMIN", MajorRole = "LMS_MAJOR_ADMIN";

    public const string CatalogRead = "lms.catalog.read", CatalogWrite = "lms.catalog.write",
        PeopleRead = "lms.people.read", PeopleWrite = "lms.people.write", Import = "lms.people.import",
        Assign = "lms.offering.assign", AdminRemove = "lms.enrollment.remove",
        Teach = "lms.teaching.read", Content = "lms.teaching.content", Publish = "lms.teaching.publish",
        Template = "lms.teaching.template", Grade = "lms.grading.draft", Release = "lms.grading.publish",
        Learn = "lms.student.read", Enroll = "lms.enrollment.join", Withdraw = "lms.enrollment.withdraw",
        Progress = "lms.student.progress", Submit = "lms.student.submit", Chat = "lms.chat.read", Send = "lms.chat.send",
        Calendar = "lms.calendar.read", Acknowledge = "lms.calendar.acknowledge",
        OwnAvatar = "lms.media.avatar", LessonUpload = "lms.media.lesson", SubmissionUpload = "lms.media.submission",
        Logs = "lms.operations.logs", ManageSms = "lms.operations.sms", Grant = "lms.operations.grant";

    public static readonly IReadOnlyDictionary<string, string> All = new Dictionary<string, string>
    {
        [CatalogRead] = "مشاهده کاتالوگ در قلمرو آموزشی", [CatalogWrite] = "ایجاد، ویرایش و بایگانی کاتالوگ",
        [PeopleRead] = "مشاهده یا استعلام پروفایل در قلمرو مجاز", [PeopleWrite] = "ثبت، انتقال و غیرفعال‌سازی پروفایل در قلمرو مجاز",
        [Import] = "پیش‌نمایش و ورود Excel در قلمرو مجاز", [Assign] = "تعریف گروه و واگذاری استاد مسئول یا هم‌مدرس",
        [AdminRemove] = "حذف ثبت‌نام دانشجو با دلیل در قلمرو مجاز", [Teach] = "مشاهده ارائه تدریس و پیش‌نمایش محتوای دانشجو",
        [Content] = "زمان‌بندی و آماده‌سازی محتوا در ارائهٔ من", [Publish] = "انتشار ارائه و فصل در ارائهٔ مجاز",
        [Template] = "نسخه‌بندی و استفاده از قالب خصوصی", [Grade] = "ثبت پیش‌نویس نمره و بازخورد خصوصی",
        [Release] = "انتشار نمره و بازخورد برای دانشجو", [Learn] = "مشاهده دروس، تحویل‌ها و کارنامهٔ خود",
        [Enroll] = "اخذ شخصیِ ارائه در مهلت مجاز", [Withdraw] = "خروج از ثبت‌نام خود در بازهٔ حذف‌واضافه",
        [Progress] = "ثبت تکمیل درسک بازشدهٔ خود", [Submit] = "تحویل و ارسال مجدد تکلیف خود",
        [Chat] = "خواندن پیام گروهی و خصوصیِ ورودیِ فعال", [Send] = "ارسال پیام در ورودی فعال",
        [Calendar] = "نمایش تقویم و یادآوری‌های شخصی", [Acknowledge] = "خوانده‌علامت‌زدن اعلان خود",
        [OwnAvatar] = "ثبت و مشاهدهٔ تصویر خصوصیِ پروفایل خود", [LessonUpload] = "ارسال و اتصال فایل به درسک مجاز",
        [SubmissionUpload] = "ارسال فایل تکلیف خود در مهلت مجاز", [Logs] = "جستجوی لاگ ارسال و امنیت (فقط ادمین کل)",
        [ManageSms] = "تنظیم ارسال پیامک آموزشی (فقط ادمین کل)", [Grant] = "تخصیص قلمرو ادمین آموزشی (فقط ادمین کل)"
    };

    private static readonly string[] Admin = [CatalogRead, CatalogWrite, PeopleRead, PeopleWrite, Import, Assign, AdminRemove];
    public static readonly IReadOnlyDictionary<string, (string Title, string[] Keys)> Baseline =
        new Dictionary<string, (string, string[])>(StringComparer.OrdinalIgnoreCase)
        {
            [StudentRole] = ("دانشجو", [Learn, Enroll, Withdraw, Progress, Submit, Chat, Send, Calendar, Acknowledge, OwnAvatar, SubmissionUpload]),
            [TeacherRole] = ("استاد", [Teach, Content, Publish, Template, Grade, Release, Calendar, Acknowledge, OwnAvatar, LessonUpload]),
            [FacultyRole] = ("ادمین دانشکده", Admin), [MajorRole] = ("ادمین رشته", Admin)
        };

    public static bool IsReservedRole(string code) => Baseline.ContainsKey(code);
    public static string Category(string key) => key.Split('.').ElementAtOrDefault(1) switch
    {
        "catalog" => "آموزش · ساختار دانشگاه", "people" => "آموزش · اشخاص",
        "offering" or "teaching" => "آموزش · تدریس و ارائه", "enrollment" or "student" => "آموزش · دانشجو و اخذ درس",
        "grading" => "آموزش · ارزیابی", "chat" or "calendar" => "آموزش · گفتگو و تقویم",
        "media" => "آموزش · فایل", _ => "آموزش · عملیات ادمین کل"
    };
}
