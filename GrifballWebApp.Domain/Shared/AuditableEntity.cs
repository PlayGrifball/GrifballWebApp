namespace GrifballWebApp.Domain.Shared;

/// <summary>
/// The shared base of the audited models: who made and last changed a row, and when. The columns are the audit
/// interceptor's alone, which sets them through <see cref="IAuditStamp"/> as it saves; nothing else writes them.
/// </summary>
public abstract class AuditableEntity : IAuditStamp
{
    public int? CreatedByID { get; private set; }
    public int? ModifiedByID { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime ModifiedAt { get; private set; }
    int? IAuditStamp.CreatedByID { get => CreatedByID; set => CreatedByID = value; }
    int? IAuditStamp.ModifiedByID { get => ModifiedByID; set => ModifiedByID = value; }
    DateTime IAuditStamp.CreatedAt { get => CreatedAt; set => CreatedAt = value; }
    DateTime IAuditStamp.ModifiedAt { get => ModifiedAt; set => ModifiedAt = value; }
}
