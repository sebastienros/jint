using Jint.Browser;

namespace Jint.Tests.Browser.SystemState;

using Browser = global::Jint.Browser.Browser;

/// <summary>
/// <c>new Notification(title, { actions })</c> converts every entry of <c>actions</c> as a <c>NotificationAction</c>
/// dictionary. The constructor only counts the entries, because a non-persistent notification has none to offer (it refuses
/// a non-empty list), but the conversion is still observable, and that is what a page sees (sebastienros/jint#4516).
/// <para>
/// The members are Chromium's, not the standard's
/// (<a href="https://notifications.spec.whatwg.org/#dictdef-notificationaction">NotificationAction</a>, whose members are
/// <c>action</c>, <c>title</c>, <c>navigate</c> and <c>icon</c>): <c>action</c> and <c>title</c> are required
/// <c>DOMString</c>s, <c>icon</c> is an optional <c>USVString</c>, <c>placeholder</c> an optional <c>DOMString?</c> and
/// <c>type</c> a <c>NotificationActionType</c> (<c>"button"</c> or <c>"text"</c>, <c>"button"</c> when absent). Microsoft
/// Edge 156 reads exactly <c>action,icon,placeholder,title,type</c> from a Proxy entry, and never reads <c>navigate</c>;
/// the Proxy rows pin that whole sequence. Jint does not read <c>navigate</c> either, a known gap recorded on
/// <c>JsNotification</c>.
/// </para>
/// <para>
/// WebIDL converts a dictionary (https://webidl.spec.whatwg.org/#js-dictionary) by reading its members in lexicographic
/// order, converting each as it is read, and throwing for a required member that is <c>undefined</c> at the moment it is
/// reached. So a missing <c>action</c> stops the entry before any other getter runs, a missing <c>title</c> only after
/// <c>action</c>, <c>icon</c> and <c>placeholder</c> have been read and converted, and <c>type</c>, read last, only
/// after <c>title</c> is known. The sequence is created from <c>@@iterator</c> (https://webidl.spec.whatwg.org/#es-sequence),
/// read once, and an entry that fails to convert closes the iterator (sebastienros/jint#4431); the matrix in
/// <see cref="Jint.Tests.Browser.Dom.BrowserWebIdlSequenceCloseTests"/> pins that for every kind of iterable, and the rows
/// here pin it by name.
/// </para>
/// <para>
/// Every expected answer, message included, is Microsoft Edge 156's (Chromium), driven headless with <c>--dump-dom</c>,
/// except the close: Chromium leaves the iterator open, where Jint follows the convention of §4.220 of the migration
/// guide. The rows Edge was not asked (an enumeration value other than <c>bogus</c>, a <c>null</c> <c>placeholder</c>)
/// follow WebIDL's enumeration and nullable conversions (https://webidl.spec.whatwg.org/#es-enumeration), and say so.
/// </para>
/// </summary>
public sealed class NotificationActionsTests
{
    private const string Persistent =
        "Failed to construct 'Notification': Actions are only supported for persistent notifications shown using ServiceWorkerRegistration.showNotification().";

    private const string ActionsPrefix =
        "Failed to construct 'Notification': Failed to read the 'actions' property from 'NotificationOptions': ";

    private const string Harness = """
        function marker(name) { const e = new Error(name); e.name = name; return e; }

        // What the constructor did, as "<name>: <message>" for a throw.
        function outcome(f) {
            try { f(); return 'ok'; }
            catch (e) { return e.name + ': ' + e.message; }
        }

        const throwsOnConversion = {
            toString() { throw marker('ConversionError'); },
            valueOf() { throw marker('ConversionError'); },
        };

        const log = [];

        // A NotificationAction whose members are getters that log, declared out of lexicographic order on purpose: the
        // order they are read in is the conversion's, not the object's.
        const values = { action: 'a', icon: 'i.png', placeholder: 'p', title: 't', type: 'text' };
        function entry(i, ...omit) {
            const o = {};
            for (const name of ['type', 'title', 'placeholder', 'icon', 'action']) {
                if (omit.includes(name)) continue;
                Object.defineProperty(o, name, { enumerable: true, get() { log.push(name + i); return values[name]; } });
            }
            return o;
        }

        // The [[Get]]s a conversion makes on an entry, in order, and what the constructor then did.
        function proxied(target) {
            const gets = [];
            const proxy = new Proxy(target, { get(t, k, r) { gets.push(String(k)); return Reflect.get(t, k, r); } });
            return create([proxy]) + '|' + gets.join();
        }

        const create = actions => outcome(() => new Notification('t', { actions }));
        """;

