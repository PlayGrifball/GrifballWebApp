import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { PastGamertagsComponent } from './pastGamertags.component';
import type { PastGamertagsDto } from '../../api/dtos/gamertagClaimDto';

const pastGamertagsUrl = '/api/past-gamertags';

function account(changes: Partial<PastGamertagsDto> = {}): PastGamertagsDto {
  return {
    xboxUserID: 2535400000000117, currentGamertag: 'Legendary117', userID: 117,
    pastGamertags: [
      {
        xboxGamertagHistoryID: 1, xboxUserID: 2535400000000117, gamertag: 'Legendary', firstSeen: '2010-01-01T00:00:00Z',
        lastSeen: '2012-06-01T00:00:00Z', source: 'Manual', note: 'From an old roster', canRemove: true,
      },
      {
        xboxGamertagHistoryID: 2, xboxUserID: 2535400000000117, gamertag: 'Old Chief', firstSeen: null,
        lastSeen: null, source: 'RenameDetected', note: null, canRemove: false,
      },
    ],
    ...changes,
  };
}

describe('PastGamertagsComponent', () => {
  let fixture: ComponentFixture<PastGamertagsComponent>;
  let http: HttpTestingController;
  let snackBar: jasmine.SpyObj<MatSnackBar>;

  function create(inputs: { xboxUserID?: number, userID?: number } = {}): void {
    snackBar = jasmine.createSpyObj('MatSnackBar', ['open']);
    TestBed.configureTestingModule({
      providers: [provideHttpClient(withXhr()), provideHttpClientTesting(), provideRouter([]), { provide: MatSnackBar, useValue: snackBar }],
    });
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(PastGamertagsComponent);
    if (inputs.xboxUserID !== undefined)
      fixture.componentRef.setInput('xboxUserID', inputs.xboxUserID);
    if (inputs.userID !== undefined)
      fixture.componentRef.setInput('userID', inputs.userID);
    fixture.detectChanges();
  }

  afterEach(() => http.verify());

  const el = () => fixture.nativeElement as HTMLElement;
  const rows = () => Array.from(el().querySelectorAll('tbody tr'));
  const cells = (row: Element) => Array.from(row.querySelectorAll('td')).map(td => td.textContent!.replace(/\s+/g, ' ').trim());
  const findButton = (text: string) => Array.from(el().querySelectorAll('button')).find(b => b.textContent!.trim() === text)!;

  async function settle(): Promise<void> {
    await fixture.whenStable();
    fixture.detectChanges();
  }

  function type(id: string, value: string): void {
    const input = el().querySelector<HTMLInputElement>('#' + id)!;
    input.value = value;
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();
  }

  it('shows a hint and fetches nothing until an XUID or user ID is given', () => {
    create();
    expect(el().textContent).toContain('Enter an XUID or a user ID');
  });

  it('loads by xboxUserID and shows the current gamertag and its history', async () => {
    create({ xboxUserID: 2535400000000117 });
    const req = http.expectOne(r => r.url === pastGamertagsUrl);
    expect(req.request.params.get('xboxUserID')).toBe('2535400000000117');
    expect(req.request.params.has('userID')).toBeFalse();
    req.flush(account());
    await settle();

    expect(el().textContent).toContain('Legendary117');
    expect(rows().length).toBe(2);
    const [manual, detected] = rows();
    expect(cells(manual)[0]).toBe('Legendary');
    expect(cells(manual)[3]).toBe('Manual');
    expect(cells(manual)[4]).toBe('From an old roster');
    expect(cells(manual)).toContain('Remove');
    expect(cells(detected)[0]).toBe('Old Chief');
    expect(cells(detected)[1]).toBe('unknown');
    expect(cells(detected)[2]).toBe('unknown');
    expect(cells(detected)).not.toContain('Remove');
  });

  it('loads by userID', async () => {
    create({ userID: 117 });
    const req = http.expectOne(r => r.url === pastGamertagsUrl);
    expect(req.request.params.get('userID')).toBe('117');
    expect(req.request.params.has('xboxUserID')).toBeFalse();
    req.flush(account());
    await settle();
  });

  it('removes a manually-added row, then shows the list as it is now', async () => {
    create({ xboxUserID: 2535400000000117 });
    http.expectOne(r => r.url === pastGamertagsUrl).flush(account());
    await settle();

    findButton('Remove').click();
    fixture.detectChanges();
    http.expectOne('/api/past-gamertags/1/remove').flush(null, { status: 204, statusText: 'No Content' });
    fixture.detectChanges();
    http.expectOne(r => r.url === pastGamertagsUrl).flush(account({ pastGamertags: [account().pastGamertags[1]] }));
    await settle();

    expect(rows().length).toBe(1);
  });

  it("says why a row couldn't be removed", async () => {
    create({ xboxUserID: 2535400000000117 });
    http.expectOne(r => r.url === pastGamertagsUrl).flush(account());
    await settle();

    findButton('Remove').click();
    http.expectOne('/api/past-gamertags/1/remove')
      .flush({ title: 'That row cannot be removed.' }, { status: 400, statusText: 'Bad Request' });
    expect(snackBar.open).toHaveBeenCalledWith('That row cannot be removed.', 'Close');
  });

  it('adds a gamertag with a note, then reloads the list', async () => {
    create({ xboxUserID: 2535400000000117 });
    http.expectOne(r => r.url === pastGamertagsUrl).flush(account({ pastGamertags: [] }));
    await settle();

    type('pg-gamertag', 'New Tag');
    type('pg-first-seen', '2015-05-01');
    type('pg-note', 'From an old roster');
    findButton('Add gamertag').click();

    const req = http.expectOne('/api/past-gamertags');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({
      xboxUserID: 2535400000000117, gamertag: 'New Tag', firstSeen: '2015-05-01T00:00:00Z', lastSeen: null, note: 'From an old roster',
    });
    req.flush(null, { status: 204, statusText: 'No Content' });
    fixture.detectChanges();
    http.expectOne(r => r.url === pastGamertagsUrl).flush(account({ pastGamertags: [] }));
    await settle();

    expect(el().querySelector<HTMLInputElement>('#pg-gamertag')!.value).toBe('');
  });

  it("says why a gamertag couldn't be added, keeping the form", async () => {
    create({ xboxUserID: 2535400000000117 });
    http.expectOne(r => r.url === pastGamertagsUrl).flush(account({ pastGamertags: [] }));
    await settle();

    type('pg-gamertag', 'New Tag');
    findButton('Add gamertag').click();
    http.expectOne('/api/past-gamertags').flush({ title: 'That gamertag is already recorded.' }, { status: 400, statusText: 'Bad Request' });
    fixture.detectChanges();

    expect(el().querySelector('[role="alert"]')!.textContent).toContain('That gamertag is already recorded.');
    expect(el().querySelector<HTMLInputElement>('#pg-gamertag')!.value).toBe('New Tag');
  });

  it('says there is no such Xbox account (404)', async () => {
    create({ xboxUserID: 999 });
    http.expectOne(r => r.url === pastGamertagsUrl).flush('Not found', { status: 404, statusText: 'Not Found' });
    await settle();
    expect(el().textContent).toContain('No Xbox account found');
  });
});
