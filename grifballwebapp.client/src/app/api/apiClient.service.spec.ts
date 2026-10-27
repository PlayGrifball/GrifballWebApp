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

  describe('getKills', () => {
    it('should fetch kills data', (done) => {
      const mockKills = [
        { gamertag: 'Player1', kills: 100, rank: 1 },
        { gamertag: 'Player2', kills: 90, rank: 2 }
      ];

      service.getKills().subscribe({
        next: (data) => {
          expect(data).toEqual(mockKills);
          done();
        }
      });

      const req = httpTestingController.expectOne('/api/stats/TopKills');
      expect(req.request.method).toBe('GET');
      req.flush(mockKills);
    });
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
          expect(data.seasonID).toBe(1);
          expect(data.seasonName).toBe('Season 1');
          expect(data.signupsOpen instanceof DateTime).toBeTrue();
          expect(data.signupsOpen.toMillis()).toBe(Date.parse('2026-01-01T00:00:00Z'));
          done();
        }
      });

      const req = httpTestingController.expectOne('/api/EventOrganizer/GetSeason/1');
      expect(req.request.method).toBe('GET');
      req.flush(mockSeason);
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
          expect(date).toBeInstanceOf(DateTime);
          expect(date!.toISO()).toBe(DateTime.fromISO(dateString).toISO());
          done();
        }
      });

      const req = httpTestingController.expectOne('/api/CommitDate');
      expect(req.request.method).toBe('GET');
      req.flush(dateString);
    });
  });

  describe('captain operations with SignalR connection', () => {
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
      expect(req.request.method).toBe('GET');
      expect(req.request.headers.get('SignalRConnectionID')).toBe('lock-conn');
      req.flush({});
    });

    it('should unlock captains with connection ID', (done) => {
      service.unlockCaptains(1, 'unlock-conn').subscribe({
        next: () => done()
      });

      const req = httpTestingController.expectOne('/api/Teams/unlockCaptains/1');
      expect(req.request.method).toBe('GET');
      expect(req.request.headers.get('SignalRConnectionID')).toBe('unlock-conn');
      req.flush({});
    });
  });
});
