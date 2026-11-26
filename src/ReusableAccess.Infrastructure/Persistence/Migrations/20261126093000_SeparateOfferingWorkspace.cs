using Microsoft.EntityFrameworkCore.Migrations;

namespace ReusableAccess.Infrastructure.Persistence.Migrations;

public partial class SeparateOfferingWorkspace : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        const string lms = "lms";
        migrationBuilder.AddColumn<bool>("NeedsAvailabilityReview", "Lesson", type: "bit", schema: lms, nullable: false, defaultValue: false);
        migrationBuilder.AddColumn<DateTimeOffset>("LegacyVisibleFromUtc", "Lesson", type: "datetimeoffset", schema: lms, nullable: true);
        migrationBuilder.AddColumn<Guid>("OfferingId", "Assessment", type: "uniqueidentifier", schema: lms, nullable: true);
        migrationBuilder.AddColumn<DateTimeOffset>("PublishedUtc", "Assessment", type: "datetimeoffset", schema: lms, nullable: true);
        migrationBuilder.AddColumn<DateTimeOffset>("VisibleUntilUtc", "Assessment", type: "datetimeoffset", schema: lms, nullable: true);
        migrationBuilder.AddColumn<DateTimeOffset>("LegacyOpensUtc", "Assessment", type: "datetimeoffset", schema: lms, nullable: true);
        migrationBuilder.AddColumn<decimal>("ExtensionPenalty", "Assessment", type: "decimal(9,2)", schema: lms, nullable: true);
        migrationBuilder.AddColumn<Guid>("TemplateVersionId", "Assessment", type: "uniqueidentifier", schema: lms, nullable: true);
        migrationBuilder.AddColumn<Guid>("Revision", "Assessment", type: "uniqueidentifier", schema: lms, nullable: false, defaultValueSql: "NEWID()");
        migrationBuilder.AddColumn<decimal>("ScoreLimit", "Submission", type: "decimal(9,2)", schema: lms, nullable: true);
        migrationBuilder.AddColumn<Guid>("RequestId", "Submission", type: "uniqueidentifier", schema: lms, nullable: true);
        migrationBuilder.AddColumn<string>("RequestHash", "Submission", type: "varchar(64)", maxLength: 64, schema: lms, nullable: true);

        // Preserve original IDs and timestamps. Dynamic SQL also works in an idempotent EF script where the new columns
        // may not yet exist at compilation time. Do not leave existing submissions attached to another offering silently.
        migrationBuilder.Sql("""
            EXEC(N'
            UPDATE l
               SET LegacyVisibleFromUtc = l.VisibleFromUtc,
                   VisibleFromUtc = CASE WHEN s.Id IS NOT NULL AND (l.VisibleFromUtc IS NULL OR s.OpensUtc > l.VisibleFromUtc)
                                         THEN s.OpensUtc ELSE l.VisibleFromUtc END,
                   NeedsAvailabilityReview = CASE WHEN l.Active = 1 AND c.PublishedUtc IS NOT NULL AND s.Id IS NULL THEN 1 ELSE 0 END
              FROM lms.Lesson l
              JOIN lms.Chapter c ON c.Id = l.ChapterId
              LEFT JOIN lms.LessonSession s ON s.Id = l.SessionId AND s.OfferingId = c.OfferingId AND s.Active = 1;
            UPDATE a
               SET OfferingId = c.OfferingId,
                   PublishedUtc = CASE WHEN c.Active = 1 THEN c.PublishedUtc ELSE NULL END,
                   LegacyOpensUtc = a.OpensUtc,
                   OpensUtc = CASE WHEN c.VisibleFromUtc IS NOT NULL AND (a.OpensUtc IS NULL OR c.VisibleFromUtc > a.OpensUtc)
                                   THEN c.VisibleFromUtc ELSE a.OpensUtc END,
                   VisibleUntilUtc = c.VisibleUntilUtc
              FROM lms.Assessment a JOIN lms.Chapter c ON c.Id = a.ChapterId;
            IF EXISTS (SELECT 1 FROM lms.Assessment WHERE OfferingId IS NULL)
                THROW 51001, N''An assessment has no valid source chapter.'', 1;
            IF EXISTS (SELECT 1 FROM lms.Submission s JOIN lms.Assessment a ON a.Id = s.AssessmentId
                       JOIN lms.Enrollment e ON e.Id = s.EnrollmentId WHERE e.OfferingId <> a.OfferingId)
                THROW 51002, N''A submission and its assessment belong to different offerings. Review a backup before upgrading.'', 1;');
            """);

        migrationBuilder.DropForeignKey("FK_Lesson_LessonSession_SessionId", "Lesson", schema: lms);
        migrationBuilder.DropForeignKey("FK_Assessment_Chapter_ChapterId", "Assessment", schema: lms);
        migrationBuilder.DropForeignKey("FK_Assessment_Lesson_ChapterId_LessonId", "Assessment", schema: lms);
        migrationBuilder.DropIndex("IX_Lesson_SessionId", "Lesson", schema: lms);
        migrationBuilder.DropIndex("IX_Assessment_ChapterId", "Assessment", schema: lms);
        migrationBuilder.DropIndex("IX_Assessment_ChapterId_LessonId", "Assessment", schema: lms);
        migrationBuilder.DropIndex("IX_Assessment_ChapterId_TemplateItemKey", "Assessment", schema: lms);
        migrationBuilder.AlterColumn<Guid>("ChapterId", "Assessment", type: "uniqueidentifier", schema: lms,
            nullable: true, oldClrType: typeof(Guid), oldType: "uniqueidentifier");
        migrationBuilder.AlterColumn<Guid>("OfferingId", "Assessment", type: "uniqueidentifier", schema: lms,
            nullable: false, oldClrType: typeof(Guid), oldType: "uniqueidentifier", oldNullable: true);

        migrationBuilder.CreateTable(name: "AssignmentTemplateVersion", schema: "lms",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                FamilyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Number = table.Column<int>(type: "int", nullable: false),
                Title = table.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: false),
                Prompt = table.Column<string>(type: "nvarchar(max)", nullable: false),
                Rubric = table.Column<string>(type: "nvarchar(max)", nullable: false),
                MaxAttempts = table.Column<int>(type: "int", nullable: false),
                MaxScore = table.Column<decimal>(type: "decimal(9,2)", nullable: false),
                CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            }, constraints: table => table.PrimaryKey("PK_AssignmentTemplateVersion", x => x.Id));
        migrationBuilder.CreateTable(name: "AssignmentExtension", schema: "lms",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                AssessmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                InstructorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                PreviousDeadlineUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                DeadlineUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                Penalty = table.Column<decimal>(type: "decimal(9,2)", nullable: false),
                RecordedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            }, constraints: table => table.PrimaryKey("PK_AssignmentExtension", x => x.Id));
        migrationBuilder.CreateTable(name: "AssignmentCalendarReceipt", schema: "lms",
            columns: table => new
            {
                SubmissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                InstructorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Title = table.Column<string>(type: "nvarchar(220)", maxLength: 220, nullable: false),
                ReceivedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            }, constraints: table => table.PrimaryKey("PK_AssignmentCalendarReceipt", x => x.SubmissionId));
        migrationBuilder.CreateTable(name: "CourseDiscussionMessage", schema: "lms",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                OfferingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                SenderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ByInstructor = table.Column<bool>(type: "bit", nullable: false),
                Text = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                SentUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            }, constraints: table => table.PrimaryKey("PK_CourseDiscussionMessage", x => x.Id));

        migrationBuilder.CreateIndex("IX_Assessment_OfferingId_Active_DueUtc", "Assessment", schema: lms, columns: new[] { "OfferingId", "Active", "DueUtc" });
        migrationBuilder.CreateIndex("IX_Assessment_TemplateVersionId", "Assessment", schema: lms, column: "TemplateVersionId");
        migrationBuilder.CreateIndex("IX_Submission_AssessmentId_EnrollmentId_RequestId", "Submission", schema: lms,
            columns: new[] { "AssessmentId", "EnrollmentId", "RequestId" }, unique: true, filter: "[RequestId] IS NOT NULL");
        migrationBuilder.CreateIndex("IX_AssignmentTemplateVersion_OwnerId_FamilyId_Number", "AssignmentTemplateVersion", schema: lms,
            columns: new[] { "OwnerId", "FamilyId", "Number" }, unique: true);
        migrationBuilder.CreateIndex("IX_AssignmentTemplateVersion_OwnerId_CreatedUtc_Id", "AssignmentTemplateVersion", schema: lms, columns: new[] { "OwnerId", "CreatedUtc", "Id" });
        migrationBuilder.CreateIndex("IX_AssignmentExtension_AssessmentId_RecordedUtc", "AssignmentExtension", schema: lms, columns: new[] { "AssessmentId", "RecordedUtc" });
        migrationBuilder.CreateIndex("IX_AssignmentExtension_InstructorId", "AssignmentExtension", schema: lms, column: "InstructorId");
        migrationBuilder.CreateIndex("IX_AssignmentCalendarReceipt_InstructorId_ReceivedUtc", "AssignmentCalendarReceipt", schema: lms, columns: new[] { "InstructorId", "ReceivedUtc" });
        migrationBuilder.CreateIndex("IX_CourseDiscussionMessage_OfferingId_SentUtc_Id", "CourseDiscussionMessage", schema: lms, columns: new[] { "OfferingId", "SentUtc", "Id" });
        migrationBuilder.CreateIndex("IX_CourseDiscussionMessage_SenderId_SentUtc", "CourseDiscussionMessage", schema: lms, columns: new[] { "SenderId", "SentUtc" });

        Link("Assessment", "OfferingId", "Offering");
        Link("Assessment", "TemplateVersionId", "AssignmentTemplateVersion");
        Link("AssignmentTemplateVersion", "OwnerId", "InstructorProfile");
        Link("AssignmentExtension", "AssessmentId", "Assessment");
        Link("AssignmentExtension", "InstructorId", "InstructorProfile");
        Link("AssignmentCalendarReceipt", "SubmissionId", "Submission");
        Link("AssignmentCalendarReceipt", "InstructorId", "InstructorProfile");
        Link("CourseDiscussionMessage", "OfferingId", "Offering");
        Link("CourseDiscussionMessage", "SenderId", "AspNetUsers", null);

        void Link(string table, string column, string principal, string? principalSchema = "lms") =>
            migrationBuilder.AddForeignKey($"FK_{table}_{principal}_{column}", table, column, principal,
                schema: lms, principalSchema: principalSchema, principalColumn: "Id", onDelete: ReferentialAction.NoAction);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
        => throw new NotSupportedException("Independent submissions cannot safely become chapter-bound again. Restore a tested backup.");
}
