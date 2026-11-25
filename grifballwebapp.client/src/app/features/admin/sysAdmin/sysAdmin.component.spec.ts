import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { APP_ROUTES } from '../../../app.routes';
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

  it('links only to pages the app has, guarded', () => {
    for (const tool of fixture.componentInstance.tools) {
      const route = APP_ROUTES.find(r => '/' + r.path === tool.path);
      expect(route).withContext(tool.path).toBeDefined();
      expect(route?.canActivate?.length).withContext(tool.path).toBe(1);
    }
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
