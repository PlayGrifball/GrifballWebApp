import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { SearchBoxComponent } from './searchBox.component';
import { signal } from '@angular/core';

describe('SearchBoxComponent', () => {
  let component: SearchBoxComponent<string>;
  let fixture: ComponentFixture<SearchBoxComponent<string>>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [SearchBoxComponent]
    }).compileComponents();

    fixture = TestBed.createComponent(SearchBoxComponent<string>);
    component = fixture.componentInstance;
    
    // Set required value signal
    fixture.componentRef.setInput('value', signal(''));
    
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should have default type as text', () => {
    expect(component.type()).toBe('text');
  });

  it('should handle text input with debounce', fakeAsync(() => {
    const inputElement = document.createElement('input');
    inputElement.value = '  test query  ';
    const event = { target: inputElement } as unknown as Event;
    
    component.onInput(event);
    tick(300); // Wait for debounce
    
    expect(component.value()).toBe('test query'); // Should be trimmed
  }));

  it('should handle number input', fakeAsync(() => {
    const componentNumber = TestBed.createComponent(SearchBoxComponent<number>);
    componentNumber.componentRef.setInput('value', signal(0));
    componentNumber.componentRef.setInput('type', 'number');
    componentNumber.detectChanges();
    
    const inputElement = document.createElement('input');
    inputElement.value = '42';
    const event = { target: inputElement } as unknown as Event;
    
    componentNumber.componentInstance.onInput(event);
    tick(300); // Wait for debounce
    
    expect(componentNumber.componentInstance.value()).toBe(42);
  }));

  it('should handle invalid number input', fakeAsync(() => {
    const componentNumber = TestBed.createComponent(SearchBoxComponent<number>);
    componentNumber.componentRef.setInput('value', signal(0));
    componentNumber.componentRef.setInput('type', 'number');
    componentNumber.detectChanges();
    
    const consoleSpy = spyOn(console, 'log');
    
    const inputElement = document.createElement('input');
    inputElement.value = 'not a number';
    const event = { target: inputElement } as unknown as Event;
    
    componentNumber.componentInstance.onInput(event);
    tick(300);
    
    expect(consoleSpy).toHaveBeenCalledWith('Invalid number input');
  }));

  it('should debounce rapid inputs', fakeAsync(() => {
    const input1 = document.createElement('input');
    input1.value = 'a';
    const input2 = document.createElement('input');
    input2.value = 'ab';
    const input3 = document.createElement('input');
    input3.value = 'abc';
    
    component.onInput({ target: input1 } as unknown as Event);
    tick(100);
    component.onInput({ target: input2 } as unknown as Event);
    tick(100);
    component.onInput({ target: input3 } as unknown as Event);
    tick(300);
    
    // Should only process the last value after full debounce
    expect(component.value()).toBe('abc');
  }));

  it('should clean up on destroy', () => {
    const completeSpy = spyOn(component['inputSubject'], 'complete');
    
    component.ngOnDestroy();
    
    expect(completeSpy).toHaveBeenCalled();
  });

  it('should trim string values', fakeAsync(() => {
    const inputElement = document.createElement('input');
    inputElement.value = '   spaces   ';
    const event = { target: inputElement } as unknown as Event;
    
    component.onInput(event);
    tick(300);
    
    expect(component.value()).toBe('spaces');
  }));

  it('should not trim non-string values', fakeAsync(() => {
    const componentNumber = TestBed.createComponent(SearchBoxComponent<number>);
    componentNumber.componentRef.setInput('value', signal(0));
    componentNumber.componentRef.setInput('type', 'number');
    componentNumber.detectChanges();
    
    const inputElement = document.createElement('input');
    inputElement.value = '123';
    const event = { target: inputElement } as unknown as Event;
    
    componentNumber.componentInstance.onInput(event);
    tick(300);
    
    expect(componentNumber.componentInstance.value()).toBe(123);
  }));
});
