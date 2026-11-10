namespace GrifballWebApp.Application.Discord;

/// <summary>The Discord bot's settings (the Discord section): its sign-in, its channels and the matchmaking queue's rules.</summary>
public class DiscordOptions
{
    public string ClientId { get; set; } = null!;
    public string ClientSecret { get; set; } = null!;
    public string Token { get; set; } = null!;
    public ulong DraftChannel { get; set; }
    public bool DisableGlobally { get; set; } = false;
    public ulong QueueChannel { get; set; }
    public ulong EventsChannel { get; set; }
    public ulong ReschedulesChannel { get; set; }
    public ulong LogChannel { get; set; }
    public int MatchPlayers { get; set; } = 8; // Default to 8 players per match
    public int KFactor { get; set; } = 32;
    public int WinThreshold { get; set; } = 3;
    public int BonusPerWin { get; set; } = 10;
    public int MaxBonus { get; set; } = 20;
    public int LossThreshold { get; set; } = 3;
    public int PenaltyPerLoss { get; set; } = 10;
    public int MaxPenalty { get; set; } = 20;
}
