import { Routes } from '@angular/router';
import { isEventOrganizerGuard } from './core/auth/isEventOrganizer.guard';
import { isSysAdminGuard } from './core/auth/isSysAdmin.guard';

export const APP_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./features/home/home.component').then(c => c.HomeComponent),
    title: 'Home',
  },
  {
    path: 'theme',
    loadComponent: () => import('./core/layout/theme/theme.component').then(m => m.ThemeComponent),
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
    loadComponent: () => import('./season/seasonTeams/seasonTeams.component').then(m => m.SeasonTeamsComponent),
    title: 'Teams'
  },
  {
    path: 'season/:seasonID/draft',
    loadComponent: () => import('./teamBuilder/teamBuilder.component').then(m => m.TeamBuilderComponent),
    title: 'Draft Board'
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
    loadComponent: () => import('./features/matches/seasonMatch/seasonMatch.component').then(m => m.SeasonMatchComponent),
    title: 'Season Match'
  },
  {
    path: 'match/:matchID',
    loadComponent: () => import('./features/matches/matchDetails/matchDetails.component').then(m => m.MatchDetailsComponent),
    title: 'Match Details'
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
    path: 'reset-password',
    loadComponent: () => import('./password-reset/password-reset.component').then(m => m.PasswordResetComponent),
    title: 'Reset Password'
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
    path: 'databasebackup',
    loadComponent: () => import('./databaseBackup/databaseBackup.component').then(m => m.DatabaseBackupComponent),
    title: 'Database Backup',
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
    path: 'usermanagement/claims',
    loadComponent: () => import('./userManagement/gamertagClaims/gamertagClaims.component').then(m => m.GamertagClaimsComponent),
    title: 'Gamertag Claims',
    canActivate: [isSysAdminGuard]
  },
  {
    path: 'usermanagement/merges',
    loadComponent: () => import('./userManagement/merges/merges.component').then(m => m.MergesComponent),
    title: 'Merges',
    canActivate: [isSysAdminGuard]
  },
  {
    path: 'usermanagement/historicalimport',
    loadComponent: () => import('./userManagement/historicalImport/historicalImport.component').then(m => m.HistoricalImportComponent),
    title: 'Historical Import',
    canActivate: [isSysAdminGuard]
  },
  {
    path: 'usermanagement/jobs',
    loadComponent: () => import('./userManagement/jobs/jobs.component').then(m => m.JobsComponent),
    title: 'Jobs',
    canActivate: [isSysAdminGuard]
  },
  {
    path: 'usermanagement/telemetry',
    loadComponent: () => import('./userManagement/telemetry/telemetry.component').then(m => m.TelemetryComponent),
    title: 'Telemetry',
    canActivate: [isSysAdminGuard]
  },
  {
    path: 'usermanagement/pastgamertags',
    loadComponent: () => import('./userManagement/pastGamertags/pastGamertags.component').then(m => m.PastGamertagsComponent),
    title: 'Past Gamertags',
    canActivate: [isSysAdminGuard]
  },
  {
    path: 'profile/:userID',
    loadComponent: () => import('./profile/profile.component').then(m => m.ProfileComponent),
    title: 'User Profile'
  },
  {
    // A team's franchise across seasons: every season's team of its name in its league
    path: 'history/team/:teamID',
    loadComponent: () => import('./teamHistory/teamHistory.component').then(m => m.TeamHistoryComponent),
    title: 'Team History'
  },
  {
    path: 'seasons',
    loadComponent: () => import('./seasons/seasons.component').then(m => m.SeasonsComponent),
    title: 'Seasons'
  },
  {
    path: 'leaderboards',
    loadComponent: () => import('./leaderboards/leaderboards.component').then(m => m.LeaderboardsComponent),
    title: 'Leaderboards'
  },
  {
    path: 'late-league',
    loadComponent: () => import('./lateLeague/lateLeague.component').then(m => m.LateLeagueComponent),
    title: 'Late League'
  },
  {
    path: 'late-league/:matchmakingSeasonID',
    loadComponent: () => import('./lateLeague/lateLeague.component').then(m => m.LateLeagueComponent),
    title: 'Late League'
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
    path: 'commissioner-dashboard',
    loadComponent: () => import('./commissioner-dashboard/commissioner-dashboard.component').then(m => m.CommissionerDashboardComponent),
    title: 'Commissioner Dashboard',
    canActivate: [isEventOrganizerGuard]
  },
  {
    path: 'reschedule-request/:seasonMatchID',
    loadComponent: () => import('./reschedule-request/reschedule-request.component').then(m => m.RescheduleRequestComponent),
    title: 'Request Reschedule'
  },
  {
    path: '**',
    loadComponent: () => import('./core/layout/notFound/notFound.component').then(m => m.NotFoundComponent),
    title: 'Page Not Found'
  },
];
