namespace VoltrokServices.Services.Factions;

public static class FactionCatalog
{
    public const string NewOrder = "NEW_ORDER";
    public const string FreeMarket = "FREE_MARKET";
    public const string IronFront = "IRON_FRONT";
    public const string CityAlliance = "CITY_ALLIANCE";
    public const string CitizensLeague = "CITIZENS_LEAGUE";
    public const string NationalBloc = "NATIONAL_BLOC";
    public const string LaborParty = "LABOR_PARTY";

    public static readonly string[] AllCodes =
    [
        NewOrder,
        FreeMarket,
        IronFront,
        CityAlliance,
        CitizensLeague,
        NationalBloc,
        LaborParty
    ];

    public static string GetRandomCode()
    {
        return AllCodes[Random.Shared.Next(AllCodes.Length)];
    }

    public static string GetTranslationKey(string factionCode)
    {
        return $"factions.{factionCode.ToLowerInvariant()}";
    }

    public static string GetDisplayName(string factionCode, string? languageCode = null)
    {
        var normalizedLanguage = string.IsNullOrWhiteSpace(languageCode)
            ? "en"
            : languageCode.Trim().ToLowerInvariant();

        var normalizedCode = factionCode?.Trim().ToUpperInvariant() ?? string.Empty;

        return normalizedLanguage switch
        {
            "pl" => normalizedCode switch
            {
                NewOrder => "Nowy Ład",
                FreeMarket => "Wolny Rynek",
                IronFront => "Żelazny Front",
                CityAlliance => "Sojusz Miast",
                CitizensLeague => "Liga Obywateli",
                NationalBloc => "Blok Narodowy",
                LaborParty => "Partia Pracy",
                _ => normalizedCode
            },
            _ => normalizedCode switch
            {
                NewOrder => "New Order",
                FreeMarket => "Free Market",
                IronFront => "Iron Front",
                CityAlliance => "Alliance of Cities",
                CitizensLeague => "Citizens League",
                NationalBloc => "National Bloc",
                LaborParty => "Labor Party",
                _ => normalizedCode
            }
        };
    }
}
