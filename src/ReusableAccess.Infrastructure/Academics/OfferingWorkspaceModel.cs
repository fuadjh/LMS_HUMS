using Microsoft.EntityFrameworkCore;
using ReusableAccess.Domain.Academics;
using ReusableAccess.Infrastructure.Identity;

namespace ReusableAccess.Infrastructure.Academics;

internal static class OfferingWorkspaceModel
{
    internal static void Configure(ModelBuilder model)
    {
        model.Entity<AssignmentTemplateVersion>(b =>
        {
            b.ToTable("AssignmentTemplateVersion", "lms");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasColumnType("uniqueidentifier").ValueGeneratedNever();
            b.Property(x => x.FamilyId).HasColumnType("uniqueidentifier");
            b.Property(x => x.OwnerId).HasColumnType("uniqueidentifier");
            b.Property(x => x.Number).HasColumnType("int");
            b.Property(x => x.Title).HasColumnType("nvarchar(180)").HasMaxLength(180).IsRequired();
            b.Property(x => x.Prompt).HasColumnType("nvarchar(max)").IsRequired();
            b.Property(x => x.Rubric).HasColumnType("nvarchar(max)").IsRequired();
            b.Property(x => x.MaxAttempts).HasColumnType("int");
            b.Property(x => x.MaxScore).HasColumnType("decimal(9,2)");
            b.Property(x => x.CreatedUtc).HasColumnType("datetimeoffset");
            b.HasIndex(x => new { x.OwnerId, x.FamilyId, x.Number }).IsUnique();
            b.HasIndex(x => new { x.OwnerId, x.CreatedUtc, x.Id });
            b.HasOne<InstructorProfile>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.NoAction);
        });
        model.Entity<AssignmentExtension>(b =>
        {
            b.ToTable("AssignmentExtension", "lms");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasColumnType("uniqueidentifier").ValueGeneratedNever();
            b.Property(x => x.AssessmentId).HasColumnType("uniqueidentifier");
            b.Property(x => x.InstructorId).HasColumnType("uniqueidentifier");
            b.Property(x => x.PreviousDeadlineUtc).HasColumnType("datetimeoffset");
            b.Property(x => x.DeadlineUtc).HasColumnType("datetimeoffset");
            b.Property(x => x.Penalty).HasColumnType("decimal(9,2)");
            b.Property(x => x.RecordedUtc).HasColumnType("datetimeoffset");
            b.HasIndex(x => new { x.AssessmentId, x.RecordedUtc });
            b.HasIndex(x => x.InstructorId);
            b.HasOne<Assessment>().WithMany().HasForeignKey(x => x.AssessmentId).OnDelete(DeleteBehavior.NoAction);
            b.HasOne<InstructorProfile>().WithMany().HasForeignKey(x => x.InstructorId).OnDelete(DeleteBehavior.NoAction);
        });
        model.Entity<AssignmentCalendarReceipt>(b =>
        {
            b.ToTable("AssignmentCalendarReceipt", "lms");
            b.HasKey(x => x.SubmissionId);
            b.Property(x => x.SubmissionId).HasColumnType("uniqueidentifier").ValueGeneratedNever();
            b.Property(x => x.InstructorId).HasColumnType("uniqueidentifier");
            b.Property(x => x.Title).HasColumnType("nvarchar(220)").HasMaxLength(220).IsRequired();
            b.Property(x => x.ReceivedUtc).HasColumnType("datetimeoffset");
            b.HasIndex(x => new { x.InstructorId, x.ReceivedUtc });
            b.HasOne<Submission>().WithMany().HasForeignKey(x => x.SubmissionId).OnDelete(DeleteBehavior.NoAction);
            b.HasOne<InstructorProfile>().WithMany().HasForeignKey(x => x.InstructorId).OnDelete(DeleteBehavior.NoAction);
        });
        model.Entity<CourseDiscussionMessage>(b =>
        {
            b.ToTable("CourseDiscussionMessage", "lms");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasColumnType("uniqueidentifier").ValueGeneratedNever();
            b.Property(x => x.OfferingId).HasColumnType("uniqueidentifier");
            b.Property(x => x.SenderId).HasColumnType("uniqueidentifier");
            b.Property(x => x.ByInstructor).HasColumnType("bit");
            b.Property(x => x.Text).HasColumnType("nvarchar(2000)").HasMaxLength(2000).IsRequired();
            b.Property(x => x.SentUtc).HasColumnType("datetimeoffset");
            b.HasIndex(x => new { x.OfferingId, x.SentUtc, x.Id });
            b.HasIndex(x => new { x.SenderId, x.SentUtc });
            b.HasOne<Offering>().WithMany().HasForeignKey(x => x.OfferingId).OnDelete(DeleteBehavior.NoAction);
            b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.SenderId).OnDelete(DeleteBehavior.NoAction);
        });
    }
}
