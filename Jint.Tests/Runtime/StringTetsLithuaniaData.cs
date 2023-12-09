namespace Jint.Tests.Runtime
{
    public class StringTetsLithuaniaData
    {
        // Results obtained from node -v 18.12.0.
        public TheoryData<string, string> TestData()
        {
            return new TheoryData<string, string> // <stringToParse, result>
            {
                // COMBINING DOT ABOVE (U+0307) not removed when uppercasing capital I and J.
                // I
                { "İlorem ipsum", "İLOREM IPSUM" },
                { "İİlorem ipsum", "İİLOREM IPSUM" },
                { "loremİipsum", "LOREMİIPSUM" },
                { "loremİİipsum", "LOREMİİIPSUM" },
                { "lorem ipsumİ", "LOREM IPSUMİ" },
                { "lorem ipsumİİ", "LOREM IPSUMİİ" },

                // J
                { "J̇lorem ipsum", "J̇LOREM IPSUM"  },
                { "J̇J̇lorem ipsum", "J̇J̇LOREM IPSUM" },
                { "loremJ̇ipsum", "LOREMJ̇IPSUM" },
                { "loremJ̇J̇ipsum", "LOREMJ̇J̇IPSUM" },
                { "lorem ipsumJ̇", "LOREM IPSUMJ̇" },
                { "lorem ipsumJ̇J̇", "LOREM IPSUMJ̇J̇" },

                // DOT ABOVE (U+0307) removed if its other capital letter other than I or J.
                { "Ȧlorem ipsum", "ALOREM IPSUM" },
                { "ȦȦlorem ipsum", "AALOREM IPSUM" },
                { "loremȦipsum", "LOREMAIPSUM" },
                { "loremȦȦipsum", "LOREMAAIPSUM" },
                { "lorem ipsumȦ", "LOREM IPSUMA" },
                {  "lorem ipsumȦȦ", "LOREM IPSUMAA" },

                // COMBINING DOT ABOVE (U+0307) removed when preceded by Soft_Dotted
                // Character directly preceded by Soft_Dotted.
                // "\u0069" + "\u0307", not latin 'i'.
                { "ilorem ipsum", "ILOREM IPSUM" },
                { "iilorem ipsum", "IILOREM IPSUM" },
                { "loremiipsum", "LOREMIIPSUM" },
                { "loremiiipsum", "LOREMIIIPSUM" },
                { "loremipsumi", "LOREMIPSUMI" },
                { "loremipsumii", "LOREMIPSUMII" },
                // "\u006A" + "\u0307", not latin 'j'.
                { "j̇lorem ipsum", "JLOREM IPSUM" },
                { "j̇j̇lorem ipsum", "JJLOREM IPSUM" },
                { "loremj̇ipsum", "LOREMJIPSUM" },
                { "loremj̇j̇ipsum", "LOREMJJIPSUM" },
                { "loremipsumj̇", "LOREMIPSUMJ" },
                { "loremipsumj̇j̇", "LOREMIPSUMJJ" },
                // "\u012F" + "\u0307" 
                { "į̇lorem ipsum", "ĮLOREM IPSUM" },
                { "į̇į̇lorem ipsum", "ĮĮLOREM IPSUM" },
                { "loremį̇ipsum", "LOREMĮIPSUM" },
                { "loremį̇į̇ipsum", "LOREMĮĮIPSUM" },
                { "loremipsumį̇", "LOREMIPSUMĮ" },
                { "loremipsumį̇į̇", "LOREMIPSUMĮĮ" },
            };
        }
    }
}

