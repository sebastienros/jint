#nullable enable

namespace Jint.Tests.Browser.Navigation;

public sealed class NavigationApiTests
{
    private static async Task<LoopbackPage> LoadedAsync()
    {
        var fixture = await LoopbackPage.CreateAsync(server => server
            .MapHtml("/", "<input id='focus'><p id='target'>Navigation</p>")
            .MapHtml("/other", "<p>Other</p>"));
        await fixture.Page.NavigateAsync(fixture.Url("/"));
        return fixture;
    }

    [Test]
    public async Task InterfacesHaveWebIdlShapesAndBrands()
    {
        await using var fixture = await LoadedAsync();
        (await fixture.Page.EvaluateAsync<string>(
            """
            (() => {
              const names = ['Navigation', 'NavigationHistoryEntry', 'NavigationDestination', 'NavigationTransition', 'NavigationActivation'];
              const illegal = names.map(n => { try { new window[n](); } catch (e) { return e.name; } });
              const methods = ['entries','updateCurrentEntry','navigate','reload','traverseTo','back','forward'];
              const brands = methods.map(n => { try { Navigation.prototype[n].call({}); } catch (e) { return e.name; } });
              return [
                navigation === window.navigation, navigation instanceof EventTarget,
                Object.getPrototypeOf(NavigationHistoryEntry.prototype) === EventTarget.prototype,
                Object.getPrototypeOf(NavigateEvent.prototype) === Event.prototype,
                Object.getPrototypeOf(NavigationCurrentEntryChangeEvent.prototype) === Event.prototype,
                names.every(n => window[n].length === 0),
                illegal.every(n => n === 'TypeError'), brands.every(n => n === 'TypeError'),
                methods.map(n => navigation[n].length).join(''),
                methods.every(n => Object.getOwnPropertyDescriptor(Navigation.prototype, n).enumerable),
                Object.getOwnPropertyDescriptor(window, 'navigation').enumerable,
                Object.prototype.toString.call(navigation)
              ].join('|');
            })()
            """)).Should().Be("true|true|true|true|true|true|true|true|0110100|true|true|[object Navigation]");
    }

    [Test]
    public async Task InitialEntryAndActivationAreStable()
    {
        await using var fixture = await LoadedAsync();
        (await fixture.Page.EvaluateAsync<string>(
            """
            (() => {
              const entry = navigation.currentEntry, activation = navigation.activation;
              const a = navigation.entries(), b = navigation.entries();
              return [a !== b, a.length, a[0] === entry, b[0] === entry, entry.url === location.href,
                entry.key.length > 0, entry.id.length > 0, entry.index, entry.sameDocument,
                entry.getState() === undefined, entry instanceof EventTarget,
                activation.entry === entry, activation.from === null, activation.navigationType,
                navigation.transition === null, navigation.canGoBack, navigation.canGoForward].join('|');
            })()
            """)).Should().Be("true|1|true|true|true|true|true|0|true|true|true|true|true|replace|true|false|false");
    }

    [Test]
    public async Task HostNavigationsAreBrowserUiAndCannotBeInterceptedOrCancelled()
    {
        await using var fixture = await LoadedAsync();
        const string hijack =
            """
            window.marker = 1;
            navigation.addEventListener('navigate', e => { e.intercept(); e.preventDefault(); });
            """;

        await fixture.Page.EvaluateAsync<object>(hijack);
        await fixture.Page.ReloadAsync();
        (await fixture.Page.EvaluateAsync<string>("typeof window.marker")).Should().Be("undefined");

        await fixture.Page.EvaluateAsync<object>(hijack);
        await fixture.Page.NavigateAsync(fixture.Url("/other"));
        (await fixture.Page.EvaluateAsync<string>("[typeof window.marker, location.pathname, navigation.currentEntry.index].join('|')"))
            .Should().Be("undefined|/other|1");
    }

    [Test]
    public async Task InterceptionCommitsBeforeHandlersAndSettlesInSpecOrder()
    {
        await using var fixture = await LoadedAsync();
        (await fixture.Page.EvaluateAndAwaitAsync<string>(
            """
            (async () => {
              const log = [], from = navigation.currentEntry, info = {};
              let properties, savedTransition;
              navigation.onnavigate = e => {
                log.push('navigate');
                properties = [e.isTrusted, e.cancelable, e.navigationType, e.canIntercept, e.userInitiated,
                  e.hashChange, e.signal instanceof AbortSignal, e.formData === null,
                  e.downloadRequest === null, e.info === info, e.hasUAVisualTransition,
                  e.sourceElement === null, e.destination.sameDocument, e.destination.index,
                  e.destination.key === '', e.destination.id === '', e.destination.getState().value].join(',');
                e.intercept({handler() {
                  log.push('handler');
                  savedTransition = navigation.transition;
                  log.push(location.pathname);
                  Promise.resolve().then(() => log.push('handler-microtask'));
                }});
                Promise.resolve().then(() => log.push('listener-microtask'));
              };
              navigation.oncurrententrychange = e => log.push('change:' + e.navigationType + ':' + (e.from === from));
              navigation.onnavigatesuccess = () => log.push('success');
              const result = navigation.navigate('/spa', {state: {value: 42}, info});
              log.push('returned');
              result.committed.then(() => log.push('committed'));
              const committed = await result.committed;
              const finished = await result.finished;
              return [properties, log.join(','), committed === navigation.currentEntry,
                committed === finished, navigation.entries().length, history.length, history.state === null,
                savedTransition.from === from, savedTransition.to.url === location.href,
                await savedTransition.committed === undefined, await savedTransition.finished === undefined,
                navigation.transition === null].join('|');
            })()
            """)).Should().Be("true,true,push,true,false,false,true,true,true,true,false,true,false,-1,true,true,42|navigate,change:push:true,handler,/spa,returned,listener-microtask,handler-microtask,success,committed|true|true|2|2|true|true|true|true|true|true");
    }

