import { ComponentFixture, TestBed } from '@angular/core/testing';
import { SkeletonComponent } from './skeleton.component';

describe('SkeletonComponent', () => {
  let fixture: ComponentFixture<SkeletonComponent>;

  beforeEach(() => {
    fixture = TestBed.createComponent(SkeletonComponent);
    fixture.componentRef.setInput('label', 'standings');
  });

  const el = () => fixture.nativeElement as HTMLElement;
  const bars = () => el().querySelectorAll('.skeleton__bar');

  it('is a status saying what is loading, with its shapes hidden from screen readers', () => {
    fixture.detectChanges();
    expect(el().getAttribute('role')).toBe('status');
    expect(el().textContent!.trim()).toBe('Loading standings…');
    expect(el().querySelector('.skeleton__lines')!.getAttribute('aria-hidden')).toBe('true');
  });

  it('draws three lines by default, or as many as asked for', () => {
    fixture.detectChanges();
    expect(bars()).toHaveSize(3);
    fixture.componentRef.setInput('count', 5);
    fixture.detectChanges();
    expect(bars()).toHaveSize(5);
  });

  it('draws a table as a header and a cell per column a row', () => {
    fixture.componentRef.setInput('shape', 'table');
    fixture.componentRef.setInput('count', 4);
    fixture.componentRef.setInput('columns', 3);
    fixture.detectChanges();
    expect(el().querySelectorAll('.skeleton__bar--head')).toHaveSize(3);
    expect(bars()).toHaveSize(3 + 12);
  });

  it('leaves a table\'s header out when asked to', () => {
    fixture.componentRef.setInput('shape', 'table');
    fixture.componentRef.setInput('header', false);
    fixture.componentRef.setInput('count', 2);
    fixture.componentRef.setInput('columns', 2);
    fixture.detectChanges();
    expect(el().querySelector('.skeleton__bar--head')).toBeNull();
    expect(bars()).toHaveSize(4);
  });

  it('draws cards at the height asked for, 160px by default', () => {
    fixture.componentRef.setInput('shape', 'cards');
    fixture.componentRef.setInput('count', 2);
    fixture.detectChanges();
    const cards = el().querySelectorAll<HTMLElement>('.skeleton__card');
    expect(cards).toHaveSize(2);
    expect(cards[0].style.height).toBe('160px');
    fixture.componentRef.setInput('height', '90px');
    fixture.detectChanges();
    expect(el().querySelector<HTMLElement>('.skeleton__card')!.style.height).toBe('90px');
  });

  it('draws one block, 240px high by default', () => {
    fixture.componentRef.setInput('shape', 'block');
    fixture.detectChanges();
    expect(bars()).toHaveSize(1);
    expect(el().querySelector<HTMLElement>('.skeleton__block')!.style.height).toBe('240px');
  });

  it('draws nothing for a negative count', () => {
    fixture.componentRef.setInput('count', -1);
    fixture.detectChanges();
    expect(bars()).toHaveSize(0);
  });
});