//var softDotted = [
//    "\u0069",
//    "\u006A",   // LATIN SMALL LETTER I..LATIN SMALL LETTER J
//    "\u012F",             // LATIN SMALL LETTER I WITH OGONEK
//    "\u0249",             // LATIN SMALL LETTER J WITH STROKE
//    "\u0268",             // LATIN SMALL LETTER I WITH STROKE
//    "\u029D",             // LATIN SMALL LETTER J WITH CROSSED-TAIL
//    "\u02B2",             // MODIFIER LETTER SMALL J
//    "\u03F3",             // GREEK LETTER YOT
//    "\u0456",             // CYRILLIC SMALL LETTER BYELORUSSIAN-UKRAINIAN I
//    "\u0458",             // CYRILLIC SMALL LETTER JE
//    "\u1D62",             // LATIN SUBSCRIPT SMALL LETTER I
//    "\u1D96",             // LATIN SMALL LETTER I WITH RETROFLEX HOOK
//    "\u1DA4",             // MODIFIER LETTER SMALL I WITH STROKE
//    "\u1DA8",             // MODIFIER LETTER SMALL J WITH CROSSED-TAIL
//    "\u1E2D",             // LATIN SMALL LETTER I WITH TILDE BELOW
//    "\u1ECB",             // LATIN SMALL LETTER I WITH DOT BELOW
//    "\u2071",             // SUPERSCRIPT LATIN SMALL LETTER I
//    "\u2148",
//    "\u2149",   // DOUBLE-STRUCK ITALIC SMALL I..DOUBLE-STRUCK ITALIC SMALL J
//    "\u2C7C",             // LATIN SUBSCRIPT SMALL LETTER J
//    "\uD835\uDC22",
//    "\uD835\uDC23",   // MATHEMATICAL BOLD SMALL I..MATHEMATICAL BOLD SMALL J
//    "\uD835\uDC56",
//    "\uD835\uDC57",   // MATHEMATICAL ITALIC SMALL I..MATHEMATICAL ITALIC SMALL J
//    "\uD835\uDC8A",
//    "\uD835\uDC8B",   // MATHEMATICAL BOLD ITALIC SMALL I..MATHEMATICAL BOLD ITALIC SMALL J
//    "\uD835\uDCBE",
//    "\uD835\uDCBF",   // MATHEMATICAL SCRIPT SMALL I..MATHEMATICAL SCRIPT SMALL J
//    "\uD835\uDCF2",
//    "\uD835\uDCF3",   // MATHEMATICAL BOLD SCRIPT SMALL I..MATHEMATICAL BOLD SCRIPT SMALL J
//    "\uD835\uDD26",
//    "\uD835\uDD27",   // MATHEMATICAL FRAKTUR SMALL I..MATHEMATICAL FRAKTUR SMALL J
//    "\uD835\uDD5A",
//    "\uD835\uDD5B",   // MATHEMATICAL DOUBLE-STRUCK SMALL I..MATHEMATICAL DOUBLE-STRUCK SMALL J
//    "\uD835\uDD8E",
//    "\uD835\uDD8F",   // MATHEMATICAL BOLD FRAKTUR SMALL I..MATHEMATICAL BOLD FRAKTUR SMALL J
//    "\uD835\uDDC2",
//    "\uD835\uDDC3",   // MATHEMATICAL SANS-SERIF SMALL I..MATHEMATICAL SANS-SERIF SMALL J
//    "\uD835\uDDF6",
//    "\uD835\uDDF7",   // MATHEMATICAL SANS-SERIF BOLD SMALL I..MATHEMATICAL SANS-SERIF BOLD SMALL J
//    "\uD835\uDE2A",
//    "\uD835\uDE2B",   // MATHEMATICAL SANS-SERIF ITALIC SMALL I..MATHEMATICAL SANS-SERIF ITALIC SMALL J
//    "\uD835\uDE5E",
//    "\uD835\uDE5F",   // MATHEMATICAL SANS-SERIF BOLD ITALIC SMALL I..MATHEMATICAL SANS-SERIF BOLD ITALIC SMALL J
//    "\uD835\uDE92",
//    "\uD835\uDE93",   // MATHEMATICAL MONOSPACE SMALL I..MATHEMATICAL MONOSPACE SMALL J
//];
