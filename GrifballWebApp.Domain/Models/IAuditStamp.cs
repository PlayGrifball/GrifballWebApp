namespace GrifballWebApp.Domain.Models;

/// <summary>
/// The audit columns' only way in: the audit interceptor stamps them through it as it saves. Internal, so the app can't
/// reach it; inside the Domain only AuditInterceptor sets them (AggregateBoundaryTests holds it to that).
/// </summary>
internal interface IAuditStamp : IAuditable
{
    new int? CreatedByID { get; set; }
    new int? ModifiedByID { get; set; }
    new DateTime CreatedAt { get; set; }
    new DateTime ModifiedAt { get; set; }
}
