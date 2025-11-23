import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { ExcelComponent } from './excel.component';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';

describe('ExcelComponent', () => {
  let component: ExcelComponent;
  let fixture: ComponentFixture<ExcelComponent>;
  let httpTestingController: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ExcelComponent, HttpClientTestingModule, NoopAnimationsModule]
    }).compileComponents();

    httpTestingController = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpTestingController.verify();
  });

  it('should create', () => {
    fixture = TestBed.createComponent(ExcelComponent);
    component = fixture.componentInstance;
    
    expect(component).toBeTruthy();
  });

  it('should initialize with empty sheet info', () => {
    fixture = TestBed.createComponent(ExcelComponent);
    component = fixture.componentInstance;
    
    expect(component.sheetInfo()).toEqual([]);
  });

  it('should set sheet info from inputs when provided', () => {
    fixture = TestBed.createComponent(ExcelComponent);
    component = fixture.componentInstance;
    
    fixture.componentRef.setInput('inputSpreadsheetId', 'test-spreadsheet-123');
    fixture.componentRef.setInput('inputSheetName', 'Sheet1');
    
    component.ngOnInit();

    const sheetInfo = component.sheetInfo();
    expect(sheetInfo.length).toBe(1);
    expect(sheetInfo[0].spreadsheetID).toBe('test-spreadsheet-123');
    expect(sheetInfo[0].sheetName).toBe('Sheet1');
    expect(sheetInfo[0].name).toBe('');
  });

  it('should fetch default sheet info when inputs are not provided', () => {
    fixture = TestBed.createComponent(ExcelComponent);
    component = fixture.componentInstance;
    
    fixture.componentRef.setInput('inputSpreadsheetId', '');
    fixture.componentRef.setInput('inputSheetName', '');
    
    const mockSheetInfo = [
      { name: 'Default Sheet', spreadsheetID: 'default-123', sheetName: 'Default' }
    ];

    component.ngOnInit();

    const req = httpTestingController.expectOne('/api/Excel/DefaultSheetInfo');
    expect(req.request.method).toBe('GET');
    req.flush(mockSheetInfo);

    expect(component.sheetInfo()).toEqual(mockSheetInfo);
  });

  it('should use inputs over default when both spreadsheetId and sheetName are provided', () => {
    fixture = TestBed.createComponent(ExcelComponent);
    component = fixture.componentInstance;
    
    fixture.componentRef.setInput('inputSpreadsheetId', 'custom-spreadsheet');
    fixture.componentRef.setInput('inputSheetName', 'CustomSheet');
    
    component.ngOnInit();

    // Should not make HTTP request
    httpTestingController.expectNone('/api/Excel/DefaultSheetInfo');
    
    expect(component.sheetInfo().length).toBe(1);
  });

  it('should fetch default info when only spreadsheetId is provided', () => {
    fixture = TestBed.createComponent(ExcelComponent);
    component = fixture.componentInstance;
    
    fixture.componentRef.setInput('inputSpreadsheetId', 'spreadsheet-123');
    fixture.componentRef.setInput('inputSheetName', '');
    
    component.ngOnInit();

    const req = httpTestingController.expectOne('/api/Excel/DefaultSheetInfo');
    req.flush([]);
  });

  it('should fetch default info when only sheetName is provided', () => {
    fixture = TestBed.createComponent(ExcelComponent);
    component = fixture.componentInstance;
    
    fixture.componentRef.setInput('inputSpreadsheetId', '');
    fixture.componentRef.setInput('inputSheetName', 'Sheet1');
    
    component.ngOnInit();

    const req = httpTestingController.expectOne('/api/Excel/DefaultSheetInfo');
    req.flush([]);
  });

  it('should handle multiple sheet info from API', () => {
    fixture = TestBed.createComponent(ExcelComponent);
    component = fixture.componentInstance;
    
    fixture.componentRef.setInput('inputSpreadsheetId', '');
    fixture.componentRef.setInput('inputSheetName', '');
    
    const mockSheetInfo = [
      { name: 'Sheet 1', spreadsheetID: 'id-1', sheetName: 'Sheet1' },
      { name: 'Sheet 2', spreadsheetID: 'id-2', sheetName: 'Sheet2' }
    ];

    component.ngOnInit();

    const req = httpTestingController.expectOne('/api/Excel/DefaultSheetInfo');
    req.flush(mockSheetInfo);

    expect(component.sheetInfo().length).toBe(2);
    expect(component.sheetInfo()[0].name).toBe('Sheet 1');
    expect(component.sheetInfo()[1].name).toBe('Sheet 2');
  });

  it('should have empty inputs by default', () => {
    fixture = TestBed.createComponent(ExcelComponent);
    component = fixture.componentInstance;
    
    expect(component.inputSpreadsheetId()).toBe('');
    expect(component.inputSheetName()).toBe('');
  });
});
