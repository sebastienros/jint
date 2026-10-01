using Jint.Browser.Runtime;
using Jint.Native;
using Jint.WebApi.Url;
using Jint.WebApi.Url.Parsing;

namespace Jint.Browser.Dom;

/// <summary>The page's Location identity, backed by its actual navigation position.</summary>
internal sealed class DomLocation(PageRuntime runtime)
{
    internal string Get(string component)
    {
        if (component == "href") return runtime.DocumentUrl;
        var url = UrlParser.Parse(runtime.DocumentUrl);
        if (url is null) return "";
        return component switch
        {
            "origin" => url.SerializeOrigin(),
            "protocol" => url.SerializeProtocol(),
            "host" => url.SerializeHostAndPort(),
            "hostname" => url.SerializeHost(),
            "port" => url.SerializePort(),
            "pathname" => url.SerializePath(),
            "search" => url.SerializeSearch(),
            "hash" => url.SerializeHash(),
            "username" => url.Username,
            "password" => url.Password,
            _ => throw new InvalidOperationException("Unknown Location component: " + component),
        };
    }

    internal JsValue Set(string component, JsValue[] arguments)
    {
        var value = UrlValues.ToUsvString(DomConvert.At(arguments, 0));
        if (component == "href") runtime.Page.RequestNavigation(value, replace: false, engine: runtime.Engine);
        else if (component == "hash") LocationInstaller.WriteHash(runtime, value);
        else LocationInstaller.Write(runtime, value, component switch
        {
            "protocol" => UrlSetters.SetProtocol,
            "host" => UrlSetters.SetHost,
            "hostname" => UrlSetters.SetHostname,
            "port" => UrlSetters.SetPort,
            "pathname" => UrlSetters.SetPathname,
            "username" => UrlSetters.SetUsername,
            "password" => UrlSetters.SetPassword,
            "search" => static (url, input) => UrlSetters.SetSearch(url, input),
            _ => throw new InvalidOperationException("Unknown Location component: " + component),
        });
        return JsValue.Undefined;
    }
}
