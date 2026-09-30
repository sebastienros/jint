namespace Jint.Browser.Runtime;

/// <summary>
/// The User-Agent Client Hints a page reads from <c>navigator.userAgentData</c>: the low-entropy three every
/// page gets and the high-entropy rest <c>getHighEntropyValues</c> hands over.
/// </summary>
/// <remarks>
/// <para>
/// https://wicg.github.io/ua-client-hints/#interface — the values are the ones a client supplied as
/// <c>userAgentMetadata</c> with <c>Emulation.setUserAgentOverride</c> or <c>Network.setUserAgentOverride</c>,
/// the protocol's own shape for them, or else the ones <see cref="For"/> derives.
/// </para>
/// <para>
/// <b>Without an override the browser names itself</b>, the same honesty <see cref="BrowserOptions.UserAgent"/>
/// keeps: one brand, <c>Jint.Browser</c> at the assembly's version, and an empty <c>platform</c> because
/// <c>navigator.platform</c> is empty too and the two must not disagree. <b>An override without metadata
/// empties the brands</b>, which is what Chrome does: a user agent a client replaced is no longer the one the
/// brands described.
/// </para>
/// </remarks>
internal sealed record UserAgentClientHints(
    IReadOnlyList<(string Brand, string Version)> Brands,
    IReadOnlyList<(string Brand, string Version)> FullVersionList,
    string Platform,
    string PlatformVersion,
    string Architecture,
    string Model,
    bool Mobile,
    string Bitness,
    bool Wow64,
    IReadOnlyList<string> FormFactors)
{
    private static readonly Version _version = typeof(BrowserOptions).Assembly.GetName().Version ?? new Version(0, 0, 0);

    /// <summary>What a page sees when nobody overrode the user agent.</summary>
    internal static UserAgentClientHints Default { get; } = new(
        Brands: [("Jint.Browser", _version.Major.ToString(System.Globalization.CultureInfo.InvariantCulture))],
        FullVersionList: [("Jint.Browser", _version.ToString(3))],
        Platform: "",
        PlatformVersion: "",
        Architecture: "",
        Model: "",
        Mobile: false,
        Bitness: "",
        Wow64: false,
        FormFactors: [])
    {
        FullVersion = _version.ToString(3),
    };

    /// <summary>What a page sees when a client replaced the user agent and described nothing about it.</summary>
    internal static UserAgentClientHints Empty { get; } = new(
        Brands: [],
        FullVersionList: [],
        Platform: "",
        PlatformVersion: "",
        Architecture: "",
        Model: "",
        Mobile: false,
        Bitness: "",
        Wow64: false,
        FormFactors: []);

    /// <summary>The hints a page reads given the emulation state it is in.</summary>
    /// <remarks>
    /// Any user agent other than Jint.Browser's own — a <see cref="BrowserOptions.UserAgent"/> or a protocol
    /// override without metadata — answers <see cref="Empty"/>: brands naming Jint.Browser beside a string
    /// claiming to be something else would be the contradiction a fingerprinting script looks for.
    /// </remarks>
    internal static UserAgentClientHints For(EmulationState emulation)
        => emulation.UserAgentMetadata
            ?? (string.Equals(emulation.EffectiveUserAgent, BrowserOptions.DefaultUserAgent, StringComparison.Ordinal) ? Default : Empty);

    /// <summary>
    /// <c>uaFullVersion</c>, deprecated but still requested: the full version of the first brand, which is the
    /// one Chrome derives it from when a client did not name it.
    /// </summary>
    internal string FullVersion { get; init; } = "";
}
