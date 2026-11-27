namespace GrifballWebApp.Domain.Players;

/// <summary>
/// What an Xbox account has equipped in Halo Infinite, as the last check read it: its armor core and the equipped theme's
/// pieces (Halo's item paths, kept as given), its stance (the action pose) and its body. One row per account, which also
/// records the last check so an account is not asked about again too soon. A profile's own armor (an override) is kept
/// apart from this, so what the game says is always here.
/// </summary>
public class XboxUserArmor
{
    public long XboxUserID { get; set; }
    public DateTime CheckedAt { get; set; }
    public XboxUserArmorStatus Status { get; set; }
    /// <summary>Why the last check failed (Status Failed), to try again later.</summary>
    public string? Error { get; set; }
    /// <summary>When the equipped set last changed, as far as the checks saw.</summary>
    public DateTime? ChangedAt { get; set; }

    public string? CoreId { get; set; }
    public string? CorePath { get; set; }
    public string? ThemePath { get; set; }
    public string? CoatingPath { get; set; }
    public string? HelmetPath { get; set; }
    public string? VisorPath { get; set; }
    public string? GlovePath { get; set; }
    public string? HelmetAttachmentPath { get; set; }
    public string? ChestAttachmentPath { get; set; }
    public string? KneePadPath { get; set; }
    public string? LeftShoulderPadPath { get; set; }
    public string? RightShoulderPadPath { get; set; }
    public string? HipAttachmentPath { get; set; }
    public string? WristAttachmentPath { get; set; }
    public string? ArmorFxPath { get; set; }
    public string? MythicFxPath { get; set; }
    public string? ActionPosePath { get; set; }
    public string? StancePath { get; set; }
    public string? BodyType { get; set; }
    public string? LeftArm { get; set; }
    public string? RightArm { get; set; }
    public string? LeftLeg { get; set; }
    public string? RightLeg { get; set; }

    public virtual XboxUser XboxUser { get; set; } = null!;
}

public enum XboxUserArmorStatus
{
    /// <summary>The equipped set is stored.</summary>
    Found = 1,
    /// <summary>Halo has no customization for the account.</summary>
    NotFound = 2,
    /// <summary>The check failed (the API down), tried again sooner.</summary>
    Failed = 3,
}
