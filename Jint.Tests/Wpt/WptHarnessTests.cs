#if NET8_0_OR_GREATER
#nullable enable

namespace Jint.Tests.Wpt;

/// <summary>
/// The harness shim, tested in its own right.
/// </summary>
/// <remarks>
/// <para>
/// Everything <see cref="WptTestRunner"/> reports rests on the shim classifying correctly, and the failure
/// mode that matters is the quiet one: an <c>assert_equals</c> that never threw would turn five thousand
/// vendored cases green and mean nothing at all. So each assertion is exercised from both sides — a case it
/// must pass and a case it must fail — and the two completion rules that decide when a file is finished
/// (<c>promise_test</c> chaining, <c>async_test</c> plus <c>done()</c>) are pinned as ordering, never as
/// timing.
/// </para>
/// </remarks>
public class WptHarnessTests
{
    private static WptRunOutcome Run(string script) => WptHarness.RunInline(script);

    /// <summary>The status of the single test the script registers.</summary>
    private static string StatusOf(string script)
    {
        var outcome = Run(script);
        outcome.HarnessError.Should().BeNull();
        outcome.Results.Should().HaveCount(1);
        return outcome.Results[0].Status;
    }

    [Theory]
    // Each row is one assertion, a body that must be recorded PASS and a body that must be recorded FAIL.
    [InlineData("assert_true(true)", "assert_true(1)")]
    [InlineData("assert_false(false)", "assert_false(0)")]
    [InlineData("assert_equals(1, 1)", "assert_equals(1, 2)")]
    // same-value, not ===: NaN equals itself and the two zeroes are different values.
    [InlineData("assert_equals(NaN, NaN)", "assert_equals(0, -0)")]
    [InlineData("assert_equals('a', 'a')", "assert_equals('a', 'A')")]
    [InlineData("assert_not_equals(1, 2)", "assert_not_equals(1, 1)")]
    [InlineData("assert_array_equals([1, 2], [1, 2])", "assert_array_equals([1, 2], [1, 2, 3])")]
    [InlineData("assert_array_equals([NaN], [NaN])", "assert_array_equals([1, 2], [2, 1])")]
    [InlineData("assert_throws_js(TypeError, () => { throw new TypeError(); })", "assert_throws_js(TypeError, () => {})")]
    // The constructor has to be the one asked for, so a RangeError does not satisfy a TypeError expectation.
    [InlineData("assert_throws_js(RangeError, () => { throw new RangeError(); })", "assert_throws_js(TypeError, () => { throw new RangeError(); })")]
    [InlineData(
        "assert_throws_dom('DataCloneError', () => { throw new DOMException('x', 'DataCloneError'); })",
        "assert_throws_dom('DataCloneError', () => { throw new DOMException('x', 'NotFoundError'); })")]
    // A plain Error wearing the right name and code is not a DOMException.
    [InlineData(
        "assert_throws_dom('NotFoundError', () => { throw new DOMException('x', 'NotFoundError'); })",
        "assert_throws_dom('NotFoundError', () => { var e = new Error(); e.name = 'NotFoundError'; e.code = 8; throw e; })")]
    [InlineData("assert_throws_exactly(1, () => { throw 1; })", "assert_throws_exactly(1, () => { throw 2; })")]
    [InlineData("if (false) assert_unreached('x')", "assert_unreached('x')")]
    [InlineData("assert_in_array(2, [1, 2, 3])", "assert_in_array(4, [1, 2, 3])")]
    // indexOf rather than same_value, which is upstream's own documented caveat: -0 finds 0, NaN finds nothing.
    [InlineData("assert_in_array(-0, [0])", "assert_in_array(NaN, [NaN])")]
    [InlineData("assert_object_equals({ a: 1, b: { c: 2 } }, { a: 1, b: { c: 2 } })", "assert_object_equals({ a: { b: 1 } }, { a: { b: 2 } })")]
    // A property on one side and not the other, in both directions: the second walk is what catches the second.
    [InlineData("assert_object_equals({ a: 1 }, { a: 1 })", "assert_object_equals({ a: 1, b: 2 }, { a: 1 })")]
    [InlineData("assert_object_equals([1, 2], [1, 2])", "assert_object_equals({ a: 1 }, { a: 1, b: 2 })")]
    // Leaves compare by same_value, exactly as assert_equals does.
    [InlineData("assert_object_equals({ a: NaN }, { a: NaN })", "assert_object_equals({ a: 0 }, { a: -0 })")]
    [InlineData("assert_object_equals({}, {})", "assert_object_equals(1, {})")]
    // What upstream's `for..in` walk plus its `hasOwnProperty` check add up to: an enumerable property has to
    // be an own property on *both* sides, because the walk reaches an inherited one and the check refuses it.
    // Both directions, since the two loops are what catch the two of them.
    [InlineData("assert_object_equals({ a: 'x' }, { a: 'x' })", "assert_object_equals(Object.create({ a: 1 }), { a: 1 })")]
    [InlineData("assert_object_equals({ a: undefined }, { a: undefined })", "assert_object_equals({ a: 1 }, Object.create({ a: 1 }))")]
    [InlineData("assert_class_string(new URL('https://example.com/'), 'URL')", "assert_class_string(new URL('https://example.com/'), 'Object')")]
    [InlineData("assert_class_string(Math, 'Math')", "assert_class_string([], 'Object')")]
    [InlineData("assert_own_property({ x: 1 }, 'x')", "assert_own_property({}, 'x')")]
    // Own, so a property reached through the prototype chain does not satisfy it.
    [InlineData("assert_own_property([], 'length')", "assert_own_property(Object.create({ x: 1 }), 'x')")]
    public void AnAssertionRecordsBothOutcomes(string passing, string failing)
    {
        StatusOf($"test(() => {{ {passing} }}, 'row');").Should().Be("PASS");
        StatusOf($"test(() => {{ {failing} }}, 'row');").Should().Be("FAIL");
    }

