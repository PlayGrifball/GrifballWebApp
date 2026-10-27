import { PlayerDto } from '../api/dtos/playerDto';
import { TeamResponseDto } from '../api/dtos/teamResponseDto';
import {
  addCaptain, addPlayerToTeam, Draft, dropPick, dropTeam, movePlayerToTeam, movePoolPlayer, renameTeam,
  removeCaptain, removePlayerFromTeam, resortCaptain,
} from './draft';

describe('team builder draft operations', () => {
  const player = (id: number, round: number | null = null): PlayerDto => ({ name: 'P' + id, personID: id, pick: round, round });
  const team = (captainID: number, order: number, players: PlayerDto[] = []): TeamResponseDto =>
    ({ teamID: captainID, teamName: 'T' + captainID, captain: { name: 'C' + captainID, personID: captainID, order }, players });

  let draft: Draft;
  let snapshot: Draft;

  beforeEach(() => {
    draft = {
      teams: [team(101, 1, [player(1, 1), player(2, 2)]), team(102, 2), team(103, 3)],
      pool: [player(3), player(4), player(5)],
    };
    snapshot = structuredClone(draft);
  });

  // Every operation leaves its input exactly as it was.
  afterEach(() => expect(draft).toEqual(snapshot));

  const order = (d: Draft) => d.teams.map(t => `${t.captain.order}:${t.captain.personID}`);
  const roster = (d: Draft, captainID: number) => d.teams.find(t => t.captain.personID === captainID)!.players.map(p => `${p.round}:${p.personID}`);
  const pool = (d: Draft) => d.pool.map(p => p.personID);

  it('addCaptain inserts the new team at its order and takes the captain out of the pool', () => {
    const next = addCaptain(draft, 4, 'New', 'P4', 2)!;
    expect(order(next)).toEqual(['1:101', '2:4', '3:102', '4:103']);
    expect(next.teams[1]).toEqual({ teamID: 0, teamName: 'New', captain: { name: 'P4', personID: 4, order: 2 }, players: [] });
    expect(pool(next)).toEqual([3, 5]);
    expect(next.teams[0]).toBe(draft.teams[0]); // unchanged teams are shared
    expect(addCaptain(draft, 99, 'X', 'X', 1)).toBeNull();
  });

  it('resortCaptain moves a team to its new order and leaves the pool alone', () => {
    const next = resortCaptain(draft, 103, 1)!;
    expect(order(next)).toEqual(['1:103', '2:101', '3:102']);
    expect(next.pool).toBe(draft.pool);
    expect(resortCaptain(draft, 99, 1)).toBeNull();
  });

  it('removeCaptain returns the picks (last first) and the captain to the pool', () => {
    const next = removeCaptain(draft, 101, 1)!;
    expect(order(next)).toEqual(['1:102', '2:103']);
    expect(pool(next)).toEqual([3, 2, 1, 101, 4, 5]);
    expect(next.pool.every(p => p.round === null && p.pick === null)).toBeTrue();
    expect(pool(removeCaptain(draft, 102)!)).toEqual([102, 3, 4, 5]);
    expect(removeCaptain(draft, 99)).toBeNull();
  });

  it('addPlayerToTeam drafts from the pool, at an index or last', () => {
    // Always the last round, as TeamService.AddPlayerToTeam does.
    const next = addPlayerToTeam(draft, 101, 4)!;
    expect(roster(next, 101)).toEqual(['1:1', '2:2', '3:4']);
    expect(pool(next)).toEqual([3, 5]);
    expect(next.teams[1]).toBe(draft.teams[1]);
    expect(addPlayerToTeam(draft, 101, 99)).toBeNull();
    expect(addPlayerToTeam(draft, 99, 4)).toBeNull();
  });

  it('movePlayerToTeam moves a pick between teams or within one', () => {
    const across = movePlayerToTeam(draft, 101, 102, 1)!;
    expect(roster(across, 101)).toEqual(['1:2']);
    expect(roster(across, 102)).toEqual(['1:1']);
    expect(across.pool).toBe(draft.pool);

    expect(roster(movePlayerToTeam(draft, 101, 101, 1)!, 101)).toEqual(['1:2', '2:1']);
    expect(roster(movePlayerToTeam(draft, 101, 101, 2, 0)!, 101)).toEqual(['1:2', '2:1']);

    expect(movePlayerToTeam(draft, 99, 102, 1)).toBeNull();
    expect(movePlayerToTeam(draft, 101, 102, 99)).toBeNull();
    expect(movePlayerToTeam(draft, 101, 99, 1)).toBeNull();
  });

  it('removePlayerFromTeam undrafts a pick into the pool', () => {
    const next = removePlayerFromTeam(draft, 101, 1, 2)!;
    expect(roster(next, 101)).toEqual(['1:2']);
    expect(pool(next)).toEqual([3, 4, 1, 5]);
    expect(next.pool[2].round).toBeNull();
    expect(removePlayerFromTeam(draft, 99, 1)).toBeNull();
    expect(removePlayerFromTeam(draft, 101, 99)).toBeNull();
  });

  it('movePoolPlayer reorders the pool, counting the index with the player still in it', () => {
    expect(pool(movePoolPlayer(draft, 3, 3)!)).toEqual([4, 5, 3]);
    expect(pool(movePoolPlayer(draft, 5, 0)!)).toEqual([5, 3, 4]);
    expect(movePoolPlayer(draft, 99, 0)).toBeNull();
  });

  it('dropTeam takes the drop index, which still counts the dragged team', () => {
    expect(order(dropTeam(draft, 101, 2)!)).toEqual(['1:102', '2:101', '3:103']); // dropped between 102 and 103
    expect(order(dropTeam(draft, 103, 0)!)).toEqual(['1:103', '2:101', '3:102']);
    expect(order(dropTeam(draft, 101, 3)!)).toEqual(['1:102', '2:103', '3:101']); // dropped at the end
    expect(dropTeam(draft, 99, 0)).toBeNull();
  });

  it('dropPick takes the drop index within the same team, and a final position on another', () => {
    expect(roster(dropPick(draft, 101, 101, 1, 2)!, 101)).toEqual(['1:2', '2:1']); // after pick 2
    expect(roster(dropPick(draft, 101, 101, 2, 0)!, 101)).toEqual(['1:2', '2:1']);
    expect(roster(dropPick(draft, 101, 101, 1)!, 101)).toEqual(['1:2', '2:1']); // no index: last
    const onto = { ...draft, teams: draft.teams.map(t => t.captain.personID === 102 ? { ...t, players: [player(7, 1)] } : t) };
    expect(roster(dropPick(onto, 101, 102, 1, 0)!, 102)).toEqual(['1:1', '2:7']);
    expect(dropPick(draft, 99, 101, 1, 0)).toBeNull();
  });

  it('movePlayerToTeam puts the pick at the given index, as a SignalR roundNumber - 1', () => {
    const onto = { ...draft, teams: draft.teams.map(t => t.captain.personID === 102 ? { ...t, players: [player(7, 1), player(8, 2)] } : t) };
    expect(roster(movePlayerToTeam(onto, 101, 102, 1, 1)!, 102)).toEqual(['1:7', '2:1', '3:8']);
  });

  it('renameTeam names a team, and leaves the draft as it is when nothing changes', () => {
    const next = renameTeam(draft, 102, 'The Hammers');
    expect(next.teams[1].teamName).toBe('The Hammers');
    expect(next.teams[0]).toBe(draft.teams[0]);
    expect(renameTeam(draft, 102, 'T102')).toBe(draft);
    expect(renameTeam(draft, 99, 'Gone')).toBe(draft);
  });
});
