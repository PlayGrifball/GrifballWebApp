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
    path: 'season/:seasonID',
    loadComponent: () => import('./season/season.component').then(m => m.SeasonComponent),
    title: 'Season'
  },
  {
    path: 'season/:seasonID/signups',
    loadComponent: () => import('./signups/signups.component').then(m => m.SignupsComponent),
    title: 'Signups'
  },
  {
    path: 'season/:seasonID/signupForm',
    loadComponent: () => import('./signupForm/signupForm.component').then(m => m.SignupFormComponent),
    title: 'Signup Form'
  },
  {
    path: 'season/:seasonID/teams',
    loadComponent: () => import('./teamBuilder/teamBuilder.component').then(m => m.TeamBuilderComponent),
    title: 'Teams'
  },
  {
    path: 'season/:seasonID/teamss',
    loadComponent: () => import('./teams/teams.component').then(m => m.TeamsComponent),
    title: 'Teams'
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
    path: 'seasonManager',
    loadComponent: () => import('./seasonManager/seasonManager.component').then(m => m.SeasonManagerComponent),
    title: 'Season Manager',
    canActivate: [isEventOrganizerGuard]
  },
  {
    path: 'seasonEdit/:seasonID',
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
