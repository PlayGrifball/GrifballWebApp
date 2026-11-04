namespace GrifballWebApp.Server.Excel;

public class SheetInfo : GrifballWebApp.Server.Jobs.IRequestedByUser
{
    public required string Name { get; set; }
    public required string SpreadsheetID { get; set; }
    public required string SheetName { get; set; }

    /// <summary>Who queued the export (it runs as them): set by the server when it's queued, whatever the request says.</summary>
    public int? RequestedByUserID { get; set; }
}