    private static async Task<Page> NewPageAsync(Browser browser)
    {
        var page = await browser.NewPageAsync();
        await page.EvaluateAsync(Harness);
        return page;
    }

    /// <summary>Evaluates <paramref name="expression"/> in a page that has the harness.</summary>
    private static async Task<string> EvaluateAsync(string expression)
    {
        await using var browser = new Browser();
        var page = await NewPageAsync(browser);
        return await page.EvaluateAsync<string>(expression) ?? "<null>";
    }

    private static string Missing(string member)
        => "TypeError: " + ActionsPrefix + "Failed to read the '" + member + "' property from 'NotificationAction': Required member is undefined.";

    /// <summary>
    /// A required member that is <c>undefined</c> (or absent) is a <c>TypeError</c>, and it is the first one the
    /// lexicographic walk reaches that names the member. <c>[{}]</c> was accepted: only the count was taken.
    /// </summary>
    [TestCase("[{}]", "action")]
    [TestCase("[{ action: 'a' }]", "title")]
    [TestCase("[{ title: 't' }]", "action")]
    [TestCase("[{ icon: 'i.png' }]", "action")]
    [TestCase("[{ action: 'a', title: undefined }]", "title")]
    [TestCase("[{ action: undefined, title: 't' }]", "action")]
    [TestCase("[{ action: 'a', title: 't' }, {}]", "action")]
    [TestCase("[{ action: 'a', title: 't' }, { action: 'b' }]", "title")]
    [TestCase("new Set([{}])", "action")]
    [TestCase("[function () { }]", "action")]
    [TestCase("[[]]", "action")]
    public async Task AnEntryMissingARequiredMemberIsATypeError(string actions, string member)
        => (await EvaluateAsync($"create({actions})")).Should().Be(Missing(member));

    /// <summary>
    /// A value that is not an object cannot be a dictionary. Chromium's message is the same for <c>null</c> and
    /// <c>undefined</c>, though WebIDL would treat them as an empty dictionary and fail on the missing member.
    /// </summary>
    [TestCase("['x']")]
    [TestCase("[1]")]
    [TestCase("[true]")]
    [TestCase("[null]")]
    [TestCase("[undefined]")]
    [TestCase("[Symbol()]")]
    [TestCase("[10n]")]
    public async Task AnEntryThatIsNotAnObjectIsATypeError(string actions)
        => (await EvaluateAsync($"create({actions})")).Should().Be("TypeError: " + ActionsPrefix + "The provided value is not of type 'NotificationAction'.");

    /// <summary>
    /// What converts: every value a <c>DOMString</c> or <c>USVString</c> takes, an absent or undefined or null
    /// <c>icon</c>, a <c>null</c> <c>placeholder</c> (<c>DOMString?</c>), either <c>NotificationActionType</c> value, and
    /// members the dictionary does not know, <c>navigate</c> included (Edge never reads it, so even a Symbol there is
    /// harmless). The conversion completes, and the constructor then refuses the non-empty list as it always has.
    /// </summary>
    [TestCase("[{ action: 'a', title: 't' }]")]
    [TestCase("[{ action: 'a', title: 't', icon: 'i.png' }]")]
    [TestCase("[{ action: 'a', title: 't', icon: undefined }]")]
    [TestCase("[{ action: 'a', title: 't', icon: null }]")]
    [TestCase("[{ action: 'a', title: 't', icon: '\\ud800' }]")]
    [TestCase("[{ action: null, title: null }]")]
    [TestCase("[{ action: 1, title: {} }]")]
    [TestCase("[{ action: 'a', title: 't', unknown: 1 }]")]
    [TestCase("[{ action: 'a', title: 't', navigate: Symbol() }]")]
    [TestCase("[{ action: 'a', title: 't', placeholder: 'p', type: 'text' }]")]
    [TestCase("[{ action: 'a', title: 't', type: 'button' }]")]
    [TestCase("[{ action: 'a', title: 't', type: undefined }]")]
    [TestCase("[{ action: 'a', title: 't', placeholder: undefined }]")]
    [TestCase("[{ action: 'a', title: 't', placeholder: null }]")]
    [TestCase("[{ action: 'a', title: 't', placeholder: 1 }]")]
    [TestCase("new Set([{ action: 'a', title: 't' }])")]
    [TestCase("[{ action: 'a', title: 't' }, { action: 'b', title: 'u' }]")]
    public async Task AFullyConvertedListIsThenRefusedAsNonPersistent(string actions)
        => (await EvaluateAsync($"create({actions})")).Should().Be("TypeError: " + Persistent);

