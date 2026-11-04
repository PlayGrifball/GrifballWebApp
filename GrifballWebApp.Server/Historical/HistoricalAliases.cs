namespace GrifballWebApp.Server.Historical;

/// <summary>
/// Version-controlled canonical alias mapping for historical players.
/// Maps historical alias variants, former gamertags, and typos to their canonical player handle.
/// Used during historical import resolution (PlayerResolver) and player search/spotlight.
/// </summary>
public static class HistoricalAliases
{
    private static readonly Dictionary<string, string> NormalizedToCanonical = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, List<string>> CanonicalToAliases = new(StringComparer.OrdinalIgnoreCase);

    static HistoricalAliases()
    {
        void Map(string canonical, params string[] aliases)
        {
            var cleanCanonical = HistoricalKeys.CleanGamertag(canonical);
            NormalizedToCanonical[HistoricalKeys.Normalize(cleanCanonical)] = cleanCanonical;
            if (!CanonicalToAliases.TryGetValue(cleanCanonical, out var list))
            {
                list = new List<string>();
                CanonicalToAliases[cleanCanonical] = list;
            }
            foreach (var alias in aliases)
            {
                var cleanAlias = HistoricalKeys.CleanGamertag(alias);
                NormalizedToCanonical[HistoricalKeys.Normalize(cleanAlias)] = cleanCanonical;
                if (!list.Contains(cleanAlias, StringComparer.OrdinalIgnoreCase) && !cleanAlias.Equals(cleanCanonical, StringComparison.OrdinalIgnoreCase))
                {
                    list.Add(cleanAlias);
                }
            }
        }

        // Menz / Its Rainin MenZ / MenztrualCramps / Zerosum33 / I CITI I
        Map("MenZ", "MenztrualCramps", "Its Rainin MenZ", "Itz Rainin MenZ", "Zerosum33", "I CITI I");

        // Major Silva
        Map("Silva", "Major Silva", "I Major Silva I", "I MajorSilva I", "The Silvaa");

        // S1lent
        Map("ACE B S1lentND1", "AceBS1lentND1", "Ace B S1entNd1");

        // Regresser (Oh My Godlxss - Summer 2014 Pro Next Dimenstion)
        Map("Regresser", "Oh My Godlxss", "Regresser TV", "Regresser TTV");

        // cxrnchip (DI Cell4225 - Summer 2020 Reach PC FTN)
        Map("cxrnchip", "DI Cell4225");

        // Scet Shin
        Map("Scet Shin", "oScet", "Scet", "The Scet", "The  Scet", "Scet1663");

        // OVERweightGOAT
        Map("OVERweightGOAT", "overweightgoat", "Overweighttank", "OverweightTank");

        // TommyWstSide793
        Map("TommyWstSide793", "TommyWstSide", "TommyWestSide");

        // The Piggies
        Map("The Piggies", "ThePiggies", "x352x piggies");

        // PriestXYZ
        Map("PriestXYZ", "Priest XYZ");

        // Flowers
        Map("ii flowers ii", "I Flowers I", "l Flowers l");

        // Crusader
        Map("Crusader0102", "OHW Crusader", "Ming Crusader");

        // SilentShadow
        Map("SilentShadow459", "SilentShadow451");

        // King Rick
        Map("KING RICKXL", "KING RICK XL");

        // The Exile Dragon
        Map("ID I2 IA G O N", "THEXILEDRAGON", "THEXILEDDRAGON");

        // Dime
        Map("I DIM3 I", "oDime");

        // Paragon Saber / L1ght5aber
        Map("L1ght5aber", "Paragon Saber", "Sabasauros Rex", "SabasaurosRex");

        // AJ DaJuiceMayne
        Map("AJDaJuiceMayne", "AJ DaJuiceMayne");

        // Man Canon
        Map("a man canon", "the man canon", "Man Canon", "ManCanonPls", "Mang Canon");

        // Gingy
        Map("ixGingy", "Gingy");

        // Digital Pain (capital I vs lowercase l)
        Map("DlGITAL PAIN", "DIGITAL PAIN");

        // DJBlue
        Map("DJBlue PDX", "DJBluePDX", "DJ Blue PDX");

        // Trespass
        Map("Trespass", "Trespa5s", "Tre5pass");

        // Pie
        Map("x0 pie x0", "DSR Pie");

        // Puppies
        Map("About73Puppies", "AB0UT73PUPPIES");

        // Saintz
        Map("Saintz the God", "QueOtaSaintz");

        // Freakshow
        Map("Freakshow187", "Freakshow781", "Her Freakshow");

        // GodwitIvy
        Map("GodwitIvy", "godwit1ivy", "godwit1vy");

        // CaptainSev
        Map("CaptainSev", "CaptainSev004", "CaptainSev0004");

        // iRobot
        Map("irobot9000", "iRobot 9000", "I robot9000");

        // Liquid Nitrogen
        Map("LiquidNitrogen", "LiquideNitrogen", "LiquideNitrogin");

        // MsWolverine
        Map("MsWolverine", "MsWolv3rine");

        // Stop Killing Me
        Map("ST0P KILLING ME", "ST0P K1LLING ME");

        // Wrecking Nerds
        Map("Wrecking Nerds", "Wrecking Nerdz");

        // Mystical Novice
        Map("MysticalNovice", "Mystical Novice");

        // Myst the Cold
        Map("Myst the Cold", "MystTheCold", "Myst The Cold");

        // Mungo Pungo
        Map("MuNgO PuNgO", "MungoPungo");

        // Titan Man
        Map("TITAN MAN 23", "TITAN MAN23");

        // Fanciest Pants
        Map("Fanciest Pants", "FanciestPants");

        // Mr Epic Beast
        Map("Mr Epic Beast", "Mr_Epic_Beast");

        AllAliases = CanonicalToAliases.Values.SelectMany(a => a).Where(IsKnownAlias).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>Every alias that maps to a canonical handle other than itself (not the canonical handles), cleaned.</summary>
    public static IReadOnlyList<string> AllAliases { get; }

    /// <summary>
    /// Resolves a raw or historical gamertag to its canonical form if mapped;
    /// otherwise returns the cleaned gamertag.
    /// </summary>
    public static string CanonicalGamertag(string gamertag)
    {
        if (string.IsNullOrWhiteSpace(gamertag))
            return gamertag;

        var clean = HistoricalKeys.CleanGamertag(gamertag);
        var normalized = HistoricalKeys.Normalize(clean);
        return NormalizedToCanonical.TryGetValue(normalized, out var canonical) ? canonical : clean;
    }

    /// <summary>Whether this gamertag has a known canonical mapping different from itself.</summary>
    public static bool IsKnownAlias(string gamertag)
    {
        if (string.IsNullOrWhiteSpace(gamertag))
            return false;

        var clean = HistoricalKeys.CleanGamertag(gamertag);
        var canonical = CanonicalGamertag(clean);
        return !canonical.Equals(clean, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Returns all known historical aliases for the canonical handle (or an empty list if none).</summary>
    public static IReadOnlyList<string> GetAliasesFor(string nameOrCanonical)
    {
        if (string.IsNullOrWhiteSpace(nameOrCanonical))
            return Array.Empty<string>();

        var clean = HistoricalKeys.CleanGamertag(nameOrCanonical);
        var canonical = CanonicalGamertag(clean);

        if (CanonicalToAliases.TryGetValue(canonical, out var list))
            return list;

        return Array.Empty<string>();
    }
}
