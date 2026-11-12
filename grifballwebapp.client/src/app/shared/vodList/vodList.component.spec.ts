import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { VodService } from '@api/services/vod.service';
import { getVods, isVideoID, vodDuration } from '../../api/vods';
import type { VodDto } from '@api/models';
import { EMBED_ORIGIN, VodListComponent } from './vodList.component';

const vod = (overrides: Partial<VodDto> = {}): VodDto => ({
  vodID: 1,
  youTubeVideoID: 'Hb2JmxJhQd8',
  title: 'AGLA Summer Minor Championship | Low IQ vs Reach for the Balls',
  publishedAt: '2026-10-16T00:00:00Z',
  durationSeconds: 6601,
  seasonID: 5,
  seasonName: 'Summer League 2026',
  seasonMatchID: 9,
  teams: [{ teamID: 31, teamName: 'Low IQ' }, { teamID: 32, teamName: 'Reach for the Balls' }],
  ...overrides,
});

describe('VodListComponent', () => {
  let fixture: ComponentFixture<VodListComponent>;
  let api: jasmine.SpyObj<VodService>;
  const el = () => fixture.nativeElement as HTMLElement;

  async function create(scope: string, id: number, vods: VodDto[]): Promise<void> {
    for (const method of [api.getSeasonMatchVods, api.getSeasonVods, api.getTeamVods, api.getPlayerVods])
      method.and.returnValue(of(vods));
    fixture = TestBed.createComponent(VodListComponent);
    fixture.componentRef.setInput('scope', scope);
    fixture.componentRef.setInput('id', id);
    fixture.detectChanges();
    await settle();
  }

  async function settle(): Promise<void> {
    await fixture.whenStable();
    fixture.detectChanges();
  }

  beforeEach(() => {
    api = jasmine.createSpyObj<VodService>('VodService', ['getSeasonMatchVods', 'getSeasonVods', 'getTeamVods', 'getPlayerVods']);
    TestBed.configureTestingModule({
      imports: [VodListComponent],
      providers: [provideRouter([]), { provide: VodService, useValue: api }],
    });
  });

  it("asks for its page's VODs and shows nothing when there are none", async () => {
    await create('team', 31, []);

    expect(api.getTeamVods).toHaveBeenCalledWith({ teamID: 31 });
    expect(el().querySelector('section')).toBeNull();
  });

  it('shows nothing when they could not be loaded', async () => {
    api.getSeasonVods.and.returnValue(throwError(() => new Error('down')));
    fixture = TestBed.createComponent(VodListComponent);
    fixture.componentRef.setInput('scope', 'season');
    fixture.componentRef.setInput('id', 5);
    fixture.detectChanges();
    await settle();

    expect(el().querySelector('section')).toBeNull();
  });

  it('lists each VOD with a play button, its length, a link out and what it is of', async () => {
    await create('player', 10, [vod()]);

    const section = el().querySelector('section')!;
    expect(section.getAttribute('aria-labelledby')).toBe('vods-player-10');
    expect(section.querySelector('h2')!.id).toBe('vods-player-10');
    const play = section.querySelector<HTMLButtonElement>('.vod-card__play')!;
    expect(play.getAttribute('aria-label')).withContext('Named by its own text').toBeNull();
    expect(play.textContent).toContain('Play');
    expect(play.querySelector('.vod-card__hidden')?.textContent).toBe(': AGLA Summer Minor Championship | Low IQ vs Reach for the Balls');
    expect(play.textContent).toContain('1:50:01');
    const title = section.querySelector<HTMLAnchorElement>('.vod-card__title')!;
    expect(title.href).toBe('https://www.youtube.com/watch?v=Hb2JmxJhQd8');
    expect(title.target).toBe('_blank');
    const links = Array.from(section.querySelectorAll('.vod-card__link')).map(a => [a.textContent!.trim(), a.getAttribute('href')]);
    expect(links).toEqual([
      ['Summer League 2026', '/season/5'],
      ['Match', '/seasonmatch/9'],
      ['Low IQ', '/season/5/team/31'],
      ['Reach for the Balls', '/season/5/team/32'],
    ]);
    expect(section.querySelector('iframe')).withContext('The player loads on Play only').toBeNull();
  });

  it("leaves out the page's own season, and the teams on a match page", async () => {
    await create('season', 5, [vod()]);
    expect(Array.from(el().querySelectorAll('.vod-card__link')).map(a => a.textContent!.trim())).toEqual(['Match', 'Low IQ', 'Reach for the Balls']);

    await create('seasonMatch', 9, [vod({ durationSeconds: null })]);
    expect(Array.from(el().querySelectorAll('.vod-card__link')).map(a => a.textContent!.trim())).toEqual(['Summer League 2026']);
    expect(el().querySelector('.vod-card__length')).toBeNull();
  });

  it('plays a VOD in its card from youtube-nocookie.com', async () => {
    await create('seasonMatch', 9, [vod(), vod({ vodID: 2, youTubeVideoID: 'NnBy9EZai9Q', title: 'Blitz League II' })]);

    el().querySelectorAll<HTMLButtonElement>('.vod-card__play')[1].click();
    await settle();

    const frames = el().querySelectorAll('iframe');
    expect(frames).toHaveSize(1);
    expect(frames[0].src).toBe(`${EMBED_ORIGIN}/embed/NnBy9EZai9Q?autoplay=1&rel=0`);
    expect(frames[0].title).toBe('Blitz League II');
    expect(el().querySelectorAll('.vod-card__play')).toHaveSize(1);
  });

  it('never embeds what is not a video ID, only links it', async () => {
    await create('team', 31, [vod({ youTubeVideoID: 'x"><script>' })]);

    expect(el().querySelector('.vod-card__play')).toBeNull();
    fixture.componentInstance.play(fixture.componentInstance.vods()[0]);
    await settle();
    expect(el().querySelector('iframe')).toBeNull();
    expect(fixture.componentInstance.embedUrl()).toBeNull();
  });
});

