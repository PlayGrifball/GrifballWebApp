namespace GrifballWebApp.Database.Models;

/// <summary>
/// The shared base of the audited models: who made and last changed a row, and when. The columns are the audit
/// interceptor's alone, which sets them through <see cref="IAuditable"/> as it saves; nothing else writes them.
/// </summary>
public abstract class AuditableEntity : IAuditable
{
    public int? CreatedByID { get; private set; }
    public int? ModifiedByID { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime ModifiedAt { get; private set; }
    int? IAuditable.CreatedByID { get => CreatedByID; set => CreatedByID = value; }
    int? IAuditable.ModifiedByID { get => ModifiedByID; set => ModifiedByID = value; }
    DateTime IAuditable.CreatedAt { get => CreatedAt; set => CreatedAt = value; }
    DateTime IAuditable.ModifiedAt { get => ModifiedAt; set => ModifiedAt = value; }
}