    /// <summary>
    /// <c>type</c> is an enumeration (https://webidl.spec.whatwg.org/#es-enumeration): the value is converted to a string
    /// and must be one of the two, exactly, so a case difference is as invalid as <c>bogus</c>. Edge 156 measured
    /// <c>bogus</c>; the others follow the same algorithm. The message names the member, the dictionary and the
    /// enumeration.
    /// </summary>
    [TestCase("'bogus'", "bogus")]
    [TestCase("'TEXT'", "TEXT")]
    [TestCase("'Button'", "Button")]
    [TestCase("''", "")]
    [TestCase("null", "null")]
    [TestCase("1", "1")]
    public async Task AnEntryWithAnInvalidTypeIsATypeError(string type, string shown)
        => (await EvaluateAsync($"create([{{ action: 'a', title: 't', type: {type} }}])")).Should().Be(
            "TypeError: " + ActionsPrefix + "Failed to read the 'type' property from 'NotificationAction': The provided value '" + shown
            + "' is not a valid enum value of type NotificationActionType.");

    /// <summary>An empty list, or none at all, is nothing to refuse.</summary>
    [TestCase("[]")]
    [TestCase("undefined")]
    [TestCase("new Set()")]
    public async Task NoEntriesIsAccepted(string actions)
        => (await EvaluateAsync($"create({actions})")).Should().Be("ok");

    /// <summary>
    /// A member whose conversion throws propagates what it threw. A Symbol has no string form, and a <c>toString</c> is
    /// the page's own code.
    /// </summary>
    [TestCase("[{ action: Symbol(), title: 't' }]", "TypeError", "Symbol")]
    [TestCase("[{ action: 'a', title: Symbol() }]", "TypeError", "Symbol")]
    [TestCase("[{ action: 'a', title: 't', icon: Symbol() }]", "TypeError", "Symbol")]
    [TestCase("[{ action: 'a', title: 't', placeholder: Symbol() }]", "TypeError", "Symbol")]
    [TestCase("[{ action: 'a', title: 't', type: Symbol() }]", "TypeError", "Symbol")]
    [TestCase("[{ action: throwsOnConversion, title: 't' }]", "ConversionError", "ConversionError")]
    [TestCase("[{ action: 'a', title: throwsOnConversion }]", "ConversionError", "ConversionError")]
    [TestCase("[{ action: 'a', title: 't', icon: throwsOnConversion }]", "ConversionError", "ConversionError")]
    [TestCase("[{ action: 'a', title: 't', placeholder: throwsOnConversion }]", "ConversionError", "ConversionError")]
    [TestCase("[{ action: 'a', title: 't', type: throwsOnConversion }]", "ConversionError", "ConversionError")]
    [TestCase("[{ action: 'a', get title() { throw marker('ConversionError'); } }]", "ConversionError", "ConversionError")]
    public async Task AMemberThatCannotBeConvertedPropagatesItsError(string actions, string error, string reason)
        => (await EvaluateAsync($"create({actions})")).Should().StartWith(error + ": ").And.Contain(reason).And.NotContain("persistent");

    /// <summary>
    /// A value that is not a sequence is a <c>TypeError</c>: step 1 of https://webidl.spec.whatwg.org/#es-sequence refuses
    /// anything that is not an object, a string included, and step 3 an object with no <c>@@iterator</c>. <c>null</c> is
    /// not <c>undefined</c>, so it is not an omitted member.
    /// </summary>
    [TestCase("1", "The provided value cannot be converted to a sequence.")]
    [TestCase("'abc'", "The provided value cannot be converted to a sequence.")]
    [TestCase("''", "The provided value cannot be converted to a sequence.")]
    [TestCase("true", "The provided value cannot be converted to a sequence.")]
    [TestCase("null", "The provided value cannot be converted to a sequence.")]
    [TestCase("Symbol()", "The provided value cannot be converted to a sequence.")]
    [TestCase("{}", "The object must have a callable @@iterator property.")]
    [TestCase("{ length: 1, 0: { action: 'a', title: 't' } }", "The object must have a callable @@iterator property.")]
    [TestCase("{ [Symbol.iterator]: undefined }", "The object must have a callable @@iterator property.")]
    [TestCase("{ [Symbol.iterator]: null }", "The object must have a callable @@iterator property.")]
    public async Task ANonIterableIsATypeError(string actions, string reason)
        => (await EvaluateAsync($"create({actions})")).Should().Be("TypeError: " + ActionsPrefix + reason);

