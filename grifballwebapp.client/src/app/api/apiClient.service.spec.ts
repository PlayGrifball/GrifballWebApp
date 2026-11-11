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

      const req = httpTestingController.expectOne('/api/seasons/settings');
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

      const req = httpTestingController.expectOne('/api/seasons/1/settings');
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

      httpTestingController.expectOne('/api/seasons/2/settings').flush({ seasonID: 2, seasonName: 'S2', publicAt: '2025-12-01T00:00:00Z', signupsOpen: '2026-01-01T00:00:00Z', signupsClose: '2026-01-08T00:00:00Z', draftStart: '2026-01-09T00:00:00Z', seasonStart: '2026-01-10T00:00:00Z', seasonEnd: '2026-03-01T00:00:00Z' });
    });

    it('gives null for a season that does not exist (404)', (done) => {
      service.getSeason(99).subscribe({
        next: (data) => {
          expect(data).toBeNull();
          done();
        }
      });

      httpTestingController.expectOne('/api/seasons/99/settings').flush({ status: 404, title: 'Not found' }, { status: 404, statusText: 'Not Found' });
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

      const req = httpTestingController.expectOne('/api/seasons/settings');
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
      httpTestingController.expectOne('/api/game-versions').flush([{ gameVersionID: 6, name: 'Halo Infinite' }]);
      httpTestingController.expectOne('/api/leagues').flush([{ leagueID: 1, name: 'AGLA' }]);
      expect(games).toEqual([{ gameVersionID: 6, name: 'Halo Infinite' }]);
      expect(leagues).toEqual([{ leagueID: 1, name: 'AGLA' }]);
    });
  });

  describe('archive pages', () => {
    it('fetches a season summary', () => {
      let name = '';
      service.getSeasonSummary(5).subscribe(s => name = s.seasonName);
      httpTestingController.expectOne('/api/seasons/5/summary').flush({ seasonName: 'Five' });
      expect(name).toBe('Five');
    });

    it("fetches a season's teams and rosters", () => {
      service.getSeasonTeams(5).subscribe();
      const req = httpTestingController.expectOne('/api/seasons/5/teams');
      expect(req.request.method).toBe('GET');
      req.flush([]);
    });

    it("fetches a season's player stats, all of them or one team's", () => {
      const got: unknown[] = [];
      service.getSeasonPlayerStats(5).subscribe(stats => got.push(stats));
      service.getSeasonPlayerStats(5, 41).subscribe(stats => got.push(stats));
      const all = httpTestingController.expectOne(r => r.url === '/api/seasons/5/player-stats' && !r.params.has('teamID'));
      const team = httpTestingController.expectOne(r => r.url === '/api/seasons/5/player-stats' && r.params.get('teamID') === '41');
      expect([all.request.method, team.request.method]).toEqual(['GET', 'GET']);
      all.flush([]);
      team.flush([]);
      expect(got).toEqual([[], []]);
    });

    it('fetches the season list, filtered by league and game only when chosen', () => {
      const totals: number[] = [];
      service.getSeasonList({ leagueID: null, gameVersionID: null }, 250).subscribe(page => totals.push(page.totalCount));
      service.getSeasonList({ leagueID: 2, gameVersionID: 6 }, 250).subscribe(page => totals.push(page.totalCount));
      httpTestingController.expectOne(r => r.url === '/api/seasons' && r.params.get('pageSize') === '250' && r.params.keys().length === 1)
        .flush({ totalCount: 0, results: [] });
      httpTestingController.expectOne(r => r.params.get('leagueID') === '2' && r.params.get('gameVersionID') === '6')
        .flush({ totalCount: 0, results: [] });
      service.getSeasonList({ leagueID: null, gameVersionID: null, format: 'Tournament' }, 250).subscribe();
      httpTestingController.expectOne(r => r.params.get('format') === 'Tournament' && !r.params.has('leagueID'))
        .flush({ totalCount: 0, results: [] });
      service.getSeasonList({ leagueID: null, gameVersionID: null, year: 2010 }, 250).subscribe();
      httpTestingController.expectOne(r => r.params.get('year') === '2010' && !r.params.has('format'))
        .flush({ totalCount: 0, results: [] });
      expect(totals).toEqual([0, 0]);
    });

    it('fetches the leaderboards', () => {
      service.getLeaderboards().subscribe();
      expect(httpTestingController.expectOne('/api/Leaderboards/GetLeaderboards').request.method).toBe('GET');
      service.getLeaderboards('Tournament').subscribe();
      expect(httpTestingController.expectOne(r => r.url === '/api/Leaderboards/GetLeaderboards' && r.params.get('format') === 'Tournament').request.method).toBe('GET');
    });

    it('runs the quick search with the query as a parameter', () => {
      service.quickSearch('blue & gold').subscribe();
      expect(httpTestingController.expectOne(r => r.url === '/api/Search/QuickSearch' && r.params.get('q') === 'blue & gold').request.method).toBe('GET');
    });

    it("fetches Late League's queue, seasons and a season", () => {
      service.getLateLeagueQueue().subscribe();
      expect(httpTestingController.expectOne('/api/LateLeague/GetQueue').request.method).toBe('GET');
      service.getLateLeagueSeasons().subscribe();
      expect(httpTestingController.expectOne('/api/LateLeague/GetSeasons').request.method).toBe('GET');
      service.getLateLeagueSeason(8).subscribe();
      expect(httpTestingController.expectOne('/api/LateLeague/GetSeason/8').request.method).toBe('GET');
    });
  });

  describe('gamertag claims', () => {
    it('lists the claims with a status', () => {
      service.getGamertagClaims('Rejected').subscribe();
      const req = httpTestingController.expectOne(r => r.url === '/api/GamertagClaims/GetClaims');
      expect(req.request.params.get('status')).toBe('Rejected');
      req.flush([]);
    });

    it('approves a claim without a body, and rejects one with its reason', () => {
      service.approveGamertagClaim(3).subscribe();
      service.rejectGamertagClaim(4).subscribe();
      service.rejectGamertagClaim(5, 'Not them').subscribe();
      const approve = httpTestingController.expectOne('/api/GamertagClaims/ApproveClaim/3');
      const reject = httpTestingController.expectOne('/api/GamertagClaims/RejectClaim/4');
      const withReason = httpTestingController.expectOne('/api/GamertagClaims/RejectClaim/5');
      expect([approve.request.method, reject.request.method]).toEqual(['POST', 'POST']);
      expect(approve.request.body).toBeNull();
      expect(reject.request.body).toEqual({ reason: null });
      expect(withReason.request.body).toEqual({ reason: 'Not them' });
      approve.flush(null, { status: 204, statusText: 'No Content' });
      reject.flush(null, { status: 204, statusText: 'No Content' });
      withReason.flush(null, { status: 204, statusText: 'No Content' });
    });

    it('lists, merges and dismisses possible duplicates', () => {
      service.getDuplicateSuggestions().subscribe();
      service.mergeDuplicate(1, 2).subscribe();
      service.dismissDuplicate(3, 4).subscribe();
      expect(httpTestingController.expectOne('/api/GamertagClaims/DuplicateSuggestions').request.method).toBe('GET');
      const merge = httpTestingController.expectOne('/api/GamertagClaims/MergeDuplicate');
      const dismiss = httpTestingController.expectOne('/api/GamertagClaims/DismissDuplicate');
      expect(merge.request.body).toEqual({ sourceUserID: 1, targetUserID: 2 });
      expect(dismiss.request.body).toEqual({ sourceUserID: 3, targetUserID: 4 });
      merge.flush(null, { status: 204, statusText: 'No Content' });
      dismiss.flush(null, { status: 204, statusText: 'No Content' });
    });

    it("previews, makes and lists the user's own claims, and sets their region", () => {
      const dto = { gamertag: 'Old Me', firstSeen: null, lastSeen: '2020-01-01T00:00:00Z' };
      service.previewGamertagClaim('Old Me').subscribe();
      service.claimGamertag(dto).subscribe();
      service.getMyGamertagClaims().subscribe();
      service.setMyRegion(3).subscribe();
      expect(httpTestingController.expectOne(r => r.url === '/api/MyProfile/PreviewClaim' && r.params.get('gamertag') === 'Old Me').request.method).toBe('GET');
      expect(httpTestingController.expectOne('/api/MyProfile/ClaimGamertag').request.body).toEqual(dto);
      expect(httpTestingController.expectOne('/api/MyProfile/GamertagClaims').request.method).toBe('GET');
      expect(httpTestingController.expectOne('/api/MyProfile/SetRegion').request.body).toEqual({ regionID: 3 });
    });
  });

  describe('historical import', () => {
    it('lists the sources and enables or disables one', () => {
      let sources: unknown;
      service.getHistoricalSources().subscribe(r => sources = r);
      httpTestingController.expectOne('/api/HistoricalImport/Sources').flush([]);
      expect(sources).toEqual([]);

      service.setHistoricalSourceEnabled('2023 da/wl', false).subscribe();
      const set = httpTestingController.expectOne('/api/HistoricalImport/SetSourceEnabled/2023%20da%2Fwl');
      expect(set.request.method).toBe('POST');
      expect(set.request.body).toEqual({ enabled: false });
      set.flush(null, { status: 204, statusText: 'No Content' });
    });

    it('queues refreshes, previews, applies and review decisions without a body', () => {
      const calls: [() => void, string][] = [
        [() => service.refreshHistoricalSources().subscribe(), '/api/HistoricalImport/Refresh'],
        [() => service.previewHistoricalImport().subscribe(), '/api/HistoricalImport/Preview'],
        [() => service.applyHistoricalImportPreview(4).subscribe(), '/api/HistoricalImport/ApplyPreview/4'],
        [() => service.approveHistoricalSeasonReview(5).subscribe(), '/api/HistoricalImport/ApproveReview/5'],
        [() => service.skipHistoricalSeasonReview(6).subscribe(), '/api/HistoricalImport/SkipReview/6'],
      ];
      for (const [call, url] of calls) {
        call();
        const request = httpTestingController.expectOne(url);
        expect(request.request.method).toBe('POST');
        expect(request.request.body).toBeNull();
        request.flush({ jobID: 9 }, { status: 202, statusText: 'Accepted' });
      }
    });

    it('reads the jobs, a job and the schedule, and pauses it', () => {
      let paused: unknown;
      service.getHistoricalImportJobs().subscribe();
      service.getHistoricalImportJob(7).subscribe();
      service.getHistoricalImportSchedule().subscribe();
      service.setHistoricalImportSchedulePaused(true).subscribe(r => paused = r);
      httpTestingController.expectOne('/api/HistoricalImport/Jobs').flush([]);
      httpTestingController.expectOne('/api/HistoricalImport/Job/7').flush({});
      httpTestingController.expectOne('/api/HistoricalImport/Schedule').flush({});
      const pause = httpTestingController.expectOne('/api/HistoricalImport/SetSchedulePaused');
      expect(pause.request.body).toEqual({ paused: true });
      pause.flush({ paused: true });
      expect(paused).toEqual({ paused: true });
    });
  });

  describe('jobs', () => {
    const key = { group: 'recurring', name: 'historical-refresh' };

    it("reads the jobs and a job's runs", () => {
      service.getJobs().subscribe();
      service.getJobHistory(key, 20).subscribe();
      httpTestingController.expectOne('/api/Jobs/List').flush({ jobs: [], nodes: [] });
      const history = httpTestingController.expectOne(r => r.url === '/api/Jobs/History');
      expect(history.request.params.toString()).toBe('group=recurring&name=historical-refresh&take=20');
      history.flush([]);
    });

    it('runs, pauses, reschedules, resets and stops jobs', () => {
      service.runJob(key).subscribe();
      service.setJobPaused(key, true).subscribe();
      service.setJobInterval(key, 600).subscribe();
      service.resetJobSchedule(key).subscribe();
      service.interruptJob('f-1').subscribe();
      const expected: [string, unknown][] = [
        ['/api/Jobs/Run', key],
        ['/api/Jobs/SetPaused', { ...key, paused: true }],
        ['/api/Jobs/SetInterval', { ...key, intervalSeconds: 600 }],
        ['/api/Jobs/ResetSchedule', key],
        ['/api/Jobs/Interrupt', { fireInstanceID: 'f-1' }],
      ];
      for (const [url, body] of expected) {
        const request = httpTestingController.expectOne(url);
        expect(request.request.method).toBe('POST');
        expect(request.request.body).toEqual(body);
        request.flush(null, { status: 204, statusText: 'No Content' });
      }
    });
  });

  describe('telemetry', () => {
    it('reads the status, metrics and a trace', () => {
      service.getTelemetryStatus().subscribe();
      service.getTelemetryMetrics(360).subscribe();
      service.getTelemetryTrace('abc').subscribe();
      httpTestingController.expectOne('/api/Telemetry/Status').flush({ serviceName: 'grif-prod', logs: true, metrics: true, traces: true });
      const metrics = httpTestingController.expectOne(r => r.url === '/api/Telemetry/Metrics');
      expect(metrics.request.params.toString()).toBe('minutes=360');
      metrics.flush({ start: '', end: '', stepSeconds: 15, panels: [], routes: [] });
      const trace = httpTestingController.expectOne(r => r.url === '/api/Telemetry/Trace');
      expect(trace.request.params.toString()).toBe('id=abc');
      trace.flush({ traceId: 'abc', start: '', durationMs: 0, spans: [] });
    });

    it('sends only the log filters that are set', () => {
      service.getTelemetryLogs({ minutes: 60, levels: [], search: ' ', traceId: '', limit: 200 }).subscribe();
      service.getTelemetryLogs({ minutes: 15, levels: ['warn', 'error'], search: ' timeout ', traceId: 'abc', limit: 500 }).subscribe();
      const [plain, filtered] = httpTestingController.match(r => r.url === '/api/Telemetry/Logs');
      expect(plain.request.params.toString()).toBe('minutes=60&limit=200');
      expect(filtered.request.params.toString()).toBe('minutes=15&limit=500&level=warn&level=error&search=timeout&traceId=abc');
      plain.flush({ entries: [], truncated: false });
      filtered.flush({ entries: [], truncated: false });
    });

    it('sends the trace search', () => {
      service.getTelemetryTraces({ minutes: 60, errorsOnly: false, minDurationMs: null }).subscribe();
      service.getTelemetryTraces({ minutes: 60, errorsOnly: true, minDurationMs: 250 }).subscribe();
      const [all, slowFailures] = httpTestingController.match(r => r.url === '/api/Telemetry/Traces');
      expect(all.request.params.toString()).toBe('minutes=60&errorsOnly=false');
      expect(slowFailures.request.params.toString()).toBe('minutes=60&errorsOnly=true&minDurationMs=250');
      all.flush([]);
      slowFailures.flush([]);
    });
  });

  describe('VODs', () => {
    it("reads each page's VODs", () => {
      let answered = 0;
      service.getVods('seasonMatch', 1).subscribe(() => answered++);
      service.getVods('season', 2).subscribe(() => answered++);
      service.getVods('team', 3).subscribe(() => answered++);
      service.getVods('player', 4).subscribe(() => answered++);
      for (const url of ['/api/Vod/ForSeasonMatch/1', '/api/Vod/ForSeason/2', '/api/Vod/ForTeam/3', '/api/Vod/ForPlayer/4']) {
        const req = httpTestingController.expectOne(url);
        expect(req.request.method).withContext(url).toBe('GET');
        req.flush([]);
      }
      expect(answered).toBe(4);
    });

    it('reads the review and its link options, and links or dismisses a VOD', () => {
      service.getVodReview().subscribe();
      service.getVodLinkSeasons().subscribe();
      service.getVodLinkOptions(5).subscribe();
      service.linkVod(6, { seasonID: 5, seasonMatchID: null, teamIDs: [7] }).subscribe();
      service.dismissVod(8).subscribe();
      httpTestingController.expectOne('/api/Vod/Review').flush([]);
      httpTestingController.expectOne('/api/Vod/LinkSeasons').flush([]);
      httpTestingController.expectOne('/api/Vod/LinkOptions/5').flush({ teams: [], matches: [] });
      const link = httpTestingController.expectOne('/api/Vod/Link/6');
      expect(link.request.method).toBe('POST');
      expect(link.request.body).toEqual({ seasonID: 5, seasonMatchID: null, teamIDs: [7] });
      link.flush(null);
      const dismiss = httpTestingController.expectOne('/api/Vod/Dismiss/8');
      expect(dismiss.request.method).toBe('POST');
      dismiss.flush(null);
    });
  });

  describe('user merges', () => {
    it('lists a page of merges and undoes one with a note', () => {
      service.getUserMerges(2, 25).subscribe();
      service.undoUserMerge(7, 'wrong person').subscribe();
      const list = httpTestingController.expectOne(r => r.url === '/api/UserMerges/List');
      expect([list.request.params.get('pageNumber'), list.request.params.get('pageSize')]).toEqual(['2', '25']);
      const undo = httpTestingController.expectOne('/api/UserMerges/Undo/7');
      expect([undo.request.method, undo.request.body]).toEqual(['POST', { note: 'wrong person' }]);
      list.flush({ totalCount: 0, results: [] });
      undo.flush(null, { status: 204, statusText: 'No Content' });
    });

    it('adds a gamertag to an Xbox account history', () => {
      const dto = { xboxUserID: 2535400000000001, gamertag: 'Old Tag', firstSeen: '2010-01-01T00:00:00Z', lastSeen: null, note: 'From an old roster' };
      service.addGamertagHistory(dto).subscribe();
      const req = httpTestingController.expectOne('/api/GamertagClaims/AddGamertagHistory');
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual(dto);
      req.flush(null, { status: 204, statusText: 'No Content' });
    });

    it('removes a row from the history', () => {
      service.removeGamertagHistory(77).subscribe();
      const req = httpTestingController.expectOne('/api/GamertagClaims/RemoveGamertagHistory/77');
      expect(req.request.method).toBe('DELETE');
      req.flush(null, { status: 204, statusText: 'No Content' });
    });

    it('queues the XUID lookup and reads its status', () => {
      const status = { placeholders: 10, due: 3, found: 5, notFound: 1, failed: 1, lastLookedUpAt: '2026-10-30T12:00:00Z' };
      let lookUpResult: unknown, statusResult: unknown;
      service.lookUpXuids().subscribe(r => lookUpResult = r);
      service.getXuidLookupStatus().subscribe(r => statusResult = r);
      const lookUp = httpTestingController.expectOne('/api/GamertagClaims/LookUpXuids');
      expect(lookUp.request.method).toBe('POST');
      expect(lookUp.request.body).toBeNull();
      lookUp.flush(status, { status: 202, statusText: 'Accepted' });
      httpTestingController.expectOne('/api/GamertagClaims/XuidLookupStatus').flush(status);
      expect(lookUpResult).toEqual(status);
      expect(statusResult).toEqual(status);
    });

    it("fetches an Xbox account's past gamertags by XUID or user ID", () => {
      const gamertags: string[] = [];
      service.getPastGamertags({ xboxUserID: 2535400000000117 }).subscribe(account => gamertags.push(account?.currentGamertag ?? 'none'));
      service.getPastGamertags({ userID: 117 }).subscribe(account => gamertags.push(account?.currentGamertag ?? 'none'));
      const byXuid = httpTestingController.expectOne(r => r.url === '/api/GamertagClaims/PastGamertags' && r.params.get('xboxUserID') === '2535400000000117' && !r.params.has('userID'));
      const byUserID = httpTestingController.expectOne(r => r.url === '/api/GamertagClaims/PastGamertags' && r.params.get('userID') === '117' && !r.params.has('xboxUserID'));
      byXuid.flush({ xboxUserID: 2535400000000117, currentGamertag: 'Legendary', userID: 117, pastGamertags: [] });
      byUserID.flush({ xboxUserID: 2535400000000117, currentGamertag: 'Legendary', userID: 117, pastGamertags: [] });
      expect(gamertags).toEqual(['Legendary', 'Legendary']);
    });

    it('gives null for an Xbox account that does not exist (404)', (done) => {
      service.getPastGamertags({ xboxUserID: 999 }).subscribe(result => {
        expect(result).toBeNull();
        done();
      });
      httpTestingController.expectOne(r => r.url === '/api/GamertagClaims/PastGamertags').flush('Not found', { status: 404, statusText: 'Not Found' });
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

      const req = httpTestingController.expectOne('/api/seasons/current');
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
      const req = httpTestingController.expectOne('/api/events');
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

      const req = httpTestingController.expectOne('/api/seasons/1/name');
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

      const req = httpTestingController.expectOne('/api/seasons/999/name');
      req.flush('{"status":404,"title":"Not found"}', { status: 404, statusText: 'Not Found' });
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

      const req = httpTestingController.expectOne('/api/seasons/1/signups');
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

      const req = httpTestingController.expectOne('/api/seasons/1/signups/mine/timeslots?offset=300');
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

      const req = httpTestingController.expectOne('/api/seasons/1/signups/mine?offset=300');
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

      const req = httpTestingController.expectOne('/api/seasons/1/draft/teams');
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

      const req = httpTestingController.expectOne('/api/seasons/1/draft/player-pool');
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

      const req = httpTestingController.expectOne('/api/seasons/1/draft/captains/locked');
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

      for (const url of ['/api/seasons/1/draft/captains', '/api/seasons/1/draft/captains/resort']) {
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

      const req = httpTestingController.expectOne('/api/seasons/1/draft/captains');
      expect(req.request.method).toBe('POST');
      expect(req.request.headers.has('SignalRConnectionID')).toBe(false);
      req.flush({});
    });

    it('should add captain with connection ID', (done) => {
      const dto = { seasonID: 1, playerID: 1 } as any;

      service.addCaptain(dto, 'connection-123').subscribe({
        next: () => done()
      });

      const req = httpTestingController.expectOne('/api/seasons/1/draft/captains');
      expect(req.request.method).toBe('POST');
      expect(req.request.headers.get('SignalRConnectionID')).toBe('connection-123');
      req.flush({});
    });

    it('should resort captain with connection ID', (done) => {
      const dto = { seasonID: 1, playerID: 1 } as any;

      service.resortCaptain(dto, 'connection-456').subscribe({
        next: () => done()
      });

      const req = httpTestingController.expectOne('/api/seasons/1/draft/captains/resort');
      expect(req.request.method).toBe('POST');
      expect(req.request.headers.get('SignalRConnectionID')).toBe('connection-456');
      req.flush({});
    });

    it('should remove captain with connection ID', (done) => {
      const dto = { seasonID: 1, playerID: 1 } as any;

      service.removeCaptain(dto, 'connection-789').subscribe({
        next: () => done()
      });

      const req = httpTestingController.expectOne('/api/seasons/1/draft/captains/remove');
      expect(req.request.method).toBe('POST');
      expect(req.request.headers.get('SignalRConnectionID')).toBe('connection-789');
      req.flush({});
    });
  });

  describe('player operations with SignalR connection', () => {
    it('should remove player from team with connection ID', (done) => {
      const dto = { seasonID: 1, teamID: 1, playerID: 1 } as any;

      service.removePlayerFromTeam(dto, 'conn-1').subscribe({
        next: () => done()
      });

      const req = httpTestingController.expectOne('/api/seasons/1/draft/players/remove');
      expect(req.request.method).toBe('POST');
      expect(req.request.headers.get('SignalRConnectionID')).toBe('conn-1');
      req.flush({});
    });

    it('should move player to team with connection ID', (done) => {
      const dto = { seasonID: 1, teamID: 1, playerID: 1 } as any;

      service.movePlayerToTeam(dto, 'conn-2').subscribe({
        next: () => done()
      });

      const req = httpTestingController.expectOne('/api/seasons/1/draft/players/move');
      expect(req.request.method).toBe('POST');
      expect(req.request.headers.get('SignalRConnectionID')).toBe('conn-2');
      req.flush({});
    });

    it('should add player to team with connection ID', (done) => {
      const dto = { seasonID: 1, teamID: 1, playerID: 1 } as any;

      service.addPlayerToTeam(dto, 'conn-3').subscribe({
        next: () => done()
      });

      const req = httpTestingController.expectOne('/api/seasons/1/draft/picks');
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

      const req = httpTestingController.expectOne('/api/seasons/1/signups/mine');
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

      const req = httpTestingController.expectOne('/api/seasons/1/draft/captains/lock');
      expect(req.request.method).toBe('POST');
      expect(req.request.headers.get('SignalRConnectionID')).toBe('lock-conn');
      req.flush({});
    });

    it('should unlock captains with connection ID', (done) => {
      service.unlockCaptains(1, 'unlock-conn').subscribe({
        next: () => done()
      });

      const req = httpTestingController.expectOne('/api/seasons/1/draft/captains/unlock');
      expect(req.request.method).toBe('POST');
      expect(req.request.headers.get('SignalRConnectionID')).toBe('unlock-conn');
      req.flush({});
    });
  });
});
