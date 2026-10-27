import { PlayerDto } from '../api/dtos/playerDto';
import { TeamResponseDto } from '../api/dtos/teamResponseDto';

/**
 * The team builder's state: the teams (in pick order) and the undrafted player pool.
 *
 * Every operation here is pure. It returns a new Draft that shares the lists, teams and
 * players it did not change, or null when the draft does not contain the people the
 * operation names (the page is out of sync and should reload). Teams are identified by
 * their captain's personID and players by their personID, never by object identity,
 * because the lists are replaced on every change.
 *
 * Positions come in two kinds. The server (and so SignalR) gives final positions: a captain's
 * order, a pick's round. A drop gives ngx-drag-drop's index, which still counts the dragged
 * item where it was (it is hidden, not removed, while dragging). The drop* operations take
 * the drop index and convert it.
 */
export interface Draft {
  teams: TeamResponseDto[];
  pool: PlayerDto[];
}

const insertAt = <T>(list: T[], index: number, ...items: T[]): T[] => [...list.slice(0, index), ...items, ...list.slice(index)];
const removeAt = <T>(list: T[], index: number): T[] => [...list.slice(0, index), ...list.slice(index + 1)];
const teamIndex = (teams: TeamResponseDto[], captainID: number) => teams.findIndex(t => t.captain.personID === captainID);
const playerIndex = (players: PlayerDto[], personID: number) => players.findIndex(p => p.personID === personID);
const undrafted = (player: PlayerDto): PlayerDto => ({ ...player, round: null, pick: null });

/** Captain order follows the team's position (1-based). */
function renumberCaptains(teams: TeamResponseDto[]): TeamResponseDto[] {
  return teams.map((t, i) => t.captain.order === i + 1 ? t : { ...t, captain: { ...t.captain, order: i + 1 } });
}

/** A player's round follows their position on the team (1-based). */
function withPlayers(team: TeamResponseDto, players: PlayerDto[]): TeamResponseDto {
  return { ...team, players: players.map((p, i) => p.round === i + 1 ? p : { ...p, round: i + 1 }) };
}

/** The index to insert at once the moved item (at `from`, or -1 if elsewhere) has been taken out of the same list. */
function indexAfterRemoval(from: number, to: number): number {
  return from !== -1 && from < to ? to - 1 : to;
}

/** A pool player becomes captain of a new team at pick order `order` (1-based). */
export function addCaptain(draft: Draft, personID: number, teamName: string, captainName: string, order: number): Draft | null {
  const from = playerIndex(draft.pool, personID);
  if (from === -1)
    return null;
  const team: TeamResponseDto = { teamID: 0, teamName, captain: { name: captainName, personID, order }, players: [] };
  return { teams: renumberCaptains(insertAt(draft.teams, order - 1, team)), pool: removeAt(draft.pool, from) };
}

/** Moves a captain's team to pick order `order` (1-based). */
export function resortCaptain(draft: Draft, personID: number, order: number): Draft | null {
  const from = teamIndex(draft.teams, personID);
  if (from === -1)
    return null;
  return { ...draft, teams: renumberCaptains(insertAt(removeAt(draft.teams, from), order - 1, draft.teams[from])) };
}

/** Gives a captain's team its name (the one the server chose); a team that is gone stays gone. */
export function renameTeam(draft: Draft, captainID: number, teamName: string): Draft {
  const index = teamIndex(draft.teams, captainID);
  if (index === -1 || draft.teams[index].teamName === teamName)
    return draft;
  return { ...draft, teams: draft.teams.map((t, i) => i === index ? { ...t, teamName } : t) };
}

/** Re-sorts a dropped team to the drop index (which still counts the dragged team). */
export function dropTeam(draft: Draft, personID: number, dropIndex: number): Draft | null {
  const from = teamIndex(draft.teams, personID);
  return resortCaptain(draft, personID, indexAfterRemoval(from, dropIndex) + 1);
}

