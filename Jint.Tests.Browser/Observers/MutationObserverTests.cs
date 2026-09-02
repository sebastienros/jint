using Jint.Browser;

namespace Jint.Tests.Browser.Observers;

// The test namespace sits under Jint.Tests.Browser, so the bare name Browser binds to that namespace rather
// than to the type. The alias belongs inside the namespace declaration, where it wins that lookup.
using Browser = global::Jint.Browser.Browser;

/// <summary>
/// <c>MutationObserver</c>: the records AngleSharp produces, delivered at Jint's microtask checkpoint.
/// </summary>
public sealed class MutationObserverTests
{
    private static async Task<Page> PageWith(Browser browser, string body)
    {
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(body);
        return page;
    }

    [Test]
    public async Task AChildListMutationIsReportedWithItsAddedNodes()
    {
        await using var browser = new Browser();
        var page = await PageWith(browser,
            """
            <div id="host"></div>
            <script>
              window.log = [];
              const host = document.getElementById('host');
              new MutationObserver(records => {
                for (const r of records) {
                  window.log.push(r.type + ':' + r.addedNodes.length + ':' + r.addedNodes[0].nodeName + ':' + (r.target === host));
                }
              }).observe(host, { childList: true });
              host.appendChild(document.createElement('p'));
            </script>
            """);

        (await page.EvaluateAsync<string>("window.log.join('|')")).Should().Be("childList:1:P:true");
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task ARemovalReportsRemovedNodesAndTheSiblingsItSatBetween()
    {
        await using var browser = new Browser();
        var page = await PageWith(browser,
            """
            <div id="host"><a></a><b></b><i></i></div>
            <script>
              window.log = [];
              const host = document.getElementById('host');
              new MutationObserver(records => {
                const r = records[0];
                window.log.push(r.removedNodes.length, r.removedNodes[0].nodeName, r.previousSibling.nodeName, r.nextSibling.nodeName, r.addedNodes.length);
              }).observe(host, { childList: true });
              host.removeChild(host.querySelector('b'));
            </script>
            """);

        // addedNodes is a NodeList even for a removal: DOM declares both as non-nullable.
        (await page.EvaluateAsync<string>("window.log.join('|')")).Should().Be("1|B|A|I|0");
    }

    [Test]
    public async Task AnAttributeChangeCarriesTheNameAndTheOldValueWhenItWasAskedFor()
    {
        await using var browser = new Browser();
        var page = await PageWith(browser,
            """
            <div id="host" data-x="one"></div>
            <script>
              window.log = [];
              const host = document.getElementById('host');
              new MutationObserver(records => {
                for (const r of records) {
                  window.log.push(r.type + ':' + r.attributeName + ':' + r.oldValue + ':' + r.attributeNamespace);
                }
              }).observe(host, { attributes: true, attributeOldValue: true });
              host.setAttribute('data-x', 'two');
            </script>
            """);

        (await page.EvaluateAsync<string>("window.log.join('|')")).Should().Be("attributes:data-x:one:null");
    }

    [Test]
    public async Task AnAttributeChangeHasANullOldValueWhenItWasNotAskedFor()
    {
        await using var browser = new Browser();
        var page = await PageWith(browser,
            """
            <div id="host" data-x="one"></div>
            <script>
              window.log = [];
              const host = document.getElementById('host');
              new MutationObserver(records => { window.log.push(String(records[0].oldValue)) }).observe(host, { attributes: true });
              host.setAttribute('data-x', 'two');
            </script>
            """);

        (await page.EvaluateAsync<string>("window.log.join('|')")).Should().Be("null");
    }

    [Test]
    public async Task AnAttributeFilterKeepsTheAttributesItNames()
    {
        await using var browser = new Browser();
        var page = await PageWith(browser,
            """
            <div id="host"></div>
            <script>
              window.log = [];
              const host = document.getElementById('host');
              new MutationObserver(records => {
                for (const r of records) { window.log.push(r.attributeName) }
              }).observe(host, { attributes: true, attributeFilter: ['data-keep'] });
              host.setAttribute('data-drop', '1');
              host.setAttribute('data-keep', '1');
              host.setAttribute('data-drop', '2');
            </script>
            """);

        (await page.EvaluateAsync<string>("window.log.join('|')")).Should().Be("data-keep");
    }

    [Test]
    public async Task CharacterDataOnATextNodeIsReportedWithItsOldValue()
    {
        await using var browser = new Browser();
        var page = await PageWith(browser,
            """
            <div id="host">before</div>
            <script>
              window.log = [];
              const text = document.getElementById('host').firstChild;
              new MutationObserver(records => {
                const r = records[0];
                window.log.push(r.type, r.oldValue, r.target.nodeName, r.target.data);
              }).observe(text, { characterData: true, characterDataOldValue: true });
              text.data = 'after';
            </script>
            """);

        (await page.EvaluateAsync<string>("window.log.join('|')")).Should().Be("characterData|before|#text|after");
    }

    [Test]
    public async Task SubtreeReachesADescendantAndWithoutItNothingIsReported()
    {
        await using var browser = new Browser();
        var page = await PageWith(browser,
            """
            <div id="outer"><div id="inner"></div></div>
            <script>
              window.deep = 0;
              window.shallow = 0;
              const outer = document.getElementById('outer');
              const inner = document.getElementById('inner');
              new MutationObserver(rs => { window.deep += rs.length }).observe(outer, { childList: true, subtree: true });
              new MutationObserver(rs => { window.shallow += rs.length }).observe(outer, { childList: true });
              inner.appendChild(document.createElement('p'));
            </script>
            """);

        (await page.EvaluateAsync<int>("window.deep")).Should().Be(1);
        (await page.EvaluateAsync<int>("window.shallow")).Should().Be(0);
    }

    [Test]
    public async Task ObservingTheSameNodeTwiceReplacesTheOptions()
    {
        await using var browser = new Browser();
        var page = await PageWith(browser,
            """
            <div id="host"></div>
            <script>
              window.log = [];
              const host = document.getElementById('host');
              const observer = new MutationObserver(records => {
                for (const r of records) { window.log.push(r.type) }
              });
              observer.observe(host, { childList: true });
              observer.observe(host, { attributes: true });
              host.appendChild(document.createElement('p'));
              host.setAttribute('data-x', '1');
            </script>
            """);

        // One registration per (observer, node): the second observe() replaced the first's options rather
        // than adding a second registration, so the childList mutation is not reported at all.
        (await page.EvaluateAsync<string>("window.log.join('|')")).Should().Be("attributes");
    }

    [Test]
    public async Task TwoMutationsInOneTurnAreOneCallbackWithTwoRecords()
    {
        await using var browser = new Browser();
        var page = await PageWith(browser,
            """
            <div id="host"></div>
            <script>
              window.log = [];
              const host = document.getElementById('host');
              new MutationObserver(records => { window.log.push('mo:' + records.length) }).observe(host, { childList: true, attributes: true });
              host.appendChild(document.createElement('p'));
              host.setAttribute('data-x', '1');
              window.log.push('sync');
            </script>
            """);

        (await page.EvaluateAsync<string>("window.log.join('|')")).Should().Be("sync|mo:2");
    }

    [Test]
    public async Task DeliveryIsAMicrotaskInTheOrderTheCheckpointGivesIt()
    {
        await using var browser = new Browser();
        var page = await PageWith(browser,
            """
            <div id="host"></div>
            <script>
              window.log = [];
              const host = document.getElementById('host');
              new MutationObserver(records => { window.log.push('mo:' + records.length) }).observe(host, { childList: true });

              Promise.resolve().then(() => window.log.push('before'));
              host.appendChild(document.createElement('p'));
              host.appendChild(document.createElement('p'));
              Promise.resolve().then(() => window.log.push('after'));

              window.log.push('sync');
            </script>
            """);

        // "Queue a mutation observer microtask" runs at the first mutation of the batch, so the notify job
        // sits between the two promise reactions: a `then` registered before the mutations runs first, the
        // observer's callback next with both records, and a `then` registered after the mutations last.
        (await page.EvaluateAsync<string>("window.log.join('|')")).Should().Be("sync|before|mo:2|after");
    }

    [Test]
    public async Task TakeRecordsEmptiesTheQueueAndStopsTheDelivery()
    {
        await using var browser = new Browser();
        var page = await PageWith(browser,
            """
            <div id="host"></div>
            <script>
              window.log = [];
              const host = document.getElementById('host');
              const observer = new MutationObserver(records => { window.log.push('mo:' + records.length) });
              observer.observe(host, { childList: true });
              host.appendChild(document.createElement('p'));
              window.taken = observer.takeRecords().length;
              window.emptied = observer.takeRecords().length;
            </script>
            """);

        (await page.EvaluateAsync<int>("window.taken")).Should().Be(1);
        (await page.EvaluateAsync<int>("window.emptied")).Should().Be(0);
        (await page.EvaluateAsync<string>("window.log.join('|')")).Should().BeEmpty();
    }

    [Test]
    public async Task DisconnectStopsReportingAndDropsWhatWasQueued()
    {
        await using var browser = new Browser();
        var page = await PageWith(browser,
            """
            <div id="host"></div>
            <script>
              window.log = [];
              const host = document.getElementById('host');
              const observer = new MutationObserver(records => { window.log.push('mo:' + records.length) });
              observer.observe(host, { childList: true });
              host.appendChild(document.createElement('p'));
              observer.disconnect();
              host.appendChild(document.createElement('p'));
            </script>
            """);

        (await page.WaitForIdleAsync(TimeSpan.FromSeconds(5))).Should().BeTrue();
        (await page.EvaluateAsync<string>("window.log.join('|')")).Should().BeEmpty();
    }

    [Test]
    public async Task ObservingAgainAfterADisconnectWatchesOnlyTheNewNode()
    {
        await using var browser = new Browser();
        var page = await PageWith(browser,
            """
            <div id="one"></div><div id="two"></div>
            <script>
              window.log = [];
              const one = document.getElementById('one');
              const two = document.getElementById('two');
              const observer = new MutationObserver(records => {
                for (const r of records) { window.log.push(r.target.id) }
              });
              observer.observe(one, { childList: true });
              observer.disconnect();
              observer.observe(two, { childList: true });
              one.appendChild(document.createElement('p'));
              two.appendChild(document.createElement('p'));
            </script>
            """);

        // AngleSharp's own MutationObserver.Disconnect leaves its registration list populated, so a later
        // observe() would resurrect the first node; the wrapper throws the AngleSharp observer away instead.
        (await page.EvaluateAsync<string>("window.log.join('|')")).Should().Be("two");
    }

    [Test]
    public async Task InvalidOptionsAreATypeError()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        var cases = new[]
        {
            "{}",
            "{ childList: false }",
            "{ attributes: false, attributeOldValue: true }",
            "{ attributes: false, attributeFilter: ['x'] }",
            "{ characterData: false, characterDataOldValue: true }",
        };

        foreach (var options in cases)
        {
            var thrown = await page.EvaluateAsync<string>(
                "(() => { try { new MutationObserver(() => {}).observe(document.body, " + options + "); return 'no throw' } catch (e) { return e.constructor.name } })()");

            thrown.Should().Be("TypeError", "observe({0}) is a TypeError", options);
        }

        // But an old-value option on its own turns its flag on rather than failing, which is what the
        // dictionary's "exists" steps say.
        (await page.EvaluateAsync<string>(
            "(() => { try { new MutationObserver(() => {}).observe(document.body, { attributeOldValue: false }); return 'ok' } catch (e) { return e.name } })()"))
            .Should().Be("ok");
    }

