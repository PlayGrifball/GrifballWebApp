namespace GrifballWebApp.Domain.Players;

/// <summary>
/// An image kept for an Xbox account, one row per account and <see cref="XboxUserImageKind"/>: its gamer picture or its Halo
/// Infinite emblem, as the bytes Xbox or Halo served (so pages never link out to them). The row also records the last check,
/// so an account with no image (or one whose check failed) isn't asked about again too soon.
/// </summary>
public class XboxUserImage
{
    /// <summary>The longest image kept; a larger one is refused (<see cref="XboxUserImageStatus.Failed"/>).</summary>
    public const int MaxContentBytes = 2 * 1024 * 1024;

    public long XboxUserID { get; set; }
    public XboxUserImageKind Kind { get; set; }
    public DateTime CheckedAt { get; set; }
    public XboxUserImageStatus Status { get; set; }
    /// <summary>What the image came from, as of the last fetch (the gamer picture's URL, the emblem's image path): a check that finds the
    /// same source keeps the bytes it has.</summary>
    public string? Source { get; set; }
    public string? ContentType { get; set; }
    public byte[]? Content { get; set; }
    /// <summary>Why the last check failed (Status Failed), to try again later.</summary>
    public string? Error { get; set; }

    public virtual XboxUser XboxUser { get; set; } = null!;
}

public enum XboxUserImageKind
{
    GamerPicture = 1,
    HaloEmblem = 2,
}

public enum XboxUserImageStatus
{
    /// <summary>The image is stored.</summary>
    Found = 1,
    /// <summary>Xbox or Halo has none for the account.</summary>
    NotFound = 2,
    /// <summary>The check failed (the API down, a bad image), tried again sooner.</summary>
    Failed = 3,
}
