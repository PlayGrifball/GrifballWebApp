/**
 * This file is used to ensure all source files are included in code coverage,
 * even if they don't have their own test files.
 * 
 * By importing all source files, Karma's code coverage tool will instrument them
 * and include them in the coverage report, showing 0% coverage for untested files
 * rather than excluding them entirely.
 */

// Core services
import './account.service';
import './api/apiClient.service';
import './api/signalR.service';
import './availability.service';
import './theme.service';
import './theming.service';

// Components
import './app.component';
import './commissioner-dashboard/commissioner-dashboard.component';
import './commissioner-dashboard/process-reschedule-dialog/process-reschedule-dialog.component';
import './excel/excel.component';
import './excel/excelForm/excelForm.component';
import './home/home.component';
import './infiniteClient/infiniteClient.component';
import './lateLeague/lateLeague.component';
import './login/login.component';
import './notFound/notFound.component';
import './profile/profile.component';
import './register/register.component';
import './reschedule-request/reschedule-request.component';
import './season/playerGrades/playerGrades.component';
import './season/playoffBracket/createBracketDialog/createBracketDialog.component';
import './season/playoffBracket/playoffBracket.component';
import './season/playoffBracket/seedOrderingDialog/seedOrderingDialog.component';
import './season/scheduleList/createRegularMatchesDialog/createRegularMatchesDialog.component';
import './season/scheduleList/scheduleList.component';
import './season/season.component';
import './season/seasonMatch/seasonMatch.component';
import './season/team/team.component';
import './season/teamStandings/teamStandings.component';
import './seasonEdit/seasonAvailability/seasonAvailability.component';
import './seasonEdit/seasonEdit.component';
import './seasonManager/seasonManager.component';
import './shared/searchBox/searchBox.component';
import './shared/table/table.component';
import './signupForm/availabilityTable/availabilityTable.component';
import './signupForm/signupForm.component';
import './signups/signups.component';
import './teamBuilder/teamBuilder.component';
import './theme/palette/palette.component';
import './theme/palette/tile/tile.component';
import './theme/theme.component';
import './toolbar/toolbar.component';
import './top-stats/top-stats.component';
import './userManagement/createUser/createUser.component';
import './userManagement/editUser/editUser.component';
import './userManagement/mergeUser/mergeUser.component';
import './userManagement/mergeUser/userDetail/userDetail.component';
import './userManagement/userManagement.component';

// Guards
import './isEventOrganizer.guard';
import './isSysAdmin.guard';

// Interceptors
import './auth.interceptor';

// Routes
import './app.routes';

// Validation
import './validation/errorMessage.component';
import './validation/validationService';
import './validation/directives/matchFieldsValidator.directive';
import './validation/directives/regexMatchValidValidatorDirective.directive';

// DTOs and Models
import './accessTokenResponse';
import './api/dtos/AddPlayerToTeamRequestDto';
import './api/dtos/MovePlayerToTeamRequestDto';
import './api/dtos/RemovePlayerFromTeamRequestDto';
import './api/dtos/availabilityOption';
import './api/dtos/captainDto';
import './api/dtos/captainPlacementDto';
import './api/dtos/killsDto';
import './api/dtos/loginDto';
import './api/dtos/playerDto';
import './api/dtos/registerDto';
import './api/dtos/seasonDto';
import './api/dtos/signupRequestDto';
import './api/dtos/signupResponseDto';
import './api/dtos/teamResponseDto';
import './register/registerFormModel';
import './season/playerGrades/gradesDto';
import './season/scheduleList/scheduledMatchDto';
import './season/scheduleList/timeDto';
import './season/scheduleList/unscheduledMatchDto';
import './season/scheduleList/updateMatchTimeDto';
import './season/scheduleList/weekDto';
import './season/seasonMatch/possibleMatchDto';
import './season/seasonMatch/seasonMatchPageDto';
import './season/teamStandings/teamStandingDto';
import './shared/getPaginationResource';
import './shared/paginationResult';
import './sidebarDto';
import './teamBuilder/personStatus';
import './teamBuilder/teamModel';
import './theme/paletteTypes';
import './userManagement/createUser/createUserDto';
import './userManagement/userResponseDto';
import './validation/validators/matchFieldsValidator';
import './validation/validators/regexMatchValidator';

describe('Coverage Import', () => {
  it('should import all source files for coverage', () => {
    // This test doesn't need to do anything - it just needs to exist
    // so that all the imports above are processed by the test runner
    expect(true).toBe(true);
  });
});
