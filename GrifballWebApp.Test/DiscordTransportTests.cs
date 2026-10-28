using GrifballWebApp.Database.Models;
using GrifballWebApp.Server;
using Microsoft.EntityFrameworkCore;
using NetCord;
using User = GrifballWebApp.Database.Models.User;

namespace GrifballWebApp.Test;

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

    [OneTimeSetUp]
    public void StartServer() => _host = DiscordTransportTestHost.Start();

    [OneTimeTearDown]
    public async Task StopServer() => await _host.DisposeAsync();

    private static string[] Routes(IEnumerable<DiscordRequest> requests) => requests.Select(r => $"{r.Method} {r.Route}").ToArray();

    /// <summary>A user linked to Discord user <paramref name="discordId"/>, with gamertag <paramref name="gamertag"/> set.</summary>
    private Task<User> LinkedUser(ulong discordId, string gamertag) => _host.WithContextAsync(async context =>
    {
        var user = new User
        {
            UserName = gamertag,
            DiscordUser = new DiscordUser { DiscordUserID = (long)discordId, DiscordUsername = gamertag },
            XboxUser = new XboxUser { XboxUserID = (long)discordId, Gamertag = gamertag },
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user;
    });

    /// <summary>An active match, <paramref name="home"/> against <paramref name="away"/>, with its vote message in a thread.</summary>
    private Task<MatchedMatch> ActiveMatch(User home, User away, ulong threadId, ulong voteMessageId) => _host.WithContextAsync(async context =>
    {
        var match = new MatchedMatch
        {
            HomeTeam = new MatchedTeam { Players = [new MatchedPlayer { UserID = home.Id }] },
            AwayTeam = new MatchedTeam { Players = [new MatchedPlayer { UserID = away.Id }] },
            Active = true,
            ThreadID = threadId,
            VoteMessageID = voteMessageId,
        };
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
        var vote = await _host.WithContextAsync(context => context.MatchedWinnerVotes.AsNoTracking().SingleAsync(v => v.MatchId == match.Id));
        Assert.Multiple(() =>
        {
            Assert.That(threadEdit.GetProperty("content").GetString(), Does.StartWith("This match can be now be started in game."));
            Assert.That(embeds, Is.EqualTo(new[] { "Votes: You need a majority of at least 2 votes", "Home (1): VoteHome" }));
            Assert.That(reply.CallbackType, Is.EqualTo(DiscordRequest.ChannelMessage));
            Assert.That(reply.Content, Is.EqualTo("Thanks for voting! You voted for Home"));
            Assert.That(reply.Flags, Is.EqualTo(MessageFlags.Ephemeral));
            Assert.That(vote.WinnerVote, Is.EqualTo(WinnerVote.Home));
            // Set by DiscordInteractionPipeline before the module ran
            Assert.That(vote.CreatedByID, Is.EqualTo(voter.Id));
        });
    }

    [TestCase("join_queue", 5011ul)]
    [TestCase("leave_queue", 5012ul)]
    public async Task MatchmakingButton_Queue_WithoutAGamertag_RepliesEphemerallyThenDeletesIt(string customId, ulong discordId)
    {
        var requests = await _host.SendAsync(DiscordInteractionJson.Button(customId, discordId));

        Assert.That(Routes(requests), Is.EqualTo(new[] { Callback, DeleteOriginal }));
        Assert.Multiple(() =>
        {
            Assert.That(requests[0].CallbackType, Is.EqualTo(DiscordRequest.ChannelMessage));
            Assert.That(requests[0].Content, Is.EqualTo("You must set your gamertag first"));
            Assert.That(requests[0].Flags, Is.EqualTo(MessageFlags.Ephemeral));
        });
    }

    [Test]
    public async Task MatchmakingButton_SetGamertag_RespondsWithTheModal()
    {
        var requests = await _host.SendAsync(DiscordInteractionJson.Button("set_gamertag", 5101));

        Assert.That(Routes(requests), Is.EqualTo(new[] { Callback }));
        var modal = requests[0].Data;
        var label = modal.GetProperty("components")[0];
        var input = label.GetProperty("component");
        Assert.Multiple(() =>
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
        });
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
        Assert.Multiple(() =>
        {
            Assert.That(reply.CallbackType, Is.EqualTo(DiscordRequest.ChannelMessage));
            Assert.That(reply.Content, Is.EqualTo("KickVoter has voted to kick KickTarget"));
            Assert.That(reply.Flags, Is.EqualTo((MessageFlags)0), "announced to the whole thread");
        });
    }

    [Test]
    public async Task SignupButton_SignsUpAndRepliesEphemerallyWithTheFormLink()
    {
        var user = await LinkedUser(5301, "Signer");
        var season = await _host.WithContextAsync(async context =>
        {
            var season = new Season
            {
                SeasonName = "Transport Signup Season",
                PublicAt = DateTime.UtcNow.AddDays(-1),
                SignupsOpen = DateTime.UtcNow.AddDays(-1),
                SignupsClose = DateTime.UtcNow.AddDays(7),
                DraftStart = DateTime.UtcNow.AddDays(8),
                SeasonStart = DateTime.UtcNow.AddDays(14),
                SeasonEnd = DateTime.UtcNow.AddDays(60),
            };
            context.Seasons.Add(season);
            await context.SaveChangesAsync();
            return season;
        });

        var requests = await _host.SendAsync(DiscordInteractionJson.Button($"signup:{season.SeasonID}", 5301));

        Assert.That(Routes(requests), Is.EqualTo(new[] { Callback }));
        var reply = requests[0];
        var signup = await _host.WithContextAsync(context => context.SeasonSignups.AsNoTracking().SingleOrDefaultAsync(s => s.SeasonID == season.SeasonID));
        Assert.Multiple(() =>
        {
            Assert.That(reply.CallbackType, Is.EqualTo(DiscordRequest.ChannelMessage));
            Assert.That(reply.Content, Is.EqualTo("You have signed up for the season. Remember you can do more things like set your availability "
                + $"[here]({DiscordTransportTestHost.BaseUrl}/login?followUp=/season/{season.SeasonID}/signupForm)"));
            Assert.That(reply.Flags, Is.EqualTo(MessageFlags.Ephemeral));
            Assert.That(signup?.UserID, Is.EqualTo(user.Id));
        });
    }

    [Test]
    public async Task DraftStringMenu_DefersThenEditsInTheOutcome()
    {
        await LinkedUser(5401, "Drafter");

        // Season 1, captain 2, menu 0; player 999999 never signed up, so the pick is refused
        var requests = await _host.SendAsync(DiscordInteractionJson.StringMenu("draftpick:1:2:0", 5401, "999999"));

        Assert.That(Routes(requests), Is.EqualTo(new[] { Callback, EditOriginal }));
        Assert.Multiple(() =>
        {
            Assert.That(requests[0].CallbackType, Is.EqualTo(DiscordRequest.DeferredChannelMessage));
            Assert.That(requests[0].Flags, Is.EqualTo((MessageFlags)0), "the outcome is shown in the draft channel");
            Assert.That(requests[1].Content, Is.EqualTo("Hey <@5401>, This player has not signed up"));
        });
    }

    /// <summary>Both ways of setting a gamertag (the modal and /setgamertag) defer ephemerally, then edit in the result and delete it.</summary>
    private async Task AssertGamertagSet(IReadOnlyList<DiscordRequest> requests, ulong discordId)
    {
        Assert.That(Routes(requests), Is.EqualTo(new[] { Callback, EditOriginal, DeleteOriginal }));
        var discordUser = await _host.WithContextAsync(context => context.DiscordUsers
            .AsNoTracking()
            .Include(d => d.User)
            .SingleAsync(d => d.DiscordUserID == (long)discordId));
        Assert.Multiple(() =>
        {
            Assert.That(requests[0].CallbackType, Is.EqualTo(DiscordRequest.DeferredChannelMessage));
            Assert.That(requests[0].Flags, Is.EqualTo(MessageFlags.Ephemeral));
            Assert.That(requests[1].Content, Is.EqualTo("Gamertag has been set"));
            Assert.That(requests[1].Flags, Is.EqualTo(MessageFlags.Ephemeral));
            Assert.That(discordUser.User?.XboxUserID, Is.EqualTo((long)discordId));
        });
    }

    private Task KnownGamertag(long xuid, string gamertag) => _host.WithContextAsync(async context =>
    {
        // Already known, so no Halo Infinite lookup
        context.XboxUsers.Add(new XboxUser { XboxUserID = xuid, Gamertag = gamertag });
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
        Assert.Multiple(() =>
        {
            Assert.That(requests[0].CallbackType, Is.EqualTo(DiscordRequest.DeferredChannelMessage));
            Assert.That(requests[0].Flags, Is.EqualTo((MessageFlags)0));
            Assert.That(requests[1].Content, Is.EqualTo("You must set your gamertag first, or provide someone else's gamertag to search for"));
        });
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
        Assert.Multiple(() =>
        {
            Assert.That(requests[0].CallbackType, Is.EqualTo(DiscordRequest.AutocompleteResult));
            Assert.That(choices, Is.EquivalentTo(new[] { ("AutoTrA", "AutoTrA"), ("AutoTrB", "AutoTrB") }));
        });
    }

    [Test]
    public async Task ModuleThrows_PipelineRepliesWithTheGenericEphemeralError()
    {
        await LinkedUser(5901, "ErrSigner");

        // No such season: SignupsService throws, nothing has responded yet
        var requests = await _host.SendAsync(DiscordInteractionJson.Button("signup:2147483000", 5901));

        Assert.That(Routes(requests), Is.EqualTo(new[] { Callback }));
        Assert.Multiple(() =>
        {
            Assert.That(requests[0].CallbackType, Is.EqualTo(DiscordRequest.ChannelMessage));
            Assert.That(requests[0].Content, Is.EqualTo(DiscordInteractionPipeline.GenericErrorMessage));
            Assert.That(requests[0].Flags, Is.EqualTo(MessageFlags.Ephemeral));
            Assert.That(requests[0].Body.ToString(), Does.Not.Contain("Season does not exist"));
        });
    }
}
