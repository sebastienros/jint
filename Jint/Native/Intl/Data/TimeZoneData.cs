using System.Threading;

namespace Jint.Native.Intl.Data;

/// <summary>
/// Provides IANA time zone data for ECMA-402 compliance.
/// Contains canonical timezone IDs and case-insensitive lookup.
/// </summary>
internal static class TimeZoneData
{
    // Lazy initialization to avoid startup cost
    private static Dictionary<string, string>? _caseInsensitiveLookup;
    private static readonly Lock _lock = new();

    /// <summary>
    /// Canonical IANA timezone identifiers (Zone names only - primary identifiers).
    /// These are returned by Intl.supportedValuesOf("timeZone").
    /// Does NOT include Link names or UTC aliases (Etc/GMT, Etc/UTC, GMT).
    /// </summary>
    private static readonly string[] CanonicalZones =
    [
        // IANA TZDB Zone names (primary identifiers)
        "Africa/Abidjan", "Africa/Algiers", "Africa/Bissau", "Africa/Cairo",
        "Africa/Casablanca", "Africa/Ceuta", "Africa/El_Aaiun", "Africa/Johannesburg",
        "Africa/Juba", "Africa/Khartoum", "Africa/Lagos", "Africa/Maputo",
        "Africa/Monrovia", "Africa/Nairobi", "Africa/Ndjamena", "Africa/Sao_Tome",
        "Africa/Tripoli", "Africa/Tunis", "Africa/Windhoek",
        "America/Adak", "America/Anchorage", "America/Araguaina",
        "America/Argentina/Buenos_Aires", "America/Argentina/Catamarca",
        "America/Argentina/Cordoba", "America/Argentina/Jujuy",
        "America/Argentina/La_Rioja", "America/Argentina/Mendoza",
        "America/Argentina/Rio_Gallegos", "America/Argentina/Salta",
        "America/Argentina/San_Juan", "America/Argentina/San_Luis",
        "America/Argentina/Tucuman", "America/Argentina/Ushuaia",
        "America/Asuncion", "America/Bahia", "America/Bahia_Banderas",
        "America/Barbados", "America/Belem", "America/Belize", "America/Boa_Vista",
        "America/Bogota", "America/Boise", "America/Cambridge_Bay",
        "America/Campo_Grande", "America/Cancun", "America/Caracas", "America/Cayenne",
        "America/Chicago", "America/Chihuahua", "America/Ciudad_Juarez",
        "America/Costa_Rica", "America/Cuiaba", "America/Danmarkshavn",
        "America/Dawson", "America/Dawson_Creek", "America/Denver", "America/Detroit",
        "America/Edmonton", "America/Eirunepe", "America/El_Salvador",
        "America/Fort_Nelson", "America/Fortaleza", "America/Glace_Bay",
        "America/Goose_Bay", "America/Grand_Turk", "America/Guatemala",
        "America/Guayaquil", "America/Guyana", "America/Halifax", "America/Havana",
        "America/Hermosillo", "America/Indiana/Indianapolis", "America/Indiana/Knox",
        "America/Indiana/Marengo", "America/Indiana/Petersburg",
        "America/Indiana/Tell_City", "America/Indiana/Vevay",
        "America/Indiana/Vincennes", "America/Indiana/Winamac", "America/Inuvik",
        "America/Iqaluit", "America/Jamaica", "America/Juneau",
        "America/Kentucky/Louisville", "America/Kentucky/Monticello",
        "America/La_Paz", "America/Lima", "America/Los_Angeles", "America/Maceio",
        "America/Managua", "America/Manaus", "America/Martinique", "America/Matamoros",
        "America/Mazatlan", "America/Menominee", "America/Merida", "America/Metlakatla",
        "America/Mexico_City", "America/Miquelon", "America/Moncton",
        "America/Monterrey", "America/Montevideo", "America/New_York", "America/Nome",
        "America/Noronha", "America/North_Dakota/Beulah", "America/North_Dakota/Center",
        "America/North_Dakota/New_Salem", "America/Nuuk", "America/Ojinaga",
        "America/Panama", "America/Paramaribo", "America/Phoenix",
        "America/Port-au-Prince", "America/Porto_Velho", "America/Puerto_Rico",
        "America/Punta_Arenas", "America/Rankin_Inlet", "America/Recife",
        "America/Regina", "America/Resolute", "America/Rio_Branco",
        "America/Santarem", "America/Santiago", "America/Santo_Domingo",
        "America/Sao_Paulo", "America/Scoresbysund", "America/Sitka",
        "America/St_Johns", "America/Swift_Current", "America/Tegucigalpa",
        "America/Thule", "America/Tijuana", "America/Toronto", "America/Vancouver",
        "America/Whitehorse", "America/Winnipeg", "America/Yakutat",
        // Note: America/Yellowknife is a Link to America/Edmonton since IANA TZDB 2022b
        "Antarctica/Casey", "Antarctica/Davis", "Antarctica/Macquarie",
        "Antarctica/Mawson", "Antarctica/Palmer", "Antarctica/Rothera",
        "Antarctica/Troll",
        "Asia/Almaty", "Asia/Amman", "Asia/Anadyr", "Asia/Aqtau", "Asia/Aqtobe",
        "Asia/Ashgabat", "Asia/Atyrau", "Asia/Baghdad", "Asia/Baku", "Asia/Bangkok",
        "Asia/Barnaul", "Asia/Beirut", "Asia/Bishkek", "Asia/Chita",
        // Note: Asia/Choibalsan is a Link to Asia/Ulaanbaatar since IANA TZDB 2022b
        "Asia/Colombo", "Asia/Damascus", "Asia/Dhaka", "Asia/Dili", "Asia/Dubai",
        "Asia/Dushanbe", "Asia/Famagusta", "Asia/Gaza", "Asia/Hebron",
        "Asia/Ho_Chi_Minh", "Asia/Hong_Kong", "Asia/Hovd", "Asia/Irkutsk",
        "Asia/Jakarta", "Asia/Jayapura", "Asia/Jerusalem", "Asia/Kabul",
        "Asia/Kamchatka", "Asia/Karachi", "Asia/Kathmandu", "Asia/Khandyga",
        "Asia/Kolkata", "Asia/Krasnoyarsk", "Asia/Kuching", "Asia/Macau",
        "Asia/Magadan", "Asia/Makassar", "Asia/Manila", "Asia/Nicosia",
        "Asia/Novokuznetsk", "Asia/Novosibirsk", "Asia/Omsk", "Asia/Oral",
        "Asia/Pontianak", "Asia/Pyongyang", "Asia/Qatar", "Asia/Qostanay",
        "Asia/Qyzylorda", "Asia/Riyadh", "Asia/Sakhalin", "Asia/Samarkand",
        "Asia/Seoul", "Asia/Shanghai", "Asia/Singapore", "Asia/Srednekolymsk",
        "Asia/Taipei", "Asia/Tashkent", "Asia/Tbilisi", "Asia/Tehran", "Asia/Thimphu",
        "Asia/Tokyo", "Asia/Tomsk", "Asia/Ulaanbaatar", "Asia/Urumqi", "Asia/Ust-Nera",
        "Asia/Vladivostok", "Asia/Yakutsk", "Asia/Yangon", "Asia/Yekaterinburg",
        "Asia/Yerevan",
        "Atlantic/Azores", "Atlantic/Bermuda", "Atlantic/Canary",
        "Atlantic/Cape_Verde", "Atlantic/Faroe", "Atlantic/Madeira",
        "Atlantic/South_Georgia", "Atlantic/Stanley",
        "Australia/Adelaide", "Australia/Brisbane", "Australia/Broken_Hill",
        "Australia/Darwin", "Australia/Eucla", "Australia/Hobart",
        "Australia/Lindeman", "Australia/Lord_Howe", "Australia/Melbourne",
        "Australia/Perth", "Australia/Sydney",
        // These are non-canonical IDs, but not all are Links: the four *6* / *5* / *7* POSIX names
        // are legacy Zones in tzdb's backward file, shipped separately in tzdata-legacy on some systems.
        // CET, EET, EST, HST, MET, MST and WET are Links in modern IANA TZDB (canonical-tz).
        // Note: Etc/GMT and Etc/UTC are NOT canonical - they are valid identifiers
        // but canonicalize to UTC for supportedValuesOf purposes
        "Etc/GMT+1", "Etc/GMT+10", "Etc/GMT+11", "Etc/GMT+12",
        "Etc/GMT+2", "Etc/GMT+3", "Etc/GMT+4", "Etc/GMT+5", "Etc/GMT+6",
        "Etc/GMT+7", "Etc/GMT+8", "Etc/GMT+9", "Etc/GMT-1", "Etc/GMT-10",
        "Etc/GMT-11", "Etc/GMT-12", "Etc/GMT-13", "Etc/GMT-14", "Etc/GMT-2",
        "Etc/GMT-3", "Etc/GMT-4", "Etc/GMT-5", "Etc/GMT-6", "Etc/GMT-7",
        "Etc/GMT-8", "Etc/GMT-9",
        "Europe/Andorra", "Europe/Astrakhan", "Europe/Athens", "Europe/Belgrade",
        "Europe/Berlin", "Europe/Brussels", "Europe/Bucharest", "Europe/Budapest",
        "Europe/Chisinau", "Europe/Dublin", "Europe/Gibraltar", "Europe/Helsinki",
        "Europe/Istanbul", "Europe/Kaliningrad", "Europe/Kirov", "Europe/Kyiv",
        "Europe/Lisbon", "Europe/London", "Europe/Madrid", "Europe/Malta",
        "Europe/Minsk", "Europe/Moscow", "Europe/Paris", "Europe/Prague",
        "Europe/Riga", "Europe/Rome", "Europe/Samara", "Europe/Saratov",
        "Europe/Simferopol", "Europe/Sofia", "Europe/Tallinn", "Europe/Tirane",
        "Europe/Ulyanovsk", "Europe/Vienna", "Europe/Vilnius", "Europe/Volgograd",
        "Europe/Warsaw", "Europe/Zurich",
        "Indian/Chagos", "Indian/Maldives", "Indian/Mauritius",
        "Pacific/Apia", "Pacific/Auckland", "Pacific/Bougainville",
        "Pacific/Chatham", "Pacific/Easter", "Pacific/Efate", "Pacific/Fakaofo",
        "Pacific/Fiji", "Pacific/Galapagos", "Pacific/Gambier",
        "Pacific/Guadalcanal", "Pacific/Guam", "Pacific/Honolulu", "Pacific/Kanton",
        "Pacific/Kiritimati", "Pacific/Kosrae", "Pacific/Kwajalein",
        "Pacific/Marquesas", "Pacific/Nauru", "Pacific/Niue", "Pacific/Norfolk",
        "Pacific/Noumea", "Pacific/Pago_Pago", "Pacific/Palau", "Pacific/Pitcairn",
        "Pacific/Port_Moresby", "Pacific/Rarotonga", "Pacific/Tahiti",
        "Pacific/Tarawa", "Pacific/Tongatapu",
        "UTC"
    ];

