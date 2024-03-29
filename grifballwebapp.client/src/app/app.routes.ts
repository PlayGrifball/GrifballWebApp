import { Routes } from '@angular/router';
import { isEventOrganizerGuard } from './isEventOrganizer.guard';

export const APP_ROUTES: Routes = [
  {
    path: '',
    //loadComponent: () => import('./home/home.component').then(c => c.HomeComponent),
    loadComponent: () => import('./top-stats/top-stats.component').then(m => m.TopStatsComponent),
    title: 'Home',
  },
  {
    path: 'topstats',
    loadComponent: () => import('./top-stats/top-stats.component').then(m => m.TopStatsComponent),
    title: 'Top Stats'
  },
  {
    path: 'login',
    loadComponent: () => import('./login/login.component').then(m => m.LoginComponent),
    title: 'Login'
  },
  {
    path: 'register',
    loadComponent: () => import('./register/register.component').then(m => m.RegisterComponent),
    title: 'Register'
  },
  {
    path: 'seasons',
    loadComponent: () => import('./seasonManager/seasonManager.component').then(m => m.SeasonManagerComponent),
    title: 'Seasons',
    canActivate: [isEventOrganizerGuard]
  },
  {
    path: 'season/:seasonID',
    loadComponent: () => import('./seasonEdit/seasonEdit.component').then(m => m.SeasonEditComponent),
    title: 'Season Edit',
    canActivate: [isEventOrganizerGuard]
  },
  {
    path: '**',
    loadComponent: () => import('./notFound/notFound.component').then(m => m.NotFoundComponent),
    title: 'Page Not Found'
  }
];
