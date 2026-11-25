using Microsoft.EntityFrameworkCore;

namespace ReusableAccess.Infrastructure.Persistence.Migrations;

// Frozen additions for the second LMS migration. Do not replace with the runtime configuration.
internal static class AcademicStudioSchemaV2
{
    internal static void Build(ModelBuilder model)
    {
        AcademicSchemaV1.Build(model);
        model.Entity("ReusableAccess.Domain.Academics.CourseTemplate", b =>
        {
            b.Property<string>("Description").HasColumnType("nvarchar(180)").HasMaxLength(180);
            b.Property<DateTimeOffset?>("UpdatedUtc").HasColumnType("datetimeoffset");
            b.Property<Guid>("Revision").HasColumnType("uniqueidentifier").HasDefaultValueSql("NEWID()").IsConcurrencyToken();
            b.HasIndex("OwnerId", "Active", "UpdatedUtc", "Id");
        });
        model.Entity("ReusableAccess.Domain.Academics.TemplateVersion", b =>
        {
            b.Property<int?>("ChapterCount").HasColumnType("int");
            b.Property<int?>("LessonCount").HasColumnType("int");
        });
        model.Entity("ReusableAccess.Domain.Academics.Chapter", b =>
        {
            b.Property<DateTimeOffset?>("VisibleFromUtc").HasColumnType("datetimeoffset");
            b.Property<DateTimeOffset?>("VisibleUntilUtc").HasColumnType("datetimeoffset");
            b.Property<Guid?>("TemplateItemKey").HasColumnType("uniqueidentifier");
            b.HasIndex("OfferingId", "TemplateItemKey").IsUnique().HasFilter("[TemplateItemKey] IS NOT NULL");
        });
        model.Entity("ReusableAccess.Domain.Academics.Lesson", b =>
        {
            b.Property<DateTimeOffset?>("VisibleFromUtc").HasColumnType("datetimeoffset");
            b.Property<DateTimeOffset?>("VisibleUntilUtc").HasColumnType("datetimeoffset");
            b.Property<Guid?>("TemplateItemKey").HasColumnType("uniqueidentifier");
            b.Property<string>("Summary").HasColumnType("nvarchar(250)").HasMaxLength(250);
            b.Property<Guid?>("CoverMediaId").HasColumnType("uniqueidentifier");
            b.HasAlternateKey("ChapterId", "Id");
            b.HasIndex("ChapterId", "TemplateItemKey").IsUnique().HasFilter("[TemplateItemKey] IS NOT NULL");
            b.HasIndex("CoverMediaId");
        });
        model.Entity("ReusableAccess.Domain.Academics.LessonFile", b =>
        {
            b.Property<int?>("Position").HasColumnType("int");
        });
        model.Entity("ReusableAccess.Domain.Academics.Assessment", b =>
        {
            b.Property<Guid?>("LessonId").HasColumnType("uniqueidentifier");
            b.Property<DateTimeOffset?>("OpensUtc").HasColumnType("datetimeoffset");
            b.Property<Guid?>("TemplateItemKey").HasColumnType("uniqueidentifier");
            b.HasIndex("ChapterId", "TemplateItemKey").IsUnique().HasFilter("[TemplateItemKey] IS NOT NULL");
            b.HasIndex("ChapterId", "LessonId");
        });
        model.Entity("ReusableAccess.Domain.Academics.OfferingScheduleDraft", b =>
        {
            b.Property<Guid>("OfferingId").HasColumnType("uniqueidentifier").ValueGeneratedNever();
            b.Property<Guid>("TemplateVersionId").HasColumnType("uniqueidentifier");
            b.Property<Guid?>("OriginOfferingId").HasColumnType("uniqueidentifier");
            b.Property<DateOnly>("ClassesStartDate").HasColumnType("date");
            b.Property<int>("Mode").HasColumnType("int");
            b.Property<int>("State").HasColumnType("int");
            b.Property<Guid>("OfferingRevision").HasColumnType("uniqueidentifier");
            b.Property<Guid?>("AppliedOfferingRevision").HasColumnType("uniqueidentifier");
            b.Property<Guid>("Revision").HasColumnType("uniqueidentifier").IsConcurrencyToken();
            b.Property<Guid>("UpdatedBy").HasColumnType("uniqueidentifier");
            b.Property<DateTimeOffset>("UpdatedUtc").HasColumnType("datetimeoffset");
            b.Property<DateTimeOffset?>("AppliedUtc").HasColumnType("datetimeoffset");
            b.Property<DateTimeOffset?>("PublishedUtc").HasColumnType("datetimeoffset");
            b.Property<string>("TermVersion").HasColumnType("varchar(64)").HasMaxLength(64).IsRequired();
            b.Property<string>("OriginVersion").HasColumnType("varchar(64)").HasMaxLength(64);
            b.Property<string>("Payload").HasColumnType("nvarchar(max)").IsRequired();
            b.HasKey("OfferingId");
            b.ToTable("OfferingScheduleDraft", "lms");
            b.HasIndex("TemplateVersionId", "State");
            b.HasIndex("OriginOfferingId");
            b.HasIndex("UpdatedBy");
        });
        model.Entity("ReusableAccess.Domain.Academics.TemplateVersionMedia", b =>
        {
            b.Property<Guid>("TemplateVersionId").HasColumnType("uniqueidentifier");
            b.Property<Guid>("MediaId").HasColumnType("uniqueidentifier");
            b.HasKey("TemplateVersionId", "MediaId");
            b.ToTable("TemplateVersionMedia", "lms");
            b.HasIndex("MediaId");
        });
        model.Entity("ReusableAccess.Domain.Academics.Lesson", b =>
        {
            b.HasOne("ReusableAccess.Domain.Academics.Media", null).WithMany().HasForeignKey("CoverMediaId").OnDelete(DeleteBehavior.NoAction);
        });
        model.Entity("ReusableAccess.Domain.Academics.Assessment", b =>
        {
            b.HasOne("ReusableAccess.Domain.Academics.Lesson", null).WithMany().HasForeignKey("ChapterId", "LessonId").HasPrincipalKey("ChapterId", "Id").OnDelete(DeleteBehavior.NoAction);
        });
        model.Entity("ReusableAccess.Domain.Academics.OfferingScheduleDraft", b =>
        {
            b.HasOne("ReusableAccess.Domain.Academics.Offering", null).WithMany().HasForeignKey("OfferingId").OnDelete(DeleteBehavior.NoAction).IsRequired();
            b.HasOne("ReusableAccess.Domain.Academics.TemplateVersion", null).WithMany().HasForeignKey("TemplateVersionId").OnDelete(DeleteBehavior.NoAction).IsRequired();
            b.HasOne("ReusableAccess.Domain.Academics.Offering", null).WithMany().HasForeignKey("OriginOfferingId").OnDelete(DeleteBehavior.NoAction);
            b.HasOne("ReusableAccess.Domain.Academics.InstructorProfile", null).WithMany().HasForeignKey("UpdatedBy").OnDelete(DeleteBehavior.NoAction).IsRequired();
        });
        model.Entity("ReusableAccess.Domain.Academics.TemplateVersionMedia", b =>
        {
            b.HasOne("ReusableAccess.Domain.Academics.TemplateVersion", null).WithMany().HasForeignKey("TemplateVersionId").OnDelete(DeleteBehavior.NoAction).IsRequired();
            b.HasOne("ReusableAccess.Domain.Academics.Media", null).WithMany().HasForeignKey("MediaId").OnDelete(DeleteBehavior.NoAction).IsRequired();
        });
    }
}