    /// <summary>
    /// All valid IANA timezone identifiers (Zone names AND Link names).
    /// This list is used for validation and case-insensitive lookup.
    /// Includes Link names that are valid input but may not be returned by supportedValuesOf.
    /// </summary>
    private static readonly string[] AllTimeZones =
    [
        // IANA TZDB Zone names (primary identifiers)
        "Africa/Abidjan", "Africa/Algiers", "Africa/Bissau", "Africa/Cairo",
        "Africa/Casablanca", "Africa/Ceuta", "Africa/El_Aaiun", "Africa/Johannesburg",
        "Africa/Juba", "Africa/Khartoum", "Africa/Lagos", "Africa/Maputo",
        "Africa/Monrovia", "Africa/Nairobi", "Africa/Ndjamena", "Africa/Sao_Tome",
        "Africa/Tripoli", "Africa/Tunis", "Africa/Windhoek",
        "America/Adak", "America/Anchorage", "America/Araguaina",
        "America/Argentina/Buenos_Aires", "America/Argentina/Catamarca",
        "America/Argentina/Cordoba", "America/Argentina/Jujuy",
        "America/Argentina/La_Rioja", "America/Argentina/Mendoza",
        "America/Argentina/Rio_Gallegos", "America/Argentina/Salta",
        "America/Argentina/San_Juan", "America/Argentina/San_Luis",
        "America/Argentina/Tucuman", "America/Argentina/Ushuaia",
        "America/Asuncion", "America/Bahia", "America/Bahia_Banderas",
        "America/Barbados", "America/Belem", "America/Belize", "America/Boa_Vista",
        "America/Bogota", "America/Boise", "America/Cambridge_Bay",
        "America/Campo_Grande", "America/Cancun", "America/Caracas", "America/Cayenne",
        "America/Chicago", "America/Chihuahua", "America/Ciudad_Juarez",
        "America/Costa_Rica", "America/Cuiaba", "America/Danmarkshavn",
        "America/Dawson", "America/Dawson_Creek", "America/Denver", "America/Detroit",
        "America/Edmonton", "America/Eirunepe", "America/El_Salvador",
        "America/Fort_Nelson", "America/Fortaleza", "America/Glace_Bay",
        "America/Goose_Bay", "America/Grand_Turk", "America/Guatemala",
        "America/Guayaquil", "America/Guyana", "America/Halifax", "America/Havana",
        "America/Hermosillo", "America/Indiana/Indianapolis", "America/Indiana/Knox",
        "America/Indiana/Marengo", "America/Indiana/Petersburg",
        "America/Indiana/Tell_City", "America/Indiana/Vevay",
        "America/Indiana/Vincennes", "America/Indiana/Winamac", "America/Inuvik",
        "America/Iqaluit", "America/Jamaica", "America/Juneau",
        "America/Kentucky/Louisville", "America/Kentucky/Monticello",
        "America/La_Paz", "America/Lima", "America/Los_Angeles", "America/Maceio",
        "America/Managua", "America/Manaus", "America/Martinique", "America/Matamoros",
        "America/Mazatlan", "America/Menominee", "America/Merida", "America/Metlakatla",
        "America/Mexico_City", "America/Miquelon", "America/Moncton",
        "America/Monterrey", "America/Montevideo", "America/New_York", "America/Nome",
        "America/Noronha", "America/North_Dakota/Beulah", "America/North_Dakota/Center",
        "America/North_Dakota/New_Salem", "America/Nuuk", "America/Ojinaga",
        "America/Panama", "America/Paramaribo", "America/Phoenix",
        "America/Port-au-Prince", "America/Porto_Velho", "America/Puerto_Rico",
        "America/Punta_Arenas", "America/Rankin_Inlet", "America/Recife",
        "America/Regina", "America/Resolute", "America/Rio_Branco",
        "America/Santarem", "America/Santiago", "America/Santo_Domingo",
        "America/Sao_Paulo", "America/Scoresbysund", "America/Sitka",
        "America/St_Johns", "America/Swift_Current", "America/Tegucigalpa",
        "America/Thule", "America/Tijuana", "America/Toronto", "America/Vancouver",
        "America/Whitehorse", "America/Winnipeg", "America/Yakutat",
        "Antarctica/Casey", "Antarctica/Davis", "Antarctica/Macquarie",
        "Antarctica/Mawson", "Antarctica/Palmer", "Antarctica/Rothera",
        "Antarctica/Troll",
        "Asia/Almaty", "Asia/Amman", "Asia/Anadyr", "Asia/Aqtau", "Asia/Aqtobe",
        "Asia/Ashgabat", "Asia/Atyrau", "Asia/Baghdad", "Asia/Baku", "Asia/Bangkok",
        "Asia/Barnaul", "Asia/Beirut", "Asia/Bishkek", "Asia/Chita",
        "Asia/Colombo", "Asia/Damascus", "Asia/Dhaka", "Asia/Dili", "Asia/Dubai",
        "Asia/Dushanbe", "Asia/Famagusta", "Asia/Gaza", "Asia/Hebron",
        "Asia/Ho_Chi_Minh", "Asia/Hong_Kong", "Asia/Hovd", "Asia/Irkutsk",
        "Asia/Jakarta", "Asia/Jayapura", "Asia/Jerusalem", "Asia/Kabul",
        "Asia/Kamchatka", "Asia/Karachi", "Asia/Kathmandu", "Asia/Khandyga",
        "Asia/Kolkata", "Asia/Krasnoyarsk", "Asia/Kuching", "Asia/Macau",
        "Asia/Magadan", "Asia/Makassar", "Asia/Manila", "Asia/Nicosia",
        "Asia/Novokuznetsk", "Asia/Novosibirsk", "Asia/Omsk", "Asia/Oral",
        "Asia/Pontianak", "Asia/Pyongyang", "Asia/Qatar", "Asia/Qostanay",
        "Asia/Qyzylorda", "Asia/Riyadh", "Asia/Sakhalin", "Asia/Samarkand",
        "Asia/Seoul", "Asia/Shanghai", "Asia/Singapore", "Asia/Srednekolymsk",
        "Asia/Taipei", "Asia/Tashkent", "Asia/Tbilisi", "Asia/Tehran", "Asia/Thimphu",
        "Asia/Tokyo", "Asia/Tomsk", "Asia/Ulaanbaatar", "Asia/Urumqi", "Asia/Ust-Nera",
        "Asia/Vladivostok", "Asia/Yakutsk", "Asia/Yangon", "Asia/Yekaterinburg",
        "Asia/Yerevan",
        "Atlantic/Azores", "Atlantic/Bermuda", "Atlantic/Canary",
        "Atlantic/Cape_Verde", "Atlantic/Faroe", "Atlantic/Madeira",
        "Atlantic/South_Georgia", "Atlantic/Stanley",
        "Australia/Adelaide", "Australia/Brisbane", "Australia/Broken_Hill",
        "Australia/Darwin", "Australia/Eucla", "Australia/Hobart",
        "Australia/Lindeman", "Australia/Lord_Howe", "Australia/Melbourne",
        "Australia/Perth", "Australia/Sydney",
        // Per ECMA-402 2024 spec, Etc/GMT and Etc/UTC should be preserved (not canonicalized to UTC)
        // when returned in resolvedOptions().timeZone
        "Etc/GMT", "Etc/UTC",
        "Etc/GMT+1", "Etc/GMT+10", "Etc/GMT+11", "Etc/GMT+12",
        "Etc/GMT+2", "Etc/GMT+3", "Etc/GMT+4", "Etc/GMT+5", "Etc/GMT+6",
        "Etc/GMT+7", "Etc/GMT+8", "Etc/GMT+9", "Etc/GMT-1", "Etc/GMT-10",
        "Etc/GMT-11", "Etc/GMT-12", "Etc/GMT-13", "Etc/GMT-14", "Etc/GMT-2",
        "Etc/GMT-3", "Etc/GMT-4", "Etc/GMT-5", "Etc/GMT-6", "Etc/GMT-7",
        "Etc/GMT-8", "Etc/GMT-9",
        "Europe/Andorra", "Europe/Astrakhan", "Europe/Athens", "Europe/Belgrade",
        "Europe/Berlin", "Europe/Brussels", "Europe/Bucharest", "Europe/Budapest",
        "Europe/Chisinau", "Europe/Dublin", "Europe/Gibraltar", "Europe/Helsinki",
        "Europe/Istanbul", "Europe/Kaliningrad", "Europe/Kirov", "Europe/Kyiv",
        "Europe/Lisbon", "Europe/London", "Europe/Madrid", "Europe/Malta",
        "Europe/Minsk", "Europe/Moscow", "Europe/Paris", "Europe/Prague",
        "Europe/Riga", "Europe/Rome", "Europe/Samara", "Europe/Saratov",
        "Europe/Simferopol", "Europe/Sofia", "Europe/Tallinn", "Europe/Tirane",
        "Europe/Ulyanovsk", "Europe/Vienna", "Europe/Vilnius", "Europe/Volgograd",
        "Europe/Warsaw", "Europe/Zurich",
        "Indian/Chagos", "Indian/Maldives", "Indian/Mauritius",
        "Pacific/Apia", "Pacific/Auckland", "Pacific/Bougainville",
        "Pacific/Chatham", "Pacific/Easter", "Pacific/Efate", "Pacific/Fakaofo",
        "Pacific/Fiji", "Pacific/Galapagos", "Pacific/Gambier",
        "Pacific/Guadalcanal", "Pacific/Guam", "Pacific/Honolulu", "Pacific/Kanton",
        "Pacific/Kiritimati", "Pacific/Kosrae", "Pacific/Kwajalein",
        "Pacific/Marquesas", "Pacific/Nauru", "Pacific/Niue", "Pacific/Norfolk",
        "Pacific/Noumea", "Pacific/Pago_Pago", "Pacific/Palau", "Pacific/Pitcairn",
        "Pacific/Port_Moresby", "Pacific/Rarotonga", "Pacific/Tahiti",
        "Pacific/Tarawa", "Pacific/Tongatapu",
        "UTC",

        // Non-canonical IANA identifiers and legacy POSIX Zones (all valid identifiers).
        // CET, EET, EST, HST, MET, MST and WET are Links in canonical-tz; the four *6* / *5* / *7* names
        // are Zones in tzdb's backward file and may be shipped separately in tzdata-legacy.
        "CET", "CST6CDT", "EET", "EST", "EST5EDT", "HST",
        "MET", "MST", "MST7MDT", "PST8PDT", "WET",
        "Africa/Accra", "Africa/Addis_Ababa", "Africa/Asmara", "Africa/Asmera",
        "Africa/Bamako", "Africa/Bangui", "Africa/Banjul", "Africa/Blantyre",
        "Africa/Brazzaville", "Africa/Bujumbura", "Africa/Conakry", "Africa/Dakar",
        "Africa/Dar_es_Salaam", "Africa/Djibouti", "Africa/Douala", "Africa/Freetown",
        "Africa/Gaborone", "Africa/Harare", "Africa/Kampala", "Africa/Kigali",
        "Africa/Kinshasa", "Africa/Libreville", "Africa/Lome", "Africa/Luanda",
        "Africa/Lubumbashi", "Africa/Lusaka", "Africa/Malabo", "Africa/Maseru",
        "Africa/Mbabane", "Africa/Mogadishu", "Africa/Niamey", "Africa/Nouakchott",
        "Africa/Ouagadougou", "Africa/Porto-Novo", "Africa/Timbuktu",
        "America/Anguilla", "America/Antigua", "America/Argentina/ComodRivadavia",
        "America/Aruba", "America/Atikokan", "America/Atka", "America/Blanc-Sablon",
        "America/Buenos_Aires", "America/Catamarca", "America/Cayman",
        "America/Coral_Harbour", "America/Cordoba", "America/Creston",
        "America/Curacao", "America/Dominica", "America/Ensenada",
        "America/Fort_Wayne", "America/Godthab", "America/Grenada",
        "America/Guadeloupe", "America/Indianapolis", "America/Jujuy",
        "America/Knox_IN", "America/Kralendijk", "America/Louisville",
        "America/Lower_Princes", "America/Marigot", "America/Mendoza",
        "America/Montreal", "America/Montserrat", "America/Nassau",
        "America/Nipigon", "America/Pangnirtung", "America/Port_of_Spain",
        "America/Porto_Acre", "America/Rainy_River", "America/Rosario",
        "America/Santa_Isabel", "America/Shiprock", "America/St_Barthelemy",
        "America/St_Kitts", "America/St_Lucia", "America/St_Thomas",
        "America/St_Vincent", "America/Thunder_Bay", "America/Tortola",
        "America/Virgin", "America/Yellowknife",
        "Antarctica/DumontDUrville", "Antarctica/McMurdo", "Antarctica/South_Pole",
        "Antarctica/Syowa", "Antarctica/Vostok",
        "Arctic/Longyearbyen",
        "Asia/Aden", "Asia/Ashkhabad", "Asia/Bahrain", "Asia/Brunei",
        "Asia/Calcutta", "Asia/Choibalsan", "Asia/Chongqing", "Asia/Chungking", "Asia/Dacca",
        "Asia/Harbin", "Asia/Istanbul", "Asia/Kashgar", "Asia/Katmandu",
        "Asia/Kuala_Lumpur", "Asia/Kuwait", "Asia/Macao", "Asia/Muscat",
        "Asia/Phnom_Penh", "Asia/Rangoon", "Asia/Saigon", "Asia/Tel_Aviv",
        "Asia/Thimbu", "Asia/Ujung_Pandang", "Asia/Ulan_Bator", "Asia/Vientiane",
        "Atlantic/Faeroe", "Atlantic/Jan_Mayen", "Atlantic/Reykjavik",
        "Atlantic/St_Helena",
        "Australia/ACT", "Australia/Canberra", "Australia/Currie", "Australia/LHI",
        "Australia/NSW", "Australia/North", "Australia/Queensland",
        "Australia/South", "Australia/Tasmania", "Australia/Victoria",
        "Australia/West", "Australia/Yancowinna",
        "Brazil/Acre", "Brazil/DeNoronha", "Brazil/East", "Brazil/West",
        "Canada/Atlantic", "Canada/Central", "Canada/Eastern", "Canada/Mountain",
        "Canada/Newfoundland", "Canada/Pacific", "Canada/Saskatchewan", "Canada/Yukon",
        "Chile/Continental", "Chile/EasterIsland",
        "Cuba",
        "Egypt",
        "Eire",
        // Etc/* Link names that are valid IANA identifiers
        "Etc/GMT+0", "Etc/GMT-0", "Etc/GMT0", "Etc/Greenwich", "Etc/UCT", "Etc/Universal", "Etc/Zulu",
        "Europe/Amsterdam", "Europe/Belfast", "Europe/Bratislava", "Europe/Busingen",
        "Europe/Copenhagen", "Europe/Guernsey", "Europe/Isle_of_Man", "Europe/Jersey",
        "Europe/Kiev", "Europe/Ljubljana", "Europe/Luxembourg", "Europe/Mariehamn",
        "Europe/Monaco", "Europe/Nicosia", "Europe/Oslo", "Europe/Podgorica",
        "Europe/San_Marino", "Europe/Sarajevo", "Europe/Skopje", "Europe/Stockholm",
        "Europe/Tiraspol", "Europe/Uzhgorod", "Europe/Vaduz", "Europe/Vatican",
        "Europe/Zagreb", "Europe/Zaporozhye",
        "GB", "GB-Eire",
        // Per ECMA-402 2024 spec, GMT should be preserved (not canonicalized to UTC)
        "GMT",
        // GMT aliases that resolve to UTC (but are still valid identifiers)
        "GMT+0", "GMT-0", "GMT0", "Greenwich",
        "Hongkong",
        "Iceland",
        "Indian/Antananarivo", "Indian/Christmas", "Indian/Cocos", "Indian/Comoro",
        "Indian/Kerguelen", "Indian/Mahe", "Indian/Mayotte", "Indian/Reunion",
        "Iran", "Israel",
        "Jamaica", "Japan",
        "Kwajalein",
        "Libya",
        "Mexico/BajaNorte", "Mexico/BajaSur", "Mexico/General",
        "NZ", "NZ-CHAT", "Navajo",
        "PRC",
        "Pacific/Chuuk", "Pacific/Enderbury", "Pacific/Funafuti", "Pacific/Johnston",
        "Pacific/Majuro", "Pacific/Midway", "Pacific/Pohnpei", "Pacific/Ponape",
        "Pacific/Saipan", "Pacific/Samoa", "Pacific/Truk", "Pacific/Wake",
        "Pacific/Wallis", "Pacific/Yap",
        "Poland", "Portugal",
        "ROC", "ROK",
        "Singapore",
        "Turkey",
        // UCT is a valid IANA Link name
        "UCT",
        "US/Alaska", "US/Aleutian", "US/Arizona", "US/Central", "US/East-Indiana",
        "US/Eastern", "US/Hawaii", "US/Indiana-Starke", "US/Michigan", "US/Mountain",
        "US/Pacific", "US/Samoa",
        // Universal and Zulu are valid IANA Link names
        "Universal",
        "W-SU",
        "Zulu"
    ];

