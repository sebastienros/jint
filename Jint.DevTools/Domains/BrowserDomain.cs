using System.Reflection;
using Jint.DevTools.Protocol;
using Jint.DevTools.Protocol.Browser;
using Jint.DevTools.Session;

namespace Jint.DevTools.Domains;

/// <summary>
/// Answers what a client asks about the browser itself: its version, its window, and its shutdown.
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
/// <b>There is no window, and three commands are about one.</b> Playwright asks for the window a target is
/// in, sets its bounds and sets a download behaviour, all before its first script result; refusing any of
/// them fails an ordinary connection. So one window is reported — identifier <c>1</c>, in the
/// <c>normal</c> state, sized from the target — <c>setWindowBounds</c> is accepted and moves nothing, and
/// <c>setDownloadBehavior</c> is accepted because nothing here downloads a file to a path.
/// </para>
/// <para>
/// See <see href="https://chromedevtools.github.io/devtools-protocol/tot/Browser/#method-getVersion"/> and
/// <see href="https://chromedevtools.github.io/devtools-protocol/tot/Browser/#method-close"/>.
/// </para>
/// </remarks>
internal sealed class BrowserDomain : BrowserDomainBase
{
    /// <summary>
    /// The one window there is. A client holds it and hands it back to <c>setWindowBounds</c>; nothing about
    /// it is per target, because a target has no window to be in.
    /// </summary>
    private const int WindowId = 1;

    private readonly GetVersionResponse _version;
    private readonly Action? _closeRequested;
    private readonly DevToolsServer? _server;

    /// <summary>
    /// Creates the domain over what the server calls itself, and what to run when a client asks it to close.
    /// </summary>
    /// <param name="version">What <c>getVersion</c> answers.</param>
    /// <param name="closeRequested">What to run when a client sends <c>Browser.close</c>, if anything.</param>
    /// <param name="server">
    /// The server, so that a window's bounds can be the size the target actually believes it is, or
    /// <see langword="null"/> when the domain is registered without one.
    /// </param>
    internal BrowserDomain(GetVersionResponse version, Action? closeRequested = null, DevToolsServer? server = null)
    {
        _version = version;
        _closeRequested = closeRequested;
        _server = server;
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

    /// <summary>Answers the one window, sized from whatever the target believes its viewport to be.</summary>
    /// <remarks>
    /// The size is the target's rather than a constant so that a client reading the window back after
    /// <c>Emulation.setDeviceMetricsOverride</c> is told what the page now thinks, which is the only sense in
    /// which a window exists here at all.
    /// </remarks>
    protected override ValueTask<GetWindowForTargetResponse> GetWindowForTargetAsync(GetWindowForTargetRequest parameters, CommandContext context)
    {
        var target = parameters.TargetId is { } targetId ? _server?.FindTarget(targetId) : null;
        var (width, height) = target?.WindowSize ?? DevToolsTarget.DefaultWindowSize;

        return new ValueTask<GetWindowForTargetResponse>(new GetWindowForTargetResponse
        {
            WindowId = WindowId,
            Bounds = new Bounds { Left = 0, Top = 0, Width = width, Height = height, WindowState = WindowStateValues.Normal },
        });
    }

    /// <summary>Answers success and moves nothing, because there is no window to move.</summary>
    /// <remarks>
    /// A client sets bounds to give a page a size; the command that gives a page a size here is
    /// <c>Emulation.setDeviceMetricsOverride</c>, and every client that sends this sends that too.
    /// </remarks>
    protected override ValueTask<EmptyResult> SetWindowBoundsAsync(SetWindowBoundsRequest parameters, CommandContext context)
    {
        return new ValueTask<EmptyResult>(EmptyResult.Instance);
    }

    /// <summary>Answers success and downloads nothing to a path.</summary>
    /// <remarks>
    /// A navigation that would download rather than render is a failure here — the page has no document to
    /// show — so there is no file for a behaviour to govern. Playwright sends this while connecting and on
    /// every context it creates, and reads a failure as a broken browser.
    /// </remarks>
    protected override ValueTask<EmptyResult> SetDownloadBehaviorAsync(SetDownloadBehaviorRequest parameters, CommandContext context)
    {
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
