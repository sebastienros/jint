using Jint.Browser.DevTools;
using Jint.DevTools;
using Jint.DevTools.Domains;
using Jint.DevTools.Transport;

namespace Jint.Tests.Browser.DevTools;

/// <summary>
/// What an attachment to a page target registers, and what it gives back again.
/// </summary>
public class PageTargetDomainTests
{
    /// <summary>
    /// Every page-level domain that listens to its target is observing it after registration, and none of
    /// them is after the attachment detaches.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The two halves used to be written in different places. <c>TargetDomains.Detach</c> walks the extra
    /// domains and unobserves every <c>ITargetObserver</c> among them without being told which, while
    /// registration observed nothing — so <c>PageTarget.RegisterDomains</c> called <c>Observe</c> by hand for
    /// the one domain that needed it. A second one added without that line is released without ever having
    /// been registered, and what it silently stops hearing is the engine being replaced under it, which is
    /// the one thing a page-level domain most needs to know.
    /// </para>
    /// <para>
    /// The set is taken from the registration rather than named here, so a page-level domain that starts
    /// listening is covered the day it does.
    /// </para>
    /// </remarks>
    [Test]
    public async Task EveryExtraDomainThatListensIsObservedByRegistrationAndReleasedByDetach()
    {
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        var target = await PageTarget.CreateAsync(page, browserContextId: null);

        var server = new DevToolsServer();
        server.AddTarget(target);

        var client = server.OpenBrowserSession(new InProcessConnection());
        var attachment = client.Session.CreateChild("S1");

        var domains = target.RegisterDomains(attachment, client);

        var listeners = domains.Extra!.OfType<ITargetObserver>().ToArray();
        listeners.Should().NotBeEmpty("at least one page-level domain hears about the engine being replaced");

        foreach (var listener in listeners)
        {
            target.Observers.Should().Contain(listener, "registration is what observes a domain that listens");
        }

        domains.Detach();

        foreach (var listener in listeners)
        {
            target.Observers.Should().NotContain(listener, "detaching gives back exactly what registering took");
        }
    }
}
