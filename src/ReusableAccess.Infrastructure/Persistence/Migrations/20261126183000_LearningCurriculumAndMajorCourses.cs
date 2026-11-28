using Microsoft.EntityFrameworkCore.Migrations;

namespace ReusableAccess.Infrastructure.Persistence.Migrations;

public partial class LearningCurriculumAndMajorCourses : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        const string schema = "lms";
        m.AddColumn<Guid>("MajorId", "CatalogCourse", schema: schema, type: "uniqueidentifier", nullable: true);
        m.AddUniqueConstraint("AK_Major_FacultyId_Id", "Major", new[] { "FacultyId", "Id" }, schema: schema);
        // Do not assign a major by faculty alone. Every existing offering must agree, or administrators review the row.
        m.Sql("""
            EXEC(N'UPDATE c SET MajorId = x.OwnerMajorId
                FROM lms.CatalogCourse c
                CROSS APPLY (SELECT TOP (1) o.OwnerMajorId FROM lms.Offering o WHERE o.CourseId = c.Id AND o.Audience = 3 AND o.OwnerMajorId IS NOT NULL ORDER BY o.Id) x
                JOIN lms.Major j ON j.Id = x.OwnerMajorId AND j.FacultyId = c.FacultyId
                WHERE NOT EXISTS (SELECT 1 FROM lms.Offering o WHERE o.CourseId = c.Id AND (o.Audience <> 3 OR o.OwnerMajorId IS NULL OR o.OwnerMajorId <> x.OwnerMajorId));');
            """);
        m.DropIndex("IX_CatalogCourse_FacultyId_Code", "CatalogCourse", schema: schema);
        m.CreateIndex("IX_CatalogCourse_FacultyId_Code", "CatalogCourse", new[] { "FacultyId", "Code" }, schema: schema, unique: true, filter: "[MajorId] IS NULL");
        m.CreateIndex("IX_CatalogCourse_MajorId_Code", "CatalogCourse", new[] { "MajorId", "Code" }, schema: schema, unique: true, filter: "[MajorId] IS NOT NULL");
        m.CreateIndex("IX_CatalogCourse_FacultyId_MajorId", "CatalogCourse", new[] { "FacultyId", "MajorId" }, schema: schema);
        m.AddForeignKey("FK_CatalogCourse_Major_FacultyId_MajorId", "CatalogCourse", new[] { "FacultyId", "MajorId" }, "Major",
            principalColumns: new[] { "FacultyId", "Id" }, schema: schema, principalSchema: schema, onDelete: ReferentialAction.NoAction);

        m.AddColumn<string>("DraftIntroduction", "Offering", schema: schema, type: "nvarchar(320)", maxLength: 320, nullable: true);
        m.AddColumn<string>("PublishedIntroduction", "Offering", schema: schema, type: "nvarchar(320)", maxLength: 320, nullable: true);
        m.AddColumn<int>("CurriculumVersion", "Offering", schema: schema, type: "int", defaultValue: 0);
        m.AddColumn<Guid>("PublishedCurriculumToken", "Offering", schema: schema, type: "uniqueidentifier", nullable: true);
        m.AddColumn<Guid>("PublishedCurriculumScope", "Offering", schema: schema, type: "uniqueidentifier", nullable: true);
        m.AddColumn<string>("Summary", "Chapter", schema: schema, type: "nvarchar(320)", maxLength: 320, nullable: true);
        m.AddColumn<bool>("WorkspaceManaged", "Chapter", schema: schema, type: "bit", defaultValue: false);
        m.AddColumn<bool>("Retired", "Chapter", schema: schema, type: "bit", defaultValue: false);
        m.AddColumn<Guid>("PendingBaseId", "Chapter", schema: schema, type: "uniqueidentifier", nullable: true);
        m.AddColumn<Guid>("StableChapterId", "Chapter", schema: schema, type: "uniqueidentifier", nullable: true);
        m.AlterColumn<string>("Summary", "Lesson", schema: schema, type: "nvarchar(320)", maxLength: 320, nullable: true,
            oldClrType: typeof(string), oldType: "nvarchar(250)", oldMaxLength: 250, oldNullable: true);
        m.AddColumn<Guid>("StableLessonId", "Lesson", schema: schema, type: "uniqueidentifier", nullable: true);
        m.AddColumn<Guid>("FeaturedMaterialKey", "Lesson", schema: schema, type: "uniqueidentifier", nullable: true);
        m.AddColumn<DateTimeOffset>("ClearedUtc", "LessonProgress", schema: schema, type: "datetimeoffset", nullable: true);
        m.DropIndex("IX_Chapter_OfferingId_Position", "Chapter", schema: schema);
        m.DropIndex("IX_Chapter_OfferingId_TemplateItemKey", "Chapter", schema: schema);
        m.CreateIndex("IX_Chapter_OfferingId_Position", "Chapter", new[] { "OfferingId", "Position" }, schema: schema, unique: true, filter: "[PendingBaseId] IS NULL AND [Retired] = 0");
        m.CreateIndex("IX_Chapter_OfferingId_TemplateItemKey", "Chapter", new[] { "OfferingId", "TemplateItemKey" }, schema: schema, unique: true, filter: "[TemplateItemKey] IS NOT NULL AND [PendingBaseId] IS NULL AND [Retired] = 0");
        m.CreateIndex("IX_Chapter_OfferingId", "Chapter", "OfferingId", schema: schema);
        m.CreateIndex("IX_Chapter_PendingBaseId", "Chapter", "PendingBaseId", schema: schema, unique: true, filter: "[PendingBaseId] IS NOT NULL AND [Retired] = 0");
        m.CreateIndex("IX_Lesson_StableLessonId", "Lesson", "StableLessonId", schema: schema);
        m.AddForeignKey("FK_Chapter_Chapter_PendingBaseId", "Chapter", "PendingBaseId", "Chapter", schema: schema, principalSchema: schema, principalColumn: "Id", onDelete: ReferentialAction.NoAction);
        m.AddForeignKey("FK_Lesson_Lesson_StableLessonId", "Lesson", "StableLessonId", "Lesson", schema: schema, principalSchema: schema, principalColumn: "Id", onDelete: ReferentialAction.NoAction);
        m.CreateTable(name: "LearningMaterial", schema: schema, columns: t => new
        {
            Id = t.Column<Guid>("uniqueidentifier", nullable: false), Key = t.Column<Guid>("uniqueidentifier", nullable: false),
            LessonId = t.Column<Guid>("uniqueidentifier", nullable: false), Position = t.Column<int>("int", nullable: false),
            Kind = t.Column<int>("int", nullable: false), Title = t.Column<string>("nvarchar(180)", maxLength: 180, nullable: false),
            Description = t.Column<string>("nvarchar(450)", maxLength: 450, nullable: true), Body = t.Column<string>("nvarchar(max)", nullable: true),
            Alt = t.Column<string>("nvarchar(250)", maxLength: 250, nullable: true), MediaId = t.Column<Guid>("uniqueidentifier", nullable: true),
            CoverId = t.Column<Guid>("uniqueidentifier", nullable: true), Removed = t.Column<bool>("bit", nullable: false)
        }, constraints: t => t.PrimaryKey("PK_LearningMaterial", x => x.Id));
        m.CreateIndex("IX_LearningMaterial_LessonId_Key", "LearningMaterial", new[] { "LessonId", "Key" }, schema: schema, unique: true);
        m.CreateIndex("IX_LearningMaterial_LessonId_Position_Id", "LearningMaterial", new[] { "LessonId", "Position", "Id" }, schema: schema);
        m.CreateIndex("IX_LearningMaterial_MediaId", "LearningMaterial", "MediaId", schema: schema);
        m.CreateIndex("IX_LearningMaterial_CoverId", "LearningMaterial", "CoverId", schema: schema);
        m.AddForeignKey("FK_LearningMaterial_Lesson_LessonId", "LearningMaterial", "LessonId", "Lesson", schema: schema, principalSchema: schema, principalColumn: "Id", onDelete: ReferentialAction.NoAction);
        m.AddForeignKey("FK_LearningMaterial_Media_MediaId", "LearningMaterial", "MediaId", "Media", schema: schema, principalSchema: schema, principalColumn: "Id", onDelete: ReferentialAction.NoAction);
        m.AddForeignKey("FK_LearningMaterial_Media_CoverId", "LearningMaterial", "CoverId", "Media", schema: schema, principalSchema: schema, principalColumn: "Id", onDelete: ReferentialAction.NoAction);
    }

    protected override void Down(MigrationBuilder m) => throw new NotSupportedException("Restore a verified backup to roll back; this migration preserves curriculum versions and stable IDs.");
}