    [Test]
    public async Task TransitionRemainsUntilAllHandlersComplete()
    {
        await using var fixture = await LoadedAsync();
        (await fixture.Page.EvaluateAndAwaitAsync<string>(
            """
            (async () => {
              let resolveFirst, resolveSecond, signal;
              const first = new Promise(r => resolveFirst = r), second = new Promise(r => resolveSecond = r);
              navigation.onnavigate = e => {
                signal = e.signal;
                e.intercept({handler: () => first});
                e.intercept({handler: () => second});
              };
              const result = navigation.navigate('/spa');
              const transition = navigation.transition;
              const entry = await result.committed;
              let finished = false;
              result.finished.then(() => finished = true);
              resolveFirst();
              await Promise.resolve();
              const during = !finished && navigation.transition === transition;
              resolveSecond();
              await result.finished;
              return [transition.navigationType, transition.from !== entry, during,
                navigation.transition === null, signal.aborted].join('|');
            })()
            """)).Should().Be("push|true|true|true|false");
    }

    [Test]
    public async Task RejectedHandlerErrorsOnlyFinishedAndAbortsTheSignal()
    {
        await using var fixture = await LoadedAsync();
        (await fixture.Page.EvaluateAndAwaitAsync<string>(
            """
            (async () => {
              const reason = new Error('handler failed');
              let signal, error, successes = 0;
              navigation.onnavigate = e => { signal = e.signal; e.intercept({handler: () => Promise.reject(reason)}); };
              navigation.onnavigateerror = e => error = e;
              navigation.onnavigatesuccess = () => successes++;
              const result = navigation.navigate('/spa');
              const entry = await result.committed;
              let failure;
              try { await result.finished; } catch (e) { failure = e; }
              return [entry === navigation.currentEntry, failure === reason, signal.aborted, signal.reason === reason,
                error instanceof ErrorEvent, error.error === reason, error.isTrusted, successes,
                navigation.transition === null, location.pathname].join('|');
            })()
            """)).Should().Be("true|true|true|true|true|true|true|0|true|/spa");
    }

    [Test]
    public async Task PreventDefaultRejectsBothPromisesWithoutCommitting()
    {
        await using var fixture = await LoadedAsync();
        (await fixture.Page.EvaluateAndAwaitAsync<string>(
            """
            (async () => {
              const from = navigation.currentEntry;
              let signal, errors = 0;
              navigation.onnavigate = e => { signal = e.signal; e.preventDefault(); };
              navigation.onnavigateerror = () => errors++;
              const result = navigation.navigate('/other');
              const names = await Promise.all([result.committed, result.finished].map(p => p.catch(e => e.name)));
              return [names.join(','), navigation.currentEntry === from, location.pathname, history.length,
                signal.aborted, errors].join('|');
            })()
            """)).Should().Be("AbortError,AbortError|true|/|1|true|1");
    }

    [Test]
    public async Task ReplaceRetainsKeyChangesIdAndDisposesOldEntry()
    {
        await using var fixture = await LoadedAsync();
        (await fixture.Page.EvaluateAndAwaitAsync<string>(
            """
            (async () => {
              const old = navigation.currentEntry, log = [];
              old.ondispose = e => log.push('dispose:' + e.isTrusted + ':' + old.index);
              navigation.onnavigate = e => e.intercept({handler: () => log.push('handler')});
              navigation.oncurrententrychange = () => log.push('change');
              await navigation.navigate('/replaced', {history: 'replace', state: 7}).finished;
              const entry = navigation.currentEntry;
              return [entry !== old, entry.key === old.key, entry.id !== old.id, history.length,
                entry.getState(), log.join(','), old.url.endsWith('/')].join('|');
            })()
            """)).Should().Be("true|true|true|1|7|change,dispose:true:-1,handler|true");
    }

    [Test]
    public async Task PushingAfterBackDisposesPrunedForwardEntries()
    {
        await using var fixture = await LoadedAsync();
        (await fixture.Page.EvaluateAndAwaitAsync<string>(
            """
            (async () => {
              navigation.onnavigate = e => e.intercept();
              const a = await navigation.navigate('/a').finished;
              const b = await navigation.navigate('/b').finished;
              let disposed = 0;
              b.ondispose = () => disposed++;
              await navigation.back().finished;
              const back = navigation.currentEntry === a && navigation.canGoForward;
              await navigation.navigate('/c').finished;
              return [back, disposed, b.index, navigation.entries().length, history.length,
                navigation.canGoForward, navigation.canGoBack].join('|');
            })()
            """)).Should().Be("true|1|-1|3|3|false|true");
    }

