using System.Globalization;
using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;
using ReusableAccess.Contracts;

namespace ReusableAccess.Infrastructure.Academics;

internal static class RosterWorkbook
{
    private const int MaxRows = 500;
    private const int MaxUncompressed = 20_000_000;
    private static readonly XNamespace X = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    internal sealed record Row(int Line, ProfileEdit Input, string? Error);

    // Read only the first worksheet of the supplied template; never evaluate formulas or relationships.
    internal static Row[] Read(Stream stream, bool student, Guid scope)
    {
        try
        {
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
            if (archive.Entries.Count is 0 or > 128 || archive.Entries.Sum(x => x.Length) > MaxUncompressed)
                throw LmsError.Invalid("ساختار یا حجم بازشدهٔ فایل Excel مجاز نیست.");
            var shared = Load(archive, "xl/sharedStrings.xml", false)?.Root?.Elements(X + "si")
                .Select(Flatten).ToArray() ?? [];
            var body = Load(archive, "xl/worksheets/sheet1.xml", true)?.Root?.Element(X + "sheetData")
                ?? throw LmsError.Invalid("برگهٔ اول قالب پیدا نشد.");
            var rows = body.Elements(X + "row").ToArray();
            if (rows.Length is < 2 or > MaxRows + 1) throw LmsError.Invalid("فایل باید ۱ تا ۵۰۰ ردیف داده داشته باشد.");
            var expected = student
                ? new[] { "national_id", "first_name", "last_name", "organization_code", "email", "mobile", "student_number" }
                : new[] { "national_id", "first_name", "last_name", "organization_code", "email", "mobile", "classification", "cv_url" };
            var header = Cells(rows[0], shared, expected.Length);
            if (header.Error is not null || !header.Values.SequenceEqual(expected, StringComparer.OrdinalIgnoreCase))
                throw LmsError.Invalid("ستون‌ها مطابق قالب همراه پروژه نیستند؛ برگهٔ اول را بررسی کنید.");
            var entries = new List<Row>();
            foreach (var (row, index) in rows.Skip(1).Select((r, i) => (r, i)))
            {
                var (v, error) = Cells(row, shared, expected.Length);
                if (error is null && v.All(string.IsNullOrWhiteSpace)) continue;
                var line = int.TryParse(row.Attribute("r")?.Value, out var n) && n >= 2 ? n : index + 2;
                entries.Add(new(line, new ProfileEdit(v[0], scope, v[1], v[2], v[3], v[4], v[5],
                    StudentNumber: student ? v[6] : null, Classification: student ? null : v[6], CvUrl: student ? null : v[7]), error));
            }
            if (entries.Count == 0) throw LmsError.Invalid("فایل داده‌ای ندارد.");
            return entries.ToArray();
        }
        catch (Exception e) when (e is IOException or InvalidDataException or XmlException or OverflowException)
        { throw LmsError.Invalid("فایل XLSX ناقص یا نامعتبر است."); }
    }
    private static (string[] Values, string? Error) Cells(XElement row, string[] shared, int count)
    {
        var result = Enumerable.Repeat("", count).ToArray();
        string? error = null;
        var seen = new HashSet<int>();
        foreach (var cell in row.Elements(X + "c"))
        {
            var address = cell.Attribute("r")?.Value ?? "";
            if (address.Length < 2 || address[1] is < '0' or > '9' || address[0] < 'A' || address[0] >= 'A' + count) continue;
            var column = address[0] - 'A';
            if (!seen.Add(column)) { error = "سلول تکراری در ردیف وجود دارد."; continue; }
            if (cell.Element(X + "f") is not null) { error = "استفاده از فرمول مجاز نیست."; continue; }
            var kind = cell.Attribute("t")?.Value ?? "n";
            var raw = cell.Element(X + "v")?.Value ?? "";
            if (kind == "s")
            {
                if (!int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var index) || index >= shared.Length)
                { error = "رشتهٔ اشتراکی Excel نامعتبر است."; continue; }
                raw = shared[index];
            }
            else if (kind == "inlineStr") raw = cell.Element(X + "is") is { } content ? Flatten(content) : "";
            else if (raw.Length != 0 && (kind is not ("n" or "str") || kind == "n" && column is 0 or 5 or 6))
            { error = "کد ملی، موبایل و شمارهٔ دانشجویی را به صورت متن وارد کنید تا صفر آغازین حذف نشود."; continue; }
            if (raw.Length > 750) { error = "طول مقدار سلول بیش از حد مجاز است."; continue; }
            result[column] = raw.Trim();
        }
        return (result, error);
    }
    private static string Flatten(XElement e) => string.Concat(e.Descendants(X + "t").Select(x => x.Value));
    private static XDocument? Load(ZipArchive archive, string name, bool required)
    {
        var entry = archive.GetEntry(name);
        if (entry is null) return required ? throw LmsError.Invalid("فایل با قالب XLSX سازگار نیست.") : null;
        using var stream = entry.Open();
        using var reader = XmlReader.Create(stream, new XmlReaderSettings
        { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 4_000_000 });
        return XDocument.Load(reader);
    }
}
