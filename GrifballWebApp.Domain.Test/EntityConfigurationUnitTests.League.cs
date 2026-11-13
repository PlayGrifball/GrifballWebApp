using GrifballWebApp.Domain.League;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Domain.Test;

/// <summary>The League configurations (teams, signups, bracket slots, series games, availability): see the class's main file.</summary>
public partial class EntityConfigurationUnitTests
{
    [Test]
    public void MatchBracketInfoConfiguration_EachSideASeedOrAMatchBefore()
    {
        var entity = Configured(new MatchBracketInfoConfiguration());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(TableOf(entity), Is.EqualTo(("Event", "MatchBracketInfo")));
            Assert.That(entity.IsTemporal(), Is.True);
            // One line each: a new line in a constraint's SQL is a pending model change on every build
            Assert.That(entity.FindCheckConstraint("CK_Event_MatchBracketInfo_RequireHomeSeedOrPreviousMatch")!.Sql, Is.EqualTo(
                "(HomeTeamSeedNumber IS NOT NULL AND HomeTeamPreviousMatchBracketInfoID IS NULL) OR (HomeTeamPreviousMatchBracketInfoID IS NOT NULL AND HomeTeamSeedNumber IS NULL)"));
            Assert.That(entity.FindCheckConstraint("CK_Event_MatchBracketInfo_RequireAwaySeedOrPreviousMatch")!.Sql, Is.EqualTo(
                "(AwayTeamSeedNumber IS NOT NULL AND AwayTeamPreviousMatchBracketInfoID IS NULL) OR (AwayTeamPreviousMatchBracketInfoID IS NOT NULL AND AwayTeamSeedNumber IS NULL)"));
            Assert.That(KeyOf(entity), Is.EqualTo(new List<string> { nameof(MatchBracketInfo.MatchBracketInfoID) }));
            Assert.That(IsIgnored(entity, nameof(MatchBracketInfo.IsGrandFinal)), Is.True, "Worked out from the bracket");
            var bySeasonMatch = ForeignKeyOf(entity, nameof(MatchBracketInfo.SeasonMatch));
            Assert.That((Names(bySeasonMatch.Properties), bySeasonMatch.IsUnique), Is.EqualTo((new List<string> { nameof(MatchBracketInfo.SeasonMatchID) }, true)),
                "A season match has one slot at most");
            var home = ForeignKeyOf(entity, nameof(MatchBracketInfo.HomeTeamPreviousMatchBracketInfo));
            var away = ForeignKeyOf(entity, nameof(MatchBracketInfo.AwayTeamPreviousMatchBracketInfo));
            Assert.That((Names(home.Properties), home.PrincipalToDependent!.Name),
                Is.EqualTo((new List<string> { nameof(MatchBracketInfo.HomeTeamPreviousMatchBracketInfoID) }, nameof(MatchBracketInfo.InverseHomeTeamPreviousMatchBracketInfo))));
            Assert.That((Names(away.Properties), away.PrincipalToDependent!.Name),
                Is.EqualTo((new List<string> { nameof(MatchBracketInfo.AwayTeamPreviousMatchBracketInfoID) }, nameof(MatchBracketInfo.InverseAwayTeamNextMatchBracketInfo))));
        }
    }

    [Test]
    public void SeriesGameConfiguration_OneGameAndUniqueIndexes()
    {
        var entity = Configured(new SeriesGameConfiguration());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(TableOf(entity), Is.EqualTo(("Event", "SeriesGames")));
            Assert.That(entity.IsTemporal(), Is.True);
            Assert.That(entity.FindCheckConstraint("CK_Event_SeriesGames_OneGame")!.Sql,
                Is.EqualTo("(MatchID IS NOT NULL AND RecordedGameID IS NULL) OR (MatchID IS NULL AND RecordedGameID IS NOT NULL)"));
            Assert.That(KeyOf(entity), Is.EqualTo(new List<string> { nameof(SeriesGame.SeriesGameID) }));
            var byMatch = ForeignKeyOf(entity, nameof(SeriesGame.Match));
            var byRecordedGame = ForeignKeyOf(entity, nameof(SeriesGame.RecordedGame));
            Assert.That((byMatch.DeleteBehavior, byMatch.IsRequired, byRecordedGame.DeleteBehavior, byRecordedGame.IsRequired),
                Is.EqualTo((DeleteBehavior.Cascade, false, DeleteBehavior.Cascade, false)), "One or the other, and it goes with its game");
            Assert.That(Names(ForeignKeyOf(entity, nameof(SeriesGame.SeasonMatch)).Properties), Is.EqualTo(new List<string> { nameof(SeriesGame.SeasonMatchID) }));
            Assert.That(IndexOn(entity, nameof(SeriesGame.MatchID)).IsUnique, Is.True, "A game is in one series at most");
            Assert.That(IndexOn(entity, nameof(SeriesGame.RecordedGameID)).IsUnique, Is.True);
            Assert.That(IndexOn(entity, nameof(SeriesGame.SeasonMatchID), nameof(SeriesGame.GameNumber)).IsUnique, Is.True,
                "A series has one game 1, one game 2, ...");
        }
    }

    [Test]
    public void TeamConfiguration_RecordCheckCaptainAndUniqueName()
    {
        var entity = Configured(new TeamConfiguration());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(TableOf(entity), Is.EqualTo(("Event", "Teams")));
            Assert.That(entity.IsTemporal(), Is.True);
            Assert.That(entity.FindCheckConstraint("CK_Event_Teams_RecordBothOrNeither")!.Sql,
                Is.EqualTo("(RecordedWins IS NULL AND RecordedLosses IS NULL) OR (RecordedWins IS NOT NULL AND RecordedLosses IS NOT NULL)"));
            Assert.That(KeyOf(entity), Is.EqualTo(new List<string> { nameof(Team.TeamID) }));
            Assert.That(Property(entity, nameof(Team.TeamName)).GetMaxLength(), Is.EqualTo(Team.TeamNameColumnLength));
            var byCaptain = ForeignKeyOf(entity, nameof(Team.Captain));
            Assert.That((Names(byCaptain.Properties), byCaptain.IsUnique, byCaptain.DeleteBehavior),
                Is.EqualTo((new List<string> { nameof(Team.CaptainID) }, true, DeleteBehavior.NoAction)), "A player captains one team at most");
            Assert.That(IndexOn(entity, nameof(Team.TeamName), nameof(Team.SeasonID)).IsUnique, Is.True, "A team name is unique in its season");
            Assert.That((IsIgnored(entity, nameof(Team.Picks)), IsIgnored(entity, nameof(Team.IsRecordArchived))), Is.EqualTo((true, true)),
                "Worked out from the roster and the record");
        }
    }

    [Test]
    public void SeasonSignupConfiguration_ChecksAndOneSignupAUserASeason()
    {
        var entity = Configured(new SeasonSignupConfiguration());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(TableOf(entity), Is.EqualTo(("Event", "SeasonSignups")));
            Assert.That(entity.IsTemporal(), Is.True);
            Assert.That(entity.FindCheckConstraint("CK_Event_SeasonSignups_TeamNameTrimmed")!.Sql,
                Is.EqualTo("TeamName IS NULL OR (TeamName <> '' AND TeamName = LTRIM(RTRIM(TeamName)))"));
            Assert.That(entity.FindCheckConstraint("CK_Event_SeasonSignups_AssistanceOnlyForCaptains")!.Sql,
                Is.EqualTo("CAST(RequiresAssistanceDrafting AS INT) = 0 OR CAST(WillCaptain AS INT) = 1"));
            Assert.That(KeyOf(entity), Is.EqualTo(new List<string> { nameof(SeasonSignup.SeasonSignupID) }));
            Assert.That(IndexOn(entity, nameof(SeasonSignup.UserID), nameof(SeasonSignup.SeasonID)).IsUnique, Is.True, "One signup a user a season");
            Assert.That(Names(ForeignKeyOf(entity, nameof(SeasonSignup.Season)).Properties), Is.EqualTo(new List<string> { nameof(SeasonSignup.SeasonID) }));
            Assert.That(Names(ForeignKeyOf(entity, nameof(SeasonSignup.User)).Properties), Is.EqualTo(new List<string> { nameof(SeasonSignup.UserID) }));
        }
    }

    [Test]
    public void VodTeamAndTeamPlayerConfigurations_KeysAndDeletes()
    {
        var vodTeam = Configured(new VodTeamConfiguration());
        var teamPlayer = Configured(new TeamPlayerConfiguration());

        using (Assert.EnterMultipleScope())
        {
            Assert.That((TableOf(vodTeam), TableOf(teamPlayer)), Is.EqualTo((("Event", "VodTeams"), ("Event", "TeamPlayers"))));
            Assert.That((vodTeam.IsTemporal(), teamPlayer.IsTemporal()), Is.EqualTo((true, true)));
            Assert.That(KeyOf(vodTeam), Is.EqualTo(new List<string> { nameof(VodTeam.VodID), nameof(VodTeam.TeamID) }));
            Assert.That((ForeignKeyOf(vodTeam, nameof(VodTeam.Vod)).DeleteBehavior, ForeignKeyOf(vodTeam, nameof(VodTeam.Team)).DeleteBehavior),
                Is.EqualTo((DeleteBehavior.ClientCascade, DeleteBehavior.Cascade)), "Only one path cascades in the database");
            Assert.That(KeyOf(teamPlayer), Is.EqualTo(new List<string> { nameof(TeamPlayer.TeamPlayerID) }));
            Assert.That(IndexOn(teamPlayer, nameof(TeamPlayer.UserID), nameof(TeamPlayer.TeamID)).IsUnique, Is.True, "On a team once");
            Assert.That(Names(ForeignKeyOf(teamPlayer, nameof(TeamPlayer.Team)).Properties), Is.EqualTo(new List<string> { nameof(TeamPlayer.TeamID) }));
            Assert.That(Names(ForeignKeyOf(teamPlayer, nameof(TeamPlayer.User)).Properties), Is.EqualTo(new List<string> { nameof(TeamPlayer.UserID) }));
        }
    }

    [Test]
    public void AvailabilityConfigurations_KeyedByOwnerAndOption()
    {
        var season = Configured(new SeasonAvailabilityConfiguration());
        var signup = Configured(new SignupAvailabilityConfiguration());
        var team = Configured(new TeamAvailabilityConfiguration());

        using (Assert.EnterMultipleScope())
        {
            Assert.That((TableOf(season), TableOf(signup), TableOf(team)),
                Is.EqualTo((("Event", "SeasonAvailability"), ("Event", "SignupAvailability"), ("Event", "TeamAvailability"))));
            Assert.That((season.IsTemporal(), signup.IsTemporal(), team.IsTemporal()), Is.EqualTo((true, true, true)));
            Assert.That(KeyOf(season), Is.EqualTo(new List<string> { nameof(SeasonAvailability.SeasonID), nameof(SeasonAvailability.AvailabilityOptionID) }));
            Assert.That(KeyOf(signup), Is.EqualTo(new List<string> { nameof(SignupAvailability.SeasonSignupID), nameof(SignupAvailability.AvailabilityOptionID) }));
            Assert.That(KeyOf(team), Is.EqualTo(new List<string> { nameof(TeamAvailability.TeamID), nameof(TeamAvailability.AvailabilityOptionID) }));
            Assert.That(Names(ForeignKeyOf(season, nameof(SeasonAvailability.AvailabilityOption)).Properties),
                Is.EqualTo(new List<string> { nameof(SeasonAvailability.AvailabilityOptionID) }));
            Assert.That(ForeignKeyOf(signup, nameof(SignupAvailability.AvailabilityOption)).PrincipalToDependent, Is.Null,
                "An option doesn't list the signups that chose it");
            Assert.That(Names(ForeignKeyOf(signup, nameof(SignupAvailability.SeasonSignup)).Properties),
                Is.EqualTo(new List<string> { nameof(SignupAvailability.SeasonSignupID) }));
            Assert.That(Names(ForeignKeyOf(team, nameof(TeamAvailability.Team)).Properties), Is.EqualTo(new List<string> { nameof(TeamAvailability.TeamID) }));
        }
    }
}
