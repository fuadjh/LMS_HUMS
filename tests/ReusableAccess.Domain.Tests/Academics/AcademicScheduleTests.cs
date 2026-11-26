using ReusableAccess.Domain.Academics;
using Xunit;

namespace ReusableAccess.Domain.Tests.Academics;

public sealed class AcademicScheduleTests
{
    private static readonly DateOnly Start = new(2027, 3, 20);
    private static DateTimeOffset Local(int day, int hour = 8) => JalaliTime.FromDate(Start.AddDays(day).ToDateTime(new(hour, 0)));
    private static Term Window() => new() { StartsUtc = Local(0, 0), EndsUtc = Local(45, 0), EnrollOpensUtc = Local(-6),
        EnrollClosesUtc = Local(5), Active = true };

    [Fact]
    public void StartsAtEightAndCloseBoundaryIsNextLocalMidnight()
    {
        var item = new ScheduleItem(Guid.NewGuid(), ScheduleKind.Chapter, "الف", StartDay: 0, EndDay: 1);
        var result = AcademicSchedule.Calculate(Window(), Start, [item], [], null);
        Assert.Empty(result.Issues);
        Assert.Equal(Local(-6), result.Rows[0].Start.AtUtc);
        Assert.Equal(Local(0), result.Rows[1].Start.AtUtc);
        Assert.Equal(Local(2, 0), result.Rows[1].End!.AtUtc);
    }

    [Fact]
    public void EarlierThanParentIsNotValidEvenWhenRowIsInOrder()
    {
        var c = Guid.NewGuid(); var l = Guid.NewGuid();
        var result = AcademicSchedule.Calculate(Window(), Start,
            [new(c, ScheduleKind.Chapter, "فصل", StartDay: 4, EndDay: 10),
             new(l, ScheduleKind.Lesson, "درسک", c, StartDay: 1, EndDay: 5)], [], null);
        Assert.Contains(result.Issues, x => x.Key == l && x.Field == ScheduleField.Start && x.Code == "ChapterWindow");
    }

    [Fact]
    public void LinkedAssessmentCanOutlastLessonButNotChapter()
    {
        var chapter = Guid.NewGuid(); var lesson = Guid.NewGuid(); var task = Guid.NewGuid();
        ScheduleItem[] content = [new(chapter, ScheduleKind.Chapter, "فصل", StartDay: 0, EndDay: 10),
            new(lesson, ScheduleKind.Lesson, "درسک", chapter, StartDay: 1, EndDay: 2),
            new(task, ScheduleKind.Assessment, "تکلیف", chapter, lesson, 1, 9, 7)];
        Assert.Empty(AcademicSchedule.Calculate(Window(), Start, content, [], null).Issues);
        var late = new ScheduleOverride(task, ScheduleField.End, ScheduleOverrideMode.Set, Local(12));
        Assert.Contains(AcademicSchedule.Calculate(Window(), Start, content, [late], null).Issues,
            x => x.Key == task && x.Field == ScheduleField.End && x.Code == "ChapterWindow");
    }

    [Fact]
    public void ShiftUsesActualTimeAndSameKindAndRespectsManualClear()
    {
        var chapter = Guid.NewGuid(); var added = Guid.NewGuid();
        var origin = new ScheduleOrigin(Start.AddDays(-7), new Dictionary<Guid, ScheduleOriginRow>
        {
            [chapter] = new(ScheduleKind.Chapter, new(Local(-5, 11), Local(3, 0), null)),
            [added] = new(ScheduleKind.Assessment, new(Local(-5, 11), null, null))
        });
        var result = AcademicSchedule.Calculate(Window(), Start,
            [new(chapter, ScheduleKind.Chapter, "عنوان تازه", StartDay: 0, EndDay: 11),
             new(added, ScheduleKind.Chapter, "تازه", StartDay: 4)],
            [new(chapter, ScheduleField.End, ScheduleOverrideMode.Clear, null)], origin);
        Assert.Empty(result.Issues);
        Assert.Equal(Local(2, 11), result.Rows[1].Start.AtUtc);
        Assert.Equal(ScheduleDateSource.Previous, result.Rows[1].Start.Source);
        Assert.Null(result.Rows[1].End!.AtUtc);
        Assert.Equal(Local(10, 0), result.Rows[1].End!.SuggestedUtc);
        Assert.Equal(Local(4), result.Rows[2].Start.AtUtc);
    }

    [Fact]
    public void RequiredDateCannotBeClearedAndDuplicateOverrideIsRejected()
    {
        var key = Guid.NewGuid();
        ScheduleItem[] items = [new(key, ScheduleKind.Chapter, "الف", StartDay: 1)];
        var overrideStart = new ScheduleOverride(key, ScheduleField.Start, ScheduleOverrideMode.Set, Local(2));
        Assert.Throws<ArgumentException>(() => AcademicSchedule.Calculate(Window(), Start, items, [overrideStart, overrideStart], null));
        Assert.Throws<ArgumentException>(() => AcademicSchedule.Calculate(Window(), Start, items,
            [new(key, ScheduleField.Start, ScheduleOverrideMode.Clear, null)], null));
    }

    [Fact]
    public void LessonAndAssignmentHonorIndependentEndBoundaries()
    {
        var chapter = new Chapter { Active = true, PublishedUtc = Local(0), VisibleFromUtc = Local(1), VisibleUntilUtc = Local(4, 0) };
        var lesson = new Lesson { Active = true, ChapterId = chapter.Id, VisibleUntilUtc = Local(3, 0) };
        var task = new Assessment { Active = true, OfferingId = chapter.OfferingId, PublishedUtc = Local(0), OpensUtc = Local(1),
            DueUtc = Local(7), CutoffUtc = Local(7) };
        Assert.False(AcademicRules.CanReadLesson(Local(1, 7), chapter, lesson));
        Assert.True(AcademicRules.CanReadLesson(Local(1), chapter, lesson));
        Assert.False(AcademicRules.CanReadLesson(Local(3, 0), chapter, lesson));
        Assert.True(AssignmentPolicies.CanSubmit(Local(4, 0), task, Window()));
        Assert.False(AssignmentPolicies.CanSubmit(Local(7), task, Window()));
    }

    [Fact]
    public void ParsingLocalAndConvertingToTehranAreSeparateOperations()
    {
        Assert.True(JalaliTime.TryParseLocal("۱۴۰۵/۰۹/۰۱ ۰۸:۰۰", out var local));
        Assert.Equal(8, local.Hour);
        Assert.True(JalaliTime.TryParse("۱۴۰۵/۰۹/۰۱ ۰۸:۰۰", out var utc));
        Assert.Equal(TimeSpan.Zero, utc.Offset);
        Assert.Equal(local, JalaliTime.InTehran(utc));
        Assert.False(JalaliTime.TryParseLocal("۱۴۰۵/۰۷/۳۱ ۰۸:۰۰", out _));
    }
}
