using GrifballWebApp.Database.Models;

namespace GrifballWebApp.Test;

/// <summary>Seeding recorded games past their methods (<see cref="RecordedGame.Seed"/>), for tests.</summary>
internal static class RecordedGameTestExtensions
{
    /// <summary>The game with this key, as one loaded from the database would have it.</summary>
    public static RecordedGame WithID(this RecordedGame game, int recordedGameID)
    {
        game.RecordedGameID = recordedGameID;
        return game;
    }
}
