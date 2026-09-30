using System.Text;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.WebApi.Fetch;
using Jint.WebApi.Files;
using Jint.WebApi.Url;
using Jint.WebApi.Url.Parsing;

namespace Jint.Browser.Cookies;

/// <summary>WebIDL conversions and https://cookiestore.spec.whatwg.org/#set-a-cookie validation.</summary>
internal static class CookieValues
{
    internal static ObjectInstance? Dictionary(Realm realm, JsValue value)
    {
        if (!value.IsNullOrUndefined() && value is not ObjectInstance)
        {
            Throw.TypeError(realm, "Cookie options must be a dictionary.");
        }
        return value as ObjectInstance;
    }

    internal static string? Text(ObjectInstance? dictionary, string name)
    {
        var value = dictionary?.Get(name) ?? JsValue.Undefined;
        return value.IsUndefined() ? null : UrlValues.ToUsvString(value);
    }

    private static string Required(Realm realm, ObjectInstance? dictionary, string name)
    {
        var value = Text(dictionary, name);
        if (value is null) Throw.TypeError(realm, "Required cookie member '" + name + "' is missing.");
        return value!;
    }

    /// <summary>https://webidl.spec.whatwg.org/#es-dictionary - read members in lexicographic order, once.</summary>
    internal static CookieOptions Options(Realm realm, ObjectInstance? dictionary, bool delete)
    {
        var domainValue = dictionary?.Get("domain") ?? JsValue.Undefined;
        var domain = domainValue.IsNullOrUndefined() ? null : UrlValues.ToUsvString(domainValue);
        double? expires = null;
        long? maxAge = delete ? 0 : null;
        if (!delete)
        {
            var expiresValue = dictionary?.Get("expires") ?? JsValue.Undefined;
            if (!expiresValue.IsNullOrUndefined())
            {
                expires = TypeConverter.ToNumber(expiresValue);
                if (!double.IsFinite(expires.Value)) Throw.TypeError(realm, "Cookie expires must be finite.");
            }
            var ageValue = dictionary?.Get("maxAge") ?? JsValue.Undefined;
            if (!ageValue.IsNullOrUndefined()) maxAge = FileApi.ToLongLong(ageValue);
        }
        var name = Required(realm, dictionary, "name");
        var partitioned = TypeConverter.ToBoolean(dictionary?.Get("partitioned") ?? JsValue.Undefined);
        var path = Text(dictionary, "path") ?? "/";
        var sameSite = delete ? "strict" : Text(dictionary, "sameSite") ?? "strict";
        if (sameSite is not ("strict" or "lax" or "none")) Throw.TypeError(realm, "Invalid CookieSameSite value.");
        var value = delete ? (Normalize(name).Length == 0 ? "deleted" : "") : Required(realm, dictionary, "value");
        return new CookieOptions(name, value, domain, path, expires, maxAge, sameSite, partitioned);
    }

    internal static string Normalize(string text) => text.Trim(' ', '\t');