    /// <summary>
    /// What every <c>promise_rejects_*</c> row runs before its assertion, so a row can name a value by
    /// identity and can name a <c>DOMException</c> constructor that is not this global's.
    /// </summary>
    private const string RejectionPreamble = """
        var sentinel = new Error('sentinel');
        // What a cross-realm DOMException looks like to the overload check — a function named DOMException
        // that is not the one this global exposes — without a second realm to obtain one from.
        var foreignDOMException = function DOMException() {};
        """;

    [Theory]
    // Each row is one rejection assertion inside a promise_test, a body that must be recorded PASS and a body
    // that must be recorded FAIL. `t` is the test the shim passes the body.
    [InlineData(
        "promise_rejects_js(t, TypeError, Promise.reject(new TypeError()))",
        "promise_rejects_js(t, TypeError, Promise.reject(new RangeError()))")]
    // A promise that resolves has to fail the assertion rather than satisfy it by not rejecting.
    [InlineData(
        "promise_rejects_js(t, RangeError, Promise.reject(new RangeError()))",
        "promise_rejects_js(t, TypeError, Promise.resolve('resolved'))")]
    // The same sanity check assert_throws_js makes: a rejection reason that is not an object is not an error.
    [InlineData(
        "promise_rejects_js(t, TypeError, Promise.reject(new TypeError()))",
        "promise_rejects_js(t, TypeError, Promise.reject('not an object'))")]
    [InlineData(
        "promise_rejects_dom(t, 'NotFoundError', Promise.reject(new DOMException('x', 'NotFoundError')))",
        "promise_rejects_dom(t, 'NotFoundError', Promise.reject(new DOMException('x', 'DataCloneError')))")]
    [InlineData(
        "promise_rejects_dom(t, 'DataCloneError', Promise.reject(new DOMException('x', 'DataCloneError')))",
        "promise_rejects_dom(t, 'NotFoundError', Promise.resolve())")]
    // A plain Error wearing the right name and the right legacy code is still not a DOMException.
    [InlineData(
        "promise_rejects_dom(t, 'AbortError', Promise.reject(new DOMException('x', 'AbortError')))",
        "promise_rejects_dom(t, 'NotFoundError', Promise.reject(Object.assign(new Error(), { name: 'NotFoundError', code: 8 })))")]
    // The four-argument form names the global the exception must come from: this one satisfies it, and a
    // DOMException constructor that is not this global's does not, however right the name and code are.
    [InlineData(
        "promise_rejects_dom(t, 'NotFoundError', DOMException, Promise.reject(new DOMException('x', 'NotFoundError')))",
        "promise_rejects_dom(t, 'NotFoundError', foreignDOMException, Promise.reject(new DOMException('x', 'NotFoundError')))")]
    [InlineData(
        "promise_rejects_exactly(t, sentinel, Promise.reject(sentinel))",
        "promise_rejects_exactly(t, sentinel, Promise.reject(new Error('sentinel')))")]
    [InlineData(
        "promise_rejects_exactly(t, 1, Promise.reject(1))",
        "promise_rejects_exactly(t, 1, Promise.resolve(1))")]
    public void ARejectionAssertionRecordsBothOutcomes(string passing, string failing)
    {
        StatusOf($"{RejectionPreamble}\npromise_test(t => {passing}, 'row');").Should().Be("PASS");
        StatusOf($"{RejectionPreamble}\npromise_test(t => {failing}, 'row');").Should().Be("FAIL");
    }

