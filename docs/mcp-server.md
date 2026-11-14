# MCP server

The site runs an [MCP](https://modelcontextprotocol.io) server, so AI agents such as Claude Code or Claude Desktop can work
with the platform directly. It is at `/api/mcp` on each environment (for example `https://grifball.xyz/api/mcp`), and uses
streamable HTTP in stateless mode, so any API pod answers any request. The code is in `GrifballWebApp.Server/Mcp`, and the
tools are in `GrifballWebApp.Application/Tools`.

The tools aren't the MCP server's own. They are one set for every host:

- the MCP server, which serves them all;
- the Discord bot's chat, which gets `GrifTools.Chat` (see [The chat hosts](#the-chat-hosts));
- the site's chat, still to be built, which will get the same list.

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

The tools show no more than the site shows signed out (or, for `get_season_match`, shows the caller). All of them only
read, except `get_recent_games`: it pulls the player's latest games from Halo and saves them first. It is marked as not
read-only, so MCP clients ask before calling it. It never changes or deletes anything. Tools take IDs: `search` finds players, teams and seasons by name, and `list_seasons` lists seasons. A season tool
called without a `seasonID` uses the running season, and a player tool called without a `userID` uses the caller.

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
| `get_recent_games` | A player's latest Grifball custom games, pulled fresh from Halo, each with its Waypoint link |
| `get_current_time` | The time now, in UTC and as Unix seconds |

A refusal the model can act on (no such season, say) is a `ToolException`. The MCP server returns it as a tool error with
its reason. The chat hosts return it as the tool's result, `Refused: …`. Any other error's details stay in the server's
logs.

## The chat hosts

A chat host builds its tools with `GrifTools.AIFunctions`, the Microsoft.Extensions.AI functions its model calls. It passes
three things:
- a scope factory: each call runs in a scope of its own, so a tool never shares a `DbContext` with the host;
- the caller (`ToolCaller`);
- the names of the tools to give.

A small local model picks worse from a long list, so the chats get `GrifTools.Chat`, the questions people ask most. The MCP
server's clients get every tool.

- **The Discord bot** (`MessageCreateHandler`): the caller is the author's linked site account, with its roles
  (`DiscordCaller.AsToolCaller`).
  - An author with no linked account is `ToolCaller.Anonymous`. They can use every tool that isn't about "you", and those
    that are refuse and ask them to name the player.
  - The bot's instructions have it write times in UTC, as the tools give them. A small model can't reliably turn ISO times into
    Discord's epoch timestamps.
- **The site's chat** (still to come): the caller will be the signed-in user.

## Adding a tool

A tool is a public method with a `[Description]` on a class in `GrifballWebApp.Application/Tools`, and each of its
parameters has a `[Description]` too. Its name is the method's name in snake_case. It calls the same service as the
controller for that page and returns its DTO. A class whose constructor takes a `ToolCaller` gets the signed-in user. A new
class is listed in `GrifTools.ReadOnly`, or in `GrifTools.FetchingFromHalo` if it pulls from Halo first. A tool that chats
should have is named in `GrifTools.Chat`. `GrifToolsTests` checks the descriptions and names.

Tools that change things (reporting a match, setting availability) will come with the same role checks as the site's
endpoints, and will be marked as not read-only, so clients ask before calling them.
