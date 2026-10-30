namespace GrifballWebApp.Server.Reschedule;

/// <summary>The match cannot be rescheduled: it already has a result (a BYE included) or does not have both teams yet.</summary>
public class RescheduleNotAllowedException(string message) : Exception(message);

/// <summary>The user may not ask for this match to be rescheduled: they are on neither team's roster and not a commissioner.</summary>
public class RescheduleForbiddenException() : Exception("Only players on either team, or a commissioner, can request a reschedule for this match.");