    [Fact]
    public void ARejectionAssertionReportsTheSameMismatchItsSynchronousTwinWould()
    {
        // The point of the promise_rejects_* family routing through assert_throws_* rather than repeating the
        // matching rules: the message is the one assert_throws_js produces, character for character.
        var rejected = Run("promise_test(t => promise_rejects_js(t, TypeError, Promise.reject(new RangeError()), 'why'), 'row');");
        var thrown = Run("test(() => assert_throws_js(TypeError, () => { throw new RangeError(); }, 'why'), 'row');");

        rejected.HarnessError.Should().BeNull();
        rejected.Results[0].Status.Should().Be("FAIL");
        rejected.Results[0].Message.Should().Be("expected TypeError but got object \"RangeError\" (why)");
        rejected.Results[0].Message.Should().Be(thrown.Results[0].Message);
    }

    [Fact]
    public void APromiseThatResolvesSaysThatIsWhyTheRejectionAssertionFailed()
    {
        var outcome = Run("promise_test(t => promise_rejects_exactly(t, 1, Promise.resolve(), 'the operation'), 'row');");

        outcome.HarnessError.Should().BeNull();
        outcome.Results[0].Status.Should().Be("FAIL");
        outcome.Results[0].Message.Should().Contain("Should have rejected: the operation");
    }

    [Fact]
    public void TheNoConstructorFormOfPromiseRejectsDomRefusesAFifthArgument()
    {
        // A suite that meant the constructor form and spelled it wrong would otherwise have its description
        // silently swallowed and the assertion still pass, which is what upstream's guard is for.
        var outcome = Run(
            "promise_test(t => promise_rejects_dom(t, 'NotFoundError', Promise.reject(new DOMException('x', 'NotFoundError')), 'why', 'extra'), 'row');");

        outcome.HarnessError.Should().BeNull();
        outcome.Results[0].Status.Should().Be("FAIL");
        outcome.Results[0].Message.Should().Contain("Too many args passed to no-constructor version of promise_rejects_dom");
    }

    [Fact]
    public void ARejectionAssertionIsWaitedOnRatherThanFireAndForgotten()
    {
        // The assertion has to be part of what settles the test: if the returned promise were dropped, the
        // test would be recorded before the rejection was ever observed and this row would read PASS.
        var outcome = Run("""
            promise_test(t => promise_rejects_js(t, TypeError, new Promise((resolve, reject) => setTimeout(() => reject(new RangeError()), 1))), 'row');
            """);

        outcome.HarnessError.Should().BeNull();
        outcome.Results[0].Status.Should().Be("FAIL");
        outcome.Results[0].Message.Should().Contain("RangeError");
    }

    [Fact]
    public void AFailingTestKeepsTheMessageThatExplainsIt()
    {
        var outcome = Run("test(() => assert_equals('got', 'want', 'why'), 'row');");

        outcome.Results.Should().HaveCount(1);
        outcome.Results[0].Status.Should().Be("FAIL");
        outcome.Results[0].Message.Should().Be("expected \"want\" but got \"got\" (why)");
    }

