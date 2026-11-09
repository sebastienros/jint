#nullable enable

using Jint.Runtime;

namespace Jint.Tests.Runtime;

/// <summary>
/// The AggregateError <c>Promise.any</c> rejects with is "a newly created AggregateError object" whose <c>errors</c>
/// is defined from <c>CreateArrayFromList(errors)</c> (https://tc39.es/ecma262/#sec-performpromiseany): no iteration,
/// no call to the <c>AggregateError</c> constructor, so a script that replaced <c>Array.prototype[Symbol.iterator]</c>
/// or the <c>AggregateError</c> global observes nothing (issue #4478). Every expectation was cross-checked in Node.
/// </summary>
public class PromiseAnyAggregateErrorTests
{
    /// <summary>
    /// The three ways <c>Promise.any</c> reaches the rejection. Every iterable is built before a test hijacks the
    /// Array iterator, because building a <c>Set</c> from an array iterates it.
    /// </summary>
    private const string Setup = """
        // settles through the reject element function, once the iteration is long done
        var asyncInput = { *[Symbol.iterator]() { yield Promise.reject(1); yield Promise.reject(2); } };
        // every element rejects inside the combinator's own call to then, so the iteration's end raises the error
        var SyncPromise = function (executor) { return new Promise(executor); };
        SyncPromise.resolve = function (v) { return { then(onFulfilled, onRejected) { onRejected(v); } }; };
        var syncInput = new Set(['a', 'b']);
        // nothing to iterate: the iteration's end raises the error
        var emptyInput = new Set();
        var OriginalAggregateErrorPrototype = AggregateError.prototype;
        """;

    private const string AsyncRejections = "Promise.any(asyncInput)";
    private const string SynchronousRejections = "Promise.any.call(SyncPromise, syncInput)";
    private const string EmptyIterable = "Promise.any(emptyInput)";

    private const string Hijack = "Array.prototype[Symbol.iterator] = function* () { yield 'hijacked'; };";

    private static string Run(string script)
    {
        var engine = new Engine();
        engine.Execute(Setup);
        return engine.Evaluate(script).UnwrapIfPromise().AsString();
    }

    [TestCase(AsyncRejections, "2:1,2")]
    [TestCase(SynchronousRejections, "2:a,b")]
    [TestCase(EmptyIterable, "0:")]
    public void AReplacedArrayIteratorIsNotObserved(string call, string expected)
    {
        Run($$"""
            {{Hijack}}
            {{call}}.catch(e => e.errors.length + ':' + Array.prototype.join.call(e.errors))
            """).Should().Be(expected);
    }

    [TestCase(AsyncRejections, "2:1,2")]
    [TestCase(SynchronousRejections, "2:a,b")]
    [TestCase(EmptyIterable, "0:")]
    public void AMissingArrayIteratorIsNotObserved(string call, string expected)
    {
        Run($$"""
            delete Array.prototype[Symbol.iterator];
            {{call}}.catch(e => e.errors.length + ':' + Array.prototype.join.call(e.errors))
            """).Should().Be(expected);
    }

    /// <summary>
    /// The issue's own repro, awaited from an async function: the same settlement reaches the caller through
    /// <c>await</c> rather than <c>catch</c>.
    /// </summary>
    [Test]
    public void AReplacedArrayIteratorIsNotObservedWhenAwaited()
    {
        Run($$"""
            {{Hijack}}
            (async function () {
                try { await Promise.any(asyncInput); return 'resolved'; }
                catch (e) { return Array.prototype.join.call(e.errors); }
            })()
            """).Should().Be("1,2");
    }

    [TestCase(AsyncRejections)]
    [TestCase(SynchronousRejections)]
    [TestCase(EmptyIterable)]
    public void ErrorsIsAWritableNonEnumerableConfigurableOwnDataProperty(string call)
    {
        Run($$"""
            {{Hijack}}
            {{call}}.catch(e => {
                var d = Object.getOwnPropertyDescriptor(e, 'errors');
                return [d.writable, d.enumerable, d.configurable, 'value' in d, Array.isArray(d.value),
                    Object.getPrototypeOf(d.value) === Array.prototype, Object.isExtensible(d.value)].join();
            })
            """).Should().Be("true,false,true,true,true,true,true");
    }

    [TestCase(AsyncRejections)]
    [TestCase(SynchronousRejections)]
    [TestCase(EmptyIterable)]
    public void TheErrorIsAnAggregateErrorOfTheRealmWhateverTheGlobalHoldsNow(string call)
    {
        Run($$"""
            var constructed = 0;
            globalThis.AggregateError = function () { constructed++; return { fake: true }; };
            {{call}}.catch(e => [
                Object.getPrototypeOf(e) === OriginalAggregateErrorPrototype,
                e instanceof Error,
                Object.prototype.toString.call(e),
                constructed,
                Array.isArray(e.errors)].join())
            """).Should().Be("true,true,[object Error],0,true");
    }

