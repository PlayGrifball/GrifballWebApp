import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { ApiClientService } from './apiClient.service';
import { DateTime } from 'luxon';

describe('ApiClientService', () => {
  let service: ApiClientService;
  let httpTestingController: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule]
    });
    service = TestBed.inject(ApiClientService);
    httpTestingController = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpTestingController.verify();
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  describe('getSeasons', () => {
    it('should fetch seasons data', (done) => {
      const mockSeasons: any[] = [{ seasonID: 1, seasonName: 'Season 1', signupsOpen: '2026-01-01T00:00:00Z', signupsClose: '2026-01-08T00:00:00Z', draftStart: '2026-01-09T00:00:00Z', seasonStart: '2026-01-10T00:00:00Z', seasonEnd: '2026-03-01T00:00:00Z', signupsCount: 3 }];

      service.getSeasons().subscribe({
        next: (data) => {
          expect(data.length).toBe(1);
          expect(data[0].seasonID).toBe(1);
          expect(data[0].seasonEnd instanceof DateTime).toBeTrue();
          expect(data[0].seasonEnd.toMillis()).toBe(Date.parse('2026-03-01T00:00:00Z'));
          done();
        }
      });

      const req = httpTestingController.expectOne('/api/EventOrganizer/GetSeasons');
      expect(req.request.method).toBe('GET');
      req.flush(mockSeasons);
    });
  });

  describe('getSeason', () => {
    it('should fetch single season by ID', (done) => {
      const mockSeason: any = { seasonID: 1, seasonName: 'Season 1', signupsOpen: '2026-01-01T00:00:00Z', signupsClose: '2026-01-08T00:00:00Z', draftStart: '2026-01-09T00:00:00Z', seasonStart: '2026-01-10T00:00:00Z', seasonEnd: '2026-03-01T00:00:00Z', signupsCount: 3 };

      service.getSeason(1).subscribe({
        next: (data) => {
          expect(data!.seasonID).toBe(1);
          expect(data!.seasonName).toBe('Season 1');
          expect(data!.signupsOpen instanceof DateTime).toBeTrue();
          expect(data!.signupsOpen.toMillis()).toBe(Date.parse('2026-01-01T00:00:00Z'));
          expect(data!.publicAt).toBeNull(); // not announced
          done();
        }
      });

      const req = httpTestingController.expectOne('/api/EventOrganizer/GetSeason/1');
      expect(req.request.method).toBe('GET');
      req.flush(mockSeason);
    });

    it('reads when a season is public from', (done) => {
      service.getSeason(2).subscribe({
        next: (data) => {
          expect(data!.publicAt!.toMillis()).toBe(Date.parse('2025-12-01T00:00:00Z'));
          done();
        }
      });

      httpTestingController.expectOne('/api/EventOrganizer/GetSeason/2').flush({ seasonID: 2, seasonName: 'S2', publicAt: '2025-12-01T00:00:00Z', signupsOpen: '2026-01-01T00:00:00Z', signupsClose: '2026-01-08T00:00:00Z', draftStart: '2026-01-09T00:00:00Z', seasonStart: '2026-01-10T00:00:00Z', seasonEnd: '2026-03-01T00:00:00Z' });
    });

    it('gives null for a season that does not exist (204)', (done) => {
      service.getSeason(99).subscribe({
        next: (data) => {
          expect(data).toBeNull();
          done();
        }
      });

      httpTestingController.expectOne('/api/EventOrganizer/GetSeason/99').flush(null, { status: 204, statusText: 'No Content' });
    });
  });

  describe('upsertSeason', () => {
    it('should post season data and return ID', (done) => {
      const seasonDto = { seasonID: 0, name: 'New Season' } as any;

      service.upsertSeason(seasonDto).subscribe({
        next: (id) => {
          expect(id).toBe(5);
          done();
        }
      });

      const req = httpTestingController.expectOne('/api/EventOrganizer/UpsertSeason/');
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual(seasonDto);
      req.flush(5);
    });
  });

  describe('season editor options', () => {
    it('fetches the games and leagues a season can have', () => {
      let games: unknown, leagues: unknown;
      service.getGameVersions().subscribe(g => games = g);
      service.getLeagues().subscribe(l => leagues = l);
      httpTestingController.expectOne('/api/EventOrganizer/GetGameVersions').flush([{ gameVersionID: 6, name: 'Halo Infinite' }]);
      httpTestingController.expectOne('/api/EventOrganizer/GetLeagues').flush([{ leagueID: 1, name: 'AGLA' }]);
      expect(games).toEqual([{ gameVersionID: 6, name: 'Halo Infinite' }]);
      expect(leagues).toEqual([{ leagueID: 1, name: 'AGLA' }]);
    });
  });

  describe('archive pages', () => {
    it('fetches a season summary', () => {
      let name = '';
      service.getSeasonSummary(5).subscribe(s => name = s.seasonName);
      httpTestingController.expectOne('/api/Season/getSeasonSummary/5').flush({ seasonName: 'Five' });
      expect(name).toBe('Five');
    });

    it("fetches a season's player stats, all of them or one team's", () => {
      service.getSeasonPlayerStats(5).subscribe();
      service.getSeasonPlayerStats(5, 41).subscribe();
      const all = httpTestingController.expectOne(r => r.url === '/api/Season/GetSeasonPlayerStats/5' && !r.params.has('teamID'));
      const team = httpTestingController.expectOne(r => r.url === '/api/Season/GetSeasonPlayerStats/5' && r.params.get('teamID') === '41');
      all.flush([]);
      team.flush([]);
    });

    it('fetches the season list, filtered by league and game only when chosen', () => {
      service.getSeasonList({ leagueID: null, gameVersionID: null }, 250).subscribe();
      service.getSeasonList({ leagueID: 2, gameVersionID: 6 }, 250).subscribe();
      httpTestingController.expectOne(r => r.url === '/api/Season/GetSeasons' && r.params.get('pageSize') === '250' && r.params.keys().length === 1)
        .flush({ totalCount: 0, results: [] });
      httpTestingController.expectOne(r => r.params.get('leagueID') === '2' && r.params.get('gameVersionID') === '6')
        .flush({ totalCount: 0, results: [] });
    });

    it('fetches the leaderboards', () => {
      service.getLeaderboards().subscribe();
      expect(httpTestingController.expectOne('/api/Leaderboards/GetLeaderboards').request.method).toBe('GET');
    });
  });

  describe('gamertag claims', () => {
    it('lists the claims with a status', () => {
      service.getGamertagClaims('Rejected').subscribe();
      const req = httpTestingController.expectOne(r => r.url === '/api/GamertagClaims/GetClaims');
      expect(req.request.params.get('status')).toBe('Rejected');
      req.flush([]);
    });

    it('approves and rejects a claim without a body', () => {
      service.approveGamertagClaim(3).subscribe();
      service.rejectGamertagClaim(4).subscribe();
      const approve = httpTestingController.expectOne('/api/GamertagClaims/ApproveClaim/3');
      const reject = httpTestingController.expectOne('/api/GamertagClaims/RejectClaim/4');
      expect([approve.request.method, reject.request.method]).toEqual(['POST', 'POST']);
      expect(approve.request.body).toBeNull();
      approve.flush(null, { status: 204, statusText: 'No Content' });
      reject.flush(null, { status: 204, statusText: 'No Content' });
    });

    it('adds a gamertag to an Xbox account history', () => {
      const dto = { xboxUserID: 2535400000000001, gamertag: 'Old Tag', firstSeen: '2010-01-01T00:00:00Z', lastSeen: null };
      service.addGamertagHistory(dto).subscribe();
      const req = httpTestingController.expectOne('/api/GamertagClaims/AddGamertagHistory');
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual(dto);
      req.flush(null, { status: 204, statusText: 'No Content' });
    });
  });

  describe('getCurrentSeasonID', () => {
    it('should fetch current season ID', (done) => {
      service.getCurrentSeasonID().subscribe({
        next: (id) => {
          expect(id).toBe(3);
          done();
        }
      });

      const req = httpTestingController.expectOne('/api/Season/getCurrentSeasonID/');
      expect(req.request.method).toBe('GET');
      req.flush(3);
    });
  });

  describe('getCurrentAndFutureEvents', () => {
    it('fetches the current and upcoming events', (done) => {
      const events = [{ seasonID: 1, name: 'S', start: '2026-01-01T00:00:00Z', end: '2026-01-02T00:00:00Z', eventType: 'Signup' as const }];
      service.getCurrentAndFutureEvents().subscribe(result => {
        expect(result).toEqual(events);
        done();
      });
      const req = httpTestingController.expectOne('/api/Home/CurrentAndFutureEvents/');
      expect(req.request.method).toBe('GET');
      req.flush(events);
    });
  });

  describe('getSeasonName', () => {
    it('should fetch season name', (done) => {
      service.getSeasonName(1).subscribe({
        next: (name) => {
          expect(name).toBe('Test Season');
          done();
        }
      });

      const req = httpTestingController.expectOne('/api/Season/getSeasonName/1');
      expect(req.request.method).toBe('GET');
      req.flush('Test Season');
    });

    it('should handle null season name', (done) => {
      service.getSeasonName(999).subscribe({
        next: (name) => {
          expect(name).toBeNull();
          done();
        }
      });

      const req = httpTestingController.expectOne('/api/Season/getSeasonName/999');
      req.flush(null);
    });
  });

  describe('getSignups', () => {
    it('should fetch signups for a season', (done) => {
      const mockSignups = [{ seasonID: 1, userID: 7, personName: 'Grif', timestamp: '2026-01-07T19:00:00Z', teamName: null, willCaptain: false, requiresAssistanceDrafting: false, timeslots: [] }] as any;

      service.getSignups(1).subscribe({
        next: (data) => {
          expect(data).toEqual(mockSignups);
          done();
        }
      });

      const req = httpTestingController.expectOne('/api/Signups/getSignups/1');
      expect(req.request.method).toBe('GET');
      req.flush(mockSignups);
    });
  });

  describe('getTimeslots', () => {
    it('should fetch timeslots with offset', (done) => {
      const mockTimeslots = [{ optionID: 41, dayOfWeek: 'Monday', time: '19:00:00', isChecked: false }];

      service.getTimeslots(1, 300).subscribe({
        next: (data) => {
          expect(data).toEqual(mockTimeslots);
          done();
        }
      });

      const req = httpTestingController.expectOne('/api/Signups/getTimeslots/1?offset=300');
      expect(req.request.method).toBe('GET');
      req.flush(mockTimeslots);
    });
  });

  describe('getSignup', () => {
    it('should fetch signup with offset', (done) => {
      const mockSignup = { seasonID: 1, userID: 7, personName: 'Grif', timestamp: '2026-01-07T19:00:00Z', teamName: null, willCaptain: false, requiresAssistanceDrafting: false, timeslots: [] } as any;

      service.getSignup(1, 300).subscribe({
        next: (data) => {
          expect(data).toEqual(mockSignup);
          done();
        }
      });

      const req = httpTestingController.expectOne('/api/Signups/getSignup/1?offset=300');
      expect(req.request.method).toBe('GET');
      req.flush(mockSignup);
    });
  });

  describe('getTeams', () => {
    it('should fetch teams for a season', (done) => {
      const mockTeams = [{ teamID: 1, teamName: 'Team A', captain: { name: 'Cap', personID: 3, order: 1 }, players: [] }];

      service.getTeams(1).subscribe({
        next: (data) => {
          expect(data).toEqual(mockTeams);
          done();
        }
      });

      const req = httpTestingController.expectOne('/api/Teams/GetTeams/1');
      expect(req.request.method).toBe('GET');
      req.flush(mockTeams);
    });
  });

  describe('getPlayerPool', () => {
    it('should fetch player pool for a season', (done) => {
      const mockPlayers = [{ personID: 1, name: 'Player1', round: null, pick: null }];

      service.getPlayerPool(1).subscribe({
        next: (data) => {
          expect(data).toEqual(mockPlayers);
          done();
        }
      });

      const req = httpTestingController.expectOne('/api/Teams/GetPlayerPool/1');
      expect(req.request.method).toBe('GET');
      req.flush(mockPlayers);
    });
  });

  describe('areCaptainsLocked', () => {
    it('should check if captains are locked', (done) => {
      service.areCaptainsLocked(1).subscribe({
        next: (locked) => {
          expect(locked).toBe(true);
          done();
        }
      });

      const req = httpTestingController.expectOne('/api/Teams/areCaptainsLocked/1');
      expect(req.request.method).toBe('GET');
      req.flush(true);
    });
  });

  describe('commitHash', () => {
    it('should fetch commit hash', (done) => {
      service.commitHash().subscribe({
        next: (hash) => {
          expect(hash).toBe('abc123def456');
          done();
        }
      });

      const req = httpTestingController.expectOne('/api/CommitHash');
      expect(req.request.method).toBe('GET');
      req.flush('abc123def456');
    });
  });

  describe('commitDate', () => {
    it('should fetch and parse commit date', (done) => {
      const dateString = '2024-01-15T10:30:00Z';

      service.commitDate().subscribe({
        next: (date) => {
          expect(date).toBeInstanceOf(Date);
          expect(date!.getTime()).toBe(Date.parse(dateString));
          done();
        }
      });

      const req = httpTestingController.expectOne('/api/CommitDate');
      expect(req.request.method).toBe('GET');
      req.flush(dateString);
    });

    it('gives null when the server has no commit date', (done) => {
      service.commitDate().subscribe({
        next: (date) => {
          expect(date).toBeNull();
          done();
        }
      });

      httpTestingController.expectOne('/api/CommitDate').flush('');
    });
  });

  describe('captain operations with SignalR connection', () => {
    it('sends no connection header while the hub has no connection id (undefined or empty)', () => {
      const dto = { seasonID: 1, personID: 1, orderNumber: 1 } as any;
      service.addCaptain(dto, undefined as any).subscribe();
      service.resortCaptain(dto, '').subscribe();

      for (const url of ['/api/Teams/AddCaptain/', '/api/Teams/ResortCaptain/']) {
        const req = httpTestingController.expectOne(url);
        expect(req.request.headers.has('SignalRConnectionID')).toBe(false);
        req.flush({});
      }
    });

    it('should add captain without connection ID', (done) => {
      const dto = { seasonID: 1, playerID: 1 } as any;

      service.addCaptain(dto, null).subscribe({
        next: () => done()
      });

      const req = httpTestingController.expectOne('/api/Teams/AddCaptain/');
      expect(req.request.method).toBe('POST');
      expect(req.request.headers.has('SignalRConnectionID')).toBe(false);
      req.flush({});
    });

    it('should add captain with connection ID', (done) => {
      const dto = { seasonID: 1, playerID: 1 } as any;

      service.addCaptain(dto, 'connection-123').subscribe({
        next: () => done()
      });

      const req = httpTestingController.expectOne('/api/Teams/AddCaptain/');
      expect(req.request.method).toBe('POST');
      expect(req.request.headers.get('SignalRConnectionID')).toBe('connection-123');
      req.flush({});
    });

    it('should resort captain with connection ID', (done) => {
      const dto = { seasonID: 1, playerID: 1 } as any;

      service.resortCaptain(dto, 'connection-456').subscribe({
        next: () => done()
      });

      const req = httpTestingController.expectOne('/api/Teams/ResortCaptain/');
      expect(req.request.method).toBe('POST');
      expect(req.request.headers.get('SignalRConnectionID')).toBe('connection-456');
      req.flush({});
    });

    it('should remove captain with connection ID', (done) => {
      const dto = { seasonID: 1, playerID: 1 } as any;

      service.removeCaptain(dto, 'connection-789').subscribe({
        next: () => done()
      });

      const req = httpTestingController.expectOne('/api/Teams/RemoveCaptain/');
      expect(req.request.method).toBe('POST');
      expect(req.request.headers.get('SignalRConnectionID')).toBe('connection-789');
      req.flush({});
    });
  });

  describe('player operations with SignalR connection', () => {
    it('should remove player from team with connection ID', (done) => {
      const dto = { teamID: 1, playerID: 1 } as any;

      service.removePlayerFromTeam(dto, 'conn-1').subscribe({
        next: () => done()
      });

      const req = httpTestingController.expectOne('/api/Teams/removePlayerFromTeam/');
      expect(req.request.method).toBe('POST');
      expect(req.request.headers.get('SignalRConnectionID')).toBe('conn-1');
      req.flush({});
    });

    it('should move player to team with connection ID', (done) => {
      const dto = { teamID: 1, playerID: 1 } as any;

      service.movePlayerToTeam(dto, 'conn-2').subscribe({
        next: () => done()
      });

      const req = httpTestingController.expectOne('/api/Teams/movePlayerToTeam/');
      expect(req.request.method).toBe('POST');
      expect(req.request.headers.get('SignalRConnectionID')).toBe('conn-2');
      req.flush({});
    });

    it('should add player to team with connection ID', (done) => {
      const dto = { teamID: 1, playerID: 1 } as any;

      service.addPlayerToTeam(dto, 'conn-3').subscribe({
        next: () => done()
      });

      const req = httpTestingController.expectOne('/api/Teams/addPlayerToTeam/');
      expect(req.request.method).toBe('POST');
      expect(req.request.headers.get('SignalRConnectionID')).toBe('conn-3');
      req.flush({});
    });
  });

  describe('upsertSignup', () => {
    it('posts the signup with the timeslots in the API time format, as loaded', (done) => {
      const signupDto: any = {
        seasonID: 1,
        timeslots: [
          { optionID: 41, dayOfWeek: 'Monday', time: '19:00:00', isChecked: true },
          { optionID: 42, dayOfWeek: 'Tuesday', time: '20:00:00', isChecked: false }
        ]
      };

      service.upsertSignup(signupDto).subscribe({
        next: () => done()
      });

      const req = httpTestingController.expectOne('/api/Signups/UpsertSignup/');
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual(signupDto);
      expect(req.request.body.timeslots.map((t: any) => [t.optionID, t.isChecked])).toEqual([[41, true], [42, false]]);
      req.flush({});
    });
  });

  describe('captain locking operations', () => {
    it('should lock captains with connection ID', (done) => {
      service.lockCaptains(1, 'lock-conn').subscribe({
        next: () => done()
      });

      const req = httpTestingController.expectOne('/api/Teams/lockCaptains/1');
      expect(req.request.method).toBe('POST');
      expect(req.request.headers.get('SignalRConnectionID')).toBe('lock-conn');
      req.flush({});
    });

    it('should unlock captains with connection ID', (done) => {
      service.unlockCaptains(1, 'unlock-conn').subscribe({
        next: () => done()
      });

      const req = httpTestingController.expectOne('/api/Teams/unlockCaptains/1');
      expect(req.request.method).toBe('POST');
      expect(req.request.headers.get('SignalRConnectionID')).toBe('unlock-conn');
      req.flush({});
    });
  });
});
