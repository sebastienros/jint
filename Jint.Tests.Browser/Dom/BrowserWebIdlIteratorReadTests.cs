#nullable enable

namespace Jint.Tests.Browser.Dom;

using Browser = global::Jint.Browser.Browser;

/// <summary>
/// Pins the one read of <c>@@iterator</c> a WebIDL conversion makes when it decides whether a value is a sequence. The
/// union conversion (https://webidl.spec.whatwg.org/#es-union) takes <c>? GetMethod(V, %Symbol.iterator%)</c>
/// (<a href="https://tc39.es/ecma262/#sec-getmethod">GetMethod</a>) once, and so does the sequence conversion
/// (https://webidl.spec.whatwg.org/#es-sequence) on its own. When it found a method they create the sequence from V and
/// that method (https://webidl.spec.whatwg.org/#create-sequence-from-iterable), whose
/// <a href="https://tc39.es/ecma262/#sec-getiteratorfrommethod">GetIteratorFromMethod</a> calls it without a second
/// read. GetMethod answers "no method" for undefined and null, which take the union's other arm (or, for a bare
/// sequence, are a <c>TypeError</c>), and throws a <c>TypeError</c> for anything else that is not callable.
/// <para>
/// Seven lanes in <c>Jint.Browser</c> asked first and then took GetIterator, which read again
/// (sebastienros/jint#4455, the same defect #4449 fixed under <c>Jint/WebApi/</c>): the <c>DOMMatrix</c> and
/// <c>DOMMatrixReadOnly</c> constructors, <c>roundRect</c> on a 2D context and on <c>Path2D</c>, the <c>hints</c> of
/// <c>getHighEntropyValues</c>, and a <c>Notification</c>'s <c>vibrate</c> pattern and <c>actions</c> count. Read
/// counts and the arm a nullish method takes are Microsoft Edge 156's (Chromium) for these non-Array values, which
/// it reads once everywhere except <c>roundRect</c>: Chromium reads <c>@@iterator</c> twice there, against WebIDL,
/// so the <c>roundRect</c> rows follow the specification.
/// </para>
/// </summary>
/// <remarks>
/// Whether a conversion closes the iterator when an element fails is not pinned here; that is
/// <see cref="BrowserWebIdlSequenceCloseTests"/> (sebastienros/jint#4431).
/// </remarks>
public sealed class BrowserWebIdlIteratorReadTests
{
    private const string Harness = """
        const ctx = document.createElement('canvas').getContext('2d');

        // Each lane: `method` is the iterator method its sequence arm runs, `own` the own properties its other arm reads
        // (a dictionary, a toString or a valueOf), and `run` the conversion, answering what it produced.
        const lanes = {
            domMatrix: () => ({
                method: function* () { yield* [1, 0, 0, 1, 5, 6]; },
                own: { toString() { return 'translate(7px, 8px)'; } },
                run: init => { const m = new DOMMatrix(init); return m.e + ',' + m.f; },
            }),
            domMatrixReadOnly: () => ({
                method: function* () { yield* [1, 0, 0, 1, 5, 6]; },
                own: { toString() { return 'translate(7px, 8px)'; } },
                run: init => { const m = new DOMMatrixReadOnly(init); return m.e + ',' + m.f; },
            }),
            roundRect: () => ({
                method: function* () { yield 1; },
                own: { x: -1 },
                run: init => { ctx.roundRect(0, 0, 10, 10, init); return 'ok'; },
            }),
            path2DRoundRect: () => ({
                method: function* () { yield 1; },
                own: { x: -1 },
                run: init => { new Path2D().roundRect(0, 0, 10, 10, init); return 'ok'; },
            }),
            hints: () => ({
                method: function* () { yield 'architecture'; },
                own: {},
                run: init => navigator.userAgentData.getHighEntropyValues(init).then(v => 'architecture' in v ? 'architecture' : 'none'),
            }),
            vibrate: () => ({
                method: function* () { yield 5; yield 6; },
                own: { valueOf() { return 7; } },
                run: init => new Notification('x', { vibrate: init }).vibrate.join(),
            }),
            // A non-persistent notification refuses a non-empty actions list, which is how its count is seen.
            actions: () => ({
                method: function* () { yield { action: 'a', title: 't' }; },
                own: {},
                run: init => {
                    try { new Notification('x', { actions: init }); return 'ok'; }
                    catch (e) { return e.name === 'TypeError' && e.message.includes('Actions are only supported') ? 'counted' : e.name; }
                },
            }),
        };

        // Makes the lane's argument an object whose Symbol.iterator is an accessor - on the object itself ('own') or on
        // its prototype ('proto') - that counts its reads and answers per `answers`, runs the lane and answers
        // "<result> <reads>", the result being what the lane produced or the name of what it threw.
        //   method                 the lane's iterator method, every read.
        //   methodThenUndefined    the lane's method on the first read, undefined on any later one.
        //   undefinedThenMethod    undefined on the first read, the lane's method on any later one.
        //   undefined, null        GetMethod's "no method", which takes the other arm.
        //   number, string, object, boolean, symbol, bigint
        //                          neither callable nor nullish, for which GetMethod throws a TypeError.
        async function reads(laneName, where, answers) {
            const lane = lanes[laneName]();
            let count = 0;
            const answer = {
                method: () => lane.method,
                methodThenUndefined: () => count === 1 ? lane.method : undefined,
                undefinedThenMethod: () => count === 1 ? undefined : lane.method,
                undefined: () => undefined,
                null: () => null,
                number: () => 42,
                string: () => 'x',
                object: () => ({}),
                boolean: () => true,
                symbol: () => Symbol.iterator,
                bigint: () => 1n,
            }[answers];
            const proto = {};
            const init = Object.assign(Object.create(proto), lane.own);
            Object.defineProperty(where === 'own' ? init : proto, Symbol.iterator, { get() { count++; return answer(); } });
            let result;
            try { result = await lane.run(init); } catch (e) { result = e.name; }
            return result + ' ' + count;
        }
        """;