    [TestCase(AsyncRejections)]
    [TestCase(SynchronousRejections)]
    [TestCase(EmptyIterable)]
    public void ARedefinedPrototypeConstructorIsNotCalled(string call)
    {
        Run($$"""
            var called = 0;
            Object.defineProperty(OriginalAggregateErrorPrototype, 'constructor', { value: function () { called++; } });
            {{call}}.catch(e => called + ':' + e.name)
            """).Should().Be("0:AggregateError");
    }

    /// <summary>
    /// <c>CreateArrayFromList</c> defines each element, so an inherited setter on the array prototype is not run.
    /// </summary>
    [Test]
    public void AnInheritedIndexSetterIsNotRun()
    {
        Run("""
            var hits = 0;
            Object.defineProperty(Array.prototype, '0', { set(v) { hits++; }, configurable: true });
            Promise.any(asyncInput).catch(e => hits + ':' + Object.prototype.hasOwnProperty.call(e.errors, '0') + ':' + e.errors[0])
            """).Should().Be("0:true:1");
    }

    [Test]
    public void ErrorsKeepTheirIndexWhenTheyArriveOutOfOrder()
    {
        Run("""
            function delay(v) { var p = Promise.resolve(v); for (var i = 0; i < v; i++) p = p.then(x => x); return p; }
            var ps = [3, 1, 2].map(v => delay(v).then(x => { throw x; }));
            Array.prototype[Symbol.iterator] = function* () { yield 'hijacked'; };
            Promise.any({ *[Symbol.iterator]() { yield ps[0]; yield ps[1]; yield ps[2]; } })
                .catch(e => Array.prototype.join.call(e.errors))
            """).Should().Be("3,1,2");
    }

    [Test]
    public void EachRejectionGetsItsOwnErrorAndItsOwnArray()
    {
        Run("""
            Promise.any(emptyInput).catch(first =>
                Promise.any(emptyInput).catch(second => (first !== second) + ',' + (first.errors !== second.errors)))
            """).Should().Be("true,true");
    }

    /// <summary>
    /// The <c>errors</c> array is still bounded by <c>Constraints.MaxArraySize</c>: building it directly must not
    /// open a way around the limit that going through the constructor (<c>ArrayCreate</c>) always enforced.
    /// </summary>
    [TestCase("Promise.any({ *[Symbol.iterator]() { yield Promise.reject(1); yield Promise.reject(2); yield Promise.reject(3); } })")]
    [TestCase("Promise.any.call(SyncPromise, { *[Symbol.iterator]() { yield 'a'; yield 'b'; yield 'c'; } })")]
    public void TheErrorsArrayHonoursMaxArraySize(string call)
    {
        var engine = new Engine(options => options.Constraints.MaxArraySize = 2);
        engine.Execute("""
            var SyncPromise = function (executor) { return new Promise(executor); };
            SyncPromise.resolve = function (v) { return { then(onFulfilled, onRejected) { onRejected(v); } }; };
            """);

        var thrown = Caught.Exception(() => engine.Evaluate(call + ".catch(e => e.errors.length)").UnwrapIfPromise());

        thrown.Should().BeOfType<MemoryLimitExceededException>();
    }

    /// <summary>
    /// The limit is a ceiling, not a refusal: as many errors as it allows are still delivered.
    /// </summary>
    [Test]
    public void TheErrorsArrayAtMaxArraySizeIsDelivered()
    {
        var engine = new Engine(options => options.Constraints.MaxArraySize = 2);

        engine.Evaluate("""
            Promise.any({ *[Symbol.iterator]() { yield Promise.reject(1); yield Promise.reject(2); } })
                .catch(e => e.errors.length + ':' + Array.prototype.join.call(e.errors))
            """).UnwrapIfPromise().AsString().Should().Be("2:1,2");
    }

    /// <summary>
    /// The constructor is the one place that does iterate its argument, as
    /// https://tc39.es/ecma262/#sec-aggregate-error-constructor asks; building the error directly must not change it.
    /// </summary>
    [Test]
    public void TheAggregateErrorConstructorStillIteratesItsErrorsArgument()
    {
        Run($$"""
            var input = [1, 2];
            {{Hijack}}
            Array.prototype.join.call(new AggregateError(input).errors)
            """).Should().Be("hijacked");
    }
}