    [Test]
    public async Task NavigationStateIsClonedAndIndependentOfHistoryState()
    {
        await using var fixture = await LoadedAsync();
        (await fixture.Page.EvaluateAsync<string>(
            """
            (() => {
              history.replaceState({classic: 1}, '', location.href);
              const state = {nested: {value: 2}}, entry = navigation.currentEntry;
              let event;
              navigation.oncurrententrychange = e => event = e;
              navigation.updateCurrentEntry({state});
              state.nested.value = 9;
              const a = entry.getState(), b = entry.getState();
              a.nested.value = 10;
              const errors = [];
              for (const options of [{state: () => {}}, {}, undefined]) {
                try { navigation.updateCurrentEntry(options); } catch (e) { errors.push(e.name); }
              }
              return [a !== b, b.nested.value, entry.getState().nested.value, history.state.classic,
                event.from === entry, event.navigationType === null, event.isTrusted,
                navigation.currentEntry === entry, errors.join(',')].join('|');
            })()
            """)).Should().Be("true|2|2|1|true|true|true|true|DataCloneError,TypeError,TypeError");
    }

    [Test]
    public async Task NativeHistoryAndLocationUseNavigationEvents()
    {
        await using var fixture = await LoadedAsync();
        (await fixture.Page.EvaluateAndAwaitAsync<string>(
            """
            (async () => {
              const log = [];
              navigation.onnavigate = e => log.push('navigate:' + e.navigationType + ':' + e.hashChange + ':' + e.destination.sameDocument);
              navigation.oncurrententrychange = e => log.push('change:' + e.navigationType);
              history.pushState({v: 1}, '', '/a#one');
              history.replaceState({v: 2}, '', '/a#two');
              const done = new Promise(resolve => addEventListener('hashchange', () => { log.push('hash'); resolve(); }, {once:true}));
              location.hash = 'three';
              await done;
              return [log.join(','), navigation.entries().length, navigation.currentEntry.url === location.href,
                navigation.currentEntry.getState() === undefined, history.length].join('|');
            })()
            """)).Should().Be("navigate:push:false:true,change:push,navigate:replace:false:true,change:replace,navigate:push:true:true,change:push,hash|3|true|true|3");
    }

    [Test]
    public async Task TraversalsRestoreStateAndFireNavigateBeforePopstateAndHashchange()
    {
        await using var fixture = await LoadedAsync();
        (await fixture.Page.EvaluateAndAwaitAsync<string>(
            """
            (async () => {
              history.pushState({n:1}, '', '#one');
              const one = navigation.currentEntry;
              navigation.updateCurrentEntry({state: {api: 1}});
              history.pushState({n:2}, '', '#two');
              const log = [];
              navigation.onnavigate = e => log.push('navigate:' + e.navigationType + ':' + e.cancelable + ':' + e.destination.getState()?.api);
              navigation.oncurrententrychange = () => log.push('change');
              addEventListener('popstate', e => log.push('pop:' + e.state.n));
              addEventListener('hashchange', () => log.push('hash'));
              const result = await navigation.traverseTo(one.key).finished;
              const afterBack = log.join(',');
              log.length = 0;
              await navigation.forward().finished;
              const afterForward = log.join(',');
              log.length = 0;
              await new Promise(resolve => {
                navigation.addEventListener('navigatesuccess', resolve, {once: true});
                history.back();
              });
              return [result === one, afterBack, afterForward, log.join(','), history.state.n,
                navigation.currentEntry.getState().api, navigation.canGoBack, navigation.canGoForward].join('|');
            })()
            """)).Should().Be("true|navigate:traverse:true:1,change,pop:1,hash|navigate:traverse:true:undefined,change,pop:2,hash|navigate:traverse:true:1,change,pop:1,hash|1|1|true|true");
    }

    [Test]
    public async Task SameDocumentTraversalCanBeCanceledAndCurrentKeyIsANoop()
    {
        await using var fixture = await LoadedAsync();
        (await fixture.Page.EvaluateAndAwaitAsync<string>(
            """
            (async () => {
              history.pushState({}, '', '#one');
              const entry = navigation.currentEntry;
              let events = 0;
              navigation.onnavigate = e => { events++; e.preventDefault(); };
              const same = await navigation.traverseTo(entry.key).finished;
              const result = navigation.back();
              const errors = await Promise.all([result.committed, result.finished].map(p => p.catch(e => e.name)));
              return [same === entry, navigation.currentEntry === entry, events, errors.join(',')].join('|');
            })()
            """)).Should().Be("true|true|1|AbortError,AbortError");
    }

