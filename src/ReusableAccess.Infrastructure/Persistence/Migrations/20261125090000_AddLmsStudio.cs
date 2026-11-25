using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReusableAccess.Infrastructure.Persistence.Migrations;

public partial class AddLmsStudio : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>("Description", schema: "lms", table: "CourseTemplate", type: "nvarchar(180)", maxLength: 180, nullable: true);
        migrationBuilder.AddColumn<DateTimeOffset>("UpdatedUtc", schema: "lms", table: "CourseTemplate", type: "datetimeoffset", nullable: true);
        migrationBuilder.AddColumn<Guid>("Revision", schema: "lms", table: "CourseTemplate", type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()");
        migrationBuilder.AddColumn<int>("ChapterCount", schema: "lms", table: "TemplateVersion", type: "int", nullable: true);
        migrationBuilder.AddColumn<int>("LessonCount", schema: "lms", table: "TemplateVersion", type: "int", nullable: true);
        migrationBuilder.AddColumn<DateTimeOffset>("VisibleFromUtc", schema: "lms", table: "Chapter", type: "datetimeoffset", nullable: true);
        migrationBuilder.AddColumn<DateTimeOffset>("VisibleUntilUtc", schema: "lms", table: "Chapter", type: "datetimeoffset", nullable: true);
        migrationBuilder.AddColumn<Guid>("TemplateItemKey", schema: "lms", table: "Chapter", type: "uniqueidentifier", nullable: true);
        migrationBuilder.AddColumn<DateTimeOffset>("VisibleFromUtc", schema: "lms", table: "Lesson", type: "datetimeoffset", nullable: true);
        migrationBuilder.AddColumn<DateTimeOffset>("VisibleUntilUtc", schema: "lms", table: "Lesson", type: "datetimeoffset", nullable: true);
        migrationBuilder.AddColumn<Guid>("TemplateItemKey", schema: "lms", table: "Lesson", type: "uniqueidentifier", nullable: true);
        migrationBuilder.AddColumn<string>("Summary", schema: "lms", table: "Lesson", type: "nvarchar(250)", maxLength: 250, nullable: true);
        migrationBuilder.AddColumn<Guid>("CoverMediaId", schema: "lms", table: "Lesson", type: "uniqueidentifier", nullable: true);
        migrationBuilder.AddColumn<int>("Position", schema: "lms", table: "LessonFile", type: "int", nullable: true);
        migrationBuilder.AddColumn<Guid>("LessonId", schema: "lms", table: "Assessment", type: "uniqueidentifier", nullable: true);
        migrationBuilder.AddColumn<DateTimeOffset>("OpensUtc", schema: "lms", table: "Assessment", type: "datetimeoffset", nullable: true);
        migrationBuilder.AddColumn<Guid>("TemplateItemKey", schema: "lms", table: "Assessment", type: "uniqueidentifier", nullable: true);
        migrationBuilder.AddUniqueConstraint("AK_Lesson_ChapterId_Id", "Lesson", new[] { "ChapterId", "Id" }, schema: "lms");

        migrationBuilder.CreateTable(
            name: "OfferingScheduleDraft", schema: "lms",
            columns: table => new
            {
                OfferingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TemplateVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                OriginOfferingId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                ClassesStartDate = table.Column<DateOnly>(type: "date", nullable: false),
                Mode = table.Column<int>(type: "int", nullable: false),
                State = table.Column<int>(type: "int", nullable: false),
                OfferingRevision = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                AppliedOfferingRevision = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                Revision = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                AppliedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                PublishedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                TermVersion = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                OriginVersion = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true),
                Payload = table.Column<string>(type: "nvarchar(max)", nullable: false),
            },
            constraints: table => table.PrimaryKey("PK_OfferingScheduleDraft", x => x.OfferingId));
        migrationBuilder.CreateTable(
            name: "TemplateVersionMedia", schema: "lms",
            columns: table => new
            {
                TemplateVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                MediaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
            },
            constraints: table => table.PrimaryKey("PK_TemplateVersionMedia", x => new { x.TemplateVersionId, x.MediaId }));
        migrationBuilder.CreateIndex("IX_CourseTemplate_OwnerId_Active_UpdatedUtc_Id", "CourseTemplate", schema: "lms", columns: new[] { "OwnerId", "Active", "UpdatedUtc", "Id" });
        migrationBuilder.CreateIndex("IX_Chapter_OfferingId_TemplateItemKey", "Chapter", schema: "lms", columns: new[] { "OfferingId", "TemplateItemKey" }, unique: true, filter: "[TemplateItemKey] IS NOT NULL");
        migrationBuilder.CreateIndex("IX_Lesson_ChapterId_TemplateItemKey", "Lesson", schema: "lms", columns: new[] { "ChapterId", "TemplateItemKey" }, unique: true, filter: "[TemplateItemKey] IS NOT NULL");
        migrationBuilder.CreateIndex("IX_Lesson_CoverMediaId", "Lesson", schema: "lms", column: "CoverMediaId");
        migrationBuilder.CreateIndex("IX_Assessment_ChapterId_TemplateItemKey", "Assessment", schema: "lms", columns: new[] { "ChapterId", "TemplateItemKey" }, unique: true, filter: "[TemplateItemKey] IS NOT NULL");
        migrationBuilder.CreateIndex("IX_Assessment_ChapterId_LessonId", "Assessment", schema: "lms", columns: new[] { "ChapterId", "LessonId" });
        migrationBuilder.CreateIndex("IX_OfferingScheduleDraft_TemplateVersionId_State", "OfferingScheduleDraft", schema: "lms", columns: new[] { "TemplateVersionId", "State" });
        migrationBuilder.CreateIndex("IX_OfferingScheduleDraft_OriginOfferingId", "OfferingScheduleDraft", schema: "lms", column: "OriginOfferingId");
        migrationBuilder.CreateIndex("IX_OfferingScheduleDraft_UpdatedBy", "OfferingScheduleDraft", schema: "lms", column: "UpdatedBy");
        migrationBuilder.CreateIndex("IX_TemplateVersionMedia_MediaId", "TemplateVersionMedia", schema: "lms", column: "MediaId");
        migrationBuilder.AddForeignKey("FK_Lesson_Media_CoverMediaId", "Lesson",
            column: "CoverMediaId", principalTable: "Media", schema: "lms", principalSchema: "lms",
            principalColumn: "Id", onDelete: ReferentialAction.NoAction);
        migrationBuilder.AddForeignKey("FK_Assessment_Lesson_ChapterId_LessonId", "Assessment",
            columns: new[] { "ChapterId", "LessonId" }, principalTable: "Lesson", schema: "lms", principalSchema: "lms",
            principalColumns: new[] { "ChapterId", "Id" }, onDelete: ReferentialAction.NoAction);
        migrationBuilder.AddForeignKey("FK_OfferingScheduleDraft_Offering_OfferingId", "OfferingScheduleDraft",
            column: "OfferingId", principalTable: "Offering", schema: "lms", principalSchema: "lms",
            principalColumn: "Id", onDelete: ReferentialAction.NoAction);
        migrationBuilder.AddForeignKey("FK_OfferingScheduleDraft_TemplateVersion_TemplateVersionId", "OfferingScheduleDraft",
            column: "TemplateVersionId", principalTable: "TemplateVersion", schema: "lms", principalSchema: "lms",
            principalColumn: "Id", onDelete: ReferentialAction.NoAction);
        migrationBuilder.AddForeignKey("FK_OfferingScheduleDraft_Offering_OriginOfferingId", "OfferingScheduleDraft",
            column: "OriginOfferingId", principalTable: "Offering", schema: "lms", principalSchema: "lms",
            principalColumn: "Id", onDelete: ReferentialAction.NoAction);
        migrationBuilder.AddForeignKey("FK_OfferingScheduleDraft_InstructorProfile_UpdatedBy", "OfferingScheduleDraft",
            column: "UpdatedBy", principalTable: "InstructorProfile", schema: "lms", principalSchema: "lms",
            principalColumn: "Id", onDelete: ReferentialAction.NoAction);
        migrationBuilder.AddForeignKey("FK_TemplateVersionMedia_TemplateVersion_TemplateVersionId", "TemplateVersionMedia",
            column: "TemplateVersionId", principalTable: "TemplateVersion", schema: "lms", principalSchema: "lms",
            principalColumn: "Id", onDelete: ReferentialAction.NoAction);
        migrationBuilder.AddForeignKey("FK_TemplateVersionMedia_Media_MediaId", "TemplateVersionMedia",
            column: "MediaId", principalTable: "Media", schema: "lms", principalSchema: "lms",
            principalColumn: "Id", onDelete: ReferentialAction.NoAction);

        // Backfill only GUIDs present as educational media in a valid V1 document. The document remains unchanged.
        migrationBuilder.Sql("""
            INSERT INTO [lms].[TemplateVersionMedia] ([TemplateVersionId], [MediaId])
            SELECT DISTINCT v.[Id], f.[Id]
              FROM [lms].[TemplateVersion] AS v
             CROSS APPLY OPENJSON(CASE WHEN ISJSON(v.[Snapshot]) = 1 THEN v.[Snapshot] ELSE N'{}' END, '$.Chapters')
                         WITH ([Lessons] nvarchar(max) '$.Lessons' AS JSON) AS chapter
             CROSS APPLY OPENJSON(chapter.[Lessons]) WITH ([Files] nvarchar(max) '$.MediaIds' AS JSON) AS lesson
             CROSS APPLY OPENJSON(lesson.[Files]) AS item
              JOIN [lms].[Media] AS f ON f.[Id] = TRY_CONVERT(uniqueidentifier, item.[value]) AND f.[Purpose] = 3
             WHERE NOT EXISTS (SELECT 1 FROM [lms].[TemplateVersionMedia] AS link
                 WHERE link.[TemplateVersionId] = v.[Id] AND link.[MediaId] = f.[Id]);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
        => throw new NotSupportedException("Downgrading drops template versions and schedules; restore a tested backup instead.");
}
