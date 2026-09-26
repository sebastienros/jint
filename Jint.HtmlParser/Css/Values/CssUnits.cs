namespace Jint.HtmlParser.Css.Values;

internal enum CssUnitCategory { None, Length, Angle, Time, Frequency, Resolution, Flex }

internal enum CssUnit
{
    None,
    Px, Cm, Mm, Q, In, Pt, Pc,
    Em, Rem, Ex, Rex, Cap, Rcap, Ch, Rch, Ic, Ric, Lh, Rlh,
    Vw, Vh, Vi, Vb, Vmin, Vmax,
    Svw, Svh, Svi, Svb, Svmin, Svmax,
    Lvw, Lvh, Lvi, Lvb, Lvmin, Lvmax,
    Dvw, Dvh, Dvi, Dvb, Dvmin, Dvmax,
    Cqw, Cqh, Cqi, Cqb, Cqmin, Cqmax,
    Deg, Grad, Rad, Turn,
    S, Ms,
    Hz, Khz,
    Dpi, Dpcm, Dppx, X,
    Fr
}

internal static class CssUnits
{
    // CSS Values 4, §§ 6–7; CSS Conditional 5, § 2.2; Editor's Draft, 2026-09-23.
    private static readonly Dictionary<string, CssUnit> Units = new(StringComparer.OrdinalIgnoreCase)
    {
        ["px"] = CssUnit.Px,
        ["cm"] = CssUnit.Cm,
        ["mm"] = CssUnit.Mm,
        ["q"] = CssUnit.Q,
        ["in"] = CssUnit.In,
        ["pt"] = CssUnit.Pt,
        ["pc"] = CssUnit.Pc,
        ["em"] = CssUnit.Em,
        ["rem"] = CssUnit.Rem,
        ["ex"] = CssUnit.Ex,
        ["rex"] = CssUnit.Rex,
        ["cap"] = CssUnit.Cap,
        ["rcap"] = CssUnit.Rcap,
        ["ch"] = CssUnit.Ch,
        ["rch"] = CssUnit.Rch,
        ["ic"] = CssUnit.Ic,
        ["ric"] = CssUnit.Ric,
        ["lh"] = CssUnit.Lh,
        ["rlh"] = CssUnit.Rlh,
        ["vw"] = CssUnit.Vw,
        ["vh"] = CssUnit.Vh,
        ["vi"] = CssUnit.Vi,
        ["vb"] = CssUnit.Vb,
        ["vmin"] = CssUnit.Vmin,
        ["vmax"] = CssUnit.Vmax,
        ["svw"] = CssUnit.Svw,
        ["svh"] = CssUnit.Svh,
        ["svi"] = CssUnit.Svi,
        ["svb"] = CssUnit.Svb,
        ["svmin"] = CssUnit.Svmin,
        ["svmax"] = CssUnit.Svmax,
        ["lvw"] = CssUnit.Lvw,
        ["lvh"] = CssUnit.Lvh,
        ["lvi"] = CssUnit.Lvi,
        ["lvb"] = CssUnit.Lvb,
        ["lvmin"] = CssUnit.Lvmin,
        ["lvmax"] = CssUnit.Lvmax,
        ["dvw"] = CssUnit.Dvw,
        ["dvh"] = CssUnit.Dvh,
        ["dvi"] = CssUnit.Dvi,
        ["dvb"] = CssUnit.Dvb,
        ["dvmin"] = CssUnit.Dvmin,
        ["dvmax"] = CssUnit.Dvmax,
        ["cqw"] = CssUnit.Cqw,
        ["cqh"] = CssUnit.Cqh,
        ["cqi"] = CssUnit.Cqi,
        ["cqb"] = CssUnit.Cqb,
        ["cqmin"] = CssUnit.Cqmin,
        ["cqmax"] = CssUnit.Cqmax,
        ["deg"] = CssUnit.Deg,
        ["grad"] = CssUnit.Grad,
        ["rad"] = CssUnit.Rad,
        ["turn"] = CssUnit.Turn,
        ["s"] = CssUnit.S,
        ["ms"] = CssUnit.Ms,
        ["hz"] = CssUnit.Hz,
        ["khz"] = CssUnit.Khz,
        ["dpi"] = CssUnit.Dpi,
        ["dpcm"] = CssUnit.Dpcm,
        ["dppx"] = CssUnit.Dppx,
        ["x"] = CssUnit.X,
        ["fr"] = CssUnit.Fr,
    };

    internal static CssUnit Recognize(string text, CssValueWork work)
    {
        work.CheckCancellation();
        if (text.Length is < 1 or > 5)
        {
            work.CheckCancellation();
            return CssUnit.None;
        }
        foreach (var ch in text)
        {
            work.Charge(1);
            if (ch > 0x7f)
            {
                work.CheckCancellation();
                return CssUnit.None;
            }
        }
        work.Charge(text.Length);
        var result = Units.TryGetValue(text, out var unit) ? unit : CssUnit.None;
        work.CheckCancellation();
        return result;
    }

    internal static CssUnitCategory Category(this CssUnit unit) => unit switch
    {
        >= CssUnit.Px and <= CssUnit.Cqmax => CssUnitCategory.Length,
        >= CssUnit.Deg and <= CssUnit.Turn => CssUnitCategory.Angle,
        >= CssUnit.S and <= CssUnit.Ms => CssUnitCategory.Time,
        >= CssUnit.Hz and <= CssUnit.Khz => CssUnitCategory.Frequency,
        >= CssUnit.Dpi and <= CssUnit.X => CssUnitCategory.Resolution,
        CssUnit.Fr => CssUnitCategory.Flex,
        _ => CssUnitCategory.None
    };
}
