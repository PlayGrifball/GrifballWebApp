namespace GrifballWebApp.Server.Extensions;

public static class DateTimeExtensions
{
    public static long ToUnix(this DateTime toUnix)
    {
        return new DateTimeOffset(toUnix, TimeSpan.FromTicks(0)).ToUnixTimeSeconds();
    }

    public static string DiscordTimeEmbed(this DateTime t)
    {
        return $"<t:{t.ToUnix()}>";
    }
}
