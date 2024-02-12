namespace GrifballWebApp.Server.Extensions;

public static class StringExtensions
{
    public static string RemoveXUIDWrapper(this string s)
    {
        return s.Replace("xuid(", "").Replace(")", "");
    }

    public static string AddXUIDWrapper(this string s)
    {
        return $"xuid({s})";
    }
}
