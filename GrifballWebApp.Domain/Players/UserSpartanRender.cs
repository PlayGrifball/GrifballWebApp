namespace GrifballWebApp.Domain.Players;

/// <summary>
/// Where the render of a user's Spartan stands, one row per user who asked for one: asked for, being rendered, or done with
/// its files (<see cref="UserSpartanFile"/>). Bookkeeping of the render job, so no history is kept.
/// </summary>
public class UserSpartanRender
{
    public int UserId { get; set; }
    public UserSpartanRenderState State { get; set; }
    /// <summary>When the render was last asked for (a choice made or changed, the equipped set changed, or the user asking).</summary>
    public DateTime RequestedAt { get; set; }
    /// <summary>When the files were last made; null until the first is.</summary>
    public DateTime? RenderedAt { get; set; }
    /// <summary>What the files show: a hash of the loadout sent to the render service.</summary>
    public string? LoadoutKey { get; set; }
    /// <summary>Why the last render failed (State Failed).</summary>
    public string? Error { get; set; }

    public virtual User User { get; set; } = null!;
    public virtual ICollection<UserSpartanFile> Files { get; set; } = new List<UserSpartanFile>();
}

public enum UserSpartanRenderState
{
    /// <summary>Waiting for the render job.</summary>
    Requested = 1,
    /// <summary>The render job has it with the render service.</summary>
    Rendering = 2,
    /// <summary>The files are stored.</summary>
    Done = 3,
    /// <summary>The last render failed; <see cref="UserSpartanRender.Error"/> says why. Asked for again when the user does.</summary>
    Failed = 4,
}

/// <summary>One file of a user's rendered Spartan: glb (one model with the neutral and stance poses as clips), stance-body, stance-head, neutral-body or neutral-head.</summary>
public class UserSpartanFile
{
    public const int MaxNameLength = 40;

    public int UserId { get; set; }
    public string Name { get; set; } = "";
    public string ContentType { get; set; } = "";
    public byte[] Content { get; set; } = [];

    public virtual UserSpartanRender Render { get; set; } = null!;
}
