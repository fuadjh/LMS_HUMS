using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ReusableAccess.Infrastructure.Persistence.Migrations;

// Frozen LMS schema v1. This is intentionally independent from the runtime model.
internal static class AcademicSchemaV1
{
    internal static void Build(ModelBuilder model)
    {
        model.Entity("ReusableAccess.Domain.Academics.Faculty", b =>
        {
            b.Property<bool>("Active").HasColumnType("bit");
            b.Property<string>("Code").HasColumnType("nvarchar(50)").HasMaxLength(50).IsRequired();
            b.Property<Guid>("Id").ValueGeneratedNever().HasColumnType("uniqueidentifier");
            b.Property<string>("Title").HasColumnType("nvarchar(180)").HasMaxLength(180).IsRequired();
            b.HasKey("Id");
            b.HasIndex("Code").IsUnique();
            b.ToTable("Faculty", "lms");
        });
        model.Entity("ReusableAccess.Domain.Academics.Major", b =>
        {
            b.Property<bool>("Active").HasColumnType("bit");
            b.Property<string>("Code").HasColumnType("nvarchar(50)").HasMaxLength(50).IsRequired();
            b.Property<Guid>("FacultyId").HasColumnType("uniqueidentifier");
            b.Property<Guid>("Id").ValueGeneratedNever().HasColumnType("uniqueidentifier");
            b.Property<string>("Title").HasColumnType("nvarchar(180)").HasMaxLength(180).IsRequired();
            b.HasKey("Id");
            b.HasIndex("FacultyId", "Code").IsUnique();
            b.ToTable("Major", "lms");
        });
        model.Entity("ReusableAccess.Domain.Academics.Degree", b =>
        {
            b.Property<bool>("Active").HasColumnType("bit");
            b.Property<string>("Code").HasColumnType("nvarchar(50)").HasMaxLength(50).IsRequired();
            b.Property<Guid>("Id").ValueGeneratedNever().HasColumnType("uniqueidentifier");
            b.Property<string>("Title").HasColumnType("nvarchar(120)").HasMaxLength(120).IsRequired();
            b.HasKey("Id");
            b.HasIndex("Code").IsUnique();
            b.ToTable("Degree", "lms");
        });
        model.Entity("ReusableAccess.Domain.Academics.AcademicProgram", b =>
        {
            b.Property<bool>("Active").HasColumnType("bit");
            b.Property<Guid>("DegreeId").HasColumnType("uniqueidentifier");
            b.Property<Guid>("Id").ValueGeneratedNever().HasColumnType("uniqueidentifier");
            b.Property<Guid>("MajorId").HasColumnType("uniqueidentifier");
            b.HasKey("Id");
            b.HasIndex("MajorId", "DegreeId").IsUnique();
            b.HasIndex("DegreeId");
            b.ToTable("AcademicProgram", "lms");
        });
        model.Entity("ReusableAccess.Domain.Academics.Cohort", b =>
        {
            b.Property<bool>("Active").HasColumnType("bit");
            b.Property<Guid>("Id").ValueGeneratedNever().HasColumnType("uniqueidentifier");
            b.Property<Guid>("ProgramId").HasColumnType("uniqueidentifier");
            b.Property<int>("Season").HasColumnType("int");
            b.Property<int>("Year").HasColumnType("int");
            b.HasKey("Id");
            b.HasIndex("ProgramId", "Year", "Season").IsUnique();
            b.ToTable("Cohort", "lms");
        });
        model.Entity("ReusableAccess.Domain.Academics.Term", b =>
        {
            b.Property<bool>("Active").HasColumnType("bit");
            b.Property<DateTimeOffset?>("ClosedUtc").HasColumnType("datetimeoffset");
            b.Property<DateTimeOffset>("DropClosesUtc").HasColumnType("datetimeoffset");
            b.Property<DateTimeOffset>("DropOpensUtc").HasColumnType("datetimeoffset");
            b.Property<DateTimeOffset>("EndsUtc").HasColumnType("datetimeoffset");
            b.Property<DateTimeOffset>("EnrollClosesUtc").HasColumnType("datetimeoffset");
            b.Property<DateTimeOffset>("EnrollOpensUtc").HasColumnType("datetimeoffset");
            b.Property<Guid>("Id").ValueGeneratedNever().HasColumnType("uniqueidentifier");
            b.Property<int>("Season").HasColumnType("int");
            b.Property<DateTimeOffset>("StartsUtc").HasColumnType("datetimeoffset");
            b.Property<int>("Year").HasColumnType("int");
            b.HasKey("Id");
            b.HasIndex("Year", "Season").IsUnique();
            b.ToTable("Term", "lms");
        });
        model.Entity("ReusableAccess.Domain.Academics.InstructorProfile", b =>
        {
            b.Property<bool>("Active").HasColumnType("bit");
            b.Property<Guid?>("AvatarId").HasColumnType("uniqueidentifier");
            b.Property<string>("Classification").HasColumnType("nvarchar(100)").HasMaxLength(100).IsRequired();
            b.Property<string>("CvUrl").HasColumnType("nvarchar(600)").HasMaxLength(600);
            b.Property<Guid>("Id").ValueGeneratedNever().HasColumnType("uniqueidentifier");
            b.Property<Guid>("MajorId").HasColumnType("uniqueidentifier");
            b.HasKey("Id");
            b.HasIndex("MajorId");
            b.HasIndex("AvatarId");
            b.ToTable("InstructorProfile", "lms");
        });
        model.Entity("ReusableAccess.Domain.Academics.StudentProfile", b =>
        {
            b.Property<bool>("Active").HasColumnType("bit");
            b.Property<Guid?>("AvatarId").HasColumnType("uniqueidentifier");
            b.Property<Guid>("Id").ValueGeneratedNever().HasColumnType("uniqueidentifier");
            b.HasKey("Id");
            b.HasIndex("AvatarId");
            b.ToTable("StudentProfile", "lms");
        });
        model.Entity("ReusableAccess.Domain.Academics.StudyRecord", b =>
        {
            b.Property<Guid>("CohortId").HasColumnType("uniqueidentifier");
            b.Property<DateTimeOffset?>("EndedUtc").HasColumnType("datetimeoffset");
            b.Property<Guid>("Id").ValueGeneratedNever().HasColumnType("uniqueidentifier");
            b.Property<string>("Number").HasColumnType("nvarchar(50)").HasMaxLength(50).IsRequired();
            b.Property<DateTimeOffset>("OpenedUtc").HasColumnType("datetimeoffset");
            b.Property<Guid>("StudentId").HasColumnType("uniqueidentifier");
            b.HasKey("Id");
            b.HasIndex("StudentId").IsUnique().HasFilter("[EndedUtc] IS NULL");
            b.HasIndex("CohortId", "EndedUtc");
            b.HasIndex("CohortId", "Number").IsUnique().HasFilter("[EndedUtc] IS NULL");
            b.ToTable("StudyRecord", "lms");
        });
        model.Entity("ReusableAccess.Domain.Academics.AcademicGrant", b =>
        {
            b.Property<bool>("Active").HasColumnType("bit");
            b.Property<Guid>("FacultyId").HasColumnType("uniqueidentifier");
            b.Property<Guid>("Id").ValueGeneratedNever().HasColumnType("uniqueidentifier");
            b.Property<Guid?>("MajorId").HasColumnType("uniqueidentifier");
            b.Property<Guid>("UserId").HasColumnType("uniqueidentifier");
            b.HasKey("Id");
            b.HasIndex("UserId", "FacultyId", "MajorId").IsUnique().HasFilter("[MajorId] IS NOT NULL");
            b.HasIndex("UserId", "FacultyId").IsUnique().HasFilter("[MajorId] IS NULL");
            b.HasIndex("FacultyId");
            b.HasIndex("MajorId");
            b.ToTable("AcademicGrant", "lms");
        });
        model.Entity("ReusableAccess.Domain.Academics.CatalogCourse", b =>
        {
            b.Property<bool>("Active").HasColumnType("bit");
            b.Property<string>("Code").HasColumnType("nvarchar(50)").HasMaxLength(50).IsRequired();
            b.Property<Guid>("FacultyId").HasColumnType("uniqueidentifier");
            b.Property<Guid>("Id").ValueGeneratedNever().HasColumnType("uniqueidentifier");
            b.Property<string>("Title").HasColumnType("nvarchar(180)").HasMaxLength(180).IsRequired();
            b.HasKey("Id");
            b.HasIndex("FacultyId", "Code").IsUnique();
            b.ToTable("CatalogCourse", "lms");
        });
        model.Entity("ReusableAccess.Domain.Academics.Offering", b =>
        {
            b.Property<bool>("Active").HasColumnType("bit");
            b.Property<int>("Audience").HasColumnType("int");
            b.Property<Guid>("CourseId").HasColumnType("uniqueidentifier");
            b.Property<string>("GroupCode").HasColumnType("nvarchar(50)").HasMaxLength(50).IsRequired();
            b.Property<Guid>("Id").ValueGeneratedNever().HasColumnType("uniqueidentifier");
            b.Property<Guid>("LeadId").HasColumnType("uniqueidentifier");
            b.Property<Guid?>("OwnerMajorId").HasColumnType("uniqueidentifier");
            b.Property<DateTimeOffset?>("PublishedUtc").HasColumnType("datetimeoffset");
            b.Property<Guid>("Revision").HasColumnType("uniqueidentifier").IsConcurrencyToken();
            b.Property<Guid>("TermId").HasColumnType("uniqueidentifier");
            b.Property<DateTimeOffset?>("VisibleUtc").HasColumnType("datetimeoffset");
            b.HasKey("Id");
            b.HasIndex("TermId", "CourseId", "GroupCode").IsUnique();
            b.HasIndex("LeadId", "Active");
            b.HasIndex("CourseId");
            b.HasIndex("OwnerMajorId");
            b.ToTable("Offering", "lms");
        });
        model.Entity("ReusableAccess.Domain.Academics.OfferingCohort", b =>
        {
            b.Property<Guid>("CohortId").HasColumnType("uniqueidentifier");
            b.Property<Guid>("Id").ValueGeneratedNever().HasColumnType("uniqueidentifier");
            b.Property<Guid>("OfferingId").HasColumnType("uniqueidentifier");
            b.HasKey("Id");
            b.HasIndex("OfferingId", "CohortId").IsUnique();
            b.HasIndex("CohortId");
            b.ToTable("OfferingCohort", "lms");
        });
        model.Entity("ReusableAccess.Domain.Academics.CoTeacher", b =>
        {
            b.Property<bool>("Active").HasColumnType("bit");
            b.Property<bool>("CanGrade").HasColumnType("bit");
            b.Property<bool>("CanPublish").HasColumnType("bit");
            b.Property<Guid>("Id").ValueGeneratedNever().HasColumnType("uniqueidentifier");
            b.Property<Guid>("InstructorId").HasColumnType("uniqueidentifier");
            b.Property<Guid>("OfferingId").HasColumnType("uniqueidentifier");
            b.HasKey("Id");
            b.HasIndex("OfferingId", "InstructorId").IsUnique();
            b.HasIndex("InstructorId");
            b.ToTable("CoTeacher", "lms");
        });
        model.Entity("ReusableAccess.Domain.Academics.CourseTemplate", b =>
        {
            b.Property<bool>("Active").HasColumnType("bit");
            b.Property<Guid>("CourseId").HasColumnType("uniqueidentifier");
            b.Property<Guid>("Id").ValueGeneratedNever().HasColumnType("uniqueidentifier");
            b.Property<Guid>("OwnerId").HasColumnType("uniqueidentifier");
            b.Property<string>("Title").HasColumnType("nvarchar(180)").HasMaxLength(180).IsRequired();
            b.HasKey("Id");
            b.HasIndex("OwnerId", "CourseId", "Title").IsUnique();
            b.HasIndex("CourseId");
            b.ToTable("CourseTemplate", "lms");
        });
        model.Entity("ReusableAccess.Domain.Academics.TemplateVersion", b =>
        {
            b.Property<DateTimeOffset>("CreatedUtc").HasColumnType("datetimeoffset");
            b.Property<Guid>("Id").ValueGeneratedNever().HasColumnType("uniqueidentifier");
            b.Property<int>("Number").HasColumnType("int");
            b.Property<string>("Snapshot").HasColumnType("nvarchar(max)").IsRequired();
            b.Property<Guid>("TemplateId").HasColumnType("uniqueidentifier");
            b.HasKey("Id");
            b.HasIndex("TemplateId", "Number").IsUnique();
            b.ToTable("TemplateVersion", "lms");
        });
        model.Entity("ReusableAccess.Domain.Academics.Chapter", b =>
        {
            b.Property<bool>("Active").HasColumnType("bit");
            b.Property<Guid>("Id").ValueGeneratedNever().HasColumnType("uniqueidentifier");
            b.Property<Guid>("OfferingId").HasColumnType("uniqueidentifier");
            b.Property<int>("Position").HasColumnType("int");
            b.Property<DateTimeOffset?>("PublishedUtc").HasColumnType("datetimeoffset");
            b.Property<string>("Title").HasColumnType("nvarchar(180)").HasMaxLength(180).IsRequired();
            b.HasKey("Id");
            b.HasIndex("OfferingId", "Position").IsUnique();
            b.ToTable("Chapter", "lms");
        });
        model.Entity("ReusableAccess.Domain.Academics.LessonSession", b =>
        {
            b.Property<bool>("Active").HasColumnType("bit");
            b.Property<Guid>("Id").ValueGeneratedNever().HasColumnType("uniqueidentifier");
            b.Property<Guid>("OfferingId").HasColumnType("uniqueidentifier");
            b.Property<DateTimeOffset>("OpensUtc").HasColumnType("datetimeoffset");
            b.Property<string>("Title").HasColumnType("nvarchar(180)").HasMaxLength(180).IsRequired();
            b.HasKey("Id");
            b.HasIndex("OfferingId", "OpensUtc");
            b.ToTable("LessonSession", "lms");
        });
        model.Entity("ReusableAccess.Domain.Academics.Lesson", b =>
        {
            b.Property<bool>("Active").HasColumnType("bit");
            b.Property<string>("Body").HasColumnType("nvarchar(max)").IsRequired();
            b.Property<Guid>("ChapterId").HasColumnType("uniqueidentifier");
            b.Property<Guid>("Id").ValueGeneratedNever().HasColumnType("uniqueidentifier");
            b.Property<int>("Position").HasColumnType("int");
            b.Property<Guid?>("SessionId").HasColumnType("uniqueidentifier");
            b.Property<string>("Title").HasColumnType("nvarchar(180)").HasMaxLength(180).IsRequired();
            b.HasKey("Id");
            b.HasIndex("ChapterId", "Position").IsUnique();
            b.HasIndex("SessionId");
            b.ToTable("Lesson", "lms");
        });
        model.Entity("ReusableAccess.Domain.Academics.LessonFile", b =>
        {
            b.Property<Guid>("Id").ValueGeneratedNever().HasColumnType("uniqueidentifier");
            b.Property<Guid>("LessonId").HasColumnType("uniqueidentifier");
            b.Property<Guid>("MediaId").HasColumnType("uniqueidentifier");
            b.HasKey("Id");
            b.HasIndex("LessonId", "MediaId").IsUnique();
            b.HasIndex("MediaId");
            b.ToTable("LessonFile", "lms");
        });
        model.Entity("ReusableAccess.Domain.Academics.Assessment", b =>
        {
            b.Property<bool>("Active").HasColumnType("bit");
            b.Property<Guid>("ChapterId").HasColumnType("uniqueidentifier");
            b.Property<DateTimeOffset?>("CutoffUtc").HasColumnType("datetimeoffset");
            b.Property<DateTimeOffset?>("DueUtc").HasColumnType("datetimeoffset");
            b.Property<Guid>("Id").ValueGeneratedNever().HasColumnType("uniqueidentifier");
            b.Property<int>("MaxAttempts").HasColumnType("int");
            b.Property<decimal>("MaxScore").HasColumnType("decimal(9,2)");
            b.Property<string>("Prompt").HasColumnType("nvarchar(max)").IsRequired();
            b.Property<string>("QuizSpec").HasColumnType("nvarchar(max)");
            b.Property<string>("Rubric").HasColumnType("nvarchar(max)").IsRequired();
            b.Property<string>("Title").HasColumnType("nvarchar(180)").HasMaxLength(180).IsRequired();
            b.HasKey("Id");
            b.HasIndex("ChapterId");
            b.ToTable("Assessment", "lms");
        });
        model.Entity("ReusableAccess.Domain.Academics.Enrollment", b =>
        {
            b.Property<DateTimeOffset>("ChangedUtc").HasColumnType("datetimeoffset");
            b.Property<DateTimeOffset>("CreatedUtc").HasColumnType("datetimeoffset");
            b.Property<Guid>("Id").ValueGeneratedNever().HasColumnType("uniqueidentifier");
            b.Property<Guid>("OfferingId").HasColumnType("uniqueidentifier");
            b.Property<Guid>("Revision").HasColumnType("uniqueidentifier").IsConcurrencyToken();
            b.Property<int>("Status").HasColumnType("int");
            b.Property<Guid>("StudyRecordId").HasColumnType("uniqueidentifier");
            b.HasKey("Id");
            b.HasIndex("OfferingId", "StudyRecordId").IsUnique();
            b.HasIndex("StudyRecordId", "Status");
            b.ToTable("Enrollment", "lms");
        });
        model.Entity("ReusableAccess.Domain.Academics.EnrollmentEvent", b =>
        {
            b.Property<Guid>("ActorId").HasColumnType("uniqueidentifier");
            b.Property<Guid>("EnrollmentId").HasColumnType("uniqueidentifier");
            b.Property<Guid>("Id").ValueGeneratedNever().HasColumnType("uniqueidentifier");
            b.Property<DateTimeOffset>("OccurredUtc").HasColumnType("datetimeoffset");
            b.Property<string>("Reason").HasColumnType("nvarchar(500)").HasMaxLength(500);
            b.Property<int>("Status").HasColumnType("int");
            b.HasKey("Id");
            b.HasIndex("EnrollmentId", "OccurredUtc");
            b.HasIndex("ActorId");
            b.ToTable("EnrollmentEvent", "lms");
        });
        model.Entity("ReusableAccess.Domain.Academics.LessonProgress", b =>
        {
            b.Property<DateTimeOffset>("CompletedUtc").HasColumnType("datetimeoffset");
            b.Property<Guid>("EnrollmentId").HasColumnType("uniqueidentifier");
            b.Property<Guid>("Id").ValueGeneratedNever().HasColumnType("uniqueidentifier");
            b.Property<Guid>("LessonId").HasColumnType("uniqueidentifier");
            b.HasKey("Id");
            b.HasIndex("EnrollmentId", "LessonId").IsUnique();
            b.HasIndex("LessonId");
            b.ToTable("LessonProgress", "lms");
        });
        model.Entity("ReusableAccess.Domain.Academics.Submission", b =>
        {
            b.Property<Guid>("AssessmentId").HasColumnType("uniqueidentifier");
            b.Property<int>("Attempt").HasColumnType("int");
            b.Property<string>("Body").HasColumnType("nvarchar(max)").IsRequired();
            b.Property<Guid>("EnrollmentId").HasColumnType("uniqueidentifier");
            b.Property<Guid>("Id").ValueGeneratedNever().HasColumnType("uniqueidentifier");
            b.Property<bool>("IsLate").HasColumnType("bit");
            b.Property<string>("QuizAnswers").HasColumnType("nvarchar(max)");
            b.Property<DateTimeOffset>("SentUtc").HasColumnType("datetimeoffset");
            b.HasKey("Id");
            b.HasIndex("AssessmentId", "EnrollmentId", "Attempt").IsUnique();
            b.HasIndex("EnrollmentId");
            b.ToTable("Submission", "lms");
        });
        model.Entity("ReusableAccess.Domain.Academics.SubmissionFile", b =>
        {
            b.Property<Guid>("Id").ValueGeneratedNever().HasColumnType("uniqueidentifier");
            b.Property<Guid>("MediaId").HasColumnType("uniqueidentifier");
            b.Property<Guid>("SubmissionId").HasColumnType("uniqueidentifier");
            b.HasKey("Id");
            b.HasIndex("SubmissionId", "MediaId").IsUnique();
            b.HasIndex("MediaId");
            b.ToTable("SubmissionFile", "lms");
        });
        model.Entity("ReusableAccess.Domain.Academics.AssessmentGrade", b =>
        {
            b.Property<DateTimeOffset>("CreatedUtc").HasColumnType("datetimeoffset");
            b.Property<string>("Feedback").HasColumnType("nvarchar(max)").IsRequired();
            b.Property<Guid?>("GraderId").HasColumnType("uniqueidentifier");
            b.Property<Guid>("Id").ValueGeneratedNever().HasColumnType("uniqueidentifier");
            b.Property<bool>("Published").HasColumnType("bit");
            b.Property<int>("Revision").HasColumnType("int");
            b.Property<decimal>("Score").HasColumnType("decimal(9,2)");
            b.Property<Guid>("SubmissionId").HasColumnType("uniqueidentifier");
            b.HasKey("Id");
            b.HasIndex("SubmissionId", "Revision").IsUnique();
            b.HasIndex("GraderId");
            b.ToTable("AssessmentGrade", "lms");
        });
        model.Entity("ReusableAccess.Domain.Academics.FinalGrade", b =>
        {
            b.Property<DateTimeOffset>("CreatedUtc").HasColumnType("datetimeoffset");
            b.Property<Guid>("EnrollmentId").HasColumnType("uniqueidentifier");
            b.Property<string>("Feedback").HasColumnType("nvarchar(max)").IsRequired();
            b.Property<Guid>("GraderId").HasColumnType("uniqueidentifier");
            b.Property<Guid>("Id").ValueGeneratedNever().HasColumnType("uniqueidentifier");
            b.Property<bool>("Published").HasColumnType("bit");
            b.Property<int>("Revision").HasColumnType("int");
            b.Property<decimal>("Score").HasColumnType("decimal(9,2)");
            b.HasKey("Id");
            b.HasIndex("EnrollmentId", "Revision").IsUnique();
            b.HasIndex("GraderId");
            b.ToTable("FinalGrade", "lms");
        });
        model.Entity("ReusableAccess.Domain.Academics.CohortMessage", b =>
        {
            b.Property<string>("Body").HasColumnType("nvarchar(2000)").HasMaxLength(2000).IsRequired();
            b.Property<Guid>("CohortId").HasColumnType("uniqueidentifier");
            b.Property<Guid>("Id").ValueGeneratedNever().HasColumnType("uniqueidentifier");
            b.Property<Guid?>("RecipientId").HasColumnType("uniqueidentifier");
            b.Property<Guid>("SenderId").HasColumnType("uniqueidentifier");
            b.Property<DateTimeOffset>("SentUtc").HasColumnType("datetimeoffset");
            b.HasKey("Id");
            b.HasIndex("CohortId", "SentUtc");
            b.HasIndex("SenderId");
            b.HasIndex("RecipientId");
            b.ToTable("CohortMessage", "lms");
        });
        model.Entity("ReusableAccess.Domain.Academics.Reminder", b =>
        {
            b.Property<DateTimeOffset>("CreatedUtc").HasColumnType("datetimeoffset");
            b.Property<DateTimeOffset>("DueUtc").HasColumnType("datetimeoffset");
            b.Property<Guid>("EventId").HasColumnType("uniqueidentifier");
            b.Property<Guid>("Id").ValueGeneratedNever().HasColumnType("uniqueidentifier");
            b.Property<string>("Kind").HasColumnType("nvarchar(30)").HasMaxLength(30).IsRequired();
            b.Property<Guid>("OfferingId").HasColumnType("uniqueidentifier");
            b.Property<DateTimeOffset?>("ReadUtc").HasColumnType("datetimeoffset");
            b.Property<string>("SmsState").HasColumnType("nvarchar(30)").HasMaxLength(30).IsRequired();
            b.Property<string>("Title").HasColumnType("nvarchar(180)").HasMaxLength(180).IsRequired();
            b.Property<Guid>("UserId").HasColumnType("uniqueidentifier");
            b.HasKey("Id");
            b.HasIndex("UserId", "Kind", "EventId", "DueUtc").IsUnique();
            b.HasIndex("UserId", "CreatedUtc");
            b.HasIndex("SmsState", "CreatedUtc");
            b.HasIndex("OfferingId");
            b.ToTable("Reminder", "lms");
        });
        model.Entity("ReusableAccess.Domain.Academics.SmsSettings", b =>
        {
            b.Property<Guid>("Id").ValueGeneratedNever().HasColumnType("uniqueidentifier");
            b.Property<string>("ProtectedKey").HasColumnType("nvarchar(max)").IsRequired();
            b.Property<string>("Provider").HasColumnType("nvarchar(50)").HasMaxLength(50).IsRequired();
            b.Property<DateTimeOffset>("UpdatedUtc").HasColumnType("datetimeoffset");
            b.HasKey("Id");
            b.ToTable("SmsSettings", "lms");
        });
        model.Entity("ReusableAccess.Domain.Academics.Media", b =>
        {
            b.Property<long>("Bytes").HasColumnType("bigint");
            b.Property<DateTimeOffset>("CreatedUtc").HasColumnType("datetimeoffset");
            b.Property<string>("Extension").HasColumnType("nvarchar(10)").HasMaxLength(10).IsRequired();
            b.Property<Guid>("Id").ValueGeneratedNever().HasColumnType("uniqueidentifier");
            b.Property<string>("Name").HasColumnType("nvarchar(180)").HasMaxLength(180).IsRequired();
            b.Property<Guid>("OwnerId").HasColumnType("uniqueidentifier");
            b.Property<int>("Purpose").HasColumnType("int");
            b.Property<Guid>("TargetId").HasColumnType("uniqueidentifier");
            b.HasKey("Id");
            b.HasIndex("OwnerId", "Purpose", "TargetId");
            b.ToTable("Media", "lms");
        });
        model.Entity("ReusableAccess.Domain.Academics.UploadAttempt", b =>
        {
            b.Property<Guid>("ActorId").HasColumnType("uniqueidentifier");
            b.Property<string>("Code").HasColumnType("nvarchar(80)").HasMaxLength(80);
            b.Property<string>("Correlation").HasColumnType("nvarchar(100)").HasMaxLength(100);
            b.Property<DateTimeOffset>("CreatedUtc").HasColumnType("datetimeoffset");
            b.Property<string>("Extension").HasColumnType("nvarchar(10)").HasMaxLength(10).IsRequired();
            b.Property<Guid>("Id").ValueGeneratedNever().HasColumnType("uniqueidentifier");
            b.Property<string>("Ip").HasColumnType("nvarchar(64)").HasMaxLength(64);
            b.Property<string>("Name").HasColumnType("nvarchar(180)").HasMaxLength(180).IsRequired();
            b.Property<int>("Purpose").HasColumnType("int");
            b.Property<long?>("ReceivedBytes").HasColumnType("bigint");
            b.Property<long>("ReportedBytes").HasColumnType("bigint");
            b.Property<int>("State").HasColumnType("int");
            b.Property<Guid>("TargetId").HasColumnType("uniqueidentifier");
            b.Property<DateTimeOffset>("UpdatedUtc").HasColumnType("datetimeoffset");
            b.HasKey("Id");
            b.HasIndex("CreatedUtc");
            b.HasIndex("State", "UpdatedUtc");
            b.HasIndex("ActorId");
            b.ToTable("UploadAttempt", "lms");
        });
        model.Entity("ReusableAccess.Domain.Academics.OperationalEvent", b =>
        {
            b.Property<string>("Action").HasColumnType("nvarchar(120)").HasMaxLength(120).IsRequired();
            b.Property<Guid?>("ActorId").HasColumnType("uniqueidentifier");
            b.Property<string>("Correlation").HasColumnType("nvarchar(100)").HasMaxLength(100);
            b.Property<int?>("HttpStatus").HasColumnType("int");
            b.Property<Guid>("Id").ValueGeneratedNever().HasColumnType("uniqueidentifier");
            b.Property<string>("Ip").HasColumnType("nvarchar(64)").HasMaxLength(64);
            b.Property<DateTimeOffset>("OccurredUtc").HasColumnType("datetimeoffset");
            b.Property<string>("Outcome").HasColumnType("nvarchar(40)").HasMaxLength(40).IsRequired();
            b.Property<Guid?>("ResourceId").HasColumnType("uniqueidentifier");
            b.HasKey("Id");
            b.HasIndex("OccurredUtc");
            b.HasIndex("ActorId", "OccurredUtc");
            b.ToTable("OperationalEvent", "lms");
        });
        model.Entity("ReusableAccess.Domain.Academics.Major", b =>
        {
            b.HasOne("ReusableAccess.Domain.Academics.Faculty", null).WithMany().HasForeignKey("FacultyId").OnDelete(DeleteBehavior.NoAction).IsRequired();
        });
        model.Entity("ReusableAccess.Domain.Academics.AcademicProgram", b =>
        {
            b.HasOne("ReusableAccess.Domain.Academics.Major", null).WithMany().HasForeignKey("MajorId").OnDelete(DeleteBehavior.NoAction).IsRequired();
            b.HasOne("ReusableAccess.Domain.Academics.Degree", null).WithMany().HasForeignKey("DegreeId").OnDelete(DeleteBehavior.NoAction).IsRequired();
        });
        model.Entity("ReusableAccess.Domain.Academics.Cohort", b =>
        {
            b.HasOne("ReusableAccess.Domain.Academics.AcademicProgram", null).WithMany().HasForeignKey("ProgramId").OnDelete(DeleteBehavior.NoAction).IsRequired();
        });
        model.Entity("ReusableAccess.Domain.Academics.InstructorProfile", b =>
        {
            b.HasOne("ReusableAccess.Infrastructure.Identity.ApplicationUser", null).WithMany().HasForeignKey("Id").OnDelete(DeleteBehavior.NoAction).IsRequired();
            b.HasOne("ReusableAccess.Domain.Academics.Major", null).WithMany().HasForeignKey("MajorId").OnDelete(DeleteBehavior.NoAction).IsRequired();
            b.HasOne("ReusableAccess.Domain.Academics.Media", null).WithMany().HasForeignKey("AvatarId").OnDelete(DeleteBehavior.NoAction);
        });
        model.Entity("ReusableAccess.Domain.Academics.StudentProfile", b =>
        {
            b.HasOne("ReusableAccess.Infrastructure.Identity.ApplicationUser", null).WithMany().HasForeignKey("Id").OnDelete(DeleteBehavior.NoAction).IsRequired();
            b.HasOne("ReusableAccess.Domain.Academics.Media", null).WithMany().HasForeignKey("AvatarId").OnDelete(DeleteBehavior.NoAction);
        });
        model.Entity("ReusableAccess.Domain.Academics.StudyRecord", b =>
        {
            b.HasOne("ReusableAccess.Domain.Academics.StudentProfile", null).WithMany().HasForeignKey("StudentId").OnDelete(DeleteBehavior.NoAction).IsRequired();
            b.HasOne("ReusableAccess.Domain.Academics.Cohort", null).WithMany().HasForeignKey("CohortId").OnDelete(DeleteBehavior.NoAction).IsRequired();
        });
        model.Entity("ReusableAccess.Domain.Academics.AcademicGrant", b =>
        {
            b.HasOne("ReusableAccess.Infrastructure.Identity.ApplicationUser", null).WithMany().HasForeignKey("UserId").OnDelete(DeleteBehavior.NoAction).IsRequired();
            b.HasOne("ReusableAccess.Domain.Academics.Faculty", null).WithMany().HasForeignKey("FacultyId").OnDelete(DeleteBehavior.NoAction).IsRequired();
            b.HasOne("ReusableAccess.Domain.Academics.Major", null).WithMany().HasForeignKey("MajorId").OnDelete(DeleteBehavior.NoAction);
        });
        model.Entity("ReusableAccess.Domain.Academics.CatalogCourse", b =>
        {
            b.HasOne("ReusableAccess.Domain.Academics.Faculty", null).WithMany().HasForeignKey("FacultyId").OnDelete(DeleteBehavior.NoAction).IsRequired();
        });
        model.Entity("ReusableAccess.Domain.Academics.Offering", b =>
        {
            b.HasOne("ReusableAccess.Domain.Academics.CatalogCourse", null).WithMany().HasForeignKey("CourseId").OnDelete(DeleteBehavior.NoAction).IsRequired();
            b.HasOne("ReusableAccess.Domain.Academics.Term", null).WithMany().HasForeignKey("TermId").OnDelete(DeleteBehavior.NoAction).IsRequired();
            b.HasOne("ReusableAccess.Domain.Academics.Major", null).WithMany().HasForeignKey("OwnerMajorId").OnDelete(DeleteBehavior.NoAction);
            b.HasOne("ReusableAccess.Domain.Academics.InstructorProfile", null).WithMany().HasForeignKey("LeadId").OnDelete(DeleteBehavior.NoAction).IsRequired();
        });
        model.Entity("ReusableAccess.Domain.Academics.OfferingCohort", b =>
        {
            b.HasOne("ReusableAccess.Domain.Academics.Offering", null).WithMany().HasForeignKey("OfferingId").OnDelete(DeleteBehavior.NoAction).IsRequired();
            b.HasOne("ReusableAccess.Domain.Academics.Cohort", null).WithMany().HasForeignKey("CohortId").OnDelete(DeleteBehavior.NoAction).IsRequired();
        });
        model.Entity("ReusableAccess.Domain.Academics.CoTeacher", b =>
        {
            b.HasOne("ReusableAccess.Domain.Academics.Offering", null).WithMany().HasForeignKey("OfferingId").OnDelete(DeleteBehavior.NoAction).IsRequired();
            b.HasOne("ReusableAccess.Domain.Academics.InstructorProfile", null).WithMany().HasForeignKey("InstructorId").OnDelete(DeleteBehavior.NoAction).IsRequired();
        });
        model.Entity("ReusableAccess.Domain.Academics.CourseTemplate", b =>
        {
            b.HasOne("ReusableAccess.Domain.Academics.InstructorProfile", null).WithMany().HasForeignKey("OwnerId").OnDelete(DeleteBehavior.NoAction).IsRequired();
            b.HasOne("ReusableAccess.Domain.Academics.CatalogCourse", null).WithMany().HasForeignKey("CourseId").OnDelete(DeleteBehavior.NoAction).IsRequired();
        });
        model.Entity("ReusableAccess.Domain.Academics.TemplateVersion", b =>
        {
            b.HasOne("ReusableAccess.Domain.Academics.CourseTemplate", null).WithMany().HasForeignKey("TemplateId").OnDelete(DeleteBehavior.NoAction).IsRequired();
        });
        model.Entity("ReusableAccess.Domain.Academics.Chapter", b =>
        {
            b.HasOne("ReusableAccess.Domain.Academics.Offering", null).WithMany().HasForeignKey("OfferingId").OnDelete(DeleteBehavior.NoAction).IsRequired();
        });
        model.Entity("ReusableAccess.Domain.Academics.LessonSession", b =>
        {
            b.HasOne("ReusableAccess.Domain.Academics.Offering", null).WithMany().HasForeignKey("OfferingId").OnDelete(DeleteBehavior.NoAction).IsRequired();
        });
        model.Entity("ReusableAccess.Domain.Academics.Lesson", b =>
        {
            b.HasOne("ReusableAccess.Domain.Academics.Chapter", null).WithMany().HasForeignKey("ChapterId").OnDelete(DeleteBehavior.NoAction).IsRequired();
            b.HasOne("ReusableAccess.Domain.Academics.LessonSession", null).WithMany().HasForeignKey("SessionId").OnDelete(DeleteBehavior.NoAction);
        });
        model.Entity("ReusableAccess.Domain.Academics.LessonFile", b =>
        {
            b.HasOne("ReusableAccess.Domain.Academics.Lesson", null).WithMany().HasForeignKey("LessonId").OnDelete(DeleteBehavior.NoAction).IsRequired();
            b.HasOne("ReusableAccess.Domain.Academics.Media", null).WithMany().HasForeignKey("MediaId").OnDelete(DeleteBehavior.NoAction).IsRequired();
        });
        model.Entity("ReusableAccess.Domain.Academics.Assessment", b =>
        {
            b.HasOne("ReusableAccess.Domain.Academics.Chapter", null).WithMany().HasForeignKey("ChapterId").OnDelete(DeleteBehavior.NoAction).IsRequired();
        });
        model.Entity("ReusableAccess.Domain.Academics.Enrollment", b =>
        {
            b.HasOne("ReusableAccess.Domain.Academics.Offering", null).WithMany().HasForeignKey("OfferingId").OnDelete(DeleteBehavior.NoAction).IsRequired();
            b.HasOne("ReusableAccess.Domain.Academics.StudyRecord", null).WithMany().HasForeignKey("StudyRecordId").OnDelete(DeleteBehavior.NoAction).IsRequired();
        });
        model.Entity("ReusableAccess.Domain.Academics.EnrollmentEvent", b =>
        {
            b.HasOne("ReusableAccess.Domain.Academics.Enrollment", null).WithMany().HasForeignKey("EnrollmentId").OnDelete(DeleteBehavior.NoAction).IsRequired();
            b.HasOne("ReusableAccess.Infrastructure.Identity.ApplicationUser", null).WithMany().HasForeignKey("ActorId").OnDelete(DeleteBehavior.NoAction).IsRequired();
        });
        model.Entity("ReusableAccess.Domain.Academics.LessonProgress", b =>
        {
            b.HasOne("ReusableAccess.Domain.Academics.Enrollment", null).WithMany().HasForeignKey("EnrollmentId").OnDelete(DeleteBehavior.NoAction).IsRequired();
            b.HasOne("ReusableAccess.Domain.Academics.Lesson", null).WithMany().HasForeignKey("LessonId").OnDelete(DeleteBehavior.NoAction).IsRequired();
        });
        model.Entity("ReusableAccess.Domain.Academics.Submission", b =>
        {
            b.HasOne("ReusableAccess.Domain.Academics.Assessment", null).WithMany().HasForeignKey("AssessmentId").OnDelete(DeleteBehavior.NoAction).IsRequired();
            b.HasOne("ReusableAccess.Domain.Academics.Enrollment", null).WithMany().HasForeignKey("EnrollmentId").OnDelete(DeleteBehavior.NoAction).IsRequired();
        });
        model.Entity("ReusableAccess.Domain.Academics.SubmissionFile", b =>
        {
            b.HasOne("ReusableAccess.Domain.Academics.Submission", null).WithMany().HasForeignKey("SubmissionId").OnDelete(DeleteBehavior.NoAction).IsRequired();
            b.HasOne("ReusableAccess.Domain.Academics.Media", null).WithMany().HasForeignKey("MediaId").OnDelete(DeleteBehavior.NoAction).IsRequired();
        });
        model.Entity("ReusableAccess.Domain.Academics.AssessmentGrade", b =>
        {
            b.HasOne("ReusableAccess.Domain.Academics.Submission", null).WithMany().HasForeignKey("SubmissionId").OnDelete(DeleteBehavior.NoAction).IsRequired();
            b.HasOne("ReusableAccess.Infrastructure.Identity.ApplicationUser", null).WithMany().HasForeignKey("GraderId").OnDelete(DeleteBehavior.NoAction);
        });
        model.Entity("ReusableAccess.Domain.Academics.FinalGrade", b =>
        {
            b.HasOne("ReusableAccess.Domain.Academics.Enrollment", null).WithMany().HasForeignKey("EnrollmentId").OnDelete(DeleteBehavior.NoAction).IsRequired();
            b.HasOne("ReusableAccess.Infrastructure.Identity.ApplicationUser", null).WithMany().HasForeignKey("GraderId").OnDelete(DeleteBehavior.NoAction).IsRequired();
        });
        model.Entity("ReusableAccess.Domain.Academics.CohortMessage", b =>
        {
            b.HasOne("ReusableAccess.Domain.Academics.Cohort", null).WithMany().HasForeignKey("CohortId").OnDelete(DeleteBehavior.NoAction).IsRequired();
            b.HasOne("ReusableAccess.Domain.Academics.StudentProfile", null).WithMany().HasForeignKey("SenderId").OnDelete(DeleteBehavior.NoAction).IsRequired();
            b.HasOne("ReusableAccess.Domain.Academics.StudentProfile", null).WithMany().HasForeignKey("RecipientId").OnDelete(DeleteBehavior.NoAction);
        });
        model.Entity("ReusableAccess.Domain.Academics.Reminder", b =>
        {
            b.HasOne("ReusableAccess.Infrastructure.Identity.ApplicationUser", null).WithMany().HasForeignKey("UserId").OnDelete(DeleteBehavior.NoAction).IsRequired();
            b.HasOne("ReusableAccess.Domain.Academics.Offering", null).WithMany().HasForeignKey("OfferingId").OnDelete(DeleteBehavior.NoAction).IsRequired();
        });
        model.Entity("ReusableAccess.Domain.Academics.Media", b =>
        {
            b.HasOne("ReusableAccess.Infrastructure.Identity.ApplicationUser", null).WithMany().HasForeignKey("OwnerId").OnDelete(DeleteBehavior.NoAction).IsRequired();
        });
        model.Entity("ReusableAccess.Domain.Academics.UploadAttempt", b =>
        {
            b.HasOne("ReusableAccess.Infrastructure.Identity.ApplicationUser", null).WithMany().HasForeignKey("ActorId").OnDelete(DeleteBehavior.NoAction).IsRequired();
        });
    }
}
