# MCP server

The site runs an [MCP](https://modelcontextprotocol.io) server, so AI agents such as Claude Code or Claude Desktop can work
with the platform directly. It is at `/api/mcp` on each environment (for example `https://grifball.xyz/api/mcp`), and uses
streamable HTTP in stateless mode, so any API pod answers any request. The code is in `GrifballWebApp.Server/Mcp`, and the
tools are in `GrifballWebApp.Application/Tools`.

The tools aren't the MCP server's own. They are one set for every host:

- the MCP server, which serves them all;
- the Discord bot's chat, which gets `GrifTools.Chat` (see [The chat hosts](#the-chat-hosts));
- the site's chat (`ChatHub`), which gets the same list.

## Signing in

A client signs in with a **personal access token**, and acts as the user who made it, with that user's roles as they are
at the time of each call. Everything it reads or does shows up as that user.

- Make a token on your profile page, under Access tokens. The site shows it once. It lasts 1 to 365 days (90 by default),
  and you can have up to 10.
- A token opens the MCP endpoint only. The rest of the API doesn't accept it, so a token can't make more tokens or change
  your account. The browser's sign-in cookie doesn't open the MCP endpoint either.
- Revoke a token on the same page. It stops working at once.
- Only the token's SHA-256 is kept, in Identity's user tokens (`Auth.UserTokens`, login provider `Grifball.AccessToken`).

To add it to Claude Code:

```sh
claude mcp add --transport http grifball https://grifball.xyz/api/mcp --header "Authorization: Bearer grif_..."
```

## Tools

The tools show no more than the site shows the caller (signed out, for most; staff's tools are for staff, below). All of
them only read, except staff's actions (below) and the pull tools (`pull_season_match_games`, `pull_xbox_account_games`). They queue a page's pull from Halo
(a season match's, an Xbox account's), a background job that saves the games it finds. Only players, commissioners and sysadmins may use them, as on the pages (see [Roles](#roles)). They are marked
as not read-only, so MCP clients ask before calling them. They never change or delete anything. Tools take IDs: `search` finds players, teams and seasons by name, and `list_seasons` lists seasons. A season tool
called without a `seasonID` uses the running season, else the one that ended last (`get_season_signups`: the next to
start), as the slash commands do; a player tool called without a `userID` uses the caller. A season not announced yet,
its teams and its matches are no such thing to anyone but a commissioner or sysadmin; the Discord bot's chat answers in
the channel, so it uses the public view whoever asks (`ToolCaller.InPublic`, #435).

| Tool | What it returns |
| --- | --- |
| `who_am_i` | Your user ID, gamertag and roles |
| `search` | Players, teams and seasons matching a name or year |
| `list_seasons` | The seasons, 25 to a page |
| `get_season` | A season's summary and champions |
| `get_season_standings` | Each team's record |
| `get_season_schedule` | Scheduled and unscheduled matches |
| `get_season_player_stats` | Each player's stats, or one team's |
| `get_season_grades` | Players' totals, per-minute rates and letter grades |
| `get_season_signups` | Who signed up |
| `get_season_bracket` | The playoff bracket |
| `get_team` | A team's season, record, titles and roster |
| `get_team_matches` | A team's matches and results |
| `get_team_history` | A franchise's seasons, records and titles |
| `get_team_vods` | Videos of a team's matches |
| `get_player` | A player's profile and career stats |
| `get_player_commendations` | A player's medals |
| `get_player_history` | A player's teams by season |
| `get_player_vods` | Videos a player is in |
| `get_player_rank` | A player's all-time leaderboard ranks |
| `get_season_match` | A season match, its result and its games |
| `get_game` | A recorded Halo game's teams, players and medals |
| `get_leaderboards` | The all-time leaderboards |
| `list_late_league_seasons` | Late League's seasons |
| `get_late_league_season` | A Late League season's player stats |
| `get_recent_games` | A player's latest Grifball custom games as the site saved them, each with the player's outcome and its Waypoint link (`/matches` shows the same) |
| `pull_season_match_games` | Queues the pull of a season match's players' games from Halo (players and staff; `/match pull` too) |
| `pull_xbox_account_games` | Queues the pull of an Xbox account's recent games from Halo (players and staff; `/player pull` too) |
| `get_current_time` | The time now, in UTC and as Unix seconds |

### Staff's tools

The MCP server lists these to the roles each names alone, as their pages and `/admin` are; the Discord bot's chat, which
answers in a public channel, doesn't get them. They call the same services as the pages and `/admin`. The actions are
marked as changing data (not read-only, destructive), so MCP clients ask before calling them, and a service's refusal
(a match already reported, a claim decided) is the tool's error, with the reason the site gives. Jobs and schedule runs
reach Infrastructure through Application ports (`IJobControl`, `IScheduleRecommendationRuns`); what a staff tool records as
done by someone (a schedule applied, a job paused) is the token's user name (`ToolCaller.Name`), as the site records it.

| Tool | Roles | What it does |
| --- | --- | --- |
| `get_commissioner_dashboard` | Commissioner, Sysadmin | Pending reschedules, overdue matches, seasons that won't be public by their signups, and the counts |
| `list_pending_gamertag_claims` | Sysadmin | The claims waiting for a decision |
| `decide_reschedule` | Commissioner, Sysadmin | Approves or rejects a reschedule request |
| `report_season_match_game` | Commissioner | Reports a Halo game as one of a season match's games |
| `forfeit_season_match` | Commissioner | Home, away or both forfeit |
| `undo_season_match_result` | Commissioner | Undoes a match's result |
| `set_season_match_time` | Commissioner | Schedules a match, in UTC |
| `generate_season_matches` | Commissioner | Makes a season's matches (refused once one is played) |
| `create_season_bracket` | Commissioner | Makes the playoff bracket (refused once it's played) |
| `seed_season_bracket` | Commissioner | Seeds the bracket by the standings |
| `set_captains_locked` | Commissioner | Locks or unlocks the draft's captains |
| `decide_gamertag_claim` | Sysadmin | Approves or rejects a gamertag claim |
| `pull_halo_game` | Sysadmin | Pulls one Halo game's stats; refused when Halo has no such team game |
| `refresh_medals` | Sysadmin | Downloads Halo's medals, once (refused once they're downloaded) |
| `get_schedule_recommendation` | Commissioner, Sysadmin | A season's latest schedule recommendation, and whether one is being worked out |
| `run_schedule_recommendation` | Commissioner, Sysadmin | Starts working one out (a background job) |
| `apply_schedule_recommendation` | Commissioner, Sysadmin | Schedules the matches as one suggests |
| `list_jobs` | Sysadmin | The background jobs and their state |
| `run_job` | Sysadmin | Runs a job now |
| `set_job_paused` | Sysadmin | Pauses or resumes a job's schedule |

The tools that call Halo themselves (`pull_halo_game`, `refresh_medals`, `report_season_match_game`) carry `[ToolReachesHalo]`, so
the MCP server marks them open-world, as it does the pulls players queue.

A refusal the model can act on (no such season, say) is a `ToolException`. The MCP server returns it as a tool error with
its reason, as it does a service's refusal the site answers with a 4xx (`GlobalExceptionHandler.Expected`). The chat hosts return it as the tool's result, `Refused: …`. Any other error's details stay in the server's
logs.

## The chat hosts

Both chats run through one service, `GrifChat` (`GrifballWebApp.Application/Chat`), written against Microsoft.Extensions.AI
alone. A host keeps the conversation, passes the caller and its `ChatHost`, and shows the streamed reply. `GrifChat` does the rest:
- the instructions: the lines every host shares, between the host's own first and last line;
- the history: the conversation's last 25 messages (`GrifChat.HistoryLimit`), each cut to 4000 characters;
- the tools: `GrifTools.AIFunctions` with a scope factory (each call runs in a scope of its own, so a tool never shares a
  `DbContext` with the host), the caller (`ToolCaller`, `InPublic` as the host says) and `GrifTools.Chat`;
- the tool calls: it calls the tools the model asks for (`UseFunctionInvocation`) until it answers;
- the limits: a question of 2000 characters at most (a Discord message's limit), and the host's timeout.

The model is `IChatClientFactory`'s, which Infrastructure registers (`OllamaChatClients`: llama3.1 on Ollama, at
`Ollama:Url`). It gives only the model, so another provider is another `IChatClientFactory` registered in its place.

A small local model picks worse from a long list, so the chats get `GrifTools.Chat`, the questions people ask most. The MCP
server's clients get every tool.

- **The Discord bot** (`MessageCreateHandler`, `ChatHost.Discord`): the caller is the author's linked site account, with its roles
  (`DiscordCaller.AsToolCaller`).
  - An author with no linked account is `ToolCaller.Anonymous`. They can use every tool that isn't about "you", and those
    that are refuse and ask them to name the player.
  - It answers in the channel, so it is `InPublic`, and its reply has no time limit. The conversation is kept in
    `DiscordMessages`.
  - The bot's instructions have it write times in UTC, as the tools give them. A small model can't reliably turn ISO times into
    Discord's epoch timestamps.
- **The site's chat** (`ChatHub` at `hub/ChatHub`, the `/chat` page, `ChatHost.Site`): the reply streams to the browser over
  SignalR.
  - The caller is the signed-in user with their roles. Signed out, it is `ToolCaller.Anonymous`, as for a Discord author with
    no linked account.
  - The reply is the user's alone, so it isn't `InPublic`: staff see seasons not announced yet, as the site shows them. It gets
    `GrifTools.SiteChat`: the chat tools and staff's (`GrifTools.Staff`, [Staff's tools](#staffs-tools)), each for its roles
    as the MCP server gives them, so a player gets none of staff's and a commissioner or sysadmin gets theirs. Its instructions
    have it use a tool that changes data only when the person asks for that change.
  - The browser keeps the conversation (the tab's `sessionStorage`) and sends it with each question. The server keeps none.
  - A reply that takes over 2 minutes is given up.
  - `ChatLimits`: one reply at a time per connection, and 10 questions a minute per signed-in user, 5 per address signed out
    (counted on each API pod).

## Adding a tool

A tool is a public method with a `[Description]` on a class in `GrifballWebApp.Application/Tools`, and each of its
parameters has a `[Description]` too. Its name is the method's name in snake_case. It calls the same service as the
controller for that page and returns its DTO. A class whose constructor takes a `ToolCaller` gets the signed-in user. A new
class is listed in `GrifTools.ReadOnly`, in `GrifTools.QueuingHaloPulls` if it queues a pull from Halo, or in
`GrifTools.ChangingData` if it changes the league's data (staff's actions). A tool that chats
should have is named in `GrifTools.Chat`. `GrifToolsTests` checks the descriptions and names.

## Roles

A tool only some roles may use has `[ToolRoles(...)]` on its method or class, naming the roles its site endpoint allows. Any one of
them will do. A caller without one doesn't get the tool:
- `GrifTools.AIFunctions` leaves it out of a chat's tools, so an anonymous Discord user never sees it.
- The MCP server leaves it out of the user's tool list, and refuses a call to it anyway.
- The tool also checks itself (`ToolCaller.Require`, or `ToolCaller.RequireRolesOf()` for its own `[ToolRoles]`), so a
  host's filtering can't be the only guard.
