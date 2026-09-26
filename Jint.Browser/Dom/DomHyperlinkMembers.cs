using Jint.Browser.Runtime;
using Jint.HtmlParser;
using Jint.Native;
using Jint.WebApi.Url;
using Jint.WebApi.Url.Parsing;

namespace Jint.Browser.Dom;

/// <summary>HTML §4.6's hyperlink URL IDL members use the shared WHATWG URL algorithms.</summary>
internal static class DomHyperlinkMembers
{
    internal static JsValue Get(DomRealm realm, Element element, string component)
    {
        var work = new DomReadWork(realm.NativeReadCheckpoint, realm.CancellationToken);
        work.Check();
        var value = work.Attribute(element, "href");
        var url = value is null ? null : Parse(realm, element, value);
        work.Check();
        return JsString.Create(component switch
        {
            "href" => url?.Serialize() ?? value ?? "",
            "origin" => url?.SerializeOrigin() ?? "",
            "protocol" => url?.SerializeProtocol() ?? ":",
            "username" => url?.Username ?? "",
            "password" => url?.Password ?? "",
            "host" => url?.SerializeHostAndPort() ?? "",
            "hostname" => url?.SerializeHost() ?? "",
            "port" => url?.SerializePort() ?? "",
            "pathname" => url?.SerializePath() ?? "",
            "search" => url?.SerializeSearch() ?? "",
            "hash" => url?.SerializeHash() ?? "",
            _ => throw new InvalidOperationException("Unknown hyperlink component: " + component),
        });
    }

    internal static JsValue Set(DomRealm realm, Element element, string component, JsValue[] arguments)
    {
        var value = UrlValues.ToUsvString(DomConvert.At(arguments, 0));
        if (component == "href")
        {
            element.SetAttribute("href", value);
            return JsValue.Undefined;
        }
        var work = new DomReadWork(realm.NativeReadCheckpoint, realm.CancellationToken);
        work.Check();
        if (work.Attribute(element, "href") is not { } href || Parse(realm, element, href) is not { } url)
            return JsValue.Undefined;
        if (url.HasOpaquePath && component is "host" or "hostname" or "pathname") return JsValue.Undefined;
        if (url.CannotHaveCredentialsOrPort && component is "username" or "password" or "port") return JsValue.Undefined;
        switch (component)
        {
            case "protocol": UrlSetters.SetProtocol(url, value); break;
            case "username": UrlSetters.SetUsername(url, value); break;
            case "password": UrlSetters.SetPassword(url, value); break;
            case "host": UrlSetters.SetHost(url, value); break;
            case "hostname": UrlSetters.SetHostname(url, value); break;
            case "port": UrlSetters.SetPort(url, value); break;
            case "pathname": UrlSetters.SetPathname(url, value); break;
            case "search": UrlSetters.SetSearch(url, value); break;
            case "hash": UrlSetters.SetHash(url, value); break;
            default: throw new InvalidOperationException("Unknown hyperlink component: " + component);
        }
        work.Check();
        element.SetAttribute("href", url.Serialize());
        return JsValue.Undefined;
    }

    private static UrlRecord? Parse(DomRealm realm, Element element, string value)
    {
        var baseUri = DomDocumentState.BaseUri(element.OwnerDocument!, realm.Engine.Constraints.Check, realm.CancellationToken);
        var url = PageUrl.Parse(value, baseUri);
        realm.Engine.Constraints.Check();
        return url;
    }
}
