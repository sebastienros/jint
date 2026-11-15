#nullable enable

namespace Jint.Tests.Runtime;

/// <summary>
/// Pins <a href="https://tc39.es/ecma402/#sec-createstringlistfromiterable">StringListFromIterable</a>, which
/// <c>Intl.ListFormat.prototype.format</c> and <c>formatToParts</c> turn their argument into a list with:
/// <a href="https://tc39.es/ecma262/#sec-getiterator">GetIterator(iterable, sync)</a> once, then
/// <a href="https://tc39.es/ecma262/#sec-iteratorstepvalue">IteratorStepValue</a> until done, and a non-String value
/// closes the iterator with <a href="https://tc39.es/ecma262/#sec-iteratorclose">IteratorClose</a> under the TypeError,
/// which wins over anything the close itself throws.
/// <para>
/// The loop read <c>next</c> off the iterator on every step instead of calling the [[NextMethod]]
/// <a href="https://tc39.es/ecma262/#sec-getiteratorfrommethod">GetIteratorFromMethod</a> read once
/// (sebastienros/jint#4438). It also walked a string by UTF-16 code unit and an array by index without calling either's
/// <c>@@iterator</c>, treated an object with no <c>@@iterator</c> as array-like, ended the list quietly where a step or
/// <c>@@iterator</c> answered a non-object, and let a <c>return</c> that threw replace the TypeError. Every expected value
/// is Node 26's, each case in a fresh realm; <c>formatToParts</c> answers its parts' values joined, so both methods share
/// one.
/// </para>
/// </summary>
public class IntlListFormatIterableTests
{
    private const string Harness = """
        function describe(e) { return e instanceof Error ? e.name : String(e); }

        // Formats `list` with an en Intl.ListFormat through `method`, 'format' or 'formatToParts' (whose part values are
        // joined, so both answer the same string), or answers 'caught:<error name>'.
        function fmt(method, list) {
            const lf = new Intl.ListFormat('en');
            try {
                return method === 'format' ? lf.format(list) : lf.formatToParts(list).map(p => p.value).join('');
            } catch (e) {
                return 'caught:' + describe(e);
            }
        }

        // An iterable whose iterator is `it`.
        function iterableOf(it) { return { [Symbol.iterator]() { return it; } }; }

        // A `next` stepping through `values`, then done.
        function stepper(values) {
            let i = 0;
            return function () { return i < values.length ? { value: values[i++], done: false } : { value: undefined, done: true }; };
        }

        // %ArrayIteratorPrototype%.
        const ArrayIteratorPrototype = Object.getPrototypeOf([][Symbol.iterator]());

        const cases = {
            // The issue's example: an array iterator with an own `next` getter.
            getterOnAnArrayIteratorsNext(m) {
                let reads = 0;
                const it = ['a', 'b'].values();
                const next = it.next.bind(it);
                Object.defineProperty(it, 'next', { get() { reads++; return next; } });
                const v = fmt(m, iterableOf(it));
                return 'reads=' + reads + ':' + v;
            },
            getterOnAPlainIteratorsNext(m) {
                let reads = 0;
                const next = stepper(['a', 'b', 'c']);
                const v = fmt(m, iterableOf({ get next() { reads++; return next; } }));
                return 'reads=' + reads + ':' + v;
            },
            nextReplacedMidIteration(m) {
                const step = stepper(['a', 'b', 'c']);
                const it = {
                    next() {
                        this.next = function () { return { value: 'replaced', done: true }; };
                        return step();
                    }
                };
                return fmt(m, iterableOf(it));
            },
            generatorsNextReplacedMidIteration(m) {
                const gen = (function* () {
                    yield 'a';
                    gen.next = function () { return { value: 'replaced', done: true }; };
                    yield 'b';
                    yield 'c';
                })();
                return fmt(m, gen);
            },
            nextInstalledByTheGetterThatAnsweredUndefined(m) {
                const step = stepper(['a', 'b']);
                const it = {
                    get next() {
                        Object.defineProperty(this, 'next', { value: step });
                        return undefined;
                    }
                };
                return fmt(m, iterableOf(it));
            },
            proxyIteratorTraps(m) {
                const log = [];
                const target = { [Symbol.iterator]() { return this; }, next: stepper(['a', 'b']) };
                const p = new Proxy(target, { get(t, k, r) { log.push(String(k)); return Reflect.get(t, k, r); } });
                const v = fmt(m, p);
                return log.join(',') + ':' + v;
            },
            resultReads(m) {
                const log = [];
                const step = stepper(['a', 'b']);
                const it = {
                    next() {
                        return new Proxy(step(), { get(t, k, r) { log.push(String(k)); return Reflect.get(t, k, r); } });
                    }
                };
                const v = fmt(m, iterableOf(it));
                return log.join(',') + ':' + v;
            },
            nonStringCloses(m) {
                let returns = 0;
                let steps = 0;
                const step = stepper(['a', 1, 'c']);
                const it = { next() { steps++; return step(); }, return() { returns++; return {}; } };
                const v = fmt(m, iterableOf(it));
                return v + ':steps=' + steps + ':returns=' + returns;
            },
            nonStringReadsReturnOnce(m) {
                let reads = 0;
                let calls = 0;
                const it = { next: stepper([undefined]), get return() { reads++; return function () { calls++; return {}; }; } };
                const v = fmt(m, iterableOf(it));
                return v + ':reads=' + reads + ':calls=' + calls;
            },
            nonStringWhoseReturnThrows(m) {
                const it = { next: stepper([1]), return() { throw new RangeError('from return'); } };
                return fmt(m, iterableOf(it));
            },
            nonStringWhoseReturnGetterThrows(m) {
                const it = { next: stepper([1]), get return() { throw new RangeError('from the return getter'); } };
                return fmt(m, iterableOf(it));
            },
            nonStringWhoseReturnIsNotCallable(m) {
                const it = { next: stepper([1]), return: 1 };
                return fmt(m, iterableOf(it));
            },
            nonStringWhoseReturnAnswersAPrimitive(m) {
                const it = { next: stepper([1]), return() { return 1; } };
                return fmt(m, iterableOf(it));
            },
            completionDoesNotClose(m) {
                let returns = 0;
                const it = { next: stepper(['a', 'b']), return() { returns++; return {}; } };
                const v = fmt(m, iterableOf(it));
                return v + ':returns=' + returns;
            },
            throwingNextDoesNotClose(m) {
                let returns = 0;
                const it = { next() { throw new RangeError('from next'); }, return() { returns++; return {}; } };
                const v = fmt(m, iterableOf(it));
                return v + ':returns=' + returns;
            },
            throwingDoneDoesNotClose(m) {
                let returns = 0;
                const it = { next() { return { get done() { throw new RangeError('from done'); } }; }, return() { returns++; return {}; } };
                const v = fmt(m, iterableOf(it));
                return v + ':returns=' + returns;
            },
            primitiveResultDoesNotClose(m) {
                let returns = 0;
                const it = { next() { return 1; }, return() { returns++; return {}; } };
                const v = fmt(m, iterableOf(it));
                return v + ':returns=' + returns;
            },
            primitiveResultAfterAValue(m) {
                let i = 0;
                const it = { next() { return i++ === 0 ? { value: 'a', done: false } : 'not an object'; } };
                return fmt(m, iterableOf(it));
            },
            iteratorMethodAnswersAPrimitive(m) {
                return fmt(m, { [Symbol.iterator]() { return 1; } });
            },
            iteratorMethodIsNotCallable(m) {
                return fmt(m, { [Symbol.iterator]: 1 });
            },
            iteratorMethodIsNull(m) {
                return fmt(m, { [Symbol.iterator]: null, length: 1, 0: 'a' });
            },
            arrayLikeWithoutAnIteratorMethod(m) {
                return fmt(m, { length: 2, 0: 'a', 1: 'b' });
            },
            stringIsIteratedByCodePoint(m) {
                return fmt(m, 'a\u{1F600}b');
            },
            replacedStringIteratorIsCalled(m) {
                String.prototype[Symbol.iterator] = function* () { yield 'x'; yield 'y'; };
                return fmt(m, 'ab');
            },
            replacedArrayIteratorMethodIsCalled(m) {
                const a = ['a', 'b'];
                a[Symbol.iterator] = function* () { yield 'x'; };
                return fmt(m, a);
            },
            replacedArrayPrototypeIteratorIsCalled(m) {
                Array.prototype[Symbol.iterator] = function* () { yield 'y'; };
                return fmt(m, ['a', 'b']);
            },
            replacedArrayIteratorNextIsCalled(m) {
                let calls = 0;
                const original = ArrayIteratorPrototype.next;
                ArrayIteratorPrototype.next = function () { calls++; return original.call(this); };
                const v = fmt(m, ['a', 'b']);
                return v + ':calls=' + calls;
            },
            arrayIteratorNextReplacedMidIteration(m) {
                const a = ['a', 'b', 'c'];
                Object.defineProperty(a, 0, {
                    get() {
                        ArrayIteratorPrototype.next = function () { return { value: 'replaced', done: true }; };
                        return 'a';
                    }
                });
                return fmt(m, a);
            },
            arrayIteratorReturnClosesOnNonString(m) {
                let returns = 0;
                ArrayIteratorPrototype.return = function () { returns++; return {}; };
                const v = fmt(m, ['a', 1]);
                return v + ':returns=' + returns;
            },
            arrayGrowsDuringIteration(m) {
                const a = ['a'];
                Object.defineProperty(a, 0, { get() { if (a.length === 1) a.push('b'); return 'a'; } });
                return fmt(m, a);
            },
            arrayHoleIsATypeError(m) {
                return fmt(m, ['a', , 'b']);
            },
            arrayOfStrings(m) {
                return fmt(m, ['a', 'b', 'c']);
            },
            setOfStrings(m) {
                return fmt(m, new Set(['a', 'b']));
            },
            numberWithAnIteratorMethod(m) {
                Number.prototype[Symbol.iterator] = function* () { yield 'n' + this; };
                return fmt(m, 5);
            },
            numberWithoutAnIteratorMethod(m) {
                return fmt(m, 5);
            },
            nullIsATypeError(m) {
                return fmt(m, null);
            },
            undefinedIsAnEmptyList(m) {
                return '[' + fmt(m, undefined) + ']';
            },
        };
        """;