    /// <summary>A <c>@@iterator</c> that is neither callable nor nullish is the <c>TypeError</c> GetMethod throws.</summary>
    [Test]
    public async Task ANonCallableIteratorIsATypeError()
        => (await EvaluateAsync("create({ [Symbol.iterator]: 1 })")).Should().StartWith("TypeError: ");

    /// <summary>
    /// Each entry's members are read once, converted as they are read, and in lexicographic order, whichever order the
    /// entry declares them in: <c>action</c>, <c>icon</c>, <c>title</c>, entry by entry.
    /// </summary>
    [Test]
    public async Task EveryMemberOfEveryEntryIsReadOnceInLexicographicOrder()
        => (await EvaluateAsync("create([entry(0), entry(1)]) + '|' + log.join()"))
            .Should().Be("TypeError: " + Persistent + "|action0,icon0,placeholder0,title0,type0,action1,icon1,placeholder1,title1,type1");

    /// <summary>
    /// <c>icon</c>, <c>placeholder</c> and <c>type</c> are optional, so an entry without them reads <c>action</c> and
    /// <c>title</c> and converts, and one without just <c>icon</c> still reads the rest.
    /// </summary>
    [Test]
    public async Task AnAbsentOptionalMemberIsNotRead()
    {
        (await EvaluateAsync("create([entry(0, 'icon', 'placeholder', 'type')]) + '|' + log.join()"))
            .Should().Be("TypeError: " + Persistent + "|action0,title0");
        (await EvaluateAsync("create([entry(1, 'icon')]) + '|' + log.join()"))
            .Should().Be("TypeError: " + Persistent + "|action1,placeholder1,title1,type1");
    }

    /// <summary>
    /// A missing <c>title</c> is reached after <c>action</c>, <c>icon</c> and <c>placeholder</c> have been read, and stops
    /// the entry: <c>type</c> is never read, and the later entries are never touched.
    /// </summary>
    [Test]
    public async Task AMissingTitleStopsTheConversionAfterActionIconAndPlaceholder()
        => (await EvaluateAsync("create([entry(0, 'title'), entry(1)]) + '|' + log.join()"))
            .Should().Be(Missing("title") + "|action0,icon0,placeholder0");

    /// <summary>A missing <c>action</c> is the first member read, so no getter of the entry runs.</summary>
    [Test]
    public async Task AMissingActionStopsTheConversionBeforeAnyOtherMember()
        => (await EvaluateAsync("create([entry(0, 'action')]) + '|' + log.join()"))
            .Should().Be(Missing("action") + "|");

    /// <summary>
    /// A member is converted when it is read, before the next member is read, so the page's <c>toString</c> and its
    /// next getter interleave as they do in a browser.
    /// </summary>
    [Test]
    public async Task EachMemberIsConvertedBeforeTheNextIsRead()
        => (await EvaluateAsync("""
            const o = {};
            const conversion = (name, value) => ({ toString() { log.push('toString ' + name); return value; } });
            for (const [name, value] of [['action', 'a'], ['icon', 'i'], ['placeholder', 'p'], ['title', 't'], ['type', 'text']])
                Object.defineProperty(o, name, { enumerable: true, get() { log.push('get ' + name); return conversion(name, value); } });
            create([o]) + '|' + log.join()
            """)).Should().Be("TypeError: " + Persistent
                + "|get action,toString action,get icon,toString icon,get placeholder,toString placeholder,get title,toString title,get type,toString type");

