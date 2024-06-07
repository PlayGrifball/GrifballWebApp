namespace GrifballWebApp.Server.Grades;

public class TotalDto
{
    public long XboxUserID { get; set; }
    public string Gamertag { get; set; }
    public int TotalGoals { get; set; }
    public int TotalKDSpread { get; set; }
    public int TotalPunches { get; set; }
    public int TotalSprees { get; set; }
    public int TotalDamageDealt { get; set; }
    public int TotalDamageTaken { get; set; }
    public int TotalMultiKills { get; set; }
    public int TotalXFactor { get; set; }
    public int TotalKills { get; set; }
    public double TotalGameTime { get; set; }
}

public class PerMinuteDto
{
    public required long XboxUserID { get; set; }
    public required string Gamertag { get; set; }
    public required double GoalsPM { get; set; }
    public required double KDSpreadPM { get; set; }
    public required double PunchesPM { get; set; }
    public required double SpreesPM { get; set; }
    public required double DamageDealtPM { get; set; }
    public required double DamageTakenPM { get; set; }
    public required double MultiKillsPM { get; set; }
    public required double XFactorPM { get; set; }
    public required double KillsPM { get; set; }
}

public class LetterDto
{
    public required long XboxUserID { get; set; }
    public required string Gamertag { get; set; }
    public required string Goals { get; set; }
    public required string KDSpread { get; set; }
    public required string Punches { get; set; }
    public required string Sprees { get; set; }
    public required string DamageDealt { get; set; }
    public required string DamageTaken { get; set; }
    public required string MultiKills { get; set; }
    public required string XFactor { get; set; }
    public required string Kills { get; set; }
    public required double GradeAvgMath { get; set; }
    public string GradeAvg { get; set; }
}

public class GradesDto
{
    public required List<TotalDto> Totals { get; set; }
    public required List<PerMinuteDto> PerMinutes { get; set; }
    public required List<LetterDto> Letters { get; set; }
}