    private static readonly string[] _methods = ["format", "formatToParts"];

    /// <summary>
    /// What Node 26 answers for each case, for <c>format</c> and <c>formatToParts</c> alike.
    /// </summary>
    private static readonly Dictionary<string, string> _expected = new(StringComparer.Ordinal)
    {
        ["getterOnAnArrayIteratorsNext"] = "reads=1:a and b",
        ["getterOnAPlainIteratorsNext"] = "reads=1:a, b, and c",
        ["proxyIteratorTraps"] = "Symbol(Symbol.iterator),next:a and b",
        ["resultReads"] = "done,value,done,value,done:a and b",

        ["nextReplacedMidIteration"] = "a, b, and c",
        ["generatorsNextReplacedMidIteration"] = "a, b, and c",
        ["nextInstalledByTheGetterThatAnsweredUndefined"] = "caught:TypeError",
        ["arrayIteratorNextReplacedMidIteration"] = "a, b, and c",

        ["nonStringCloses"] = "caught:TypeError:steps=2:returns=1",
        ["nonStringReadsReturnOnce"] = "caught:TypeError:reads=1:calls=1",
        ["nonStringWhoseReturnThrows"] = "caught:TypeError",
        ["nonStringWhoseReturnGetterThrows"] = "caught:TypeError",
        ["nonStringWhoseReturnIsNotCallable"] = "caught:TypeError",
        ["nonStringWhoseReturnAnswersAPrimitive"] = "caught:TypeError",
        ["arrayIteratorReturnClosesOnNonString"] = "caught:TypeError:returns=1",

        ["completionDoesNotClose"] = "a and b:returns=0",
        ["throwingNextDoesNotClose"] = "caught:RangeError:returns=0",
        ["throwingDoneDoesNotClose"] = "caught:RangeError:returns=0",
        ["primitiveResultDoesNotClose"] = "caught:TypeError:returns=0",
        ["primitiveResultAfterAValue"] = "caught:TypeError",

        ["iteratorMethodAnswersAPrimitive"] = "caught:TypeError",
        ["iteratorMethodIsNotCallable"] = "caught:TypeError",
        ["iteratorMethodIsNull"] = "caught:TypeError",
        ["arrayLikeWithoutAnIteratorMethod"] = "caught:TypeError",
        ["numberWithAnIteratorMethod"] = "n5",
        ["numberWithoutAnIteratorMethod"] = "caught:TypeError",
        ["nullIsATypeError"] = "caught:TypeError",
        ["undefinedIsAnEmptyList"] = "[]",
        ["setOfStrings"] = "a and b",

        ["stringIsIteratedByCodePoint"] = "a, \U0001F600, and b",
        ["replacedStringIteratorIsCalled"] = "x and y",
        ["replacedArrayIteratorMethodIsCalled"] = "x",
        ["replacedArrayPrototypeIteratorIsCalled"] = "y",
        ["replacedArrayIteratorNextIsCalled"] = "a and b:calls=3",
        ["arrayGrowsDuringIteration"] = "a and b",
        ["arrayHoleIsATypeError"] = "caught:TypeError",
        ["arrayOfStrings"] = "a, b, and c",
    };