    [Fact]
    public void TheValueAssertionsSayWhichPropertyDisagreedAndHow()
    {
        // Messages are what an exclusion is triaged from, and every one of these is a name the corresponding
        // upstream assertion produces once its ${p}/${actual} substitutions have gone through format_value.
        static string? MessageOf(string body) => Run($"test(() => {body}, 'row');").Results[0].Message;

        MessageOf("assert_object_equals({ a: 1 }, { a: 2 }, 'why')").Should().Be("property \"a\" expected 2 got 1 (why)");
        MessageOf("assert_object_equals({ a: 1, b: 2 }, { a: 1 })").Should().Be("unexpected property \"b\"");
        MessageOf("assert_object_equals({ a: 1 }, { a: 1, b: 2 })").Should().Be("expected property \"b\" missing");
        MessageOf("assert_object_equals(1, {})").Should().Be("value is 1, expected object");
        MessageOf("assert_own_property({}, 'x')").Should().Be("expected property \"x\" missing");
        MessageOf("assert_class_string([], 'URL')").Should().Be("expected \"[object URL]\" but got \"[object Array]\"");
        MessageOf("assert_in_array(4, [1, 2, 3])").Should().Be("value 4 not in array [1, 2, 3]");
    }

    [Fact]
    public void AThrowThatIsNotAnAssertionIsAlsoAFailure()
    {
        var outcome = Run("test(() => { null.x; }, 'row');");

        outcome.Results[0].Status.Should().Be("FAIL");
        outcome.Results[0].Message.Should().Contain("TypeError");
    }

    [Fact]
    public void AssertionsInsideAThrowsAssertionAreNotSwallowedByIt()
    {
        // assert_throws_js catches whatever the body throws; an AssertionError raised *by an assertion the
        // body ran* has already been classified and must not be counted as the exception being asked for.
        StatusOf("test(() => assert_throws_js(TypeError, () => assert_true(false)), 'row');")
            .Should().Be("FAIL");
    }

    [Fact]
    public void ADomExceptionAssertionChecksTheLegacyCodeAsWellAsTheName()
    {
        // https://webidl.spec.whatwg.org/#idl-DOMException-error-names — DataCloneError carries code 25, and
        // a name added after the legacy list carries 0.
        StatusOf("test(() => assert_throws_dom('DataCloneError', () => { throw new DOMException('x', 'DataCloneError'); }), 'row');")
            .Should().Be("PASS");
        StatusOf("test(() => assert_throws_dom('EncodingError', () => { throw new DOMException('x', 'EncodingError'); }), 'row');")
            .Should().Be("PASS");
    }

    [Fact]
    public void FormatValueEscapesTheControlCharactersTestNamesAreBuiltFrom()
    {
        // api-invalid-label.any.js names every one of its ~3,400 cases with format_value, so this decides
        // whether an exclusion written against wpt's own name for a case would match ours. Both sides are
        // spelled with fromCharCode so that no layer of escaping can be read two ways: 34 is a quote, 92 a
        // backslash.
        var outcome = Run("""
            var chr = String.fromCharCode;
            test(() => {
                assert_equals(format_value('a'), '"a"');
                assert_equals(format_value(chr(0)), chr(34, 92, 48, 34), 'NUL becomes backslash-zero');
                assert_equals(format_value(chr(11)), chr(34, 92, 118, 34), 'VT becomes backslash-v');
                assert_equals(format_value(chr(31)), chr(34, 92) + 'x1f' + chr(34), 'U+001F becomes backslash-x1f');
                assert_equals(format_value(chr(92)), chr(34, 92, 92, 34), 'a backslash doubles');
                assert_equals(format_value(chr(34)), chr(34, 92, 34, 34), 'a quote is escaped');
                assert_equals(format_value(chr(160)), chr(34, 160, 34), 'NBSP is left alone');
                assert_equals(format_value(-0), '-0', 'negative zero is not zero');
                assert_equals(format_value(undefined), 'undefined');
                assert_equals(format_value([1, 'a']), '[1, "a"]');
            }, 'row');
            """);

        outcome.Results[0].Status.Should().Be("PASS", outcome.Results[0].Message);
    }

    [Fact]
    public void EveryRegisteredTestIsRecordedInRegistrationOrder()
    {
        var outcome = Run("test(() => {}, 'first'); test(() => assert_true(false), 'second'); test(() => {}, 'third');");

        outcome.Results.Select(r => r.Name).Should().Equal("first", "second", "third");
        outcome.Results.Select(r => r.Status).Should().Equal("PASS", "FAIL", "PASS");
    }

