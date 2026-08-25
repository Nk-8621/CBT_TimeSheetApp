namespace Meridian.Application.DTOs;

/// <summary>Result of importing one week's timesheet from Excel. Rows that
/// don't match real master data are skipped and reported here rather than
/// silently creating new projects/tasks from typos.</summary>
public record ExcelImportResult(int LinesImported, IReadOnlyList<string> Errors);