    [Test]
    public async Task EarlyErrorsRejectBothResultPromises()
    {
        await using var fixture = await LoadedAsync();
        (await fixture.Page.EvaluateAndAwaitAsync<string>(
            """
            (async () => {
              const calls = [
                () => navigation.navigate('http://['),
                () => navigation.navigate('javascript:1'),
                () => navigation.traverseTo('missing'),
                () => navigation.back(),
                () => navigation.forward(),
                () => navigation.navigate('/x', {state: () => {}})
              ];
              const names = [];
              for (const call of calls) {
                const result = call();
                names.push((await Promise.all([result.committed, result.finished].map(p => p.catch(e => e.name)))).join(','));
              }
              return names.join('|');
            })()
            """)).Should().Be("SyntaxError,SyntaxError|NotSupportedError,NotSupportedError|InvalidStateError,InvalidStateError|InvalidStateError,InvalidStateError|InvalidStateError,InvalidStateError|DataCloneError,DataCloneError");
    }

    [Test]
    public async Task EventConstructorsEnforceRequiredMembersAndSyntheticEventsCannotIntercept()
    {
        await using var fixture = await LoadedAsync();
        (await fixture.Page.EvaluateAndAwaitAsync<string>(
            """
            (async () => {
              let destination;
              navigation.onnavigate = e => { destination = e.destination; e.intercept(); };
              await navigation.navigate('/spa').finished;
              const signal = new AbortController().signal;
              const ev = new NavigateEvent('x', {destination, signal, info: 3, canIntercept: true});
              const change = new NavigationCurrentEntryChangeEvent('x', {from: navigation.currentEntry});
              const errors = [];
              for (const make of [
                () => new NavigateEvent('x'),
                () => new NavigateEvent('x', {destination}),
                () => new NavigateEvent('x', {destination: {}, signal}),
                () => new NavigateEvent('x', {destination, signal, navigationType: 'wrong'}),
                () => new NavigationCurrentEntryChangeEvent('x'),
                () => new NavigationCurrentEntryChangeEvent('x', {from: {}}),
                () => ev.intercept(), () => ev.scroll()
              ]) { try { make(); } catch (e) { errors.push(e.name); } }
              return [NavigateEvent.length, NavigationCurrentEntryChangeEvent.length,
                ev.navigationType, ev.destination === destination, ev.signal === signal, ev.canIntercept,
                ev.info, ev.userInitiated, ev.hashChange, ev.formData === null, ev.downloadRequest === null,
                ev.sourceElement === null, ev.hasUAVisualTransition, ev.isTrusted,
                change.navigationType === null, change.from === navigation.currentEntry, errors.join(',')].join('|');
            })()
            """)).Should().Be("2|2|push|true|true|true|3|false|false|true|true|true|false|false|true|true|TypeError,TypeError,TypeError,TypeError,TypeError,TypeError,SecurityError,SecurityError");
    }

    [Test]
    public async Task CrossDocumentNavigationPreservesIdentitiesAndActivation()
    {
        await using var fixture = await LoadedAsync();
        var key = await fixture.Page.EvaluateAsync<string>("navigation.currentEntry.key");
        var id = await fixture.Page.EvaluateAsync<string>("navigation.currentEntry.id");
        await fixture.NavigateByScriptAsync(
            """
            navigation.onnavigate = e => {
              sessionStorage.setItem('event', [e.navigationType,e.canIntercept,e.destination.sameDocument,e.cancelable].join(','));
            };
            const result = navigation.navigate('/other', {state: {value: 8}});
            result.committed.then(() => sessionStorage.setItem('settled', 'committed'), () => sessionStorage.setItem('settled', 'rejected'));
            result.finished.then(() => sessionStorage.setItem('settled', 'finished'), () => sessionStorage.setItem('settled', 'rejected'));
            """);
        (await fixture.Page.EvaluateAsync<string>(
            $$"""
            [navigation.entries().length, navigation.entries()[0].key === '{{key}}',
              navigation.entries()[0].id === '{{id}}', navigation.entries()[0].sameDocument,
              navigation.currentEntry.getState().value, navigation.activation.from === navigation.entries()[0],
              navigation.activation.entry === navigation.currentEntry, navigation.activation.navigationType,
              sessionStorage.getItem('event'), sessionStorage.getItem('settled') === null,
              history.length, location.pathname].join('|')
            """)).Should().Be("2|true|true|false|8|true|true|push|push,true,false,true|true|2|/other");
        await fixture.NavigateByScriptAsync("navigation.back()");
        (await fixture.Page.EvaluateAsync<string>(
            $$"""
            [navigation.currentEntry.key === '{{key}}', navigation.currentEntry.id === '{{id}}',
              navigation.activation.navigationType, navigation.activation.from.url.endsWith('/other'),
              navigation.canGoForward, navigation.entries().length].join('|')
            """)).Should().Be("true|true|traverse|true|true|2");
    }

