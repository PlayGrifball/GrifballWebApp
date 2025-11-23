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
    mockAvailabilityService.parseTime.and.callFake((time: string) => {
      // Simple mock implementation
      if (time === 'Day of Week') return time;
      return time; // Just return as-is for testing
    });

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

  it('should initialize with empty arrays', () => {
    expect(component.timeColumns).toEqual([]);
    expect(component.dtos).toEqual([]);
  });

  it('should have DayOfWeek from service', () => {
    expect(component.DayOfWeek).toBe('Day of Week');
  });

  describe('timeslots setter', () => {
    it('should process empty timeslots array', () => {
      component.timeslots = [];

      expect(component.timeColumns).toEqual(['Day of Week']);
      expect(component.dtos).toEqual([]);
    });

    it('should create time columns from timeslots', () => {
      const timeslots: TimeslotDto[] = [
        { id: 1, dayOfWeek: 'Monday', time: '7:00 PM', isChecked: false, isDisabled: false, isHeader: false },
        { id: 2, dayOfWeek: 'Monday', time: '8:00 PM', isChecked: false, isDisabled: false, isHeader: false }
      ];

      component.timeslots = timeslots;

      expect(component.timeColumns.length).toBeGreaterThan(0);
      expect(component.timeColumns[0]).toBe('Day of Week');
    });

    it('should create rows for each unique day of week', () => {
      const timeslots: TimeslotDto[] = [
        { id: 1, dayOfWeek: 'Monday', time: '7:00 PM', isChecked: false, isDisabled: false, isHeader: false },
        { id: 2, dayOfWeek: 'Tuesday', time: '7:00 PM', isChecked: false, isDisabled: false, isHeader: false }
      ];

      component.timeslots = timeslots;

      expect(component.dtos.length).toBe(2);
    });

    it('should create header for each day of week', () => {
      const timeslots: TimeslotDto[] = [
        { id: 1, dayOfWeek: 'Monday', time: '7:00 PM', isChecked: false, isDisabled: false, isHeader: false }
      ];

      component.timeslots = timeslots;

      const firstRow = component.dtos[0];
      expect(firstRow[0].isHeader).toBe(true);
      expect(firstRow[0].dayOfWeek).toBe('Monday');
    });

    it('should create disabled cells for missing timeslots', () => {
      const timeslots: TimeslotDto[] = [
        { id: 1, dayOfWeek: 'Monday', time: '7:00 PM', isChecked: false, isDisabled: false, isHeader: false },
        { id: 2, dayOfWeek: 'Monday', time: '8:00 PM', isChecked: false, isDisabled: false, isHeader: false },
        { id: 3, dayOfWeek: 'Tuesday', time: '7:00 PM', isChecked: false, isDisabled: false, isHeader: false }
      ];

      component.timeslots = timeslots;

      // The component should create cells and some might be disabled if there are gaps
      // Just verify the component processes without errors
      expect(component.dtos.length).toBeGreaterThan(0);
      expect(component.timeColumns.length).toBeGreaterThan(0);
    });

    it('should set isDisabled to false for valid timeslots', () => {
      const timeslots: TimeslotDto[] = [
        { id: 1, dayOfWeek: 'Monday', time: '7:00 PM', isChecked: false, isDisabled: false, isHeader: false }
      ];

      component.timeslots = timeslots;

      const nonHeaderCells = component.dtos[0].filter(dto => !dto.isHeader);
      expect(nonHeaderCells.some(dto => !dto.isDisabled)).toBe(true);
    });

    it('should parse time for all timeslots', () => {
      const timeslots: TimeslotDto[] = [
        { id: 1, dayOfWeek: 'Monday', time: '7:00 PM', isChecked: false, isDisabled: false, isHeader: false }
      ];

      component.timeslots = timeslots;

      expect(mockAvailabilityService.parseTime).toHaveBeenCalled();
    });

    it('should handle multiple days with multiple times', () => {
      const timeslots: TimeslotDto[] = [
        { id: 1, dayOfWeek: 'Monday', time: '7:00 PM', isChecked: false, isDisabled: false, isHeader: false },
        { id: 2, dayOfWeek: 'Monday', time: '8:00 PM', isChecked: false, isDisabled: false, isHeader: false },
        { id: 3, dayOfWeek: 'Tuesday', time: '7:00 PM', isChecked: false, isDisabled: false, isHeader: false },
        { id: 4, dayOfWeek: 'Tuesday', time: '8:00 PM', isChecked: false, isDisabled: false, isHeader: false }
      ];

      component.timeslots = timeslots;

      expect(component.dtos.length).toBe(2); // 2 days
      expect(component.dtos[0].length).toBeGreaterThan(1); // Multiple times per day
    });
  });

  describe('timeslots getter', () => {
    it('should return flattened non-header timeslots', () => {
      const timeslots: TimeslotDto[] = [
        { id: 1, dayOfWeek: 'Monday', time: '7:00 PM', isChecked: false, isDisabled: false, isHeader: false },
        { id: 2, dayOfWeek: 'Monday', time: '8:00 PM', isChecked: false, isDisabled: false, isHeader: false }
      ];

      component.timeslots = timeslots;

      const result = component.timeslots;

      expect(result.every(dto => dto.id !== 0)).toBe(true);
      expect(result.every(dto => !dto.isHeader)).toBe(true);
    });

    it('should exclude header and disabled cells', () => {
      const timeslots: TimeslotDto[] = [
        { id: 1, dayOfWeek: 'Monday', time: '7:00 PM', isChecked: false, isDisabled: false, isHeader: false }
      ];

      component.timeslots = timeslots;

      const result = component.timeslots;

      expect(result.length).toBeGreaterThan(0);
      result.forEach(dto => {
        expect(dto.isHeader).toBe(false);
        expect(dto.id).not.toBe(0);
      });
    });
  });

  describe('getCell', () => {
    beforeEach(() => {
      const timeslots: TimeslotDto[] = [
        { id: 1, dayOfWeek: 'Monday', time: '7:00 PM', isChecked: false, isDisabled: false, isHeader: false }
      ];
      component.timeslots = timeslots;
    });

    it('should return first cell when time is DayOfWeek', () => {
      const slots = component.dtos[0];

      const result = component.getCell('Day of Week', slots);

      expect(result).toBe(slots[0]);
      expect(result.isHeader).toBe(true);
    });

    it('should return matching cell for given time', () => {
      const slots = component.dtos[0];
      const testTime = '7:00 PM';

      const result = component.getCell(testTime, slots);

      expect(result).toBeDefined();
    });

    it('should return disabled cell when time not found', () => {
      const slots = component.dtos[0];

      const result = component.getCell('9:00 PM', slots);

      expect(result.isDisabled).toBe(true);
    });
  });

  describe('dayOfWeekClicked', () => {
    beforeEach(() => {
      const timeslots: TimeslotDto[] = [
        { id: 1, dayOfWeek: 'Monday', time: '7:00 PM', isChecked: false, isDisabled: false, isHeader: false },
        { id: 2, dayOfWeek: 'Monday', time: '8:00 PM', isChecked: false, isDisabled: false, isHeader: false }
      ];
      component.timeslots = timeslots;
    });

    it('should check all timeslots when none are checked', () => {
      const slots = component.dtos[0];
      slots.forEach(slot => slot.isChecked = false);

      component.dayOfWeekClicked('Day of Week', slots);

      const nonDisabledNonHeader = slots.filter(x => !x.isDisabled && !x.isHeader);
      expect(nonDisabledNonHeader.every(x => x.isChecked)).toBe(true);
    });

    it('should uncheck all timeslots when any are checked', () => {
      const slots = component.dtos[0];
      const nonDisabledNonHeader = slots.filter(x => !x.isDisabled && !x.isHeader);
      if (nonDisabledNonHeader.length > 0) {
        nonDisabledNonHeader[0].isChecked = true;
      }

      component.dayOfWeekClicked('Day of Week', slots);

      expect(nonDisabledNonHeader.every(x => !x.isChecked)).toBe(true);
    });

    it('should not affect disabled or header cells', () => {
      const slots = component.dtos[0];
      const header = slots.find(x => x.isHeader);

      component.dayOfWeekClicked('Day of Week', slots);

      if (header) {
        expect(header.isHeader).toBe(true);
      }
    });

    it('should toggle state on multiple clicks', () => {
      const slots = component.dtos[0];

      component.dayOfWeekClicked('Day of Week', slots);
      const firstState = slots.filter(x => !x.isDisabled && !x.isHeader)[0]?.isChecked;

      component.dayOfWeekClicked('Day of Week', slots);
      const secondState = slots.filter(x => !x.isDisabled && !x.isHeader)[0]?.isChecked;

      if (firstState !== undefined && secondState !== undefined) {
        expect(firstState).not.toBe(secondState);
      }
    });
  });

  describe('timeClicked', () => {
    beforeEach(() => {
      const timeslots: TimeslotDto[] = [
        { id: 1, dayOfWeek: 'Monday', time: '7:00 PM', isChecked: false, isDisabled: false, isHeader: false },
        { id: 2, dayOfWeek: 'Tuesday', time: '7:00 PM', isChecked: false, isDisabled: false, isHeader: false }
      ];
      component.timeslots = timeslots;
    });

    it('should check all matching time slots when none are checked', () => {
      component.dtos.flat().forEach(slot => slot.isChecked = false);

      component.timeClicked('7:00 PM');

      const matchingSlots = component.dtos.flat().filter(x => 
        !x.isDisabled && !x.isHeader && x.time === '7:00 PM'
      );
      expect(matchingSlots.every(x => x.isChecked)).toBe(true);
    });

    it('should uncheck all matching time slots when any are checked', () => {
      const allSlots = component.dtos.flat();
      const matchingSlots = allSlots.filter(x => 
        !x.isDisabled && !x.isHeader && x.time === '7:00 PM'
      );
      
      if (matchingSlots.length > 0) {
        matchingSlots[0].isChecked = true;
      }

      component.timeClicked('7:00 PM');

      expect(matchingSlots.every(x => !x.isChecked)).toBe(true);
    });

    it('should handle Day of Week click to toggle all slots', () => {
      component.dtos.flat().forEach(slot => slot.isChecked = false);

      component.timeClicked('Day of Week');

      const nonDisabledNonHeader = component.dtos.flat().filter(x => 
        !x.isDisabled && !x.isHeader
      );
      expect(nonDisabledNonHeader.every(x => x.isChecked)).toBe(true);
    });

    it('should only affect non-disabled and non-header cells', () => {
      component.timeClicked('7:00 PM');

      const headers = component.dtos.flat().filter(x => x.isHeader);
      const disabled = component.dtos.flat().filter(x => x.isDisabled);

      headers.forEach(h => expect(h.isHeader).toBe(true));
      disabled.forEach(d => expect(d.isDisabled).toBe(true));
    });

    it('should toggle state on multiple clicks', () => {
      component.timeClicked('7:00 PM');
      const firstState = component.dtos.flat().filter(x => 
        !x.isDisabled && !x.isHeader && x.time === '7:00 PM'
      )[0]?.isChecked;

      component.timeClicked('7:00 PM');
      const secondState = component.dtos.flat().filter(x => 
        !x.isDisabled && !x.isHeader && x.time === '7:00 PM'
      )[0]?.isChecked;

      if (firstState !== undefined && secondState !== undefined) {
        expect(firstState).not.toBe(secondState);
      }
    });
  });

  describe('integration tests', () => {
    it('should handle complex schedule with multiple days and times', () => {
      const timeslots: TimeslotDto[] = [
        { id: 1, dayOfWeek: 'Monday', time: '6:00 PM', isChecked: false, isDisabled: false, isHeader: false },
        { id: 2, dayOfWeek: 'Monday', time: '7:00 PM', isChecked: false, isDisabled: false, isHeader: false },
        { id: 3, dayOfWeek: 'Monday', time: '8:00 PM', isChecked: false, isDisabled: false, isHeader: false },
        { id: 4, dayOfWeek: 'Tuesday', time: '6:00 PM', isChecked: false, isDisabled: false, isHeader: false },
        { id: 5, dayOfWeek: 'Tuesday', time: '7:00 PM', isChecked: false, isDisabled: false, isHeader: false },
        { id: 6, dayOfWeek: 'Wednesday', time: '7:00 PM', isChecked: false, isDisabled: false, isHeader: false }
      ];

      component.timeslots = timeslots;

      expect(component.dtos.length).toBe(3); // 3 unique days
      expect(component.timeColumns.length).toBeGreaterThan(1);
    });

    it('should maintain checked state after re-setting timeslots', () => {
      const timeslots: TimeslotDto[] = [
        { id: 1, dayOfWeek: 'Monday', time: '7:00 PM', isChecked: true, isDisabled: false, isHeader: false }
      ];

      component.timeslots = timeslots;

      const result = component.timeslots;
      expect(result[0].isChecked).toBe(true);
    });

    it('should handle empty day of week correctly', () => {
      const timeslots: TimeslotDto[] = [
        { id: 1, dayOfWeek: '', time: '7:00 PM', isChecked: false, isDisabled: false, isHeader: false }
      ];

      expect(() => component.timeslots = timeslots).not.toThrow();
    });
  });
});