    private static void Check(string scenario, string method)
    {
        var actual = new Engine().Evaluate($"{Harness}\ncases['{scenario}']('{method}')").AsString();
        actual.Should().Be(_expected[scenario]);
    }

    /// <summary>
    /// GetIteratorFromMethod reads <c>next</c> once, and every step calls what it read: a getter on <c>next</c> runs once
    /// whatever the number of steps, on an array iterator (the issue's example) and on a plain one, and a Proxy sees
    /// <c>@@iterator</c> and <c>next</c> read once each. The reads of each step's result, <c>done</c> then <c>value</c> and
    /// only <c>done</c> on the last, are the control.
    /// </summary>
    [Test]
    public void NextIsReadOnce(
        [Values("getterOnAnArrayIteratorsNext", "getterOnAPlainIteratorsNext", "proxyIteratorTraps", "resultReads")] string scenario,
        [ValueSource(nameof(_methods))] string method)
        => Check(scenario, method);

    /// <summary>
    /// A <c>next</c> the script puts in place once the record exists is never called: one the iterator assigns itself on
    /// its first step, one a generator's body assigns, and a %ArrayIteratorPrototype%.next replaced by an element's getter
    /// while an array's record steps natively. A getter that answered <c>undefined</c> and installed a real <c>next</c>
    /// left the record holding <c>undefined</c>, so the first step's Call throws.
    /// </summary>
    [Test]
    public void ANextReplacedAfterTheRecordWasBuiltIsNotCalled(
        [Values("nextReplacedMidIteration", "generatorsNextReplacedMidIteration", "nextInstalledByTheGetterThatAnsweredUndefined", "arrayIteratorNextReplacedMidIteration")] string scenario,
        [ValueSource(nameof(_methods))] string method)
        => Check(scenario, method);

