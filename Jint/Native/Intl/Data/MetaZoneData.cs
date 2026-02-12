using System.Runtime.InteropServices;

namespace Jint.Native.Intl.Data;

/// <summary>
/// CLDR metazone data for timezone display name resolution.
/// Maps IANA timezone IDs to metazones, and provides en-US display names.
/// Based on CLDR metaZones.xml and timeZoneNames.json for en-US locale.
/// </summary>
internal static class MetaZoneData
{
    [StructLayout(LayoutKind.Auto)]
    internal readonly record struct MetaZoneNames(
        string LongStandard,
        string LongDaylight,
        string LongGeneric,
        string ShortStandard,
        string ShortDaylight,
        string ShortGeneric);

    /// <summary>
    /// Maps IANA timezone IDs to their current CLDR metazone name.
    /// </summary>
    internal static readonly Dictionary<string, string> IanaToMetaZone = new(StringComparer.OrdinalIgnoreCase)
    {
        // Europe_Central
        ["Europe/Andorra"] = "Europe_Central",
        ["Europe/Belgrade"] = "Europe_Central",
        ["Europe/Berlin"] = "Europe_Central",
        ["Europe/Bratislava"] = "Europe_Central",
        ["Europe/Brussels"] = "Europe_Central",
        ["Europe/Budapest"] = "Europe_Central",
        ["Europe/Busingen"] = "Europe_Central",
        ["Europe/Copenhagen"] = "Europe_Central",
        ["Europe/Gibraltar"] = "Europe_Central",
        ["Europe/Ljubljana"] = "Europe_Central",
        ["Europe/Luxembourg"] = "Europe_Central",
        ["Europe/Madrid"] = "Europe_Central",
        ["Europe/Malta"] = "Europe_Central",
        ["Europe/Monaco"] = "Europe_Central",
        ["Europe/Oslo"] = "Europe_Central",
        ["Europe/Paris"] = "Europe_Central",
        ["Europe/Podgorica"] = "Europe_Central",
        ["Europe/Prague"] = "Europe_Central",
        ["Europe/Rome"] = "Europe_Central",
        ["Europe/San_Marino"] = "Europe_Central",
        ["Europe/Sarajevo"] = "Europe_Central",
        ["Europe/Skopje"] = "Europe_Central",
        ["Europe/Stockholm"] = "Europe_Central",
        ["Europe/Tirane"] = "Europe_Central",
        ["Europe/Vaduz"] = "Europe_Central",
        ["Europe/Vatican"] = "Europe_Central",
        ["Europe/Vienna"] = "Europe_Central",
        ["Europe/Warsaw"] = "Europe_Central",
        ["Europe/Zagreb"] = "Europe_Central",
        ["Europe/Zurich"] = "Europe_Central",
        ["Africa/Ceuta"] = "Europe_Central",
        ["Africa/Tunis"] = "Europe_Central",
        ["Arctic/Longyearbyen"] = "Europe_Central",

        // Europe_Eastern
        ["Europe/Athens"] = "Europe_Eastern",
        ["Europe/Bucharest"] = "Europe_Eastern",
        ["Europe/Chisinau"] = "Europe_Eastern",
        ["Europe/Helsinki"] = "Europe_Eastern",
        ["Europe/Kyiv"] = "Europe_Eastern",
        ["Europe/Mariehamn"] = "Europe_Eastern",
        ["Europe/Nicosia"] = "Europe_Eastern",
        ["Europe/Riga"] = "Europe_Eastern",
        ["Europe/Sofia"] = "Europe_Eastern",
        ["Europe/Tallinn"] = "Europe_Eastern",
        ["Europe/Uzhgorod"] = "Europe_Eastern",
        ["Europe/Vilnius"] = "Europe_Eastern",
        ["Europe/Zaporozhye"] = "Europe_Eastern",
        ["Africa/Cairo"] = "Europe_Eastern",
        ["Asia/Nicosia"] = "Europe_Eastern",
        ["Asia/Famagusta"] = "Europe_Eastern",

        // Europe_Western
        ["Atlantic/Canary"] = "Europe_Western",
        ["Atlantic/Faroe"] = "Europe_Western",
        ["Atlantic/Madeira"] = "Europe_Western",
        ["Europe/Lisbon"] = "Europe_Western",

        // GMT
        ["Europe/London"] = "GMT",
        ["Europe/Dublin"] = "GMT",
        ["Atlantic/Reykjavik"] = "GMT",
        ["Africa/Abidjan"] = "GMT",
        ["Africa/Accra"] = "GMT",
        ["Africa/Bamako"] = "GMT",
        ["Africa/Banjul"] = "GMT",
        ["Africa/Bissau"] = "GMT",
        ["Africa/Conakry"] = "GMT",
        ["Africa/Dakar"] = "GMT",
        ["Africa/Freetown"] = "GMT",
        ["Africa/Lome"] = "GMT",
        ["Africa/Monrovia"] = "GMT",
        ["Africa/Nouakchott"] = "GMT",
        ["Africa/Ouagadougou"] = "GMT",
        ["Africa/Sao_Tome"] = "GMT",

        // America_Eastern
        ["America/New_York"] = "America_Eastern",
        ["America/Detroit"] = "America_Eastern",
        ["America/Indiana/Indianapolis"] = "America_Eastern",
        ["America/Indiana/Marengo"] = "America_Eastern",
        ["America/Indiana/Petersburg"] = "America_Eastern",
        ["America/Indiana/Tell_City"] = "America_Eastern",
        ["America/Indiana/Vevay"] = "America_Eastern",
        ["America/Indiana/Vincennes"] = "America_Eastern",
        ["America/Indiana/Winamac"] = "America_Eastern",
        ["America/Iqaluit"] = "America_Eastern",
        ["America/Kentucky/Louisville"] = "America_Eastern",
        ["America/Kentucky/Monticello"] = "America_Eastern",
        ["America/Nassau"] = "America_Eastern",
        ["America/Nipigon"] = "America_Eastern",
        ["America/Pangnirtung"] = "America_Eastern",
        ["America/Port-au-Prince"] = "America_Eastern",
        ["America/Thunder_Bay"] = "America_Eastern",
        ["America/Toronto"] = "America_Eastern",

        // America_Central
        ["America/Chicago"] = "America_Central",
        ["America/Indiana/Knox"] = "America_Central",
        ["America/Matamoros"] = "America_Central",
        ["America/Menominee"] = "America_Central",
        ["America/North_Dakota/Beulah"] = "America_Central",
        ["America/North_Dakota/Center"] = "America_Central",
        ["America/North_Dakota/New_Salem"] = "America_Central",
        ["America/Rainy_River"] = "America_Central",
        ["America/Rankin_Inlet"] = "America_Central",
        ["America/Resolute"] = "America_Central",
        ["America/Winnipeg"] = "America_Central",
        ["America/Bahia_Banderas"] = "America_Central",
        ["America/Belize"] = "America_Central",
        ["America/Costa_Rica"] = "America_Central",
        ["America/El_Salvador"] = "America_Central",
        ["America/Guatemala"] = "America_Central",
        ["America/Managua"] = "America_Central",
        ["America/Merida"] = "America_Central",
        ["America/Mexico_City"] = "America_Central",
        ["America/Monterrey"] = "America_Central",
        ["America/Regina"] = "America_Central",
        ["America/Swift_Current"] = "America_Central",
        ["America/Tegucigalpa"] = "America_Central",

        // America_Mountain
        ["America/Denver"] = "America_Mountain",
        ["America/Boise"] = "America_Mountain",
        ["America/Cambridge_Bay"] = "America_Mountain",
        ["America/Edmonton"] = "America_Mountain",
        ["America/Inuvik"] = "America_Mountain",
        ["America/Ojinaga"] = "America_Mountain",
        ["America/Chihuahua"] = "America_Mountain",
        ["America/Mazatlan"] = "America_Mountain",
        ["America/Phoenix"] = "America_Mountain",
        ["America/Creston"] = "America_Mountain",
        ["America/Dawson_Creek"] = "America_Mountain",
        ["America/Fort_Nelson"] = "America_Mountain",

        // America_Pacific
        ["America/Los_Angeles"] = "America_Pacific",
        ["America/Dawson"] = "America_Pacific",
        ["America/Tijuana"] = "America_Pacific",
        ["America/Vancouver"] = "America_Pacific",
        ["America/Whitehorse"] = "America_Pacific",

        // Alaska
        ["America/Anchorage"] = "Alaska",
        ["America/Juneau"] = "Alaska",
        ["America/Metlakatla"] = "Alaska",
        ["America/Nome"] = "Alaska",
        ["America/Sitka"] = "Alaska",
        ["America/Yakutat"] = "Alaska",

        // Hawaii_Aleutian
        ["Pacific/Honolulu"] = "Hawaii_Aleutian",
        ["America/Adak"] = "Hawaii_Aleutian",

        // Atlantic
        ["America/Halifax"] = "Atlantic",
        ["America/Glace_Bay"] = "Atlantic",
        ["America/Goose_Bay"] = "Atlantic",
        ["America/Moncton"] = "Atlantic",
        ["America/Thule"] = "Atlantic",
        ["Atlantic/Bermuda"] = "Atlantic",
        ["America/Barbados"] = "Atlantic",
        ["America/Martinique"] = "Atlantic",
        ["America/Puerto_Rico"] = "Atlantic",
        ["America/Santo_Domingo"] = "Atlantic",
        ["America/Virgin"] = "Atlantic",

        // Newfoundland
        ["America/St_Johns"] = "Newfoundland",

        // Brasilia
        ["America/Sao_Paulo"] = "Brasilia",
        ["America/Araguaina"] = "Brasilia",
        ["America/Bahia"] = "Brasilia",
        ["America/Belem"] = "Brasilia",
        ["America/Fortaleza"] = "Brasilia",
        ["America/Maceio"] = "Brasilia",
        ["America/Recife"] = "Brasilia",
        ["America/Santarem"] = "Brasilia",

        // Amazon
        ["America/Manaus"] = "Amazon",
        ["America/Boa_Vista"] = "Amazon",
        ["America/Campo_Grande"] = "Amazon",
        ["America/Cuiaba"] = "Amazon",
        ["America/Porto_Velho"] = "Amazon",

        // Argentina
        ["America/Argentina/Buenos_Aires"] = "Argentina",
        ["America/Argentina/Catamarca"] = "Argentina",
        ["America/Argentina/Cordoba"] = "Argentina",
        ["America/Argentina/Jujuy"] = "Argentina",
        ["America/Argentina/La_Rioja"] = "Argentina",
        ["America/Argentina/Mendoza"] = "Argentina",
        ["America/Argentina/Rio_Gallegos"] = "Argentina",
        ["America/Argentina/Salta"] = "Argentina",
        ["America/Argentina/San_Juan"] = "Argentina",
        ["America/Argentina/San_Luis"] = "Argentina",
        ["America/Argentina/Tucuman"] = "Argentina",
        ["America/Argentina/Ushuaia"] = "Argentina",

        // Moscow
        ["Europe/Moscow"] = "Moscow",
        ["Europe/Kirov"] = "Moscow",
        ["Europe/Simferopol"] = "Moscow",
        ["Europe/Volgograd"] = "Moscow",

        // Japan
        ["Asia/Tokyo"] = "Japan",

        // Korea
        ["Asia/Seoul"] = "Korea",

        // China
        ["Asia/Shanghai"] = "China",
        ["Asia/Macau"] = "China",

        // Taipei
        ["Asia/Taipei"] = "Taipei",

        // Hong_Kong
        ["Asia/Hong_Kong"] = "Hong_Kong",

        // India
        ["Asia/Kolkata"] = "India",
        ["Asia/Calcutta"] = "India",

        // Pakistan
        ["Asia/Karachi"] = "Pakistan",

        // Bangladesh
        ["Asia/Dhaka"] = "Bangladesh",

        // Gulf
        ["Asia/Dubai"] = "Gulf",
        ["Asia/Muscat"] = "Gulf",

        // Arabian
        ["Asia/Riyadh"] = "Arabian",
        ["Asia/Aden"] = "Arabian",
        ["Asia/Baghdad"] = "Arabian",
        ["Asia/Bahrain"] = "Arabian",
        ["Asia/Kuwait"] = "Arabian",
        ["Asia/Qatar"] = "Arabian",

        // Iran
        ["Asia/Tehran"] = "Iran",

        // Israel
        ["Asia/Jerusalem"] = "Israel",

        // Singapore
        ["Asia/Singapore"] = "Singapore",

        // Indochina
        ["Asia/Bangkok"] = "Indochina",
        ["Asia/Ho_Chi_Minh"] = "Indochina",
        ["Asia/Phnom_Penh"] = "Indochina",
        ["Asia/Vientiane"] = "Indochina",

        // Indonesia_Western
        ["Asia/Jakarta"] = "Indonesia_Western",
        ["Asia/Pontianak"] = "Indonesia_Western",

        // Indonesia_Central
        ["Asia/Makassar"] = "Indonesia_Central",

        // Indonesia_Eastern
        ["Asia/Jayapura"] = "Indonesia_Eastern",

        // Philippines
        ["Asia/Manila"] = "Philippines",

        // Malaysia
        ["Asia/Kuala_Lumpur"] = "Malaysia",

        // Australia_Eastern
        ["Australia/Sydney"] = "Australia_Eastern",
        ["Australia/Melbourne"] = "Australia_Eastern",
        ["Australia/Brisbane"] = "Australia_Eastern",
        ["Australia/Hobart"] = "Australia_Eastern",
        ["Australia/Currie"] = "Australia_Eastern",
        ["Australia/Lindeman"] = "Australia_Eastern",
        ["Antarctica/Macquarie"] = "Australia_Eastern",

        // Australia_Central
        ["Australia/Adelaide"] = "Australia_Central",
        ["Australia/Broken_Hill"] = "Australia_Central",
        ["Australia/Darwin"] = "Australia_Central",

        // Australia_Western
        ["Australia/Perth"] = "Australia_Western",

        // New_Zealand
        ["Pacific/Auckland"] = "New_Zealand",
        ["Antarctica/McMurdo"] = "New_Zealand",

        // Africa_Central
        ["Africa/Maputo"] = "Africa_Central",
        ["Africa/Blantyre"] = "Africa_Central",
        ["Africa/Bujumbura"] = "Africa_Central",
        ["Africa/Gaborone"] = "Africa_Central",
        ["Africa/Harare"] = "Africa_Central",
        ["Africa/Kigali"] = "Africa_Central",
        ["Africa/Lubumbashi"] = "Africa_Central",
        ["Africa/Lusaka"] = "Africa_Central",
        ["Africa/Windhoek"] = "Africa_Central",

        // Africa_Eastern
        ["Africa/Nairobi"] = "Africa_Eastern",
        ["Africa/Addis_Ababa"] = "Africa_Eastern",
        ["Africa/Asmera"] = "Africa_Eastern",
        ["Africa/Dar_es_Salaam"] = "Africa_Eastern",
        ["Africa/Djibouti"] = "Africa_Eastern",
        ["Africa/Kampala"] = "Africa_Eastern",
        ["Africa/Mogadishu"] = "Africa_Eastern",
        ["Indian/Antananarivo"] = "Africa_Eastern",
        ["Indian/Comoro"] = "Africa_Eastern",
        ["Indian/Mayotte"] = "Africa_Eastern",

        // Africa_Southern
        ["Africa/Johannesburg"] = "Africa_Southern",
        ["Africa/Maseru"] = "Africa_Southern",
        ["Africa/Mbabane"] = "Africa_Southern",

        // Africa_Western
        ["Africa/Lagos"] = "Africa_Western",
        ["Africa/Bangui"] = "Africa_Western",
        ["Africa/Brazzaville"] = "Africa_Western",
        ["Africa/Douala"] = "Africa_Western",
        ["Africa/Kinshasa"] = "Africa_Western",
        ["Africa/Libreville"] = "Africa_Western",
        ["Africa/Luanda"] = "Africa_Western",
        ["Africa/Malabo"] = "Africa_Western",
        ["Africa/Ndjamena"] = "Africa_Western",
        ["Africa/Niamey"] = "Africa_Western",
        ["Africa/Porto-Novo"] = "Africa_Western",

        // Novosibirsk
        ["Asia/Novosibirsk"] = "Novosibirsk",

        // Vladivostok
        ["Asia/Vladivostok"] = "Vladivostok",

        // Yakutsk
        ["Asia/Yakutsk"] = "Yakutsk",
        ["Asia/Chita"] = "Yakutsk",
        ["Asia/Khandyga"] = "Yakutsk",

        // Magadan
        ["Asia/Magadan"] = "Magadan",

        // Kamchatka
        ["Asia/Kamchatka"] = "Kamchatka",

        // Apia
        ["Pacific/Apia"] = "Apia",

        // Tonga
        ["Pacific/Tongatapu"] = "Tonga",

        // Fiji
        ["Pacific/Fiji"] = "Fiji",

        // Chatham
        ["Pacific/Chatham"] = "Chatham",

        // Gambier
        ["Pacific/Gambier"] = "Gambier",

        // Marquesas
        ["Pacific/Marquesas"] = "Marquesas",

        // Norfolk
        ["Pacific/Norfolk"] = "Norfolk",

        // Colombia
        ["America/Bogota"] = "Colombia",

        // Peru
        ["America/Lima"] = "Peru",

        // Chile
        ["America/Santiago"] = "Chile",

        // Paraguay
        ["America/Asuncion"] = "Paraguay",

        // Uruguay
        ["America/Montevideo"] = "Uruguay",

        // Bolivia
        ["America/La_Paz"] = "Bolivia",

        // Venezuela
        ["America/Caracas"] = "Venezuela",

        // Guyana
        ["America/Guyana"] = "Guyana",

        // Suriname
        ["America/Paramaribo"] = "Suriname",

        // French_Guiana
        ["America/Cayenne"] = "French_Guiana",

        // Ecuador
        ["America/Guayaquil"] = "Ecuador",

        // Galapagos
        ["Pacific/Galapagos"] = "Galapagos",

        // East_Timor
        ["Asia/Dili"] = "East_Timor",

        // Afghanistan
        ["Asia/Kabul"] = "Afghanistan",

        // Nepal
        ["Asia/Kathmandu"] = "Nepal",

        // Bhutan
        ["Asia/Thimphu"] = "Bhutan",

        // Myanmar
        ["Asia/Yangon"] = "Myanmar",
        ["Asia/Rangoon"] = "Myanmar",

        // Lanka
        ["Asia/Colombo"] = "Lanka",

        // Kazakhstan_Eastern
        ["Asia/Almaty"] = "Kazakhstan_Eastern",

        // Kazakhstan_Western
        ["Asia/Aqtau"] = "Kazakhstan_Western",
        ["Asia/Aqtobe"] = "Kazakhstan_Western",
        ["Asia/Atyrau"] = "Kazakhstan_Western",
        ["Asia/Oral"] = "Kazakhstan_Western",
        ["Asia/Qostanay"] = "Kazakhstan_Western",
        ["Asia/Qyzylorda"] = "Kazakhstan_Western",

        // Uzbekistan
        ["Asia/Tashkent"] = "Uzbekistan",
        ["Asia/Samarkand"] = "Uzbekistan",

        // Turkmenistan
        ["Asia/Ashgabat"] = "Turkmenistan",

        // Georgia
        ["Asia/Tbilisi"] = "Georgia",

        // Azerbaijan
        ["Asia/Baku"] = "Azerbaijan",

        // Armenia
        ["Asia/Yerevan"] = "Armenia",

        // Yekaterinburg
        ["Asia/Yekaterinburg"] = "Yekaterinburg",

        // Omsk
        ["Asia/Omsk"] = "Omsk",

        // Krasnoyarsk
        ["Asia/Krasnoyarsk"] = "Krasnoyarsk",

        // Irkutsk
        ["Asia/Irkutsk"] = "Irkutsk",

        // Europe_Further_Eastern
        ["Europe/Minsk"] = "Moscow",

        // Samoa (additional)
        ["Pacific/Pago_Pago"] = "Samoa",
        ["Pacific/Midway"] = "Samoa",

        // Cuba
        ["America/Havana"] = "Cuba",

        // Mexico_Pacific
        ["America/Hermosillo"] = "Mexico_Pacific",

        // Pierre_Miquelon
        ["America/Miquelon"] = "Pierre_Miquelon",

        // Greenland_Western
        ["America/Nuuk"] = "Greenland_Western",

        // Apia (Pacific/Apia mapped above under Samoa section - current metazone is Apia)

    };

