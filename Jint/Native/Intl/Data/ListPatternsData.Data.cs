namespace Jint.Native.Intl.Data;

internal static partial class ListPatternsData
{
    private static readonly Dictionary<string, Dictionary<string, ListPatterns>> _patterns = new(4, StringComparer.OrdinalIgnoreCase)
    {
        ["en"] = new(9, StringComparer.Ordinal)
        {
            ["conjunction_long"] = new ListPatterns { Start = "{0}, {1}", Middle = "{0}, {1}", End = "{0}, and {1}", Two = "{0} and {1}" },
            ["conjunction_short"] = new ListPatterns { Start = "{0}, {1}", Middle = "{0}, {1}", End = "{0}, & {1}", Two = "{0} & {1}" },
            ["conjunction_narrow"] = new ListPatterns { Start = "{0}, {1}", Middle = "{0}, {1}", End = "{0}, {1}", Two = "{0}, {1}" },
            ["disjunction_long"] = new ListPatterns { Start = "{0}, {1}", Middle = "{0}, {1}", End = "{0}, or {1}", Two = "{0} or {1}" },
            ["disjunction_short"] = new ListPatterns { Start = "{0}, {1}", Middle = "{0}, {1}", End = "{0}, or {1}", Two = "{0} or {1}" },
            ["disjunction_narrow"] = new ListPatterns { Start = "{0}, {1}", Middle = "{0}, {1}", End = "{0}, or {1}", Two = "{0} or {1}" },
            ["unit_long"] = new ListPatterns { Start = "{0}, {1}", Middle = "{0}, {1}", End = "{0}, {1}", Two = "{0}, {1}" },
            ["unit_short"] = new ListPatterns { Start = "{0}, {1}", Middle = "{0}, {1}", End = "{0}, {1}", Two = "{0}, {1}" },
            ["unit_narrow"] = new ListPatterns { Start = "{0} {1}", Middle = "{0} {1}", End = "{0} {1}", Two = "{0} {1}" },
        },
        ["es"] = new(9, StringComparer.Ordinal)
        {
            ["conjunction_long"] = new ListPatterns { Start = "{0}, {1}", Middle = "{0}, {1}", End = "{0} y {1}", Two = "{0} y {1}" },
            ["conjunction_short"] = new ListPatterns { Start = "{0}, {1}", Middle = "{0}, {1}", End = "{0} y {1}", Two = "{0} y {1}" },
            ["conjunction_narrow"] = new ListPatterns { Start = "{0}, {1}", Middle = "{0}, {1}", End = "{0} y {1}", Two = "{0} y {1}" },
            ["disjunction_long"] = new ListPatterns { Start = "{0}, {1}", Middle = "{0}, {1}", End = "{0} o {1}", Two = "{0} o {1}" },
            ["disjunction_short"] = new ListPatterns { Start = "{0}, {1}", Middle = "{0}, {1}", End = "{0} o {1}", Two = "{0} o {1}" },
            ["disjunction_narrow"] = new ListPatterns { Start = "{0}, {1}", Middle = "{0}, {1}", End = "{0} o {1}", Two = "{0} o {1}" },
            ["unit_long"] = new ListPatterns { Start = "{0}, {1}", Middle = "{0}, {1}", End = "{0} y {1}", Two = "{0} y {1}" },
            ["unit_short"] = new ListPatterns { Start = "{0}, {1}", Middle = "{0}, {1}", End = "{0}, {1}", Two = "{0} y {1}" },
            ["unit_narrow"] = new ListPatterns { Start = "{0} {1}", Middle = "{0} {1}", End = "{0} {1}", Two = "{0} {1}" },
        },
        ["de"] = new(9, StringComparer.Ordinal)
        {
            ["conjunction_long"] = new ListPatterns { Start = "{0}, {1}", Middle = "{0}, {1}", End = "{0} und {1}", Two = "{0} und {1}" },
            ["conjunction_short"] = new ListPatterns { Start = "{0}, {1}", Middle = "{0}, {1}", End = "{0}, {1}", Two = "{0}, {1}" },
            ["conjunction_narrow"] = new ListPatterns { Start = "{0}, {1}", Middle = "{0}, {1}", End = "{0}, {1}", Two = "{0}, {1}" },
            ["disjunction_long"] = new ListPatterns { Start = "{0}, {1}", Middle = "{0}, {1}", End = "{0} oder {1}", Two = "{0} oder {1}" },
            ["disjunction_short"] = new ListPatterns { Start = "{0}, {1}", Middle = "{0}, {1}", End = "{0} od. {1}", Two = "{0} od. {1}" },
            ["disjunction_narrow"] = new ListPatterns { Start = "{0}, {1}", Middle = "{0}, {1}", End = "{0} od. {1}", Two = "{0} od. {1}" },
            ["unit_long"] = new ListPatterns { Start = "{0}, {1}", Middle = "{0}, {1}", End = "{0}, {1}", Two = "{0}, {1}" },
            ["unit_short"] = new ListPatterns { Start = "{0}, {1}", Middle = "{0}, {1}", End = "{0}, {1}", Two = "{0}, {1}" },
            ["unit_narrow"] = new ListPatterns { Start = "{0} {1}", Middle = "{0} {1}", End = "{0} {1}", Two = "{0} {1}" },
        },
        ["pl"] = new(9, StringComparer.Ordinal)
        {
            ["conjunction_long"] = new ListPatterns { Start = "{0}, {1}", Middle = "{0}, {1}", End = "{0} i {1}", Two = "{0} i {1}" },
            ["conjunction_short"] = new ListPatterns { Start = "{0}, {1}", Middle = "{0}, {1}", End = "{0} i {1}", Two = "{0} i {1}" },
            ["conjunction_narrow"] = new ListPatterns { Start = "{0}, {1}", Middle = "{0}, {1}", End = "{0} i {1}", Two = "{0} i {1}" },
            ["disjunction_long"] = new ListPatterns { Start = "{0}, {1}", Middle = "{0}, {1}", End = "{0} lub {1}", Two = "{0} lub {1}" },
            ["disjunction_short"] = new ListPatterns { Start = "{0}, {1}", Middle = "{0}, {1}", End = "{0} lub {1}", Two = "{0} lub {1}" },
            ["disjunction_narrow"] = new ListPatterns { Start = "{0}, {1}", Middle = "{0}, {1}", End = "{0} lub {1}", Two = "{0} lub {1}" },
            ["unit_long"] = new ListPatterns { Start = "{0}, {1}", Middle = "{0}, {1}", End = "{0}, {1}", Two = "{0}, {1}" },
            ["unit_short"] = new ListPatterns { Start = "{0}, {1}", Middle = "{0}, {1}", End = "{0}, {1}", Two = "{0}, {1}" },
            ["unit_narrow"] = new ListPatterns { Start = "{0} {1}", Middle = "{0} {1}", End = "{0} {1}", Two = "{0} {1}" },
        },
    };
}
