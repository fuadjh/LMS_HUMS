namespace ReusableAccess.Contracts;

public static class UploadLimits
{
    public const long CurriculumFileBytes = 25 * 1024 * 1024;
    public const long CurriculumCoverBytes = 3 * 1024 * 1024;
    public const long FileBytes = 250_000_000;
    public const long CoverBytes = 3 * 1024 * 1024;
    public const int CoverMaxAxis = 4096;
    public const long RosterBytes = 8_000_000;
}
