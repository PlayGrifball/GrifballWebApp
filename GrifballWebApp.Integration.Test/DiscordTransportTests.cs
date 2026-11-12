using System.Text.Json;
using GrifballWebApp.Server;
using GrifballWebApp.Domain.LateLeague;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Domain.Shared;
using Microsoft.EntityFrameworkCore;
using NetCord;
using User = GrifballWebApp.Domain.Players.User;

namespace GrifballWebApp.Integration.Test;

/// <summary>
/// End to end through NetCord with only the transport faked (NetCord issue #288): each test delivers Discord's JSON for one
/// interaction to the real server's NetCord handlers (<see cref="DiscordTransportTestHost"/>), so the thin modules, their
/// custom IDs and parameters, <see cref="DiscordInteractionPipeline"/> and the handlers all run, then checks the requests that
/// reached Discord. The handlers' own branches are unit tested against substitutes (e.g. <see cref="ButtonInteractionsTests"/>);
/// these cover the glue the substitutes can't: what NetCord actually parses and sends.
/// </summary>
/// <remarks>
/// One server and database for the fixture, so each test uses its own Discord user IDs, gamertags and channels.
/// Discord's interaction is always 1 with token "tok" (application 2), see <see cref="DiscordInteractionJson"/>.
/// </remarks>
[TestFixture]
[Category("DiscordTransport")]
[NonParallelizable]
public class DiscordTransportTests
{
    private const string Callback = "POST /interactions/1/tok/callback";
    private const string EditOriginal = "PATCH /webhooks/2/tok/messages/@original";
    private const string DeleteOriginal = "DELETE /webhooks/2/tok/messages/@original";

    private DiscordTransportTestHost _host = null!;
    private static readonly string[] expected = new[] { "Votes: You need a majority of at least 2 votes", "Home (1): VoteHome" };
    private static readonly string[] expectedArray = new[] { "VoteHome", "VoteAway" };
    private static readonly string[] expectedArray0 = new[] { "KickVoter", "KickTarget" };

    [OneTimeSetUp]
    public void StartServer() => _host = DiscordTransportTestHost.Start();

    [OneTimeTearDown]
    public async Task StopServer() => await _host.DisposeAsync();

    private static string[] Routes(IEnumerable<DiscordRequest> requests) => requests.Select(r => $"{r.Method} {r.Route}").ToArray();

    /// <summary>The labels of the kick menu in a vote message edit: one per player still in the match.</summary>
    private static string[] KickMenuLabels(JsonElement message) => Descendants(message.GetProperty("components"))
        .Where(e => e.ValueKind == JsonValueKind.Object && e.TryGetProperty("options", out _))
        .SelectMany(menu => menu.GetProperty("options").EnumerateArray().Select(o => o.GetProperty("label").GetString()!))
        .ToArray();

    private static IEnumerable<JsonElement> Descendants(JsonElement element)
    {
        yield return element;
        var children = element.ValueKind switch
        {
            JsonValueKind.Array => element.EnumerateArray().ToList(),
            JsonValueKind.Object => element.EnumerateObject().Select(p => p.Value).ToList(),
            _ => [],
        };
        foreach (var descendant in children.SelectMany(Descendants))
            yield return descendant;
    }

    /// <summary>A user linked to Discord user <paramref name="discordId"/>, with gamertag <paramref name="gamertag"/> set.</summary>
    private Task<User> LinkedUser(ulong discordId, string gamertag) => _host.WithContextAsync(async context =>
    {
        var user = User.Seed(
            userName: gamertag,
            discordUser: DiscordUser.FromDiscord((long)discordId, gamertag),
            xboxUser: XboxUser.FromXbox((long)discordId, gamertag));
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user;
    });

    /// <summary>An active match, <paramref name="home"/> against <paramref name="away"/>, with its vote message in a thread.</summary>
    private Task<MatchedMatch> ActiveMatch(User home, User away, ulong threadId, ulong voteMessageId) => _host.WithContextAsync(async context =>
    {
        var match = MatchedMatch.Start(
            new MatchedTeam([new MatchedPlayer(home.Id)]),
            new MatchedTeam([new MatchedPlayer(away.Id)])).Announced(threadId, voteMessageId);
        context.MatchedMatches.Add(match);
        await context.SaveChangesAsync();
        return match;
    });

