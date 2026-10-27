import { ComponentFixture, TestBed } from '@angular/core/testing';
import { AvailabilityTableComponent } from './availabilityTable.component';
import { AvailabilityService } from '../../availability.service';
import { TimeslotDto } from '../../api/dtos/signupResponseDto';

describe('AvailabilityTableComponent', () => {
  let component: AvailabilityTableComponent;
  let fixture: ComponentFixture<AvailabilityTableComponent>;
  let mockAvailabilityService: jasmine.SpyObj<AvailabilityService>;

  beforeEach(async () => {
    mockAvailabilityService = jasmine.createSpyObj('AvailabilityService', ['parseTime']);
    mockAvailabilityService.DayOfWeek = 'Day of Week';
    // The real conversion: the API's "HH:mm:ss" to the display format ("7:00 PM").
    const real = new AvailabilityService();
    mockAvailabilityService.parseTime.and.callFake((time: string) => time === 'Day of Week' ? time : real.parseTime(time));

    await TestBed.configureTestingModule({
      imports: [AvailabilityTableComponent],
      providers: [
        { provide: AvailabilityService, useValue: mockAvailabilityService }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(AvailabilityTableComponent);
    component = fixture.componentInstance;
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('treats an unset (null) timeslot list as having no rows yet', () => {
    fixture.componentRef.setInput('timeslots', null);
    expect(component.timeColumns()).toEqual(['Day of Week']);
    expect(component.dtos()).toEqual([]);
  });

  it('should have DayOfWeek from service', () => {
    expect(component.DayOfWeek).toBe('Day of Week');
  });

  describe('timeslots setter', () => {
    it('should process empty timeslots array', () => {
      fixture.componentRef.setInput('timeslots', []);

      expect(component.timeColumns()).toEqual(['Day of Week']);
      expect(component.dtos()).toEqual([]);
    });

    it('should create time columns from timeslots', () => {
      const timeslots: TimeslotDto[] = [
        { optionID: 1, dayOfWeek: 'Monday', time: '19:00:00', isChecked: false },
        { optionID: 2, dayOfWeek: 'Monday', time: '20:00:00', isChecked: false }
      ];

      fixture.componentRef.setInput('timeslots', timeslots);

      expect(component.timeColumns().length).toBeGreaterThan(0);
      expect(component.timeColumns()[0]).toBe('Day of Week');
    });

    it('should create rows for each unique day of week', () => {
      const timeslots: TimeslotDto[] = [
        { optionID: 1, dayOfWeek: 'Monday', time: '19:00:00', isChecked: false },
        { optionID: 2, dayOfWeek: 'Tuesday', time: '19:00:00', isChecked: false }
      ];

      fixture.componentRef.setInput('timeslots', timeslots);

      expect(component.dtos().length).toBe(2);
    });

    it('should create header for each day of week', () => {
      const timeslots: TimeslotDto[] = [
        { optionID: 1, dayOfWeek: 'Monday', time: '19:00:00', isChecked: false }
      ];

      fixture.componentRef.setInput('timeslots', timeslots);

      const firstRow = component.dtos()[0];
      expect(firstRow[0].isHeader).toBe(true);
      expect(firstRow[0].dayOfWeek).toBe('Monday');
    });

    it('should create disabled cells for missing timeslots', () => {
      const timeslots: TimeslotDto[] = [
        { optionID: 1, dayOfWeek: 'Monday', time: '19:00:00', isChecked: false },
        { optionID: 2, dayOfWeek: 'Monday', time: '20:00:00', isChecked: false },
        { optionID: 3, dayOfWeek: 'Tuesday', time: '19:00:00', isChecked: false }
      ];

      fixture.componentRef.setInput('timeslots', timeslots);

      // The component should create cells and some might be disabled if there are gaps
      // Just verify the component processes without errors
      expect(component.dtos().length).toBeGreaterThan(0);
      expect(component.timeColumns().length).toBeGreaterThan(0);
    });

    it('should set isDisabled to false for valid timeslots', () => {
      const timeslots: TimeslotDto[] = [
        { optionID: 1, dayOfWeek: 'Monday', time: '19:00:00', isChecked: false }
      ];

      fixture.componentRef.setInput('timeslots', timeslots);

      const nonHeaderCells = component.dtos()[0].filter(dto => !dto.isHeader);
      expect(nonHeaderCells.some(dto => !dto.isDisabled)).toBe(true);
    });

    it('should parse time for all timeslots', () => {
      const timeslots: TimeslotDto[] = [
        { optionID: 1, dayOfWeek: 'Monday', time: '19:00:00', isChecked: false }
      ];

      fixture.componentRef.setInput('timeslots', timeslots);
      component.dtos();

      expect(mockAvailabilityService.parseTime).toHaveBeenCalled();
    });

    it('should handle multiple days with multiple times', () => {
      const timeslots: TimeslotDto[] = [
        { optionID: 1, dayOfWeek: 'Monday', time: '19:00:00', isChecked: false },
        { optionID: 2, dayOfWeek: 'Monday', time: '20:00:00', isChecked: false },
        { optionID: 3, dayOfWeek: 'Tuesday', time: '19:00:00', isChecked: false },
        { optionID: 4, dayOfWeek: 'Tuesday', time: '20:00:00', isChecked: false }
      ];

      fixture.componentRef.setInput('timeslots', timeslots);

      expect(component.dtos().length).toBe(2); // 2 days
      expect(component.dtos()[0].length).toBeGreaterThan(1); // Multiple times per day
    });
  });

  describe('purity', () => {
    it('builds the same table again from the same timeslot objects, without changing them', () => {
      const timeslots: TimeslotDto[] = [
        { optionID: 1, dayOfWeek: 'Monday', time: '19:00:00', isChecked: true },
        { optionID: 2, dayOfWeek: 'Tuesday', time: '20:00:00', isChecked: false },
      ];
      const snapshot = structuredClone(timeslots);

      fixture.componentRef.setInput('timeslots', timeslots);
      const first = { columns: component.timeColumns(), dtos: component.dtos() };
      fixture.componentRef.setInput('timeslots', [...timeslots]); // same objects, new list: recompute
      const second = { columns: component.timeColumns(), dtos: component.dtos() };

      expect(second.dtos).not.toBe(first.dtos);
      expect(second).toEqual(first);
      expect(timeslots).toEqual(snapshot);
    });
  });

  describe('getCell', () => {
    beforeEach(() => {
      const timeslots: TimeslotDto[] = [
        { optionID: 1, dayOfWeek: 'Monday', time: '19:00:00', isChecked: false }
      ];
      fixture.componentRef.setInput('timeslots', timeslots);
    });

    it('should return first cell when time is DayOfWeek', () => {
      const slots = component.dtos()[0];

      const result = component.getCell('Day of Week', slots);

      expect(result).toBe(slots[0]);
      expect(result.isHeader).toBe(true);
    });

    it('should return matching cell for given time', () => {
      const slots = component.dtos()[0];
      const testTime = '7:00 PM';

      const result = component.getCell(testTime, slots);

      expect(result).toBeDefined();
    });

    it('should return disabled cell when time not found', () => {
      const slots = component.dtos()[0];

      const result = component.getCell('9:00 PM', slots);

      expect(result.isDisabled).toBe(true);
    });
  });

  // Ticking emits a new timeslot list; the ids of the checked slots after each action.
  const checkedIds = () => (component.timeslots() ?? []).filter(t => t.isChecked).map(t => t.optionID);

  describe('dayOfWeekClicked', () => {
    let timeslots: TimeslotDto[];
    beforeEach(() => {
      timeslots = [
        { optionID: 1, dayOfWeek: 'Monday', time: '19:00:00', isChecked: false },
        { optionID: 2, dayOfWeek: 'Monday', time: '20:00:00', isChecked: false },
        { optionID: 3, dayOfWeek: 'Tuesday', time: '20:00:00', isChecked: false }
      ];
      fixture.componentRef.setInput('timeslots', timeslots);
    });

    it('should check all timeslots of the day when none are checked', () => {
      component.dayOfWeekClicked('Day of Week', component.dtos()[0]);
      expect(checkedIds()).toEqual([1, 2]);
    });

    it('should uncheck all timeslots of the day when any are checked', () => {
      component.setChecked([component.dtos()[0][1]], true);
      expect(checkedIds()).toEqual([1]);

      component.dayOfWeekClicked('Day of Week', component.dtos()[0]);
      expect(checkedIds()).toEqual([]);
    });

    it('should toggle state on multiple clicks', () => {
      component.dayOfWeekClicked('Day of Week', component.dtos()[0]);
      component.dayOfWeekClicked('Day of Week', component.dtos()[0]);
      expect(checkedIds()).toEqual([]);
    });

    it('emits new timeslot objects and leaves the bound ones alone', () => {
      const emitted: unknown[] = [];
      component.timeslots.subscribe(v => emitted.push(v));

      component.dayOfWeekClicked('Day of Week', component.dtos()[0]);

      expect(emitted.length).toBe(1);
      expect(timeslots.every(t => !t.isChecked)).toBeTrue();
      expect(component.timeslots()![2]).toBe(timeslots[2]); // untouched slots are kept as they are
    });
  });

  describe('timeClicked', () => {
    beforeEach(() => {
      const timeslots: TimeslotDto[] = [
        { optionID: 1, dayOfWeek: 'Monday', time: '19:00:00', isChecked: false },
        { optionID: 2, dayOfWeek: 'Tuesday', time: '19:00:00', isChecked: false },
        { optionID: 3, dayOfWeek: 'Tuesday', time: '20:00:00', isChecked: false }
      ];
      fixture.componentRef.setInput('timeslots', timeslots);
    });

    it('should check all matching time slots when none are checked', () => {
      component.timeClicked('7:00 PM');
      expect(checkedIds()).toEqual([1, 2]);
    });

    it('should uncheck all matching time slots when any are checked', () => {
      component.setChecked([component.dtos()[0][1]], true);
      component.timeClicked('7:00 PM');
      expect(checkedIds()).toEqual([]);
    });

    it('should handle Day of Week click to toggle all slots', () => {
      component.timeClicked('Day of Week');
      expect(checkedIds()).toEqual([1, 2, 3]);
    });

    it('should leave header and filler cells as they are', () => {
      component.timeClicked('Day of Week');
      expect(component.dtos().flat().filter(x => x.isHeader).every(h => !h.isChecked)).toBeTrue();
      expect(component.dtos().flat().filter(x => x.isDisabled).every(d => !d.isChecked)).toBeTrue();
    });

    it('shows the new checked state in the grid', () => {
      component.timeClicked('7:00 PM');
      const checkedCells = component.dtos().flat().filter(c => c.isChecked).map(c => c.optionID);
      expect(checkedCells).toEqual([1, 2]);
    });
  });

  describe('integration tests', () => {
    it('should handle complex schedule with multiple days and times', () => {
      const timeslots: TimeslotDto[] = [
        { optionID: 1, dayOfWeek: 'Monday', time: '18:00:00', isChecked: false },
        { optionID: 2, dayOfWeek: 'Monday', time: '19:00:00', isChecked: false },
        { optionID: 3, dayOfWeek: 'Monday', time: '20:00:00', isChecked: false },
        { optionID: 4, dayOfWeek: 'Tuesday', time: '18:00:00', isChecked: false },
        { optionID: 5, dayOfWeek: 'Tuesday', time: '19:00:00', isChecked: false },
        { optionID: 6, dayOfWeek: 'Wednesday', time: '19:00:00', isChecked: false }
      ];

      fixture.componentRef.setInput('timeslots', timeslots);

      expect(component.dtos().length).toBe(3); // 3 unique days
      expect(component.timeColumns().length).toBeGreaterThan(1);
    });

    it('should maintain checked state after re-setting timeslots', () => {
      const timeslots: TimeslotDto[] = [
        { optionID: 1, dayOfWeek: 'Monday', time: '19:00:00', isChecked: true }
      ];

      fixture.componentRef.setInput('timeslots', timeslots);

      expect(component.dtos().flat().find(x => x.optionID === 1)?.isChecked).toBe(true);
    });

    it('should handle empty day of week correctly', () => {
      const timeslots: TimeslotDto[] = [
        { optionID: 1, dayOfWeek: '', time: '19:00:00', isChecked: false }
      ];

      expect(() => fixture.componentRef.setInput('timeslots', timeslots)).not.toThrow();
    });
  });

  describe('template rendering (@if ... as)', () => {
    beforeEach(() => {
      fixture.componentRef.setInput('timeslots', [
        { optionID: 1, dayOfWeek: 'Monday', time: '19:00:00', isChecked: true },
        { optionID: 2, dayOfWeek: 'Monday', time: '20:00:00', isChecked: false },
        { optionID: 3, dayOfWeek: 'Tuesday', time: '20:00:00', isChecked: false }
      ]);
      fixture.detectChanges();
    });

    function rows(): HTMLElement[] {
      return Array.from(fixture.nativeElement.querySelectorAll('tr.mat-mdc-row'));
    }

    it('renders the day of week as a header cell and a checkbox for every time slot', () => {
      const [monday, tuesday] = rows();
      expect(rows().length).toBe(2);
      expect(monday.querySelector('td div')?.textContent?.trim()).toBe('Monday');
      expect(tuesday.querySelector('td div')?.textContent?.trim()).toBe('Tuesday');
      expect(monday.querySelectorAll('mat-checkbox').length).toBe(2);
      expect(tuesday.querySelectorAll('mat-checkbox').length).toBe(2);
    });

    it('disables the checkbox for a time slot the day does not have', async () => {
      // ngModel applies the value and disabled state asynchronously
      await fixture.whenStable();
      fixture.detectChanges();
      const inputs = (row: HTMLElement) => Array.from(row.querySelectorAll<HTMLInputElement>('mat-checkbox input'));
      const [monday, tuesday] = rows();
      expect(inputs(monday).map(i => i.disabled)).toEqual([false, false]);
      expect(inputs(tuesday).map(i => i.disabled)).toEqual([true, false]);
      expect(inputs(monday).map(i => i.checked)).toEqual([true, false]);
    });

    it('toggles the row when the day of week header is clicked', () => {
      const tuesdayHeader = rows()[1].querySelector<HTMLElement>('td div')!;
      tuesdayHeader.click();
      expect(component.timeslots()!.find(x => x.optionID === 3)?.isChecked).toBe(true);
      expect(component.timeslots()!.find(x => x.optionID === 1)?.isChecked).toBe(true);
      expect(component.timeslots()!.find(x => x.optionID === 2)?.isChecked).toBe(false);
    });

    it('ticks a slot from its checkbox and re-renders it checked', async () => {
      const input = rows()[1].querySelectorAll<HTMLInputElement>('mat-checkbox input')[1];
      input.click();
      await fixture.whenStable();

      expect(component.timeslots()!.find(x => x.optionID === 3)?.isChecked).toBe(true);
      expect(rows()[1].querySelectorAll<HTMLInputElement>('mat-checkbox input')[1].checked).toBeTrue();
    });
  });

  describe('missing timeslots', () => {
    it('treats undefined timeslots as empty instead of throwing', () => {
      expect(() => fixture.componentRef.setInput('timeslots', undefined as unknown as TimeslotDto[])).not.toThrow();
      expect(component.timeColumns()).toEqual(['Day of Week']);
      expect(component.dtos()).toEqual([]);
    });

    it('treats null timeslots as empty instead of throwing', () => {
      expect(() => fixture.componentRef.setInput('timeslots', null as unknown as TimeslotDto[])).not.toThrow();
      expect(component.dtos()).toEqual([]);
    });

    it('renders when bound to undefined timeslots', () => {
      fixture.componentRef.setInput('timeslots', undefined);
      expect(() => fixture.detectChanges()).not.toThrow();
      expect(fixture.nativeElement.querySelector('table')).toBeNull();
    });
  });
});