    // What each lane produces from its sequence arm and from its other arm; a bare sequence has no other arm.
    private static readonly Dictionary<string, (string Sequence, string Other)> Lanes = new()
    {
        ["domMatrix"] = ("5,6", "7,8"),
        ["domMatrixReadOnly"] = ("5,6", "7,8"),
        ["roundRect"] = ("ok", "RangeError"),
        ["path2DRoundRect"] = ("ok", "RangeError"),
        ["hints"] = ("architecture", "TypeError"),
        ["vibrate"] = ("5,6", "7"),
        ["actions"] = ("counted", "TypeError"),
    };

    private static readonly string[] Answers =
    [
        "method",
        "methodThenUndefined",
        "undefinedThenMethod",
        "undefined",
        "null",
        "number",
        "string",
        "object",
        "boolean",
        "symbol",
        "bigint",
    ];

    // One read every time; the method that read found decides the arm.
    private static string Expected(string lane, string answers)
    {
        var (sequence, other) = Lanes[lane];
        return answers switch
        {
            "method" or "methodThenUndefined" => sequence + " 1",
            "undefinedThenMethod" or "undefined" or "null" => other + " 1",
            _ => "TypeError 1",
        };
    }

    public static TestCases<string, string, string, string> EveryLaneUnderEveryAnswer
    {
        get
        {
            var rows = new TestCases<string, string, string, string>();
            foreach (var lane in Lanes.Keys)
            {
                foreach (var answers in Answers)
                {
                    foreach (var where in new[] { "own", "proto" })
                    {
                        rows.Add(lane, answers, where, Expected(lane, answers));
                    }
                }
            }

            return rows;
        }
    }

    /// <summary>
    /// Each lane reads <c>@@iterator</c> once and takes the arm that read decides: the sequence arm, iterating with the
    /// method it found even when a second read would find none; the other arm for undefined and null; and a
    /// <c>TypeError</c> for a value that is neither callable nor nullish.
    /// </summary>
    [TestCaseSource(nameof(EveryLaneUnderEveryAnswer))]
    public async Task EveryLaneReadsIteratorOnce(string lane, string answers, string where, string expected)
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.EvaluateAsync(Harness);

        (await page.EvaluateAndAwaitAsync<string>($"reads('{lane}', '{where}', '{answers}')")).Should().Be(expected);
    }

    /// <summary>
    /// The issue's own probe, on a real array: every lane reads its <c>@@iterator</c> once. Chromium reads it zero
    /// times on an Array, its fast path skipping <c>@@iterator</c>, so this row follows WebIDL.
    /// </summary>
    [Test]
    public async Task AnArrayWhoseIteratorIsAGetterIsReadOnceByEveryLane()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.EvaluateAsync(Harness);

        (await page.EvaluateAndAwaitAsync<string>("""
            (async () => {
                const arrays = {
                    domMatrix: [1, 0, 0, 1, 5, 6],
                    domMatrixReadOnly: [1, 0, 0, 1, 5, 6],
                    roundRect: [1, 2],
                    path2DRoundRect: [1, 2],
                    hints: ['architecture'],
                    vibrate: [5, 6],
                    actions: [],
                };
                const counts = [];
                for (const [name, init] of Object.entries(arrays)) {
                    let reads = 0;
                    const iterator = init[Symbol.iterator];
                    Object.defineProperty(init, Symbol.iterator, { get() { reads++; return iterator; } });
                    await lanes[name]().run(init);
                    counts.push(name + '=' + reads);
                }
                return counts.join();
            })()
            """)).Should().Be("domMatrix=1,domMatrixReadOnly=1,roundRect=1,path2DRoundRect=1,hints=1,vibrate=1,actions=1");
    }
}
