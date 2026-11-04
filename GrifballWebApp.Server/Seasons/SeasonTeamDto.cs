namespace GrifballWebApp.Server.Seasons;

public class SeasonTeamDto
{
    public int TeamID { get; set; }
    public string TeamName { get; set; } = "";
    public int Wins { get; set; }
    public int Losses { get; set; }
    public int TBD { get; set; }
    public bool RecordFromSource { get; set; }
    public List<string> Titles { get; set; } = [];
    public int? DraftCaptainOrder { get; set; }
    public List<SeasonTeamMemberDto> Players { get; set; } = [];
}

public class SeasonTeamMemberDto
{
    public int TeamPlayerID { get; set; }
    public int UserID { get; set; }
    public string Gamertag { get; set; } = "";
    public bool IsCaptain { get; set; }
    public int? DraftCaptainOrder { get; set; }
    public int? DraftRound { get; set; }
    public int? DraftPick { get; set; }
    public string? Region { get; set; }
}
