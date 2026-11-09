namespace GrifballWebApp.Domain.Models;

/// <summary>
/// An audited row: who made and last changed it, and when. Read-only to the app; the audit interceptor alone writes the
/// columns, through <see cref="IAuditStamp"/>.
/// </summary>
public interface IAuditable
{
    int? CreatedByID { get; }
    int? ModifiedByID { get; }
    DateTime CreatedAt { get; }
    DateTime ModifiedAt { get; }
}