    [Test]
    public async Task ReloadPreservesEntryIdentityAndNavigationState()
    {
        await using var fixture = await LoadedAsync();
        var identity = await fixture.Page.EvaluateAsync<string>(
            "navigation.updateCurrentEntry({state:{value:5}}); navigation.currentEntry.key + ',' + navigation.currentEntry.id");
        await fixture.NavigateByScriptAsync("navigation.reload()");
        (await fixture.Page.EvaluateAsync<string>(
            $$"""
            [navigation.currentEntry.key + ',' + navigation.currentEntry.id === '{{identity}}',
              navigation.currentEntry.getState().value, navigation.activation.navigationType,
              navigation.entries().length, history.length].join('|')
            """)).Should().Be("true|5|reload|1|1");
        (await fixture.Page.EvaluateAndAwaitAsync<string>(
            """
            (async () => {
              const before = navigation.currentEntry;
              let details;
              navigation.onnavigate = e => {
                details = [e.navigationType, e.destination.sameDocument, e.info, e.destination.getState().value].join(',');
                e.intercept();
              };
              const result = await navigation.reload({state: {value: 9}, info: 2}).finished;
              return [result === before, before.getState().value, details, history.length].join('|');
            })()
            """)).Should().Be("true|9|reload,false,2,9|1");
    }

    [Test]
    public async Task NewNavigationAbortsAnUnfinishedIntercept()
    {
        await using var fixture = await LoadedAsync();
        (await fixture.Page.EvaluateAndAwaitAsync<string>(
            """
            (async () => {
              let firstSignal;
              navigation.onnavigate = e => {
                if (e.destination.url.endsWith('/first')) {
                  firstSignal = e.signal;
                  e.intercept({handler: () => new Promise(() => {})});
                } else e.intercept();
              };
              const first = navigation.navigate('/first');
              await first.committed;
              const rejection = first.finished.catch(e => e.name);
              const second = navigation.navigate('/second');
              await second.finished;
              return [await rejection, firstSignal.aborted, location.pathname, navigation.transition === null].join('|');
            })()
            """)).Should().Be("AbortError|true|/second|true");
    }

    [Test]
    public async Task UnsupportedPrecommitIsExplicitAndScrollCanBeControlled()
    {
        await using var fixture = await LoadedAsync();
        (await fixture.Page.EvaluateAndAwaitAsync<string>(
            """
            (async () => {
              const errors = [];
              let event;
              navigation.onnavigate = e => {
                event = e;
                try { e.intercept({precommitHandler() {}}); } catch (error) { errors.push(error.name); }
                try { e.scroll(); } catch (error) { errors.push(error.name); }
                e.intercept({scroll: 'manual', focusReset: 'manual', handler() {
                  e.scroll();
                  try { e.scroll(); } catch (error) { errors.push(error.name); }
                }});
              };
              await navigation.navigate('/spa#target').finished;
              try { event.intercept(); } catch (error) { errors.push(error.name); }
              return errors.join(',');
            })()
            """)).Should().Be("NotSupportedError,InvalidStateError,InvalidStateError,InvalidStateError");
    }

    [Test]
    public async Task PromiseAttributesRejectEveryWrongReceiver()
    {
        await using var fixture = await LoadedAsync();
        (await fixture.Page.EvaluateAndAwaitAsync<string>(
            """
            (async () => {
              const names = [];
              for (const property of ['committed','finished']) {
                const getter = Object.getOwnPropertyDescriptor(NavigationTransition.prototype, property).get;
                for (const receiver of [{}, null, undefined, 1, 'x']) {
                  const promise = getter.call(receiver);
                  names.push(promise instanceof Promise && await promise.catch(e => e.name) === 'TypeError');
                }
              }
              return names.join(',');
            })()
            """)).Should().Be("true,true,true,true,true,true,true,true,true,true");
    }

    [Test]
    public async Task AbortListenersAndErrorListenersCannotRunPromiseReactionsMidAlgorithm()
    {
        await using var fixture = await LoadedAsync();
        (await fixture.Page.EvaluateAndAwaitAsync<string>(
            """
            (async () => {
              const log = [];
              navigation.onnavigate = e => {
                e.signal.addEventListener('abort', () => {
                  log.push('abort');
                  Promise.resolve().then(() => log.push('abort-microtask'));
                });
                e.intercept({handler() { throw Error('failed'); }});
              };
              navigation.onnavigateerror = () => {
                log.push('error');
                Promise.resolve().then(() => log.push('error-microtask'));
              };
              const result = navigation.navigate('/spa');
              await result.committed;
              await result.finished.catch(() => log.push('rejected'));
              return [log.join(','), navigation.transition === null].join('|');
            })()
            """)).Should().Be("abort,error,abort-microtask,error-microtask,rejected|true");
    }

    [Test]
    public async Task LazyNavigationSeesHistoryCreatedBeforeItsFirstAccess()
    {
        await using var fixture = await LoadedAsync();
        await fixture.Page.EvaluateAsync("history.pushState({v:1}, '', '/router');");
        await fixture.NavigateByScriptAsync("location.assign('/other')");
        (await fixture.Page.EvaluateAsync<string>(
            """
            [navigation.entries().length, history.length,
              navigation.entries().map(e => e.sameDocument).join(','),
              navigation.activation.from === navigation.entries()[1],
              navigation.activation.navigationType,
              navigation.entries().map(e => e.index).join(',')].join('|')
            """)).Should().Be("3|3|false,false,true|true|push|0,1,2");
        await fixture.NavigateByScriptAsync("navigation.back()");
        (await fixture.Page.EvaluateAndAwaitAsync<string>(
            """
            (async () => {
              const before = navigation.entries().map(e => e.sameDocument).join(',');
              const entry = await navigation.back().finished;
              return [before, entry.index, location.pathname, history.state === null].join('|');
            })()
            """)).Should().Be("true,true,false|0|/|true");
    }