describe('getVods', () => {
  let api: jasmine.SpyObj<VodService>;

  beforeEach(() => {
    api = jasmine.createSpyObj<VodService>('VodService', ['getSeasonMatchVods', 'getSeasonVods', 'getTeamVods', 'getPlayerVods']);
  });

  it("asks for a season match's VODs", () => {
    api.getSeasonMatchVods.and.returnValue(of([]));
    getVods(api, 'seasonMatch', 9);
    expect(api.getSeasonMatchVods).toHaveBeenCalledWith({ seasonMatchID: 9 });
  });

  it("asks for a season's VODs", () => {
    api.getSeasonVods.and.returnValue(of([]));
    getVods(api, 'season', 5);
    expect(api.getSeasonVods).toHaveBeenCalledWith({ seasonID: 5 });
  });

  it("asks for a team's VODs", () => {
    api.getTeamVods.and.returnValue(of([]));
    getVods(api, 'team', 31);
    expect(api.getTeamVods).toHaveBeenCalledWith({ teamID: 31 });
  });

  it("asks for a player's VODs", () => {
    api.getPlayerVods.and.returnValue(of([]));
    getVods(api, 'player', 10);
    expect(api.getPlayerVods).toHaveBeenCalledWith({ userID: 10 });
  });
});

describe('vodDuration and isVideoID', () => {
  it('reads a length like YouTube shows it', () => {
    expect(vodDuration(6601)).toBe('1:50:01');
    expect(vodDuration(3500)).toBe('58:20');
    expect(vodDuration(5)).toBe('0:05');
    expect(vodDuration(null)).toBeNull();
    expect(vodDuration(undefined)).toBeNull();
  });

  it('knows a video ID', () => {
    expect(isVideoID('Hb2JmxJhQd8')).toBeTrue();
    expect(isVideoID('CJgRG--cbX0')).toBeTrue();
    expect(isVideoID('short')).toBeFalse();
    expect(isVideoID('../../evil1')).toBeFalse();
  });
});
