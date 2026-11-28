namespace ReusableAccess.Contracts;

public sealed record LmsTablePage(int Page = 1, int Size = 15)
{
    public const int DefaultSize = 15;
    public static IReadOnlyList<int> Sizes { get; } = Array.AsReadOnly(new[] { DefaultSize, 30, 50, 100 });
}
public sealed record LmsTableSlice<T>(T[] Rows, int Total, int Page, int Size);
public sealed record AcademicWorkbookInput(Guid MediaId, Guid MajorId, Guid? TermId = null);
public sealed record AcademicWorkbookRow(int Line, string Label, string Status, string? Detail = null);
