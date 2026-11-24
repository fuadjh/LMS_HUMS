using Microsoft.EntityFrameworkCore.Migrations;

namespace ReusableAccess.Infrastructure.Persistence.Migrations;

public partial class AddUniversityLms : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema("lms");
        migrationBuilder.CreateTable(
            name: "Faculty", schema: "lms",
            columns: t => new
            {
                Id = t.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Code = t.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                Title = t.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: false),
                Active = t.Column<bool>(type: "bit", nullable: false),
            },
            constraints: t => t.PrimaryKey("PK_Faculty", x => x.Id));
        migrationBuilder.CreateTable(
            name: "Major", schema: "lms",
            columns: t => new
            {
                Id = t.Column<Guid>(type: "uniqueidentifier", nullable: false),
                FacultyId = t.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Code = t.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                Title = t.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: false),
                Active = t.Column<bool>(type: "bit", nullable: false),
            },
            constraints: t => t.PrimaryKey("PK_Major", x => x.Id));
        migrationBuilder.CreateTable(
            name: "Degree", schema: "lms",
            columns: t => new
            {
                Id = t.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Code = t.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                Title = t.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                Active = t.Column<bool>(type: "bit", nullable: false),
            },
            constraints: t => t.PrimaryKey("PK_Degree", x => x.Id));
        migrationBuilder.CreateTable(
            name: "AcademicProgram", schema: "lms",
            columns: t => new
            {
                Id = t.Column<Guid>(type: "uniqueidentifier", nullable: false),
                MajorId = t.Column<Guid>(type: "uniqueidentifier", nullable: false),
                DegreeId = t.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Active = t.Column<bool>(type: "bit", nullable: false),
            },
            constraints: t => t.PrimaryKey("PK_AcademicProgram", x => x.Id));
        migrationBuilder.CreateTable(
            name: "Cohort", schema: "lms",
            columns: t => new
            {
                Id = t.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ProgramId = t.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Year = t.Column<int>(type: "int", nullable: false),
                Season = t.Column<int>(type: "int", nullable: false),
                Active = t.Column<bool>(type: "bit", nullable: false),
            },
            constraints: t => t.PrimaryKey("PK_Cohort", x => x.Id));
        migrationBuilder.CreateTable(
            name: "Term", schema: "lms",
            columns: t => new
            {
                Id = t.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Year = t.Column<int>(type: "int", nullable: false),
                Season = t.Column<int>(type: "int", nullable: false),
                StartsUtc = t.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                EndsUtc = t.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                EnrollOpensUtc = t.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                EnrollClosesUtc = t.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                DropOpensUtc = t.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                DropClosesUtc = t.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                ClosedUtc = t.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                Active = t.Column<bool>(type: "bit", nullable: false),
            },
            constraints: t => t.PrimaryKey("PK_Term", x => x.Id));
        migrationBuilder.CreateTable(
            name: "InstructorProfile", schema: "lms",
            columns: t => new
            {
                Id = t.Column<Guid>(type: "uniqueidentifier", nullable: false),
                MajorId = t.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Classification = t.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                CvUrl = t.Column<string>(type: "nvarchar(600)", maxLength: 600, nullable: true),
                AvatarId = t.Column<Guid>(type: "uniqueidentifier", nullable: true),
                Active = t.Column<bool>(type: "bit", nullable: false),
            },
            constraints: t => t.PrimaryKey("PK_InstructorProfile", x => x.Id));
        migrationBuilder.CreateTable(
            name: "StudentProfile", schema: "lms",
            columns: t => new
            {
                Id = t.Column<Guid>(type: "uniqueidentifier", nullable: false),
                AvatarId = t.Column<Guid>(type: "uniqueidentifier", nullable: true),
                Active = t.Column<bool>(type: "bit", nullable: false),
            },
            constraints: t => t.PrimaryKey("PK_StudentProfile", x => x.Id));
        migrationBuilder.CreateTable(
            name: "StudyRecord", schema: "lms",
            columns: t => new
            {
                Id = t.Column<Guid>(type: "uniqueidentifier", nullable: false),
                StudentId = t.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CohortId = t.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Number = t.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                OpenedUtc = t.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                EndedUtc = t.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
            },
            constraints: t => t.PrimaryKey("PK_StudyRecord", x => x.Id));
        migrationBuilder.CreateTable(
            name: "AcademicGrant", schema: "lms",
            columns: t => new
            {
                Id = t.Column<Guid>(type: "uniqueidentifier", nullable: false),
                UserId = t.Column<Guid>(type: "uniqueidentifier", nullable: false),
                FacultyId = t.Column<Guid>(type: "uniqueidentifier", nullable: false),
                MajorId = t.Column<Guid>(type: "uniqueidentifier", nullable: true),
                Active = t.Column<bool>(type: "bit", nullable: false),
            },
            constraints: t => t.PrimaryKey("PK_AcademicGrant", x => x.Id));
        migrationBuilder.CreateTable(
            name: "CatalogCourse", schema: "lms",
            columns: t => new
            {
                Id = t.Column<Guid>(type: "uniqueidentifier", nullable: false),
                FacultyId = t.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Code = t.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                Title = t.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: false),
                Active = t.Column<bool>(type: "bit", nullable: false),
            },
            constraints: t => t.PrimaryKey("PK_CatalogCourse", x => x.Id));
        migrationBuilder.CreateTable(
            name: "Offering", schema: "lms",
            columns: t => new
            {
                Id = t.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CourseId = t.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TermId = t.Column<Guid>(type: "uniqueidentifier", nullable: false),
                OwnerMajorId = t.Column<Guid>(type: "uniqueidentifier", nullable: true),
                LeadId = t.Column<Guid>(type: "uniqueidentifier", nullable: false),
                GroupCode = t.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                Audience = t.Column<int>(type: "int", nullable: false),
                VisibleUtc = t.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                PublishedUtc = t.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                Active = t.Column<bool>(type: "bit", nullable: false),
                Revision = t.Column<Guid>(type: "uniqueidentifier", nullable: false),
            },
            constraints: t => t.PrimaryKey("PK_Offering", x => x.Id));
        migrationBuilder.CreateTable(
            name: "OfferingCohort", schema: "lms",
            columns: t => new
            {
                Id = t.Column<Guid>(type: "uniqueidentifier", nullable: false),
                OfferingId = t.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CohortId = t.Column<Guid>(type: "uniqueidentifier", nullable: false),
            },
            constraints: t => t.PrimaryKey("PK_OfferingCohort", x => x.Id));
        migrationBuilder.CreateTable(
            name: "CoTeacher", schema: "lms",
            columns: t => new
            {
                Id = t.Column<Guid>(type: "uniqueidentifier", nullable: false),
                OfferingId = t.Column<Guid>(type: "uniqueidentifier", nullable: false),
                InstructorId = t.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CanGrade = t.Column<bool>(type: "bit", nullable: false),
                CanPublish = t.Column<bool>(type: "bit", nullable: false),
                Active = t.Column<bool>(type: "bit", nullable: false),
            },
            constraints: t => t.PrimaryKey("PK_CoTeacher", x => x.Id));
        migrationBuilder.CreateTable(
            name: "CourseTemplate", schema: "lms",
            columns: t => new
            {
                Id = t.Column<Guid>(type: "uniqueidentifier", nullable: false),
                OwnerId = t.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CourseId = t.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Title = t.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: false),
                Active = t.Column<bool>(type: "bit", nullable: false),
            },
            constraints: t => t.PrimaryKey("PK_CourseTemplate", x => x.Id));
        migrationBuilder.CreateTable(
            name: "TemplateVersion", schema: "lms",
            columns: t => new
            {
                Id = t.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TemplateId = t.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Number = t.Column<int>(type: "int", nullable: false),
                CreatedUtc = t.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                Snapshot = t.Column<string>(type: "nvarchar(max)", nullable: false),
            },
            constraints: t => t.PrimaryKey("PK_TemplateVersion", x => x.Id));
        migrationBuilder.CreateTable(
            name: "Chapter", schema: "lms",
            columns: t => new
            {
                Id = t.Column<Guid>(type: "uniqueidentifier", nullable: false),
                OfferingId = t.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Title = t.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: false),
                Position = t.Column<int>(type: "int", nullable: false),
                PublishedUtc = t.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                Active = t.Column<bool>(type: "bit", nullable: false),
            },
            constraints: t => t.PrimaryKey("PK_Chapter", x => x.Id));
        migrationBuilder.CreateTable(
            name: "LessonSession", schema: "lms",
            columns: t => new
            {
                Id = t.Column<Guid>(type: "uniqueidentifier", nullable: false),
                OfferingId = t.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Title = t.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: false),
                OpensUtc = t.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                Active = t.Column<bool>(type: "bit", nullable: false),
            },
            constraints: t => t.PrimaryKey("PK_LessonSession", x => x.Id));
        migrationBuilder.CreateTable(
            name: "Lesson", schema: "lms",
            columns: t => new
            {
                Id = t.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ChapterId = t.Column<Guid>(type: "uniqueidentifier", nullable: false),
                SessionId = t.Column<Guid>(type: "uniqueidentifier", nullable: true),
                Title = t.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: false),
                Body = t.Column<string>(type: "nvarchar(max)", nullable: false),
                Position = t.Column<int>(type: "int", nullable: false),
                Active = t.Column<bool>(type: "bit", nullable: false),
            },
            constraints: t => t.PrimaryKey("PK_Lesson", x => x.Id));
        migrationBuilder.CreateTable(
            name: "LessonFile", schema: "lms",
            columns: t => new
            {
                Id = t.Column<Guid>(type: "uniqueidentifier", nullable: false),
                LessonId = t.Column<Guid>(type: "uniqueidentifier", nullable: false),
                MediaId = t.Column<Guid>(type: "uniqueidentifier", nullable: false),
            },
            constraints: t => t.PrimaryKey("PK_LessonFile", x => x.Id));
        migrationBuilder.CreateTable(
            name: "Assessment", schema: "lms",
            columns: t => new
            {
                Id = t.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ChapterId = t.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Title = t.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: false),
                Prompt = t.Column<string>(type: "nvarchar(max)", nullable: false),
                Rubric = t.Column<string>(type: "nvarchar(max)", nullable: false),
                DueUtc = t.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                CutoffUtc = t.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                MaxAttempts = t.Column<int>(type: "int", nullable: false),
                MaxScore = t.Column<decimal>(type: "decimal(9,2)", nullable: false),
                QuizSpec = t.Column<string>(type: "nvarchar(max)", nullable: true),
                Active = t.Column<bool>(type: "bit", nullable: false),
            },
            constraints: t => t.PrimaryKey("PK_Assessment", x => x.Id));
        migrationBuilder.CreateTable(
            name: "Enrollment", schema: "lms",
            columns: t => new
            {
                Id = t.Column<Guid>(type: "uniqueidentifier", nullable: false),
                OfferingId = t.Column<Guid>(type: "uniqueidentifier", nullable: false),
                StudyRecordId = t.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Status = t.Column<int>(type: "int", nullable: false),
                CreatedUtc = t.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                ChangedUtc = t.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                Revision = t.Column<Guid>(type: "uniqueidentifier", nullable: false),
            },
            constraints: t => t.PrimaryKey("PK_Enrollment", x => x.Id));
        migrationBuilder.CreateTable(
            name: "EnrollmentEvent", schema: "lms",
            columns: t => new
            {
                Id = t.Column<Guid>(type: "uniqueidentifier", nullable: false),
                EnrollmentId = t.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ActorId = t.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Status = t.Column<int>(type: "int", nullable: false),
                OccurredUtc = t.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                Reason = t.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
            },
            constraints: t => t.PrimaryKey("PK_EnrollmentEvent", x => x.Id));
        migrationBuilder.CreateTable(
            name: "LessonProgress", schema: "lms",
            columns: t => new
            {
                Id = t.Column<Guid>(type: "uniqueidentifier", nullable: false),
                EnrollmentId = t.Column<Guid>(type: "uniqueidentifier", nullable: false),
                LessonId = t.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CompletedUtc = t.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
            },
            constraints: t => t.PrimaryKey("PK_LessonProgress", x => x.Id));
        migrationBuilder.CreateTable(
            name: "Submission", schema: "lms",
            columns: t => new
            {
                Id = t.Column<Guid>(type: "uniqueidentifier", nullable: false),
                AssessmentId = t.Column<Guid>(type: "uniqueidentifier", nullable: false),
                EnrollmentId = t.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Attempt = t.Column<int>(type: "int", nullable: false),
                SentUtc = t.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                IsLate = t.Column<bool>(type: "bit", nullable: false),
                Body = t.Column<string>(type: "nvarchar(max)", nullable: false),
                QuizAnswers = t.Column<string>(type: "nvarchar(max)", nullable: true),
            },
            constraints: t => t.PrimaryKey("PK_Submission", x => x.Id));
        migrationBuilder.CreateTable(
            name: "SubmissionFile", schema: "lms",
            columns: t => new
            {
                Id = t.Column<Guid>(type: "uniqueidentifier", nullable: false),
                SubmissionId = t.Column<Guid>(type: "uniqueidentifier", nullable: false),
                MediaId = t.Column<Guid>(type: "uniqueidentifier", nullable: false),
            },
            constraints: t => t.PrimaryKey("PK_SubmissionFile", x => x.Id));
        migrationBuilder.CreateTable(
            name: "AssessmentGrade", schema: "lms",
            columns: t => new
            {
                Id = t.Column<Guid>(type: "uniqueidentifier", nullable: false),
                SubmissionId = t.Column<Guid>(type: "uniqueidentifier", nullable: false),
                GraderId = t.Column<Guid>(type: "uniqueidentifier", nullable: true),
                Revision = t.Column<int>(type: "int", nullable: false),
                Score = t.Column<decimal>(type: "decimal(9,2)", nullable: false),
                Feedback = t.Column<string>(type: "nvarchar(max)", nullable: false),
                Published = t.Column<bool>(type: "bit", nullable: false),
                CreatedUtc = t.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
            },
            constraints: t => t.PrimaryKey("PK_AssessmentGrade", x => x.Id));
        migrationBuilder.CreateTable(
            name: "FinalGrade", schema: "lms",
            columns: t => new
            {
                Id = t.Column<Guid>(type: "uniqueidentifier", nullable: false),
                EnrollmentId = t.Column<Guid>(type: "uniqueidentifier", nullable: false),
                GraderId = t.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Revision = t.Column<int>(type: "int", nullable: false),
                Score = t.Column<decimal>(type: "decimal(9,2)", nullable: false),
                Feedback = t.Column<string>(type: "nvarchar(max)", nullable: false),
                Published = t.Column<bool>(type: "bit", nullable: false),
                CreatedUtc = t.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
            },
            constraints: t => t.PrimaryKey("PK_FinalGrade", x => x.Id));
        migrationBuilder.CreateTable(
            name: "CohortMessage", schema: "lms",
            columns: t => new
            {
                Id = t.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CohortId = t.Column<Guid>(type: "uniqueidentifier", nullable: false),
                SenderId = t.Column<Guid>(type: "uniqueidentifier", nullable: false),
                RecipientId = t.Column<Guid>(type: "uniqueidentifier", nullable: true),
                SentUtc = t.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                Body = t.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
            },
            constraints: t => t.PrimaryKey("PK_CohortMessage", x => x.Id));
        migrationBuilder.CreateTable(
            name: "Reminder", schema: "lms",
            columns: t => new
            {
                Id = t.Column<Guid>(type: "uniqueidentifier", nullable: false),
                UserId = t.Column<Guid>(type: "uniqueidentifier", nullable: false),
                OfferingId = t.Column<Guid>(type: "uniqueidentifier", nullable: false),
                EventId = t.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Kind = t.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                Title = t.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: false),
                DueUtc = t.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                CreatedUtc = t.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                ReadUtc = t.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                SmsState = t.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
            },
            constraints: t => t.PrimaryKey("PK_Reminder", x => x.Id));
        migrationBuilder.CreateTable(
            name: "SmsSettings", schema: "lms",
            columns: t => new
            {
                Id = t.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Provider = t.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                ProtectedKey = t.Column<string>(type: "nvarchar(max)", nullable: false),
                UpdatedUtc = t.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
            },
            constraints: t => t.PrimaryKey("PK_SmsSettings", x => x.Id));
        migrationBuilder.CreateTable(
            name: "Media", schema: "lms",
            columns: t => new
            {
                Id = t.Column<Guid>(type: "uniqueidentifier", nullable: false),
                OwnerId = t.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TargetId = t.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Purpose = t.Column<int>(type: "int", nullable: false),
                Bytes = t.Column<long>(type: "bigint", nullable: false),
                Name = t.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: false),
                Extension = t.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                CreatedUtc = t.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
            },
            constraints: t => t.PrimaryKey("PK_Media", x => x.Id));
        migrationBuilder.CreateTable(
            name: "UploadAttempt", schema: "lms",
            columns: t => new
            {
                Id = t.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ActorId = t.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TargetId = t.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Purpose = t.Column<int>(type: "int", nullable: false),
                Name = t.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: false),
                Extension = t.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                ReportedBytes = t.Column<long>(type: "bigint", nullable: false),
                ReceivedBytes = t.Column<long>(type: "bigint", nullable: true),
                CreatedUtc = t.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedUtc = t.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                State = t.Column<int>(type: "int", nullable: false),
                Code = t.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                Ip = t.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                Correlation = t.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
            },
            constraints: t => t.PrimaryKey("PK_UploadAttempt", x => x.Id));
        migrationBuilder.CreateTable(
            name: "OperationalEvent", schema: "lms",
            columns: t => new
            {
                Id = t.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ActorId = t.Column<Guid>(type: "uniqueidentifier", nullable: true),
                OccurredUtc = t.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                Action = t.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                Outcome = t.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                ResourceId = t.Column<Guid>(type: "uniqueidentifier", nullable: true),
                Ip = t.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                Correlation = t.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                HttpStatus = t.Column<int>(type: "int", nullable: true),
            },
            constraints: t => t.PrimaryKey("PK_OperationalEvent", x => x.Id));
        migrationBuilder.CreateIndex(name: "IX_Faculty_Code", schema: "lms", table: "Faculty", column: "Code", unique: true);
        migrationBuilder.CreateIndex(name: "IX_Major_FacultyId_Code", schema: "lms", table: "Major", columns: new[] { "FacultyId", "Code" }, unique: true);
        migrationBuilder.CreateIndex(name: "IX_Degree_Code", schema: "lms", table: "Degree", column: "Code", unique: true);
        migrationBuilder.CreateIndex(name: "IX_AcademicProgram_MajorId_DegreeId", schema: "lms", table: "AcademicProgram", columns: new[] { "MajorId", "DegreeId" }, unique: true);
        migrationBuilder.CreateIndex(name: "IX_AcademicProgram_DegreeId", schema: "lms", table: "AcademicProgram", column: "DegreeId");
        migrationBuilder.CreateIndex(name: "IX_Cohort_ProgramId_Year_Season", schema: "lms", table: "Cohort", columns: new[] { "ProgramId", "Year", "Season" }, unique: true);
        migrationBuilder.CreateIndex(name: "IX_Term_Year_Season", schema: "lms", table: "Term", columns: new[] { "Year", "Season" }, unique: true);
        migrationBuilder.CreateIndex(name: "IX_InstructorProfile_MajorId", schema: "lms", table: "InstructorProfile", column: "MajorId");
        migrationBuilder.CreateIndex(name: "IX_InstructorProfile_AvatarId", schema: "lms", table: "InstructorProfile", column: "AvatarId");
        migrationBuilder.CreateIndex(name: "IX_StudentProfile_AvatarId", schema: "lms", table: "StudentProfile", column: "AvatarId");
        migrationBuilder.CreateIndex(name: "IX_StudyRecord_StudentId", schema: "lms", table: "StudyRecord", column: "StudentId", unique: true, filter: "[EndedUtc] IS NULL");
        migrationBuilder.CreateIndex(name: "IX_StudyRecord_CohortId_EndedUtc", schema: "lms", table: "StudyRecord", columns: new[] { "CohortId", "EndedUtc" });
        migrationBuilder.CreateIndex(name: "IX_StudyRecord_CohortId_Number", schema: "lms", table: "StudyRecord", columns: new[] { "CohortId", "Number" }, unique: true, filter: "[EndedUtc] IS NULL");
        migrationBuilder.CreateIndex(name: "IX_AcademicGrant_UserId_FacultyId_MajorId", schema: "lms", table: "AcademicGrant", columns: new[] { "UserId", "FacultyId", "MajorId" }, unique: true, filter: "[MajorId] IS NOT NULL");
        migrationBuilder.CreateIndex(name: "IX_AcademicGrant_UserId_FacultyId", schema: "lms", table: "AcademicGrant", columns: new[] { "UserId", "FacultyId" }, unique: true, filter: "[MajorId] IS NULL");
        migrationBuilder.CreateIndex(name: "IX_AcademicGrant_FacultyId", schema: "lms", table: "AcademicGrant", column: "FacultyId");
        migrationBuilder.CreateIndex(name: "IX_AcademicGrant_MajorId", schema: "lms", table: "AcademicGrant", column: "MajorId");
        migrationBuilder.CreateIndex(name: "IX_CatalogCourse_FacultyId_Code", schema: "lms", table: "CatalogCourse", columns: new[] { "FacultyId", "Code" }, unique: true);
        migrationBuilder.CreateIndex(name: "IX_Offering_TermId_CourseId_GroupCode", schema: "lms", table: "Offering", columns: new[] { "TermId", "CourseId", "GroupCode" }, unique: true);
        migrationBuilder.CreateIndex(name: "IX_Offering_LeadId_Active", schema: "lms", table: "Offering", columns: new[] { "LeadId", "Active" });
        migrationBuilder.CreateIndex(name: "IX_Offering_CourseId", schema: "lms", table: "Offering", column: "CourseId");
        migrationBuilder.CreateIndex(name: "IX_Offering_OwnerMajorId", schema: "lms", table: "Offering", column: "OwnerMajorId");
        migrationBuilder.CreateIndex(name: "IX_OfferingCohort_OfferingId_CohortId", schema: "lms", table: "OfferingCohort", columns: new[] { "OfferingId", "CohortId" }, unique: true);
        migrationBuilder.CreateIndex(name: "IX_OfferingCohort_CohortId", schema: "lms", table: "OfferingCohort", column: "CohortId");
        migrationBuilder.CreateIndex(name: "IX_CoTeacher_OfferingId_InstructorId", schema: "lms", table: "CoTeacher", columns: new[] { "OfferingId", "InstructorId" }, unique: true);
        migrationBuilder.CreateIndex(name: "IX_CoTeacher_InstructorId", schema: "lms", table: "CoTeacher", column: "InstructorId");
        migrationBuilder.CreateIndex(name: "IX_CourseTemplate_OwnerId_CourseId_Title", schema: "lms", table: "CourseTemplate", columns: new[] { "OwnerId", "CourseId", "Title" }, unique: true);
        migrationBuilder.CreateIndex(name: "IX_CourseTemplate_CourseId", schema: "lms", table: "CourseTemplate", column: "CourseId");
        migrationBuilder.CreateIndex(name: "IX_TemplateVersion_TemplateId_Number", schema: "lms", table: "TemplateVersion", columns: new[] { "TemplateId", "Number" }, unique: true);
        migrationBuilder.CreateIndex(name: "IX_Chapter_OfferingId_Position", schema: "lms", table: "Chapter", columns: new[] { "OfferingId", "Position" }, unique: true);
        migrationBuilder.CreateIndex(name: "IX_LessonSession_OfferingId_OpensUtc", schema: "lms", table: "LessonSession", columns: new[] { "OfferingId", "OpensUtc" });
        migrationBuilder.CreateIndex(name: "IX_Lesson_ChapterId_Position", schema: "lms", table: "Lesson", columns: new[] { "ChapterId", "Position" }, unique: true);
        migrationBuilder.CreateIndex(name: "IX_Lesson_SessionId", schema: "lms", table: "Lesson", column: "SessionId");
        migrationBuilder.CreateIndex(name: "IX_LessonFile_LessonId_MediaId", schema: "lms", table: "LessonFile", columns: new[] { "LessonId", "MediaId" }, unique: true);
        migrationBuilder.CreateIndex(name: "IX_LessonFile_MediaId", schema: "lms", table: "LessonFile", column: "MediaId");
        migrationBuilder.CreateIndex(name: "IX_Assessment_ChapterId", schema: "lms", table: "Assessment", column: "ChapterId");
        migrationBuilder.CreateIndex(name: "IX_Enrollment_OfferingId_StudyRecordId", schema: "lms", table: "Enrollment", columns: new[] { "OfferingId", "StudyRecordId" }, unique: true);
        migrationBuilder.CreateIndex(name: "IX_Enrollment_StudyRecordId_Status", schema: "lms", table: "Enrollment", columns: new[] { "StudyRecordId", "Status" });
        migrationBuilder.CreateIndex(name: "IX_EnrollmentEvent_EnrollmentId_OccurredUtc", schema: "lms", table: "EnrollmentEvent", columns: new[] { "EnrollmentId", "OccurredUtc" });
        migrationBuilder.CreateIndex(name: "IX_EnrollmentEvent_ActorId", schema: "lms", table: "EnrollmentEvent", column: "ActorId");
        migrationBuilder.CreateIndex(name: "IX_LessonProgress_EnrollmentId_LessonId", schema: "lms", table: "LessonProgress", columns: new[] { "EnrollmentId", "LessonId" }, unique: true);
        migrationBuilder.CreateIndex(name: "IX_LessonProgress_LessonId", schema: "lms", table: "LessonProgress", column: "LessonId");
        migrationBuilder.CreateIndex(name: "IX_Submission_AssessmentId_EnrollmentId_Attempt", schema: "lms", table: "Submission", columns: new[] { "AssessmentId", "EnrollmentId", "Attempt" }, unique: true);
        migrationBuilder.CreateIndex(name: "IX_Submission_EnrollmentId", schema: "lms", table: "Submission", column: "EnrollmentId");
        migrationBuilder.CreateIndex(name: "IX_SubmissionFile_SubmissionId_MediaId", schema: "lms", table: "SubmissionFile", columns: new[] { "SubmissionId", "MediaId" }, unique: true);
        migrationBuilder.CreateIndex(name: "IX_SubmissionFile_MediaId", schema: "lms", table: "SubmissionFile", column: "MediaId");
        migrationBuilder.CreateIndex(name: "IX_AssessmentGrade_SubmissionId_Revision", schema: "lms", table: "AssessmentGrade", columns: new[] { "SubmissionId", "Revision" }, unique: true);
        migrationBuilder.CreateIndex(name: "IX_AssessmentGrade_GraderId", schema: "lms", table: "AssessmentGrade", column: "GraderId");
        migrationBuilder.CreateIndex(name: "IX_FinalGrade_EnrollmentId_Revision", schema: "lms", table: "FinalGrade", columns: new[] { "EnrollmentId", "Revision" }, unique: true);
        migrationBuilder.CreateIndex(name: "IX_FinalGrade_GraderId", schema: "lms", table: "FinalGrade", column: "GraderId");
        migrationBuilder.CreateIndex(name: "IX_CohortMessage_CohortId_SentUtc", schema: "lms", table: "CohortMessage", columns: new[] { "CohortId", "SentUtc" });
        migrationBuilder.CreateIndex(name: "IX_CohortMessage_SenderId", schema: "lms", table: "CohortMessage", column: "SenderId");
        migrationBuilder.CreateIndex(name: "IX_CohortMessage_RecipientId", schema: "lms", table: "CohortMessage", column: "RecipientId");
        migrationBuilder.CreateIndex(name: "IX_Reminder_UserId_Kind_EventId_DueUtc", schema: "lms", table: "Reminder", columns: new[] { "UserId", "Kind", "EventId", "DueUtc" }, unique: true);
        migrationBuilder.CreateIndex(name: "IX_Reminder_UserId_CreatedUtc", schema: "lms", table: "Reminder", columns: new[] { "UserId", "CreatedUtc" });
        migrationBuilder.CreateIndex(name: "IX_Reminder_SmsState_CreatedUtc", schema: "lms", table: "Reminder", columns: new[] { "SmsState", "CreatedUtc" });
        migrationBuilder.CreateIndex(name: "IX_Reminder_OfferingId", schema: "lms", table: "Reminder", column: "OfferingId");
        migrationBuilder.CreateIndex(name: "IX_Media_OwnerId_Purpose_TargetId", schema: "lms", table: "Media", columns: new[] { "OwnerId", "Purpose", "TargetId" });
        migrationBuilder.CreateIndex(name: "IX_UploadAttempt_CreatedUtc", schema: "lms", table: "UploadAttempt", column: "CreatedUtc");
        migrationBuilder.CreateIndex(name: "IX_UploadAttempt_State_UpdatedUtc", schema: "lms", table: "UploadAttempt", columns: new[] { "State", "UpdatedUtc" });
        migrationBuilder.CreateIndex(name: "IX_UploadAttempt_ActorId", schema: "lms", table: "UploadAttempt", column: "ActorId");
        migrationBuilder.CreateIndex(name: "IX_OperationalEvent_OccurredUtc", schema: "lms", table: "OperationalEvent", column: "OccurredUtc");
        migrationBuilder.CreateIndex(name: "IX_OperationalEvent_ActorId_OccurredUtc", schema: "lms", table: "OperationalEvent", columns: new[] { "ActorId", "OccurredUtc" });
        migrationBuilder.AddForeignKey(name: "FK_Major_Faculty_FacultyId", schema: "lms", table: "Major", column: "FacultyId", principalSchema: "lms", principalTable: "Faculty", principalColumn: "Id", onDelete: ReferentialAction.NoAction);
        migrationBuilder.AddForeignKey(name: "FK_AcademicProgram_Major_MajorId", schema: "lms", table: "AcademicProgram", column: "MajorId", principalSchema: "lms", principalTable: "Major", principalColumn: "Id", onDelete: ReferentialAction.NoAction);
        migrationBuilder.AddForeignKey(name: "FK_AcademicProgram_Degree_DegreeId", schema: "lms", table: "AcademicProgram", column: "DegreeId", principalSchema: "lms", principalTable: "Degree", principalColumn: "Id", onDelete: ReferentialAction.NoAction);
        migrationBuilder.AddForeignKey(name: "FK_Cohort_AcademicProgram_ProgramId", schema: "lms", table: "Cohort", column: "ProgramId", principalSchema: "lms", principalTable: "AcademicProgram", principalColumn: "Id", onDelete: ReferentialAction.NoAction);
        migrationBuilder.AddForeignKey(name: "FK_InstructorProfile_AspNetUsers_Id", schema: "lms", table: "InstructorProfile", column: "Id", principalTable: "AspNetUsers", principalColumn: "Id", onDelete: ReferentialAction.NoAction);
        migrationBuilder.AddForeignKey(name: "FK_InstructorProfile_Major_MajorId", schema: "lms", table: "InstructorProfile", column: "MajorId", principalSchema: "lms", principalTable: "Major", principalColumn: "Id", onDelete: ReferentialAction.NoAction);
        migrationBuilder.AddForeignKey(name: "FK_InstructorProfile_Media_AvatarId", schema: "lms", table: "InstructorProfile", column: "AvatarId", principalSchema: "lms", principalTable: "Media", principalColumn: "Id", onDelete: ReferentialAction.NoAction);
        migrationBuilder.AddForeignKey(name: "FK_StudentProfile_AspNetUsers_Id", schema: "lms", table: "StudentProfile", column: "Id", principalTable: "AspNetUsers", principalColumn: "Id", onDelete: ReferentialAction.NoAction);
        migrationBuilder.AddForeignKey(name: "FK_StudentProfile_Media_AvatarId", schema: "lms", table: "StudentProfile", column: "AvatarId", principalSchema: "lms", principalTable: "Media", principalColumn: "Id", onDelete: ReferentialAction.NoAction);
        migrationBuilder.AddForeignKey(name: "FK_StudyRecord_StudentProfile_StudentId", schema: "lms", table: "StudyRecord", column: "StudentId", principalSchema: "lms", principalTable: "StudentProfile", principalColumn: "Id", onDelete: ReferentialAction.NoAction);
        migrationBuilder.AddForeignKey(name: "FK_StudyRecord_Cohort_CohortId", schema: "lms", table: "StudyRecord", column: "CohortId", principalSchema: "lms", principalTable: "Cohort", principalColumn: "Id", onDelete: ReferentialAction.NoAction);
        migrationBuilder.AddForeignKey(name: "FK_AcademicGrant_AspNetUsers_UserId", schema: "lms", table: "AcademicGrant", column: "UserId", principalTable: "AspNetUsers", principalColumn: "Id", onDelete: ReferentialAction.NoAction);
        migrationBuilder.AddForeignKey(name: "FK_AcademicGrant_Faculty_FacultyId", schema: "lms", table: "AcademicGrant", column: "FacultyId", principalSchema: "lms", principalTable: "Faculty", principalColumn: "Id", onDelete: ReferentialAction.NoAction);
        migrationBuilder.AddForeignKey(name: "FK_AcademicGrant_Major_MajorId", schema: "lms", table: "AcademicGrant", column: "MajorId", principalSchema: "lms", principalTable: "Major", principalColumn: "Id", onDelete: ReferentialAction.NoAction);
        migrationBuilder.AddForeignKey(name: "FK_CatalogCourse_Faculty_FacultyId", schema: "lms", table: "CatalogCourse", column: "FacultyId", principalSchema: "lms", principalTable: "Faculty", principalColumn: "Id", onDelete: ReferentialAction.NoAction);
        migrationBuilder.AddForeignKey(name: "FK_Offering_CatalogCourse_CourseId", schema: "lms", table: "Offering", column: "CourseId", principalSchema: "lms", principalTable: "CatalogCourse", principalColumn: "Id", onDelete: ReferentialAction.NoAction);
        migrationBuilder.AddForeignKey(name: "FK_Offering_Term_TermId", schema: "lms", table: "Offering", column: "TermId", principalSchema: "lms", principalTable: "Term", principalColumn: "Id", onDelete: ReferentialAction.NoAction);
        migrationBuilder.AddForeignKey(name: "FK_Offering_Major_OwnerMajorId", schema: "lms", table: "Offering", column: "OwnerMajorId", principalSchema: "lms", principalTable: "Major", principalColumn: "Id", onDelete: ReferentialAction.NoAction);
        migrationBuilder.AddForeignKey(name: "FK_Offering_InstructorProfile_LeadId", schema: "lms", table: "Offering", column: "LeadId", principalSchema: "lms", principalTable: "InstructorProfile", principalColumn: "Id", onDelete: ReferentialAction.NoAction);
        migrationBuilder.AddForeignKey(name: "FK_OfferingCohort_Offering_OfferingId", schema: "lms", table: "OfferingCohort", column: "OfferingId", principalSchema: "lms", principalTable: "Offering", principalColumn: "Id", onDelete: ReferentialAction.NoAction);
        migrationBuilder.AddForeignKey(name: "FK_OfferingCohort_Cohort_CohortId", schema: "lms", table: "OfferingCohort", column: "CohortId", principalSchema: "lms", principalTable: "Cohort", principalColumn: "Id", onDelete: ReferentialAction.NoAction);
        migrationBuilder.AddForeignKey(name: "FK_CoTeacher_Offering_OfferingId", schema: "lms", table: "CoTeacher", column: "OfferingId", principalSchema: "lms", principalTable: "Offering", principalColumn: "Id", onDelete: ReferentialAction.NoAction);
        migrationBuilder.AddForeignKey(name: "FK_CoTeacher_InstructorProfile_InstructorId", schema: "lms", table: "CoTeacher", column: "InstructorId", principalSchema: "lms", principalTable: "InstructorProfile", principalColumn: "Id", onDelete: ReferentialAction.NoAction);
        migrationBuilder.AddForeignKey(name: "FK_CourseTemplate_InstructorProfile_OwnerId", schema: "lms", table: "CourseTemplate", column: "OwnerId", principalSchema: "lms", principalTable: "InstructorProfile", principalColumn: "Id", onDelete: ReferentialAction.NoAction);
        migrationBuilder.AddForeignKey(name: "FK_CourseTemplate_CatalogCourse_CourseId", schema: "lms", table: "CourseTemplate", column: "CourseId", principalSchema: "lms", principalTable: "CatalogCourse", principalColumn: "Id", onDelete: ReferentialAction.NoAction);
        migrationBuilder.AddForeignKey(name: "FK_TemplateVersion_CourseTemplate_TemplateId", schema: "lms", table: "TemplateVersion", column: "TemplateId", principalSchema: "lms", principalTable: "CourseTemplate", principalColumn: "Id", onDelete: ReferentialAction.NoAction);
        migrationBuilder.AddForeignKey(name: "FK_Chapter_Offering_OfferingId", schema: "lms", table: "Chapter", column: "OfferingId", principalSchema: "lms", principalTable: "Offering", principalColumn: "Id", onDelete: ReferentialAction.NoAction);
        migrationBuilder.AddForeignKey(name: "FK_LessonSession_Offering_OfferingId", schema: "lms", table: "LessonSession", column: "OfferingId", principalSchema: "lms", principalTable: "Offering", principalColumn: "Id", onDelete: ReferentialAction.NoAction);
        migrationBuilder.AddForeignKey(name: "FK_Lesson_Chapter_ChapterId", schema: "lms", table: "Lesson", column: "ChapterId", principalSchema: "lms", principalTable: "Chapter", principalColumn: "Id", onDelete: ReferentialAction.NoAction);
        migrationBuilder.AddForeignKey(name: "FK_Lesson_LessonSession_SessionId", schema: "lms", table: "Lesson", column: "SessionId", principalSchema: "lms", principalTable: "LessonSession", principalColumn: "Id", onDelete: ReferentialAction.NoAction);
        migrationBuilder.AddForeignKey(name: "FK_LessonFile_Lesson_LessonId", schema: "lms", table: "LessonFile", column: "LessonId", principalSchema: "lms", principalTable: "Lesson", principalColumn: "Id", onDelete: ReferentialAction.NoAction);
        migrationBuilder.AddForeignKey(name: "FK_LessonFile_Media_MediaId", schema: "lms", table: "LessonFile", column: "MediaId", principalSchema: "lms", principalTable: "Media", principalColumn: "Id", onDelete: ReferentialAction.NoAction);
        migrationBuilder.AddForeignKey(name: "FK_Assessment_Chapter_ChapterId", schema: "lms", table: "Assessment", column: "ChapterId", principalSchema: "lms", principalTable: "Chapter", principalColumn: "Id", onDelete: ReferentialAction.NoAction);
        migrationBuilder.AddForeignKey(name: "FK_Enrollment_Offering_OfferingId", schema: "lms", table: "Enrollment", column: "OfferingId", principalSchema: "lms", principalTable: "Offering", principalColumn: "Id", onDelete: ReferentialAction.NoAction);
        migrationBuilder.AddForeignKey(name: "FK_Enrollment_StudyRecord_StudyRecordId", schema: "lms", table: "Enrollment", column: "StudyRecordId", principalSchema: "lms", principalTable: "StudyRecord", principalColumn: "Id", onDelete: ReferentialAction.NoAction);
        migrationBuilder.AddForeignKey(name: "FK_EnrollmentEvent_Enrollment_EnrollmentId", schema: "lms", table: "EnrollmentEvent", column: "EnrollmentId", principalSchema: "lms", principalTable: "Enrollment", principalColumn: "Id", onDelete: ReferentialAction.NoAction);
        migrationBuilder.AddForeignKey(name: "FK_EnrollmentEvent_AspNetUsers_ActorId", schema: "lms", table: "EnrollmentEvent", column: "ActorId", principalTable: "AspNetUsers", principalColumn: "Id", onDelete: ReferentialAction.NoAction);
        migrationBuilder.AddForeignKey(name: "FK_LessonProgress_Enrollment_EnrollmentId", schema: "lms", table: "LessonProgress", column: "EnrollmentId", principalSchema: "lms", principalTable: "Enrollment", principalColumn: "Id", onDelete: ReferentialAction.NoAction);
        migrationBuilder.AddForeignKey(name: "FK_LessonProgress_Lesson_LessonId", schema: "lms", table: "LessonProgress", column: "LessonId", principalSchema: "lms", principalTable: "Lesson", principalColumn: "Id", onDelete: ReferentialAction.NoAction);
        migrationBuilder.AddForeignKey(name: "FK_Submission_Assessment_AssessmentId", schema: "lms", table: "Submission", column: "AssessmentId", principalSchema: "lms", principalTable: "Assessment", principalColumn: "Id", onDelete: ReferentialAction.NoAction);
        migrationBuilder.AddForeignKey(name: "FK_Submission_Enrollment_EnrollmentId", schema: "lms", table: "Submission", column: "EnrollmentId", principalSchema: "lms", principalTable: "Enrollment", principalColumn: "Id", onDelete: ReferentialAction.NoAction);
        migrationBuilder.AddForeignKey(name: "FK_SubmissionFile_Submission_SubmissionId", schema: "lms", table: "SubmissionFile", column: "SubmissionId", principalSchema: "lms", principalTable: "Submission", principalColumn: "Id", onDelete: ReferentialAction.NoAction);
        migrationBuilder.AddForeignKey(name: "FK_SubmissionFile_Media_MediaId", schema: "lms", table: "SubmissionFile", column: "MediaId", principalSchema: "lms", principalTable: "Media", principalColumn: "Id", onDelete: ReferentialAction.NoAction);
        migrationBuilder.AddForeignKey(name: "FK_AssessmentGrade_Submission_SubmissionId", schema: "lms", table: "AssessmentGrade", column: "SubmissionId", principalSchema: "lms", principalTable: "Submission", principalColumn: "Id", onDelete: ReferentialAction.NoAction);
        migrationBuilder.AddForeignKey(name: "FK_AssessmentGrade_AspNetUsers_GraderId", schema: "lms", table: "AssessmentGrade", column: "GraderId", principalTable: "AspNetUsers", principalColumn: "Id", onDelete: ReferentialAction.NoAction);
        migrationBuilder.AddForeignKey(name: "FK_FinalGrade_Enrollment_EnrollmentId", schema: "lms", table: "FinalGrade", column: "EnrollmentId", principalSchema: "lms", principalTable: "Enrollment", principalColumn: "Id", onDelete: ReferentialAction.NoAction);
        migrationBuilder.AddForeignKey(name: "FK_FinalGrade_AspNetUsers_GraderId", schema: "lms", table: "FinalGrade", column: "GraderId", principalTable: "AspNetUsers", principalColumn: "Id", onDelete: ReferentialAction.NoAction);
        migrationBuilder.AddForeignKey(name: "FK_CohortMessage_Cohort_CohortId", schema: "lms", table: "CohortMessage", column: "CohortId", principalSchema: "lms", principalTable: "Cohort", principalColumn: "Id", onDelete: ReferentialAction.NoAction);
        migrationBuilder.AddForeignKey(name: "FK_CohortMessage_StudentProfile_SenderId", schema: "lms", table: "CohortMessage", column: "SenderId", principalSchema: "lms", principalTable: "StudentProfile", principalColumn: "Id", onDelete: ReferentialAction.NoAction);
        migrationBuilder.AddForeignKey(name: "FK_CohortMessage_StudentProfile_RecipientId", schema: "lms", table: "CohortMessage", column: "RecipientId", principalSchema: "lms", principalTable: "StudentProfile", principalColumn: "Id", onDelete: ReferentialAction.NoAction);
        migrationBuilder.AddForeignKey(name: "FK_Reminder_AspNetUsers_UserId", schema: "lms", table: "Reminder", column: "UserId", principalTable: "AspNetUsers", principalColumn: "Id", onDelete: ReferentialAction.NoAction);
        migrationBuilder.AddForeignKey(name: "FK_Reminder_Offering_OfferingId", schema: "lms", table: "Reminder", column: "OfferingId", principalSchema: "lms", principalTable: "Offering", principalColumn: "Id", onDelete: ReferentialAction.NoAction);
        migrationBuilder.AddForeignKey(name: "FK_Media_AspNetUsers_OwnerId", schema: "lms", table: "Media", column: "OwnerId", principalTable: "AspNetUsers", principalColumn: "Id", onDelete: ReferentialAction.NoAction);
        migrationBuilder.AddForeignKey(name: "FK_UploadAttempt_AspNetUsers_ActorId", schema: "lms", table: "UploadAttempt", column: "ActorId", principalTable: "AspNetUsers", principalColumn: "Id", onDelete: ReferentialAction.NoAction);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Intentional: a normal rollback must never erase student work or audit evidence.
        throw new System.NotSupportedException("Restore a verified pre-upgrade backup instead of dropping LMS educational history.");
    }
}