    /// <summary>
    /// The same walk with a <c>title</c> that is missing: the conversions before it have already run, and <c>type</c>,
    /// which follows it, has not been read.
    /// </summary>
    [Test]
    public async Task AMissingTitleFollowsTheConversionsOfActionIconAndPlaceholder()
        => (await EvaluateAsync("""
            const o = {};
            const conversion = (name, value) => ({ toString() { log.push('toString ' + name); return value; } });
            for (const [name, value] of [['action', 'a'], ['icon', 'i'], ['placeholder', 'p']])
                Object.defineProperty(o, name, { enumerable: true, get() { log.push('get ' + name); return conversion(name, value); } });
            Object.defineProperty(o, 'title', { enumerable: true, get() { log.push('get title'); return undefined; } });
            Object.defineProperty(o, 'type', { enumerable: true, get() { log.push('get type'); return 'text'; } });
            create([o]) + '|' + log.join()
            """)).Should().Be(Missing("title") + "|get action,toString action,get icon,toString icon,get placeholder,toString placeholder,get title");

    /// <summary>
    /// Every <c>[[Get]]</c> a conversion makes on an entry, in order, pinned through a Proxy: Edge 156 reads exactly
    /// <c>action,icon,placeholder,title,type</c> and nothing else (no <c>navigate</c>, and no <c>then</c> or
    /// <c>@@toPrimitive</c> from converting the values), and a missing <c>title</c> or <c>action</c> stops the walk where
    /// it is reached. The first and third rows are Edge's own output; the others follow from them and from WebIDL's order.
    /// </summary>
    [TestCase("{ action: 'a', title: 't' }", "TypeError: " + Persistent + "|action,icon,placeholder,title,type")]
    [TestCase("{ action: 'a', title: 't', icon: 'i', placeholder: 'p', type: 'text', navigate: 'n' }", "TypeError: " + Persistent + "|action,icon,placeholder,title,type")]
    [TestCase("{ action: 'a' }", "TypeError: " + ActionsPrefix + "Failed to read the 'title' property from 'NotificationAction': Required member is undefined.|action,icon,placeholder,title")]
    [TestCase("{ title: 't' }", "TypeError: " + ActionsPrefix + "Failed to read the 'action' property from 'NotificationAction': Required member is undefined.|action")]
    [TestCase("{ action: 'a', title: 't', type: 'bogus' }", "TypeError: " + ActionsPrefix + "Failed to read the 'type' property from 'NotificationAction': The provided value 'bogus' is not a valid enum value of type NotificationActionType.|action,icon,placeholder,title,type")]
    public async Task AProxyEntryIsReadInEdgesOrderAndNothingElse(string target, string expected)
        => (await EvaluateAsync($"proxied({target})")).Should().Be(expected);

    /// <summary>The <c>actions</c> member of the options is read before the options' other members.</summary>
    [Test]
    public async Task ActionsIsReadBeforeTheOtherOptionsMembers()
        => (await EvaluateAsync("""
            const options = {};
            Object.defineProperty(options, 'body', { enumerable: true, get() { log.push('body'); return 'b'; } });
            Object.defineProperty(options, 'actions', { enumerable: true, get() { log.push('actions'); return [entry(0)]; } });
            outcome(() => new Notification('t', options)) + '|' + log.join()
            """)).Should().Be("TypeError: " + Persistent + "|actions,action0,icon0,placeholder0,title0,type0,body");

    /// <summary>
    /// A failing entry stops the walk of the options, so a later member is never read: a conversion that throws is the
    /// constructor's throw.
    /// </summary>
    [Test]
    public async Task AFailingEntryStopsTheOptionsConversion()
        => (await EvaluateAsync("""
            const options = {};
            Object.defineProperty(options, 'actions', { enumerable: true, get() { log.push('actions'); return [{}]; } });
            Object.defineProperty(options, 'body', { enumerable: true, get() { log.push('body'); return 'b'; } });
            outcome(() => new Notification('t', options)) + '|' + log.join()
            """)).Should().Be(Missing("action") + "|actions");

    /// <summary>
    /// <c>@@iterator</c> is read once, by the sequence conversion; the method it found is called, not read again
    /// (https://webidl.spec.whatwg.org/#create-sequence-from-iterable).
    /// </summary>
    [Test]
    public async Task IteratorIsReadOnce()
        => (await EvaluateAsync("""
            let reads = 0;
            const list = [{ action: 'a', title: 't' }];
            const iterable = { get [Symbol.iterator]() { reads++; return list[Symbol.iterator].bind(list); } };
            create(iterable) + '|' + reads
            """)).Should().Be("TypeError: " + Persistent + "|1");

