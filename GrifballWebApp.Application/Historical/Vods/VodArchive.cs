namespace GrifballWebApp.Application.Historical.Vods;

/// <summary>
/// The channel as the historical archive captured it (raw/youtube-grifball-channel.html and compiled/youtube-vods.json,
/// 2026-10-30), so the backfill has these VODs even once the live channel page no longer lists them. The dates are the
/// capture's "2w ago": approximate until the feed or the Data API gives the exact one.
/// </summary>
public static class VodArchive
{
    public sealed record Video(string VideoID, string Title, DateTime PublishedAt, int DurationSeconds);

    public const string ChannelID = "UCTVJapRl-GGwLCfioTb9N8w";

    /// <summary>The capture's videos, with their titles. Its Shorts are left out (clips, not VODs).</summary>
    public static IReadOnlyList<Video> Videos { get; } =
    [
        new("Hb2JmxJhQd8", "AGLA Summer Minor Championship | BO7 | Low IQ vs Reach for the Balls | Halo Infinite Grifball", new DateTime(2026, 10, 16, 0, 0, 0, DateTimeKind.Utc), 6601),
        new("NnBy9EZai9Q", "BLITZ LEAGUE II | Halo Infinite Grifball", new DateTime(2026, 10, 16, 0, 0, 0, DateTimeKind.Utc), 12793),
        new("RgOQ5JqPhB0", "AGLA Summer Minor PLAYOFFS | 3rd Place Match | Uncle Scorefester (2) vs Duck Dynasty (3)", new DateTime(2026, 10, 9, 0, 0, 0, DateTimeKind.Utc), 5206),
        new("dq3DOSH23z4", "Blitz League I", new DateTime(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc), 14597),
        new("JzI-tOK6tTY", "Fireside Chat with the Commish and Season Awards", new DateTime(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc), 3782),
        new("HhxZZ_MqDfA", "PlayGrifball: Intramural Customs (Part 2)", new DateTime(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc), 11155),
        new("HOt-y6f_ZEU", "PlayGrifball: Intramural Customs (Part 1)", new DateTime(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc), 7409),
        new("Do32R4ltee8", "AGLA Playoffs: Zero Logic (3) vs Spank's Life Coaches (6)", new DateTime(2026, 5, 30, 0, 0, 0, DateTimeKind.Utc), 7049),
        new("CJgRG--cbX0", "AGLA Semifinal Playoffs: Like CLockwork (2) vs Spank's Life Coaches (6)", new DateTime(2026, 5, 30, 0, 0, 0, DateTimeKind.Utc), 11625),
        new("Q7f-9iipLqI", "DVEO vs. Scullen cx Bo3 Series (Observer POV)", new DateTime(2026, 5, 30, 0, 0, 0, DateTimeKind.Utc), 2076),
        new("k4E0cZlPvnA", "yLuxe vs Deathcrawller Bo3 Series (yLuxe POV)", new DateTime(2026, 5, 30, 0, 0, 0, DateTimeKind.Utc), 3981),
        new("A2EXthtZi2Y", "AGLA: Zero Logic (Rebs) 0:0 Like Clockwork (YLuxe)", new DateTime(2026, 5, 30, 0, 0, 0, DateTimeKind.Utc), 5805),
    ];

    /// <summary>Every video ID youtube-vods.json lists (the capture's videos, its Shorts, and a few it found without a
    /// title). Only the Data API can say what the untitled ones are; Shorts are dropped by their length.</summary>
    public static IReadOnlyList<string> VideoIDs { get; } =
    [
        "Hb2JmxJhQd8", "NnBy9EZai9Q", "RgOQ5JqPhB0", "dq3DOSH23z4", "JzI-tOK6tTY", "HhxZZ_MqDfA", "HOt-y6f_ZEU", "Do32R4ltee8",
        "CJgRG--cbX0", "Q7f-9iipLqI", "k4E0cZlPvnA", "A2EXthtZi2Y", "dXpxOmv77lw", "jj_H_zJo6p0", "ictoYj3aO14", "53H49SMznLw",
        "yY9aPAQPAsE", "tRjCfdELJmM", "dEODtTNaj8k", "Df46t8cc3LM", "B6mbh8Hzs_w", "t6C1b_S_zNg", "LlGDs_Kg_vo", "VDRUXCnTDVI",
        "UdlgnIoiPL8", "OYwXvAO4CqQ", "XHt7LE9-kuk", "lyKgd8ScYwY", "VkP2d5wsdew", "pNTmRujy9S0", "f3LWKWBAg9k", "m7dks86S3Fg",
        "8IjYNH7Vsso", "ICIXB6SAP3Y", "WCmDeWwzP88", "kANLC5iYi24", "XmzPxV-aJ5o", "RywRbHhNyXE", "Hnl37DNSxr8", "v327Rjqsij8",
    ];
}
