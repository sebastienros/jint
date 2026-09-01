using System.Reflection;
using Jint.DevTools.Protocol;
using Jint.DevTools.Protocol.Browser;
using Jint.DevTools.Session;

namespace Jint.DevTools.Domains;

/// <summary>
/// Answers <c>Browser.getVersion</c> and <c>Browser.close</c>.
/// </summary>
/// <remarks>
/// <para>
/// <c>getVersion</c> is the first command every client sends, and what it answers is what the client then
/// believes it is driving. So the product string says Jint and its version rather than impersonating a
/// Chrome build: a client that branches on the product name should take its "unknown browser" path, and one
/// that only reports it should report the truth.
/// </para>
/// <para>
/// <c>close</c> is the host's decision, not this package's. Without a callback it succeeds and does nothing,
/// which is what keeps a client that closes on the way out from failing.
/// </para>
/// <para>
/// See <see href="https://chromedevtools.github.io/devtools-protocol/tot/Browser/#method-getVersion"/> and
/// <see href="https://chromedevtools.github.io/devtools-protocol/tot/Browser/#method-close"/>.
/// </para>
/// </remarks>
internal sealed class BrowserDomain : BrowserDomainBase
{
    private static readonly string _version = ReadJintVersion();

    private static readonly GetVersionResponse _versionResponse = new()
    {
        ProtocolVersion = ProtocolManifest.ProtocolVersion,
        Product = "Jint/" + _version,
        Revision = "",
        UserAgent = "Jint/" + _version,

        // V8 reports its own engine version here. Jint's is the honest answer, and it parses as a version
        // for the clients that read it as one.
        JsVersion = _version,
    };

    private readonly Action? _closeRequested;

    /// <summary>
    /// Creates the domain, optionally with what to run when a client asks the browser to close.
    /// </summary>
    internal BrowserDomain(Action? closeRequested = null)
    {
        _closeRequested = closeRequested;
    }

    /// <inheritdoc/>
    protected override ValueTask<GetVersionResponse> GetVersionAsync(EmptyParameters parameters, CommandContext context)
    {
        return new ValueTask<GetVersionResponse>(_versionResponse);
    }

    /// <inheritdoc/>
    protected override ValueTask<EmptyResult> CloseAsync(EmptyParameters parameters, CommandContext context)
    {
        _closeRequested?.Invoke();
        return new ValueTask<EmptyResult>(EmptyResult.Instance);
    }

    /// <summary>
    /// Jint's informational version with the source-link commit suffix removed, because a client renders
    /// this string.
    /// </summary>
    private static string ReadJintVersion()
    {
        var assembly = typeof(Engine).Assembly;
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        if (string.IsNullOrEmpty(informational))
        {
            return assembly.GetName().Version?.ToString() ?? "0.0.0";
        }

        var suffix = informational.IndexOf('+', StringComparison.Ordinal);
        return suffix < 0 ? informational : informational.Substring(0, suffix);
    }
}