    /// <summary>
    /// A non-String value is a TypeError that closes the iterator: <c>return</c> is read once and called once, after the
    /// step that answered the value and no other, including a <c>return</c> a script added to %ArrayIteratorPrototype%.
    /// The TypeError is what leaves, whatever the close does: a <c>return</c> that throws, a getter for it that throws, one
    /// that is not callable, one that answers a primitive (IteratorClose step 5).
    /// </summary>
    [Test]
    public void ANonStringClosesTheIteratorAndItsTypeErrorWins(
        [Values("nonStringCloses", "nonStringReadsReturnOnce", "nonStringWhoseReturnThrows", "nonStringWhoseReturnGetterThrows", "nonStringWhoseReturnIsNotCallable", "nonStringWhoseReturnAnswersAPrimitive", "arrayIteratorReturnClosesOnNonString")] string scenario,
        [ValueSource(nameof(_methods))] string method)
        => Check(scenario, method);

    /// <summary>
    /// IteratorStepValue leaves the record done when it finishes or fails, so nothing closes it: an iteration that runs
    /// out, a <c>next</c> or a <c>done</c> getter that throws, and a step answering a non-object, which IteratorNext makes
    /// a TypeError rather than the end of the list, before the first value or after one.
    /// </summary>
    [Test]
    public void AStepThatFinishesOrFailsDoesNotClose(
        [Values("completionDoesNotClose", "throwingNextDoesNotClose", "throwingDoneDoesNotClose", "primitiveResultDoesNotClose", "primitiveResultAfterAValue")] string scenario,
        [ValueSource(nameof(_methods))] string method)
        => Check(scenario, method);

