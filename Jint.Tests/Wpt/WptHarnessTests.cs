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

    /// <summary>The message recorded for a test whose whole body is <paramref name="body"/>.</summary>
    private static string? MessageOf(string body) => Run($"test(() => {body}, 'row');").Results[0].Message;

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
    // A legacy code *name* is accepted and mapped to the name it stands for, so it expects `NotFoundError`
    // rather than an exception literally called NOT_FOUND_ERR — upstream's `codename_name_map`.
    [InlineData(
        "assert_throws_dom('NOT_FOUND_ERR', () => { throw new DOMException('x', 'NotFoundError'); })",
        "assert_throws_dom('NOT_FOUND_ERR', () => { throw new DOMException('x', 'DataCloneError'); })")]
    [InlineData(
        "assert_throws_dom('DATA_CLONE_ERR', () => { throw new DOMException('x', 'DataCloneError'); })",
        "assert_throws_dom('DATA_CLONE_ERR', () => { throw new DOMException('x', 'NotFoundError'); })")]
    // The numeric form names a code, and the name that code stands for is checked with it.
    [InlineData(
        "assert_throws_dom(8, () => { throw new DOMException('x', 'NotFoundError'); })",
        "assert_throws_dom(8, () => { throw new DOMException('x', 'DataCloneError'); })")]
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
    [InlineData("assert_greater_than(2, 1)", "assert_greater_than(1, 2)")]
    // Strictly greater, so equal is not greater.
    [InlineData("assert_greater_than(0, -1)", "assert_greater_than(1, 1)")]
    // The type check is upstream's and is not decoration: `undefined > 0` is false, so without it the failure
    // would name the comparison rather than the value that could never have satisfied one, and `'10' > 9` is
    // true, so a string would pass a numeric assertion outright.
    [InlineData("assert_greater_than(10, 9)", "assert_greater_than('10', 9)")]
    [InlineData("assert_greater_than(1, 0)", "assert_greater_than(undefined, 0)")]
    // Greater *or equal*, which is the whole difference from the row above, and the same type check.
    [InlineData("assert_greater_than_equal(1, 1)", "assert_greater_than_equal(0, 1)")]
    [InlineData("assert_greater_than_equal(2, 1)", "assert_greater_than_equal('2', 1)")]
    // Within the tolerance, and the tolerance is inclusive.
    [InlineData("assert_approx_equals(10, 12, 3)", "assert_approx_equals(10, 12, 1)")]
    [InlineData("assert_approx_equals(10, 12, 2)", "assert_approx_equals(10, 12, 1.9)")]
    // The non-finite branch: `Math.abs(Infinity - Infinity)` is NaN and no comparison against epsilon can be
    // true, so a pair with no finite side falls through to same-value instead — which is why Infinity equals
    // itself here and does not equal its negation however large the tolerance.
    [InlineData("assert_approx_equals(Infinity, Infinity, 0)", "assert_approx_equals(Infinity, -Infinity, 1e308)")]
    [InlineData("assert_approx_equals(NaN, NaN, 0)", "assert_approx_equals(NaN, 1, Infinity)")]
    // ... and the type check, which is what stops a string that coerces from satisfying it.
    [InlineData("assert_approx_equals(1, 1, 0)", "assert_approx_equals('1', 1, 1)")]
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
    // The rejection form shares one implementation with the synchronous one, so the legacy code names it
    // accepts and the names it refuses are the same set — this row is what would catch the two drifting.
    [InlineData(
        "promise_rejects_dom(t, 'ABORT_ERR', Promise.reject(new DOMException('x', 'AbortError')))",
        "promise_rejects_dom(t, 'ABORT_ERR', Promise.reject(new DOMException('x', 'NotFoundError')))")]
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
        MessageOf("assert_object_equals({ a: 1 }, { a: 2 }, 'why')").Should().Be("property \"a\" expected 2 got 1 (why)");
        MessageOf("assert_object_equals({ a: 1, b: 2 }, { a: 1 })").Should().Be("unexpected property \"b\"");
        MessageOf("assert_object_equals({ a: 1 }, { a: 1, b: 2 })").Should().Be("expected property \"b\" missing");
        MessageOf("assert_object_equals(1, {})").Should().Be("value is 1, expected object");
        MessageOf("assert_own_property({}, 'x')").Should().Be("expected property \"x\" missing");
        MessageOf("assert_class_string([], 'URL')").Should().Be("expected \"[object URL]\" but got \"[object Array]\"");
        MessageOf("assert_in_array(4, [1, 2, 3])").Should().Be("value 4 not in array [1, 2, 3]");
        MessageOf("assert_greater_than(1, 2, 'why')").Should().Be("expected a number greater than 2 but got 1 (why)");
        MessageOf("assert_greater_than('10', 9)").Should().Be("expected a number but got a string");
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

    /// <summary>
    /// A stand-in for WebIDL's <c>QuotaExceededError</c>, deliberately shadowing the engine's own — the
    /// assertion reads <c>self.QuotaExceededError</c> at call time, exactly as upstream does, so replacing the
    /// global is all it takes.
    /// </summary>
    /// <remarks>
    /// It stays after Jint grew the real interface (https://webidl.spec.whatwg.org/#quotaexceedederror),
    /// because these rows are about the <i>shim</i>: a stand-in whose <c>quota</c> and <c>requested</c> the
    /// test dictates is what lets every arm be exercised from both sides — a wrong number, a failing
    /// predicate, the constructor call form, the wrong global — none of which an engine-thrown exception with
    /// two nulls could reach.
    /// <see cref="TheQuotaAssertionAcceptsTheEnginesOwnQuotaExceededError"/> is the other half, over the real
    /// one.
    /// </remarks>
    private const string QuotaPreamble = """
        globalThis.QuotaExceededError = class QuotaExceededError extends Error {
            constructor(requested, quota) {
                super('quota');
                this.name = 'QuotaExceededError';
                this.code = 22;
                this.requested = requested;
                this.quota = quota;
            }
        };
        var throwQuota = (requested, quota) => () => { throw new QuotaExceededError(requested, quota); };
        """;

    [Theory]
    // https://webidl.spec.whatwg.org/#quotaexceedederror — the interface carries `quota` and `requested`
    // beside the DOMException name and code, and each may be asserted as null, as a number, or by predicate.
    [InlineData("assert_throws_quotaexceedederror(throwQuota(9, 8), 9, 8)", "assert_throws_quotaexceedederror(throwQuota(9, 8), 7, 8)")]
    [InlineData("assert_throws_quotaexceedederror(throwQuota(9, 8), 9, 8)", "assert_throws_quotaexceedederror(throwQuota(9, 8), 9, 7)")]
    [InlineData(
        "assert_throws_quotaexceedederror(throwQuota(9, 8), r => r === 9, q => q === 8)",
        "assert_throws_quotaexceedederror(throwQuota(9, 8), r => r === 9, q => q === 7)")]
    [InlineData("assert_throws_quotaexceedederror(throwQuota(null, null), null, null)", "assert_throws_quotaexceedederror(() => {}, null, null)")]
    // The constructor form, and the same wrong-global check the DOMException assertion makes.
    [InlineData(
        "assert_throws_quotaexceedederror(QuotaExceededError, throwQuota(9, 8), 9, 8)",
        "assert_throws_quotaexceedederror(function QuotaExceededError() {}, throwQuota(9, 8), 9, 8)")]
    // A plain DOMException wearing the name and the legacy code is *not* the interface — the last assertion,
    // `e.constructor === constructor`, is what tells the two apart, and it is the whole reason upstream gave
    // QuotaExceededError an assertion of its own.
    [InlineData(
        "assert_throws_quotaexceedederror(throwQuota(9, 8), 9, 8)",
        "assert_throws_quotaexceedederror(() => { throw new DOMException('x', 'QuotaExceededError'); }, null, null)")]
    // The no-constructor form refuses a fifth argument, so a suite that spelled the constructor form wrong
    // cannot silently lose its description.
    [InlineData(
        "assert_throws_quotaexceedederror(throwQuota(9, 8), 9, 8, 'why')",
        "assert_throws_quotaexceedederror(throwQuota(9, 8), 9, 8, 'why', 'extra')")]
    public void TheQuotaExceededAssertionRecordsBothOutcomes(string passing, string failing)
    {
        StatusOf($"{QuotaPreamble}\ntest(() => {{ {passing} }}, 'row');").Should().Be("PASS");
        StatusOf($"{QuotaPreamble}\ntest(() => {{ {failing} }}, 'row');").Should().Be("FAIL");
    }

    /// <summary>
    /// The same assertion over the engine's own <c>QuotaExceededError</c>, with no stand-in installed — which
    /// is the arrangement the vendored <c>WebCryptoAPI/getRandomValues.any.js</c> rows actually run in.
    /// </summary>
    [Fact]
    public void TheQuotaAssertionAcceptsTheEnginesOwnQuotaExceededError()
    {
        // https://webidl.spec.whatwg.org/#quotaexceedederror — the interface, reached through the global the
        // engine installs beside DOMException, with the numbers the constructor was given.
        StatusOf("test(() => assert_throws_quotaexceedederror(() => { throw new QuotaExceededError('x', { quota: 8, requested: 9 }); }, 9, 8), 'row');")
            .Should().Be("PASS");

        // Both null is what an instance carries when nothing supplied them, and what getRandomValues throws.
        StatusOf("test(() => assert_throws_quotaexceedederror(() => { throw new QuotaExceededError('x'); }, null, null), 'row');")
            .Should().Be("PASS");
        StatusOf("test(() => assert_throws_quotaexceedederror(() => { crypto.getRandomValues(new Uint8Array(65537)); }, null, null), 'row');")
            .Should().Be("PASS");

        // The constructor call form finds the same object, since the global *is* the interface object.
        StatusOf("test(() => assert_throws_quotaexceedederror(QuotaExceededError, () => { throw new QuotaExceededError('x'); }, null, null), 'row');")
            .Should().Be("PASS");

        // And the failing side: a DOMException merely wearing the name is refused by the constructor check.
        StatusOf("test(() => assert_throws_quotaexceedederror(() => { throw new DOMException('x', 'QuotaExceededError'); }, null, null), 'row');")
            .Should().Be("FAIL");
    }

    [Fact]
    public void AnUnimplementedOptionalFeatureIsItsOwnOutcome()
    {
        // Upstream's third status. A truthy condition is a pass; a falsy one is neither a pass nor an
        // ordinary failure, because the test gave up rather than found something wrong. The driver has no
        // third bucket, so such a test still needs an exclusion — the status is what tells a reader why.
        StatusOf("test(() => assert_implements_optional(true, 'x'), 'row');").Should().Be("PASS");
        StatusOf("test(() => assert_implements_optional(false, 'x'), 'row');").Should().Be("PRECONDITION_FAILED");

        // And it is an AssertionError, which is what stops a suite's own `catch` from mistaking it for the
        // operation's rejection and re-classifying it as a failure.
        StatusOf("test(() => { try { assert_implements_optional(false, 'x'); } catch (e) { assert_true(e instanceof AssertionError); throw e; } }, 'row');")
            .Should().Be("PRECONDITION_FAILED");
    }

    [Fact]
    public void AMissingRequiredFeatureIsAnOrdinaryFailure()
    {
        // The sibling of the test above, and the pairing is the point: upstream has two assertions because
        // the specification has two kinds of feature. `assert_implements` is for one the specification
        // *requires*, so a falsy condition is an ordinary FAIL that has to be excluded and explained —
        // never the PRECONDITION_FAILED that would quietly demote it. urlpattern's hasRegExpGroups suite is
        // what reaches for it, guarding its whole body on the member being there at all.
        StatusOf("test(() => assert_implements('x' in { x: 1 }, 'why'), 'row');").Should().Be("PASS");
        StatusOf("test(() => assert_implements(false, 'why'), 'row');").Should().Be("FAIL");

        // Truthiness, not identity: upstream's body is `assert(!!condition, …)`, so a suite may pass it the
        // member it is probing for rather than a boolean.
        StatusOf("test(() => assert_implements(0, 'why'), 'row');").Should().Be("FAIL");
        StatusOf("test(() => assert_implements('non-empty', 'why'), 'row');").Should().Be("PASS");

        // And it is an AssertionError like every other, which is what a suite's own classifying `catch`
        // relies on.
        StatusOf("test(() => { try { assert_implements(false, 'why'); } catch (e) { assert_true(e instanceof AssertionError); assert_false(e instanceof OptionalFeatureUnsupportedError); throw e; } }, 'row');")
            .Should().Be("FAIL");
    }

    [Fact]
    public void AssertionErrorIsAGlobalTheSuitesCanBranchOn()
    {
        // The WebCryptoAPI suites wrap an operation in try/catch and re-throw only `err instanceof
        // AssertionError`, so that a failed assertion inside the try is reported as itself rather than as
        // "the operation threw". Upstream exposes the constructor for exactly that, and a shim that kept it
        // private turned every such file into a wall of ReferenceErrors.
        StatusOf("test(() => { try { null.x; } catch (e) { assert_false(e instanceof AssertionError); } }, 'row');")
            .Should().Be("PASS");
        StatusOf("test(() => { try { assert_true(false); } catch (e) { if (e instanceof AssertionError) { throw e; } assert_unreached('classified wrong'); } }, 'row');")
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
    public void ADomExceptionAssertionRefusesANameItsTableCannotHold()
    {
        // https://webidl.spec.whatwg.org/#dfn-error-names-table — upstream's `name_code_map` is a closed set,
        // and a name it does not hold is a bug in the test rather than a name whose legacy code happens to be
        // 0. Accepting one is the quiet wrong green this exists to stop: `assert_throws_dom('NotFundError',
        // …)` would be satisfied by an implementation that threw exactly that typo, and both sides would
        // agree while both were wrong. The refusal is upstream's message verbatim.
        MessageOf("assert_throws_dom('NotFundError', () => { throw new DOMException('x', 'NotFundError'); })")
            .Should().Be("Test bug: unrecognized DOMException code name or name \"NotFundError\" passed to assert_throws_dom()");
        StatusOf("test(() => assert_throws_dom('NotFundError', () => { throw new DOMException('x', 'NotFundError'); }), 'row');")
            .Should().Be("FAIL");

        // `QUOTA_EXCEEDED_ERR` left `codename_name_map` when the interface moved out, so the legacy code name
        // is refused as a name like any other rather than pointed anywhere.
        MessageOf("assert_throws_dom('QUOTA_EXCEEDED_ERR', () => { throw new DOMException('x', 'QuotaExceededError'); })")
            .Should().Be("Test bug: unrecognized DOMException code name or name \"QUOTA_EXCEEDED_ERR\" passed to assert_throws_dom()");

        // The numeric form has two refusals of its own: 0 names no single error, and a code outside the table
        // names none at all.
        MessageOf("assert_throws_dom(0, () => { throw new DOMException('x', 'EncodingError'); })")
            .Should().Be("Test bug: ambiguous DOMException code 0 passed to assert_throws_dom()");
        MessageOf("assert_throws_dom(2, () => { throw new DOMException('x', 'NotFoundError'); })")
            .Should().Be("Test bug: unrecognized DOMException code \"2\" passed to assert_throws_dom()");

        // And an expectation that is neither has to be caught before the two branches, or it would reach
        // neither of them and leave nothing at all to compare the exception against.
        MessageOf("assert_throws_dom(undefined, () => { throw new DOMException('x', 'NotFoundError'); })")
            .Should().Be("undefined is not a number or string");
    }

    [Fact]
    public void QuotaExceededErrorIsSentToItsOwnAssertionRatherThanMatchedAsADomExceptionName()
    {
        // https://webidl.spec.whatwg.org/#quotaexceedederror — since it became an interface of its own it is
        // in neither of upstream's tables, so the name and the legacy code 22 are both refused and named at
        // assert_throws_quotaexceedederror. The exception the body throws is precisely the shape that would
        // have satisfied the old table — a DOMException called QuotaExceededError, which a script can still
        // build by hand — so this pins the refusal and not merely a mismatch.
        const string sendItThere = "Test bug: QuotaExceededError needs to be tested for using assert_throws_quotaexceedederror()";

        MessageOf("assert_throws_dom('QuotaExceededError', () => { throw new DOMException('x', 'QuotaExceededError'); })")
            .Should().Be(sendItThere);
        MessageOf("assert_throws_dom(22, () => { throw new DOMException('x', 'QuotaExceededError'); })")
            .Should().Be(sendItThere);
        StatusOf("test(() => assert_throws_dom('QuotaExceededError', () => { throw new DOMException('x', 'QuotaExceededError'); }), 'row');")
            .Should().Be("FAIL");
    }

    [Fact]
    public void TheRejectionFormRefusesWhatTheSynchronousFormRefuses()
    {
        // Both forms share `assert_throws_dom_impl`, which is what keeps the accepted set and the refused set
        // from drifting apart — and why upstream's message names `assert_throws_dom()` from either entrance.
        var outcome = Run(
            "promise_test(t => promise_rejects_dom(t, 'NotFundError', Promise.reject(new DOMException('x', 'NotFundError'))), 'row');");

        outcome.HarnessError.Should().BeNull();
        outcome.Results[0].Status.Should().Be("FAIL");
        outcome.Results[0].Message
            .Should().Be("Test bug: unrecognized DOMException code name or name \"NotFundError\" passed to assert_throws_dom()");
    }

    [Fact]
    public void ABodyThatThrewNothingIsReportedAsSuchEvenWhenTheNameWouldHaveBeenRefused()
    {
        // The refusals sit inside the catch, where upstream's are, so "it did not throw" outranks "the name
        // is not one I hold". Hoisting them above the try would silently reclassify every did-not-throw
        // failure in a suite that named something the table does not carry.
        MessageOf("assert_throws_dom('NotFundError', () => {})").Should().Be("did not throw");
        MessageOf("assert_throws_dom(22, () => {}, 'why')").Should().Be("did not throw (why)");
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
    public void StepTimeoutSchedulesOnTheEnginesOwnTimerRatherThanOnAHarnessOne()
    {
        // The streams corpus reaches for `step_timeout` 45 times, through `delay()` in
        // resources/test-utils.js and directly. It has to be the shipped TimerQueue that runs the callback,
        // or the suites would be exercising a second implementation written for the harness — so the
        // assertion is on the id `setTimeout` handed back, which `clearTimeout` then has to recognise. A
        // harness-private queue would satisfy neither half.
        var outcome = Run("""
            var cancelled = false;
            var id = step_timeout(() => { cancelled = true; }, 0);
            clearTimeout(id);
            promise_test(() => new Promise(resolve => step_timeout(resolve, 1))
                .then(() => new Promise(resolve => step_timeout(resolve, 1)))
                .then(() => assert_false(cancelled, 'the engine cleared the timer step_timeout created')),
                'row');
            """);

        outcome.HarnessError.Should().BeNull();
        outcome.Results[0].Status.Should().Be("PASS", outcome.Results[0].Message);
    }

    [Fact]
    public void StepTimeoutForwardsTheArgumentsAfterTheDelay()
    {
        var outcome = Run("""
            var t = async_test('async');
            step_timeout(t.step_func((a, b) => {
                assert_equals(a, 'first');
                assert_equals(b, 'second');
                t.done();
            }), 0, 'first', 'second');
            """);

        outcome.HarnessError.Should().BeNull();
        outcome.Results[0].Status.Should().Be("PASS", outcome.Results[0].Message);
    }

    [Fact]
    public void TheTestBoundStepTimeoutFailsItsOwnTestRatherThanEruptingIntoThePump()
    {
        // `t.step_timeout` is what the corpus actually uses (42 of the 45 sites). The whole difference from
        // the bare form is that the callback runs as a step of the test, so a throw is recorded against it —
        // where a raw setTimeout callback would erupt out of whatever happened to be pumping the event loop
        // and be reported as a harness error for the file.
        var outcome = Run("""
            var t = async_test('async');
            t.step_timeout(() => { assert_true(false, 'inside the timer'); t.done(); }, 0);
            t.step_timeout(() => t.done(), 1);
            """);

        outcome.HarnessError.Should().BeNull();
        outcome.Results[0].Status.Should().Be("FAIL");
        outcome.Results[0].Message.Should().Contain("inside the timer");
    }

    [Fact]
    public void TheTestBoundStepTimeoutForwardsItsArgumentsAndRunsWithTheTestAsThis()
    {
        var outcome = Run("""
            var t = async_test('async');
            t.step_timeout(function (a) {
                assert_equals(this, t, 'the callback runs with the test as `this`');
                assert_equals(a, 'forwarded');
                this.done();
            }, 0, 'forwarded');
            """);

        outcome.HarnessError.Should().BeNull();
        outcome.Results[0].Status.Should().Be("PASS", outcome.Results[0].Message);
    }

    [Theory]
    [InlineData("test(t => { t.add_cleanup(() => { globalThis.ran = true; }); }, 'row');")]
    [InlineData("promise_test(t => { t.add_cleanup(() => { globalThis.ran = true; }); return Promise.resolve(); }, 'row');")]
    [InlineData("var t = async_test('row'); t.add_cleanup(() => { globalThis.ran = true; }); t.done();")]
    public void ACleanupRunsWhateverKindOfTestRegisteredIt(string registration)
    {
        // The streams corpus makes this load-bearing rather than tidy: its `patched-global` suites replace
        // `Object.prototype.then`, `Promise.prototype.then` and `ReadableStream` itself, so a cleanup that
        // did not run would take every later test in the file down with it. All three entry points funnel
        // through Test.prototype.complete, and this is what would catch one of them growing its own path.
        // The check is itself a promise_test so that it is reached after the registration above has finished
        // — a plain test() runs at file scope, which is before a promise_test's body has run at all.
        var outcome = Run(
            $"globalThis.ran = false;\n{registration}\npromise_test(() => {{ assert_true(globalThis.ran); return Promise.resolve(); }}, 'cleanup ran');");

        outcome.HarnessError.Should().BeNull();
        outcome.Results.Last().Status.Should().Be("PASS", outcome.Results.Last().Message);
    }

    [Fact]
    public void ACleanupRunsBeforeTheNextTestSeesTheGlobalItRestored()
    {
        // Ordering, not merely "it ran at some point": the restoration has to be complete by the time the
        // next test in the file starts, which is the property `patched-global.any.js` depends on.
        var outcome = Run("""
            var original = Object.prototype.toString;
            test(t => {
                t.add_cleanup(() => { Object.prototype.toString = original; });
                Object.prototype.toString = () => 'patched';
                assert_equals({}.toString(), 'patched');
            }, 'patches');
            test(() => assert_equals(Object.prototype.toString, original), 'sees it restored');
            """);

        outcome.HarnessError.Should().BeNull();
        outcome.Results.Select(r => r.Status).Should().Equal("PASS", "PASS");
    }

    [Fact]
    public void EveryCleanupRunsEvenWhenAnEarlierOneThrows()
    {
        // They undo *different* pieces of shared state, so skipping the rest would poison the file exactly as
        // not running them at all would. The throw is attributed to the test that registered it, and only
        // because that test was otherwise passing.
        var outcome = Run("""
            globalThis.second = false;
            test(t => {
                t.add_cleanup(() => { throw new Error('cleanup blew up'); });
                t.add_cleanup(() => { globalThis.second = true; });
            }, 'row');
            test(() => assert_true(globalThis.second), 'the second cleanup ran anyway');
            """);

        outcome.HarnessError.Should().BeNull();
        outcome.Results[0].Status.Should().Be("FAIL");
        outcome.Results[0].Message.Should().Contain("cleanup blew up");
        outcome.Results[1].Status.Should().Be("PASS", outcome.Results[1].Message);
    }

    [Fact]
    public void AThrowingCleanupDoesNotReplaceTheFailureThatExplainsTheTest()
    {
        // A test that failed and then left wreckage behind must still report what it was that failed, or the
        // exclusion table would be triaged from the wrong message.
        var outcome = Run("""
            test(t => {
                t.add_cleanup(() => { throw new Error('cleanup blew up'); });
                assert_equals('got', 'want', 'the real failure');
            }, 'row');
            """);

        outcome.Results[0].Status.Should().Be("FAIL");
        outcome.Results[0].Message.Should().Be("expected \"want\" but got \"got\" (the real failure)");
    }

    [Fact]
    public void ACleanupRunsOnceRatherThanOncePerCompletionAttempt()
    {
        // `done()` calls `complete()`, and a suite may call `done()` twice; a cleanup that restored a saved
        // value would be harmless run twice, but one that counts, closes or releases is not. What this pins
        // is the phase guard in `complete()`, which is the only thing between the two calls.
        var outcome = Run("""
            globalThis.runs = 0;
            var t = async_test('row');
            t.add_cleanup(() => { globalThis.runs += 1; });
            t.done();
            t.done();
            test(() => assert_equals(globalThis.runs, 1), 'ran once');
            """);

        outcome.HarnessError.Should().BeNull();
        outcome.Results[1].Status.Should().Be("PASS", outcome.Results[1].Message);
    }

    [Fact]
    public void AFailedStepEndsTheTestAndRunsItsCleanups()
    {
        // Upstream's `step` catch sets the status and then calls `done()`, so a failed step is the end of the
        // test: the cleanups run there and then, and every later step is a no-op. This shim used to leave the
        // test running instead, which was survivable only because no vendored file threw out of an
        // `async_test` body — and then the hr-time and user-timing corpora arrived, where reaching for an
        // absent `PerformanceObserver` does exactly that. Such a test stayed in `outstanding` for ever and the
        // driver reported the whole file as stalled, hiding one nameable failure behind a dead run.
        var outcome = Run("""
            globalThis.ran = false;
            globalThis.later = false;
            var t = async_test('row');
            t.add_cleanup(() => { globalThis.ran = true; });
            t.step(() => assert_true(false, 'deliberate'));
            var atFailure = globalThis.ran;
            t.step(() => { globalThis.later = true; });
            test(() => {
                assert_true(atFailure, 'the failing step must have run the cleanups');
                assert_false(globalThis.later, 'a step after the failure must not run');
            }, 'ordering');
            """);

        outcome.HarnessError.Should().BeNull();
        outcome.Results[0].Status.Should().Be("FAIL");
        outcome.Results[1].Status.Should().Be("PASS", outcome.Results[1].Message);
    }

    [Fact]
    public void AnAsyncTestWhoseBodyThrowsStopsBeingOutstanding()
    {
        // The half of the rule above that the driver depends on: nothing calls `done()` here, so if the throw
        // did not end the test the file would never complete and this would come back a harness error naming a
        // stalled run rather than a FAIL naming the reference error.
        var outcome = Run("async_test(() => { noSuchGlobal(); }, 'row');");

        outcome.HarnessError.Should().BeNull();
        outcome.Results.Should().HaveCount(1);
        outcome.Results[0].Status.Should().Be("FAIL");
        outcome.Results[0].Message.Should().Contain("noSuchGlobal");
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
    public void AnExceptionEscapingACallbackIsAHarnessErrorForTheWholeFile()
    {
        // The driver's engines carry a DiagnosticsSink, so such an exception no longer erupts from the pump —
        // and this is the rule that keeps it from disappearing instead. It is upstream's own: testharness.js
        // fails a run whose global `onerror` fired unless the file declared allow_uncaught_exception.
        var outcome = Run("""
            test(() => {}, 'row');
            queueMicrotask(() => { throw new Error('escaped'); });
            """);

        outcome.HarnessError.Should().NotBeNull();
        outcome.HarnessError.Should().Contain("allow_uncaught_exception");
        outcome.HarnessError.Should().Contain("escaped");

        // The tests that did run are still reported, exactly as they are for a top-level throw.
        outcome.Results.Should().HaveCount(1);
    }

    [Fact]
    public void AFileThatDeclaresAllowUncaughtExceptionIsNotFailedByOne()
    {
        // The other half, and the reason html/webappapis/microtask-queuing/queue-microtask-exceptions.any.js
        // can be vendored: its whole subject is a callback that throws, and it says so.
        var outcome = Run("""
            setup({ allow_uncaught_exception: true });
            async_test(t => {
                self.addEventListener('error', t.step_func_done(ev => {
                    assert_equals(ev.error.message, 'expected');
                }));
                queueMicrotask(() => { throw new Error('expected'); });
            }, 'row');
            """);

        outcome.HarnessError.Should().BeNull();
        outcome.Results.Should().HaveCount(1);
        outcome.Results[0].Status.Should().Be("PASS");
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
    public void SingleTestMakesTheWholeFileOneTestThatDoneFinishes()
    {
        // `setup({single_test: true})` is what four of the html/webappapis/timers files declare, and a shim
        // that ignored it would register nothing at all and report those files as empty runs.
        var outcome = Run("""
            setup({ single_test: true });
            assert_equals(1, 1);
            done();
            """);

        outcome.HarnessError.Should().BeNull();
        outcome.Results.Should().HaveCount(1);
        outcome.Results[0].Status.Should().Be("PASS", outcome.Results[0].Message);
        outcome.Results[0].Name.Should().Be("inline.any.js", "the shim names the file's one test after the file");
    }

    [Fact]
    public void ASingleTestFileThatNeverCallsDoneIsAStalledRunRatherThanAPass()
    {
        // The other half of the mode: the one test is asynchronous, so it is finished by `done()` and by
        // nothing else. A shim that completed it at the end of the file would report a green run for a file
        // whose timer never fired.
        var outcome = Run("setup({ single_test: true });");

        outcome.HarnessError.Should().NotBeNull();
        outcome.HarnessError.Should().Contain("inline.any.js");
    }

    [Fact]
    public void ACallbackThatThrowsAfterASingleTestFileIsDoneIsNotAHarnessError()
    {
        // Upstream's completion boundary, and the reason the four `single_test` timer files are deterministic:
        // `testharness.js`'s global error handler returns without recording anything once the file's one test
        // has a result (`tests.tests[0].phase >= HAS_RESULT`). Three of those four arm a guard timer —
        // `setTimeout(assert_unreached, 10)` in negative-settimeout.any.js — that a browser lets fire and
        // ignores, and that the driver used to turn into a harness error for a file that had passed.
        //
        // Deterministic by ordering rather than by timing, which is the rule this whole class is written to:
        // `done()` runs at file scope, so the file's one test has its result before the timer is even armed,
        // and a zero delay is due the moment it is — so the drain on the way out of `Engine.Execute` runs it
        // every time, which is window 1 of the three the driver used to leave open.
        var outcome = Run("""
            setup({ single_test: true });
            done();
            setTimeout(() => { throw new Error('late'); }, 0);
            """);

        outcome.HarnessError.Should().BeNull();
        outcome.Results.Should().HaveCount(1);
        outcome.Results[0].Status.Should().Be("PASS", outcome.Results[0].Message);
    }

    [Fact]
    public void ACallbackThatThrowsBeforeASingleTestFileIsDoneStillIsAHarnessError()
    {
        // The other half, and what stops the boundary above from being a way to go quiet. The predicate is
        // upstream's "the file's one test has a result" and never "nothing is outstanding" — the latter would
        // also silence AnExceptionEscapingACallbackIsAHarnessErrorForTheWholeFile above, whose file has an
        // empty outstanding list from its first line.
        var outcome = Run("""
            setup({ single_test: true });
            setTimeout(() => { throw new Error('early'); }, 0);
            setTimeout(done, 0);
            """);

        outcome.HarnessError.Should().NotBeNull();
        outcome.HarnessError.Should().Contain("allow_uncaught_exception");
        outcome.HarnessError.Should().Contain("early");
    }

    [Fact]
    public void TheCompletionBoundaryIsPerFileRatherThanPerRun()
    {
        // A file that never declared `single_test` has no file test to have a result, so nothing about it is
        // forgiven however finished it looks. This is the same rule as the test above stated from the other
        // end, and it is what a flag reset per run rather than per report would get wrong.
        var outcome = Run("""
            test(() => {}, 'row');
            setTimeout(() => { throw new Error('late'); }, 0);
            """);

        outcome.HarnessError.Should().NotBeNull();
        outcome.HarnessError.Should().Contain("late");
    }

    [Fact]
    public void ASynchronousXmlHttpRequestReadsAVendoredResource()
    {
        var outcome = WptHarness.RunInline("""
            test(() => {
                var xhr = new XMLHttpRequest();
                xhr.open('GET', 'resources/urltestdata.json', false);
                xhr.send(null);
                assert_equals(xhr.status, 200);
                assert_true(xhr.responseText.length > 1000);
            }, 'row');
            """,
            directory: "url");

        outcome.HarnessError.Should().BeNull();
        outcome.Results[0].Status.Should().Be("PASS", outcome.Results[0].Message);
    }

    [Fact]
    public void AnAsynchronousXmlHttpRequestFiresLoadAfterTheScriptThatSentIt()
    {
        // `xhr.onload = …` is assigned *after* `send()` in every suite that uses one, so a load event
        // dispatched inside `send()` would never reach a handler.
        var outcome = WptHarness.RunInline("""
            async_test(t => {
                var xhr = new XMLHttpRequest();
                xhr.open('GET', 'resources/urltestdata.json');
                xhr.send(null);
                assert_equals(xhr.readyState, 4, 'the read itself is synchronous');
                xhr.onload = t.step_func_done(() => {
                    assert_true(xhr.responseText.length > 1000);
                });
            }, 'row');
            """,
            directory: "url");

        outcome.HarnessError.Should().BeNull();
        outcome.Results[0].Status.Should().Be("PASS", outcome.Results[0].Message);
    }

    [Theory]
    // Everything the reader is not, each of which has to arrive as a failing test naming what was asked for —
    // never as a pass, a hang, or a CLR exception that takes the whole file down.
    [InlineData("xhr.open('POST', 'resources/urltestdata.json'); xhr.send(null);", "supports GET")]
    [InlineData("xhr.open('GET', 'resources/single-byte-raw.py?label=x'); xhr.send(null);", "holds no")]
    [InlineData("xhr.open('GET', '../../../etc/passwd'); xhr.send(null);", "holds no")]
    [InlineData("xhr.open('GET', 'resources/urltestdata.json'); xhr.send('body');", "sends no request body")]
    [InlineData("xhr.send(null);", "before open()")]
    [InlineData("xhr.open('GET', 'resources/urltestdata.json'); xhr.setRequestHeader('X', 'y');", "cannot set the header")]
    public void AnXmlHttpRequestRefusesWhatItCannotServe(string body, string expected)
    {
        var outcome = WptHarness.RunInline($"test(() => {{ var xhr = new XMLHttpRequest(); {body} }}, 'row');", directory: "url");

        outcome.HarnessError.Should().BeNull("a refusal is a failing test, not a dead file");
        outcome.Results[0].Status.Should().Be("FAIL");
        outcome.Results[0].Message.Should().Contain(expected);
    }

    [Theory]
    // One suite that reads its corpus with `fetch` and one that is about `Response` itself, because the
    // answer has to be the same object in both: every engine the driver builds carries the object model.
    [InlineData("url", "resources/urltestdata.json")]
    [InlineData("fetch/api/resources", "data.json")]
    public void TheResourceLoaderAnswersWithARealResponse(string directory, string reference)
    {
        // The `fetch/api/response/` suites read `response.body` and expect a ReadableStream. A duck-typed
        // object has no `body` at all, and `assert_throws_js(TypeError, () => response.body.getReader())`
        // passes against `undefined` — a green row that proves nothing, which is what this rules out.
        var outcome = WptHarness.RunInline($$"""
            promise_test(async () => {
                const response = await fetch('{{reference}}');
                assert_true(response instanceof Response, 'a real Response');
                assert_not_equals(response.body, null, 'with a real body');
                const json = await response.json();
                assert_equals(typeof json, 'object');
            }, 'row');
            """,
            directory);

        outcome.HarnessError.Should().BeNull();
        outcome.Results[0].Status.Should().Be("PASS", outcome.Results[0].Message);
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
