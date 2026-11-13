using GrifballWebApp.Domain.Halo;
using GrifballWebApp.Domain.LateLeague;
using GrifballWebApp.Domain.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace GrifballWebApp.Domain.Test;

/// <summary>The Late League (matchmaking), Halo (Waypoint matches and medals) and Shared reference-data configurations:
/// see the class's main file.</summary>
public partial class EntityConfigurationUnitTests
{
    [Test]
    public void MatchmakingSeasonConfiguration_ChecksLengthsAndGameVersion()
    {
        var entity = Configured(new MatchmakingSeasonConfiguration());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(TableOf(entity), Is.EqualTo(("Matchmaking", "MatchmakingSeasons")));
            Assert.That(entity.IsTemporal(), Is.True);
            Assert.That(entity.FindCheckConstraint("CK_Matchmaking_MatchmakingSeasons_DatesInOrder")!.Sql, Is.EqualTo("StartsAt <= EndsAt"));
            Assert.That(entity.FindCheckConstraint("CK_Matchmaking_MatchmakingSeasons_SourceUrlOnlyImported")!.Sql,
                Is.EqualTo("HistoricalSourceUrl IS NULL OR HistoricalSourceID IS NOT NULL"));
            Assert.That(KeyOf(entity), Is.EqualTo(new List<string> { nameof(MatchmakingSeason.MatchmakingSeasonID) }));
            Assert.That(IsIgnored(entity, nameof(MatchmakingSeason.IsImported)), Is.True);
            var name = Property(entity, nameof(MatchmakingSeason.Name));
            Assert.That((name.IsNullable, name.GetMaxLength()), Is.EqualTo((false, 100)));
            Assert.That(Property(entity, nameof(MatchmakingSeason.HistoricalSourceID)).GetMaxLength(), Is.EqualTo(200));
            Assert.That(Property(entity, nameof(MatchmakingSeason.HistoricalSourceUrl)).GetMaxLength(), Is.EqualTo(500));
            Assert.That(IndexOn(entity, nameof(MatchmakingSeason.HistoricalSourceID)).IsUnique, Is.True, "The import upserts by it");
            var byGameVersion = ForeignKeyOf(entity, nameof(MatchmakingSeason.GameVersion));
            Assert.That((byGameVersion.DeleteBehavior, byGameVersion.PrincipalToDependent), Is.EqualTo((DeleteBehavior.Restrict, (IReadOnlyNavigation?)null)));
        }
    }

    [Test]
    public void MatchedVoteConfigurations_KeysAndRestrictedPlayers()
    {
        var kick = Configured(new MatchedKickVoteConfiguration());
        var winner = Configured(new MatchedWinnerVoteConfiguration());

        using (Assert.EnterMultipleScope())
        {
            Assert.That((TableOf(kick), TableOf(winner)), Is.EqualTo((("Matchmaking", "MatchedKickVotes"), ("Matchmaking", "MatchedWinnerVotes"))));
            Assert.That((kick.IsTemporal(), winner.IsTemporal()), Is.EqualTo((true, true)));
            Assert.That(KeyOf(kick), Is.EqualTo(new List<string>
            {
                nameof(MatchedKickVote.MatchId), nameof(MatchedKickVote.VoterMatchedPlayerId), nameof(MatchedKickVote.KickMatchedPlayerId),
            }), "A player may vote to kick several, each once");
            Assert.That(KeyOf(winner), Is.EqualTo(new List<string> { nameof(MatchedWinnerVote.MatchId), nameof(MatchedWinnerVote.MatchedPlayerId) }),
                "One winner vote a player a match");
            Assert.That((ForeignKeyOf(kick, nameof(MatchedKickVote.VoterMatchedPlayer)).DeleteBehavior, ForeignKeyOf(kick, nameof(MatchedKickVote.KickMatchedPlayer)).DeleteBehavior),
                Is.EqualTo((DeleteBehavior.Restrict, DeleteBehavior.Restrict)));
            Assert.That(Names(ForeignKeyOf(kick, nameof(MatchedKickVote.MatchedMatch)).Properties), Is.EqualTo(new List<string> { nameof(MatchedKickVote.MatchId) }));
            Assert.That(Names(ForeignKeyOf(winner, nameof(MatchedWinnerVote.MatchedPlayer)).Properties),
                Is.EqualTo(new List<string> { nameof(MatchedWinnerVote.MatchedPlayerId) }));
        }
    }

    [Test]
    public void MatchedTeamPlayerAndQueueConfigurations_KeysAndSides()
    {
        var team = Configured<MatchedTeam>(new MatchedTeamConfiguration());
        var player = Configured(new MatchedPlayerConfiguration());
        var queued = Configured(new QueuedPlayerConfiguration());

        using (Assert.EnterMultipleScope())
        {
            Assert.That((TableOf(team), TableOf(player), TableOf(queued)),
                Is.EqualTo((("Matchmaking", "MatchedTeams"), ("Matchmaking", "MatchedPlayers"), ("Matchmaking", "QueuedPlayers"))));
            Assert.That((team.IsTemporal(), player.IsTemporal(), queued.IsTemporal()), Is.EqualTo((true, true, true)));
            Assert.That(KeyOf(team), Is.EqualTo(new List<string> { nameof(MatchedTeam.MatchedTeamId) }));
            var home = ForeignKeyOf(team, MatchedTeam.HomeMatchedMatchNavigation);
            var away = ForeignKeyOf(team, MatchedTeam.AwayMatchedMatchNavigation);
            Assert.That((home.DeclaringEntityType.ClrType, Names(home.Properties), home.IsUnique),
                Is.EqualTo((typeof(MatchedMatch), new List<string> { nameof(MatchedMatch.HomeTeamId) }, true)), "A team is one match's home side");
            Assert.That((away.DeclaringEntityType.ClrType, Names(away.Properties), away.IsUnique),
                Is.EqualTo((typeof(MatchedMatch), new List<string> { nameof(MatchedMatch.AwayTeamId) }, true)), "Or one match's away side");
            Assert.That(KeyOf(player), Is.EqualTo(new List<string> { nameof(MatchedPlayer.Id) }));
            Assert.That(Property(player, nameof(MatchedPlayer.UserID)).ValueGenerated, Is.EqualTo(ValueGenerated.Never));
            Assert.That(Names(ForeignKeyOf(player, nameof(MatchedPlayer.MatchedTeam)).Properties), Is.EqualTo(new List<string> { nameof(MatchedPlayer.MatchedTeamID) }));
            Assert.That(KeyOf(queued), Is.EqualTo(new List<string> { nameof(QueuedPlayer.UserID) }), "A user is in the queue once");
            Assert.That(Property(queued, nameof(QueuedPlayer.UserID)).ValueGenerated, Is.EqualTo(ValueGenerated.Never));
        }
    }

    [Test]
    public void MatchmakingPlayerStatConfiguration_TotalsAndOneLineAUserASeason()
    {
        var entity = Configured(new MatchmakingPlayerStatConfiguration());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(TableOf(entity), Is.EqualTo(("Matchmaking", "PlayerStats")));
            Assert.That(entity.IsTemporal(), Is.True);
            Assert.That(KeyOf(entity), Is.EqualTo(new List<string> { nameof(MatchmakingPlayerStat.MatchmakingPlayerStatID) }));
            // PlayerSeasonStatConfiguration.ConfigureTotals, shared with the league's stats
            var gamertag = Property(entity, nameof(MatchmakingPlayerStat.Gamertag));
            Assert.That((gamertag.IsNullable, gamertag.GetMaxLength()), Is.EqualTo((false, 50)));
            Assert.That(IsIgnored(entity, nameof(MatchmakingPlayerStat.Totals)), Is.True, "Computed from the columns");
            Assert.That(ForeignKeyOf(entity, nameof(MatchmakingPlayerStat.MatchmakingSeason)).DeleteBehavior, Is.EqualTo(DeleteBehavior.Cascade));
            Assert.That(IndexOn(entity, nameof(MatchmakingPlayerStat.MatchmakingSeasonID), nameof(MatchmakingPlayerStat.UserID)).IsUnique, Is.True);
            Assert.That(IndexOn(entity, nameof(MatchmakingPlayerStat.UserID)).IsUnique, Is.False);
        }
    }

    [Test]
    public void MatchConfiguration_ChecksIgnoredAndHaloIds()
    {
        var entity = Configured(new MatchConfiguration());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(TableOf(entity), Is.EqualTo(("Infinite", "Matches")));
            Assert.That(entity.IsTemporal(), Is.True);
            Assert.That(entity.FindCheckConstraint("CK_Infinite_Matches_GameVariantWhole")!.Sql, Is.EqualTo(MatchConfiguration.GameVariantWhole));
            Assert.That(entity.FindCheckConstraint("CK_Infinite_Matches_KnownScoreLayout")!.Sql, Is.EqualTo("ScoreLayout IS NULL OR ScoreLayout IN (1, 2, 3, 4, 5, 6)"),
                "Every CustomScoreLayout, and nothing else");
            Assert.That(KeyOf(entity), Is.EqualTo(new List<string> { nameof(Match.MatchID) }));
            Assert.That(Property(entity, nameof(Match.MatchID)).ValueGenerated, Is.EqualTo(ValueGenerated.Never), "Halo's own match ID");
            Assert.That(new List<string> { nameof(Match.Participants), nameof(Match.HasGameVariant), nameof(Match.IsDecoded), nameof(Match.HasHalosPoints) }
                .Select(name => IsIgnored(entity, name)), Is.All.True, "Worked out from its fields and teams");
            var matched = ForeignKeyOf(entity, nameof(Match.MatchedMatch));
            Assert.That((matched.DeclaringEntityType.ClrType, Names(matched.Properties), matched.IsUnique, matched.IsRequired),
                Is.EqualTo((typeof(MatchedMatch), new List<string> { nameof(MatchedMatch.MatchID) }, true, false)), "At most one matchmaking game's result");
        }
    }

    [Test]
    public void MatchTeamAndParticipantConfigurations_CompositeKeys()
    {
        var team = Configured(new MatchTeamConfiguration());
        var participant = Configured(new MatchParticipantConfiguration());

        using (Assert.EnterMultipleScope())
        {
            Assert.That((TableOf(team), TableOf(participant)), Is.EqualTo((("Infinite", "MatchTeams"), ("Infinite", "MatchParticipants"))));
            Assert.That((team.IsTemporal(), participant.IsTemporal()), Is.EqualTo((true, true)));
            Assert.That(team.FindCheckConstraint("CK_Infinite_MatchTeams_KnownOutcome")!.Sql, Is.EqualTo("Outcome IN (1, 2, 3, 4)"));
            Assert.That(KeyOf(team), Is.EqualTo(new List<string> { nameof(MatchTeam.MatchID), nameof(MatchTeam.TeamID) }));
            Assert.That(KeyOf(participant), Is.EqualTo(new List<string> { nameof(MatchParticipant.MatchID), nameof(MatchParticipant.XboxUserID) }),
                "A player is in a match once");
            Assert.That(Names(ForeignKeyOf(participant, nameof(MatchParticipant.MatchTeam)).Properties),
                Is.EqualTo(new List<string> { nameof(MatchParticipant.MatchID), nameof(MatchParticipant.TeamID) }), "Its team in the same match");
            Assert.That(Names(ForeignKeyOf(participant, nameof(MatchParticipant.XboxUser)).Properties),
                Is.EqualTo(new List<string> { nameof(MatchParticipant.XboxUserID) }));
        }
    }

    [Test]
    public void MedalConfigurations_HaloIdsAndKeys()
    {
        var medal = Configured(new MedalConfiguration());
        var earned = Configured(new MedalEarnedConfiguration());
        var difficulty = Configured(new MedalDifficultyConfiguration());
        var type = Configured(new MedalTypeConfiguration());

        using (Assert.EnterMultipleScope())
        {
            Assert.That((TableOf(medal), TableOf(earned), TableOf(difficulty), TableOf(type)), Is.EqualTo((
                ("Infinite", "Medals"), ("Infinite", "MedalEarned"), ("Infinite", "MedalDifficulties"), ("Infinite", "MedalTypes"))));
            Assert.That((medal.IsTemporal(), earned.IsTemporal(), difficulty.IsTemporal(), type.IsTemporal()), Is.EqualTo((true, true, true, true)));
            Assert.That((Property(medal, nameof(Medal.MedalID)).ValueGenerated, Property(difficulty, nameof(MedalDifficulty.MedalDifficultyID)).ValueGenerated,
                Property(type, nameof(MedalType.MedalTypeID)).ValueGenerated),
                Is.EqualTo((ValueGenerated.Never, ValueGenerated.Never, ValueGenerated.Never)), "Halo's own IDs");
            Assert.That(Names(ForeignKeyOf(medal, nameof(Medal.MedalDifficulty)).Properties), Is.EqualTo(new List<string> { nameof(Medal.MedalDifficultyID) }));
            Assert.That(Names(ForeignKeyOf(medal, nameof(Medal.MedalType)).Properties), Is.EqualTo(new List<string> { nameof(Medal.MedalTypeID) }));
            Assert.That(KeyOf(earned), Is.EqualTo(new List<string> { nameof(MedalEarned.MedalID), nameof(MedalEarned.MatchID), nameof(MedalEarned.XboxUserID) }),
                "A medal a player a match");
            Assert.That(Names(ForeignKeyOf(earned, nameof(MedalEarned.MatchParticipant)).Properties),
                Is.EqualTo(new List<string> { nameof(MedalEarned.MatchID), nameof(MedalEarned.XboxUserID) }));
        }
    }

    [Test]
    public void GameVersionConfiguration_ChecksIndexesAndSeeds()
    {
        var entity = Configured(new GameVersionConfiguration());
        var seeded = entity.GetSeedData().ToList();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(TableOf(entity), Is.EqualTo(("Other", "GameVersions")));
            Assert.That(entity.IsTemporal(), Is.True);
            Assert.That(entity.FindCheckConstraint("CK_Other_GameVersions_NameAndSlugNotBlank")!.Sql,
                Is.EqualTo("LTRIM(RTRIM(GameVersionName)) <> '' AND LTRIM(RTRIM(Slug)) <> ''"));
            Assert.That(KeyOf(entity), Is.EqualTo(new List<string> { nameof(GameVersion.GameVesionID) }));
            Assert.That(IndexOn(entity, nameof(GameVersion.GameVersionName)).IsUnique, Is.True);
            Assert.That(IndexOn(entity, nameof(GameVersion.Slug)).IsUnique, Is.True);
            var name = Property(entity, nameof(GameVersion.GameVersionName));
            Assert.That((name.IsNullable, name.GetMaxLength()), Is.EqualTo((false, 30)));
            Assert.That(seeded.Select(r => r[nameof(GameVersion.GameVesionID)]), Is.EqualTo(new List<int> { 1, 2, 3, 4, 5, 6 }));
            Assert.That(seeded.Select(r => r[nameof(GameVersion.GameVersionName)]),
                Is.EqualTo(new List<string> { "Halo 3", "Halo Reach", "Halo Reach Dash", "Halo 4", "Halo 5", "Halo Infinite" }));
            Assert.That(seeded.Select(r => r[nameof(GameVersion.Slug)]),
                Is.EqualTo(new List<string> { "halo3", "reach", "reach-dash", "halo4", "halo5", "infinite" }));
        }
    }

    [Test]
    public void RegionConfiguration_CheckIndexAndSeeds()
    {
        var entity = Configured(new RegionConfiguration());
        var seeded = entity.GetSeedData().ToList();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(TableOf(entity), Is.EqualTo(("Other", "Regions")));
            Assert.That(entity.IsTemporal(), Is.True);
            Assert.That(entity.FindCheckConstraint("CK_Other_Regions_NameNotBlank")!.Sql, Is.EqualTo("LTRIM(RTRIM(RegionName)) <> ''"));
            var name = Property(entity, nameof(Region.RegionName));
            Assert.That((name.IsNullable, name.GetMaxLength()), Is.EqualTo((false, 50)));
            Assert.That(IndexOn(entity, nameof(Region.RegionName)).IsUnique, Is.True);
            Assert.That(seeded.Select(r => r[nameof(Region.RegionID)]), Is.EqualTo(new List<int> { 1, 2, 3, 4, 5, 6, 7 }));
            Assert.That(seeded.Select(r => r[nameof(Region.RegionName)]), Is.EqualTo(new List<string>
            {
                "West North America", "Central North America", "East North America", "North Europe", "South Europe", "Australia", "Europe",
            }), "Europe last: the league sheets' players lists only say \"Europe\"");
        }
    }

    [Test]
    public void LeagueConfiguration_CheckIndexesAndSeeds()
    {
        var entity = Configured(new LeagueConfiguration());
        var seeded = entity.GetSeedData().ToList();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(TableOf(entity), Is.EqualTo(("Event", "Leagues")));
            Assert.That(entity.IsTemporal(), Is.True);
            Assert.That(entity.FindCheckConstraint("CK_Event_Leagues_NameAndSlugNotBlank")!.Sql,
                Is.EqualTo("LTRIM(RTRIM(LeagueName)) <> '' AND LTRIM(RTRIM(Slug)) <> ''"));
            Assert.That(KeyOf(entity), Is.EqualTo(new List<string> { "LeagueID" }));
            Assert.That((IndexOn(entity, "LeagueName").IsUnique, IndexOn(entity, "Slug").IsUnique), Is.EqualTo((true, true)));
            Assert.That((Property(entity, "LeagueName").GetMaxLength(), Property(entity, "Slug").GetMaxLength()), Is.EqualTo((50, 50)));
            Assert.That(seeded.Select(r => r["LeagueID"]), Is.EqualTo(new List<int> { 1, 3, 4, 5 }), "League 2 is long gone: its ID isn't reused");
            Assert.That(seeded.Select(r => r["LeagueName"]), Is.EqualTo(new List<string> { "AGLA", "GWL", "IGL", "Community/Side" }));
            Assert.That(seeded.Select(r => r["Slug"]), Is.EqualTo(new List<string> { "agla", "gwl", "igl", "side" }));
        }
    }

    [Test]
    public void MessageRankAndAvailabilityOptionConfigurations_KeysChecksAndIndexes()
    {
        var discord = Configured(new DiscordMessageConfiguration());
        var outbox = Configured(new OutboxMessageConfiguration());
        var rank = Configured(new RankConfiguration());
        var option = Configured(new AvailabilityOptionConfiguration());

        using (Assert.EnterMultipleScope())
        {
            Assert.That((TableOf(discord), TableOf(outbox), TableOf(rank), TableOf(option)), Is.EqualTo((
                ("Discord", "Messages"), ("Discord", "Outbox"), ("Matchmaking", "Ranks"), ("Event", "AvailabilityOptions"))));
            Assert.That((discord.IsTemporal(), outbox.IsTemporal(), rank.IsTemporal(), option.IsTemporal()), Is.EqualTo((true, false, true, true)),
                "The outbox is a queue, not history");
            Assert.That(Property(discord, nameof(DiscordMessage.Id)).ValueGenerated, Is.EqualTo(ValueGenerated.Never), "Discord's own message ID");
            Assert.That((ForeignKeyOf(discord, nameof(DiscordMessage.FromDiscordUser)).DeleteBehavior, ForeignKeyOf(discord, nameof(DiscordMessage.ToDiscordUser)).DeleteBehavior),
                Is.EqualTo((DeleteBehavior.Restrict, DeleteBehavior.Restrict)));
            Assert.That((Property(outbox, nameof(OutboxMessage.Kind)).GetMaxLength(), Property(outbox, nameof(OutboxMessage.GroupKey)).GetMaxLength(),
                Property(outbox, nameof(OutboxMessage.LastError)).GetMaxLength()), Is.EqualTo((100, 100, 2000)));
            Assert.That(IndexOn(outbox, nameof(OutboxMessage.Status), nameof(OutboxMessage.NextAttemptAt)).IsUnique, Is.False, "The sender's next due message");
            Assert.That(IndexOn(outbox, nameof(OutboxMessage.GroupKey), nameof(OutboxMessage.Sequence)).IsUnique, Is.True, "One message a place in its group");
            Assert.That(rank.FindCheckConstraint("CK_Matchmaking_Ranks_ThresholdNotNegative")!.Sql, Is.EqualTo("MmrThreshold >= 0"));
            Assert.That(KeyOf(rank), Is.EqualTo(new List<string> { nameof(Rank.Id) }));
            Assert.That(KeyOf(option), Is.EqualTo(new List<string> { nameof(AvailabilityOption.AvailabilityOptionID) }));
            Assert.That(IndexOn(option, nameof(AvailabilityOption.DayOfWeek), nameof(AvailabilityOption.Time)).IsUnique, Is.True, "One option a time of a day");
        }
    }
}
