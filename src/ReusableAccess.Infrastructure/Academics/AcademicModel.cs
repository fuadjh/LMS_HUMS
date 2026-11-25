using Microsoft.EntityFrameworkCore;
using ReusableAccess.Domain.Academics;
using ReusableAccess.Infrastructure.Identity;

namespace ReusableAccess.Infrastructure.Academics;

internal static class AcademicModel
{
    internal static void Configure(ModelBuilder model)
    {
        model.Entity<Faculty>(b =>
        {
            b.ToTable("Faculty", "lms");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasColumnType("uniqueidentifier").ValueGeneratedNever();
            b.Property(x => x.Code).HasColumnType("nvarchar(50)").HasMaxLength(50).IsRequired();
            b.Property(x => x.Title).HasColumnType("nvarchar(180)").HasMaxLength(180).IsRequired();
            b.Property(x => x.Active).HasColumnType("bit");
            b.HasIndex(x => x.Code).IsUnique();
        });
        model.Entity<Major>(b =>
        {
            b.ToTable("Major", "lms");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasColumnType("uniqueidentifier").ValueGeneratedNever();
            b.Property(x => x.FacultyId).HasColumnType("uniqueidentifier");
            b.Property(x => x.Code).HasColumnType("nvarchar(50)").HasMaxLength(50).IsRequired();
            b.Property(x => x.Title).HasColumnType("nvarchar(180)").HasMaxLength(180).IsRequired();
            b.Property(x => x.Active).HasColumnType("bit");
            b.HasIndex(x => new { x.FacultyId, x.Code }).IsUnique();
            b.HasOne<Faculty>().WithMany().HasForeignKey(x => x.FacultyId).OnDelete(DeleteBehavior.NoAction);
        });
        model.Entity<Degree>(b =>
        {
            b.ToTable("Degree", "lms");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasColumnType("uniqueidentifier").ValueGeneratedNever();
            b.Property(x => x.Code).HasColumnType("nvarchar(50)").HasMaxLength(50).IsRequired();
            b.Property(x => x.Title).HasColumnType("nvarchar(120)").HasMaxLength(120).IsRequired();
            b.Property(x => x.Active).HasColumnType("bit");
            b.HasIndex(x => x.Code).IsUnique();
        });
        model.Entity<AcademicProgram>(b =>
        {
            b.ToTable("AcademicProgram", "lms");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasColumnType("uniqueidentifier").ValueGeneratedNever();
            b.Property(x => x.MajorId).HasColumnType("uniqueidentifier");
            b.Property(x => x.DegreeId).HasColumnType("uniqueidentifier");
            b.Property(x => x.Active).HasColumnType("bit");
            b.HasIndex(x => new { x.MajorId, x.DegreeId }).IsUnique();
            b.HasIndex(x => x.DegreeId);
            b.HasOne<Major>().WithMany().HasForeignKey(x => x.MajorId).OnDelete(DeleteBehavior.NoAction);
            b.HasOne<Degree>().WithMany().HasForeignKey(x => x.DegreeId).OnDelete(DeleteBehavior.NoAction);
        });
        model.Entity<Cohort>(b =>
        {
            b.ToTable("Cohort", "lms");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasColumnType("uniqueidentifier").ValueGeneratedNever();
            b.Property(x => x.ProgramId).HasColumnType("uniqueidentifier");
            b.Property(x => x.Year).HasColumnType("int");
            b.Property(x => x.Season).HasColumnType("int").HasConversion<int>();
            b.Property(x => x.Active).HasColumnType("bit");
            b.HasIndex(x => new { x.ProgramId, x.Year, x.Season }).IsUnique();
            b.HasOne<AcademicProgram>().WithMany().HasForeignKey(x => x.ProgramId).OnDelete(DeleteBehavior.NoAction);
        });
        model.Entity<Term>(b =>
        {
            b.ToTable("Term", "lms");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasColumnType("uniqueidentifier").ValueGeneratedNever();
            b.Property(x => x.Year).HasColumnType("int");
            b.Property(x => x.Season).HasColumnType("int").HasConversion<int>();
            b.Property(x => x.StartsUtc).HasColumnType("datetimeoffset");
            b.Property(x => x.EndsUtc).HasColumnType("datetimeoffset");
            b.Property(x => x.EnrollOpensUtc).HasColumnType("datetimeoffset");
            b.Property(x => x.EnrollClosesUtc).HasColumnType("datetimeoffset");
            b.Property(x => x.DropOpensUtc).HasColumnType("datetimeoffset");
            b.Property(x => x.DropClosesUtc).HasColumnType("datetimeoffset");
            b.Property(x => x.ClosedUtc).HasColumnType("datetimeoffset");
            b.Property(x => x.Active).HasColumnType("bit");
            b.HasIndex(x => new { x.Year, x.Season }).IsUnique();
        });
        model.Entity<InstructorProfile>(b =>
        {
            b.ToTable("InstructorProfile", "lms");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasColumnType("uniqueidentifier").ValueGeneratedNever();
            b.Property(x => x.MajorId).HasColumnType("uniqueidentifier");
            b.Property(x => x.Classification).HasColumnType("nvarchar(100)").HasMaxLength(100).IsRequired();
            b.Property(x => x.CvUrl).HasColumnType("nvarchar(600)").HasMaxLength(600);
            b.Property(x => x.AvatarId).HasColumnType("uniqueidentifier");
            b.Property(x => x.Active).HasColumnType("bit");
            b.HasIndex(x => x.MajorId);
            b.HasIndex(x => x.AvatarId);
            b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.Id).OnDelete(DeleteBehavior.NoAction);
            b.HasOne<Major>().WithMany().HasForeignKey(x => x.MajorId).OnDelete(DeleteBehavior.NoAction);
            b.HasOne<Media>().WithMany().HasForeignKey(x => x.AvatarId).OnDelete(DeleteBehavior.NoAction);
        });
        model.Entity<StudentProfile>(b =>
        {
            b.ToTable("StudentProfile", "lms");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasColumnType("uniqueidentifier").ValueGeneratedNever();
            b.Property(x => x.AvatarId).HasColumnType("uniqueidentifier");
            b.Property(x => x.Active).HasColumnType("bit");
            b.HasIndex(x => x.AvatarId);
            b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.Id).OnDelete(DeleteBehavior.NoAction);
            b.HasOne<Media>().WithMany().HasForeignKey(x => x.AvatarId).OnDelete(DeleteBehavior.NoAction);
        });
        model.Entity<StudyRecord>(b =>
        {
            b.ToTable("StudyRecord", "lms");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasColumnType("uniqueidentifier").ValueGeneratedNever();
            b.Property(x => x.StudentId).HasColumnType("uniqueidentifier");
            b.Property(x => x.CohortId).HasColumnType("uniqueidentifier");
            b.Property(x => x.Number).HasColumnType("nvarchar(50)").HasMaxLength(50).IsRequired();
            b.Property(x => x.OpenedUtc).HasColumnType("datetimeoffset");
            b.Property(x => x.EndedUtc).HasColumnType("datetimeoffset");
            b.HasIndex(x => x.StudentId).IsUnique().HasFilter("[EndedUtc] IS NULL");
            b.HasIndex(x => new { x.CohortId, x.EndedUtc });
            b.HasIndex(x => new { x.CohortId, x.Number }).IsUnique().HasFilter("[EndedUtc] IS NULL");
            b.HasOne<StudentProfile>().WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.NoAction);
            b.HasOne<Cohort>().WithMany().HasForeignKey(x => x.CohortId).OnDelete(DeleteBehavior.NoAction);
        });
        model.Entity<AcademicGrant>(b =>
        {
            b.ToTable("AcademicGrant", "lms");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasColumnType("uniqueidentifier").ValueGeneratedNever();
            b.Property(x => x.UserId).HasColumnType("uniqueidentifier");
            b.Property(x => x.FacultyId).HasColumnType("uniqueidentifier");
            b.Property(x => x.MajorId).HasColumnType("uniqueidentifier");
            b.Property(x => x.Active).HasColumnType("bit");
            b.HasIndex(x => new { x.UserId, x.FacultyId, x.MajorId }).IsUnique().HasFilter("[MajorId] IS NOT NULL");
            b.HasIndex(x => new { x.UserId, x.FacultyId }).IsUnique().HasFilter("[MajorId] IS NULL");
            b.HasIndex(x => x.FacultyId);
            b.HasIndex(x => x.MajorId);
            b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction);
            b.HasOne<Faculty>().WithMany().HasForeignKey(x => x.FacultyId).OnDelete(DeleteBehavior.NoAction);
            b.HasOne<Major>().WithMany().HasForeignKey(x => x.MajorId).OnDelete(DeleteBehavior.NoAction);
        });
        model.Entity<CatalogCourse>(b =>
        {
            b.ToTable("CatalogCourse", "lms");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasColumnType("uniqueidentifier").ValueGeneratedNever();
            b.Property(x => x.FacultyId).HasColumnType("uniqueidentifier");
            b.Property(x => x.Code).HasColumnType("nvarchar(50)").HasMaxLength(50).IsRequired();
            b.Property(x => x.Title).HasColumnType("nvarchar(180)").HasMaxLength(180).IsRequired();
            b.Property(x => x.Active).HasColumnType("bit");
            b.HasIndex(x => new { x.FacultyId, x.Code }).IsUnique();
            b.HasOne<Faculty>().WithMany().HasForeignKey(x => x.FacultyId).OnDelete(DeleteBehavior.NoAction);
        });
        model.Entity<Offering>(b =>
        {
            b.ToTable("Offering", "lms");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasColumnType("uniqueidentifier").ValueGeneratedNever();
            b.Property(x => x.CourseId).HasColumnType("uniqueidentifier");
            b.Property(x => x.TermId).HasColumnType("uniqueidentifier");
            b.Property(x => x.OwnerMajorId).HasColumnType("uniqueidentifier");
            b.Property(x => x.LeadId).HasColumnType("uniqueidentifier");
            b.Property(x => x.GroupCode).HasColumnType("nvarchar(50)").HasMaxLength(50).IsRequired();
            b.Property(x => x.Audience).HasColumnType("int").HasConversion<int>();
            b.Property(x => x.VisibleUtc).HasColumnType("datetimeoffset");
            b.Property(x => x.PublishedUtc).HasColumnType("datetimeoffset");
            b.Property(x => x.Active).HasColumnType("bit");
            b.Property(x => x.Revision).HasColumnType("uniqueidentifier").IsConcurrencyToken();
            b.HasIndex(x => new { x.TermId, x.CourseId, x.GroupCode }).IsUnique();
            b.HasIndex(x => new { x.LeadId, x.Active });
            b.HasIndex(x => x.CourseId);
            b.HasIndex(x => x.OwnerMajorId);
            b.HasOne<CatalogCourse>().WithMany().HasForeignKey(x => x.CourseId).OnDelete(DeleteBehavior.NoAction);
            b.HasOne<Term>().WithMany().HasForeignKey(x => x.TermId).OnDelete(DeleteBehavior.NoAction);
            b.HasOne<Major>().WithMany().HasForeignKey(x => x.OwnerMajorId).OnDelete(DeleteBehavior.NoAction);
            b.HasOne<InstructorProfile>().WithMany().HasForeignKey(x => x.LeadId).OnDelete(DeleteBehavior.NoAction);
        });
        model.Entity<OfferingCohort>(b =>
        {
            b.ToTable("OfferingCohort", "lms");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasColumnType("uniqueidentifier").ValueGeneratedNever();
            b.Property(x => x.OfferingId).HasColumnType("uniqueidentifier");
            b.Property(x => x.CohortId).HasColumnType("uniqueidentifier");
            b.HasIndex(x => new { x.OfferingId, x.CohortId }).IsUnique();
            b.HasIndex(x => x.CohortId);
            b.HasOne<Offering>().WithMany().HasForeignKey(x => x.OfferingId).OnDelete(DeleteBehavior.NoAction);
            b.HasOne<Cohort>().WithMany().HasForeignKey(x => x.CohortId).OnDelete(DeleteBehavior.NoAction);
        });
        model.Entity<CoTeacher>(b =>
        {
            b.ToTable("CoTeacher", "lms");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasColumnType("uniqueidentifier").ValueGeneratedNever();
            b.Property(x => x.OfferingId).HasColumnType("uniqueidentifier");
            b.Property(x => x.InstructorId).HasColumnType("uniqueidentifier");
            b.Property(x => x.CanGrade).HasColumnType("bit");
            b.Property(x => x.CanPublish).HasColumnType("bit");
            b.Property(x => x.Active).HasColumnType("bit");
            b.HasIndex(x => new { x.OfferingId, x.InstructorId }).IsUnique();
            b.HasIndex(x => x.InstructorId);
            b.HasOne<Offering>().WithMany().HasForeignKey(x => x.OfferingId).OnDelete(DeleteBehavior.NoAction);
            b.HasOne<InstructorProfile>().WithMany().HasForeignKey(x => x.InstructorId).OnDelete(DeleteBehavior.NoAction);
        });
        model.Entity<CourseTemplate>(b =>
        {
            b.ToTable("CourseTemplate", "lms");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasColumnType("uniqueidentifier").ValueGeneratedNever();
            b.Property(x => x.OwnerId).HasColumnType("uniqueidentifier");
            b.Property(x => x.CourseId).HasColumnType("uniqueidentifier");
            b.Property(x => x.Title).HasColumnType("nvarchar(180)").HasMaxLength(180).IsRequired();
            b.Property(x => x.Active).HasColumnType("bit");
            b.HasIndex(x => new { x.OwnerId, x.CourseId, x.Title }).IsUnique();
            b.HasIndex(x => x.CourseId);
            b.HasOne<InstructorProfile>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.NoAction);
            b.HasOne<CatalogCourse>().WithMany().HasForeignKey(x => x.CourseId).OnDelete(DeleteBehavior.NoAction);
            b.Property(x => x.Description).HasColumnType("nvarchar(180)").HasMaxLength(180);
            b.Property(x => x.UpdatedUtc).HasColumnType("datetimeoffset");
            b.Property(x => x.Revision).HasColumnType("uniqueidentifier").HasDefaultValueSql("NEWID()").IsConcurrencyToken();
            b.HasIndex(x => new { x.OwnerId, x.Active, x.UpdatedUtc, x.Id });
        });
        model.Entity<TemplateVersion>(b =>
        {
            b.ToTable("TemplateVersion", "lms");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasColumnType("uniqueidentifier").ValueGeneratedNever();
            b.Property(x => x.TemplateId).HasColumnType("uniqueidentifier");
            b.Property(x => x.Number).HasColumnType("int");
            b.Property(x => x.CreatedUtc).HasColumnType("datetimeoffset");
            b.Property(x => x.Snapshot).HasColumnType("nvarchar(max)").IsRequired();
            b.HasIndex(x => new { x.TemplateId, x.Number }).IsUnique();
            b.HasOne<CourseTemplate>().WithMany().HasForeignKey(x => x.TemplateId).OnDelete(DeleteBehavior.NoAction);
            b.Property(x => x.ChapterCount).HasColumnType("int");
            b.Property(x => x.LessonCount).HasColumnType("int");
        });
        model.Entity<Chapter>(b =>
        {
            b.ToTable("Chapter", "lms");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasColumnType("uniqueidentifier").ValueGeneratedNever();
            b.Property(x => x.OfferingId).HasColumnType("uniqueidentifier");
            b.Property(x => x.Title).HasColumnType("nvarchar(180)").HasMaxLength(180).IsRequired();
            b.Property(x => x.Position).HasColumnType("int");
            b.Property(x => x.PublishedUtc).HasColumnType("datetimeoffset");
            b.Property(x => x.Active).HasColumnType("bit");
            b.HasIndex(x => new { x.OfferingId, x.Position }).IsUnique();
            b.HasOne<Offering>().WithMany().HasForeignKey(x => x.OfferingId).OnDelete(DeleteBehavior.NoAction);
            b.Property(x => x.VisibleFromUtc).HasColumnType("datetimeoffset");
            b.Property(x => x.VisibleUntilUtc).HasColumnType("datetimeoffset");
            b.Property(x => x.TemplateItemKey).HasColumnType("uniqueidentifier");
            b.HasIndex(x => new { x.OfferingId, x.TemplateItemKey }).IsUnique().HasFilter("[TemplateItemKey] IS NOT NULL");
        });
        model.Entity<LessonSession>(b =>
        {
            b.ToTable("LessonSession", "lms");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasColumnType("uniqueidentifier").ValueGeneratedNever();
            b.Property(x => x.OfferingId).HasColumnType("uniqueidentifier");
            b.Property(x => x.Title).HasColumnType("nvarchar(180)").HasMaxLength(180).IsRequired();
            b.Property(x => x.OpensUtc).HasColumnType("datetimeoffset");
            b.Property(x => x.Active).HasColumnType("bit");
            b.HasIndex(x => new { x.OfferingId, x.OpensUtc });
            b.HasOne<Offering>().WithMany().HasForeignKey(x => x.OfferingId).OnDelete(DeleteBehavior.NoAction);
        });
        model.Entity<Lesson>(b =>
        {
            b.ToTable("Lesson", "lms");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasColumnType("uniqueidentifier").ValueGeneratedNever();
            b.Property(x => x.ChapterId).HasColumnType("uniqueidentifier");
            b.Property(x => x.SessionId).HasColumnType("uniqueidentifier");
            b.Property(x => x.Title).HasColumnType("nvarchar(180)").HasMaxLength(180).IsRequired();
            b.Property(x => x.Body).HasColumnType("nvarchar(max)").IsRequired();
            b.Property(x => x.Position).HasColumnType("int");
            b.Property(x => x.Active).HasColumnType("bit");
            b.HasIndex(x => new { x.ChapterId, x.Position }).IsUnique();
            b.HasIndex(x => x.SessionId);
            b.HasOne<Chapter>().WithMany().HasForeignKey(x => x.ChapterId).OnDelete(DeleteBehavior.NoAction);
            b.HasOne<LessonSession>().WithMany().HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.NoAction);
            b.Property(x => x.VisibleFromUtc).HasColumnType("datetimeoffset");
            b.Property(x => x.VisibleUntilUtc).HasColumnType("datetimeoffset");
            b.Property(x => x.TemplateItemKey).HasColumnType("uniqueidentifier");
            b.Property(x => x.Summary).HasColumnType("nvarchar(250)").HasMaxLength(250);
            b.Property(x => x.CoverMediaId).HasColumnType("uniqueidentifier");
            b.HasAlternateKey(x => new { x.ChapterId, x.Id });
            b.HasIndex(x => new { x.ChapterId, x.TemplateItemKey }).IsUnique().HasFilter("[TemplateItemKey] IS NOT NULL");
            b.HasIndex(x => x.CoverMediaId);
            b.HasOne<Media>().WithMany().HasForeignKey(x => x.CoverMediaId).OnDelete(DeleteBehavior.NoAction);
        });
        model.Entity<LessonFile>(b =>
        {
            b.ToTable("LessonFile", "lms");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasColumnType("uniqueidentifier").ValueGeneratedNever();
            b.Property(x => x.LessonId).HasColumnType("uniqueidentifier");
            b.Property(x => x.MediaId).HasColumnType("uniqueidentifier");
            b.HasIndex(x => new { x.LessonId, x.MediaId }).IsUnique();
            b.HasIndex(x => x.MediaId);
            b.HasOne<Lesson>().WithMany().HasForeignKey(x => x.LessonId).OnDelete(DeleteBehavior.NoAction);
            b.HasOne<Media>().WithMany().HasForeignKey(x => x.MediaId).OnDelete(DeleteBehavior.NoAction);
            b.Property(x => x.Position).HasColumnType("int");
        });
        model.Entity<Assessment>(b =>
        {
            b.ToTable("Assessment", "lms");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasColumnType("uniqueidentifier").ValueGeneratedNever();
            b.Property(x => x.ChapterId).HasColumnType("uniqueidentifier");
            b.Property(x => x.Title).HasColumnType("nvarchar(180)").HasMaxLength(180).IsRequired();
            b.Property(x => x.Prompt).HasColumnType("nvarchar(max)").IsRequired();
            b.Property(x => x.Rubric).HasColumnType("nvarchar(max)").IsRequired();
            b.Property(x => x.DueUtc).HasColumnType("datetimeoffset");
            b.Property(x => x.CutoffUtc).HasColumnType("datetimeoffset");
            b.Property(x => x.MaxAttempts).HasColumnType("int");
            b.Property(x => x.MaxScore).HasColumnType("decimal(9,2)");
            b.Property(x => x.QuizSpec).HasColumnType("nvarchar(max)");
            b.Property(x => x.Active).HasColumnType("bit");
            b.HasIndex(x => x.ChapterId);
            b.HasOne<Chapter>().WithMany().HasForeignKey(x => x.ChapterId).OnDelete(DeleteBehavior.NoAction);
            b.Property(x => x.LessonId).HasColumnType("uniqueidentifier");
            b.Property(x => x.OpensUtc).HasColumnType("datetimeoffset");
            b.Property(x => x.TemplateItemKey).HasColumnType("uniqueidentifier");
            b.HasIndex(x => new { x.ChapterId, x.TemplateItemKey }).IsUnique().HasFilter("[TemplateItemKey] IS NOT NULL");
            b.HasIndex(x => new { x.ChapterId, x.LessonId });
            b.HasOne<Lesson>().WithMany().HasForeignKey(x => new { x.ChapterId, x.LessonId }).HasPrincipalKey(x => new { x.ChapterId, x.Id }).OnDelete(DeleteBehavior.NoAction);
        });
        model.Entity<Enrollment>(b =>
        {
            b.ToTable("Enrollment", "lms");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasColumnType("uniqueidentifier").ValueGeneratedNever();
            b.Property(x => x.OfferingId).HasColumnType("uniqueidentifier");
            b.Property(x => x.StudyRecordId).HasColumnType("uniqueidentifier");
            b.Property(x => x.Status).HasColumnType("int").HasConversion<int>();
            b.Property(x => x.CreatedUtc).HasColumnType("datetimeoffset");
            b.Property(x => x.ChangedUtc).HasColumnType("datetimeoffset");
            b.Property(x => x.Revision).HasColumnType("uniqueidentifier").IsConcurrencyToken();
            b.HasIndex(x => new { x.OfferingId, x.StudyRecordId }).IsUnique();
            b.HasIndex(x => new { x.StudyRecordId, x.Status });
            b.HasOne<Offering>().WithMany().HasForeignKey(x => x.OfferingId).OnDelete(DeleteBehavior.NoAction);
            b.HasOne<StudyRecord>().WithMany().HasForeignKey(x => x.StudyRecordId).OnDelete(DeleteBehavior.NoAction);
        });
        model.Entity<EnrollmentEvent>(b =>
        {
            b.ToTable("EnrollmentEvent", "lms");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasColumnType("uniqueidentifier").ValueGeneratedNever();
            b.Property(x => x.EnrollmentId).HasColumnType("uniqueidentifier");
            b.Property(x => x.ActorId).HasColumnType("uniqueidentifier");
            b.Property(x => x.Status).HasColumnType("int").HasConversion<int>();
            b.Property(x => x.OccurredUtc).HasColumnType("datetimeoffset");
            b.Property(x => x.Reason).HasColumnType("nvarchar(500)").HasMaxLength(500);
            b.HasIndex(x => new { x.EnrollmentId, x.OccurredUtc });
            b.HasIndex(x => x.ActorId);
            b.HasOne<Enrollment>().WithMany().HasForeignKey(x => x.EnrollmentId).OnDelete(DeleteBehavior.NoAction);
            b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ActorId).OnDelete(DeleteBehavior.NoAction);
        });
        model.Entity<LessonProgress>(b =>
        {
            b.ToTable("LessonProgress", "lms");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasColumnType("uniqueidentifier").ValueGeneratedNever();
            b.Property(x => x.EnrollmentId).HasColumnType("uniqueidentifier");
            b.Property(x => x.LessonId).HasColumnType("uniqueidentifier");
            b.Property(x => x.CompletedUtc).HasColumnType("datetimeoffset");
            b.HasIndex(x => new { x.EnrollmentId, x.LessonId }).IsUnique();
            b.HasIndex(x => x.LessonId);
            b.HasOne<Enrollment>().WithMany().HasForeignKey(x => x.EnrollmentId).OnDelete(DeleteBehavior.NoAction);
            b.HasOne<Lesson>().WithMany().HasForeignKey(x => x.LessonId).OnDelete(DeleteBehavior.NoAction);
        });
        model.Entity<Submission>(b =>
        {
            b.ToTable("Submission", "lms");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasColumnType("uniqueidentifier").ValueGeneratedNever();
            b.Property(x => x.AssessmentId).HasColumnType("uniqueidentifier");
            b.Property(x => x.EnrollmentId).HasColumnType("uniqueidentifier");
            b.Property(x => x.Attempt).HasColumnType("int");
            b.Property(x => x.SentUtc).HasColumnType("datetimeoffset");
            b.Property(x => x.IsLate).HasColumnType("bit");
            b.Property(x => x.Body).HasColumnType("nvarchar(max)").IsRequired();
            b.Property(x => x.QuizAnswers).HasColumnType("nvarchar(max)");
            b.HasIndex(x => new { x.AssessmentId, x.EnrollmentId, x.Attempt }).IsUnique();
            b.HasIndex(x => x.EnrollmentId);
            b.HasOne<Assessment>().WithMany().HasForeignKey(x => x.AssessmentId).OnDelete(DeleteBehavior.NoAction);
            b.HasOne<Enrollment>().WithMany().HasForeignKey(x => x.EnrollmentId).OnDelete(DeleteBehavior.NoAction);
        });
        model.Entity<SubmissionFile>(b =>
        {
            b.ToTable("SubmissionFile", "lms");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasColumnType("uniqueidentifier").ValueGeneratedNever();
            b.Property(x => x.SubmissionId).HasColumnType("uniqueidentifier");
            b.Property(x => x.MediaId).HasColumnType("uniqueidentifier");
            b.HasIndex(x => new { x.SubmissionId, x.MediaId }).IsUnique();
            b.HasIndex(x => x.MediaId);
            b.HasOne<Submission>().WithMany().HasForeignKey(x => x.SubmissionId).OnDelete(DeleteBehavior.NoAction);
            b.HasOne<Media>().WithMany().HasForeignKey(x => x.MediaId).OnDelete(DeleteBehavior.NoAction);
        });
        model.Entity<AssessmentGrade>(b =>
        {
            b.ToTable("AssessmentGrade", "lms");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasColumnType("uniqueidentifier").ValueGeneratedNever();
            b.Property(x => x.SubmissionId).HasColumnType("uniqueidentifier");
            b.Property(x => x.GraderId).HasColumnType("uniqueidentifier");
            b.Property(x => x.Revision).HasColumnType("int");
            b.Property(x => x.Score).HasColumnType("decimal(9,2)");
            b.Property(x => x.Feedback).HasColumnType("nvarchar(max)").IsRequired();
            b.Property(x => x.Published).HasColumnType("bit");
            b.Property(x => x.CreatedUtc).HasColumnType("datetimeoffset");
            b.HasIndex(x => new { x.SubmissionId, x.Revision }).IsUnique();
            b.HasIndex(x => x.GraderId);
            b.HasOne<Submission>().WithMany().HasForeignKey(x => x.SubmissionId).OnDelete(DeleteBehavior.NoAction);
            b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.GraderId).OnDelete(DeleteBehavior.NoAction);
        });
        model.Entity<FinalGrade>(b =>
        {
            b.ToTable("FinalGrade", "lms");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasColumnType("uniqueidentifier").ValueGeneratedNever();
            b.Property(x => x.EnrollmentId).HasColumnType("uniqueidentifier");
            b.Property(x => x.GraderId).HasColumnType("uniqueidentifier");
            b.Property(x => x.Revision).HasColumnType("int");
            b.Property(x => x.Score).HasColumnType("decimal(9,2)");
            b.Property(x => x.Feedback).HasColumnType("nvarchar(max)").IsRequired();
            b.Property(x => x.Published).HasColumnType("bit");
            b.Property(x => x.CreatedUtc).HasColumnType("datetimeoffset");
            b.HasIndex(x => new { x.EnrollmentId, x.Revision }).IsUnique();
            b.HasIndex(x => x.GraderId);
            b.HasOne<Enrollment>().WithMany().HasForeignKey(x => x.EnrollmentId).OnDelete(DeleteBehavior.NoAction);
            b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.GraderId).OnDelete(DeleteBehavior.NoAction);
        });
        model.Entity<CohortMessage>(b =>
        {
            b.ToTable("CohortMessage", "lms");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasColumnType("uniqueidentifier").ValueGeneratedNever();
            b.Property(x => x.CohortId).HasColumnType("uniqueidentifier");
            b.Property(x => x.SenderId).HasColumnType("uniqueidentifier");
            b.Property(x => x.RecipientId).HasColumnType("uniqueidentifier");
            b.Property(x => x.SentUtc).HasColumnType("datetimeoffset");
            b.Property(x => x.Body).HasColumnType("nvarchar(2000)").HasMaxLength(2000).IsRequired();
            b.HasIndex(x => new { x.CohortId, x.SentUtc });
            b.HasIndex(x => x.SenderId);
            b.HasIndex(x => x.RecipientId);
            b.HasOne<Cohort>().WithMany().HasForeignKey(x => x.CohortId).OnDelete(DeleteBehavior.NoAction);
            b.HasOne<StudentProfile>().WithMany().HasForeignKey(x => x.SenderId).OnDelete(DeleteBehavior.NoAction);
            b.HasOne<StudentProfile>().WithMany().HasForeignKey(x => x.RecipientId).OnDelete(DeleteBehavior.NoAction);
        });
        model.Entity<Reminder>(b =>
        {
            b.ToTable("Reminder", "lms");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasColumnType("uniqueidentifier").ValueGeneratedNever();
            b.Property(x => x.UserId).HasColumnType("uniqueidentifier");
            b.Property(x => x.OfferingId).HasColumnType("uniqueidentifier");
            b.Property(x => x.EventId).HasColumnType("uniqueidentifier");
            b.Property(x => x.Kind).HasColumnType("nvarchar(30)").HasMaxLength(30).IsRequired();
            b.Property(x => x.Title).HasColumnType("nvarchar(180)").HasMaxLength(180).IsRequired();
            b.Property(x => x.DueUtc).HasColumnType("datetimeoffset");
            b.Property(x => x.CreatedUtc).HasColumnType("datetimeoffset");
            b.Property(x => x.ReadUtc).HasColumnType("datetimeoffset");
            b.Property(x => x.SmsState).HasColumnType("nvarchar(30)").HasMaxLength(30).IsRequired();
            b.HasIndex(x => new { x.UserId, x.Kind, x.EventId, x.DueUtc }).IsUnique();
            b.HasIndex(x => new { x.UserId, x.CreatedUtc });
            b.HasIndex(x => new { x.SmsState, x.CreatedUtc });
            b.HasIndex(x => x.OfferingId);
            b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction);
            b.HasOne<Offering>().WithMany().HasForeignKey(x => x.OfferingId).OnDelete(DeleteBehavior.NoAction);
        });
        model.Entity<SmsSettings>(b =>
        {
            b.ToTable("SmsSettings", "lms");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasColumnType("uniqueidentifier").ValueGeneratedNever();
            b.Property(x => x.Provider).HasColumnType("nvarchar(50)").HasMaxLength(50).IsRequired();
            b.Property(x => x.ProtectedKey).HasColumnType("nvarchar(max)").IsRequired();
            b.Property(x => x.UpdatedUtc).HasColumnType("datetimeoffset");
        });
        model.Entity<Media>(b =>
        {
            b.ToTable("Media", "lms");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasColumnType("uniqueidentifier").ValueGeneratedNever();
            b.Property(x => x.OwnerId).HasColumnType("uniqueidentifier");
            b.Property(x => x.TargetId).HasColumnType("uniqueidentifier");
            b.Property(x => x.Purpose).HasColumnType("int").HasConversion<int>();
            b.Property(x => x.Bytes).HasColumnType("bigint");
            b.Property(x => x.Name).HasColumnType("nvarchar(180)").HasMaxLength(180).IsRequired();
            b.Property(x => x.Extension).HasColumnType("nvarchar(10)").HasMaxLength(10).IsRequired();
            b.Property(x => x.CreatedUtc).HasColumnType("datetimeoffset");
            b.HasIndex(x => new { x.OwnerId, x.Purpose, x.TargetId });
            b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.NoAction);
        });
        model.Entity<UploadAttempt>(b =>
        {
            b.ToTable("UploadAttempt", "lms");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasColumnType("uniqueidentifier").ValueGeneratedNever();
            b.Property(x => x.ActorId).HasColumnType("uniqueidentifier");
            b.Property(x => x.TargetId).HasColumnType("uniqueidentifier");
            b.Property(x => x.Purpose).HasColumnType("int").HasConversion<int>();
            b.Property(x => x.Name).HasColumnType("nvarchar(180)").HasMaxLength(180).IsRequired();
            b.Property(x => x.Extension).HasColumnType("nvarchar(10)").HasMaxLength(10).IsRequired();
            b.Property(x => x.ReportedBytes).HasColumnType("bigint");
            b.Property(x => x.ReceivedBytes).HasColumnType("bigint");
            b.Property(x => x.CreatedUtc).HasColumnType("datetimeoffset");
            b.Property(x => x.UpdatedUtc).HasColumnType("datetimeoffset");
            b.Property(x => x.State).HasColumnType("int").HasConversion<int>();
            b.Property(x => x.Code).HasColumnType("nvarchar(80)").HasMaxLength(80);
            b.Property(x => x.Ip).HasColumnType("nvarchar(64)").HasMaxLength(64);
            b.Property(x => x.Correlation).HasColumnType("nvarchar(100)").HasMaxLength(100);
            b.HasIndex(x => x.CreatedUtc);
            b.HasIndex(x => new { x.State, x.UpdatedUtc });
            b.HasIndex(x => x.ActorId);
            b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ActorId).OnDelete(DeleteBehavior.NoAction);
        });
        model.Entity<OperationalEvent>(b =>
        {
            b.ToTable("OperationalEvent", "lms");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasColumnType("uniqueidentifier").ValueGeneratedNever();
            b.Property(x => x.ActorId).HasColumnType("uniqueidentifier");
            b.Property(x => x.OccurredUtc).HasColumnType("datetimeoffset");
            b.Property(x => x.Action).HasColumnType("nvarchar(120)").HasMaxLength(120).IsRequired();
            b.Property(x => x.Outcome).HasColumnType("nvarchar(40)").HasMaxLength(40).IsRequired();
            b.Property(x => x.ResourceId).HasColumnType("uniqueidentifier");
            b.Property(x => x.Ip).HasColumnType("nvarchar(64)").HasMaxLength(64);
            b.Property(x => x.Correlation).HasColumnType("nvarchar(100)").HasMaxLength(100);
            b.Property(x => x.HttpStatus).HasColumnType("int");
            b.HasIndex(x => x.OccurredUtc);
            b.HasIndex(x => new { x.ActorId, x.OccurredUtc });
        });
        model.Entity<OfferingScheduleDraft>(b =>
        {
            b.Property(x => x.OfferingId).HasColumnType("uniqueidentifier").ValueGeneratedNever();
            b.Property(x => x.TemplateVersionId).HasColumnType("uniqueidentifier");
            b.Property(x => x.OriginOfferingId).HasColumnType("uniqueidentifier");
            b.Property(x => x.ClassesStartDate).HasColumnType("date");
            b.Property(x => x.Mode).HasColumnType("int").HasConversion<int>();
            b.Property(x => x.State).HasColumnType("int").HasConversion<int>();
            b.Property(x => x.OfferingRevision).HasColumnType("uniqueidentifier");
            b.Property(x => x.AppliedOfferingRevision).HasColumnType("uniqueidentifier");
            b.Property(x => x.Revision).HasColumnType("uniqueidentifier").IsConcurrencyToken();
            b.Property(x => x.UpdatedBy).HasColumnType("uniqueidentifier");
            b.Property(x => x.UpdatedUtc).HasColumnType("datetimeoffset");
            b.Property(x => x.AppliedUtc).HasColumnType("datetimeoffset");
            b.Property(x => x.PublishedUtc).HasColumnType("datetimeoffset");
            b.Property(x => x.TermVersion).HasColumnType("varchar(64)").HasMaxLength(64).IsRequired();
            b.Property(x => x.OriginVersion).HasColumnType("varchar(64)").HasMaxLength(64);
            b.Property(x => x.Payload).HasColumnType("nvarchar(max)").IsRequired();
            b.HasKey(x => x.OfferingId);
            b.ToTable("OfferingScheduleDraft", "lms");
            b.HasIndex(x => new { x.TemplateVersionId, x.State });
            b.HasIndex(x => x.OriginOfferingId);
            b.HasIndex(x => x.UpdatedBy);
            b.HasOne<Offering>().WithMany().HasForeignKey(x => x.OfferingId).OnDelete(DeleteBehavior.NoAction);
            b.HasOne<TemplateVersion>().WithMany().HasForeignKey(x => x.TemplateVersionId).OnDelete(DeleteBehavior.NoAction);
            b.HasOne<Offering>().WithMany().HasForeignKey(x => x.OriginOfferingId).OnDelete(DeleteBehavior.NoAction);
            b.HasOne<InstructorProfile>().WithMany().HasForeignKey(x => x.UpdatedBy).OnDelete(DeleteBehavior.NoAction);
        });
        model.Entity<TemplateVersionMedia>(b =>
        {
            b.Property(x => x.TemplateVersionId).HasColumnType("uniqueidentifier");
            b.Property(x => x.MediaId).HasColumnType("uniqueidentifier");
            b.HasKey(x => new { x.TemplateVersionId, x.MediaId });
            b.ToTable("TemplateVersionMedia", "lms");
            b.HasIndex(x => x.MediaId);
            b.HasOne<TemplateVersion>().WithMany().HasForeignKey(x => x.TemplateVersionId).OnDelete(DeleteBehavior.NoAction);
            b.HasOne<Media>().WithMany().HasForeignKey(x => x.MediaId).OnDelete(DeleteBehavior.NoAction);
        });
    }
}
