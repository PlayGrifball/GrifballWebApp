import { Routes } from '@angular/router';
import { isEventOrganizerGuard } from './isEventOrganizer.guard';
import { isSysAdminGuard } from './isSysAdmin.guard';

export const APP_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./home/home.component').then(c => c.HomeComponent),
    title: 'Home',
  },
  {
    path: 'theme',
    loadComponent: () => import('./theme/theme.component').then(m => m.ThemeComponent),
    title: 'Theme'
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
    path: 'season/:seasonID/team/:teamID',
    loadComponent: () => import('./season/team/team.component').then(m => m.TeamComponent),
    title: 'Team'
  },
  {
    path: 'season/:seasonID/playergrades',
    loadComponent: () => import('./season/playerGrades/playerGrades.component').then(m => m.PlayerGradesComponent),
    title: 'Player Grades'
  },
  {
    path: 'seasonmatch/:seasonMatchID',
    loadComponent: () => import('./season/seasonMatch/seasonMatch.component').then(m => m.SeasonMatchComponent),
    title: 'Season Match'
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
    path: 'seasonAvailability/:seasonID',
    loadComponent: () => import('./seasonEdit/seasonAvailability/seasonAvailability.component').then(m => m.SeasonAvailabilityComponent),
    title: 'Season Availability',
    canActivate: [isEventOrganizerGuard]
  },
  {
    path: 'infiniteclient',
    loadComponent: () => import('./infiniteClient/infiniteClient.component').then(m => m.InfiniteClientComponent),
    title: 'Infinite Client',
    canActivate: [isSysAdminGuard]
  },
  {
    path: 'usermanagement',
    loadComponent: () => import('./userManagement/userManagement.component').then(m => m.UserManagementComponent),
    title: 'User Management',
    canActivate: [isSysAdminGuard]
  },
  {
    path: 'usermanagement/createuser',
    loadComponent: () => import('./userManagement/createUser/createUser.component').then(m => m.CreateUserComponent),
    title: 'Create User',
    canActivate: [isSysAdminGuard]
  },
  {
    path: 'usermanagement/edituser/:userID',
    loadComponent: () => import('./userManagement/editUser/editUser.component').then(m => m.EditUserComponent),
    title: 'Edit User',
    canActivate: [isSysAdminGuard]
  },
  {
    path: 'usermanagement/mergeuser',
    loadComponent: () => import('./userManagement/mergeUser/mergeUser.component').then(m => m.MergeUserComponent),
    title: 'Merge User',
    canActivate: [isSysAdminGuard]
  },
  {
    path: 'usermanagement/mergeuser/:fromInput',
    loadComponent: () => import('./userManagement/mergeUser/mergeUser.component').then(m => m.MergeUserComponent),
    title: 'Merge User',
    canActivate: [isSysAdminGuard]
  },
  {
    path: 'usermanagement/mergeuser/:fromInput/:toInput',
    loadComponent: () => import('./userManagement/mergeUser/mergeUser.component').then(m => m.MergeUserComponent),
    title: 'Merge User',
    canActivate: [isSysAdminGuard]
  },
  {
    path: 'profile/:userID',
    loadComponent: () => import('./profile/profile.component').then(m => m.ProfileComponent),
    title: 'User Profile'
  },
  {
    path: 'excel',
    loadComponent: () => import('./excel/excel.component').then(m => m.ExcelComponent),
    title: 'Excel Exporter',
    canActivate: [isEventOrganizerGuard]
  },
  {
    path: 'excel/:inputSpreadsheetId/:inputSheetName',
    loadComponent: () => import('./excel/excel.component').then(m => m.ExcelComponent),
    title: 'Excel Exporter',
    canActivate: [isEventOrganizerGuard]
  },
  {
    path: 'lateLeague',
    loadComponent: () => import('./lateLeague/lateLeague.component').then(m => m.LateLeagueComponent),
    title: 'Late League',
  },
  {
    path: '**',
    loadComponent: () => import('./notFound/notFound.component').then(m => m.NotFoundComponent),
    title: 'Page Not Found'
  },
];
