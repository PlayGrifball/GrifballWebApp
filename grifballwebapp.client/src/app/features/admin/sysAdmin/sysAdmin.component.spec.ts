import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { SysAdminComponent } from './sysAdmin.component';

describe('SysAdminComponent', () => {
  let fixture: ComponentFixture<SysAdminComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [SysAdminComponent],
      providers: [provideRouter([])],
    }).compileComponents();

    fixture = TestBed.createComponent(SysAdminComponent);
    await fixture.whenStable();
  });

  it('links to each sysadmin tool under /sysadmin', () => {
    const links = [...(fixture.nativeElement as HTMLElement).querySelectorAll<HTMLAnchorElement>('a.hud-card')];
    expect(links.map(a => [a.querySelector('.hud-card__title')?.textContent, a.getAttribute('href')])).toEqual([
      ['Historical Import', '/sysadmin/historicalimport'],
      ['Jobs', '/sysadmin/jobs'],
      ['Telemetry', '/sysadmin/telemetry'],
      ['Infinite Client', '/infiniteclient'],
      ['Database Backup', '/sysadmin/databasebackup'],
    ]);
  });
});
