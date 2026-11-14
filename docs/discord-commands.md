# Discord commands

The bot's slash commands cover most of the site. They call the same Application services as the site's controllers, so the
rules are the same in both places. `/help` lists the commands as registered, and `/help command:<name>` lists that
command's subcommands. The handlers are in `GrifballWebApp.Server/Commands`.

## Who may run what

- **Anyone.** Every command for a page the site shows signed out (`[AllowAnonymous]`) works without an account or a
  gamertag.
- **Personal commands** run as the site account linked to the caller's Discord account. A caller with no account, or no
  gamertag, gets the Set Gamertag button: the same flow as the queue's (`DiscordCaller`).
- **Staff commands** need the role the site's endpoint names, held by that linked account. Some are the commissioner's
  alone, as on the site. The bot makes no permission checks of its own. A Discord role grants nothing; only the site's
  Identity roles count.
- Replies about the caller, and every `/admin` reply, are ephemeral: only the caller sees them, so staff commands work in
  any channel.
- A refusal the site answers with a 4xx (`GlobalExceptionHandler.Expected`) gets the same message on Discord.

Seasons, teams, players and season matches autocomplete. A name typed without picking a choice is looked up too: the
exact name, else the only one whose name has it, else (for digits that name nothing) that ID. A typed name that fits
several is refused, and the caller is asked to pick one, so a staff action never lands on a guess.

With no season named, a command means the one that fits what it's for (`SeasonService.GetDefaultSeasonID`, which the
agents' tools use too):

| Purpose | Commands | Season |
|---|---|---|
| Current | `/season info\|standings\|schedule\|stats\|grades\|bracket` | The running one, else the one that ended last, else the next to start |
| Upcoming | `/season signups\|draft`, `/signup`, `/availability show`, `/admin draft` | The next to start, else the running one, else the one that ended last |
| Active | `/admin schedule show\|run`, `/admin bracket seed` | The running one, else the next to start, else the one that ended last |

**A season not announced yet** (it hasn't started and its `PublicAt` hasn't passed) is a commissioner's and sysadmin's
alone, as on the site (#435):
- Replies the channel sees use the public view, whoever asks: a hidden season, its teams and its matches are no such
  thing, by default, by name or by ID, and aren't in the autocomplete.
- `/admin` replies are only the caller's, so its options list and find the seasons and matches the caller may see
  (`StaffSeasonAutocomplete`, `StaffSeasonMatchAutocomplete`).
A gamertag option (`/matches`, `/player pull`) is an Xbox account, sent as its XUID when picked. Its list has the accounts
by current gamertag, then (from two characters typed) by a past one, shown as `Old Name (Previous Gamertag) · now New Name`, as the site's quick
search lists a player by a past gamertag. A gamertag typed without picking finds an account by its current gamertag
only: an old name finds nothing unless it is picked from the list (`CommandLookups.XboxAccount`).
Discord has no date option, so times are typed in UTC, like `2026-11-20 20:00`.

Every command answers Discord at once (deferred), then looks things up: Discord gives an interaction 3 seconds. A transport
test sends every registered command and fails one whose first answer isn't a deferral (`DiscordTransportTests`).

## Anyone

| Command | What it shows |
|---|---|
| `/season list\|info\|standings\|schedule\|stats\|grades\|signups\|bracket\|draft` | A season's pages; with no season named, the one its purpose means (see above) |
| `/team info\|roster\|history\|matches\|vods` | A team's page |
| `/player profile\|stats\|commendations\|history\|vods` | A player's profile; with no player named, the caller's own (needs their gamertag) |
| `/matches` | An Xbox account's recent Halo matches as the site saved them (the agents' `get_recent_games`); with no gamertag, the caller's own. It pulls nothing from Halo: `/player pull` does |
| `/match info` | A season match: when, result, games |
| `/match game` | A Halo game by its match ID |
| `/leaderboard top\|rank` | The all-time boards |
| `/lateleague seasons\|season` | Late League seasons and their top players |
| `/events` | Upcoming signups, drafts and seasons |
| `/search` | Players, teams and seasons |
| `/queue status` | The Late League queue's size, and the caller's place in it |
| `/availability show` | A season's match times, in UTC |
| `/help` | The commands |

## Personal (an account with a gamertag)

| Command | Same as on the site |
|---|---|
| `/setgamertag` | Set your gamertag (makes the account if there is none) |
| `/queue join\|leave` | The queue page's Join and Leave (also the queue message's buttons) |
| `/me region` | Profile: region |
| `/me claim\|claims` | Profile: claim a past gamertag, your claims |
| `/me logins` | A link to the profile page: linking a sign-in runs in a browser |
| `/signup` | The signup form, minus the times you can play (kept, set on the form). What isn't given keeps its value. Player or Sysadmin |
| `/reschedule request` | The reschedule request form. The service decides who may (a player on either team, or a commissioner) |
| `/match pull` | A season match page's Pull buttons: queues the pull of its players' recent games from Halo (back to the season's start, when asked). The agents' `pull_season_match_games`. Player, Commissioner or Sysadmin |
| `/player pull` | An Xbox account page's Pull button: queues the pull of the account's recent games (yours when no gamertag is given). The agents' `pull_xbox_account_games`. Player, Commissioner or Sysadmin |

## Staff

| Command | Role |
|---|---|
| `/admin dashboard\|overdue`, `/reschedule pending\|decide` | Commissioner or Sysadmin |
| `/admin match report\|forfeit\|undo\|time` | Commissioner |
| `/admin season generate`, `/admin bracket create\|seed`, `/admin draft lock\|unlock` | Commissioner. Generate and create name their season and are refused once a match is played (on the site too): they replace the matches |
| `/admin schedule show\|run\|apply` | Commissioner or Sysadmin |
| `/admin halo pull`, `/admin medals`, `/admin jobs list\|run\|pause`, `/admin claims list\|approve\|reject` | Sysadmin |

## Left on the site

These stay on the site, and the matching commands link to them:

- The draft board and custom seeds: a live board, which a command can't match. Draft picks also have the draft
  channel's menu.
- Season settings, editing availability, manual games, Excel, user management and merges, VOD review, the historical
  import and telemetry: large forms or file uploads.
- Sign-in, registration and password resets, the Halo sign-in code, and database backups: secrets don't belong in chat.

## Adding a command

1. Put the logic in an Application service the controller also calls. If a controller still holds the logic, move it
   (as `TeamPageService.GetMatchesAsync`, `HomeEventsService` and `RegionService` were).
2. Write a handler in `Commands/` that takes `IDiscordInteractionContext`. Call `DiscordCaller.Require` with the
   endpoint's roles before deferring, then `Defer` and `Reply` (`CommandReplies`).
3. Add a thin NetCord module that only wraps its context. Give it a `[SlashCommand]` group with `[SubSlashCommand]`
   methods, or join an existing group.
4. Register the handler in `Program.AddDiscordHandlers`, then add the command to `DiscordModuleRegistrationTests`,
   `DiscordTransportTests.Commands` and this page.
