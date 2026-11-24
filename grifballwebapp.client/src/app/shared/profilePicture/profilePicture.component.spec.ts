import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ProfilePictureComponent } from './profilePicture.component';

describe('ProfilePictureComponent', () => {
  let fixture: ComponentFixture<ProfilePictureComponent>;

  const el = (): HTMLElement => fixture.nativeElement;
  const image = () => el().querySelector<HTMLImageElement>('img');
  const initial = () => el().querySelector('.profile-picture__initial')?.textContent;

  beforeEach(() => {
    fixture = TestBed.createComponent(ProfilePictureComponent);
  });

  it('shows only the initial for a user the site has no picture for', () => {
    fixture.componentRef.setInput('name', 'grif');
    fixture.detectChanges();

    expect(initial()).toBe('G');
    expect(image()).toBeNull();
  });

  it('shows a question mark when there is no name', () => {
    fixture.detectChanges();

    expect(initial()).toBe('?');
  });

  it('asks for the user\'s gamer picture, under the initial until it loads', () => {
    fixture.componentRef.setInput('userID', 12);
    fixture.componentRef.setInput('name', 'Grif');
    fixture.detectChanges();

    expect(image()?.getAttribute('src')).toBe('/api/users/12/gamer-picture');
    expect(image()?.classList).not.toContain('profile-picture__image--shown');

    image()?.dispatchEvent(new Event('load'));
    fixture.detectChanges();

    expect(image()?.classList).toContain('profile-picture__image--shown');
  });

  it('asks for an Xbox account\'s picture directly, ahead of the user', () => {
    fixture.componentRef.setInput('userID', 12);
    fixture.componentRef.setInput('xboxUserID', '2533274791234');
    fixture.componentRef.setInput('kind', 'haloEmblem');
    fixture.detectChanges();

    expect(image()?.getAttribute('src')).toBe('/api/xbox-accounts/2533274791234/halo-emblem');
  });

  it('asks for the Halo emblem when that is the kind', () => {
    fixture.componentRef.setInput('userID', 12);
    fixture.componentRef.setInput('kind', 'haloEmblem');
    fixture.detectChanges();

    expect(image()?.getAttribute('src')).toBe('/api/users/12/halo-emblem');
    expect(el().querySelector('.profile-picture--emblem')).not.toBeNull();
  });

  it('keeps the initial when the picture is not there', () => {
    fixture.componentRef.setInput('userID', 12);
    fixture.componentRef.setInput('name', 'Grif');
    fixture.detectChanges();

    image()?.dispatchEvent(new Event('error'));
    fixture.detectChanges();

    expect(image()).toBeNull();
    expect(initial()).toBe('G');
  });

  it('tries again for another user after one failed', () => {
    fixture.componentRef.setInput('userID', 12);
    fixture.detectChanges();
    image()?.dispatchEvent(new Event('error'));
    fixture.detectChanges();

    fixture.componentRef.setInput('userID', 13);
    fixture.detectChanges();

    expect(image()?.getAttribute('src')).toBe('/api/users/13/gamer-picture');
  });

  it('gives the signed-in user\'s square the accent border', () => {
    fixture.componentRef.setInput('accent', true);
    fixture.detectChanges();

    expect(el().querySelector('.hud-avatar--accent')).not.toBeNull();
  });
});
