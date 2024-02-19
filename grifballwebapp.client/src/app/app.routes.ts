import { Routes } from '@angular/router';

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
  }
];
