using System.Text.Json;
using GrifballWebApp.Server;
using GrifballWebApp.Server.Commands;
using GrifballWebApp.Server.Voice;
using GrifballWebApp.Domain.LateLeague;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Domain.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NetCord;
using Team = GrifballWebApp.Domain.League.Team;
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
    private static readonly string[] RescheduleInputs = ["reason", "new_time"];
    private static readonly string[] OnlyTheSeasonMenu = ["seasonview:signups"];

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

    // What follows the click is the chat's answer (MessageCreateHandlerConversationUnitTests): here, Confirm and Cancel go at once,
    // and the button the reply offered stays
    [Test]
    public async Task ChatConfirmButton_TakesConfirmAndCancelOffTheMessage_AsItsResponse()
    {
        const string rows = """
            [{"type":1,"components":[{"type":2,"style":3,"label":"Confirm","custom_id":"chatconfirm:a1:yes"},{"type":2,"style":4,"label":"Cancel","custom_id":"chatconfirm:a1:no"}]},
             {"type":1,"components":[{"type":2,"style":1,"label":"Join the queue","custom_id":"join_queue"}]}]
            """;

        var requests = await _host.SendAsync(DiscordInteractionJson.ButtonOn("chatconfirm:a1:no", 5951, rows));

        var response = requests[0];
        var left = response.Data.GetProperty("components");
        var offered = left[0].GetProperty("components")[0];
        using (Assert.EnterMultipleScope())
        {
            Assert.That(response.Route, Is.EqualTo("/interactions/1/tok/callback"));
            Assert.That(response.CallbackType, Is.EqualTo(DiscordRequest.UpdateMessage));
            Assert.That(left.GetArrayLength(), Is.EqualTo(1));
            Assert.That(offered.GetProperty("custom_id").GetString(), Is.EqualTo("join_queue"));
            Assert.That(offered.GetProperty("label").GetString(), Is.EqualTo("Join the queue"));
            Assert.That(offered.GetProperty("style").GetInt32(), Is.EqualTo((int)ButtonStyle.Primary));
        }
    }

    /// <summary>A form the chat made, held under <paramref name="id"/> for Discord user <paramref name="discordId"/> (no site account).</summary>
    private async Task<GrifballWebApp.Application.Chat.IChatForms> HeldForm(string id, ulong discordId)
    {
        var forms = _host.Services.GetRequiredService<GrifballWebApp.Application.Chat.IChatForms>();
        var form = new GrifballWebApp.Application.Chat.ChatForm("Your team",
        [
            new GrifballWebApp.Application.Chat.ChatFormField("Team name"),
            new GrifballWebApp.Application.Chat.ChatFormField("Side", GrifballWebApp.Application.Chat.ChatFormFieldKind.Choice, Choices: ["Red", "Blue"]),
        ]);
        await forms.SaveAsync(id, new GrifballWebApp.Application.Chat.HeldForm(form, GrifballWebApp.Application.Chat.ChatLimits.Discord(discordId), Direct: false),
            GrifballWebApp.Application.Chat.ChatForms.Lasts);
        return forms;
    }

    [Test]
    public async Task ChatFormButton_OpensTheFormsModal_ForItsAsker()
    {
        await HeldForm("tform1", 7401);

        var requests = await _host.SendAsync(DiscordInteractionJson.Button("chatform:tform1", 7401));

        var modal = requests[0].Data;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Routes(requests), Is.EqualTo(new[] { Callback }));
            Assert.That(requests[0].CallbackType, Is.EqualTo(DiscordRequest.Modal));
            Assert.That(modal.GetProperty("custom_id").GetString(), Is.EqualTo("chatform_answers:tform1"));
            // The submits below send these IDs back
            Assert.That(modal.GetProperty("components")[0].GetProperty("component").GetProperty("custom_id").GetString(), Is.EqualTo("field0"));
            Assert.That(modal.GetProperty("components")[1].GetProperty("component").GetProperty("type").GetInt32(), Is.EqualTo(21), "a radio group");
        }
    }

    [Test]
    public async Task ChatFormModal_AnswersThatDontFit_AreRefusedEphemerally_TheFormStillWaits()
    {
        var forms = await HeldForm("tform2", 7402);

        var requests = await _host.SendAsync(DiscordInteractionJson.ModalWith("chatform_answers:tform2",
            """[{"type":18,"id":1,"component":{"type":4,"id":2,"custom_id":"field0","value":"Bombers"}}]""", 7402));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(Routes(requests), Is.EqualTo(new[] { Callback }));
            Assert.That(requests[0].Content, Is.EqualTo("\"Side\" needs an answer."));
            Assert.That(requests[0].Flags, Is.EqualTo(MessageFlags.Ephemeral));
            Assert.That(await forms.PeekAsync("tform2"), Is.Not.Null);
        }
    }

    // What follows is the chat's answer to them (MessageCreateHandlerConversationUnitTests)
    [Test]
    public async Task ChatFormModal_Answered_PostsTheAnswers_AndTakesTheForm()
    {
        var forms = await HeldForm("tform3", 7403);

        var requests = await _host.SendAsync(DiscordInteractionJson.ModalWith("chatform_answers:tform3",
            """
            [{"type":18,"id":1,"component":{"type":4,"id":2,"custom_id":"field0","value":"Bombers"}},
             {"type":18,"id":3,"component":{"type":21,"id":4,"custom_id":"field1","value":"Blue"}}]
            """, 7403));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(Routes(requests).Take(2), Is.EqualTo(new[] { Callback, "GET /webhooks/2/tok/messages/@original" }));
            Assert.That(requests[0].CallbackType, Is.EqualTo(DiscordRequest.ChannelMessage));
            Assert.That(requests[0].Content, Is.EqualTo("My answers to \"Your team\":\n- Team name: Bombers\n- Side: Blue"));
            Assert.That(requests[0].Flags, Is.EqualTo(MessageFlags.SuppressEmbeds), "posted for the channel to see, no link previewed");
            Assert.That(await forms.PeekAsync("tform3"), Is.Null, "answered once");
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

    private Task<Season> OpenSignupSeason(string name) => _host.WithContextAsync(async context =>
    {
        var now = DateTime.UtcNow;
        var season = Season.Seed(seasonName: name).WithDates(publicAt: now.AddDays(-1), signupsOpen: now.AddDays(-1), signupsClose: now.AddDays(7), draftStart: now.AddDays(8), seasonStart: now.AddDays(14), seasonEnd: now.AddDays(60));
        context.Seasons.Add(season);
        await context.SaveChangesAsync();
        return season;
    });

    [Test]
    public async Task SignupDetailsButton_RespondsWithTheSignupModal()
    {
        await LinkedUser(5311, "Detailer");
        var season = await OpenSignupSeason("Transport Details Season");

        var requests = await _host.SendAsync(DiscordInteractionJson.Button($"signupdetails:{season.SeasonID}", 5311));

        Assert.That(Routes(requests), Is.EqualTo(new[] { Callback }));
        var modal = requests[0].Data;
        var choices = modal.GetProperty("components")[1].GetProperty("component");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(requests[0].CallbackType, Is.EqualTo(DiscordRequest.Modal));
            Assert.That(modal.GetProperty("custom_id").GetString(), Is.EqualTo($"signup:{season.SeasonID}"));
            Assert.That(choices.GetProperty("type").GetInt32(), Is.EqualTo(22), "a checkbox group");
            // The modal test below submits these IDs back
            Assert.That(choices.GetProperty("custom_id").GetString(), Is.EqualTo("choices"));
        }
    }

    [Test]
    public async Task SignupModal_DefersThenSavesTheSignupAndEditsInTheReply()
    {
        var user = await LinkedUser(5312, "ModalSigner");
        var season = await OpenSignupSeason("Transport Modal Season");
        const string components = """
            [{"type":18,"id":1,"component":{"type":4,"id":2,"custom_id":"team_name","value":"Bombers"}},
             {"type":18,"id":3,"component":{"type":22,"id":4,"custom_id":"choices","values":["captain"]}}]
            """;

        var requests = await _host.SendAsync(DiscordInteractionJson.ModalWith($"signup:{season.SeasonID}", components, 5312));

        var signup = await _host.WithContextAsync(context => context.SeasonSignups.AsNoTracking().SingleAsync(s => s.SeasonID == season.SeasonID));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Routes(requests), Is.EqualTo(new[] { Callback, EditOriginal }));
            Assert.That(requests[0].CallbackType, Is.EqualTo(DiscordRequest.DeferredChannelMessage));
            Assert.That(requests[0].Flags, Is.EqualTo(MessageFlags.Ephemeral));
            Assert.That(requests[1].Content, Is.EqualTo("Your signup is saved. No times set yet."));
            Assert.That(signup.UserID, Is.EqualTo(user.Id));
            Assert.That(signup.TeamName, Is.EqualTo("Bombers"));
            Assert.That(signup.WillCaptain, Is.True);
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
            Assert.That(requests[1].Content, Is.EqualTo(DiscordCommandsHandler.NoGamertag));
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
            Assert.That(choices, Is.EquivalentTo(new[] { ("AutoTrA", "id:5801"), ("AutoTrB", "id:5802") }));
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
        new object[] { "match info", new[] { ("match", "Missing") } },
        new object[] { "match game", new[] { ("match_id", "not-a-guid") } },
        new object[] { "leaderboard top", Array.Empty<(string, string)>() },
        new object[] { "leaderboard rank", new[] { ("player", "Missing") } },
        new object[] { "lateleague seasons", Array.Empty<(string, string)>() },
        new object[] { "lateleague season", Array.Empty<(string, string)>() },
        new object[] { "events", Array.Empty<(string, string)>() },
        new object[] { "help", Array.Empty<(string, string)>() },
        new object[] { "help", new[] { ("command", "season") } },
        new object[] { "search", new[] { ("query", "Missing") } },
        new object[] { "queue status", Array.Empty<(string, string)>() },
        new object[] { "queue join", Array.Empty<(string, string)>() },
        new object[] { "queue leave", Array.Empty<(string, string)>() },
        new object[] { "me region", new[] { ("region", "none") } },
        new object[] { "me claim", new[] { ("gamertag", "Missing") } },
        new object[] { "me claims", Array.Empty<(string, string)>() },
        new object[] { "voice join", Array.Empty<(string, string)>() },
        new object[] { "voice leave", Array.Empty<(string, string)>() },
        new object[] { "voice optin", Array.Empty<(string, string)>() },
        new object[] { "voice optout", Array.Empty<(string, string)>() },
        new object[] { "me logins", Array.Empty<(string, string)>() },
        // With nothing given /signup opens the signup modal instead (SlashCommand_Signup_WithNothingGiven_RespondsWithTheSignupModal)
        new object[] { "signup", new[] { ("will_captain", "false") } },
        new object[] { "availability show", Array.Empty<(string, string)>() },
        new object[] { "reschedule request", new[] { ("match", "Missing"), ("reason", "Busy") } },
        new object[] { "reschedule suggest", new[] { ("match", "Missing") } },
        new object[] { "reschedule pending", Array.Empty<(string, string)>() },
        new object[] { "reschedule decide", new[] { ("request", "1"), ("approve", "true") } },
        new object[] { "admin dashboard", Array.Empty<(string, string)>() },
        new object[] { "admin overdue", Array.Empty<(string, string)>() },
        new object[] { "admin medals", Array.Empty<(string, string)>() },
        new object[] { "admin match report", new[] { ("match", "Missing"), ("match_id", "not-a-guid") } },
        new object[] { "admin match report", new[] { ("match", "Missing"), ("match_id", "not-a-guid"), ("replaces", "2") } },
        new object[] { "admin match forfeit", new[] { ("match", "Missing"), ("side", "0") } },
        new object[] { "admin match undo", new[] { ("match", "Missing") } },
        new object[] { "match pull", new[] { ("match", "Missing"), ("back_to_season_start", "false") } },
        new object[] { "player pull", Array.Empty<(string, string)>() },
        new object[] { "admin match time", new[] { ("match", "Missing"), ("time", "2030-01-01 20:00") } },
        new object[] { "admin season generate", new[] { ("season", "Missing"), ("home_matches", "1"), ("best_of", "3") } },
        new object[] { "admin bracket create", new[] { ("season", "Missing"), ("participants", "4"), ("best_of", "3"), ("double_elimination", "false") } },
        new object[] { "admin bracket seed", Array.Empty<(string, string)>() },
        new object[] { "admin draft lock", Array.Empty<(string, string)>() },
        new object[] { "admin draft unlock", Array.Empty<(string, string)>() },
        new object[] { "admin schedule show", Array.Empty<(string, string)>() },
        new object[] { "admin schedule run", Array.Empty<(string, string)>() },
        new object[] { "admin schedule apply", new[] { ("recommendation", "1") } },
        new object[] { "admin halo pull", new[] { ("match_id", "not-a-guid") } },
        new object[] { "admin jobs list", Array.Empty<(string, string)>() },
        new object[] { "admin jobs run", new[] { ("job", "a/b") } },
        new object[] { "admin jobs pause", new[] { ("job", "a/b"), ("paused", "true") } },
        new object[] { "admin claims list", Array.Empty<(string, string)>() },
        new object[] { "admin claims approve", new[] { ("claim", "1") } },
        new object[] { "admin claims reject", new[] { ("claim", "1"), ("reason", "No") } },
        new object[] { "matches", Array.Empty<(string, string)>() },
    ];

    /// <summary>Commands run by their own tests rather than <see cref="Commands"/>: /setgamertag needs a gamertag the site
    /// knows (<see cref="SlashCommand_SetGamertag_ReadsTheOptionAndSetsIt"/>).</summary>
    private static readonly string[] TestedOnTheirOwn = ["setgamertag"];

    [TestCaseSource(nameof(Commands))]
    public async Task SlashCommand_ReachesItsHandler_AndDefersFirst(string path, (string Name, string Value)[] options)
    {
        var requests = await _host.SendAsync(DiscordInteractionJson.SubCommand(path, 6001, options));

        // Reached the handler, which deferred before anything else (Discord gives an interaction 3 seconds): NetCord's own
        // failures (an unknown subcommand, an option it can't read) answer in the channel, and an exception gets the generic error
        using (Assert.EnterMultipleScope())
        {
            Assert.That(requests, Is.Not.Empty);
            Assert.That(requests[0].CallbackType, Is.EqualTo(DiscordRequest.DeferredChannelMessage), $"{path}: {requests[0].Body}");
            Assert.That(requests.Select(r => r.Content), Has.None.EqualTo(DiscordInteractionPipeline.GenericErrorMessage), path);
        }
    }

    [Test]
    public void EveryRegisteredCommand_IsSentByTheDeferTest()
    {
        using var scope = _host.Services.CreateScope();
        var registered = scope.ServiceProvider.GetRequiredService<HelpCommandsHandler>().Commands().Select(x => x.Path.TrimStart('/'));
        var sent = Commands.Cast<object[]>().Select(x => (string)x[0]).Concat(TestedOnTheirOwn);

        Assert.That(registered.Except(sent), Is.Empty, "add each to Commands with its required options");
    }

    [Test]
    public async Task AdminJobCommands_RunThroughTheScheduler_ForStaff()
    {
        var staff = await LinkedUser(6101, "JobStaff");
        var (match, recommendation) = await _host.WithContextAsync(async context =>
        {
            foreach (var role in await context.Roles.Where(r => r.Name == "Commissioner" || r.Name == "Sysadmin").ToListAsync())
                context.UserRoles.Add(new UserRole { UserId = staff.Id, RoleId = role.Id });
            var season = Season.Seed(seasonName: "Transport Jobs").WithDates(seasonStart: new DateTime(2025, 1, 1), seasonEnd: new DateTime(2025, 3, 1));
            context.Seasons.Add(season);
            await context.SaveChangesAsync();
            var home = Team.Seed(seasonID: season.SeasonID, teamName: "Jobs Home");
            var away = Team.Seed(seasonID: season.SeasonID, teamName: "Jobs Away");
            context.Teams.AddRange(home, away);
            await context.SaveChangesAsync();
            var seasonMatch = SeasonMatch.Seed(season.SeasonID, home.TeamID, away.TeamID);
            context.SeasonMatches.Add(seasonMatch);
            await context.SaveChangesAsync();
            // A recommendation the scheduler could have made, for show and apply
            var proposed = ScheduleRecommendation.Propose(season.SeasonID, "commish", 0.5, 10,
                [new ProposedMatchTime(seasonMatch.SeasonMatchID, new DateTime(2030, 1, 2, 20, 0, 0, DateTimeKind.Utc), false, true, true)]);
            context.ScheduleRecommendations.Add(proposed);
            await context.SaveChangesAsync();
            return (seasonMatch, proposed.ScheduleRecommendationID);
        });
        var season = match.SeasonID.ToString();

        var pull = await _host.SendAsync(DiscordInteractionJson.SubCommand("match pull", 6101, ("match", match.SeasonMatchID.ToString()), ("back_to_season_start", "false")));
        var run = await _host.SendAsync(DiscordInteractionJson.SubCommand("admin schedule run", 6101, ("season", season)));
        var show = await _host.SendAsync(DiscordInteractionJson.SubCommand("admin schedule show", 6101, ("season", season)));
        var apply = await _host.SendAsync(DiscordInteractionJson.SubCommand("admin schedule apply", 6101, ("recommendation", recommendation.ToString())));
        var jobs = await _host.SendAsync(DiscordInteractionJson.SubCommand("admin jobs list", 6101));
        var unknown = await _host.SendAsync(DiscordInteractionJson.SubCommand("admin jobs run", 6101, ("job", "nope/nothing")));
        // Run (then pause) a job the list shows: its first line is `group/name` · state
        var listing = jobs[^1].Data.GetProperty("embeds")[0].GetProperty("description").GetString()!;
        var listed = listing.StartsWith('`') ? listing[1..listing.IndexOf('`', 1)] : "";
        var ran = listed.Length > 0 ? await _host.SendAsync(DiscordInteractionJson.SubCommand("admin jobs run", 6101, ("job", listed))) : null;
        var paused = listed.Length > 0 ? await _host.SendAsync(DiscordInteractionJson.SubCommand("admin jobs pause", 6101, ("job", listed), ("paused", "true"))) : null;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(pull[^1].Content, Does.StartWith("The pull of this match's players' games "));
            Assert.That(run[^1].Content, Does.StartWith("The recommendation is "));
            Assert.That(show[^1].Data.GetProperty("embeds")[0].GetProperty("title").GetString(), Does.StartWith("Schedule ("));
            Assert.That(show[^1].Data.GetProperty("embeds")[0].GetProperty("description").GetString(), Does.StartWith($"Recommendation #{recommendation}, not applied").And.Contain("Jobs Home vs Jobs Away"));
            Assert.That(apply[^1].Content, Is.EqualTo("1 matches are scheduled."));
            Assert.That(jobs[^1].Data.GetProperty("embeds")[0].GetProperty("title").GetString(), Is.EqualTo("Jobs"));
            Assert.That(unknown[^1].Content, Is.EqualTo("There is no such job."));
            Assert.That(ran is null || ran[^1].Content is AdminCommandsHandler.Done || ran[^1].Content!.StartsWith("The job can't"), Is.True, listed);
            Assert.That(paused is null || paused[^1].Content is AdminCommandsHandler.Done || paused[^1].Content!.StartsWith("The job can't"), Is.True, listed);
            Assert.That(new[] { pull, run, show, jobs, unknown }.Select(r => r[0].CallbackType), Has.All.EqualTo(DiscordRequest.DeferredChannelMessage));
        }
    }

    [TestCase("season standings", "season", "Transport")]
    [TestCase("team info", "team", "Transport")]
    [TestCase("player profile", "player", "Transport")]
    [TestCase("admin match report", "match", "Transport")]
    [TestCase("reschedule request", "match", "Transport")]
    [TestCase("admin bracket seed", "season", "Transport")]
    [TestCase("me region", "region", "north")]
    public async Task Autocomplete_OnASubcommandOption_SuggestsChoices(string path, string option, string typed)
    {
        // Seeded once for the fixture's shared database: each case looks at the same season, teams, match and player
        await _host.WithContextAsync(async context =>
        {
            if (await context.Seasons.AnyAsync(s => s.SeasonName == "Transport Autocomplete"))
                return 0;
            var player = User.Seed(userName: "TransportPlayer", discordUser: DiscordUser.FromDiscord(6201, "TransportPlayer"),
                xboxUser: XboxUser.FromXbox(6201, "TransportPlayer"));
            context.Users.Add(player);
            var season = Season.Seed(seasonName: "Transport Autocomplete").WithDates(seasonStart: new DateTime(2025, 1, 1), seasonEnd: new DateTime(2025, 3, 1));
            context.Seasons.Add(season);
            await context.SaveChangesAsync();
            var home = Team.Seed(seasonID: season.SeasonID, teamName: "Transport Home");
            var away = Team.Seed(seasonID: season.SeasonID, teamName: "Transport Away");
            context.Teams.AddRange(home, away);
            await context.SaveChangesAsync();
            // /reschedule request lists a player's own matches
            await context.AddPlayers(home, player.Id);
            context.SeasonMatches.Add(SeasonMatch.Seed(season.SeasonID, home.TeamID, away.TeamID));
            await context.SaveChangesAsync();
            return 1;
        });

        var requests = await _host.SendAsync(DiscordInteractionJson.SubCommandAutocomplete(path, option, typed, 6201));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(requests[0].CallbackType, Is.EqualTo(DiscordRequest.AutocompleteResult));
            Assert.That(requests[0].Data.GetProperty("choices").GetArrayLength(), Is.Positive, path);
        }
    }

    [Test]
    public async Task SlashCommand_Signup_WithNothingGiven_RespondsWithTheSignupModal()
    {
        var player = await LinkedUser(6311, "SignupModaller");
        var season = await _host.WithContextAsync(async context =>
        {
            var role = await context.Roles.SingleAsync(r => r.Name == "Player");
            context.UserRoles.Add(new UserRole { UserId = player.Id, RoleId = role.Id });
            var now = DateTime.UtcNow;
            var open = Season.Seed(seasonName: "Transport Signup Modal").WithDates(signupsOpen: now.AddDays(-1), signupsClose: now.AddDays(5), draftStart: now.AddDays(6),
                seasonStart: now.AddDays(7), seasonEnd: now.AddDays(40), publicAt: now.AddDays(-2));
            context.Seasons.Add(open);
            await context.SaveChangesAsync();
            return open;
        });

        var requests = await _host.SendAsync(DiscordInteractionJson.SubCommand("signup", 6311, ("season", season.SeasonID.ToString())));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(Routes(requests), Is.EqualTo(new[] { Callback }));
            Assert.That(requests[0].CallbackType, Is.EqualTo(DiscordRequest.Modal));
            Assert.That(requests[0].Data.GetProperty("custom_id").GetString(), Is.EqualTo($"signup:{season.SeasonID}"));
        }
    }

    [Test]
    public async Task SlashCommand_MeRegion_WithNoRegion_OpensTheRegionModal_WhoseSubmitSetsIt()
    {
        var user = await LinkedUser(6321, "RegionPicker");

        var opened = await _host.SendAsync(DiscordInteractionJson.SubCommand("me region", 6321));
        var saved = await _host.SendAsync(DiscordInteractionJson.ModalWith("set_region",
            """[{"type":18,"id":1,"component":{"type":21,"id":2,"custom_id":"region","value":"2"}}]""", 6321));

        var region = await _host.WithContextAsync(context => context.Users.AsNoTracking().Where(u => u.Id == user.Id).Select(u => u.RegionID).SingleAsync());
        var radio = opened[0].Data.GetProperty("components")[0].GetProperty("component");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(opened[0].CallbackType, Is.EqualTo(DiscordRequest.Modal));
            Assert.That(opened[0].Data.GetProperty("custom_id").GetString(), Is.EqualTo("set_region"));
            Assert.That(radio.GetProperty("type").GetInt32(), Is.EqualTo(21), "a radio group");
            // The submit below sends this ID back
            Assert.That(radio.GetProperty("custom_id").GetString(), Is.EqualTo("region"));
            Assert.That(Routes(saved), Is.EqualTo(new[] { Callback, EditOriginal }));
            Assert.That(saved[1].Content, Is.EqualTo("Your region is set."));
            Assert.That(region, Is.EqualTo(2));
        }
    }

    [Test]
    public async Task RegionModal_SubmittedWithoutAPick_SaysThereIsNoSuchRegion()
    {
        await LinkedUser(6322, "RegionBlank");

        var requests = await _host.SendAsync(DiscordInteractionJson.ModalWith("set_region", "[]", 6322));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(Routes(requests), Is.EqualTo(new[] { Callback, EditOriginal }));
            Assert.That(requests[1].Content, Does.StartWith(PersonalCommandsHandler.NoSuchRegion));
        }
    }

    /// <summary>A user linked to Discord user <paramref name="discordId"/>, with a gamertag and site role <paramref name="role"/>.</summary>
    private async Task<User> LinkedUserWithRole(ulong discordId, string gamertag, string role)
    {
        var user = await LinkedUser(discordId, gamertag);
        await _host.WithContextAsync(async context =>
        {
            var found = await context.Roles.SingleAsync(r => r.Name == role);
            context.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = found.Id });
            return await context.SaveChangesAsync();
        });
        return user;
    }

    [Test]
    public async Task BracketCreate_WithoutDoubleElimination_OpensTheModal_WhoseSubmitMakesTheBracket()
    {
        await LinkedUserWithRole(6401, "BracketMaker", "Commissioner");
        var season = await _host.WithContextAsync(async context =>
        {
            // Dated: an undated season would reach /season list's year as -infinity on Postgres
            var created = Season.Seed(seasonName: "Transport Bracket Modal").WithDates(seasonStart: new DateTime(2025, 1, 1), seasonEnd: new DateTime(2025, 3, 1));
            context.Seasons.Add(created);
            await context.SaveChangesAsync();
            return created;
        });

        var opened = await _host.SendAsync(DiscordInteractionJson.SubCommand("admin bracket create", 6401,
            ("season", season.SeasonID.ToString()), ("participants", "4"), ("best_of", "3")));
        var saved = await _host.SendAsync(DiscordInteractionJson.ModalWith($"bracket_create:{season.SeasonID}:4:3",
            """[{"type":18,"id":1,"component":{"type":23,"id":2,"custom_id":"double_elimination","value":true}}]""", 6401));

        var matches = await _host.WithContextAsync(context => context.SeasonMatches.CountAsync(x => x.SeasonID == season.SeasonID && x.BracketMatch != null));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(opened[0].CallbackType, Is.EqualTo(DiscordRequest.Modal));
            Assert.That(opened[0].Data.GetProperty("custom_id").GetString(), Is.EqualTo($"bracket_create:{season.SeasonID}:4:3"));
            Assert.That(Routes(saved), Is.EqualTo(new[] { Callback, EditOriginal }));
            Assert.That(saved[1].Content, Is.EqualTo("The bracket is made. Seed it with /admin bracket seed, or set custom seeds on the site."));
            // Four teams single elimination is three matches: the loser bracket adds more
            Assert.That(matches, Is.GreaterThan(3));
        }
    }

    [Test]
    public async Task RescheduleDecide_WithoutApprove_RefusesARequestNotWaiting_AndTheModalsSubmitGoesToTheService()
    {
        await LinkedUserWithRole(6402, "Decider", "Commissioner");

        var opened = await _host.SendAsync(DiscordInteractionJson.SubCommand("reschedule decide", 6402, ("request", "424242")));
        var saved = await _host.SendAsync(DiscordInteractionJson.ModalWith("reschedule_decide:424242",
            """[{"type":18,"id":1,"component":{"type":21,"id":2,"custom_id":"decision","value":"approve"}},{"type":18,"id":3,"component":{"type":4,"id":4,"custom_id":"notes","value":"Fine"}}]""", 6402));
        var blankNotes = await _host.SendAsync(DiscordInteractionJson.ModalWith("reschedule_decide:424242",
            """[{"type":18,"id":1,"component":{"type":21,"id":2,"custom_id":"decision","value":"reject"}},{"type":18,"id":3,"component":{"type":4,"id":4,"custom_id":"notes","value":"  "}}]""", 6402));

        using (Assert.EnterMultipleScope())
        {
            // No request 424242 waits: said before a modal opens (a submit, below, still goes to the service, which refuses it)
            Assert.That(opened[0].CallbackType, Is.EqualTo(DiscordRequest.ChannelMessage));
            Assert.That(opened[0].Content, Is.EqualTo(AdminCommandsHandler.NoRequestWaiting));
            Assert.That(Routes(saved), Is.EqualTo(new[] { Callback, EditOriginal }));
            Assert.That(saved[1].Content, Is.EqualTo("Reschedule request not found"));
            Assert.That(blankNotes[1].Content, Is.EqualTo("Reschedule request not found"));
        }
    }

    /// <summary>A modal submit's text inputs, each in its Label, as Discord sends them.</summary>
    private static string TextInputs(params (string CustomId, string Value)[] inputs) => JsonSerializer.Serialize(inputs.Select((input, i) =>
        new { type = 18, id = (i * 2) + 1, component = new { type = 4, id = (i * 2) + 2, custom_id = input.CustomId, value = input.Value } }));

    /// <summary>An opened modal's text inputs, by their custom IDs: each one's starting value (null when empty).</summary>
    private static Dictionary<string, string?> ModalInputs(JsonElement modal) => Descendants(modal.GetProperty("components"))
        .Where(e => e.ValueKind == JsonValueKind.Object && e.TryGetProperty("custom_id", out _) && e.TryGetProperty("style", out _))
        .ToDictionary(e => e.GetProperty("custom_id").GetString()!, e => e.TryGetProperty("value", out var value) ? value.GetString() : null);

    /// <summary>A season match between two new teams, <paramref name="player"/> on the home team's roster, scheduled at <paramref name="time"/>.</summary>
    private Task<SeasonMatch> MatchFor(string name, User player, DateTime? time = null) => _host.WithContextAsync(async context =>
    {
        var season = Season.Seed(seasonName: name).WithDates(seasonStart: new DateTime(2025, 1, 1), seasonEnd: new DateTime(2025, 3, 1));
        context.Seasons.Add(season);
        await context.SaveChangesAsync();
        var home = Team.Seed(seasonID: season.SeasonID, teamName: $"{name} Home");
        var away = Team.Seed(seasonID: season.SeasonID, teamName: $"{name} Away");
        context.Teams.AddRange(home, away);
        await context.SaveChangesAsync();
        await context.AddPlayers(home, player.Id);
        var seasonMatch = SeasonMatch.Seed(season.SeasonID, home.TeamID, away.TeamID, scheduledTime: time);
        context.SeasonMatches.Add(seasonMatch);
        await context.SaveChangesAsync();
        return seasonMatch;
    });

    [Test]
    public async Task RescheduleRequest_WithoutAReason_OpensTheModal_WhoseSubmitMakesTheRequest()
    {
        var player = await LinkedUser(6501, "Mover");
        var match = await MatchFor("Transport Request Modal", player);
        var customId = $"reschedule_request:{match.SeasonMatchID}";

        var opened = await _host.SendAsync(DiscordInteractionJson.SubCommand("reschedule request", 6501, ("match", match.SeasonMatchID.ToString())));
        var blank = await _host.SendAsync(DiscordInteractionJson.ModalWith(customId, TextInputs(("reason", "  "), ("new_time", "")), 6501));
        var saved = await _host.SendAsync(DiscordInteractionJson.ModalWith(customId, TextInputs(("reason", "Away that week"), ("new_time", "2030-01-05 20:00")), 6501));

        var request = await _host.WithContextAsync(context => context.MatchReschedules.AsNoTracking().SingleAsync(r => r.SeasonMatchID == match.SeasonMatchID));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(opened[0].CallbackType, Is.EqualTo(DiscordRequest.Modal));
            Assert.That(opened[0].Data.GetProperty("custom_id").GetString(), Is.EqualTo(customId));
            Assert.That(ModalInputs(opened[0].Data).Keys, Is.EqualTo(RescheduleInputs));
            Assert.That(Routes(saved), Is.EqualTo(new[] { Callback, EditOriginal }));
            Assert.That(saved[1].Content, Is.EqualTo($"Your reschedule request is in, for <t:{new DateTimeOffset(2030, 1, 5, 20, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds()}>. A commissioner will decide it."));
            Assert.That((request.Reason, request.RequestedByUserID), Is.EqualTo(("Away that week", player.Id)));
            Assert.That(blank[0].Content, Is.EqualTo(PersonalCommandsHandler.NoReason), "spaces are no reason: asked again, nothing made");
        }
    }

    [Test]
    public async Task AdminMatchReportAndTime_WithoutTheirValue_OpenTheirModals_WhoseSubmitsGoToTheService()
    {
        var commissioner = await LinkedUserWithRole(6502, "MatchModals", "Commissioner");
        var match = await MatchFor("Transport Match Modals", commissioner, new DateTime(2030, 1, 2, 20, 0, 0, DateTimeKind.Utc));
        var id = match.SeasonMatchID;

        var openedReport = await _host.SendAsync(DiscordInteractionJson.SubCommand("admin match report", 6502, ("match", id.ToString()), ("replaces", "2")));
        var savedReport = await _host.SendAsync(DiscordInteractionJson.ModalWith($"match_report:{id}", TextInputs(("game_id", "not-a-guid"), ("replaces", "2")), 6502));
        var savedNoReplaced = await _host.SendAsync(DiscordInteractionJson.ModalWith($"match_report:{id}", TextInputs(("game_id", "not-a-guid"), ("replaces", "")), 6502));
        var savedReplacesNone = await _host.SendAsync(DiscordInteractionJson.ModalWith($"match_report:{id}", TextInputs(("game_id", "not-a-guid"), ("replaces", "0")), 6502));
        var blankGame = await _host.SendAsync(DiscordInteractionJson.ModalWith($"match_report:{id}", TextInputs(("game_id", " "), ("replaces", "")), 6502));
        var notANumber = await _host.SendAsync(DiscordInteractionJson.ModalWith($"match_report:{id}", TextInputs(("game_id", "not-a-guid"), ("replaces", "two")), 6502));
        var blankTime = await _host.SendAsync(DiscordInteractionJson.ModalWith($"match_time:{id}", TextInputs(("time", " ")), 6502));
        var openedTime = await _host.SendAsync(DiscordInteractionJson.SubCommand("admin match time", 6502, ("match", id.ToString())));
        var savedTime = await _host.SendAsync(DiscordInteractionJson.ModalWith($"match_time:{id}", TextInputs(("time", "2030-01-03 20:00")), 6502));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(openedReport[0].CallbackType, Is.EqualTo(DiscordRequest.Modal));
            Assert.That(openedReport[0].Data.GetProperty("custom_id").GetString(), Is.EqualTo($"match_report:{id}"));
            Assert.That(ModalInputs(openedReport[0].Data)["replaces"], Is.EqualTo("2"));
            // Not a Halo match ID: refused before Halo is asked
            Assert.That(savedReport[1].Content, Is.EqualTo(MatchCommandsHandler.NoSuchGame));
            Assert.That(savedNoReplaced[1].Content, Is.EqualTo(MatchCommandsHandler.NoSuchGame));
            Assert.That(savedReplacesNone[1].Content, Is.EqualTo(MatchCommandsHandler.NoSuchGame));
            Assert.That(blankGame[0].Content, Is.EqualTo(AdminCommandsHandler.NoGameId));
            Assert.That(notANumber[0].Content, Is.EqualTo(AdminCommandsHandler.ReplacesNotANumber), "not added as an extra game");
            Assert.That(blankTime[0].Content, Is.EqualTo(AdminCommandsHandler.NoTime));
            Assert.That(openedTime[0].Data.GetProperty("custom_id").GetString(), Is.EqualTo($"match_time:{id}"));
            Assert.That(ModalInputs(openedTime[0].Data)["time"], Is.EqualTo("2030-01-02 20:00"), "the time it has now");
            Assert.That(savedTime[1].Content, Does.StartWith($"The match is set for <t:{new DateTimeOffset(2030, 1, 3, 20, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds()}>."));
        }
    }

    [Test]
    public async Task AdminClaimsReject_WithoutAReason_OpensTheModal_WhoseEmptySubmitRejectsWithNone()
    {
        await LinkedUserWithRole(6503, "ClaimRejecter", "Sysadmin");
        var claimant = await LinkedUser(6504, "Claimant6504");
        var (claimID, withReasonID) = await _host.WithContextAsync(async context =>
        {
            var placeholder = User.Seed(userName: "OldTag6504");
            var other = User.Seed(userName: "OtherTag6504");
            context.Users.AddRange(placeholder, other);
            await context.SaveChangesAsync();
            var claim = GrifballWebApp.Domain.Archive.GamertagClaim.ClaimedByUser(claimant.Id, null, "OldTag6504", "OLDTAG6504", placeholder.Id, null, null);
            var withReason = GrifballWebApp.Domain.Archive.GamertagClaim.ClaimedByUser(claimant.Id, null, "OtherTag6504", "OTHERTAG6504", other.Id, null, null);
            context.GamertagClaims.AddRange(claim, withReason);
            await context.SaveChangesAsync();
            return (claim.GamertagClaimID, withReason.GamertagClaimID);
        });

        var opened = await _host.SendAsync(DiscordInteractionJson.SubCommand("admin claims reject", 6503, ("claim", claimID.ToString())));
        var notPending = await _host.SendAsync(DiscordInteractionJson.SubCommand("admin claims reject", 6503, ("claim", "424242")));
        var saved = await _host.SendAsync(DiscordInteractionJson.ModalWith($"claim_reject:{claimID}", TextInputs(("reason", " ")), 6503));
        var savedWithReason = await _host.SendAsync(DiscordInteractionJson.ModalWith($"claim_reject:{withReasonID}", TextInputs(("reason", "Not them")), 6503));

        var reason = await _host.WithContextAsync(context => context.GamertagClaims.AsNoTracking().Where(c => c.GamertagClaimID == claimID).Select(c => c.RejectionReason).SingleAsync());
        var given = await _host.WithContextAsync(context => context.GamertagClaims.AsNoTracking().Where(c => c.GamertagClaimID == withReasonID).Select(c => c.RejectionReason).SingleAsync());
        using (Assert.EnterMultipleScope())
        {
            Assert.That(opened[0].CallbackType, Is.EqualTo(DiscordRequest.Modal));
            Assert.That(opened[0].Data.GetProperty("custom_id").GetString(), Is.EqualTo($"claim_reject:{claimID}"));
            Assert.That(saved[1].Content, Is.EqualTo("Rejected."));
            Assert.That(reason, Is.Null);
            Assert.That(savedWithReason[1].Content, Is.EqualTo("Rejected."));
            Assert.That(given, Is.EqualTo("Not them"));
            Assert.That(notPending[0].Content, Is.EqualTo(AdminCommandsHandler.NoClaimPending), "said before a reason is written");
        }
    }

    [Test]
    public async Task JobsPause_WithoutPaused_OpensTheModal_WhoseSubmitGoesToTheScheduler()
    {
        await LinkedUserWithRole(6403, "JobPauser", "Sysadmin");

        var opened = await _host.SendAsync(DiscordInteractionJson.SubCommand("admin jobs pause", 6403, ("job", "nope/nothing")));
        var saved = await _host.SendAsync(DiscordInteractionJson.ModalWith("job_pause:nope/nothing",
            """[{"type":18,"id":1,"component":{"type":21,"id":2,"custom_id":"state","value":"resume"}}]""", 6403));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(opened[0].CallbackType, Is.EqualTo(DiscordRequest.Modal));
            Assert.That(Routes(saved), Is.EqualTo(new[] { Callback, EditOriginal }));
            Assert.That(saved[1].Content, Is.EqualTo("There is no such job."));
        }
    }

    [Test]
    public async Task MatchPull_ModalSubmit_GoesThroughTheCommandsChecks()
    {
        await LinkedUserWithRole(6404, "ModalPuller", "Player");

        var refused = await _host.SendAsync(DiscordInteractionJson.SubCommand("match pull", 6404, ("match", "Missing")));
        var saved = await _host.SendAsync(DiscordInteractionJson.ModalWith("match_pull:999999",
            """[{"type":18,"id":1,"component":{"type":23,"id":2,"custom_id":"back_to_season_start","value":false}}]""", 6404));

        using (Assert.EnterMultipleScope())
        {
            // No such match: said in a new reply, as the modal would have been the first response
            Assert.That(refused[0].CallbackType, Is.EqualTo(DiscordRequest.ChannelMessage));
            Assert.That(refused[0].Content, Is.EqualTo(MatchCommandsHandler.NoSuchMatch));
            Assert.That(Routes(saved), Is.EqualTo(new[] { Callback, EditOriginal }));
            Assert.That(saved[1].Content, Is.EqualTo(MatchCommandsHandler.NoSuchMatch));
        }
    }

    /// <summary>Review finding 2 (#390): /signup with one option changed keeps the ones left out, as Discord leaves them out.</summary>
    [Test]
    public async Task SlashCommand_Signup_KeepsWhatItWasNotGiven()
    {
        var player = await LinkedUser(6301, "SignupKeeper");
        var season = await _host.WithContextAsync(async context =>
        {
            var role = await context.Roles.SingleAsync(r => r.Name == "Player");
            context.UserRoles.Add(new UserRole { UserId = player.Id, RoleId = role.Id });
            var now = DateTime.UtcNow;
            var open = Season.Seed(seasonName: "Transport Signups").WithDates(signupsOpen: now.AddDays(-1), signupsClose: now.AddDays(5), draftStart: now.AddDays(6),
                seasonStart: now.AddDays(7), seasonEnd: now.AddDays(40), publicAt: now.AddDays(-2));
            context.Seasons.Add(open);
            await context.SaveChangesAsync();
            return open;
        });
        var id = season.SeasonID.ToString();

        await _host.SendAsync(DiscordInteractionJson.SubCommand("signup", 6301, ("season", id), ("team_name", "Bombers"), ("will_captain", "true")));
        await _host.SendAsync(DiscordInteractionJson.SubCommand("signup", 6301, ("season", id), ("draft_help", "true")));

        var signup = await _host.WithContextAsync(context => context.SeasonSignups.AsNoTracking().SingleAsync(x => x.SeasonID == season.SeasonID && x.UserID == player.Id));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(signup.TeamName, Is.EqualTo("Bombers"));
            Assert.That(signup.WillCaptain, Is.True);
            Assert.That(signup.RequiresAssistanceDrafting, Is.True);
        }
    }

    [Test]
    public async Task Voice_OptInAndOut_AreKeptOnTheirDiscordAccount_AndSaidToThemAlone()
    {
        var optIn = await _host.SendAsync(DiscordInteractionJson.SubCommand("voice optin", 7001));
        var heardAfterOptIn = await _host.WithContextAsync(c => c.VoiceConsents.AnyAsync(v => v.DiscordUserID == 7001));
        var optOut = await _host.SendAsync(DiscordInteractionJson.SubCommand("voice optout", 7001));
        var heardAfterOptOut = await _host.WithContextAsync(c => c.VoiceConsents.AnyAsync(v => v.DiscordUserID == 7001));

        using (Assert.EnterMultipleScope())
        {
            Assert.That((heardAfterOptIn, heardAfterOptOut), Is.EqualTo((true, false)));
            Assert.That(Routes(optIn), Is.EqualTo(new[] { Callback, EditOriginal }));
            Assert.That(optIn[0].Flags, Is.EqualTo(MessageFlags.Ephemeral), "Theirs alone");
            Assert.That(optIn[1].Content, Is.EqualTo(VoiceCommandsHandler.OptedIn));
            Assert.That(optOut[1].Content, Is.EqualTo(VoiceCommandsHandler.OptedOut));
        }
    }

    [Test]
    public async Task Voice_JoinWithNoSpeechServer_SaysVoiceIsntSetUp_AndLeaveOutsideAServer_SaysWhere()
    {
        var join = await _host.SendAsync(DiscordInteractionJson.SubCommand("voice join", 7002));
        var leave = await _host.SendAsync(DiscordInteractionJson.SubCommand("voice leave", 7002));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(join[1].Content, Is.EqualTo(VoiceCommandsHandler.Unavailable));
            Assert.That(leave[1].Content, Is.EqualTo(VoiceCommandsHandler.NoGuild));
        }
    }

    /// <summary>The custom IDs of a message's buttons and menus, in order.</summary>
    private static List<string> ComponentIds(JsonElement message) => Descendants(message.GetProperty("components"))
        .Where(e => e.ValueKind == JsonValueKind.Object && e.TryGetProperty("custom_id", out _))
        .Select(e => e.GetProperty("custom_id").GetString()!)
        .ToList();

    private static JsonElement FirstEmbed(JsonElement message) => message.GetProperty("embeds")[0];

    [Test]
    public async Task SeasonSignups_PageWithTheirButtons_AndTheSeasonMenuSwitchesSeason()
    {
        await LinkedUser(7301, "Pager7301");
        var (season, other) = await _host.WithContextAsync(async context =>
        {
            var paged = Season.Seed(seasonName: "Transport Paged").WithDates(seasonStart: new DateTime(2025, 1, 1), seasonEnd: new DateTime(2025, 3, 1));
            var unpaged = Season.Seed(seasonName: "Transport Unpaged").WithDates(seasonStart: new DateTime(2025, 1, 1), seasonEnd: new DateTime(2025, 3, 1));
            context.Seasons.AddRange(paged, unpaged);
            await context.SaveChangesAsync();
            for (var i = 0; i < 12; i++)
            {
                var signer = User.Seed(userName: $"Signer7301x{i}", xboxUser: XboxUser.FromXbox(730100 + i, $"Signer7301x{i}"));
                context.Users.Add(signer);
                await context.SaveChangesAsync();
                await context.AddSignup(paged, signer);
            }
            return (paged.SeasonID, unpaged.SeasonID);
        });

        var first = await _host.SendAsync(DiscordInteractionJson.SubCommand("season signups", 7301, ("season", season.ToString())));
        var second = await _host.SendAsync(DiscordInteractionJson.Button($"page:signups:{season}:1", 7301));
        var switched = await _host.SendAsync(DiscordInteractionJson.StringMenu("seasonview:signups", 7301, $"id:{other}"));
        var gone = await _host.SendAsync(DiscordInteractionJson.StringMenu("seasonview:signups", 7301, "id:999999"));
        var noSuchView = await _host.SendAsync(DiscordInteractionJson.Button($"page:nope:{season}:0", 7301));
        var noSuchSeason = await _host.SendAsync(DiscordInteractionJson.Button("page:signups:999999:0", 7301));

        var reply = first[1].Data;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Routes(first), Is.EqualTo(new[] { Callback, EditOriginal }));
            Assert.That(FirstEmbed(reply).GetProperty("description").GetString()!.Split('\n'), Has.Length.EqualTo(CommandViews.PageSize));
            Assert.That(FirstEmbed(reply).GetProperty("footer").GetProperty("text").GetString(), Is.EqualTo("Page 1 of 2"));
            Assert.That(ComponentIds(reply), Is.EqualTo(new[] { "seasonview:signups", $"page:signups:{season}:-1", $"page:signups:{season}:1" }));
            Assert.That(second[0].CallbackType, Is.EqualTo(DiscordRequest.UpdateMessage));
            Assert.That(FirstEmbed(second[0].Data).GetProperty("description").GetString()!.Split('\n'), Has.Length.EqualTo(2));
            Assert.That(FirstEmbed(second[0].Data).GetProperty("footer").GetProperty("text").GetString(), Is.EqualTo("Page 2 of 2"));
            Assert.That(FirstEmbed(switched[0].Data).GetProperty("title").GetString(), Is.EqualTo("Transport Unpaged signups: 0"));
            Assert.That(ComponentIds(switched[0].Data), Is.EqualTo(OnlyTheSeasonMenu), "one page: no ◀ ▶");
            Assert.That(gone[0].Content, Is.EqualTo(CommandViewHandler.Gone));
            Assert.That(noSuchView[0].Content, Is.EqualTo(CommandViewHandler.Gone));
            Assert.That(noSuchSeason[0].Content, Is.EqualTo(CommandViewHandler.Gone));
        }
    }

    [Test]
    public async Task TheStatAndPlayerViewMenus_AndTheRosterPager_ShowWhatWasPicked()
    {
        var player = await LinkedUser(7302, "Viewer7302");
        var (season, team) = await _host.WithContextAsync(async context =>
        {
            var created = Season.Seed(seasonName: "Transport Views").WithDates(seasonStart: new DateTime(2025, 1, 1), seasonEnd: new DateTime(2025, 3, 1));
            context.Seasons.Add(created);
            await context.SaveChangesAsync();
            var roster = Team.Seed(seasonID: created.SeasonID, teamName: "Transport Views Team");
            context.Teams.Add(roster);
            await context.SaveChangesAsync();
            await context.AddPlayers(roster, player.Id);
            return (created.SeasonID, roster.TeamID);
        });

        var kills = await _host.SendAsync(DiscordInteractionJson.StringMenu($"seasonstat:{season}", 7302, "Kills"));
        var noSuchStat = await _host.SendAsync(DiscordInteractionJson.StringMenu($"seasonstat:{season}", 7302, "Nope"));
        var roster = await _host.SendAsync(DiscordInteractionJson.Button($"page:roster:{team}:0", 7302));
        var badKey = await _host.SendAsync(DiscordInteractionJson.Button("page:roster:abc:0", 7302));
        var history = await _host.SendAsync(DiscordInteractionJson.StringMenu($"playerview:{player.Id}", 7302, PlayerCommandsHandler.HistoryView));
        var noSuchView = await _host.SendAsync(DiscordInteractionJson.StringMenu($"playerview:{player.Id}", 7302, "nope"));
        var notAStat = await _host.SendAsync(DiscordInteractionJson.StringMenu($"seasonstat:{season}", 7302, "7"));
        var noSuchTeam = await _host.SendAsync(DiscordInteractionJson.Button("page:roster:999999:0", 7302));
        var noGamertag = await _host.WithContextAsync(async context =>
        {
            var user = User.Seed(userName: "NoGamertag7302");
            context.Users.Add(user);
            await context.SaveChangesAsync();
            return user.Id;
        });
        var unnamed = await _host.SendAsync(DiscordInteractionJson.StringMenu($"playerview:{noGamertag}", 7302, PlayerCommandsHandler.ProfileView));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(notAStat[0].Content, Is.EqualTo(CommandViewHandler.Gone), "a number no stat has");
            Assert.That(noSuchTeam[0].Content, Is.EqualTo(CommandViewHandler.Gone));
            Assert.That(unnamed[0].Content, Is.EqualTo(CommandViewHandler.Gone), "no gamertag to name them by");
            Assert.That(kills[0].CallbackType, Is.EqualTo(DiscordRequest.UpdateMessage));
            Assert.That(FirstEmbed(kills[0].Data).GetProperty("title").GetString(), Is.EqualTo("Transport Views: top Kills"));
            Assert.That(ComponentIds(kills[0].Data), Is.EqualTo(new[] { "seasonview:stats-Kills", $"seasonstat:{season}" }));
            Assert.That(noSuchStat[0].Content, Is.EqualTo(CommandViewHandler.Gone));
            Assert.That(FirstEmbed(roster[0].Data).GetProperty("title").GetString(), Is.EqualTo("Transport Views Team roster"));
            Assert.That(badKey[0].Content, Is.EqualTo(CommandViewHandler.Gone));
            Assert.That(FirstEmbed(history[0].Data).GetProperty("title").GetString(), Is.EqualTo("Viewer7302 deployment history"));
            Assert.That(ComponentIds(history[0].Data), Is.EqualTo(new[] { $"playerview:{player.Id}" }));
            Assert.That(noSuchView[0].Content, Is.EqualTo(CommandViewHandler.Gone));
        }
    }

    [Test]
    public async Task ThePendingListsButtons_ApproveAtOnce_AndRejectOpensItsModal()
    {
        await LinkedUserWithRole(7303, "Decider7303", "Commissioner");
        await LinkedUserWithRole(7304, "ClaimDecider7304", "Sysadmin");
        var player = await LinkedUser(7305, "Mover7305");
        var match = await MatchFor("Transport Pending", player);
        var (requestID, claimID) = await _host.WithContextAsync(async context =>
        {
            var tracked = await context.SeasonMatches.Include(m => m.Reschedules).SingleAsync(m => m.SeasonMatchID == match.SeasonMatchID);
            var request = MatchReschedule.Request(tracked, new DateTime(2025, 2, 1, 20, 0, 0, DateTimeKind.Utc), "Away", player.Id, DateTime.UtcNow).Reschedule!;
            var placeholder = User.Seed(userName: "OldTag7305");
            context.Users.Add(placeholder);
            await context.SaveChangesAsync();
            var claim = GrifballWebApp.Domain.Archive.GamertagClaim.ClaimedByUser(player.Id, null, "OldTag7305", "OLDTAG7305", placeholder.Id, null, null);
            context.GamertagClaims.Add(claim);
            await context.SaveChangesAsync();
            return (request.MatchRescheduleID, claim.GamertagClaimID);
        });

        var pending = await _host.SendAsync(DiscordInteractionJson.SubCommand("reschedule pending", 7303));
        var claims = await _host.SendAsync(DiscordInteractionJson.SubCommand("admin claims list", 7304));
        var rejectRequest = await _host.SendAsync(DiscordInteractionJson.Button($"reschedulereject:{requestID}", 7303));
        var rejectClaim = await _host.SendAsync(DiscordInteractionJson.Button($"claimreject:{claimID}", 7304));
        var approveRequest = await _host.SendAsync(DiscordInteractionJson.Button($"rescheduleapprove:{requestID}", 7303));
        var approveNoClaim = await _host.SendAsync(DiscordInteractionJson.Button("claimapprove:424242", 7304));

        var preset = Descendants(rejectRequest[0].Data)
            .Where(e => e.ValueKind == JsonValueKind.Object && e.TryGetProperty("value", out var v) && v.ValueKind == JsonValueKind.String && v.GetString() == "reject")
            .Select(e => e.TryGetProperty("default", out var d) && d.GetBoolean())
            .Single();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(ComponentIds(pending[1].Data), Does.Contain($"rescheduleapprove:{requestID}").And.Contain($"reschedulereject:{requestID}"));
            Assert.That(ComponentIds(claims[1].Data), Does.Contain($"claimapprove:{claimID}").And.Contain($"claimreject:{claimID}"));
            Assert.That(rejectRequest[0].CallbackType, Is.EqualTo(DiscordRequest.Modal));
            Assert.That(rejectRequest[0].Data.GetProperty("custom_id").GetString(), Is.EqualTo($"reschedule_decide:{requestID}"));
            Assert.That(preset, Is.True, "Reject picked: the notes are what's left to write");
            Assert.That(rejectClaim[0].Data.GetProperty("custom_id").GetString(), Is.EqualTo($"claim_reject:{claimID}"));
            Assert.That(approveRequest[^1].Content, Is.EqualTo("Approved."));
            Assert.That(approveNoClaim[^1].Content, Does.Contain("424242"), "the service's refusal");
        }
    }
}
