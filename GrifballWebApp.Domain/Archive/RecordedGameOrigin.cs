namespace GrifballWebApp.Domain.Archive;

/// <summary>Where a <see cref="RecordedGame"/> came from.</summary>
public enum RecordedGameOrigin
{
    /// <summary>The historical import, from the archive's carnage reports and stats sheets (every game before manual
    /// match entry). Re-imports find it by its <see cref="RecordedGame.SourceKey"/>.</summary>
    HistoricalImport = 1,
    /// <summary>Entered by hand by a commissioner on the match page (manual match entry): the import never touches it.</summary>
    Manual = 2,
}
