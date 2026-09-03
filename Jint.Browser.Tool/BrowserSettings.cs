using System.Net.Http;

namespace Jint.Browser.Tool;

/// <summary>
/// The options every command shares: the security posture, the per-turn budgets and what a page says it is.
/// </summary>
/// <remarks>
/// They are read once and turned into a <see cref="BrowserOptions"/>, because that is the object a page is
/// built from and the one place the package checks a value. Nothing here invents a limit of its own.
/// </remarks>
internal sealed class BrowserSettings
{
    private BrowserSettings()
    {
    }

    /// <summary>Whether the pages run the hardened profile for content nobody vouches for.</summary>
    internal bool Untrusted { get; private init; }

    /// <summary>What a page reports itself as, or <see langword="null"/> for the package's own string.</summary>
    internal string? UserAgent { get; private init; }

    /// <summary>The ceiling on one turn, or <see langword="null"/> to leave the default alone.</summary>
    internal TimeSpan? MaxTaskDuration { get; private init; }

    /// <summary>The allocation budget for one turn, or <see langword="null"/> to leave the default alone.</summary>
    internal long? MemoryLimit { get; private init; }

    /// <summary>Whether loopback and private addresses are refused, or <see langword="null"/> for the posture's own answer.</summary>
    internal bool? BlockPrivateNetwork { get; private init; }

    /// <summary>Adds the shared options to a command's syntax.</summary>
    internal static void Declare(Dictionary<string, OptionKind> syntax)
    {
        syntax["untrusted"] = OptionKind.Flag;
        syntax["user-agent"] = OptionKind.Value;
        syntax["max-task-duration"] = OptionKind.Value;
        syntax["memory-limit"] = OptionKind.Value;
        syntax["block-private-network"] = OptionKind.Flag;
        syntax["allow-private-network"] = OptionKind.Flag;
    }

    /// <summary>Reads the shared options off a parsed command line.</summary>
    internal static BrowserSettings Read(CommandLine line)
    {
        var block = line.Flag("block-private-network");
        var allow = line.Flag("allow-private-network");

        if (block && allow)
        {
            throw new ToolUsageException("'--block-private-network' and '--allow-private-network' say opposite things; give one of them");
        }

        return new BrowserSettings
        {
            Untrusted = line.Flag("untrusted"),
            UserAgent = line.Value("user-agent"),
            MaxTaskDuration = line.Value("max-task-duration") is { } duration
                ? ValueSyntax.Duration("max-task-duration", duration)
                : null,
            MemoryLimit = line.Value("memory-limit") is { } memory
                ? ValueSyntax.Size("memory-limit", memory)
                : null,
            BlockPrivateNetwork = block ? true : allow ? false : null,
        };
    }

    /// <summary>Builds what every page of the browser is made from.</summary>
    internal BrowserOptions ToBrowserOptions()
    {
        var options = new BrowserOptions();

        if (UserAgent is not null)
        {
            options.UserAgent = UserAgent;
        }

        if (MaxTaskDuration is { } duration)
        {
            options.MaxTaskDuration = duration;
        }

        if (MemoryLimit is { } memory)
        {
            options.MemoryLimit = memory;
        }

        // Before the browser is built and after the two budgets, which is the order the package asks for:
        // the profile fills in a budget the command line left alone and keeps one it named.
        if (Untrusted)
        {
            options.ForUntrustedContent();
        }

        // Last, because the profile turns private-network blocking on and an explicit --allow-private-network
        // has to survive it. `BlockPrivateNetwork` unset is exactly "whatever the posture decided".
        if (BlockPrivateNetwork is { } blocked)
        {
            options.BlockPrivateNetwork = blocked;
        }

        return options;
    }

    /// <summary>A client that carries <paramref name="headers"/> on every request.</summary>
    /// <param name="headers">The header lines <c>--header</c> gave, already split.</param>
    /// <returns>The client to hand the context, which the caller disposes.</returns>
    /// <remarks>
    /// <para>
    /// The headers ride on an <see cref="HttpClient"/> of the tool's own rather than on a protocol command,
    /// because a <c>fetch</c> has no client attached to send one. They are defaults, so a header the page or
    /// the package sets for a request — <c>Referer</c>, <c>Cookie</c>, a <c>fetch</c>'s own — still wins for
    /// that request, which is what "extra" has to mean.
    /// </para>
    /// <para>
    /// <b>The user agent is not one of them any more.</b> The package puts the page's own user agent on
    /// every request it makes ([#3720](https://github.com/sebastienros/jint/issues/3720)), which is a header
    /// on the request itself and therefore beats any default this client could carry — so a
    /// <c>--header 'User-Agent: …'</c> is turned into <see cref="UserAgent"/> by
    /// <see cref="UserAgentFrom"/> and dropped here, and what the page reports and what it sends stay one
    /// string.
    /// </para>
    /// </remarks>
    internal static HttpClient CreateRequestClient(IReadOnlyList<(string Name, string Value)> headers)
    {
        // Redirects stay off and so do the handler's own cookies: the redirect loop and the jar are the
        // package's, so that every hop is re-checked against the URL filter and the Cookie header recomputed.
        var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false });

        foreach (var (name, value) in headers)
        {
            if (IsUserAgent(name))
            {
                continue;
            }

            client.DefaultRequestHeaders.Remove(name);

            if (!client.DefaultRequestHeaders.TryAddWithoutValidation(name, value))
            {
                client.Dispose();
                throw new ToolUsageException($"'--header {name}: {value}' is not a header this client can send");
            }
        }

        return client;
    }

    /// <summary>
    /// The user agent a <c>--header 'User-Agent: …'</c> named, or <see langword="null"/> when none did.
    /// </summary>
    /// <remarks>
    /// The last one wins, which is what the header loop above does with every other name.
    /// </remarks>
    internal static string? UserAgentFrom(IReadOnlyList<(string Name, string Value)> headers)
    {
        string? named = null;

        foreach (var (name, value) in headers)
        {
            if (IsUserAgent(name))
            {
                named = value;
            }
        }

        return named;
    }

    private static bool IsUserAgent(string name)
        => string.Equals(name, "User-Agent", StringComparison.OrdinalIgnoreCase);
}