    [Test]
    public async Task EntriesStopAtCrossOriginBoundaries()
    {
        await using var fixture = await LoadedAsync();
        using var other = new LoopbackServer();
        other.MapHtml("/", "<p>Different origin</p>");
        await using var browser = new global::Jint.Browser.Browser();
        var context = await browser.NewContextAsync(new global::Jint.Browser.BrowserContextOptions
        {
            UrlFilter = uri => fixture.Server.Owns(uri) || other.Owns(uri),
        });
        var page = await context.NewPageAsync();
        await page.NavigateAsync(fixture.Url("/"));
        var firstKey = await page.EvaluateAsync<string>("navigation.currentEntry.key");
        await page.NavigateAsync(other.Url("/"));
        (await page.EvaluateAsync<string>(
            "[navigation.entries().length,navigation.canGoBack,navigation.activation.from === null].join('|')"))
            .Should().Be("1|false|true");
        await page.NavigateAsync(fixture.Url("/other"));
        (await page.EvaluateAndAwaitAsync<string>(
            $$"""
            (async () => {
              const result = navigation.traverseTo('{{firstKey}}');
              const errors = await Promise.all([result.committed,result.finished].map(p => p.catch(e => e.name)));
              return [history.length,navigation.entries().length,navigation.currentEntry.index,
                navigation.canGoBack,navigation.activation.from === null,errors.join(',')].join('|');
            })()
            """)).Should().Be("3|1|0|false|true|InvalidStateError,InvalidStateError");
    }

    [Test]
    public async Task CrossDocumentTraverseIsNotCancelableOrInterceptable()
    {
        await using var fixture = await LoadedAsync();
        await fixture.Page.NavigateAsync(fixture.Url("/other"));
        await fixture.NavigateByScriptAsync(
            """
            navigation.onnavigate = e => {
              let error;
              try { e.intercept(); } catch (reason) { error = reason.name; }
              e.preventDefault();
              sessionStorage.setItem('traverse', [e.navigationType,e.cancelable,e.defaultPrevented,e.canIntercept,
                e.destination.sameDocument,error].join(','));
            };
            navigation.back();
            """);
        (await fixture.Page.EvaluateAsync<string>("sessionStorage.getItem('traverse')"))
            .Should().Be("traverse,false,false,false,false,SecurityError");
    }

    [Test]
    public async Task NativeAnchorAndFormActionsSupplySourceAndPostData()
    {
        await using var fixture = await LoadedAsync();
        (await fixture.Page.EvaluateAsync<string>(
            """
            document.body.innerHTML = '<a id="link" href="/anchor" download="file.txt">Link</a>' +
              '<form action="/submit" method="post"><input name="q" value="value"><button id="submit" name="b" value="yes">Go</button></form>';
            const log = [];
            navigation.onnavigate = e => {
              log.push([e.sourceElement.id || e.sourceElement.localName, e.userInitiated, e.downloadRequest,
                e.formData instanceof FormData, e.formData?.get('q'), e.formData?.get('b')].join(','));
              e.preventDefault();
            };
            document.querySelector('a').click();
            document.querySelector('form').requestSubmit(document.querySelector('button'));
            HTMLFormElement.prototype.submit.call(document.querySelector('form'));
            log.join('|')
            """)).Should().Be("link,false,file.txt,false,,|submit,false,,true,value,yes|form,false,,true,value,");
        (await fixture.Page.ClickAsync("#link")).Should().BeTrue();
        (await fixture.Page.ClickAsync("#submit")).Should().BeTrue();
        (await fixture.Page.EvaluateAsync<string>("log.slice(-2).join('|')"))
            .Should().Be("link,true,file.txt,false,,|submit,true,,true,value,yes");
    }

