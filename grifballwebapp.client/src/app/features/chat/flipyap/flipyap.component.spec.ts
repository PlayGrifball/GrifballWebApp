import { ChangeDetectionStrategy, Component, PLATFORM_ID, output } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { Router, provideRouter } from '@angular/router';
import { BLINK_EVERY_MS, BLINK_FOR_MS, FlipyapComponent, GREETED_STORAGE_KEY, GREETING_FOR_MS, TALK_BEAT_MS } from './flipyap.component';
import { FlipyapChatComponent } from './flipyapChat.component';

@Component({ selector: 'app-flipyap-chat', template: '', changeDetection: ChangeDetectionStrategy.OnPush })
class ChatStub {
  readonly closed = output<void>();
  readonly talking = output<boolean>();
}

@Component({ template: '', changeDetection: ChangeDetectionStrategy.OnPush })
class PageStub {}

describe('FlipyapComponent', () => {
  let fixture: ComponentFixture<FlipyapComponent>;

  const el = () => fixture.nativeElement as HTMLElement;
  const pet = () => el().querySelector<HTMLButtonElement>('.flipyap__pet')!;
  const chat = () => el().querySelector<HTMLElement>('.flipyap__chat');
  const stub = () => fixture.debugElement.query(By.directive(ChatStub)).componentInstance as ChatStub;

  function create(reducedMotion = false): void {
    spyOn(window, 'matchMedia').and.returnValue({ matches: reducedMotion } as MediaQueryList);
    fixture = TestBed.createComponent(FlipyapComponent);
    fixture.detectChanges();
  }

  beforeEach(() => {
    sessionStorage.removeItem(GREETED_STORAGE_KEY);
    TestBed.configureTestingModule({
      imports: [FlipyapComponent],
      providers: [provideRouter([{ path: 'chat', component: PageStub }, { path: '**', component: PageStub }]), { provide: PLATFORM_ID, useValue: 'browser' }],
    });
    TestBed.overrideComponent(FlipyapComponent, { remove: { imports: [FlipyapChatComponent] }, add: { imports: [ChatStub] } });
  });

  afterEach(() => {
    jasmine.clock().uninstall();
    sessionStorage.removeItem(GREETED_STORAGE_KEY);
  });

  it('sits in the corner as a button that opens the chat, which is not made until then', () => {
    create();

    expect(pet().getAttribute('aria-label')).toBe('Chat with Flipyap');
    expect(pet().getAttribute('aria-expanded')).toBe('false');
    expect(el().querySelectorAll('.flipyap__sprite rect').length).toBeGreaterThan(50);
    expect(chat()).toBeNull();

    pet().click();
    fixture.detectChanges();

    expect(pet().getAttribute('aria-expanded')).toBe('true');
    expect(chat()!.hidden).toBeFalse();
    expect(el().querySelector('app-flipyap-chat')).not.toBeNull();
  });

  it('hides the chat when he is clicked again, and keeps it, so an answer goes on', () => {
    create();
    pet().click();
    fixture.detectChanges();
    const made = stub();

    pet().click();
    fixture.detectChanges();

    expect(pet().getAttribute('aria-expanded')).toBe('false');
    expect(chat()!.hidden).toBeTrue();
    expect(stub()).toBe(made);
  });

  it('closes on Escape, and the focus goes back to him', () => {
    create();
    document.body.append(el());
    pet().click();
    fixture.detectChanges();

    el().dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
    fixture.detectChanges();

    expect(chat()!.hidden).toBeTrue();
    expect(document.activeElement).toBe(pet());
    el().remove();
  });

  it('does nothing on Escape with the chat closed, and closes when the chat says it is closed', () => {
    create();
    el().dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
    expect(pet().getAttribute('aria-expanded')).toBe('false');

    pet().click();
    fixture.detectChanges();
    stub().closed.emit();
    fixture.detectChanges();
    expect(pet().getAttribute('aria-expanded')).toBe('false');
  });

  it('hops when clicked', () => {
    jasmine.clock().install();
    create();

    pet().click();
    fixture.detectChanges();
    expect(pet().classList).toContain('flipyap__pet--hop');

    jasmine.clock().tick(400);
    fixture.detectChanges();
    expect(pet().classList).not.toContain('flipyap__pet--hop');
  });

  it('says hello once per tab, and not again once he is opened', () => {
    jasmine.clock().install();
    create();
    expect(el().querySelector('.flipyap__greeting')!.textContent).toContain('Ask me about the league');

    jasmine.clock().tick(GREETING_FOR_MS);
    fixture.detectChanges();
    expect(el().querySelector('.flipyap__greeting')).toBeNull();

    fixture = TestBed.createComponent(FlipyapComponent);
    fixture.detectChanges();
    expect(el().querySelector('.flipyap__greeting')).withContext('second page load of the tab').toBeNull();
  });

  it('takes his greeting back when the chat is opened', () => {
    create();
    expect(el().querySelector('.flipyap__greeting')).not.toBeNull();

    pet().click();
    fixture.detectChanges();
    expect(el().querySelector('.flipyap__greeting')).toBeNull();
  });

  it('says hello even where the tab cannot remember it', () => {
    spyOn(Storage.prototype, 'getItem').and.throwError('off');
    create();
    expect(el().querySelector('.flipyap__greeting')).not.toBeNull();
  });

  it('blinks now and then', () => {
    jasmine.clock().install();
    create();
    expect(fixture.componentInstance.frame()).toBe('idle');

    jasmine.clock().tick(BLINK_EVERY_MS);
    expect(fixture.componentInstance.frame()).toBe('blink');

    jasmine.clock().tick(BLINK_FOR_MS);
    expect(fixture.componentInstance.frame()).toBe('idle');
  });

  it('moves his mouth while the chat answers, and shuts it after', () => {
    jasmine.clock().install();
    create();
    pet().click();
    fixture.detectChanges();

    stub().talking.emit(true);
    fixture.detectChanges();
    expect(fixture.componentInstance.frame()).toBe('idle');
    jasmine.clock().tick(TALK_BEAT_MS);
    expect(fixture.componentInstance.frame()).toBe('talk');
    jasmine.clock().tick(TALK_BEAT_MS);
    expect(fixture.componentInstance.frame()).toBe('idle');
    jasmine.clock().tick(TALK_BEAT_MS);
    expect(fixture.componentInstance.frame()).toBe('talk');

    stub().talking.emit(false);
    fixture.detectChanges();
    expect(fixture.componentInstance.frame()).toBe('idle');
  });

  it('keeps still for someone who asks for less motion: no blinking, and his mouth open while he answers', () => {
    jasmine.clock().install();
    create(true);
    pet().click();
    fixture.detectChanges();

    jasmine.clock().tick(BLINK_EVERY_MS);
    expect(fixture.componentInstance.frame()).toBe('idle');

    stub().talking.emit(true);
    fixture.detectChanges();
    expect(fixture.componentInstance.frame()).toBe('talk');
    jasmine.clock().tick(TALK_BEAT_MS * 2);
    expect(fixture.componentInstance.frame()).toBe('talk');
  });

  it('is not on the chat page, which has the whole chat, and is back off it', async () => {
    create();
    const router = TestBed.inject(Router);

    await router.navigateByUrl('/chat?x=1');
    fixture.detectChanges();
    expect(el().querySelector('.flipyap')).toBeNull();

    await router.navigateByUrl('/seasons');
    fixture.detectChanges();
    expect(el().querySelector('.flipyap')).not.toBeNull();
  });
});
