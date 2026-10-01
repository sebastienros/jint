using Jint.HtmlParser;

namespace Jint.Browser.Dom;

/// <summary>HTML §4.10.19.7.2's IDL-exposed autofill value, without an autofill provider.</summary>
internal static class BrowserAutocomplete
{
    // https://html.spec.whatwg.org/multipage/form-control-infrastructure.html#autofill
    internal static string Get(DomRealm realm, Element input)
    {
        var work = new DomReadWork(realm.NativeReadCheckpoint, realm.CancellationToken);
        work.Check();
        var raw = work.Attribute(input, "autocomplete");
        var anchor = work.EqualAsciiIgnoreCase(work.Attribute(input, "type"), "hidden");
        var result = Parse(raw, anchor, work);
        work.Check();
        return result;
    }

    internal static string Parse(string? raw, bool anchor, DomReadWork work)
    {
        if (raw is null) return "";
        // No valid category accepts more than five tokens. Charge every character inspected.
        var tokens = new List<string>(5);
        for (var position = 0; position < raw.Length;)
        {
            work.Step();
            if (Whitespace(raw[position])) { position++; continue; }
            var start = position++;
            while (position < raw.Length)
            {
                work.Step();
                if (Whitespace(raw[position])) break;
                position++;
            }
            if (tokens.Count == 5) return "";
            tokens.Add(raw[start..position]);
        }
        if (tokens.Count == 0) return "";
        var index = tokens.Count - 1;
        var category = Category(tokens[index], work);
        if (category == FieldCategory.Invalid || tokens.Count > Maximum(category)) return "";
        if (category is FieldCategory.Off or FieldCategory.Automatic)
            return anchor ? "" : category == FieldCategory.Off ? "off" : "on";

        // Field and credential token spelling is preserved; scope keywords are canonicalized.
        if (category == FieldCategory.Credential)
        {
            if (index == 0) return tokens[0];
            category = Category(tokens[--index], work);
            if (category is not (FieldCategory.Normal or FieldCategory.Contact) || index + 1 > Maximum(category)) return "";
        }
        if (index > 0)
        {
            index--;
            if (category == FieldCategory.Contact && Canonical(tokens[index], work, ContactHints) is { } contact)
            {
                tokens[index] = contact;
                if (index == 0) return string.Join(' ', tokens);
                index--;
            }
            if (Canonical(tokens[index], work, AddressHints) is { } mode)
            {
                tokens[index] = mode;
                if (index == 0) return string.Join(' ', tokens);
                index--;
            }
            if (index != 0 || !Section(tokens[index], work)) return "";
            tokens[index] = LowerAscii(tokens[index], work);
        }
        return string.Join(' ', tokens);
    }

    private static readonly string[] ContactHints = ["home", "work", "mobile", "fax", "pager"];
    private static readonly string[] AddressHints = ["shipping", "billing"];
    private static readonly string[] ContactFields = ["tel", "tel-country-code", "tel-national", "tel-area-code", "tel-local", "tel-local-prefix", "tel-local-suffix", "tel-extension", "email", "impp"];
    private static readonly string[] NormalFields = ["name", "honorific-prefix", "given-name", "additional-name", "family-name", "honorific-suffix", "nickname", "organization-title", "username", "new-password", "current-password", "one-time-code", "organization", "street-address", "address-line1", "address-line2", "address-line3", "address-level4", "address-level3", "address-level2", "address-level1", "country", "country-name", "postal-code", "cc-name", "cc-given-name", "cc-additional-name", "cc-family-name", "cc-number", "cc-exp", "cc-exp-month", "cc-exp-year", "cc-csc", "cc-type", "transaction-currency", "transaction-amount", "language", "bday", "bday-day", "bday-month", "bday-year", "sex", "url", "photo"];
    private enum FieldCategory { Invalid, Off, Automatic, Normal, Contact, Credential }
    private static FieldCategory Category(string token, DomReadWork work)
    {
        if (work.EqualAsciiIgnoreCase(token, "off")) return FieldCategory.Off;
        if (work.EqualAsciiIgnoreCase(token, "on")) return FieldCategory.Automatic;
        if (work.EqualAsciiIgnoreCase(token, "webauthn")) return FieldCategory.Credential;
        if (Canonical(token, work, ContactFields) is not null) return FieldCategory.Contact;
        return Canonical(token, work, NormalFields) is not null ? FieldCategory.Normal : FieldCategory.Invalid;
    }
    private static int Maximum(FieldCategory category) => category switch { FieldCategory.Normal => 3, FieldCategory.Contact => 4, FieldCategory.Credential => 5, _ => 1 };
    private static string? Canonical(string token, DomReadWork work, string[] keywords)
    {
        foreach (var keyword in keywords) if (work.EqualAsciiIgnoreCase(token, keyword)) return keyword;
        return null;
    }
    private static bool Section(string token, DomReadWork work)
    {
        if (token.Length < 8) return false;
        for (var i = 0; i < 8; i++)
        {
            work.Step();
            var c = token[i];
            if (c is >= 'A' and <= 'Z') c = (char) (c + ('a' - 'A'));
            if (c != "section-"[i]) return false;
        }
        return true;
    }
    private static string LowerAscii(string token, DomReadWork work)
    {
        var chars = token.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            work.Step();
            if (chars[i] is >= 'A' and <= 'Z') chars[i] = (char) (chars[i] + ('a' - 'A'));
        }
        return new string(chars);
    }
    private static bool Whitespace(char c) => c is ' ' or '\t' or '\n' or '\r' or '\f';
}
