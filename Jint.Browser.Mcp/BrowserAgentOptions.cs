namespace Jint.Browser.Mcp;

/// <summary>
/// What every page an agent drives through this server is built from.
/// </summary>
/// <remarks>
/// <para>
/// <b>The defaults are the hardened ones, and that is the difference from <see cref="BrowserOptions"/>.</b>
/// A host embedding a browser knows what it is pointing at; a Model Context Protocol client is by definition
/// driving content nobody vouches for — a page a model chose, from a search result, from a link in an email
/// — so <see cref="Trusted"/> is off, which turns on <c>BrowserOptions.ForUntrustedContent()</c> and with it
/// the private-network block. Turning it on is a decision a deployment makes once, out loud.
/// </para>
/// <para>
/// Everything here is applied when the server's one <see cref="Browser"/> is built; a session gets a context
/// of that browser rather than a browser of its own, which is what makes two sessions two visitors and one
/// process.
/// </para>
/// </remarks>
public sealed class BrowserAgentOptions
{
    /// <summary>Whether pages run without the hardened profile; <see langword="false"/>.</summary>
    /// <remarks>
    /// <see langword="false"/> — the default — applies <c>BrowserOptions.ForUntrustedContent()</c>: no
    /// <c>eval</c>, no <c>new Function</c>, no CLR interop, no module loader, bounded statements, time,
    /// allocation, recursion and regular expressions, and loopback and private addresses refused.
    /// </remarks>
    public bool Trusted { get; set; }

    /// <summary>Whether loopback and private addresses are reachable; <see langword="null"/> for the posture's answer.</summary>
    /// <remarks>
    /// Assigning it is what a deployment pointing an agent at its own staging server does, and it survives
    /// <see cref="Trusted"/> being <see langword="false"/> — which is the only reason it is separate.
    /// </remarks>
    public bool? BlockPrivateNetwork { get; set; }

    /// <summary>What a page reports itself as, or <see langword="null"/> for the package's own string.</summary>
    public string? UserAgent { get; set; }

    /// <summary>The ceiling on one turn of a page; <see langword="null"/> for the profile's own.</summary>
    public TimeSpan? MaxTaskDuration { get; set; }

    /// <summary>The allocation budget for one turn of a page; <see langword="null"/> for the profile's own.</summary>
    public long? MemoryLimit { get; set; }

    /// <summary>The ceiling on a navigation and on a wait; thirty seconds.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// The greatest number of characters a snapshot returns before it is cut; 40,000.
    /// </summary>
    /// <remarks>
    /// A ceiling rather than a suggestion: a snapshot is read by a model with a context window, and a page
    /// that answered a megabyte of markdown would fill it. A tool's own <c>maxLength</c> narrows this and
    /// never widens it.
    /// </remarks>
    public int MaxSnapshotLength { get; set; } = 40_000;

    /// <summary>The last word on whether any load may be made, or <see langword="null"/> for none.</summary>
    /// <remarks>
    /// It is the same filter <c>BrowserContextOptions.UrlFilter</c> takes — run on the first hop and on every
    /// redirect, thread-safe, non-blocking — and it is how a deployment pins an agent to one site.
    /// </remarks>
    public Func<Uri, bool>? UrlFilter { get; set; }

    /// <summary>
    /// The one directory the <c>upload</c> tool may read files from, or <see langword="null"/> — the
    /// default — for none, which refuses every upload.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Off by default because it is the one tool that can move data outwards.</b> Everything else here
    /// sends the page only what the page already had; an upload sends a file from the machine the server is
    /// running on to a site a model chose, and a model reading a page is a model that can be told what to
    /// do by one. So a deployment names the directory out loud, the way it turns
    /// <see cref="Trusted"/> on out loud, and everything outside it is refused.
    /// </para>
    /// <para>
    /// The check is on the resolved path, with a symbolic link followed to its final target first — a link
    /// inside the directory pointing outside it is exactly the shape it exists to refuse — and it is
    /// case-sensitive wherever the file system is.
    /// </para>
    /// </remarks>
    public string? UploadDirectory { get; set; }

    /// <summary>Builds what every page of the server's browser is made from.</summary>
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

        if (!Trusted)
        {
            options.ForUntrustedContent();
        }

        // After the profile, because an assignment is a choice the profile leaves alone and this is where a
        // deployment says "hardened, and it may still reach my staging server".
        if (BlockPrivateNetwork is { } blocked)
        {
            options.BlockPrivateNetwork = blocked;
        }

        return options;
    }
}
