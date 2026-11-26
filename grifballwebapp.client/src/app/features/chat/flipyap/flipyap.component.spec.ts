import { ChangeDetectionStrategy, Component, PLATFORM_ID, output } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { Router, provideRouter } from '@angular/router';
import { BLINK_EVERY_MS, BLINK_FOR_MS, CORNER_MARGIN_PX, DRAG_THRESHOLD_PX, FlipyapComponent, GREETED_STORAGE_KEY, GREETING_FOR_MS, POSITION_STORAGE_KEY, TALK_BEAT_MS } from './flipyap.component';
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
  /** The window without its scrollbars: what the fixed box is placed in. */
  const view = () => document.documentElement;
  const box = () => el().querySelector<HTMLElement>('.flipyap')!;
  const pet = () => el().querySelector<HTMLButtonElement>('.flipyap__pet')!;
  const chat = () => el().querySelector<HTMLElement>('.flipyap__chat');
  const stub = () => fixture.debugElement.query(By.directive(ChatStub)).componentInstance as ChatStub;

  function create(reducedMotion = false): void {
    spyOn(window, 'matchMedia').and.returnValue({ matches: reducedMotion } as MediaQueryList);
    fixture = TestBed.createComponent(FlipyapComponent);
    fixture.detectChanges();
  }

  function pointer(type: string, x: number, y: number, init: PointerEventInit = {}): void {
    pet().dispatchEvent(new PointerEvent(type, { pointerId: 1, clientX: x, clientY: y, button: 0, bubbles: true, ...init }));
    fixture.detectChanges();
  }

  /** Picks him up where he is, moves him by (dx, dy) and puts him down. */
  function dragBy(dx: number, dy: number): void {
    const rect = pet().getBoundingClientRect();
    const x = rect.left + rect.width / 2;
    const y = rect.top + rect.height / 2;
    pointer('pointerdown', x, y);
    pointer('pointermove', x + dx, y + dy);
    pointer('pointerup', x + dx, y + dy);
  }

  beforeEach(() => {
    sessionStorage.removeItem(GREETED_STORAGE_KEY);
    localStorage.removeItem(POSITION_STORAGE_KEY);
    spyOn(HTMLElement.prototype, 'setPointerCapture');
    TestBed.configureTestingModule({
      imports: [FlipyapComponent],
      providers: [provideRouter([{ path: 'chat', component: PageStub }, { path: '**', component: PageStub }]), { provide: PLATFORM_ID, useValue: 'browser' }],
    });
    TestBed.overrideComponent(FlipyapComponent, { remove: { imports: [FlipyapChatComponent] }, add: { imports: [ChatStub] } });
  });

  afterEach(() => {
    jasmine.clock().uninstall();
    sessionStorage.removeItem(GREETED_STORAGE_KEY);
    localStorage.removeItem(POSITION_STORAGE_KEY);
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
    expect(el().querySelector('.flipyap__greeting')!.hasAttribute('role')).withContext('a status role would answer the page\'s own status lookups').toBeFalse();

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

  describe('picked up and dragged', () => {
    const corner = () => ({ right: view().clientWidth - box().getBoundingClientRect().right, bottom: view().clientHeight - box().getBoundingClientRect().bottom });

    it('starts in the bottom right corner', () => {
      create();
      expect(corner()).toEqual({ right: CORNER_MARGIN_PX, bottom: CORNER_MARGIN_PX });
    });

    it('is picked up, follows the pointer, and stays where he is put down', () => {
      create();
      const rect = pet().getBoundingClientRect();
      const x = rect.left + 10;
      const y = rect.top + 10;

      pointer('pointerdown', x, y);
      pointer('pointermove', x - 120, y - 80);
      expect(fixture.componentInstance.held()).toBeTrue();
      expect(fixture.componentInstance.frame()).toBe('held');
      expect(pet().classList).toContain('flipyap__pet--held');
      expect(corner()).toEqual({ right: CORNER_MARGIN_PX + 120, bottom: CORNER_MARGIN_PX + 80 });

      pointer('pointerup', x - 120, y - 80);
      expect(fixture.componentInstance.held()).toBeFalse();
      expect(fixture.componentInstance.frame()).toBe('idle');
      expect(corner()).toEqual({ right: CORNER_MARGIN_PX + 120, bottom: CORNER_MARGIN_PX + 80 });
      expect(JSON.parse(localStorage.getItem(POSITION_STORAGE_KEY)!)).toEqual({ x: -120, y: -80 });
    });

    it('does not open the chat when a drag ends, and does on the next click', async () => {
      create();
      dragBy(-50, -50);
      pet().click();
      fixture.detectChanges();
      expect(pet().getAttribute('aria-expanded')).toBe('false');

      await new Promise(resolve => setTimeout(resolve));
      pet().click();
      fixture.detectChanges();
      expect(pet().getAttribute('aria-expanded')).toBe('true');
    });

    it('is a click, not a drag, when the pointer hardly moves', () => {
      create();
      const rect = pet().getBoundingClientRect();
      pointer('pointerdown', rect.left, rect.top);
      pointer('pointermove', rect.left + DRAG_THRESHOLD_PX - 2, rect.top);
      expect(fixture.componentInstance.held()).toBeFalse();
      pointer('pointerup', rect.left, rect.top);
      pet().click();
      fixture.detectChanges();

      expect(corner()).toEqual({ right: CORNER_MARGIN_PX, bottom: CORNER_MARGIN_PX });
      expect(localStorage.getItem(POSITION_STORAGE_KEY)).toBeNull();
      expect(pet().getAttribute('aria-expanded')).toBe('true');
    });

    it('ignores other buttons, other pointers, and moves with nothing held', () => {
      create();
      const rect = pet().getBoundingClientRect();
      pointer('pointermove', rect.left - 100, rect.top);
      pointer('pointerdown', rect.left, rect.top, { button: 2 });
      pointer('pointermove', rect.left - 100, rect.top);
      pointer('pointerup', rect.left - 100, rect.top);

      pointer('pointerdown', rect.left, rect.top);
      pointer('pointermove', rect.left - 100, rect.top, { pointerId: 2 });
      pointer('pointerup', rect.left - 100, rect.top, { pointerId: 2 });
      expect(fixture.componentInstance.held()).toBeFalse();
      expect(corner()).toEqual({ right: CORNER_MARGIN_PX, bottom: CORNER_MARGIN_PX });
    });

    it('stays inside the window', () => {
      create();
      dragBy(-100000, -100000);
      let rect = box().getBoundingClientRect();
      expect([rect.left, rect.top]).toEqual([0, 0]);

      dragBy(100000, 100000);
      rect = box().getBoundingClientRect();
      expect([rect.right, rect.bottom]).toEqual([view().clientWidth, view().clientHeight]);
    });

    it('opens the chat below and beside him in the top left, and above and beside him in the bottom right', () => {
      create();
      expect(el().querySelector('.flipyap--below, .flipyap--start')).toBeNull();

      dragBy(-100000, -100000);
      expect(el().querySelector('.flipyap--below.flipyap--start')).not.toBeNull();

      dragBy(100000, 100000);
      expect(el().querySelector('.flipyap--below, .flipyap--start')).toBeNull();
    });

    it('puts him where he was last time, and in the corner when what is kept is no position', () => {
      localStorage.setItem(POSITION_STORAGE_KEY, JSON.stringify({ x: -30, y: -20 }));
      create();
      expect(corner()).toEqual({ right: CORNER_MARGIN_PX + 30, bottom: CORNER_MARGIN_PX + 20 });
      fixture.destroy();

      for (const kept of ['{"x":"a","y":1}', 'null', 'nonsense', '{}']) {
        localStorage.setItem(POSITION_STORAGE_KEY, kept);
        fixture = TestBed.createComponent(FlipyapComponent);
        fixture.detectChanges();
        expect(corner()).withContext(kept).toEqual({ right: CORNER_MARGIN_PX, bottom: CORNER_MARGIN_PX });
        fixture.destroy();
      }
    });

    it('is pulled back inside when the window gets smaller than where he stands', () => {
      create();
      fixture.componentInstance.offset.set({ x: -100000, y: 0 });
      fixture.detectChanges();
      window.dispatchEvent(new Event('resize'));
      fixture.detectChanges();

      expect(box().getBoundingClientRect().left).toBe(0);
    });

    it('stays where he is when the position cannot be kept', () => {
      spyOn(Storage.prototype, 'setItem').and.throwError('off');
      create();
      dragBy(-50, -50);
      expect(corner()).toEqual({ right: CORNER_MARGIN_PX + 50, bottom: CORNER_MARGIN_PX + 50 });
    });
  });
});