/** Disbands a captain's team: the captain and their picks go back to the pool at `poolIndex`. */
export function removeCaptain(draft: Draft, personID: number, poolIndex = 0): Draft | null {
  const from = teamIndex(draft.teams, personID);
  if (from === -1)
    return null;
  const team = draft.teams[from];
  const captain: PlayerDto = { name: team.captain.name, personID: team.captain.personID, pick: null, round: null };
  const returned = [...team.players.map(undrafted).reverse(), captain];
  return { teams: renumberCaptains(removeAt(draft.teams, from)), pool: insertAt(draft.pool, poolIndex, ...returned) };
}

/** Drafts a pool player onto a team as its last pick (the server always appends, TeamService.AddPlayerToTeam). */
export function addPlayerToTeam(draft: Draft, captainID: number, personID: number): Draft | null {
  const from = playerIndex(draft.pool, personID);
  const to = teamIndex(draft.teams, captainID);
  if (from === -1 || to === -1)
    return null;
  const team = draft.teams[to];
  const players = [...team.players, undrafted(draft.pool[from])];
  return { teams: draft.teams.map((t, i) => i === to ? withPlayers(team, players) : t), pool: removeAt(draft.pool, from) };
}

/**
 * Moves a pick from one team to another, or within a team, to `index` among the new team's
 * picks once the moved one is out (default: last), i.e. round `index + 1` as the server does
 * (TeamService.MovePlayerToTeam inserts at RoundNumber - 1).
 */
export function movePlayerToTeam(draft: Draft, previousCaptainID: number, newCaptainID: number, personID: number, index?: number): Draft | null {
  const fromTeam = teamIndex(draft.teams, previousCaptainID);
  if (fromTeam === -1)
    return null;
  const from = playerIndex(draft.teams[fromTeam].players, personID);
  if (from === -1)
    return null;
  const player = draft.teams[fromTeam].players[from];
  const teams = draft.teams.map((t, i) => i === fromTeam ? withPlayers(t, removeAt(t.players, from)) : t);

  const toTeam = teamIndex(teams, newCaptainID);
  if (toTeam === -1)
    return null;
  const target = teams[toTeam];
  const players = insertAt(target.players, index ?? target.players.length, player);
  return { ...draft, teams: teams.map((t, i) => i === toTeam ? withPlayers(target, players) : t) };
}

/** A pick dropped on a team's picks at the drop index (default: last). */
export function dropPick(draft: Draft, previousCaptainID: number, newCaptainID: number, personID: number, dropIndex?: number): Draft | null {
  if (dropIndex === undefined)
    return movePlayerToTeam(draft, previousCaptainID, newCaptainID, personID);
  const sameTeam = previousCaptainID === newCaptainID ? draft.teams[teamIndex(draft.teams, newCaptainID)] : undefined;
  const from = sameTeam ? playerIndex(sameTeam.players, personID) : -1;
  return movePlayerToTeam(draft, previousCaptainID, newCaptainID, personID, indexAfterRemoval(from, dropIndex));
}

/** Undrafts a pick: back to the pool at `poolIndex`. */
export function removePlayerFromTeam(draft: Draft, captainID: number, personID: number, poolIndex = 0): Draft | null {
  const fromTeam = teamIndex(draft.teams, captainID);
  if (fromTeam === -1)
    return null;
  const team = draft.teams[fromTeam];
  const from = playerIndex(team.players, personID);
  if (from === -1)
    return null;
  return {
    teams: draft.teams.map((t, i) => i === fromTeam ? withPlayers(team, removeAt(team.players, from)) : t),
    pool: insertAt(draft.pool, poolIndex, undrafted(team.players[from])),
  };
}

/** Reorders the pool. `index` counts the pool with the moved player still in it (as a drop does). */
export function movePoolPlayer(draft: Draft, personID: number, index: number): Draft | null {
  const from = playerIndex(draft.pool, personID);
  if (from === -1)
    return null;
  return { ...draft, pool: insertAt(removeAt(draft.pool, from), indexAfterRemoval(from, index), draft.pool[from]) };
}
