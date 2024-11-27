# Grifball Web App
Full stack web application for planning and organizing Grifball tournaments, pulling match stats, and providing data insights.


### Objectives
- Users of this application are primarily expected to be players, event organizers, and/or viewers.
- Event organizers should have the ability to create and plan Seasons and/or short-term tournaments.
- Players should be able to define their standard availability.
- Players should be able to sign up for events.
- Players should be able to adjust availability on a per event basis and otherwise track any deviations to standard availability
- Event organizers should be able to use availability data to plan matches
- Captains should be able to submit the MatchID (found in Waypoint link) after match for data to be pulled
- There must be a draft page
- Captains must be to use the draft page to pick players
- Viewers should be able to view seasons, teams, and players.
- Player profile page should show all teams and seasons they are participated in along with grand total stats.
- There must be a Schedule list page with dates and times
- There must be a Team Standings page with Wins and Losses
- There must be a Game History page for to display final score of each match. Each should link to a more detailed match page.
- There must be a Playoffs Bracket page


### Missing Requirements
- Need to know how to determine whether a team makes playoffs
- Need to know how teams are seeded in playoffs


Up until now stats have been compiled into excel sheets, see Winter League 2023 Stats:
https://docs.google.com/spreadsheets/d/14tRPXLkjauRV-xfQiegUD6VMlqso7_EYe68E9HY45_c
https://docs.google.com/spreadsheets/d/1xpraseVR_rQu7LCuUNWuFWf4qSHcLyJlxDN2RtMF240

### Backend Technologies
- [.NET 9](https://learn.microsoft.com/en-us/dotnet/) - [source](https://github.com/dotnet)
- [ASP.NET Core Web API](https://learn.microsoft.com/en-us/aspnet/core) - [source](https://github.com/dotnet/aspnetcore)
- [SignalR](https://learn.microsoft.com/en-us/aspnet/core/signalr/) - [source](https://github.com/dotnet/aspnetcore/tree/main/src/SignalR)
- [Entity Framework Core](https://learn.microsoft.com/en-us/ef/core/) - [source](https://github.com/dotnet/efcore)
- [Grunt API](https://github.com/NoahSurprenant/grunt) for pulling Halo Infinite stats

### Frontend Technologies
- [Angular 17](https://angular.io/) - [source](https://github.com/angular/angular)
- [Angular Material](https://material.angular.io/) - [source](https://github.com/angular/components)
- [ngx-drag-drop](https://reppners.github.io/ngx-drag-drop/) - [source](https://github.com/reppners/ngx-drag-drop) because [the standard angular dnd does not know how to flex](https://github.com/angular/components/issues/13372)
- [ng-matero extensions](https://ng-matero.github.io/extensions/) - [source](https://github.com/ng-matero/extensions) for its DateTimepicker
- [Luxon](https://moment.github.io/luxon) - [source](https://github.com/moment/luxon/) because Date + Javascript + Coordinated Lunar Time = :(
