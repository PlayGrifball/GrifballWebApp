namespace GrifballWebApp.Domain.League;

/// <summary>One spreadsheet of the historical archive that feeds a season: its link and a name for it.</summary>
public sealed record SeasonSourceLink(string Url, string Label);
