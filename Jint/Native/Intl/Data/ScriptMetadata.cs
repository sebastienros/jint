// Generated from CLDR 48.2 (https://github.com/unicode-org/cldr/releases/tag/release-48-2),
// common/properties/scriptMetadata.txt, field 6 (RTL): the 37 scripts marked YES and the 140 marked NO.
// The four marked UNKNOWN (Zyyy, Zinh, Zzzz, Brai) and every script the file does not list fall through to
// the default arm. Unicode License v3 (CREDITS.txt). The entries are CLDR's own: update them from a later
// release of that file, not one at a time.

namespace Jint.Native.Intl.Data;

/// <summary>
/// The script metadata https://unicode.org/reports/tr35/tr35.html#Script_Metadata records for a script.
/// </summary>
internal static class ScriptMetadata
{
    /// <summary>
    /// The default general ordering of characters within a line in <paramref name="script"/>, a title-cased
    /// four-letter script subtag: <c>"rtl"</c>, <c>"ltr"</c>, or null when the metadata does not say.
    /// </summary>
    internal static string? GetDirection(string script) => script switch
    {
        "Adlm" or "Arab" or "Armi" or "Avst" or "Chrs" or "Cprt" or "Elym" or "Gara" or "Hatr" or "Hebr"
            or "Hung" or "Khar" or "Lydi" or "Mand" or "Mani" or "Mend" or "Merc" or "Mero" or "Narb" or "Nbat"
            or "Nkoo" or "Orkh" or "Ougr" or "Palm" or "Phli" or "Phlp" or "Phnx" or "Prti" or "Rohg" or "Samr"
            or "Sarb" or "Sidt" or "Sogd" or "Sogo" or "Syrc" or "Thaa" or "Yezi" => "rtl",

        "Aghb" or "Ahom" or "Armn" or "Bali" or "Bamu" or "Bass" or "Batk" or "Beng" or "Berf" or "Bhks"
            or "Bopo" or "Brah" or "Bugi" or "Buhd" or "Cakm" or "Cans" or "Cari" or "Cham" or "Cher" or "Copt"
            or "Cpmn" or "Cyrl" or "Deva" or "Diak" or "Dogr" or "Dsrt" or "Dupl" or "Egyp" or "Elba" or "Ethi"
            or "Geor" or "Glag" or "Gong" or "Gonm" or "Goth" or "Gran" or "Grek" or "Gujr" or "Gukh" or "Guru"
            or "Hanb" or "Hang" or "Hani" or "Hano" or "Hans" or "Hant" or "Hira" or "Hluw" or "Hmng" or "Hmnp"
            or "Ital" or "Jamo" or "Java" or "Jpan" or "Kali" or "Kana" or "Kawi" or "Khmr" or "Khoj" or "Kits"
            or "Knda" or "Kore" or "Krai" or "Kthi" or "Lana" or "Laoo" or "Latn" or "Lepc" or "Limb" or "Lina"
            or "Linb" or "Lisu" or "Lyci" or "Mahj" or "Maka" or "Marc" or "Medf" or "Mlym" or "Modi" or "Mong"
            or "Mroo" or "Mtei" or "Mult" or "Mymr" or "Nagm" or "Nand" or "Newa" or "Nshu" or "Ogam" or "Olck"
            or "Onao" or "Orya" or "Osge" or "Osma" or "Pauc" or "Perm" or "Phag" or "Plrd" or "Rjng" or "Runr"
            or "Saur" or "Sgnw" or "Shaw" or "Shrd" or "Sidd" or "Sind" or "Sinh" or "Sora" or "Soyo" or "Sund"
            or "Sunu" or "Sylo" or "Tagb" or "Takr" or "Tale" or "Talu" or "Taml" or "Tang" or "Tavt" or "Tayo"
            or "Telu" or "Tfng" or "Tglg" or "Thai" or "Tibt" or "Tirh" or "Tnsa" or "Todr" or "Tols" or "Toto"
            or "Tutg" or "Ugar" or "Vaii" or "Vith" or "Wara" or "Wcho" or "Xpeo" or "Xsux" or "Yiii" or "Zanb" => "ltr",

        _ => null,
    };
}