    internal static SetCookie Validate(Realm realm, Uri uri, in CookieOptions options)
    {
        var name = Normalize(options.Name);
        var value = Normalize(options.Value);
        if (InvalidPair(name) || InvalidPair(value) || name.Contains('=')
            || name.Length == 0 && (value.Length == 0 || value.Contains('=') || HasPrefix(value))
            || Prefix(name, "__http-")
            || Prefix(name, "__host-http-")
            || Encoding.UTF8.GetByteCount(name) + (long) Encoding.UTF8.GetByteCount(value) > 4096)
        {
            Throw.TypeError(realm, "Invalid cookie name or value.");
        }

        var hostPrefix = Prefix(name, "__host-");
        string? domain = null;
        if (options.Domain is { } supplied)
        {
            if (supplied.StartsWith('.') || hostPrefix
                || !HostParser.TryParse(supplied, isOpaque: false, out var parsed))
            {
                Throw.TypeError(realm, "Invalid cookie domain.");
            }
            else
            {
                domain = parsed.Serialized;
            }
            if (domain is null || Encoding.UTF8.GetByteCount(domain) > 1024
                || !(string.Equals(uri.IdnHost, domain, StringComparison.OrdinalIgnoreCase)
                    || uri.HostNameType == UriHostNameType.Dns && domain.Contains('.')
                    && uri.IdnHost.EndsWith("." + domain, StringComparison.OrdinalIgnoreCase)))
            {
                Throw.TypeError(realm, "Cookie domain must domain-match the document host.");
            }
        }

        if (options.Expires is not null && options.MaxAge is not null)
        {
            Throw.TypeError(realm, "Cookie expires and maxAge cannot both be specified.");
        }
        var path = options.Path.Length == 0 ? CookieContainerCookieJar.DefaultPath(uri) : options.Path;
        if (!path.StartsWith('/') || hostPrefix && path != "/" || Encoding.UTF8.GetByteCount(path) > 1024)
        {
            Throw.TypeError(realm, "Invalid cookie path.");
        }

        DateTimeOffset? expires = options.Expires is { } millis ? Date(Math.Round(millis / 1000) * 1000) : null;
        if (options.MaxAge is { } seconds)
        {
            expires = seconds <= 0 ? DateTimeOffset.UnixEpoch
                : Date(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + seconds * 1000d);
        }
        return new SetCookie { Name = name, Value = value, Domain = domain, Path = path, Expires = expires, Secure = true };
    }

    private static DateTimeOffset Date(double millis)
        => DateTimeOffset.FromUnixTimeMilliseconds((long) Math.Clamp(Math.Round(millis), -11644473600000d, 253402300799000d));

    private static bool InvalidPair(string text)
    {
        foreach (var c in text)
        {
            if (c == ';' || c < ' ' && c != '\t' || c == '\x7f') return true;
        }
        return false;
    }

    private static bool Prefix(string value, string prefix)
        => value.Length >= prefix.Length && Ascii.EqualsIgnoreCase(value.AsSpan(0, prefix.Length), prefix);

    private static bool HasPrefix(string value) => Prefix(value, "__host-") || Prefix(value, "__http-") || Prefix(value, "__secure-");

    /// <summary>https://cookiestore.spec.whatwg.org/#create-a-cookielistitem - only name and value.</summary>
    internal static JsObject Item(Engine engine, Realm realm, string? name, string? value)
    {
        var item = new JsObject(engine) { Prototype = realm.Intrinsics.Object.PrototypeObject };
        if (name is not null) item.CreateDataProperty("name", JsString.Create(name));
        if (value is not null) item.CreateDataProperty("value", JsString.Create(value));
        return item;
    }

    /// <summary>https://webidl.spec.whatwg.org/#es-sequence with per-element dictionary conversion.</summary>
    internal static JsArray Sequence(Engine engine, Realm realm, JsValue value)
    {
        var items = new List<JsValue>();
        if (!value.IsUndefined())
        {
            if (value is not ObjectInstance) Throw.TypeError(realm, "CookieList must be an iterable object.");
            var iterator = value.GetIterator(realm);
            while (iterator.TryIteratorStepValue(out var entry))
            {
                engine.Constraints.Check();
                try
                {
                    var dictionary = Dictionary(realm, entry);
                    var name = Text(dictionary, "name");
                    var contents = Text(dictionary, "value");
                    items.Add(Item(engine, realm, name, contents));
                }
                catch (Exception exception) when (exception is JavaScriptException or TypeErrorException)
                {
                    iterator.Close(CompletionType.Throw);
                    throw;
                }
            }
        }
        var array = realm.Intrinsics.Array.ConstructFast(items.ToArray());
        array.SetIntegrityLevel(ObjectInstance.IntegrityLevel.Frozen);
        return array;
    }
}

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct CookieOptions(string Name, string Value, string? Domain = null, string Path = "/",
    double? Expires = null, long? MaxAge = null, string SameSite = "strict", bool Partitioned = false);
