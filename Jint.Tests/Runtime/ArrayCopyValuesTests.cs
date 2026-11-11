#nullable enable

using Jint.Native;

namespace Jint.Tests.Runtime;

/// <summary>
/// Pins <a href="https://tc39.es/ecma262/#sec-array.prototype.slice">Array.prototype.slice</a> and
/// <a href="https://tc39.es/ecma262/#sec-array.prototype.concat">Array.prototype.concat</a> over the arrays the
/// engine copies from by bulk (<c>ArrayInstance.CopyValues</c>): both ask <c>HasProperty</c> of each source index
/// and <c>Get</c> it when present, so a hole the source's prototype chain fills is read through, and a hole nothing
/// fills adds no property to the result.
/// <para>
/// Two things were wrong. The loop that serves a sparse-mode source, a frozen one, or a result that is not
/// freshly allocated stopped at the length of the range rather than at the end of it, so any start offset left the
/// tail of the range uncopied. And the loop that serves a dense source copied a hole as a hole without asking the
/// prototype chain, and copied an index past the dense backing as <c>undefined</c> where the source had no
/// such property at all.
/// The dense loop and slice also assumed a freshly allocated result: a species constructor can return an array it
/// has already filled or given another length, a hole must leave an element it filled alone, and slice's closing
/// <c>Set(A, "length", n, true)</c> must grow or truncate it.
/// </para>
/// <para>
/// Every expected value is Node 26's, each row run in a fresh <c>vm</c> context. Each row needs its own engine: the
/// first index property written to <c>Array.prototype</c> or <c>Object.prototype</c> retires the pristine-prototype
/// invariant for the realm, permanently, which is what routes a copy to the lane under test. <c>hole</c> is an
/// absent index, <c>v:x</c> an own value and <c>i:x</c> one only the result's prototype chain supplies.
/// </para>
/// </summary>
public class ArrayCopyValuesTests
{
    private const string Harness = """
        function show(a) {
            var s = '';
            for (var i = 0; i < a.length; i++) {
                s += (i ? ',' : '') + (!(i in a) ? 'hole' : (Object.prototype.hasOwnProperty.call(a, i) ? 'v:' : 'i:') + String(a[i]));
            }
            return a.length + '[' + s + ']';
        }
        function keys(a) { return a.length + ':' + Object.keys(a).join(','); }
        """;

    [TestCaseSource(nameof(Copies))]
    public void SliceAndConcatCopyWhatTheSpecSays(string name, string script, string expected)
    {
        _ = name;

        new Engine().Evaluate(Harness + "\n" + script).AsString().Should().Be(expected);
    }