    [Test]
    public async Task RefusedNetworkNavigationRejectsAndReportsNavigateerror()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server.MapHtml("/", "<p>x</p>"));
        await fixture.Page.NavigateAsync(fixture.Url("/"));
        (await fixture.Page.EvaluateAndAwaitAsync<string>(
            """
            (async () => {
              let event;
              navigation.onnavigateerror = e => event = e;
              const result = navigation.navigate('https://not-allowed.invalid/');
              const errors = await Promise.all([result.committed,result.finished].map(p => p.catch(e => e.name)));
              return [errors.join(','),event.error.name,event.isTrusted,navigation.entries().length].join('|');
            })()
            """)).Should().Be("NetworkError,NetworkError|NetworkError|true|1");
    }

    [Test]
    public async Task QueuedTraversalCallsSharePromisesAndPrunedTargetsReject()
    {
        await using var fixture = await LoadedAsync();
        (await fixture.Page.EvaluateAndAwaitAsync<string>(
            """
            (async () => {
              history.pushState({}, '', '#one');
              const first = navigation.back(), second = navigation.back();
              const shared = first.committed === second.committed && first.finished === second.finished;
              await first.finished;
              const forward = navigation.forward();
              history.pushState({}, '', '#replacement');
              const errors = await Promise.all([forward.committed,forward.finished].map(p => p.catch(e => e.name)));
              return [shared,errors.join(','),location.hash].join('|');
            })()
            """)).Should().Be("true|InvalidStateError,InvalidStateError|#replacement");
    }

    [Test]
    public async Task EntryChangeCanStartAnotherNavigationWithoutSettlingTheWrongTracker()
    {
        await using var fixture = await LoadedAsync();
        (await fixture.Page.EvaluateAndAwaitAsync<string>(
            """
            (async () => {
              navigation.onnavigate = e => e.intercept();
              let second;
              navigation.oncurrententrychange = () => {
                if (location.pathname === '/first') second = navigation.navigate('/second');
              };
              const first = navigation.navigate('/first');
              const entry = await first.committed;
              const error = await first.finished.catch(e => e.name);
              const final = await second.finished;
              return [entry.url.endsWith('/first'),error,final === navigation.currentEntry,
                location.pathname,navigation.transition === null].join('|');
            })()
            """)).Should().Be("true|AbortError|true|/second|true");
    }

    [Test]
    public async Task InitialAndOpaqueDocumentsDisableEntriesAndEvents()
    {
        await using var browser = new global::Jint.Browser.Browser();
        var page = await browser.NewPageAsync();
        const string probe =
            "[navigation.entries().length,navigation.currentEntry === null,navigation.activation === null,navigation.canGoBack,navigation.canGoForward].join('|')";
        (await page.EvaluateAsync<string>(probe)).Should().Be("0|true|true|false|false");
        await page.NavigateAsync("data:text/html,<p>Opaque</p>");
        (await page.EvaluateAsync<string>(probe)).Should().Be("0|true|true|false|false");
    }

    [Test]
    public async Task NativeFragmentCommitsSynchronouslyButQueuesHashchange()
    {
        await using var fixture = await LoadedAsync();
        (await fixture.Page.EvaluateAndAwaitAsync<string>(
            """
            (async () => {
              const log = [];
              navigation.onnavigate = e => log.push('navigate:' + (e.destination.getState() === null));
              navigation.oncurrententrychange = () => log.push('change');
              navigation.onnavigatesuccess = () => log.push('success');
              const hash = new Promise(resolve => addEventListener('hashchange', () => { log.push('hash'); resolve(); }, {once: true}));
              location.hash = 'one';
              log.push('returned:' + location.hash);
              await hash;
              return log.join(',');
            })()
            """)).Should().Be("navigate:true,change,returned:#one,success,hash");
    }

    [Test]
    public async Task NativeCrossOriginTraversalDoesNotExposeTheOtherOriginsEntryState()
    {
        await using var fixture = await LoadedAsync();
        using var other = new LoopbackServer();
        other.MapHtml("/", "<p>Other</p>");
        await using var browser = new global::Jint.Browser.Browser();
        var context = await browser.NewContextAsync(new global::Jint.Browser.BrowserContextOptions
        {
            UrlFilter = uri => fixture.Server.Owns(uri) || other.Owns(uri),
        });
        var page = await context.NewPageAsync();
        await page.NavigateAsync(fixture.Url("/"));
        await page.EvaluateAsync("navigation.updateCurrentEntry({state: {privateValue: 123}})");
        await page.NavigateAsync(other.Url("/"));
        await page.EvaluateAsync(
            """
            navigation.onnavigate = e => {
              sessionStorage.setItem('destination', [e.destination.key === '',e.destination.id === '',
                e.destination.index,e.destination.getState() === null].join('|'));
            };
            """);
        var navigated = page.WaitForNavigationAsync(TimeSpan.FromSeconds(30));
        await page.EvaluateAsync("history.back()");
        (await navigated).Should().BeTrue();
        await page.NavigateAsync(other.Url("/"));
        (await page.EvaluateAsync<string>("sessionStorage.getItem('destination')")).Should().Be("true|true|-1|true");
    }

    [Test]
    public async Task InterceptionCancelsAnInFlightHostNavigationBeforeItCanReplaceTheDocument()
    {
        using var release = new ManualResetEventSlim();
        var requested = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var fixture = await LoopbackPage.CreateAsync(server => server
            .MapHtml("/", "<p>Initial</p>")
            .Map("/slow", _ =>
            {
                requested.TrySetResult();
                release.Wait(TimeSpan.FromSeconds(30));
                return LoopbackResponse.Html("<p>Slow</p>");
            }));
        await fixture.Page.NavigateAsync(fixture.Url("/"));
        await fixture.Page.EvaluateAsync(
            """
            var seen = [];
            navigation.onnavigate = e => { seen.push(e.destination.url.split('/').pop()); e.intercept(); };
            """);
        var loading = fixture.Page.NavigateAsync(fixture.Url("/slow"));
        try
        {
            await requested.Task.WaitAsync(TimeSpan.FromSeconds(30));
            (await fixture.Page.EvaluateAndAwaitAsync<string>(
                "navigation.navigate('/spa').finished.then(() => seen.join() + '|' + location.pathname)"))
                .Should().Be("spa|/spa");
            var exception = await Caught.ExceptionAsync(() => loading.WaitAsync(TimeSpan.FromSeconds(30)));
            exception.Should().BeAssignableTo<OperationCanceledException>();
        }
        finally
        {
            release.Set();
        }
        (await fixture.Page.EvaluateAsync<string>("location.pathname")).Should().Be("/spa");
    }

    [Test]
    public async Task ChildRealmsHaveTheirOwnInterfacesButCannotNavigateTheParent()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server
            .MapHtml("/", "<iframe src='/child'></iframe>")
            .MapHtml("/child", "<p>Child</p>"));
        await fixture.Page.NavigateAsync(fixture.Url("/"));
        (await fixture.Page.EvaluateAndAwaitAsync<string>(
            """
            (async () => {
              const child = frames[0], nav = child.navigation;
              const result = nav.navigate('/other');
              const names = await Promise.all([result.committed,result.finished].map(p => p.catch(e => e.name)));
              return [nav === child.navigation,nav !== navigation,nav instanceof child.Navigation,
                child.Navigation !== Navigation,child.NavigateEvent !== NavigateEvent,
                nav.entries().length,nav.currentEntry === null,nav.activation === null,
                names.join(','),location.pathname].join('|');
            })()
            """)).Should().Be("true|true|true|true|true|0|true|true|InvalidStateError,InvalidStateError|/");
    }

    [Test]
    [NonParallelizable]
    public async Task CdpHistoryTraversalUsesTheSameNavigationEventsAndEntries()
    {
        using var server = new LoopbackServer();
        server.MapHtml("/", "<p>CDP</p>");
        await using var session = await DevTools.PageSession.CreateAsync(
            new global::Jint.Browser.BrowserContextOptions { UrlFilter = server.Owns });
        var page = await session.NewPageAsync();
        var attachment = await session.AttachAsync(await session.TargetForAsync(page));
        await session.EnablePageAsync(attachment);
        await page.NavigateAsync(server.Url("/"));
        await page.EvaluateAsync(
            """
            history.pushState({}, '', '#one');
            const log = [];
            navigation.onnavigate = e => log.push(e.navigationType);
            navigation.oncurrententrychange = () => log.push('change');
            addEventListener('popstate', () => log.push('pop'));
            """);
        var before = await session.ResultAsync("Page.getNavigationHistory", null, attachment);
        before.GetProperty("entries").GetArrayLength().Should().Be(2);
        var navigated = page.WaitForNavigationAsync(TimeSpan.FromSeconds(30));
        await session.ResultAsync("Page.navigateToHistoryEntry", """{"entryId":0}""", attachment);
        (await navigated).Should().BeTrue();
        (await page.EvaluateAsync<string>("[log.join(','),navigation.currentEntry.index,navigation.canGoForward].join('|')"))
            .Should().Be("traverse,change,pop|0|true");
        var after = await session.ResultAsync("Page.getNavigationHistory", null, attachment);
        after.GetProperty("currentIndex").GetInt32().Should().Be(0);
    }

    [Test]
    public async Task UnloadRefusesNewNavigationEvenIfTheNavigationObjectIsCreatedByPagehide()
    {
        await using var fixture = await LoadedAsync();
        await fixture.NavigateByScriptAsync(
            """
            addEventListener('pagehide', () => {
              sessionStorage.setItem('entryDuringHide', String(navigation.currentEntry !== null));
              const results = [navigation.navigate('/late'), navigation.reload(), navigation.traverseTo(navigation.currentEntry.key)];
              Promise.all(results.flatMap(r => [r.committed,r.finished]).map(p => p.catch(e => e.name)))
                .then(names => sessionStorage.setItem('unloadErrors', names.join(',')));
            });
            location.assign('/other');
            """);
        (await fixture.Page.EvaluateAsync<string>(
            "sessionStorage.getItem('entryDuringHide') + '|' + sessionStorage.getItem('unloadErrors')"))
            .Should().Be("true|InvalidStateError,InvalidStateError,InvalidStateError,InvalidStateError,InvalidStateError,InvalidStateError");
    }

    [Test]
    public async Task ReentrantEntryChangeDoesNotRepeatDisposalOrRewriteTraversalEventPayloads()
    {
        await using var fixture = await LoadedAsync();
        (await fixture.Page.EvaluateAndAwaitAsync<string>(
            """
            (async () => {
              history.pushState({v:1}, '', '#one');
              const one = navigation.currentEntry;
              history.pushState({v:2}, '', '#two');
              const log = [];
              navigation.currentEntry.ondispose = () => log.push('dispose');
              navigation.oncurrententrychange = e => {
                if (e.navigationType === 'traverse') history.pushState({v:3}, '', '#three');
              };
              addEventListener('popstate', e => log.push('pop:' + e.state.v));
              addEventListener('hashchange', e => log.push('hash:' + new URL(e.oldURL).hash + ':' + new URL(e.newURL).hash));
              const result = navigation.back();
              const entry = await result.committed;
              const error = await result.finished.catch(e => e.name);
              return [entry === one,error,log.join(','),location.hash].join('|');
            })()
            """)).Should().Be("true|AbortError|dispose,pop:1,hash:#two:#one|#three");
    }
}