    [Test]
    public async Task MatchmakingButton_VoteForWinner_EditsTheVoteMessageAndRepliesEphemerally()
    {
        var voter = await LinkedUser(5001, "VoteHome");
        var other = await LinkedUser(5002, "VoteAway");
        var match = await ActiveMatch(voter, other, threadId: 7001, voteMessageId: 8001);

        var requests = await _host.SendAsync(DiscordInteractionJson.Button($"voteforwinner:{match.Id}:Home", 5001));

        Assert.That(Routes(requests), Is.EqualTo(new[] { "PATCH /channels/7001/messages/8001", Callback, DeleteOriginal }));
        var threadEdit = requests[0].Body;
        var embeds = threadEdit.GetProperty("embeds").EnumerateArray()
            .Select(e => $"{e.GetProperty("title").GetString()}: {e.GetProperty("description").GetString()}")
            .ToList();
        var reply = requests[1];
        var vote = await _host.WithContextAsync(context => context.Set<MatchedWinnerVote>().AsNoTracking().SingleAsync(v => v.MatchId == match.Id));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(threadEdit.GetProperty("content").GetString(), Does.StartWith("This match can be now be started in game."));
            Assert.That(embeds, Is.EqualTo(expected));
            Assert.That(KickMenuLabels(threadEdit), Is.EquivalentTo(expectedArray));
            Assert.That(reply.CallbackType, Is.EqualTo(DiscordRequest.ChannelMessage));
            Assert.That(reply.Content, Is.EqualTo("Thanks for voting! You voted for Home"));
            Assert.That(reply.Flags, Is.EqualTo(MessageFlags.Ephemeral));
            Assert.That(vote.WinnerVote, Is.EqualTo(WinnerVote.Home));
            // Set by DiscordInteractionPipeline before the module ran
            Assert.That(vote.CreatedByID, Is.EqualTo(voter.Id));
        }
    }

    // Leaving needs no gamertag (nobody should be stuck in the queue): an unknown user is just not in it
    [TestCase("join_queue", 5011ul, "You must set your gamertag first")]
    [TestCase("leave_queue", 5012ul, "You are not in the Late League queue.")]
    public async Task MatchmakingButton_Queue_WithoutAGamertag_RepliesEphemerallyThenDeletesIt(string customId, ulong discordId, string reply)
    {
        var requests = await _host.SendAsync(DiscordInteractionJson.Button(customId, discordId));

        Assert.That(Routes(requests), Is.EqualTo(new[] { Callback, DeleteOriginal }));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(requests[0].CallbackType, Is.EqualTo(DiscordRequest.ChannelMessage));
            Assert.That(requests[0].Content, Is.EqualTo(reply));
            Assert.That(requests[0].Flags, Is.EqualTo(MessageFlags.Ephemeral));
        }
    }

    [Test]
    public async Task MatchmakingButton_SetGamertag_RespondsWithTheModal()
    {
        var requests = await _host.SendAsync(DiscordInteractionJson.Button("set_gamertag", 5101));

        Assert.That(Routes(requests), Is.EqualTo(new[] { Callback }));
        var modal = requests[0].Data;
        var label = modal.GetProperty("components")[0];
        var input = label.GetProperty("component");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(requests[0].CallbackType, Is.EqualTo(DiscordRequest.Modal));
            Assert.That(modal.GetProperty("custom_id").GetString(), Is.EqualTo("set_gamertag"));
            Assert.That(modal.GetProperty("title").GetString(), Is.EqualTo("Set Gamertag"));
            Assert.That(label.GetProperty("type").GetInt32(), Is.EqualTo((int)ComponentType.Label));
            Assert.That(label.GetProperty("label").GetString(), Is.EqualTo("Gamertag"));
            Assert.That(input.GetProperty("type").GetInt32(), Is.EqualTo((int)ComponentType.TextInput));
            // The modal test below submits this ID back
            Assert.That(input.GetProperty("custom_id").GetString(), Is.EqualTo("gamertag"));
            Assert.That(input.GetProperty("required").GetBoolean(), Is.True);
        }
    }

    [Test]
    public async Task MatchmakingStringMenu_VoteToKick_EditsTheVoteMessageAndAnnouncesTheVote()
    {
        var voter = await LinkedUser(5201, "KickVoter");
        var target = await LinkedUser(5202, "KickTarget");
        var match = await ActiveMatch(voter, target, threadId: 7201, voteMessageId: 8201);

        var requests = await _host.SendAsync(DiscordInteractionJson.StringMenu($"votetokick:{match.Id}", 5201, target.Id.ToString()));

        Assert.That(Routes(requests), Is.EqualTo(new[] { "PATCH /channels/7201/messages/8201", Callback }));
        var reply = requests[1];
        using (Assert.EnterMultipleScope())
        {
            Assert.That(reply.CallbackType, Is.EqualTo(DiscordRequest.ChannelMessage));
            // The target's gamertag was blank before their XboxUser was loaded (only the voter's was, by the user check)
            Assert.That(reply.Content, Is.EqualTo("KickVoter has voted to kick KickTarget"));
            Assert.That(reply.Flags, Is.Default, "announced to the whole thread");
            Assert.That(KickMenuLabels(requests[0].Body), Is.EquivalentTo(expectedArray0));
        }
    }

    [Test]
    public async Task SignupButton_SignsUpAndRepliesEphemerallyWithTheFormLink()
    {
        var user = await LinkedUser(5301, "Signer");
        var season = await _host.WithContextAsync(async context =>
        {
            var season = Season.Seed(seasonName: "Transport Signup Season").WithDates(publicAt: DateTime.UtcNow.AddDays(-1), signupsOpen: DateTime.UtcNow.AddDays(-1), signupsClose: DateTime.UtcNow.AddDays(7), draftStart: DateTime.UtcNow.AddDays(8), seasonStart: DateTime.UtcNow.AddDays(14), seasonEnd: DateTime.UtcNow.AddDays(60));
            context.Seasons.Add(season);
            await context.SaveChangesAsync();
            return season;
        });

        var requests = await _host.SendAsync(DiscordInteractionJson.Button($"signup:{season.SeasonID}", 5301));

        Assert.That(Routes(requests), Is.EqualTo(new[] { Callback }));
        var reply = requests[0];
        var signup = await _host.WithContextAsync(context => context.SeasonSignups.AsNoTracking().SingleOrDefaultAsync(s => s.SeasonID == season.SeasonID));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(reply.CallbackType, Is.EqualTo(DiscordRequest.ChannelMessage));
            Assert.That(reply.Content, Is.EqualTo("You have signed up for the season. Remember you can do more things like set your availability "
                + $"[here]({DiscordTransportTestHost.BaseUrl}/login?followUp=/season/{season.SeasonID}/signupForm)"));
            Assert.That(reply.Flags, Is.EqualTo(MessageFlags.Ephemeral));
            Assert.That(signup?.UserID, Is.EqualTo(user.Id));
        }
    }

    [Test]
    public async Task DraftStringMenu_DefersThenEditsInTheOutcome()
    {
        await LinkedUser(5401, "Drafter");

        // Season 1, captain 2, menu 0; player 999999 never signed up, so the pick is refused
        var requests = await _host.SendAsync(DiscordInteractionJson.StringMenu("draftpick:1:2:0", 5401, "999999"));

        Assert.That(Routes(requests), Is.EqualTo(new[] { Callback, EditOriginal }));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(requests[0].CallbackType, Is.EqualTo(DiscordRequest.DeferredChannelMessage));
            Assert.That(requests[0].Flags, Is.Default, "the outcome is shown in the draft channel");
            Assert.That(requests[1].Content, Is.EqualTo("Hey <@5401>, This player has not signed up"));
        }
    }

    /// <summary>Both ways of setting a gamertag (the modal and /setgamertag) defer ephemerally, then edit in the result and delete it.</summary>
    private async Task AssertGamertagSet(IReadOnlyList<DiscordRequest> requests, ulong discordId)
    {
        Assert.That(Routes(requests), Is.EqualTo(new[] { Callback, EditOriginal, DeleteOriginal }));
        var discordUser = await _host.WithContextAsync(context => context.DiscordUsers
            .AsNoTracking()
            .Include(d => d.User)
            .SingleAsync(d => d.DiscordUserID == (long)discordId));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(requests[0].CallbackType, Is.EqualTo(DiscordRequest.DeferredChannelMessage));
            Assert.That(requests[0].Flags, Is.EqualTo(MessageFlags.Ephemeral));
            Assert.That(requests[1].Content, Is.EqualTo("Gamertag has been set"));
            Assert.That(requests[1].Flags, Is.EqualTo(MessageFlags.Ephemeral));
            Assert.That(discordUser.User?.XboxUserID, Is.EqualTo((long)discordId));
        }
    }

    private Task<int> KnownGamertag(long xuid, string gamertag) => _host.WithContextAsync(async context =>
    {
        // Already known, so no Halo Infinite lookup
        context.XboxUsers.Add(XboxUser.FromXbox(xuid, gamertag));
        return await context.SaveChangesAsync();
    });

    [Test]
    public async Task Modal_SetGamertag_ReadsTheTextInputAndSetsIt()
    {
        await KnownGamertag(5501, "ModalGT");

        var requests = await _host.SendAsync(DiscordInteractionJson.Modal("set_gamertag", "gamertag", "ModalGT", 5501, "modal-user"));

        await AssertGamertagSet(requests, 5501);
    }

    [Test]
    public async Task SlashCommand_SetGamertag_ReadsTheOptionAndSetsIt()
    {
        await KnownGamertag(5601, "SlashGT");

        var requests = await _host.SendAsync(DiscordInteractionJson.SlashCommand("setgamertag", 5601, "slash-user", ("gamertag", "SlashGT")));

        await AssertGamertagSet(requests, 5601);
    }

    [Test]
    public async Task SlashCommand_Matches_WithoutAGamertag_DefersThenAsksForOne()
    {
        var requests = await _host.SendAsync(DiscordInteractionJson.SlashCommand("matches", 5701));

        Assert.That(Routes(requests), Is.EqualTo(new[] { Callback, EditOriginal }));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(requests[0].CallbackType, Is.EqualTo(DiscordRequest.DeferredChannelMessage));
            Assert.That(requests[0].Flags, Is.Default);
            Assert.That(requests[1].Content, Is.EqualTo("You must set your gamertag first, or provide someone else's gamertag to search for"));
        }
    }

    [Test]
    public async Task Autocomplete_MatchesGamertag_SuggestsKnownGamertags()
    {
        await KnownGamertag(5801, "AutoTrA");
        await KnownGamertag(5802, "AutoTrB");
        await KnownGamertag(5803, "Unrelated");

        var requests = await _host.SendAsync(DiscordInteractionJson.Autocomplete("matches", "gamertag", "AutoTr", 5801));

        Assert.That(Routes(requests), Is.EqualTo(new[] { Callback }));
        var choices = requests[0].Data.GetProperty("choices").EnumerateArray()
            .Select(c => (Name: c.GetProperty("name").GetString(), Value: c.GetProperty("value").GetString()))
            .ToList();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(requests[0].CallbackType, Is.EqualTo(DiscordRequest.AutocompleteResult));
            Assert.That(choices, Is.EquivalentTo(new[] { ("AutoTrA", "AutoTrA"), ("AutoTrB", "AutoTrB") }));
        }
    }

    [Test]
    public async Task ModuleThrows_PipelineRepliesWithTheGenericEphemeralError()
    {
        await LinkedUser(5901, "ErrSigner");

        // No such season: SignupsService throws, nothing has responded yet
        var requests = await _host.SendAsync(DiscordInteractionJson.Button("signup:2147483000", 5901));

        Assert.That(Routes(requests), Is.EqualTo(new[] { Callback }));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(requests[0].CallbackType, Is.EqualTo(DiscordRequest.ChannelMessage));
            Assert.That(requests[0].Content, Is.EqualTo(DiscordInteractionPipeline.GenericErrorMessage));
            Assert.That(requests[0].Flags, Is.EqualTo(MessageFlags.Ephemeral));
            Assert.That(requests[0].Body.ToString(), Does.Not.Contain("Season does not exist"));
        }
    }

    /// <summary>
    /// Every slash command of #271 with its required options, as Discord sends it: subcommands and their groups nested in the
    /// command. From a Discord user with no account, so the staff and personal ones stop at the Set Gamertag reply.
    /// </summary>
    private static readonly object[] Commands =
    [
        new object[] { "season list", Array.Empty<(string, string)>() },
        new object[] { "season standings", new[] { ("season", "Missing") } },
        new object[] { "season info", Array.Empty<(string, string)>() },
        new object[] { "season schedule", Array.Empty<(string, string)>() },
        new object[] { "season stats", Array.Empty<(string, string)>() },
        new object[] { "season grades", Array.Empty<(string, string)>() },
        new object[] { "season signups", Array.Empty<(string, string)>() },
        new object[] { "season bracket", Array.Empty<(string, string)>() },
        new object[] { "season draft", Array.Empty<(string, string)>() },
        new object[] { "team info", new[] { ("team", "Missing") } },
        new object[] { "team roster", new[] { ("team", "Missing") } },
        new object[] { "team history", new[] { ("team", "Missing") } },
        new object[] { "team matches", new[] { ("team", "Missing") } },
        new object[] { "team vods", new[] { ("team", "Missing") } },
        new object[] { "player profile", new[] { ("player", "Missing") } },
        new object[] { "player stats", Array.Empty<(string, string)>() },
        new object[] { "player commendations", Array.Empty<(string, string)>() },
        new object[] { "player history", Array.Empty<(string, string)>() },
        new object[] { "player vods", Array.Empty<(string, string)>() },
        new object[] { "player matches", new[] { ("player", "Missing") } },
        new object[] { "match info", new[] { ("match", "Missing") } },
        new object[] { "match game", new[] { ("match_id", "not-a-guid") } },
        new object[] { "leaderboard top", Array.Empty<(string, string)>() },
        new object[] { "leaderboard rank", new[] { ("player", "Missing") } },
        new object[] { "lateleague seasons", Array.Empty<(string, string)>() },
        new object[] { "lateleague season", Array.Empty<(string, string)>() },
        new object[] { "events", Array.Empty<(string, string)>() },
        new object[] { "search", new[] { ("query", "Missing") } },
        new object[] { "queue status", Array.Empty<(string, string)>() },
        new object[] { "queue join", Array.Empty<(string, string)>() },
        new object[] { "queue leave", Array.Empty<(string, string)>() },
        new object[] { "me region", new[] { ("region", "none") } },
        new object[] { "me claim", new[] { ("gamertag", "Missing") } },
        new object[] { "me claims", Array.Empty<(string, string)>() },
        new object[] { "me logins", Array.Empty<(string, string)>() },
        new object[] { "signup", Array.Empty<(string, string)>() },
        new object[] { "availability show", Array.Empty<(string, string)>() },
        new object[] { "reschedule request", new[] { ("match", "Missing"), ("reason", "Busy") } },
        new object[] { "reschedule pending", Array.Empty<(string, string)>() },
        new object[] { "reschedule decide", new[] { ("request", "1"), ("approve", "true") } },
        new object[] { "admin dashboard", Array.Empty<(string, string)>() },
        new object[] { "admin overdue", Array.Empty<(string, string)>() },
        new object[] { "admin medals", Array.Empty<(string, string)>() },
        new object[] { "admin match report", new[] { ("match", "Missing"), ("match_id", "not-a-guid") } },
        new object[] { "admin match forfeit", new[] { ("match", "Missing"), ("side", "0") } },
        new object[] { "admin match undo", new[] { ("match", "Missing") } },
        new object[] { "admin match pull", new[] { ("match", "Missing") } },
        new object[] { "admin match time", new[] { ("match", "Missing"), ("time", "2030-01-01 20:00") } },
        new object[] { "admin season generate", new[] { ("home_matches", "1"), ("best_of", "3") } },
        new object[] { "admin bracket create", new[] { ("participants", "4"), ("best_of", "3") } },
        new object[] { "admin bracket seed", Array.Empty<(string, string)>() },
        new object[] { "admin draft lock", Array.Empty<(string, string)>() },
        new object[] { "admin draft unlock", Array.Empty<(string, string)>() },
        new object[] { "admin schedule show", Array.Empty<(string, string)>() },
        new object[] { "admin schedule run", Array.Empty<(string, string)>() },
        new object[] { "admin schedule apply", new[] { ("recommendation", "1") } },
        new object[] { "admin halo pull", new[] { ("match_id", "not-a-guid") } },
        new object[] { "admin jobs list", Array.Empty<(string, string)>() },
        new object[] { "admin jobs run", new[] { ("job", "a/b") } },
        new object[] { "admin jobs pause", new[] { ("job", "a/b") } },
        new object[] { "admin claims list", Array.Empty<(string, string)>() },
        new object[] { "admin claims approve", new[] { ("claim", "1") } },
        new object[] { "admin claims reject", new[] { ("claim", "1") } },
    ];

    [TestCaseSource(nameof(Commands))]
    public async Task SlashCommand_ReachesItsHandler(string path, (string Name, string Value)[] options)
    {
        var requests = await _host.SendAsync(DiscordInteractionJson.SubCommand(path, 6001, options));

        // Reached the handler: it deferred, or answered privately itself; NetCord's own failures (an unknown subcommand, an
        // option it can't read) answer in the channel, and an exception gets the generic error
        using (Assert.EnterMultipleScope())
        {
            Assert.That(requests, Is.Not.Empty);
            Assert.That(requests[0].CallbackType == DiscordRequest.DeferredChannelMessage || requests[0].Flags.HasFlag(MessageFlags.Ephemeral),
                $"{path}: {requests[0].Body}");
            Assert.That(requests.Select(r => r.Content), Has.None.EqualTo(DiscordInteractionPipeline.GenericErrorMessage), path);
        }
    }

    [Test]
    public async Task AdminJobCommands_RunThroughTheScheduler_ForStaff()
    {
        var staff = await LinkedUser(6101, "JobStaff");
        var match = await _host.WithContextAsync(async context =>
        {
            foreach (var role in await context.Roles.Where(r => r.Name == "Commissioner" || r.Name == "Sysadmin").ToListAsync())
                context.UserRoles.Add(new UserRole { UserId = staff.Id, RoleId = role.Id });
            var season = Season.Seed(seasonName: "Transport Jobs");
            context.Seasons.Add(season);
            await context.SaveChangesAsync();
            var seasonMatch = SeasonMatch.Seed(season.SeasonID, null, null);
            context.SeasonMatches.Add(seasonMatch);
            await context.SaveChangesAsync();
            return seasonMatch;
        });
        var season = match.SeasonID.ToString();

        var pull = await _host.SendAsync(DiscordInteractionJson.SubCommand("admin match pull", 6101, ("match", match.SeasonMatchID.ToString())));
        var run = await _host.SendAsync(DiscordInteractionJson.SubCommand("admin schedule run", 6101, ("season", season)));
        var show = await _host.SendAsync(DiscordInteractionJson.SubCommand("admin schedule show", 6101, ("season", season)));
        var jobs = await _host.SendAsync(DiscordInteractionJson.SubCommand("admin jobs list", 6101));
        var unknown = await _host.SendAsync(DiscordInteractionJson.SubCommand("admin jobs run", 6101, ("job", "nope/nothing")));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(pull[^1].Content, Does.StartWith("The pull is "));
            Assert.That(run[^1].Content, Does.StartWith("The recommendation is "));
            Assert.That(show[^1].Data.GetProperty("embeds")[0].GetProperty("title").GetString(), Does.StartWith("Schedule ("));
            Assert.That(jobs[^1].Data.GetProperty("embeds")[0].GetProperty("title").GetString(), Is.EqualTo("Jobs"));
            Assert.That(unknown[^1].Content, Is.EqualTo("There is no such job."));
            Assert.That(new[] { pull, run, show, jobs, unknown }.Select(r => r[0].CallbackType), Has.All.EqualTo(DiscordRequest.DeferredChannelMessage));
        }
    }
}