    public static TestCases<string, string, string> Copies
    {
        get
        {
            var rows = new TestCases<string, string, string>();

            // a source with no dense backing: the copy loop must run to the end of the range, not to its length
            rows.Add("SliceSparseSourceStartInside", """
                var a = [0, 1, 2]; a[6000000] = 5;
                show(a.slice(1, 3));
                """, "2[v:1,v:2]");
            rows.Add("SliceSparseSourceStartPastDensePart", """
                var a = [0, 1, 2]; a[6000000] = 5;
                show(a.slice(5999999, 6000001));
                """, "2[hole,v:5]");
            rows.Add("SliceSparseSourceNegativeStart", """
                var a = [0, 1, 2]; a[6000000] = 5;
                show(a.slice(-3));
                """, "3[hole,hole,v:5]");
            rows.Add("SliceSparseSourceWithHolesInside", """
                var a = [0]; a[3] = 3; a[6000000] = 6;
                show(a.slice(2, 5));
                """, "3[hole,v:3,hole]");
            rows.Add("SliceSparseSourceFromZero", """
                var a = [0, 1, 2]; a[6000000] = 5;
                show(a.slice(0, 3));
                """, "3[v:0,v:1,v:2]");
            rows.Add("SliceSparseSourceEmptyRange", """
                var a = [0, 1, 2]; a[6000000] = 5;
                show(a.slice(2, 2)) + show(a.slice(3, 1));
                """, "0[]0[]");
            rows.Add("SliceDefinedDescriptorStartInside", """
                var a = [0, 1, 2, 3, 4];
                Object.defineProperty(a, 3, { value: 33, writable: false, enumerable: true, configurable: true });
                show(a.slice(1, 4));
                """, "3[v:1,v:2,v:33]");
            rows.Add("SliceDefinedDescriptorStartToEnd", """
                var a = [0, 1, 2, 3, 4];
                Object.defineProperty(a, 3, { value: 33, writable: false, enumerable: true, configurable: true });
                show(a.slice(3));
                """, "2[v:33,v:4]");
            rows.Add("SliceDefinedDescriptorWithHoles", """
                var a = [0, , 2, , 4];
                Object.defineProperty(a, 2, { value: 22, writable: false, enumerable: true, configurable: true });
                show(a.slice(1, 4));
                """, "3[hole,v:22,hole]");

            // a dense source with a gap: the dense lane reaches every index of the range
            rows.Add("SliceHoleyDenseSourceStartInside", """
                show([0, , 2, , 4].slice(1, 4));
                """, "3[hole,v:2,hole]");
            rows.Add("SliceHoleyDenseSourceStartToEnd", """
                show([0, , 2, , 4].slice(2));
                """, "3[v:2,hole,v:4]");
            rows.Add("SliceHoleyDenseSourceWholeArray", """
                show([0, , 2, , 4].slice());
                """, "5[v:0,hole,v:2,hole,v:4]");
            rows.Add("SliceGappyDenseSourceStartInside", """
                var a = [0, 1, 2]; a[500] = 5;
                show(a.slice(1, 3));
                """, "2[v:1,v:2]");
            rows.Add("SliceGappyDenseSourceStartToEnd", """
                var a = [0, 1, 2]; a[500] = 5;
                keys(a.slice(1));
                """, "500:0,1,499");
            rows.Add("SliceGappyDenseSourceStartPastDensePart", """
                var a = [0, 1, 2]; a[500] = 5;
                show(a.slice(499, 501));
                """, "2[hole,v:5]");
            rows.Add("SliceGappyDenseSourceNegativeStart", """
                var a = [0, 1, 2]; a[500] = 5;
                show(a.slice(-3));
                """, "3[hole,hole,v:5]");

            // a source whose logical length exceeds its dense backing
            rows.Add("SliceShortBackingWhole", """
                var a = [1, 2]; a.length = 5;
                show(a.slice());
                """, "5[v:1,v:2,hole,hole,hole]");
            rows.Add("SliceShortBackingStartInside", """
                var a = [1, 2]; a.length = 5;
                show(a.slice(1, 4));
                """, "3[v:2,hole,hole]");
            rows.Add("SliceShortBackingStartPast", """
                var a = [1, 2]; a.length = 5;
                show(a.slice(3));
                """, "2[hole,hole]");
            rows.Add("SliceNewArrayWithOneElement", """
                var a = new Array(6); a[0] = 'x';
                show(a.slice(0, 6)) + show(a.slice(2, 5));
                """, "6[v:x,hole,hole,hole,hole,hole]3[hole,hole,hole]");
            rows.Add("ConcatShortBackingReceiver", """
                var a = [1, 2]; a.length = 5;
                show(a.concat([9]));
                """, "6[v:1,v:2,hole,hole,hole,v:9]");
            rows.Add("ConcatShortBackingArgument", """
                var a = [1, 2]; a.length = 5;
                show([0].concat(a));
                """, "6[v:0,v:1,v:2,hole,hole,hole]");
            rows.Add("ConcatShortBackingTwice", """
                var a = [1, 2]; a.length = 4;
                show(a.concat(a));
                """, "8[v:1,v:2,hole,hole,v:1,v:2,hole,hole]");

            // concat of holey and sparse arrays
            rows.Add("ConcatHoleyDense", """
                show([1, , 3].concat([4, , 6]));
                """, "6[v:1,hole,v:3,v:4,hole,v:6]");
            rows.Add("ConcatSparseModeReceiver", """
                var s = [1, 2, 3];
                Object.defineProperty(s, 1, { value: 22, writable: false, enumerable: true, configurable: true });
                show(s.concat([9]));
                """, "4[v:1,v:22,v:3,v:9]");
            rows.Add("ConcatSparseModeArgument", """
                var s = [1, , 3];
                Object.defineProperty(s, 2, { value: 33, writable: false, enumerable: true, configurable: true });
                show([0].concat(s, [7]));
                """, "5[v:0,v:1,hole,v:33,v:7]");
            rows.Add("ConcatSparseModeBoth", """
                var s = [1, 2, 3]; var t = [4, , 6];
                Object.defineProperty(s, 0, { value: 11, writable: false, enumerable: true, configurable: true });
                Object.defineProperty(t, 2, { value: 66, writable: false, enumerable: true, configurable: true });
                show(s.concat(t));
                """, "6[v:11,v:2,v:3,v:4,hole,v:66]");
            rows.Add("ConcatSparseModeLargeIndex", """
                var s = [1]; s[6000000] = 2;
                keys([0].concat(s, [7]));
                """, "6000003:0,1,6000001,6000002");
            rows.Add("ConcatGappyDense", """
                var s = [1]; s[200] = 2;
                keys(s.concat([9]));
                """, "202:0,200,201");
            rows.Add("ConcatScalarsAndHoles", """
                show([, 1].concat(2, [, 3], [,]));
                """, "6[hole,v:1,v:2,hole,v:3,hole]");

            // an index inherited from Array.prototype
            rows.Add("SliceInheritedIndexWhole", """
                Array.prototype[1] = 'P';
                show([0, , 2].slice());
                """, "3[v:0,v:P,v:2]");
            rows.Add("SliceInheritedIndexStartInside", """
                Array.prototype[1] = 'P';
                show([0, , 2].slice(1, 3));
                """, "2[v:P,v:2]");
            rows.Add("SliceInheritedIndexPastDenseBacking", """
                Array.prototype[3] = 'P';
                var a = [1, 2]; a.length = 5;
                show(a.slice());
                """, "5[v:1,v:2,hole,v:P,hole]");
            rows.Add("SliceInheritedIndexOwnPropertyFlag", """
                Array.prototype[1] = 'P';
                var r = [0, , 2].slice();
                String(Object.prototype.hasOwnProperty.call(r, 1)) + ',' + Object.keys(r).join(',');
                """, "true,0,1,2");
            rows.Add("SliceInheritedIndexFromObjectPrototype", """
                Object.prototype[1] = 'O';
                show([0, , 2].slice());
                """, "3[v:0,v:O,v:2]");
            rows.Add("SliceInheritedIndexSparseMode", """
                Array.prototype[2] = 'P';
                var a = [0, 1]; a[6000000] = 3;
                show(a.slice(1, 4));
                """, "3[v:1,v:P,i:P]");
            rows.Add("SliceInheritedAccessorReadOncePerHole", """
                var seen = [];
                var a = [0, , 2, , 4];
                Object.defineProperty(Array.prototype, 1, { configurable: true, get: function () { seen.push(this === a); return 'G'; } });
                show(a.slice()) + ' ' + seen.join();
                """, "5[v:0,v:G,v:2,hole,v:4] true");
            rows.Add("SliceInheritedAccessorSeesLaterMutation", """
                var a = [0, , 2, 3];
                Object.defineProperty(Array.prototype, 1, { configurable: true, get: function () { a.length = 2; return 'G'; } });
                show(a.slice());
                """, "4[v:0,v:G,hole,hole]");
            rows.Add("SliceInheritedAccessorDeletesLaterElement", """
                var a = [0, , 2, 3];
                Object.defineProperty(Array.prototype, 1, { configurable: true, get: function () { delete a[3]; return 'G'; } });
                show(a.slice());
                """, "4[v:0,v:G,v:2,hole]");
            rows.Add("ConcatInheritedIndexReceiverAndArgument", """
                Array.prototype[1] = 'P';
                show([0, , 2].concat([5, , 7]));
                """, "6[v:0,v:P,v:2,v:5,v:P,v:7]");
            rows.Add("ConcatInheritedIndexArgumentOnly", """
                Array.prototype[1] = 'P';
                show([].concat([5, , 7]));
                """, "3[v:5,v:P,v:7]");
            rows.Add("ConcatInheritedIndexFromObjectPrototype", """
                Object.prototype[1] = 'O';
                show([0, , 2].concat([5, , 7]));
                """, "6[v:0,v:O,v:2,v:5,v:O,v:7]");
            rows.Add("ConcatInheritedIndexPastDenseBacking", """
                Array.prototype[4] = 'P';
                var a = [1, 2]; a.length = 6;
                show(a.concat(a));
                """, "12[v:1,v:2,hole,hole,v:P,hole,v:1,v:2,hole,hole,v:P,hole]");
            rows.Add("ConcatInheritedIndexSparseMode", """
                Array.prototype[2] = 'P';
                var s = [0, 1, , 3];
                Object.defineProperty(s, 0, { value: 0, writable: false, enumerable: true, configurable: true });
                show(s.concat([9]));
                """, "5[v:0,v:1,v:P,v:3,v:9]");

            // an index inherited through a prototype the array was switched to
            rows.Add("SliceCustomPrototypeIndex", """
                var a = [0, , 2];
                Object.setPrototypeOf(a, { 1: 'C', __proto__: Array.prototype });
                show(Array.prototype.slice.call(a));
                """, "3[v:0,v:C,v:2]");
            rows.Add("SliceCustomPrototypeIndexStartInside", """
                var a = [0, , 2];
                Object.setPrototypeOf(a, { 1: 'C', __proto__: Array.prototype });
                show(Array.prototype.slice.call(a, 1, 3));
                """, "2[v:C,v:2]");
            rows.Add("SliceCustomPrototypeIndexViaMethod", """
                var a = [0, , 2];
                Object.setPrototypeOf(a, { 1: 'C', __proto__: Array.prototype });
                show(a.slice());
                """, "3[v:0,v:C,v:2]");
            rows.Add("SliceCustomPrototypeWithoutIndex", """
                var a = [0, , 2];
                Object.setPrototypeOf(a, { __proto__: Array.prototype });
                show(a.slice());
                """, "3[v:0,hole,v:2]");
            rows.Add("SliceNullPrototype", """
                var a = [0, , 2];
                Object.setPrototypeOf(a, null);
                show(Array.prototype.slice.call(a));
                """, "3[v:0,hole,v:2]");
            rows.Add("ConcatCustomPrototypeIndexArgument", """
                var x = [1, , 3];
                Object.setPrototypeOf(x, { 1: 'C', __proto__: Array.prototype });
                show([0].concat(x));
                """, "4[v:0,v:1,v:C,v:3]");
            rows.Add("ConcatCustomPrototypeIndexReceiver", """
                var x = [1, , 3];
                Object.setPrototypeOf(x, { 1: 'C', __proto__: Array.prototype });
                show(Array.prototype.concat.call(x, [9]));
                """, "4[v:1,v:C,v:3,v:9]");
            rows.Add("ConcatCustomPrototypeIndexSpreadableObject", """
                var x = [1, , 3];
                Object.setPrototypeOf(x, { 1: 'C', __proto__: Array.prototype });
                x[Symbol.isConcatSpreadable] = true;
                show([].concat(x, [8]));
                """, "4[v:1,v:C,v:3,v:8]");

            // frozen, sealed and non-extensible sources
            rows.Add("SliceFrozenStartInside", """
                show(Object.freeze([0, 1, 2, 3]).slice(1, 3));
                """, "2[v:1,v:2]");
            rows.Add("SliceFrozenHoley", """
                show(Object.freeze([0, , 2, , 4]).slice(1, 4));
                """, "3[hole,v:2,hole]");
            rows.Add("SliceFrozenResultIsNotFrozen", """
                var r = Object.freeze([0, 1, 2, 3]).slice(1, 3);
                var d = Object.getOwnPropertyDescriptor(r, 0);
                [Object.isFrozen(r), Object.isExtensible(r), d.writable, d.configurable, d.enumerable].join();
                """, "false,true,true,true,true");
            rows.Add("SliceSealedStartInside", """
                show(Object.seal([0, 1, 2, 3]).slice(2));
                """, "2[v:2,v:3]");
            rows.Add("SliceNonExtensibleStartInside", """
                show(Object.preventExtensions([0, 1, 2, 3]).slice(1, 3));
                """, "2[v:1,v:2]");
            rows.Add("SliceNonExtensibleResultIsExtensible", """
                var r = Object.preventExtensions([0, , 2, 3]).slice(1);
                String(Object.isExtensible(r)) + ' ' + show(r);
                """, "true 3[hole,v:2,v:3]");
            rows.Add("SliceFrozenInheritedIndex", """
                Array.prototype[1] = 'P';
                show(Object.freeze([0, , 2]).slice());
                """, "3[v:0,v:P,v:2]");
            rows.Add("ConcatFrozenReceiver", """
                show(Object.freeze([1, , 3]).concat([4]));
                """, "4[v:1,hole,v:3,v:4]");
            rows.Add("ConcatFrozenArgument", """
                show([0].concat(Object.freeze([1, , 3])));
                """, "4[v:0,v:1,hole,v:3]");
            rows.Add("ConcatNonExtensibleArgument", """
                show([0].concat(Object.preventExtensions([1, , 3])));
                """, "4[v:0,v:1,hole,v:3]");
            rows.Add("ConcatFrozenResultIsNotFrozen", """
                var r = Object.freeze([1, 2]).concat(Object.freeze([3]));
                [Object.isFrozen(r), Object.isExtensible(r), Object.getOwnPropertyDescriptor(r, 2).writable].join();
                """, "false,true,true");
            rows.Add("ConcatFrozenInheritedIndex", """
                Array.prototype[1] = 'P';
                show(Object.freeze([1, , 3]).concat(Object.freeze([4, , 6])));
                """, "6[v:1,v:P,v:3,v:4,v:P,v:6]");

            // the species result: a target the constructor filled keeps what a hole leaves alone, and takes the length the algorithm sets
            rows.Add("SliceIntoPrefilledSpeciesTarget", """
                var a = [1, 2, 3, 4];
                a.constructor = { [Symbol.species]: function (n) { var r = new Array(n); r[0] = 'x'; return r; } };
                show(a.slice(1, 3));
                """, "2[v:2,v:3]");
            rows.Add("SliceSpeciesKeepsAPrefilledSlotAtAHole", """
                var a = [1, , 3];
                a.constructor = { [Symbol.species]: function (n) { var r = new Array(n); r[1] = 'x'; return r; } };
                show(a.slice());
                """, "3[v:1,v:x,v:3]");
            rows.Add("SliceSpeciesKeepsAPrefilledSlotPastTheBacking", """
                var a = [1, 2]; a.length = 5;
                a.constructor = { [Symbol.species]: function (n) { var r = new Array(n); r[4] = 'z'; return r; } };
                show(a.slice());
                """, "5[v:1,v:2,hole,hole,v:z]");
            rows.Add("SliceSpeciesKeepsAPrefilledSlotWithAStartOffset", """
                var a = [1, 2, , 4, 5];
                a.constructor = { [Symbol.species]: function (n) { var r = new Array(n); r[0] = 'x'; return r; } };
                show(a.slice(2));
                """, "3[v:x,v:4,v:5]");
            rows.Add("SliceSpeciesOverwritesAPrefilledSlotAnElementFills", """
                var a = [1, 2, 3];
                a.constructor = { [Symbol.species]: function (n) { var r = new Array(n); r[1] = 'x'; return r; } };
                show(a.slice());
                """, "3[v:1,v:2,v:3]");
            rows.Add("SliceIntoShortSpeciesTarget", """
                var a = [1, 2, 3, 4];
                a.constructor = { [Symbol.species]: function (n) { return []; } };
                show(a.slice(1, 3));
                """, "2[v:2,v:3]");
            rows.Add("SliceIntoLongSpeciesTarget", """
                var a = [1, 2, 3, 4];
                a.constructor = { [Symbol.species]: function (n) { return [9, 9, 9, 9, 9]; } };
                show(a.slice(1, 3));
                """, "2[v:2,v:3]");
            rows.Add("SliceIntoShortSpeciesTargetWithTrailingHoles", """
                var a = [1, 2]; a.length = 5;
                a.constructor = { [Symbol.species]: function (n) { return []; } };
                show(a.slice(1));
                """, "4[v:2,hole,hole,hole]");
            rows.Add("ConcatIntoPrefilledSpeciesTarget", """
                var a = [1, 2];
                a.constructor = { [Symbol.species]: function (n) { var r = []; r[0] = 'x'; r[1] = 'y'; return r; } };
                show(a.concat([3, , 5]));
                """, "5[v:1,v:2,v:3,hole,v:5]");
            rows.Add("ConcatSpeciesKeepsAPrefilledSlotAtAHole", """
                var a = [1, , 3];
                a.constructor = { [Symbol.species]: function () { return [ , 'b']; } };
                show(a.concat());
                """, "3[v:1,v:b,v:3]");
            rows.Add("ConcatSpeciesKeepsAPrefilledSlotAtAnArgumentHole", """
                var a = [1, 2];
                a.constructor = { [Symbol.species]: function () { var r = []; r[3] = 'y'; return r; } };
                show(a.concat([5, , 7]));
                """, "5[v:1,v:2,v:5,v:y,v:7]");

            // the pristine case: holes stay holes
            rows.Add("SlicePristineHolesStayHoles", """
                show([0, , 2, , ].slice()) + show([0, , 2, , ].slice(1)) + show([ , , ].slice(1, 2));
                """, "4[v:0,hole,v:2,hole]3[hole,v:2,hole]1[hole]");
            rows.Add("ConcatPristineHolesStayHoles", """
                show([0, , 2].concat([ , 4], [ , ]));
                """, "6[v:0,hole,v:2,hole,v:4,hole]");
            rows.Add("SlicePristineDoesNotInventOwnProperties", """
                Object.keys([0, , 2, , 4].slice(1)).join();
                """, "1,3");

            return rows;
        }
    }

    /// <summary>
    /// A result is sized by the range it copies, not by the backing store of the array it copies from. The copy used
    /// to ask for room for the whole backing, so a two-element slice of a 100000-element array left the result a
    /// backing of 131072 slots.
    /// </summary>
    [TestCase("big.slice(0, 2)", 2)]
    [TestCase("big.slice(50000, 50003)", 3)]
    [TestCase("big.slice(99990)", 10)]
    [TestCase("[].concat(big.slice(0, 3), big.slice(10, 12))", 8)]
    public void TheResultIsNotSizedByTheBackingOfTheSource(string expression, int largestBacking)
    {
        var engine = new Engine();
        engine.Execute("var big = []; for (var i = 0; i < 100000; i++) big.push(i);");

        var result = (JsArray) engine.Evaluate(expression);

        result._dense.Should().NotBeNull();
        result._dense!.Length.Should().BeLessThanOrEqualTo(largestBacking);
    }
}
