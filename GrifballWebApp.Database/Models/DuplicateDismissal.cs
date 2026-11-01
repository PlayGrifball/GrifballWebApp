namespace GrifballWebApp.Database.Models;

/// <summary>
/// A "possible duplicate" pair a sysadmin dismissed on the claims page: it isn't suggested again. The pair is stored
/// lower user ID first. A log of decisions: no row history and no foreign keys (either user may be merged away later).
/// </summary>
public class DuplicateDismissal
{
    public int LowUserID { get; set; }
    public int HighUserID { get; set; }
    public DateTime DismissedAt { get; set; }
    public int? DismissedByUserID { get; set; }
}
