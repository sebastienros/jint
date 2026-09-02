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
/// <c>getVersion</c> is the first command most clients send, and what it answers is what the client then
/// believes it is driving. So the product string says Jint and its version rather than impersonating a
/// Chrome build: a client that branches on the product name should take its "unknown browser" path, and one
/// that only reports it should report the truth.
/// </para>
/// <para>
/// <c>close</c> is the host's decision, not this package's. Every client sends it on the way out and a
/// browser would exit; Jint is embedded in somebody's process, so what the server does with it is
/// <see cref="DevToolsServerOptions.CloseIsDisconnect"/>. Without a callback at all it succeeds and does
/// nothing, which is what keeps a client that closes on the way out from failing.
/// </para>
/// <para>
/// See <see href="https://chromedevtools.github.io/devtools-protocol/tot/Browser/#method-getVersion"/> and
/// <see href="https://chromedevtools.github.io/devtools-protocol/tot/Browser/#method-close"/>.
/// </para>
/// </remarks>
internal sealed class BrowserDomain : BrowserDomainBase
{
    private readonly GetVersionResponse _version;
    private readonly Action? _closeRequested;

    /// <summary>
    /// Creates the domain over what the server calls itself, and what to run when a client asks it to close.
    /// </summary>
    internal BrowserDomain(GetVersionResponse version, Action? closeRequested = null)
    {
        _version = version;
        _closeRequested = closeRequested;
    }

    /// <summary>Jint's own version, with the source-link commit suffix removed because a client renders it.</summary>
    internal static string JintVersion { get; } = ReadJintVersion();

    /// <summary>Builds the answer to <c>getVersion</c>, taking the product string a host chose.</summary>
    /// <param name="product">What the server calls itself, or <see langword="null"/> for Jint and its version.</param>
    internal static GetVersionResponse Version(string? product)
    {
        var name = string.IsNullOrEmpty(product) ? "Jint/" + JintVersion : product;

        return new GetVersionResponse
        {
            ProtocolVersion = ProtocolManifest.ProtocolVersion,
            Product = name,
            Revision = "",
            UserAgent = name,

            // V8 reports its own engine version here. Jint's is the honest answer, and it parses as a
            // version for the clients that read it as one.
            JsVersion = JintVersion,
        };
    }

    /// <inheritdoc/>
    protected override ValueTask<GetVersionResponse> GetVersionAsync(EmptyParameters parameters, CommandContext context)
    {
        return new ValueTask<GetVersionResponse>(_version);
    }

    /// <inheritdoc/>
    protected override ValueTask<EmptyResult> CloseAsync(EmptyParameters parameters, CommandContext context)
    {
        _closeRequested?.Invoke();
        return new ValueTask<EmptyResult>(EmptyResult.Instance);
    }

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
