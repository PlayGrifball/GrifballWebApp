import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ExcelStore } from './excel.store';

describe('ExcelStore', () => {
  let store: ExcelStore;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [ExcelStore, provideHttpClient(withXhr()), provideHttpClientTesting()] });
    store = TestBed.inject(ExcelStore);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('loads the default sheets, and keeps none when they fail', () => {
    const log = spyOn(console, 'log');
    expect(store.defaults()).toEqual([]);
    store.loadDefaults();
    http.expectOne('/api/sheets/default').flush('x', { status: 500, statusText: 'Server Error' });
    expect(store.defaults()).toEqual([]);
    expect(log).toHaveBeenCalled();

    store.loadDefaults();
    http.expectOne('/api/sheets/default').flush([{ name: 'Sheet 1', spreadsheetID: 'a', sheetName: 'One' }]);
    expect(store.defaults().map(s => s.name)).toEqual(['Sheet 1']);
  });
});
