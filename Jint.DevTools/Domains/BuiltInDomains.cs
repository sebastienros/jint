using Jint.DevTools.Session;

namespace Jint.DevTools.Domains;

/// <summary>
/// The domains this package answers, named in one place.
/// </summary>
/// <remarks>
/// This list and <c>manifest.json</c>'s <c>implementedMethods</c> are two statements of the same fact, and
/// <c>Jint.Tests.DevTools/Protocol/ProtocolManifestTests.cs</c> holds them to each other: every listed
/// method is overridden here, and nothing else is. Adding a domain without a manifest entry, or the other
/// way round, fails rather than ships.
/// </remarks>
internal static class BuiltInDomains
{
    /// <summary>Registers every domain this package answers on <paramref name="session"/>.</summary>
    /// <param name="session">The session to register on.</param>
    /// <param name="closeRequested">What to run when a client sends <c>Browser.close</c>, if anything.</param>
    internal static DevToolsSession RegisterOn(DevToolsSession session, Action? closeRequested = null)
    {
        if (session is null)
        {
            Throw.ArgumentNull(nameof(session));
        }

        return session
            .Register(new SchemaDomain())
            .Register(new BrowserDomain(closeRequested));
    }
}