    [Test]
    public async Task ARecordIsAMutationRecordAndTheObserverIsAMutationObserver()
    {
        await using var browser = new Browser();
        var page = await PageWith(browser,
            """
            <div id="host"></div>
            <script>
              window.log = [];
              const host = document.getElementById('host');
              const observer = new MutationObserver(function (records, self) {
                window.log.push(
                  records[0] instanceof MutationRecord,
                  self === observer,
                  this === observer,
                  Array.isArray(records),
                  records[0].addedNodes instanceof NodeList,
                  Object.prototype.toString.call(records[0]) === '[object MutationRecord]');
              });
              observer.observe(host, { childList: true });
              host.appendChild(document.createElement('p'));
              window.isObserver = observer instanceof MutationObserver;
            </script>
            """);

        (await page.EvaluateAsync<bool>("window.isObserver")).Should().BeTrue();
        (await page.EvaluateAsync<string>("window.log.join('|')")).Should().Be("true|true|true|true|true|true");
    }

    [Test]
    public async Task ACallbackThatThrowsIsReportedAndTheOtherObserversStillRun()
    {
        await using var browser = new Browser();
        var page = await PageWith(browser,
            """
            <div id="host"></div>
            <script>
              window.ran = 0;
              const host = document.getElementById('host');
              new MutationObserver(() => { throw new Error('from an observer') }).observe(host, { childList: true });
              new MutationObserver(() => { window.ran++ }).observe(host, { childList: true });
              host.appendChild(document.createElement('p'));
            </script>
            """);

        (await page.EvaluateAsync<int>("window.ran")).Should().Be(1);
        page.Errors.Should().ContainSingle();
    }

    [Test]
    public async Task ObservingANonNodeIsATypeError()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        (await page.EvaluateAsync<string>(
            "(() => { try { new MutationObserver(() => {}).observe({}, { childList: true }); return 'no throw' } catch (e) { return e.constructor.name } })()"))
            .Should().Be("TypeError");

        (await page.EvaluateAsync<string>(
            "(() => { try { new MutationObserver(); return 'no throw' } catch (e) { return e.constructor.name } })()"))
            .Should().Be("TypeError");
    }
}