    /// <summary>
    /// Only <c>undefined</c> is the empty list; everything else goes through GetIterator, so an object without a usable
    /// <c>@@iterator</c> is a TypeError, an array-like included, and so is an <c>@@iterator</c> answering a primitive. A
    /// primitive with an <c>@@iterator</c> on its prototype is iterable, and one without is not.
    /// </summary>
    [Test]
    public void GetIteratorDecidesWhatIsIterable(
        [Values("iteratorMethodAnswersAPrimitive", "iteratorMethodIsNotCallable", "iteratorMethodIsNull", "arrayLikeWithoutAnIteratorMethod", "numberWithAnIteratorMethod", "numberWithoutAnIteratorMethod", "nullIsATypeError", "undefinedIsAnEmptyList", "setOfStrings")] string scenario,
        [ValueSource(nameof(_methods))] string method)
        => Check(scenario, method);

    /// <summary>
    /// A string and an array are iterated through their <c>@@iterator</c> like anything else: a string by code point
    /// (<a href="https://tc39.es/ecma262/#sec-string.prototype-%symbol.iterator%">String.prototype[@@iterator]</a>), an
    /// array by its iterator, which reads the length on every step
    /// (<a href="https://tc39.es/ecma262/#sec-%arrayiteratorprototype%.next">%ArrayIteratorPrototype%.next</a>), and a
    /// replaced <c>@@iterator</c> or <c>next</c> is called. An untouched array of strings and a hole are the controls.
    /// </summary>
    [Test]
    public void StringsAndArraysAreIteratedThroughTheirIterators(
        [Values("stringIsIteratedByCodePoint", "replacedStringIteratorIsCalled", "replacedArrayIteratorMethodIsCalled", "replacedArrayPrototypeIteratorIsCalled", "replacedArrayIteratorNextIsCalled", "arrayGrowsDuringIteration", "arrayHoleIsATypeError", "arrayOfStrings")] string scenario,
        [ValueSource(nameof(_methods))] string method)
        => Check(scenario, method);

    /// <summary>
    /// Every case the harness defines has an expected value above, and every expected value a case, so neither drifts.
    /// </summary>
    [Test]
    public void EveryHarnessCaseHasAnExpectedValue()
    {
        var names = new Engine().Evaluate($"{Harness}\nObject.keys(cases).join(',')").AsString().Split(',');
        names.Should().BeEquivalentTo(_expected.Keys);
    }
}
