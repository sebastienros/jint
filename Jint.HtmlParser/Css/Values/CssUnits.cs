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
        var result = CssUnitLookup.Match(text);
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
