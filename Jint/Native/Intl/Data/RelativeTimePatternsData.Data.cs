namespace Jint.Native.Intl.Data;

internal static partial class RelativeTimePatternsData
{
    private static readonly Dictionary<string, LocaleRelativeTimeData> _data = new(4, StringComparer.OrdinalIgnoreCase)
    {
        ["en"] = new LocaleRelativeTimeData
        {
            Patterns = new(24, StringComparer.Ordinal)
            {
                ["second_long"] = new UnitStylePatterns
                {
                    Patterns = new(4, StringComparer.Ordinal)
                    {
                        ["future_one"] = "in {0} second",
                        ["future_other"] = "in {0} seconds",
                        ["past_one"] = "{0} second ago",
                        ["past_other"] = "{0} seconds ago",
                    }
                },
                ["second_short"] = new UnitStylePatterns
                {
                    Patterns = new(4, StringComparer.Ordinal)
                    {
                        ["future_one"] = "in {0} sec.",
                        ["future_other"] = "in {0} sec.",
                        ["past_one"] = "{0} sec. ago",
                        ["past_other"] = "{0} sec. ago",
                    }
                },
                ["second_narrow"] = new UnitStylePatterns
                {
                    Patterns = new(4, StringComparer.Ordinal)
                    {
                        ["future_one"] = "in {0}s",
                        ["future_other"] = "in {0}s",
                        ["past_one"] = "{0}s ago",
                        ["past_other"] = "{0}s ago",
                    }
                },
                ["minute_long"] = new UnitStylePatterns
                {
                    Patterns = new(4, StringComparer.Ordinal)
                    {
                        ["future_one"] = "in {0} minute",
                        ["future_other"] = "in {0} minutes",
                        ["past_one"] = "{0} minute ago",
                        ["past_other"] = "{0} minutes ago",
                    }
                },
                ["minute_short"] = new UnitStylePatterns
                {
                    Patterns = new(4, StringComparer.Ordinal)
                    {
                        ["future_one"] = "in {0} min.",
                        ["future_other"] = "in {0} min.",
                        ["past_one"] = "{0} min. ago",
                        ["past_other"] = "{0} min. ago",
                    }
                },
                ["minute_narrow"] = new UnitStylePatterns
                {
                    Patterns = new(4, StringComparer.Ordinal)
                    {
                        ["future_one"] = "in {0}m",
                        ["future_other"] = "in {0}m",
                        ["past_one"] = "{0}m ago",
                        ["past_other"] = "{0}m ago",
                    }
                },
                ["hour_long"] = new UnitStylePatterns
                {
                    Patterns = new(4, StringComparer.Ordinal)
                    {
                        ["future_one"] = "in {0} hour",
                        ["future_other"] = "in {0} hours",
                        ["past_one"] = "{0} hour ago",
                        ["past_other"] = "{0} hours ago",
                    }
                },
                ["hour_short"] = new UnitStylePatterns
                {
                    Patterns = new(4, StringComparer.Ordinal)
                    {
                        ["future_one"] = "in {0} hr.",
                        ["future_other"] = "in {0} hr.",
                        ["past_one"] = "{0} hr. ago",
                        ["past_other"] = "{0} hr. ago",
                    }
                },
                ["hour_narrow"] = new UnitStylePatterns
                {
                    Patterns = new(4, StringComparer.Ordinal)
                    {
                        ["future_one"] = "in {0}h",
                        ["future_other"] = "in {0}h",
                        ["past_one"] = "{0}h ago",
                        ["past_other"] = "{0}h ago",
                    }
                },
                ["day_long"] = new UnitStylePatterns
                {
                    Patterns = new(4, StringComparer.Ordinal)
                    {
                        ["future_one"] = "in {0} day",
                        ["future_other"] = "in {0} days",
                        ["past_one"] = "{0} day ago",
                        ["past_other"] = "{0} days ago",
                    }
                },
                ["day_short"] = new UnitStylePatterns
                {
                    Patterns = new(4, StringComparer.Ordinal)
                    {
                        ["future_one"] = "in {0} day",
                        ["future_other"] = "in {0} days",
                        ["past_one"] = "{0} day ago",
                        ["past_other"] = "{0} days ago",
                    }
                },
                ["day_narrow"] = new UnitStylePatterns
                {
                    Patterns = new(4, StringComparer.Ordinal)
                    {
                        ["future_one"] = "in {0}d",
                        ["future_other"] = "in {0}d",
                        ["past_one"] = "{0}d ago",
                        ["past_other"] = "{0}d ago",
                    }
                },
                ["week_long"] = new UnitStylePatterns
                {
                    Patterns = new(4, StringComparer.Ordinal)
                    {
                        ["future_one"] = "in {0} week",
                        ["future_other"] = "in {0} weeks",
                        ["past_one"] = "{0} week ago",
                        ["past_other"] = "{0} weeks ago",
                    }
                },
                ["week_short"] = new UnitStylePatterns
                {
                    Patterns = new(4, StringComparer.Ordinal)
                    {
                        ["future_one"] = "in {0} wk.",
                        ["future_other"] = "in {0} wk.",
                        ["past_one"] = "{0} wk. ago",
                        ["past_other"] = "{0} wk. ago",
                    }
                },
                ["week_narrow"] = new UnitStylePatterns
                {
                    Patterns = new(4, StringComparer.Ordinal)
                    {
                        ["future_one"] = "in {0}w",
                        ["future_other"] = "in {0}w",
                        ["past_one"] = "{0}w ago",
                        ["past_other"] = "{0}w ago",
                    }
                },
                ["month_long"] = new UnitStylePatterns
                {
                    Patterns = new(4, StringComparer.Ordinal)
                    {
                        ["future_one"] = "in {0} month",
                        ["future_other"] = "in {0} months",
                        ["past_one"] = "{0} month ago",
                        ["past_other"] = "{0} months ago",
                    }
                },
                ["month_short"] = new UnitStylePatterns
                {
                    Patterns = new(4, StringComparer.Ordinal)
                    {
                        ["future_one"] = "in {0} mo.",
                        ["future_other"] = "in {0} mo.",
                        ["past_one"] = "{0} mo. ago",
                        ["past_other"] = "{0} mo. ago",
                    }
                },
                ["month_narrow"] = new UnitStylePatterns
                {
                    Patterns = new(4, StringComparer.Ordinal)
                    {
                        ["future_one"] = "in {0}mo",
                        ["future_other"] = "in {0}mo",
                        ["past_one"] = "{0}mo ago",
                        ["past_other"] = "{0}mo ago",
                    }
                },
                ["quarter_long"] = new UnitStylePatterns
                {
                    Patterns = new(4, StringComparer.Ordinal)
                    {
                        ["future_one"] = "in {0} quarter",
                        ["future_other"] = "in {0} quarters",
                        ["past_one"] = "{0} quarter ago",
                        ["past_other"] = "{0} quarters ago",
                    }
                },
                ["quarter_short"] = new UnitStylePatterns
                {
                    Patterns = new(4, StringComparer.Ordinal)
                    {
                        ["future_one"] = "in {0} qtr.",
                        ["future_other"] = "in {0} qtrs.",
                        ["past_one"] = "{0} qtr. ago",
                        ["past_other"] = "{0} qtrs. ago",
                    }
                },
                ["quarter_narrow"] = new UnitStylePatterns
                {
                    Patterns = new(4, StringComparer.Ordinal)
                    {
                        ["future_one"] = "in {0}q",
                        ["future_other"] = "in {0}q",
                        ["past_one"] = "{0}q ago",
                        ["past_other"] = "{0}q ago",
                    }
                },
                ["year_long"] = new UnitStylePatterns
                {
                    Patterns = new(4, StringComparer.Ordinal)
                    {
                        ["future_one"] = "in {0} year",
                        ["future_other"] = "in {0} years",
                        ["past_one"] = "{0} year ago",
                        ["past_other"] = "{0} years ago",
                    }
                },
                ["year_short"] = new UnitStylePatterns
                {
                    Patterns = new(4, StringComparer.Ordinal)
                    {
                        ["future_one"] = "in {0} yr.",
                        ["future_other"] = "in {0} yr.",
                        ["past_one"] = "{0} yr. ago",
                        ["past_other"] = "{0} yr. ago",
                    }
                },
                ["year_narrow"] = new UnitStylePatterns
                {
                    Patterns = new(4, StringComparer.Ordinal)
                    {
                        ["future_one"] = "in {0}y",
                        ["future_other"] = "in {0}y",
                        ["past_one"] = "{0}y ago",
                        ["past_other"] = "{0}y ago",
                    }
                },
            }
        },
        ["es"] = new LocaleRelativeTimeData
        {
            Patterns = new(24, StringComparer.Ordinal)
            {
                ["second_long"] = new UnitStylePatterns
                {
                    Patterns = new(4, StringComparer.Ordinal)
                    {
                        ["future_one"] = "dentro de {0} segundo",
                        ["future_other"] = "dentro de {0} segundos",
                        ["past_one"] = "hace {0} segundo",
                        ["past_other"] = "hace {0} segundos",
                    }
                },
                ["second_short"] = new UnitStylePatterns
                {
                    Patterns = new(4, StringComparer.Ordinal)
                    {
                        ["future_one"] = "dentro de {0} s",
                        ["future_other"] = "dentro de {0} s",
                        ["past_one"] = "hace {0} s",
                        ["past_other"] = "hace {0} s",
                    }
                },
                ["second_narrow"] = new UnitStylePatterns
                {
                    Patterns = new(4, StringComparer.Ordinal)
                    {
                        ["future_one"] = "en {0} s",
                        ["future_other"] = "en {0} s",
                        ["past_one"] = "hace {0} s",
                        ["past_other"] = "hace {0} s",
                    }
                },
                ["minute_long"] = new UnitStylePatterns
                {
                    Patterns = new(4, StringComparer.Ordinal)
                    {
                        ["future_one"] = "dentro de {0} minuto",
                        ["future_other"] = "dentro de {0} minutos",
                        ["past_one"] = "hace {0} minuto",
                        ["past_other"] = "hace {0} minutos",
                    }
                },
                ["minute_short"] = new UnitStylePatterns
                {
                    Patterns = new(4, StringComparer.Ordinal)
                    {
                        ["future_one"] = "dentro de {0} min",
                        ["future_other"] = "dentro de {0} min",
                        ["past_one"] = "hace {0} min",
                        ["past_other"] = "hace {0} min",
                    }
                },
                ["minute_narrow"] = new UnitStylePatterns
                {
                    Patterns = new(4, StringComparer.Ordinal)
                    {
                        ["future_one"] = "en {0} min",
                        ["future_other"] = "en {0} min",
                        ["past_one"] = "hace {0} min",
                        ["past_other"] = "hace {0} min",
                    }
                },
                ["hour_long"] = new UnitStylePatterns
                {
                    Patterns = new(4, StringComparer.Ordinal)
                    {
                        ["future_one"] = "dentro de {0} hora",
                        ["future_other"] = "dentro de {0} horas",
                        ["past_one"] = "hace {0} hora",
                        ["past_other"] = "hace {0} horas",
                    }
                },
                ["hour_short"] = new UnitStylePatterns
                {
                    Patterns = new(4, StringComparer.Ordinal)
                    {
                        ["future_one"] = "dentro de {0} h",
                        ["future_other"] = "dentro de {0} h",
                        ["past_one"] = "hace {0} h",
                        ["past_other"] = "hace {0} h",
                    }
                },
                ["hour_narrow"] = new UnitStylePatterns
                {
                    Patterns = new(4, StringComparer.Ordinal)
                    {
                        ["future_one"] = "en {0} h",
                        ["future_other"] = "en {0} h",
                        ["past_one"] = "hace {0} h",
                        ["past_other"] = "hace {0} h",
                    }
                },
                ["day_long"] = new UnitStylePatterns
                {
                    Patterns = new(4, StringComparer.Ordinal)
                    {
                        ["future_one"] = "dentro de {0} d\u00eda",
                        ["future_other"] = "dentro de {0} d\u00edas",
                        ["past_one"] = "hace {0} d\u00eda",
                        ["past_other"] = "hace {0} d\u00edas",
                    }
                },
                ["day_short"] = new UnitStylePatterns
                {
                    Patterns = new(4, StringComparer.Ordinal)
                    {
                        ["future_one"] = "dentro de {0} d\u00eda",
                        ["future_other"] = "dentro de {0} d\u00edas",
                        ["past_one"] = "hace {0} d\u00eda",
                        ["past_other"] = "hace {0} d\u00edas",
                    }
                },
                ["day_narrow"] = new UnitStylePatterns
                {
                    Patterns = new(4, StringComparer.Ordinal)
                    {
                        ["future_one"] = "en {0} d",
                        ["future_other"] = "en {0} d",
                        ["past_one"] = "hace {0} d",
                        ["past_other"] = "hace {0} d",
                    }
                },
                ["week_long"] = new UnitStylePatterns
                {
                    Patterns = new(4, StringComparer.Ordinal)
                    {
                        ["future_one"] = "dentro de {0} semana",
                        ["future_other"] = "dentro de {0} semanas",
                        ["past_one"] = "hace {0} semana",
                        ["past_other"] = "hace {0} semanas",
                    }
                },
                ["week_short"] = new UnitStylePatterns
                {
                    Patterns = new(4, StringComparer.Ordinal)
                    {
                        ["future_one"] = "dentro de {0} sem.",
                        ["future_other"] = "dentro de {0} sem.",
                        ["past_one"] = "hace {0} sem.",
                        ["past_other"] = "hace {0} sem.",
                    }
                },
                ["week_narrow"] = new UnitStylePatterns
                {
                    Patterns = new(4, StringComparer.Ordinal)
                    {
                        ["future_one"] = "en {0} sem.",
                        ["future_other"] = "en {0} sem.",
                        ["past_one"] = "hace {0} sem.",
                        ["past_other"] = "hace {0} sem.",
                    }
                },
                ["month_long"] = new UnitStylePatterns
                {
                    Patterns = new(4, StringComparer.Ordinal)
                    {
                        ["future_one"] = "dentro de {0} mes",
                        ["future_other"] = "dentro de {0} meses",
                        ["past_one"] = "hace {0} mes",
                        ["past_other"] = "hace {0} meses",
                    }
                },
                ["month_short"] = new UnitStylePatterns
                {
                    Patterns = new(4, StringComparer.Ordinal)
                    {
                        ["future_one"] = "dentro de {0} m",
                        ["future_other"] = "dentro de {0} m",
                        ["past_one"] = "hace {0} m",
                        ["past_other"] = "hace {0} m",
                    }
                },
                ["month_narrow"] = new UnitStylePatterns
                {
                    Patterns = new(4, StringComparer.Ordinal)
                    {
                        ["future_one"] = "en {0} m",
                        ["future_other"] = "en {0} m",
                        ["past_one"] = "hace {0} m",
                        ["past_other"] = "hace {0} m",
                    }
                },
                ["quarter_long"] = new UnitStylePatterns
                {
                    Patterns = new(4, StringComparer.Ordinal)
                    {
                        ["future_one"] = "dentro de {0} trimestre",
                        ["future_other"] = "dentro de {0} trimestres",
                        ["past_one"] = "hace {0} trimestre",
                        ["past_other"] = "hace {0} trimestres",
                    }
                },
                ["quarter_short"] = new UnitStylePatterns
                {
                    Patterns = new(4, StringComparer.Ordinal)
                    {
                        ["future_one"] = "dentro de {0} trim.",
                        ["future_other"] = "dentro de {0} trim.",
                        ["past_one"] = "hace {0} trim.",
                        ["past_other"] = "hace {0} trim.",
                    }
                },
                ["quarter_narrow"] = new UnitStylePatterns
                {
                    Patterns = new(4, StringComparer.Ordinal)
                    {
                        ["future_one"] = "en {0} T",
                        ["future_other"] = "en {0} T",
                        ["past_one"] = "hace {0} T",
                        ["past_other"] = "hace {0} T",
                    }
                },
                ["year_long"] = new UnitStylePatterns
                {
                    Patterns = new(4, StringComparer.Ordinal)
                    {
                        ["future_one"] = "dentro de {0} a\u00f1o",
                        ["future_other"] = "dentro de {0} a\u00f1os",
                        ["past_one"] = "hace {0} a\u00f1o",
                        ["past_other"] = "hace {0} a\u00f1os",
                    }
                },
                ["year_short"] = new UnitStylePatterns
                {
                    Patterns = new(4, StringComparer.Ordinal)
                    {
                        ["future_one"] = "dentro de {0} a",
                        ["future_other"] = "dentro de {0} a",
                        ["past_one"] = "hace {0} a",
                        ["past_other"] = "hace {0} a",
                    }
                },
                ["year_narrow"] = new UnitStylePatterns
                {
                    Patterns = new(4, StringComparer.Ordinal)
                    {
                        ["future_one"] = "en {0} a",
                        ["future_other"] = "en {0} a",
                        ["past_one"] = "hace {0} a",
                        ["past_other"] = "hace {0} a",
                    }
                },
            }
        },
        ["de"] = new LocaleRelativeTimeData
        {
            Patterns = new(24, StringComparer.Ordinal)
            {
                ["second_long"] = new UnitStylePatterns
                {
                    Patterns = new(4, StringComparer.Ordinal)
                    {
                        ["future_one"] = "in {0} Sekunde",
                        ["future_other"] = "in {0} Sekunden",
                        ["past_one"] = "vor {0} Sekunde",
                        ["past_other"] = "vor {0} Sekunden",
                    }
                },
                ["second_short"] = new UnitStylePatterns
                {
                    Patterns = new(4, StringComparer.Ordinal)
                    {
                        ["future_one"] = "in {0} Sek.",
                        ["future_other"] = "in {0} Sek.",
                        ["past_one"] = "vor {0} Sek.",
                        ["past_other"] = "vor {0} Sek.",
                    }
                },
                ["second_narrow"] = new UnitStylePatterns
                {
                    Patterns = new(4, StringComparer.Ordinal)
                    {
                        ["future_one"] = "in {0} s",
                        ["future_other"] = "in {0} s",
                        ["past_one"] = "vor {0} s",
                        ["past_other"] = "vor {0} s",
                    }
                },
                ["minute_long"] = new UnitStylePatterns
                {
                    Patterns = new(4, StringComparer.Ordinal)
                    {
                        ["future_one"] = "in {0} Minute",
                        ["future_other"] = "in {0} Minuten",
                        ["past_one"] = "vor {0} Minute",
                        ["past_other"] = "vor {0} Minuten",
                    }
                },
                ["minute_short"] = new UnitStylePatterns
                {
                    Patterns = new(4, StringComparer.Ordinal)
                    {
                        ["future_one"] = "in {0} Min.",
                        ["future_other"] = "in {0} Min.",
                        ["past_one"] = "vor {0} Min.",
                        ["past_other"] = "vor {0} Min.",
                    }
                },
                ["minute_narrow"] = new UnitStylePatterns
                {
                    Patterns = new(4, StringComparer.Ordinal)
                    {
                        ["future_one"] = "in {0} Min.",
                        ["future_other"] = "in {0} Min.",
                        ["past_one"] = "vor {0} Min.",
                        ["past_other"] = "vor {0} Min.",
                    }
                },
                ["hour_long"] = new UnitStylePatterns
                {
                    Patterns = new(4, StringComparer.Ordinal)
                    {
                        ["future_one"] = "in {0} Stunde",
                        ["future_other"] = "in {0} Stunden",
                        ["past_one"] = "vor {0} Stunde",
                        ["past_other"] = "vor {0} Stunden",
                    }
                },
                ["hour_short"] = new UnitStylePatterns
                {
                    Patterns = new(4, StringComparer.Ordinal)
                    {
                        ["future_one"] = "in {0} Std.",
                        ["future_other"] = "in {0} Std.",
                        ["past_one"] = "vor {0} Std.",
                        ["past_other"] = "vor {0} Std.",
                    }
                },
                ["hour_narrow"] = new UnitStylePatterns
                {
                    Patterns = new(4, StringComparer.Ordinal)
                    {
                        ["future_one"] = "in {0} Std.",
                        ["future_other"] = "in {0} Std.",
                        ["past_one"] = "vor {0} Std.",
                        ["past_other"] = "vor {0} Std.",
                    }
                },
                ["day_long"] = new UnitStylePatterns
                {
                    Patterns = new(4, StringComparer.Ordinal)
                    {
                        ["future_one"] = "in {0} Tag",
                        ["future_other"] = "in {0} Tagen",
                        ["past_one"] = "vor {0} Tag",
                        ["past_other"] = "vor {0} Tagen",
                    }
                },
                ["day_short"] = new UnitStylePatterns
                {
                    Patterns = new(4, StringComparer.Ordinal)
                    {
                        ["future_one"] = "in {0} Tag",
                        ["future_other"] = "in {0} Tagen",
                        ["past_one"] = "vor {0} Tag",
                        ["past_other"] = "vor {0} Tagen",
                    }
                },
                ["day_narrow"] = new UnitStylePatterns
                {
                    Patterns = new(4, StringComparer.Ordinal)
                    {
                        ["future_one"] = "in {0} T",
                        ["future_other"] = "in {0} T",
                        ["past_one"] = "vor {0} T",
                        ["past_other"] = "vor {0} T",
                    }
                },
                ["week_long"] = new UnitStylePatterns
                {
                    Patterns = new(4, StringComparer.Ordinal)
                    {
                        ["future_one"] = "in {0} Woche",
                        ["future_other"] = "in {0} Wochen",
                        ["past_one"] = "vor {0} Woche",
                        ["past_other"] = "vor {0} Wochen",
                    }
                },
                ["week_short"] = new UnitStylePatterns
                {
                    Patterns = new(4, StringComparer.Ordinal)
                    {
                        ["future_one"] = "in {0} Wo.",
                        ["future_other"] = "in {0} Wo.",
                        ["past_one"] = "vor {0} Wo.",
                        ["past_other"] = "vor {0} Wo.",
                    }
                },
                ["week_narrow"] = new UnitStylePatterns
                {
                    Patterns = new(4, StringComparer.Ordinal)
                    {
                        ["future_one"] = "in {0} W",
                        ["future_other"] = "in {0} W",
                        ["past_one"] = "vor {0} W",
                        ["past_other"] = "vor {0} W",
                    }
                },
                ["month_long"] = new UnitStylePatterns
                {
                    Patterns = new(4, StringComparer.Ordinal)
                    {
                        ["future_one"] = "in {0} Monat",
                        ["future_other"] = "in {0} Monaten",
                        ["past_one"] = "vor {0} Monat",
                        ["past_other"] = "vor {0} Monaten",
                    }
                },
                ["month_short"] = new UnitStylePatterns
                {
                    Patterns = new(4, StringComparer.Ordinal)
                    {
                        ["future_one"] = "in {0} Mon.",
                        ["future_other"] = "in {0} Mon.",
                        ["past_one"] = "vor {0} Mon.",
                        ["past_other"] = "vor {0} Mon.",
                    }
                },
                ["month_narrow"] = new UnitStylePatterns
                {
                    Patterns = new(4, StringComparer.Ordinal)
                    {
                        ["future_one"] = "in {0} M",
                        ["future_other"] = "in {0} M",
                        ["past_one"] = "vor {0} M",
                        ["past_other"] = "vor {0} M",
                    }
                },
                ["quarter_long"] = new UnitStylePatterns
                {
                    Patterns = new(4, StringComparer.Ordinal)
                    {
                        ["future_one"] = "in {0} Quartal",
                        ["future_other"] = "in {0} Quartalen",
                        ["past_one"] = "vor {0} Quartal",
                        ["past_other"] = "vor {0} Quartalen",
                    }
                },
                ["quarter_short"] = new UnitStylePatterns
                {
                    Patterns = new(4, StringComparer.Ordinal)
                    {
                        ["future_one"] = "in {0} Quart.",
                        ["future_other"] = "in {0} Quart.",
                        ["past_one"] = "vor {0} Quart.",
                        ["past_other"] = "vor {0} Quart.",
                    }
                },
                ["quarter_narrow"] = new UnitStylePatterns
                {
                    Patterns = new(4, StringComparer.Ordinal)
                    {
                        ["future_one"] = "in {0} Q",
                        ["future_other"] = "in {0} Q",
                        ["past_one"] = "vor {0} Q",
                        ["past_other"] = "vor {0} Q",
                    }
                },
                ["year_long"] = new UnitStylePatterns
                {
                    Patterns = new(4, StringComparer.Ordinal)
                    {
                        ["future_one"] = "in {0} Jahr",
                        ["future_other"] = "in {0} Jahren",
                        ["past_one"] = "vor {0} Jahr",
                        ["past_other"] = "vor {0} Jahren",
                    }
                },
                ["year_short"] = new UnitStylePatterns
                {
                    Patterns = new(4, StringComparer.Ordinal)
                    {
                        ["future_one"] = "in {0} J",
                        ["future_other"] = "in {0} J",
                        ["past_one"] = "vor {0} J",
                        ["past_other"] = "vor {0} J",
                    }
                },
                ["year_narrow"] = new UnitStylePatterns
                {
                    Patterns = new(4, StringComparer.Ordinal)
                    {
                        ["future_one"] = "in {0} J",
                        ["future_other"] = "in {0} J",
                        ["past_one"] = "vor {0} J",
                        ["past_other"] = "vor {0} J",
                    }
                },
            }
        },
        ["pl"] = new LocaleRelativeTimeData
        {
            Patterns = new(24, StringComparer.Ordinal)
            {
                ["second_long"] = new UnitStylePatterns
                {
                    Patterns = new(8, StringComparer.Ordinal)
                    {
                        ["future_one"] = "za {0} sekund\u0119",
                        ["future_few"] = "za {0} sekundy",
                        ["future_many"] = "za {0} sekund",
                        ["future_other"] = "za {0} sekundy",
                        ["past_one"] = "{0} sekund\u0119 temu",
                        ["past_few"] = "{0} sekundy temu",
                        ["past_many"] = "{0} sekund temu",
                        ["past_other"] = "{0} sekundy temu",
                    }
                },
                ["second_short"] = new UnitStylePatterns
                {
                    Patterns = new(8, StringComparer.Ordinal)
                    {
                        ["future_one"] = "za {0} sek.",
                        ["future_few"] = "za {0} sek.",
                        ["future_many"] = "za {0} sek.",
                        ["future_other"] = "za {0} sek.",
                        ["past_one"] = "{0} sek. temu",
                        ["past_few"] = "{0} sek. temu",
                        ["past_many"] = "{0} sek. temu",
                        ["past_other"] = "{0} sek. temu",
                    }
                },
                ["second_narrow"] = new UnitStylePatterns
                {
                    Patterns = new(8, StringComparer.Ordinal)
                    {
                        ["future_one"] = "za {0} s",
                        ["future_few"] = "za {0} s",
                        ["future_many"] = "za {0} s",
                        ["future_other"] = "za {0} s",
                        ["past_one"] = "{0} s temu",
                        ["past_few"] = "{0} s temu",
                        ["past_many"] = "{0} s temu",
                        ["past_other"] = "{0} s temu",
                    }
                },
                ["minute_long"] = new UnitStylePatterns
                {
                    Patterns = new(8, StringComparer.Ordinal)
                    {
                        ["future_one"] = "za {0} minut\u0119",
                        ["future_few"] = "za {0} minuty",
                        ["future_many"] = "za {0} minut",
                        ["future_other"] = "za {0} minuty",
                        ["past_one"] = "{0} minut\u0119 temu",
                        ["past_few"] = "{0} minuty temu",
                        ["past_many"] = "{0} minut temu",
                        ["past_other"] = "{0} minuty temu",
                    }
                },
                ["minute_short"] = new UnitStylePatterns
                {
                    Patterns = new(8, StringComparer.Ordinal)
                    {
                        ["future_one"] = "za {0} min",
                        ["future_few"] = "za {0} min",
                        ["future_many"] = "za {0} min",
                        ["future_other"] = "za {0} min",
                        ["past_one"] = "{0} min temu",
                        ["past_few"] = "{0} min temu",
                        ["past_many"] = "{0} min temu",
                        ["past_other"] = "{0} min temu",
                    }
                },
                ["minute_narrow"] = new UnitStylePatterns
                {
                    Patterns = new(8, StringComparer.Ordinal)
                    {
                        ["future_one"] = "za {0} min",
                        ["future_few"] = "za {0} min",
                        ["future_many"] = "za {0} min",
                        ["future_other"] = "za {0} min",
                        ["past_one"] = "{0} min temu",
                        ["past_few"] = "{0} min temu",
                        ["past_many"] = "{0} min temu",
                        ["past_other"] = "{0} min temu",
                    }
                },
                ["hour_long"] = new UnitStylePatterns
                {
                    Patterns = new(8, StringComparer.Ordinal)
                    {
                        ["future_one"] = "za {0} godzin\u0119",
                        ["future_few"] = "za {0} godziny",
                        ["future_many"] = "za {0} godzin",
                        ["future_other"] = "za {0} godziny",
                        ["past_one"] = "{0} godzin\u0119 temu",
                        ["past_few"] = "{0} godziny temu",
                        ["past_many"] = "{0} godzin temu",
                        ["past_other"] = "{0} godziny temu",
                    }
                },
                ["hour_short"] = new UnitStylePatterns
                {
                    Patterns = new(8, StringComparer.Ordinal)
                    {
                        ["future_one"] = "za {0} godz.",
                        ["future_few"] = "za {0} godz.",
                        ["future_many"] = "za {0} godz.",
                        ["future_other"] = "za {0} godz.",
                        ["past_one"] = "{0} godz. temu",
                        ["past_few"] = "{0} godz. temu",
                        ["past_many"] = "{0} godz. temu",
                        ["past_other"] = "{0} godz. temu",
                    }
                },
                ["hour_narrow"] = new UnitStylePatterns
                {
                    Patterns = new(8, StringComparer.Ordinal)
                    {
                        ["future_one"] = "za {0} g.",
                        ["future_few"] = "za {0} g.",
                        ["future_many"] = "za {0} g.",
                        ["future_other"] = "za {0} g.",
                        ["past_one"] = "{0} g. temu",
                        ["past_few"] = "{0} g. temu",
                        ["past_many"] = "{0} g. temu",
                        ["past_other"] = "{0} g. temu",
                    }
                },
                ["day_long"] = new UnitStylePatterns
                {
                    Patterns = new(8, StringComparer.Ordinal)
                    {
                        ["future_one"] = "za {0} dzie\u0144",
                        ["future_few"] = "za {0} dni",
                        ["future_many"] = "za {0} dni",
                        ["future_other"] = "za {0} dnia",
                        ["past_one"] = "{0} dzie\u0144 temu",
                        ["past_few"] = "{0} dni temu",
                        ["past_many"] = "{0} dni temu",
                        ["past_other"] = "{0} dnia temu",
                    }
                },
                ["day_short"] = new UnitStylePatterns
                {
                    Patterns = new(8, StringComparer.Ordinal)
                    {
                        ["future_one"] = "za {0} dzie\u0144",
                        ["future_few"] = "za {0} dni",
                        ["future_many"] = "za {0} dni",
                        ["future_other"] = "za {0} dnia",
                        ["past_one"] = "{0} dzie\u0144 temu",
                        ["past_few"] = "{0} dni temu",
                        ["past_many"] = "{0} dni temu",
                        ["past_other"] = "{0} dnia temu",
                    }
                },
                ["day_narrow"] = new UnitStylePatterns
                {
                    Patterns = new(8, StringComparer.Ordinal)
                    {
                        ["future_one"] = "za {0} dzie\u0144",
                        ["future_few"] = "za {0} dni",
                        ["future_many"] = "za {0} dni",
                        ["future_other"] = "za {0} dnia",
                        ["past_one"] = "{0} dzie\u0144 temu",
                        ["past_few"] = "{0} dni temu",
                        ["past_many"] = "{0} dni temu",
                        ["past_other"] = "{0} dnia temu",
                    }
                },
                ["week_long"] = new UnitStylePatterns
                {
                    Patterns = new(8, StringComparer.Ordinal)
                    {
                        ["future_one"] = "za {0} tydzie\u0144",
                        ["future_few"] = "za {0} tygodnie",
                        ["future_many"] = "za {0} tygodni",
                        ["future_other"] = "za {0} tygodnia",
                        ["past_one"] = "{0} tydzie\u0144 temu",
                        ["past_few"] = "{0} tygodnie temu",
                        ["past_many"] = "{0} tygodni temu",
                        ["past_other"] = "{0} tygodnia temu",
                    }
                },
                ["week_short"] = new UnitStylePatterns
                {
                    Patterns = new(8, StringComparer.Ordinal)
                    {
                        ["future_one"] = "za {0} tydz.",
                        ["future_few"] = "za {0} tyg.",
                        ["future_many"] = "za {0} tyg.",
                        ["future_other"] = "za {0} tyg.",
                        ["past_one"] = "{0} tydz. temu",
                        ["past_few"] = "{0} tyg. temu",
                        ["past_many"] = "{0} tyg. temu",
                        ["past_other"] = "{0} tyg. temu",
                    }
                },
                ["week_narrow"] = new UnitStylePatterns
                {
                    Patterns = new(8, StringComparer.Ordinal)
                    {
                        ["future_one"] = "za {0} tydz.",
                        ["future_few"] = "za {0} tyg.",
                        ["future_many"] = "za {0} tyg.",
                        ["future_other"] = "za {0} tyg.",
                        ["past_one"] = "{0} tydz. temu",
                        ["past_few"] = "{0} tyg. temu",
                        ["past_many"] = "{0} tyg. temu",
                        ["past_other"] = "{0} tyg. temu",
                    }
                },
                ["month_long"] = new UnitStylePatterns
                {
                    Patterns = new(8, StringComparer.Ordinal)
                    {
                        ["future_one"] = "za {0} miesi\u0105c",
                        ["future_few"] = "za {0} miesi\u0105ce",
                        ["future_many"] = "za {0} miesi\u0119cy",
                        ["future_other"] = "za {0} miesi\u0105ca",
                        ["past_one"] = "{0} miesi\u0105c temu",
                        ["past_few"] = "{0} miesi\u0105ce temu",
                        ["past_many"] = "{0} miesi\u0119cy temu",
                        ["past_other"] = "{0} miesi\u0105ca temu",
                    }
                },
                ["month_short"] = new UnitStylePatterns
                {
                    Patterns = new(8, StringComparer.Ordinal)
                    {
                        ["future_one"] = "za {0} mies.",
                        ["future_few"] = "za {0} mies.",
                        ["future_many"] = "za {0} mies.",
                        ["future_other"] = "za {0} mies.",
                        ["past_one"] = "{0} mies. temu",
                        ["past_few"] = "{0} mies. temu",
                        ["past_many"] = "{0} mies. temu",
                        ["past_other"] = "{0} mies. temu",
                    }
                },
                ["month_narrow"] = new UnitStylePatterns
                {
                    Patterns = new(8, StringComparer.Ordinal)
                    {
                        ["future_one"] = "za {0} mies.",
                        ["future_few"] = "za {0} mies.",
                        ["future_many"] = "za {0} mies.",
                        ["future_other"] = "za {0} mies.",
                        ["past_one"] = "{0} mies. temu",
                        ["past_few"] = "{0} mies. temu",
                        ["past_many"] = "{0} mies. temu",
                        ["past_other"] = "{0} mies. temu",
                    }
                },
                ["quarter_long"] = new UnitStylePatterns
                {
                    Patterns = new(8, StringComparer.Ordinal)
                    {
                        ["future_one"] = "za {0} kwarta\u0142",
                        ["future_few"] = "za {0} kwarta\u0142y",
                        ["future_many"] = "za {0} kwarta\u0142\u00f3w",
                        ["future_other"] = "za {0} kwarta\u0142u",
                        ["past_one"] = "{0} kwarta\u0142 temu",
                        ["past_few"] = "{0} kwarta\u0142y temu",
                        ["past_many"] = "{0} kwarta\u0142\u00f3w temu",
                        ["past_other"] = "{0} kwarta\u0142u temu",
                    }
                },
                ["quarter_short"] = new UnitStylePatterns
                {
                    Patterns = new(8, StringComparer.Ordinal)
                    {
                        ["future_one"] = "za {0} kw.",
                        ["future_few"] = "za {0} kw.",
                        ["future_many"] = "za {0} kw.",
                        ["future_other"] = "za {0} kw.",
                        ["past_one"] = "{0} kw. temu",
                        ["past_few"] = "{0} kw. temu",
                        ["past_many"] = "{0} kw. temu",
                        ["past_other"] = "{0} kw. temu",
                    }
                },
                ["quarter_narrow"] = new UnitStylePatterns
                {
                    Patterns = new(8, StringComparer.Ordinal)
                    {
                        ["future_one"] = "za {0} kw.",
                        ["future_few"] = "za {0} kw.",
                        ["future_many"] = "za {0} kw.",
                        ["future_other"] = "za {0} kw.",
                        ["past_one"] = "{0} kw. temu",
                        ["past_few"] = "{0} kw. temu",
                        ["past_many"] = "{0} kw. temu",
                        ["past_other"] = "{0} kw. temu",
                    }
                },
                ["year_long"] = new UnitStylePatterns
                {
                    Patterns = new(8, StringComparer.Ordinal)
                    {
                        ["future_one"] = "za {0} rok",
                        ["future_few"] = "za {0} lata",
                        ["future_many"] = "za {0} lat",
                        ["future_other"] = "za {0} roku",
                        ["past_one"] = "{0} rok temu",
                        ["past_few"] = "{0} lata temu",
                        ["past_many"] = "{0} lat temu",
                        ["past_other"] = "{0} roku temu",
                    }
                },
                ["year_short"] = new UnitStylePatterns
                {
                    Patterns = new(8, StringComparer.Ordinal)
                    {
                        ["future_one"] = "za {0} rok",
                        ["future_few"] = "za {0} lata",
                        ["future_many"] = "za {0} lat",
                        ["future_other"] = "za {0} roku",
                        ["past_one"] = "{0} rok temu",
                        ["past_few"] = "{0} lata temu",
                        ["past_many"] = "{0} lat temu",
                        ["past_other"] = "{0} roku temu",
                    }
                },
                ["year_narrow"] = new UnitStylePatterns
                {
                    Patterns = new(8, StringComparer.Ordinal)
                    {
                        ["future_one"] = "za {0} rok",
                        ["future_few"] = "za {0} lata",
                        ["future_many"] = "za {0} lat",
                        ["future_other"] = "za {0} roku",
                        ["past_one"] = "{0} rok temu",
                        ["past_few"] = "{0} lata temu",
                        ["past_many"] = "{0} lat temu",
                        ["past_other"] = "{0} roku temu",
                    }
                },
            }
        },
    };
}
