using GrifballWebApp.Infrastructure.Excel;

namespace GrifballWebApp.Server.Excel;

/// <summary>A Google sheet, as the Excel page names it: what it asks to export to, and the defaults it offers.</summary>
public class SheetDto
{
    public required string Name { get; set; }
    public required string SpreadsheetID { get; set; }
    public required string SheetName { get; set; }

    public static SheetDto From(SheetInfo sheet) => new() { Name = sheet.Name, SpreadsheetID = sheet.SpreadsheetID, SheetName = sheet.SheetName };

    /// <summary>The sheet as the export runs it: <paramref name="requestedByUserID"/> is who asked, never the request's say.</summary>
    public SheetInfo ToInfo(int? requestedByUserID) => new() { Name = Name, SpreadsheetID = SpreadsheetID, SheetName = SheetName, RequestedByUserID = requestedByUserID };
}