    /// <summary>
    /// The entry that fails to convert closes the iterator, once, and the entries after it are never produced. The
    /// error that propagates is the conversion's, not anything <c>return</c> does.
    /// </summary>
    [Test]
    public async Task AnEntryThatFailsToConvertClosesTheGeneratorOnce()
        => (await EvaluateAsync("""
            const state = { returns: 0, finallies: 0, produced: 0 };
            const GeneratorPrototype = Object.getPrototypeOf(function* () { }).prototype;
            const original = GeneratorPrototype.return;
            GeneratorPrototype.return = function (value) { state.returns++; return original.call(this, value); };
            const generator = (function* () {
                try {
                    state.produced++; yield { action: 'a', title: 't' };
                    state.produced++; yield {};
                    state.produced++; yield { action: 'never', title: 'never' };
                } finally { state.finallies++; }
            })();
            create(generator) + '|' + JSON.stringify(state)
            """)).Should().Be(Missing("action") + "|{\"returns\":1,\"finallies\":1,\"produced\":2}");

    /// <summary>
    /// Whichever member fails closes the generator once and stops it there: a bad <c>type</c>, a Symbol
    /// <c>placeholder</c>, and a getter that throws.
    /// </summary>
    [TestCase("{ action: 'a', title: 't', type: 'bogus' }", "NotificationActionType")]
    [TestCase("{ action: 'a', title: 't', placeholder: Symbol() }", "Symbol")]
    [TestCase("{ action: 'a', get title() { throw marker('ConversionError'); } }", "ConversionError")]
    public async Task AnEntryThatFailsOnAnyMemberClosesTheGeneratorOnce(string failing, string reason)
        => (await EvaluateAsync($$"""
            const state = { returns: 0, produced: 0 };
            const GeneratorPrototype = Object.getPrototypeOf(function* () { }).prototype;
            const original = GeneratorPrototype.return;
            GeneratorPrototype.return = function (value) { state.returns++; return original.call(this, value); };
            const generator = (function* () {
                state.produced++; yield { action: 'a', title: 't' };
                state.produced++; yield {{failing}};
                state.produced++; yield { action: 'never', title: 'never' };
            })();
            create(generator) + '|' + JSON.stringify(state)
            """)).Should().Contain(reason).And.NotContain("persistent").And.EndWith("|{\"returns\":1,\"produced\":2}");

    /// <summary>
    /// The same for a hand-written iterator, whose <c>return</c> is the page's: called once, after the failing entry, and
    /// <c>next</c> is not called again.
    /// </summary>
    [Test]
    public async Task AnEntryThatFailsToConvertClosesAnIteratorOnce()
        => (await EvaluateAsync("""
            const state = { nexts: 0, returns: 0 };
            const iterable = {
                [Symbol.iterator]() {
                    return {
                        next() { state.nexts++; return { value: state.nexts === 1 ? { action: 'a', title: 't' } : { action: 'b' }, done: false }; },
                        return() { state.returns++; return {}; },
                    };
                },
            };
            create(iterable) + '|' + JSON.stringify(state)
            """)).Should().Be(Missing("title") + "|{\"nexts\":2,\"returns\":1}");

    /// <summary>
    /// A step that throws leaves the iterator done, and a list that runs out has nothing to close: neither calls
    /// <c>return</c>, even though the entries before them converted.
    /// </summary>
    [Test]
    public async Task ABrokenStepAndAListThatRunsOutCloseNothing()
    {
        await using var browser = new Browser();
        var page = await NewPageAsync(browser);

        const string Script = """
            function run(throwsInNext) {
                const state = { nexts: 0, returns: 0 };
                const iterable = {
                    [Symbol.iterator]() {
                        return {
                            next() {
                                state.nexts++;
                                if (state.nexts === 2 && throwsInNext) throw marker('NextError');
                                return state.nexts === 1 ? { value: { action: 'a', title: 't' }, done: false } : { value: undefined, done: true };
                            },
                            return() { state.returns++; return {}; },
                        };
                    },
                };
                return create(iterable) + '|' + JSON.stringify(state);
            }
            """;
        await page.EvaluateAsync(Script);

        (await page.EvaluateAsync<string>("run(true)")).Should().Be("NextError: NextError|{\"nexts\":2,\"returns\":0}");
        (await page.EvaluateAsync<string>("run(false)")).Should().Be("TypeError: " + Persistent + "|{\"nexts\":2,\"returns\":0}");
    }
}
