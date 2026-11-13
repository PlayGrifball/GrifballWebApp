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
            Assert.That((NullableOf(entity, nameof(MatchmakingSeason.Name)), MaxLengthOf(entity, nameof(MatchmakingSeason.Name))), Is.EqualTo(((bool?)false, (int?)100)));
            Assert.That(MaxLengthOf(entity, nameof(MatchmakingSeason.HistoricalSourceID)), Is.EqualTo(200));
            Assert.That(MaxLengthOf(entity, nameof(MatchmakingSeason.HistoricalSourceUrl)), Is.EqualTo(500));
            Assert.That(IndexIsUnique(entity, nameof(MatchmakingSeason.HistoricalSourceID)), Is.True, "The import upserts by it");
            var byGameVersion = ForeignKeyOf(entity, nameof(MatchmakingSeason.GameVersion));
            Assert.That((DeleteOf(byGameVersion), ColumnsOf(byGameVersion)),
                Is.EqualTo(((DeleteBehavior?)DeleteBehavior.Restrict, new List<string> { nameof(MatchmakingSeason.GameVersionID) })));
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
            var voter = ForeignKeyOf(kick, nameof(MatchedKickVote.VoterMatchedPlayer));
            var kicked = ForeignKeyOf(kick, nameof(MatchedKickVote.KickMatchedPlayer));
            Assert.That((DeleteOf(voter), DeleteOf(kicked)), Is.EqualTo(((DeleteBehavior?)DeleteBehavior.Restrict, (DeleteBehavior?)DeleteBehavior.Restrict)));
            Assert.That((ColumnsOf(voter), ColumnsOf(kicked)), Is.EqualTo((new List<string> { nameof(MatchedKickVote.VoterMatchedPlayerId) },
                new List<string> { nameof(MatchedKickVote.KickMatchedPlayerId) })), "Two links to the same table, each its own column");
            Assert.That(ColumnsOf(ForeignKeyOf(kick, nameof(MatchedKickVote.MatchedMatch))), Is.EqualTo(new List<string> { nameof(MatchedKickVote.MatchId) }));
            Assert.That(ColumnsOf(ForeignKeyOf(winner, nameof(MatchedWinnerVote.MatchedPlayer))),
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
            Assert.That((home.DeclaringEntityType.ClrType, ColumnsOf(home), home.IsUnique),
                Is.EqualTo((typeof(MatchedMatch), new List<string> { nameof(MatchedMatch.HomeTeamId) }, true)), "A team is one match's home side");
            Assert.That((away.DeclaringEntityType.ClrType, ColumnsOf(away), away.IsUnique),
                Is.EqualTo((typeof(MatchedMatch), new List<string> { nameof(MatchedMatch.AwayTeamId) }, true)), "Or one match's away side");
            Assert.That(KeyOf(player), Is.EqualTo(new List<string> { nameof(MatchedPlayer.Id) }));
            Assert.That(ValueGeneratedOf(player, nameof(MatchedPlayer.UserID)), Is.EqualTo(ValueGenerated.Never));
            Assert.That(ColumnsOf(ForeignKeyOf(player, nameof(MatchedPlayer.MatchedTeam))), Is.EqualTo(new List<string> { nameof(MatchedPlayer.MatchedTeamID) }));
            Assert.That(KeyOf(queued), Is.EqualTo(new List<string> { nameof(QueuedPlayer.UserID) }), "A user is in the queue once");
            Assert.That(ValueGeneratedOf(queued, nameof(QueuedPlayer.UserID)), Is.EqualTo(ValueGenerated.Never), "The user's ID, not a new one");
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
            Assert.That((NullableOf(entity, nameof(MatchmakingPlayerStat.Gamertag)), MaxLengthOf(entity, nameof(MatchmakingPlayerStat.Gamertag))),
                Is.EqualTo(((bool?)false, (int?)50)));
            Assert.That(IsIgnored(entity, nameof(MatchmakingPlayerStat.Totals)), Is.True, "Computed from the columns");
            Assert.That(DeleteOf(ForeignKeyOf(entity, nameof(MatchmakingPlayerStat.MatchmakingSeason))), Is.EqualTo(DeleteBehavior.Cascade));
            Assert.That(IndexIsUnique(entity, nameof(MatchmakingPlayerStat.MatchmakingSeasonID), nameof(MatchmakingPlayerStat.UserID)), Is.True);
            Assert.That(IndexIsUnique(entity, nameof(MatchmakingPlayerStat.UserID)), Is.False);
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
            Assert.That(ValueGeneratedOf(entity, nameof(Match.MatchID)), Is.EqualTo(ValueGenerated.Never), "Halo's own match ID");
            Assert.That(new List<string> { nameof(Match.Participants), nameof(Match.HasGameVariant), nameof(Match.IsDecoded), nameof(Match.HasHalosPoints) }
                .Select(name => IsIgnored(entity, name)), Is.All.True, "Worked out from its fields and teams");
            var matched = ForeignKeyOf(entity, nameof(Match.MatchedMatch));
            Assert.That((matched.DeclaringEntityType.ClrType, ColumnsOf(matched), matched.IsUnique, RequiredOf(matched)),
                Is.EqualTo((typeof(MatchedMatch), new List<string> { nameof(MatchedMatch.MatchID) }, true, (bool?)false)), "At most one matchmaking game's result");
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
            Assert.That(ColumnsOf(ForeignKeyOf(participant, nameof(MatchParticipant.MatchTeam))),
                Is.EqualTo(new List<string> { nameof(MatchParticipant.MatchID), nameof(MatchParticipant.TeamID) }), "Its team in the same match");
            Assert.That(ColumnsOf(ForeignKeyOf(participant, nameof(MatchParticipant.XboxUser))),
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
            Assert.That((ValueGeneratedOf(medal, nameof(Medal.MedalID)), ValueGeneratedOf(difficulty, nameof(MedalDifficulty.MedalDifficultyID)),
                ValueGeneratedOf(type, nameof(MedalType.MedalTypeID))),
                Is.EqualTo(((ValueGenerated?)ValueGenerated.Never, (ValueGenerated?)ValueGenerated.Never, (ValueGenerated?)ValueGenerated.Never)), "Halo's own IDs");
            Assert.That(ColumnsOf(ForeignKeyOf(medal, nameof(Medal.MedalDifficulty))), Is.EqualTo(new List<string> { nameof(Medal.MedalDifficultyID) }));
            Assert.That(ColumnsOf(ForeignKeyOf(medal, nameof(Medal.MedalType))), Is.EqualTo(new List<string> { nameof(Medal.MedalTypeID) }));
            Assert.That(KeyOf(earned), Is.EqualTo(new List<string> { nameof(MedalEarned.MedalID), nameof(MedalEarned.MatchID), nameof(MedalEarned.XboxUserID) }),
                "A medal a player a match");
            Assert.That(ColumnsOf(ForeignKeyOf(earned, nameof(MedalEarned.MatchParticipant))),
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
            Assert.That(IndexIsUnique(entity, nameof(GameVersion.GameVersionName)), Is.True);
            Assert.That(IndexIsUnique(entity, nameof(GameVersion.Slug)), Is.True);
            Assert.That((NullableOf(entity, nameof(GameVersion.GameVersionName)), MaxLengthOf(entity, nameof(GameVersion.GameVersionName))),
                Is.EqualTo(((bool?)false, (int?)30)));
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
            Assert.That(KeyOf(entity), Is.EqualTo(new List<string> { nameof(Region.RegionID) }));
            Assert.That((NullableOf(entity, nameof(Region.RegionName)), MaxLengthOf(entity, nameof(Region.RegionName))), Is.EqualTo(((bool?)false, (int?)50)));
            Assert.That(IndexIsUnique(entity, nameof(Region.RegionName)), Is.True);
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
            Assert.That((IndexIsUnique(entity, "LeagueName"), IndexIsUnique(entity, "Slug")), Is.EqualTo(((bool?)true, (bool?)true)));
            Assert.That((MaxLengthOf(entity, "LeagueName"), MaxLengthOf(entity, "Slug")), Is.EqualTo(((int?)50, (int?)50)));
            Assert.That((NullableOf(entity, "LeagueName"), NullableOf(entity, "Slug")), Is.EqualTo(((bool?)false, (bool?)false)));
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
            Assert.That(ValueGeneratedOf(discord, nameof(DiscordMessage.Id)), Is.EqualTo(ValueGenerated.Never), "Discord's own message ID");
            var from = ForeignKeyOf(discord, nameof(DiscordMessage.FromDiscordUser));
            var to = ForeignKeyOf(discord, nameof(DiscordMessage.ToDiscordUser));
            Assert.That((DeleteOf(from), DeleteOf(to)), Is.EqualTo(((DeleteBehavior?)DeleteBehavior.Restrict, (DeleteBehavior?)DeleteBehavior.Restrict)));
            Assert.That((ColumnsOf(from), ColumnsOf(to)), Is.EqualTo((new List<string> { nameof(DiscordMessage.FromDiscordUserId) },
                new List<string> { nameof(DiscordMessage.ToDiscordUserId) })));
            Assert.That((MaxLengthOf(outbox, nameof(OutboxMessage.Kind)), MaxLengthOf(outbox, nameof(OutboxMessage.GroupKey)),
                MaxLengthOf(outbox, nameof(OutboxMessage.LastError))), Is.EqualTo(((int?)100, (int?)100, (int?)2000)));
            Assert.That(IndexIsUnique(outbox, nameof(OutboxMessage.Status), nameof(OutboxMessage.NextAttemptAt)), Is.False, "The sender's next due message");
            Assert.That(IndexIsUnique(outbox, nameof(OutboxMessage.GroupKey), nameof(OutboxMessage.Sequence)), Is.True, "One message a place in its group");
            Assert.That(rank.FindCheckConstraint("CK_Matchmaking_Ranks_ThresholdNotNegative")!.Sql, Is.EqualTo("MmrThreshold >= 0"));
            Assert.That((KeyOf(outbox), KeyOf(rank), KeyOf(option)), Is.EqualTo((new List<string> { nameof(OutboxMessage.Id) },
                new List<string> { nameof(Rank.Id) }, new List<string> { nameof(AvailabilityOption.AvailabilityOptionID) })));
            Assert.That(IndexIsUnique(option, nameof(AvailabilityOption.DayOfWeek), nameof(AvailabilityOption.Time)), Is.True, "One option a time of a day");
        }
    }
}