    [Fact]
    public void PromiseTestsRunOneAfterTheOtherRatherThanConcurrently()
    {
        // Promise tests run in sequence, each starting only once the previous has finished —
        // https://web-platform-tests.org/writing-tests/testharness-api.html#promise-tests. Interleaving would
        // spell "a1b1a2b2"; the assertion is on order alone, so nothing here depends on how long anything takes.
        var outcome = Run("""
            var log = [];
            function later(value) { return Promise.resolve().then(() => Promise.resolve()).then(() => log.push(value)); }
            promise_test(() => later('a1').then(() => later('a2')), 'a');
            promise_test(() => later('b1').then(() => later('b2')), 'b');
            promise_test(() => { assert_equals(log.join(''), 'a1a2b1b2'); return Promise.resolve(); }, 'order');
            """);

        outcome.HarnessError.Should().BeNull();
        outcome.Results.Select(r => r.Status).Should().Equal("PASS", "PASS", "PASS");
    }

    [Fact]
    public void ARejectedPromiseTestIsAFailureAndDoesNotStopTheNextOne()
    {
        var outcome = Run("""
            promise_test(() => Promise.reject(new Error('nope')), 'rejects');
            promise_test(() => Promise.resolve(), 'still runs');
            """);

        outcome.HarnessError.Should().BeNull();
        outcome.Results.Select(r => r.Status).Should().Equal("FAIL", "PASS");
        outcome.Results[0].Message.Should().Contain("nope");
    }

    [Fact]
    public void AThrowingPromiseTestBodyIsAFailureRatherThanAHarnessError()
    {
        var outcome = Run("promise_test(() => { throw new Error('sync'); }, 'throws synchronously');");

        outcome.HarnessError.Should().BeNull();
        outcome.Results[0].Status.Should().Be("FAIL");
    }

    [Fact]
    public void ATestRegisteredFromInsideAPromiseTestIsStillCollected()
    {
        // This is the shape every corpus-driven suite has: one promise_test loads the JSON and registers a
        // plain test() per row from inside the chain.
        var outcome = Run("""
            promise_test(() => Promise.resolve().then(() => {
                test(() => {}, 'row 1');
                test(() => assert_true(false), 'row 2');
            }), 'loading');
            """);

        outcome.HarnessError.Should().BeNull();
        outcome.Results.Select(r => r.Name).Should().Equal("loading", "row 1", "row 2");
        outcome.Results.Select(r => r.Status).Should().Equal("PASS", "PASS", "FAIL");
    }

    [Fact]
    public void AnAsyncTestIsOutstandingUntilItCallsDone()
    {
        var outcome = Run("""
            var t = async_test('async');
            Promise.resolve().then(t.step_func(() => assert_true(true))).then(() => t.done());
            """);

        outcome.HarnessError.Should().BeNull();
        outcome.Results.Select(r => r.Status).Should().Equal("PASS");
    }

    [Fact]
    public void AnAsyncTestThatNeverFinishesIsReportedRatherThanSilentlyPassing()
    {
        // The one thing an outstanding test must not do is disappear. Nothing is queued here and the engine
        // has scheduled nothing for itself, so the drive loop says so at once — the five-minute runaway
        // guard is never reached, and no assertion here is about how long anything took.
        var outcome = Run("async_test('never finishes'); test(() => {}, 'other');");

        outcome.HarnessError.Should().NotBeNull();
        outcome.HarnessError.Should().Contain("never finishes");
        outcome.HarnessError.Should().NotContain("other");
    }

    [Fact]
    public void ATestThatWaitsOnATimerIsDrivenToCompletion()
    {
        // The harness installs no setTimeout of its own: this is the engine's own, from
        // WebApiFeatures.Timers, and the drive loop is what pumps it. Note what is *not* asserted — whether
        // the timer came due during Execute's own drain or during a later pass of the loop is not something
        // this pins, and either way the recorded outcome has to be the same. It is the only test here that
        // reaches the drive loop's waiting branch at all, and it earned its place by catching a race there:
        // asking the shim whether it had finished by evaluating a script drained the event loop as the
        // evaluation returned, so the already-computed "not finished" was acted on after the timer had in
        // fact settled the last test, and the run was declared stalled.
        var outcome = Run("""
            test(() => assert_equals(typeof setTimeout, 'function'), 'the engine supplies setTimeout');
            promise_test(() => new Promise(resolve => setTimeout(resolve, 1)), 'waits on a timer');
            """);

        outcome.HarnessError.Should().BeNull();
        outcome.Results.Select(r => r.Status).Should().Equal("PASS", "PASS");
    }