    /// <summary>
    /// En-US display names for metazones.
    /// From CLDR dates/timeZoneNames for en-US locale.
    /// </summary>
    internal static readonly Dictionary<string, MetaZoneNames> EnUsNames = new(StringComparer.Ordinal)
    {
        ["Europe_Central"] = new("Central European Standard Time", "Central European Summer Time", "Central European Time", "CET", "CEST", "CET"),
        ["Europe_Eastern"] = new("Eastern European Standard Time", "Eastern European Summer Time", "Eastern European Time", "EET", "EEST", "EET"),
        ["Europe_Western"] = new("Western European Standard Time", "Western European Summer Time", "Western European Time", "WET", "WEST", "WET"),
        ["GMT"] = new("Greenwich Mean Time", "Greenwich Mean Time", "Greenwich Mean Time", "GMT", "GMT", "GMT"),
        ["America_Eastern"] = new("Eastern Standard Time", "Eastern Daylight Time", "Eastern Time", "EST", "EDT", "ET"),
        ["America_Central"] = new("Central Standard Time", "Central Daylight Time", "Central Time", "CST", "CDT", "CT"),
        ["America_Mountain"] = new("Mountain Standard Time", "Mountain Daylight Time", "Mountain Time", "MST", "MDT", "MT"),
        ["America_Pacific"] = new("Pacific Standard Time", "Pacific Daylight Time", "Pacific Time", "PST", "PDT", "PT"),
        ["Alaska"] = new("Alaska Standard Time", "Alaska Daylight Time", "Alaska Time", "AKST", "AKDT", "AKT"),
        ["Hawaii_Aleutian"] = new("Hawaii-Aleutian Standard Time", "Hawaii-Aleutian Daylight Time", "Hawaii-Aleutian Time", "HST", "HDT", "HAT"),
        ["Atlantic"] = new("Atlantic Standard Time", "Atlantic Daylight Time", "Atlantic Time", "AST", "ADT", "AT"),
        ["Newfoundland"] = new("Newfoundland Standard Time", "Newfoundland Daylight Time", "Newfoundland Time", "NST", "NDT", "NT"),
        ["Brasilia"] = new("Brasilia Standard Time", "Brasilia Summer Time", "Brasilia Time", "BRT", "BRST", "BRT"),
        ["Amazon"] = new("Amazon Standard Time", "Amazon Summer Time", "Amazon Time", "AMT", "AMST", "AMT"),
        ["Argentina"] = new("Argentina Standard Time", "Argentina Summer Time", "Argentina Time", "ART", "ARST", "ART"),
        ["Moscow"] = new("Moscow Standard Time", "Moscow Daylight Time", "Moscow Time", "MSK", "MSD", "MSK"),
        ["Japan"] = new("Japan Standard Time", "Japan Daylight Time", "Japan Time", "JST", "JDT", "JST"),
        ["Korea"] = new("Korean Standard Time", "Korean Daylight Time", "Korean Time", "KST", "KDT", "KST"),
        ["China"] = new("China Standard Time", "China Daylight Time", "China Time", "CST", "CDT", "CT"),
        ["Taipei"] = new("Taipei Standard Time", "Taipei Daylight Time", "Taipei Time", "CST", "CDT", "CT"),
        ["Hong_Kong"] = new("Hong Kong Standard Time", "Hong Kong Summer Time", "Hong Kong Time", "HKT", "HKST", "HKT"),
        ["India"] = new("India Standard Time", "India Daylight Time", "India Standard Time", "IST", "IDT", "IST"),
        ["Pakistan"] = new("Pakistan Standard Time", "Pakistan Summer Time", "Pakistan Time", "PKT", "PKST", "PKT"),
        ["Bangladesh"] = new("Bangladesh Standard Time", "Bangladesh Summer Time", "Bangladesh Time", "BST", "BSST", "BST"),
        ["Gulf"] = new("Gulf Standard Time", "Gulf Daylight Time", "Gulf Time", "GST", "GDT", "GT"),
        ["Arabian"] = new("Arabian Standard Time", "Arabian Daylight Time", "Arabian Time", "AST", "ADT", "AT"),
        ["Iran"] = new("Iran Standard Time", "Iran Daylight Time", "Iran Time", "IRST", "IRDT", "IRT"),
        ["Israel"] = new("Israel Standard Time", "Israel Daylight Time", "Israel Time", "IST", "IDT", "IT"),
        ["Singapore"] = new("Singapore Standard Time", "Singapore Daylight Time", "Singapore Time", "SGT", "SGT", "SGT"),
        ["Indochina"] = new("Indochina Time", "Indochina Time", "Indochina Time", "ICT", "ICT", "ICT"),
        ["Indonesia_Western"] = new("Western Indonesia Time", "Western Indonesia Time", "Western Indonesia Time", "WIB", "WIB", "WIB"),
        ["Indonesia_Central"] = new("Central Indonesia Time", "Central Indonesia Time", "Central Indonesia Time", "WITA", "WITA", "WITA"),
        ["Indonesia_Eastern"] = new("Eastern Indonesia Time", "Eastern Indonesia Time", "Eastern Indonesia Time", "WIT", "WIT", "WIT"),
        ["Philippines"] = new("Philippine Standard Time", "Philippine Summer Time", "Philippine Time", "PHT", "PHST", "PHT"),
        ["Malaysia"] = new("Malaysia Time", "Malaysia Time", "Malaysia Time", "MYT", "MYT", "MYT"),
        ["Australia_Eastern"] = new("Australian Eastern Standard Time", "Australian Eastern Daylight Time", "Australian Eastern Time", "AEST", "AEDT", "AET"),
        ["Australia_Central"] = new("Australian Central Standard Time", "Australian Central Daylight Time", "Australian Central Time", "ACST", "ACDT", "ACT"),
        ["Australia_Western"] = new("Australian Western Standard Time", "Australian Western Daylight Time", "Australian Western Time", "AWST", "AWDT", "AWT"),
        ["New_Zealand"] = new("New Zealand Standard Time", "New Zealand Daylight Time", "New Zealand Time", "NZST", "NZDT", "NZT"),
        ["Africa_Central"] = new("Central Africa Time", "Central Africa Time", "Central Africa Time", "CAT", "CAT", "CAT"),
        ["Africa_Eastern"] = new("East Africa Time", "East Africa Time", "East Africa Time", "EAT", "EAT", "EAT"),
        ["Africa_Southern"] = new("South Africa Standard Time", "South Africa Standard Time", "South Africa Time", "SAST", "SAST", "SAST"),
        ["Africa_Western"] = new("West Africa Standard Time", "West Africa Summer Time", "West Africa Time", "WAT", "WAST", "WAT"),
        ["Novosibirsk"] = new("Novosibirsk Standard Time", "Novosibirsk Summer Time", "Novosibirsk Time", "NOVT", "NOVST", "NOVT"),
        ["Vladivostok"] = new("Vladivostok Standard Time", "Vladivostok Summer Time", "Vladivostok Time", "VLAT", "VLAST", "VLAT"),
        ["Yakutsk"] = new("Yakutsk Standard Time", "Yakutsk Summer Time", "Yakutsk Time", "YAKT", "YAKST", "YAKT"),
        ["Magadan"] = new("Magadan Standard Time", "Magadan Summer Time", "Magadan Time", "MAGT", "MAGST", "MAGT"),
        ["Kamchatka"] = new("Kamchatka Standard Time", "Kamchatka Summer Time", "Kamchatka Time", "PETT", "PETST", "PETT"),
        ["Fiji"] = new("Fiji Standard Time", "Fiji Summer Time", "Fiji Time", "FJT", "FJST", "FJT"),
        ["Chatham"] = new("Chatham Standard Time", "Chatham Daylight Time", "Chatham Time", "CHAST", "CHADT", "CHAT"),
        ["Tonga"] = new("Tonga Standard Time", "Tonga Summer Time", "Tonga Time", "TOT", "TOST", "TOT"),
        ["Samoa"] = new("Samoa Standard Time", "Samoa Daylight Time", "Samoa Time", "SST", "SDT", "ST"),
        ["Colombia"] = new("Colombia Standard Time", "Colombia Summer Time", "Colombia Time", "COT", "COST", "COT"),
        ["Peru"] = new("Peru Standard Time", "Peru Summer Time", "Peru Time", "PET", "PEST", "PET"),
        ["Chile"] = new("Chile Standard Time", "Chile Summer Time", "Chile Time", "CLT", "CLST", "CLT"),
        ["Paraguay"] = new("Paraguay Standard Time", "Paraguay Summer Time", "Paraguay Time", "PYT", "PYST", "PYT"),
        ["Uruguay"] = new("Uruguay Standard Time", "Uruguay Summer Time", "Uruguay Time", "UYT", "UYST", "UYT"),
        ["Bolivia"] = new("Bolivia Time", "Bolivia Time", "Bolivia Time", "BOT", "BOT", "BOT"),
        ["Venezuela"] = new("Venezuela Time", "Venezuela Time", "Venezuela Time", "VET", "VET", "VET"),
        ["French_Guiana"] = new("French Guiana Time", "French Guiana Time", "French Guiana Time", "GFT", "GFT", "GFT"),
        ["Guyana"] = new("Guyana Time", "Guyana Time", "Guyana Time", "GYT", "GYT", "GYT"),
        ["Suriname"] = new("Suriname Time", "Suriname Time", "Suriname Time", "SRT", "SRT", "SRT"),
        ["Ecuador"] = new("Ecuador Time", "Ecuador Time", "Ecuador Time", "ECT", "ECT", "ECT"),
        ["Galapagos"] = new("Galapagos Time", "Galapagos Time", "Galapagos Time", "GALT", "GALT", "GALT"),
        ["Afghanistan"] = new("Afghanistan Time", "Afghanistan Time", "Afghanistan Time", "AFT", "AFT", "AFT"),
        ["Nepal"] = new("Nepal Time", "Nepal Time", "Nepal Time", "NPT", "NPT", "NPT"),
        ["Bhutan"] = new("Bhutan Time", "Bhutan Time", "Bhutan Time", "BTT", "BTT", "BTT"),
        ["Myanmar"] = new("Myanmar Time", "Myanmar Time", "Myanmar Time", "MMT", "MMT", "MMT"),
        ["Lanka"] = new("Sri Lanka Time", "Sri Lanka Time", "Sri Lanka Time", "SLT", "SLT", "SLT"),
        ["Kazakhstan_Eastern"] = new("East Kazakhstan Time", "East Kazakhstan Time", "East Kazakhstan Time", "ALMT", "ALMT", "ALMT"),
        ["Kazakhstan_Western"] = new("West Kazakhstan Time", "West Kazakhstan Time", "West Kazakhstan Time", "AQTT", "AQTT", "AQTT"),
        ["Uzbekistan"] = new("Uzbekistan Standard Time", "Uzbekistan Summer Time", "Uzbekistan Time", "UZT", "UZST", "UZT"),
        ["Turkmenistan"] = new("Turkmenistan Standard Time", "Turkmenistan Summer Time", "Turkmenistan Time", "TMT", "TMST", "TMT"),
        ["Georgia"] = new("Georgia Standard Time", "Georgia Summer Time", "Georgia Time", "GET", "GEST", "GET"),
        ["Azerbaijan"] = new("Azerbaijan Standard Time", "Azerbaijan Summer Time", "Azerbaijan Time", "AZT", "AZST", "AZT"),
        ["Armenia"] = new("Armenia Standard Time", "Armenia Summer Time", "Armenia Time", "AMT", "AMST", "AMT"),
        ["Yekaterinburg"] = new("Yekaterinburg Standard Time", "Yekaterinburg Summer Time", "Yekaterinburg Time", "YEKT", "YEKST", "YEKT"),
        ["Omsk"] = new("Omsk Standard Time", "Omsk Summer Time", "Omsk Time", "OMST", "OMSST", "OMST"),
        ["Krasnoyarsk"] = new("Krasnoyarsk Standard Time", "Krasnoyarsk Summer Time", "Krasnoyarsk Time", "KRAT", "KRAST", "KRAT"),
        ["Irkutsk"] = new("Irkutsk Standard Time", "Irkutsk Summer Time", "Irkutsk Time", "IRKT", "IRKST", "IRKT"),
        ["Norfolk"] = new("Norfolk Island Standard Time", "Norfolk Island Daylight Time", "Norfolk Island Time", "NFT", "NFDT", "NFT"),
        ["Marquesas"] = new("Marquesas Time", "Marquesas Time", "Marquesas Time", "MART", "MART", "MART"),
        ["Gambier"] = new("Gambier Time", "Gambier Time", "Gambier Time", "GAMT", "GAMT", "GAMT"),
        ["Cuba"] = new("Cuba Standard Time", "Cuba Daylight Time", "Cuba Time", "CST", "CDT", "CT"),
        ["Mexico_Pacific"] = new("Mexican Pacific Standard Time", "Mexican Pacific Daylight Time", "Mexican Pacific Time", "MST", "MDT", "MT"),
        ["Pierre_Miquelon"] = new("St. Pierre & Miquelon Standard Time", "St. Pierre & Miquelon Daylight Time", "St. Pierre & Miquelon Time", "PMST", "PMDT", "PMT"),
        ["Greenland_Western"] = new("West Greenland Standard Time", "West Greenland Summer Time", "West Greenland Time", "WGT", "WGST", "WGT"),
        ["Apia"] = new("Apia Standard Time", "Apia Daylight Time", "Apia Time", "WSST", "WSDT", "WST"),
        ["East_Timor"] = new("East Timor Time", "East Timor Time", "East Timor Time", "TLT", "TLT", "TLT"),
    };

    /// <summary>
    /// Resolves the CLDR timezone display name for a given IANA timezone ID.
    /// </summary>
    /// <param name="ianaId">The IANA timezone identifier</param>
    /// <param name="isDaylightSaving">Whether daylight saving time is currently in effect</param>
    /// <param name="longName">True for long format, false for short format</param>
    /// <param name="generic">True for generic (non-specific) format</param>
    /// <returns>The display name, or null if not found in CLDR data</returns>
    internal static string? GetDisplayName(string ianaId, bool isDaylightSaving, bool longName, bool generic)
    {
        if (!IanaToMetaZone.TryGetValue(ianaId, out var metaZone))
        {
            return null;
        }

        if (!EnUsNames.TryGetValue(metaZone, out var names))
        {
            return null;
        }

        if (generic)
        {
            return longName ? names.LongGeneric : names.ShortGeneric;
        }

        if (isDaylightSaving)
        {
            return longName ? names.LongDaylight : names.ShortDaylight;
        }

        return longName ? names.LongStandard : names.ShortStandard;
    }
}