    /// <summary>
    /// The zone each Link name in <see cref="AllTimeZones"/> points to, from the tz database's <c>backward</c> and
    /// region files (tzdata 2026e), with chains of links followed to the zone. A system whose tz database lacks the
    /// backward links -- Ubuntu 26.04 without <c>tzdata-legacy</c> -- has no file for these names, so a lookup that
    /// fails under the Link name retries under its zone (issue #4474).
    /// </summary>
    private static readonly Dictionary<string, string> LinkTargets = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Africa/Accra"] = "Africa/Abidjan",
        ["Africa/Addis_Ababa"] = "Africa/Nairobi",
        ["Africa/Asmara"] = "Africa/Nairobi",
        ["Africa/Asmera"] = "Africa/Nairobi",
        ["Africa/Bamako"] = "Africa/Abidjan",
        ["Africa/Bangui"] = "Africa/Lagos",
        ["Africa/Banjul"] = "Africa/Abidjan",
        ["Africa/Blantyre"] = "Africa/Maputo",
        ["Africa/Brazzaville"] = "Africa/Lagos",
        ["Africa/Bujumbura"] = "Africa/Maputo",
        ["Africa/Conakry"] = "Africa/Abidjan",
        ["Africa/Dakar"] = "Africa/Abidjan",
        ["Africa/Dar_es_Salaam"] = "Africa/Nairobi",
        ["Africa/Djibouti"] = "Africa/Nairobi",
        ["Africa/Douala"] = "Africa/Lagos",
        ["Africa/Freetown"] = "Africa/Abidjan",
        ["Africa/Gaborone"] = "Africa/Maputo",
        ["Africa/Harare"] = "Africa/Maputo",
        ["Africa/Kampala"] = "Africa/Nairobi",
        ["Africa/Kigali"] = "Africa/Maputo",
        ["Africa/Kinshasa"] = "Africa/Lagos",
        ["Africa/Libreville"] = "Africa/Lagos",
        ["Africa/Lome"] = "Africa/Abidjan",
        ["Africa/Luanda"] = "Africa/Lagos",
        ["Africa/Lubumbashi"] = "Africa/Maputo",
        ["Africa/Lusaka"] = "Africa/Maputo",
        ["Africa/Malabo"] = "Africa/Lagos",
        ["Africa/Maseru"] = "Africa/Johannesburg",
        ["Africa/Mbabane"] = "Africa/Johannesburg",
        ["Africa/Mogadishu"] = "Africa/Nairobi",
        ["Africa/Niamey"] = "Africa/Lagos",
        ["Africa/Nouakchott"] = "Africa/Abidjan",
        ["Africa/Ouagadougou"] = "Africa/Abidjan",
        ["Africa/Porto-Novo"] = "Africa/Lagos",
        ["Africa/Timbuktu"] = "Africa/Abidjan",
        ["America/Anguilla"] = "America/Puerto_Rico",
        ["America/Antigua"] = "America/Puerto_Rico",
        ["America/Argentina/ComodRivadavia"] = "America/Argentina/Catamarca",
        ["America/Aruba"] = "America/Puerto_Rico",
        ["America/Atikokan"] = "America/Panama",
        ["America/Atka"] = "America/Adak",
        ["America/Blanc-Sablon"] = "America/Puerto_Rico",
        ["America/Buenos_Aires"] = "America/Argentina/Buenos_Aires",
        ["America/Catamarca"] = "America/Argentina/Catamarca",
        ["America/Cayman"] = "America/Panama",
        ["America/Coral_Harbour"] = "America/Panama",
        ["America/Cordoba"] = "America/Argentina/Cordoba",
        ["America/Creston"] = "America/Phoenix",
        ["America/Curacao"] = "America/Puerto_Rico",
        ["America/Dominica"] = "America/Puerto_Rico",
        ["America/Ensenada"] = "America/Tijuana",
        ["America/Fort_Wayne"] = "America/Indiana/Indianapolis",
        ["America/Godthab"] = "America/Nuuk",
        ["America/Grenada"] = "America/Puerto_Rico",
        ["America/Guadeloupe"] = "America/Puerto_Rico",
        ["America/Indianapolis"] = "America/Indiana/Indianapolis",
        ["America/Jujuy"] = "America/Argentina/Jujuy",
        ["America/Knox_IN"] = "America/Indiana/Knox",
        ["America/Kralendijk"] = "America/Puerto_Rico",
        ["America/Louisville"] = "America/Kentucky/Louisville",
        ["America/Lower_Princes"] = "America/Puerto_Rico",
        ["America/Marigot"] = "America/Puerto_Rico",
        ["America/Mendoza"] = "America/Argentina/Mendoza",
        ["America/Montreal"] = "America/Toronto",
        ["America/Montserrat"] = "America/Puerto_Rico",
        ["America/Nassau"] = "America/Toronto",
        ["America/Nipigon"] = "America/Toronto",
        ["America/Pangnirtung"] = "America/Iqaluit",
        ["America/Port_of_Spain"] = "America/Puerto_Rico",
        ["America/Porto_Acre"] = "America/Rio_Branco",
        ["America/Rainy_River"] = "America/Winnipeg",
        ["America/Rosario"] = "America/Argentina/Cordoba",
        ["America/Santa_Isabel"] = "America/Tijuana",
        ["America/Shiprock"] = "America/Denver",
        ["America/St_Barthelemy"] = "America/Puerto_Rico",
        ["America/St_Kitts"] = "America/Puerto_Rico",
        ["America/St_Lucia"] = "America/Puerto_Rico",
        ["America/St_Thomas"] = "America/Puerto_Rico",
        ["America/St_Vincent"] = "America/Puerto_Rico",
        ["America/Thunder_Bay"] = "America/Toronto",
        ["America/Tortola"] = "America/Puerto_Rico",
        ["America/Virgin"] = "America/Puerto_Rico",
        ["America/Yellowknife"] = "America/Edmonton",
        ["Antarctica/DumontDUrville"] = "Pacific/Port_Moresby",
        ["Antarctica/McMurdo"] = "Pacific/Auckland",
        ["Antarctica/South_Pole"] = "Pacific/Auckland",
        ["Antarctica/Syowa"] = "Asia/Riyadh",
        ["Arctic/Longyearbyen"] = "Europe/Berlin",
        ["Asia/Aden"] = "Asia/Riyadh",
        ["Asia/Ashkhabad"] = "Asia/Ashgabat",
        ["Asia/Bahrain"] = "Asia/Qatar",
        ["Asia/Brunei"] = "Asia/Kuching",
        ["Asia/Calcutta"] = "Asia/Kolkata",
        ["Asia/Choibalsan"] = "Asia/Ulaanbaatar",
        ["Asia/Chongqing"] = "Asia/Shanghai",
        ["Asia/Chungking"] = "Asia/Shanghai",
        ["Asia/Dacca"] = "Asia/Dhaka",
        ["Asia/Harbin"] = "Asia/Shanghai",
        ["Asia/Istanbul"] = "Europe/Istanbul",
        ["Asia/Kashgar"] = "Asia/Urumqi",
        ["Asia/Katmandu"] = "Asia/Kathmandu",
        ["Asia/Kuala_Lumpur"] = "Asia/Singapore",
        ["Asia/Kuwait"] = "Asia/Riyadh",
        ["Asia/Macao"] = "Asia/Macau",
        ["Asia/Muscat"] = "Asia/Dubai",
        ["Asia/Phnom_Penh"] = "Asia/Bangkok",
        ["Asia/Rangoon"] = "Asia/Yangon",
        ["Asia/Saigon"] = "Asia/Ho_Chi_Minh",
        ["Asia/Tel_Aviv"] = "Asia/Jerusalem",
        ["Asia/Thimbu"] = "Asia/Thimphu",
        ["Asia/Ujung_Pandang"] = "Asia/Makassar",
        ["Asia/Ulan_Bator"] = "Asia/Ulaanbaatar",
        ["Asia/Vientiane"] = "Asia/Bangkok",
        ["Atlantic/Faeroe"] = "Atlantic/Faroe",
        ["Atlantic/Jan_Mayen"] = "Europe/Berlin",
        ["Atlantic/Reykjavik"] = "Africa/Abidjan",
        ["Atlantic/St_Helena"] = "Africa/Abidjan",
        ["Australia/ACT"] = "Australia/Sydney",
        ["Australia/Canberra"] = "Australia/Sydney",
        ["Australia/Currie"] = "Australia/Hobart",
        ["Australia/LHI"] = "Australia/Lord_Howe",
        ["Australia/NSW"] = "Australia/Sydney",
        ["Australia/North"] = "Australia/Darwin",
        ["Australia/Queensland"] = "Australia/Brisbane",
        ["Australia/South"] = "Australia/Adelaide",
        ["Australia/Tasmania"] = "Australia/Hobart",
        ["Australia/Victoria"] = "Australia/Melbourne",
        ["Australia/West"] = "Australia/Perth",
        ["Australia/Yancowinna"] = "Australia/Broken_Hill",
        ["Brazil/Acre"] = "America/Rio_Branco",
        ["Brazil/DeNoronha"] = "America/Noronha",
        ["Brazil/East"] = "America/Sao_Paulo",
        ["Brazil/West"] = "America/Manaus",
        ["CET"] = "Europe/Brussels",
        ["Canada/Atlantic"] = "America/Halifax",
        ["Canada/Central"] = "America/Winnipeg",
        ["Canada/Eastern"] = "America/Toronto",
        ["Canada/Mountain"] = "America/Edmonton",
        ["Canada/Newfoundland"] = "America/St_Johns",
        ["Canada/Pacific"] = "America/Vancouver",
        ["Canada/Saskatchewan"] = "America/Regina",
        ["Canada/Yukon"] = "America/Whitehorse",
        ["Chile/Continental"] = "America/Santiago",
        ["Chile/EasterIsland"] = "Pacific/Easter",
        ["Cuba"] = "America/Havana",
        ["EET"] = "Europe/Athens",
        ["EST"] = "America/Panama",
        ["Egypt"] = "Africa/Cairo",
        ["Eire"] = "Europe/Dublin",
        ["Etc/GMT+0"] = "Etc/GMT",
        ["Etc/GMT-0"] = "Etc/GMT",
        ["Etc/GMT0"] = "Etc/GMT",
        ["Etc/Greenwich"] = "Etc/GMT",
        ["Etc/UCT"] = "Etc/UTC",
        ["Etc/Universal"] = "Etc/UTC",
        ["Etc/Zulu"] = "Etc/UTC",
        ["Europe/Amsterdam"] = "Europe/Brussels",
        ["Europe/Belfast"] = "Europe/London",
        ["Europe/Bratislava"] = "Europe/Prague",
        ["Europe/Busingen"] = "Europe/Zurich",
        ["Europe/Copenhagen"] = "Europe/Berlin",
        ["Europe/Guernsey"] = "Europe/London",
        ["Europe/Isle_of_Man"] = "Europe/London",
        ["Europe/Jersey"] = "Europe/London",
        ["Europe/Kiev"] = "Europe/Kyiv",
        ["Europe/Ljubljana"] = "Europe/Belgrade",
        ["Europe/Luxembourg"] = "Europe/Brussels",
        ["Europe/Mariehamn"] = "Europe/Helsinki",
        ["Europe/Monaco"] = "Europe/Paris",
        ["Europe/Nicosia"] = "Asia/Nicosia",
        ["Europe/Oslo"] = "Europe/Berlin",
        ["Europe/Podgorica"] = "Europe/Belgrade",
        ["Europe/San_Marino"] = "Europe/Rome",
        ["Europe/Sarajevo"] = "Europe/Belgrade",
        ["Europe/Skopje"] = "Europe/Belgrade",
        ["Europe/Stockholm"] = "Europe/Berlin",
        ["Europe/Tiraspol"] = "Europe/Chisinau",
        ["Europe/Uzhgorod"] = "Europe/Kyiv",
        ["Europe/Vaduz"] = "Europe/Zurich",
        ["Europe/Vatican"] = "Europe/Rome",
        ["Europe/Zagreb"] = "Europe/Belgrade",
        ["Europe/Zaporozhye"] = "Europe/Kyiv",
        ["GB"] = "Europe/London",
        ["GB-Eire"] = "Europe/London",
        ["GMT"] = "Etc/GMT",
        ["GMT+0"] = "Etc/GMT",
        ["GMT-0"] = "Etc/GMT",
        ["GMT0"] = "Etc/GMT",
        ["Greenwich"] = "Etc/GMT",
        ["HST"] = "Pacific/Honolulu",
        ["Hongkong"] = "Asia/Hong_Kong",
        ["Iceland"] = "Africa/Abidjan",
        ["Indian/Antananarivo"] = "Africa/Nairobi",
        ["Indian/Christmas"] = "Asia/Bangkok",
        ["Indian/Cocos"] = "Asia/Yangon",
        ["Indian/Comoro"] = "Africa/Nairobi",
        ["Indian/Kerguelen"] = "Indian/Maldives",
        ["Indian/Mahe"] = "Asia/Dubai",
        ["Indian/Mayotte"] = "Africa/Nairobi",
        ["Indian/Reunion"] = "Asia/Dubai",
        ["Iran"] = "Asia/Tehran",
        ["Israel"] = "Asia/Jerusalem",
        ["Jamaica"] = "America/Jamaica",
        ["Japan"] = "Asia/Tokyo",
        ["Kwajalein"] = "Pacific/Kwajalein",
        ["Libya"] = "Africa/Tripoli",
        ["MET"] = "Europe/Brussels",
        ["MST"] = "America/Phoenix",
        ["Mexico/BajaNorte"] = "America/Tijuana",
        ["Mexico/BajaSur"] = "America/Mazatlan",
        ["Mexico/General"] = "America/Mexico_City",
        ["NZ"] = "Pacific/Auckland",
        ["NZ-CHAT"] = "Pacific/Chatham",
        ["Navajo"] = "America/Denver",
        ["PRC"] = "Asia/Shanghai",
        ["Pacific/Chuuk"] = "Pacific/Port_Moresby",
        ["Pacific/Enderbury"] = "Pacific/Kanton",
        ["Pacific/Funafuti"] = "Pacific/Tarawa",
        ["Pacific/Johnston"] = "Pacific/Honolulu",
        ["Pacific/Majuro"] = "Pacific/Tarawa",
        ["Pacific/Midway"] = "Pacific/Pago_Pago",
        ["Pacific/Pohnpei"] = "Pacific/Guadalcanal",
        ["Pacific/Ponape"] = "Pacific/Guadalcanal",
        ["Pacific/Saipan"] = "Pacific/Guam",
        ["Pacific/Samoa"] = "Pacific/Pago_Pago",
        ["Pacific/Truk"] = "Pacific/Port_Moresby",
        ["Pacific/Wake"] = "Pacific/Tarawa",
        ["Pacific/Wallis"] = "Pacific/Tarawa",
        ["Pacific/Yap"] = "Pacific/Port_Moresby",
        ["Poland"] = "Europe/Warsaw",
        ["Portugal"] = "Europe/Lisbon",
        ["ROC"] = "Asia/Taipei",
        ["ROK"] = "Asia/Seoul",
        ["Singapore"] = "Asia/Singapore",
        ["Turkey"] = "Europe/Istanbul",
        ["UCT"] = "Etc/UTC",
        ["US/Alaska"] = "America/Anchorage",
        ["US/Aleutian"] = "America/Adak",
        ["US/Arizona"] = "America/Phoenix",
        ["US/Central"] = "America/Chicago",
        ["US/East-Indiana"] = "America/Indiana/Indianapolis",
        ["US/Eastern"] = "America/New_York",
        ["US/Hawaii"] = "Pacific/Honolulu",
        ["US/Indiana-Starke"] = "America/Indiana/Knox",
        ["US/Michigan"] = "America/Detroit",
        ["US/Mountain"] = "America/Denver",
        ["US/Pacific"] = "America/Los_Angeles",
        ["US/Samoa"] = "Pacific/Pago_Pago",
        ["Universal"] = "Etc/UTC",
        ["W-SU"] = "Europe/Moscow",
        ["WET"] = "Europe/Lisbon",
        ["Zulu"] = "Etc/UTC",
    };

    // POSIX zone IDs retained in tzdata-legacy. Their rules match these US zones from 1967 onward.
    private static readonly Dictionary<string, string> LegacyZoneFallbackTargets = new(StringComparer.OrdinalIgnoreCase)
    {
        ["CST6CDT"] = "America/Chicago",
        ["EST5EDT"] = "America/New_York",
        ["MST7MDT"] = "America/Denver",
        ["PST8PDT"] = "America/Los_Angeles",
    };

    /// <summary>
    /// Gets all supported IANA timezone identifiers (Zone names and Link names).
    /// Used for validation and case-insensitive lookup.
    /// </summary>
    public static IReadOnlyList<string> GetAllTimeZones() => AllTimeZones;

    /// <summary>
    /// Gets only canonical (primary) timezone identifiers.
    /// Used by Intl.supportedValuesOf("timeZone").
    /// Does not include Link names or UTC aliases (Etc/GMT, Etc/UTC, GMT).
    /// </summary>
    public static IReadOnlyList<string> GetCanonicalTimeZones() => CanonicalZones;

    /// <summary>
    /// Performs a case-insensitive lookup of a timezone identifier.
    /// Returns the canonical (properly-cased) form if found, null otherwise.
    /// </summary>
    public static string? FindCanonical(string timeZone)
    {
        EnsureLookupInitialized();
        return _caseInsensitiveLookup!.TryGetValue(timeZone, out var canonical)
            ? canonical
            : null;
    }

    /// <summary>
    /// Checks if a timezone identifier is supported (case-insensitive).
    /// </summary>
    public static bool IsSupported(string timeZone)
    {
        EnsureLookupInitialized();
        return _caseInsensitiveLookup!.ContainsKey(timeZone);
    }

    /// <summary>
    /// The zone a Link name points to, e.g. <c>Europe/Kiev</c> to <c>Europe/Kyiv</c>.
    /// </summary>
    internal static bool TryGetLinkTarget(string timeZone, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out string? zone)
        => LinkTargets.TryGetValue(timeZone, out zone);

    /// <summary>
    /// Gets the fallback zone for an IANA Link or an optional legacy POSIX zone name.
    /// </summary>
    internal static bool TryGetFallbackTarget(string timeZone, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out string? zone)
        => TryGetLinkTarget(timeZone, out zone) || LegacyZoneFallbackTargets.TryGetValue(timeZone, out zone);

    /// <summary>
    /// <see cref="TimeZoneInfo.FindSystemTimeZoneById"/>, retried under a Link's target or a legacy zone's
    /// approximate target when the system does not know the identifier itself. Throws what the system throws for
    /// the name as given when neither is found.
    /// </summary>
    internal static TimeZoneInfo FindSystemTimeZone(string timeZone)
    {
        if (TryFindSystemTimeZone(timeZone, out var found))
        {
            return found;
        }

        return TimeZoneInfo.FindSystemTimeZoneById(timeZone);
    }

    /// <summary>
    /// Finds a system time zone by ID or by its Link or legacy fallback target without throwing when absent.
    /// </summary>
    internal static bool TryFindSystemTimeZone(
        string timeZone,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out TimeZoneInfo? found)
    {
        if (TimeZoneInfo.TryFindSystemTimeZoneById(timeZone, out found)
            || (TryGetFallbackTarget(timeZone, out var zone) && TimeZoneInfo.TryFindSystemTimeZoneById(zone, out found)))
        {
            return true;
        }

        found = null;
        return false;
    }

    private static void EnsureLookupInitialized()
    {
        if (_caseInsensitiveLookup != null) return;

        lock (_lock)
        {
            if (_caseInsensitiveLookup != null) return;

            // Keyed case-insensitively rather than pre-lowered, so a lookup no longer has to allocate a
            // lowercased copy of the caller's identifier. IANA identifiers are ASCII, where
            // OrdinalIgnoreCase and ToLowerInvariant agree exactly.
            var lookup = new Dictionary<string, string>(AllTimeZones.Length, StringComparer.OrdinalIgnoreCase);
            foreach (var tz in AllTimeZones)
            {
                lookup[tz] = tz;
            }
            _caseInsensitiveLookup = lookup;
        }
    }
}