    [Fact]
    public void AStepThatThrowsFailsTheAsyncTestItBelongsTo()
    {
        var outcome = Run("""
            var t = async_test('async');
            t.step(() => assert_true(false));
            t.done();
            """);

        outcome.Results[0].Status.Should().Be("FAIL");
    }

    [Fact]
    public void AnUnreachedFuncFailsTheTestWhenItIsCalled()
    {
        var outcome = Run("""
            var t = async_test('async');
            Promise.resolve().then(t.unreached_func('should not resolve')).then(() => t.done());
            """);

        outcome.Results[0].Status.Should().Be("FAIL");
        outcome.Results[0].Message.Should().Contain("should not resolve");
    }

    [Fact]
    public void SetupRunsBeforeTheTestsThatUseWhatItPrepared()
    {
        var outcome = Run("""
            var prepared = [];
            setup(function () { prepared.push('ready'); });
            test(() => assert_array_equals(prepared, ['ready']), 'row');
            """);

        outcome.Results[0].Status.Should().Be("PASS", outcome.Results[0].Message);
    }

    [Fact]
    public void TheGlobalIsNoneOfTheThreeWptKnowsAbout()
    {
        // .any.js files branch on this to skip what their global cannot do; saying "not a window" is what
        // keeps url/historical.any.js off document.createElement.
        var outcome = Run("""
            test(() => {
                assert_equals(self, globalThis);
                assert_false(GLOBAL.isWindow());
                assert_false(GLOBAL.isWorker());
                assert_false(GLOBAL.isShadowRealm());
                assert_equals(typeof document, 'undefined');
                assert_equals(location.search, '');
            }, 'row');
            """);

        outcome.Results[0].Status.Should().Be("PASS", outcome.Results[0].Message);
    }

    [Fact]
    public void FetchReadsAVendoredResourceRelativeToTheFileThatAskedForIt()
    {
        var outcome = WptHarness.RunInline(
            """
            promise_test(() => fetch('resources/urltestdata-javascript-only.json')
                .then(response => response.json())
                .then(rows => assert_true(Array.isArray(rows) && rows.length > 0)), 'row');
            """,
            directory: "url");

        outcome.HarnessError.Should().BeNull();
        outcome.Results[0].Status.Should().Be("PASS", outcome.Results[0].Message);
    }

    [Fact]
    public void FetchingSomethingTheCorpusDoesNotHoldIsAHarnessErrorRatherThanATestFailure()
    {
        // A missing resource is a vendoring bug. Letting it become a rejected promise would turn it into a
        // failing test that an exclusion could then paper over, so it erupts for the whole file instead.
        var outcome = WptHarness.RunInline("promise_test(() => fetch('resources/nope.json'), 'row');", directory: "url");

        outcome.HarnessError.Should().NotBeNull();
        outcome.HarnessError.Should().Contain("url/resources/nope.json");
    }

    [Fact]
    public void AThrowOutOfTheTopLevelIsAHarnessErrorForTheWholeFile()
    {
        var outcome = Run("test(() => {}, 'row'); missingGlobal();");

        outcome.HarnessError.Should().NotBeNull();
        outcome.HarnessError.Should().Contain("missingGlobal");
        outcome.Results.Should().HaveCount(1, "what ran before the throw is still reported");
    }

    [Fact]
    public void MetaScriptsAreLoadedInTheOrderTheyAreDeclared()
    {
        // The vendored suites depend on this: encodings.js has to define encodings_table before the file's
        // own body reads it, and subset-tests.js has to have defined subsetTest before it is called.
        var outcome = WptHarness.Run("encoding/api-invalid-label.any.js");

        outcome.HarnessError.Should().BeNull();
        outcome.Results.Should().NotBeEmpty("its cases are built from encodings_table, which its first META script defines");
    }
}
#endif
