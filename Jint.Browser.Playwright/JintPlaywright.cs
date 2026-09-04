using Jint.Browser;
using Microsoft.Playwright;

namespace Jint.Browser.Playwright;

/// <summary>Creates Microsoft.Playwright browser interfaces backed directly by Jint.Browser.</summary>
public static class JintPlaywright
{
    /// <summary>Gets the default Jint browser type.</summary>
    public static IBrowserType BrowserType { get; } = CreateBrowserType();

    /// <summary>Creates a Jint browser type with browser-wide configuration.</summary>
    /// <param name="configure">The configuration applied to each launched browser.</param>
    public static IBrowserType CreateBrowserType(Action<BrowserOptions>? configure = null)
        => ProxyFactory.Create<IBrowserType>(new BrowserTypeTarget(configure));
}
