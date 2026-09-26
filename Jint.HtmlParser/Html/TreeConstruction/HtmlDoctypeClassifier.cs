using System;

namespace Jint.HtmlParser.Html;

// HTML Standard §13.2.6.4.1 (snapshot 2026-09-22).
internal static class HtmlDoctypeClassifier
{
    private static readonly string[] QuirksPublicPrefixes =
    [
        "+//Silmaril//dtd html Pro v0r11 19970101//",
        "-//AS//DTD HTML 3.0 asWedit + extensions//",
        "-//AdvaSoft Ltd//DTD HTML 3.0 asWedit + extensions//",
        "-//IETF//DTD HTML 2.0 Level 1//",
        "-//IETF//DTD HTML 2.0 Level 2//",
        "-//IETF//DTD HTML 2.0 Strict Level 1//",
        "-//IETF//DTD HTML 2.0 Strict Level 2//",
        "-//IETF//DTD HTML 2.0 Strict//",
        "-//IETF//DTD HTML 2.0//",
        "-//IETF//DTD HTML 2.1E//",
        "-//IETF//DTD HTML 3.0//",
        "-//IETF//DTD HTML 3.2 Final//",
        "-//IETF//DTD HTML 3.2//",
        "-//IETF//DTD HTML 3//",
        "-//IETF//DTD HTML Level 0//",
        "-//IETF//DTD HTML Level 1//",
        "-//IETF//DTD HTML Level 2//",
        "-//IETF//DTD HTML Level 3//",
        "-//IETF//DTD HTML Strict Level 0//",
        "-//IETF//DTD HTML Strict Level 1//",
        "-//IETF//DTD HTML Strict Level 2//",
        "-//IETF//DTD HTML Strict Level 3//",
        "-//IETF//DTD HTML Strict//",
        "-//IETF//DTD HTML//",
        "-//Metrius//DTD Metrius Presentational//",
        "-//Microsoft//DTD Internet Explorer 2.0 HTML Strict//",
        "-//Microsoft//DTD Internet Explorer 2.0 HTML//",
        "-//Microsoft//DTD Internet Explorer 2.0 Tables//",
        "-//Microsoft//DTD Internet Explorer 3.0 HTML Strict//",
        "-//Microsoft//DTD Internet Explorer 3.0 HTML//",
        "-//Microsoft//DTD Internet Explorer 3.0 Tables//",
        "-//Netscape Comm. Corp.//DTD HTML//",
        "-//Netscape Comm. Corp.//DTD Strict HTML//",
        "-//O'Reilly and Associates//DTD HTML 2.0//",
        "-//O'Reilly and Associates//DTD HTML Extended 1.0//",
        "-//O'Reilly and Associates//DTD HTML Extended Relaxed 1.0//",
        "-//SQ//DTD HTML 2.0 HoTMetaL + extensions//",
        "-//SoftQuad Software//DTD HoTMetaL PRO 6.0::19990601::extensions to HTML 4.0//",
        "-//SoftQuad//DTD HoTMetaL PRO 4.0::19971010::extensions to HTML 4.0//",
        "-//Spyglass//DTD HTML 2.0 Extended//",
        "-//Sun Microsystems Corp.//DTD HotJava HTML//",
        "-//Sun Microsystems Corp.//DTD HotJava Strict HTML//",
        "-//W3C//DTD HTML 3 1995-03-24//",
        "-//W3C//DTD HTML 3.2 Draft//",
        "-//W3C//DTD HTML 3.2 Final//",
        "-//W3C//DTD HTML 3.2//",
        "-//W3C//DTD HTML 3.2S Draft//",
        "-//W3C//DTD HTML 4.0 Frameset//",
        "-//W3C//DTD HTML 4.0 Transitional//",
        "-//W3C//DTD HTML Experimental 19960712//",
        "-//W3C//DTD HTML Experimental 970421//",
        "-//W3C//DTD W3 HTML//",
        "-//W3O//DTD W3 HTML 3.0//",
        "-//WebTechs//DTD Mozilla HTML 2.0//",
        "-//WebTechs//DTD Mozilla HTML//"
    ];

    private static readonly string[] Html401Prefixes =
    [
        "-//W3C//DTD HTML 4.01 Frameset//",
        "-//W3C//DTD HTML 4.01 Transitional//"
    ];

    private static readonly string[] LimitedPublicPrefixes =
    [
        "-//W3C//DTD XHTML 1.0 Frameset//",
        "-//W3C//DTD XHTML 1.0 Transitional//"
    ];

    internal static DocumentMode Classify(HtmlToken token)
    {
        if (token.ForceQuirks || !Equal(token.Name, "html") ||
            Equal(token.PublicIdentifier, "-//W3O//DTD W3 HTML Strict 3.0//EN//") ||
            Equal(token.PublicIdentifier, "-/W3C/DTD HTML 4.0 Transitional/EN") ||
            Equal(token.PublicIdentifier, "HTML") ||
            Equal(token.SystemIdentifier, "http://www.ibm.com/data/dtd/v11/ibmxhtml1-transitional.dtd") ||
            HasPrefix(token.PublicIdentifier, QuirksPublicPrefixes) ||
            (string.IsNullOrEmpty(token.SystemIdentifier) && HasPrefix(token.PublicIdentifier, Html401Prefixes)))
            return DocumentMode.Quirks;

        if (HasPrefix(token.PublicIdentifier, LimitedPublicPrefixes) ||
            (!string.IsNullOrEmpty(token.SystemIdentifier) && HasPrefix(token.PublicIdentifier, Html401Prefixes)))
            return DocumentMode.LimitedQuirks;

        return DocumentMode.NoQuirks;
    }

    private static bool HasPrefix(string? value, string[] prefixes)
    {
        if (value is null) return false;
        foreach (var prefix in prefixes)
        {
            if (value.Length < prefix.Length) continue;
            var matches = true;
            for (var i = 0; i < prefix.Length; i++)
            {
                if (Lower(value[i]) == Lower(prefix[i])) continue;
                matches = false;
                break;
            }
            if (matches) return true;
        }
        return false;
    }

    private static bool Equal(string? value, string expected)
    {
        if (value is null || value.Length != expected.Length) return false;
        for (var i = 0; i < expected.Length; i++)
            if (Lower(value[i]) != Lower(expected[i])) return false;
        return true;
    }

    private static char Lower(char value) => (uint) (value - 'A') <= 25 ? (char) (value + 32) : value;
}
