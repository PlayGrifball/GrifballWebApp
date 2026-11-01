namespace GrifballWebApp.Server.Profile;

/// <summary>
/// The regions (Other.Regions, seeded with fixed IDs in RegionConfiguration) and the one place a region written any
/// other way ("Eastern", "NA East", "EST", "EU") is read as one of them.
/// </summary>
public static class Regions
{
    public const int WestNorthAmerica = 1;
    public const int CentralNorthAmerica = 2;
    public const int EastNorthAmerica = 3;
    public const int NorthEurope = 4;
    public const int SouthEurope = 5;
    public const int Australia = 6;
    public const int Europe = 7;

    private static readonly Dictionary<string, int> Names = new(StringComparer.OrdinalIgnoreCase)
    {
        ["EAST"] = EastNorthAmerica, ["EASTERN"] = EastNorthAmerica, ["NAEAST"] = EastNorthAmerica, ["EASTNA"] = EastNorthAmerica,
        ["EASTNORTHAMERICA"] = EastNorthAmerica, ["EASTCOAST"] = EastNorthAmerica, ["EST"] = EastNorthAmerica, ["EDT"] = EastNorthAmerica,
        ["ET"] = EastNorthAmerica, ["USEAST"] = EastNorthAmerica, ["NAE"] = EastNorthAmerica, ["EAST1"] = EastNorthAmerica, ["EAST2"] = EastNorthAmerica,

        ["CENTRAL"] = CentralNorthAmerica, ["NACENTRAL"] = CentralNorthAmerica, ["CENTRALNA"] = CentralNorthAmerica,
        ["CENTRALNORTHAMERICA"] = CentralNorthAmerica, ["CST"] = CentralNorthAmerica, ["CDT"] = CentralNorthAmerica, ["CT"] = CentralNorthAmerica,
        ["USCENTRAL"] = CentralNorthAmerica, ["MIDWEST"] = CentralNorthAmerica, ["NAC"] = CentralNorthAmerica,
        ["SOUTHCENTRAL"] = CentralNorthAmerica, ["NORTHCENTRAL"] = CentralNorthAmerica,

        ["WEST"] = WestNorthAmerica, ["WESTERN"] = WestNorthAmerica, ["NAWEST"] = WestNorthAmerica, ["WESTNA"] = WestNorthAmerica,
        ["WESTNORTHAMERICA"] = WestNorthAmerica, ["WESTCOAST"] = WestNorthAmerica, ["PST"] = WestNorthAmerica, ["PDT"] = WestNorthAmerica,
        ["PT"] = WestNorthAmerica, ["PACIFIC"] = WestNorthAmerica, ["USWEST"] = WestNorthAmerica, ["NAW"] = WestNorthAmerica,
        ["MOUNTAIN"] = WestNorthAmerica, ["MST"] = WestNorthAmerica, ["MDT"] = WestNorthAmerica,

        ["EUROPE"] = Europe, ["EU"] = Europe, ["EUW"] = Europe, ["EUROPEAN"] = Europe, ["UK"] = Europe,
        ["NORTHEUROPE"] = NorthEurope, ["NORTHERNEUROPE"] = NorthEurope, ["EUNORTH"] = NorthEurope,
        ["SOUTHEUROPE"] = SouthEurope, ["SOUTHERNEUROPE"] = SouthEurope, ["EUSOUTH"] = SouthEurope,

        ["AUSTRALIA"] = Australia, ["AU"] = Australia, ["AUS"] = Australia, ["OCE"] = Australia, ["OCEANIA"] = Australia,
        ["NZ"] = Australia, ["ANZ"] = Australia,
    };

    /// <summary>The region ID a sheet or a person means, or null (blank, "Bot", or unknown).</summary>
    public static int? Normalize(string? region)
    {
        if (string.IsNullOrWhiteSpace(region))
            return null;
        var key = new string(region.Where(char.IsLetterOrDigit).ToArray());
        return Names.TryGetValue(key, out var id) ? id : null;
    }
}
