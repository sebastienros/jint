#nullable enable

using Jint.Runtime.Interpreter;
using Jint.Runtime.Interpreter.Expressions;

namespace Jint.Tests.Runtime;

/// <summary>
/// Pins the global-binding inline cache of <see cref="JintIdentifierExpression"/> around globals that are not
/// writable. <c>undefined</c>, <c>NaN</c> and <c>Infinity</c> are non-writable, non-configurable data properties
/// of the global object (https://tc39.es/ecma262/#sec-undefined,
/// https://tc39.es/ecma262/#sec-value-properties-of-the-global-object-nan,
/// https://tc39.es/ecma262/#sec-value-properties-of-the-global-object-infinity), and a host may install a
/// read-only global of its own. Every scenario warms the cache first and then checks what script observes,
/// so a read served from a stale descriptor, or a write let through one, shows up as a wrong value.
/// </summary>
/// <remarks>
/// <para>A read-only global stays readable through the cache, but a write never goes through it: the global
/// environment's SetMutableBinding (https://tc39.es/ecma262/#sec-global-environment-records-setmutablebinding-n-v-s)
/// goes to the object record's (https://tc39.es/ecma262/#sec-object-environment-records-setmutablebinding-n-v-s),
/// whose [[Set]] returns false for a non-writable own data property
/// (https://tc39.es/ecma262/#sec-ordinarysetwithowndescriptor), which throws a <c>TypeError</c> in strict code
/// and is ignored in sloppy code. A redefinition goes through
/// https://tc39.es/ecma262/#sec-validateandapplypropertydescriptor. The expected strings were taken from
/// Node 26, running each script in its main context.</para>
/// <para>Each scenario runs from source, prepared, and prepared with the static analysis off, and each of those
/// twice: as written, and with a dead direct <c>eval</c> call appended. Only a prepared, analysed program has its
/// identifier <c>undefined</c> folded to a constant before it runs, and only when nothing in the program can shadow
/// it, so the appended call is what keeps the prepared mode's reads unfolded; source text is never analysed
/// (<c>Execute(string)</c> and <c>Evaluate(string)</c> only parse), so the source mode's reads are never folded
/// statically, with or without it. A read the analysis left unfolded is folded at run time by its first resolution
/// (see <c>RuntimeUndefinedFoldTests</c>), so in every mode the cache serves <c>undefined</c> for that first read only;
/// <c>NaN</c>, <c>Infinity</c> and the host's global are what keep exercising it.</para>
/// </remarks>
public class ReadOnlyGlobalBindingCacheTests
{
    public enum Preparation
    {
        Source,
        Prepared,
        PreparedWithoutStaticAnalysis,
    }

    private const string FoldBlocker = "\nfunction unusedEval() { return eval('0'); }\n";

    public static readonly TestCases<Preparation, bool> Modes = new()
    {
        { Preparation.Source, false },
        { Preparation.Source, true },
        { Preparation.Prepared, false },
        { Preparation.Prepared, true },
        { Preparation.PreparedWithoutStaticAnalysis, false },
        { Preparation.PreparedWithoutStaticAnalysis, true },
    };

    private static string Run(string code, Preparation preparation, bool blockUndefinedFold)
    {
        if (blockUndefinedFold)
        {
            code += FoldBlocker;
        }

        using var engine = new Engine();
        var result = preparation switch
        {
            Preparation.Source => engine.Evaluate(code),
            Preparation.Prepared => engine.Evaluate(Engine.PrepareScript(code)),
            _ => engine.Evaluate(Engine.PrepareScript(code, options: new ScriptPreparationOptions { StaticAnalysis = false })),
        };

        return result.AsString();
    }

    [TestCaseSource(nameof(Modes))]
    public void WarmReadsOfUndefinedNaNAndInfinityKeepTheirValues(Preparation preparation, bool blockUndefinedFold)
    {
        // top-level reads in a loop, the numeric operand lanes (global with global, slot with global,
        // bitwise with a constant), nested function and block scopes, and closures
        const string Code = """
            var hits = 0, total = 0;
            for (var i = 0; i < 200; i++) {
                total++;
                if (undefined === void 0) hits++;
                if (typeof undefined === 'undefined') hits++;
                if (NaN !== NaN) hits++;
                if (i < Infinity) hits++;
                if (-Infinity < i) hits++;
                if ((NaN | 0) === 0 && (Infinity | 0) === 0) hits++;
                if (!(i < NaN) && !(i >= NaN)) hits++;
            }
            function nested() {
                var n = 0;
                for (let j = 0; j < 100; j++) {
                    for (let k = 0; k < 2; k++) {
                        if (undefined === void 0 && NaN !== NaN && j < Infinity && Infinity - j === Infinity) n++;
                    }
                }
                return n;
            }
            var closures = [];
            for (let m = 0; m < 3; m++) closures.push(() => [typeof undefined, NaN !== NaN, Infinity > m].join());
            [hits, total, nested(), nested(), closures.map(f => f()).join('|'), String(undefined), String(NaN), String(Infinity), 1 / Infinity, Infinity * -1].join(';')
            """;

        Run(Code, preparation, blockUndefinedFold)
            .Should().Be("1400;200;200;200;undefined,true,true|undefined,true,true|undefined,true,true;undefined;NaN;Infinity;0;-Infinity");
    }

    [TestCaseSource(nameof(Modes))]
    public void AStrictWriteToUndefinedOrNaNThrowsAfterWarmReads(Preparation preparation, bool blockUndefinedFold)
    {
        const string Code = """
            'use strict';
            var log = [];
            for (var i = 0; i < 50; i++) { undefined; NaN; Infinity; }
            function attempt(label, f) { try { f(); log.push(label + ':ok'); } catch (e) { log.push(label + ':' + e.constructor.name); } }
            function readAll() { var r; for (let k = 0; k < 5; k++) { r = [typeof undefined, NaN !== NaN, Infinity === 1 / 0].join(); } return r; }
            readAll();
            for (var j = 0; j < 3; j++) {
                attempt('undefined=', () => { undefined = 1; });
                attempt('NaN=', () => { NaN = 1; });
                attempt('Infinity++', () => { Infinity++; });
                attempt('--undefined', () => { --undefined; });
                try { NaN = 2; log.push('top NaN=:ok'); } catch (e) { log.push('top NaN=:' + e.constructor.name); }
                try { undefined++; log.push('top undefined++:ok'); } catch (e) { log.push('top undefined++:' + e.constructor.name); }
            }
            log.push(readAll(), typeof undefined, String(NaN), String(Infinity));
            log.join(';')
            """;

        const string Round = "undefined=:TypeError;NaN=:TypeError;Infinity++:TypeError;--undefined:TypeError;top NaN=:TypeError;top undefined++:TypeError;";
        Run(Code, preparation, blockUndefinedFold)
            .Should().Be(Round + Round + Round + "undefined,true,true;undefined;NaN;Infinity");
    }

    [TestCaseSource(nameof(Modes))]
    public void ASloppyWriteToUndefinedNaNOrInfinityIsIgnoredAfterWarmReads(Preparation preparation, bool blockUndefinedFold)
    {
        // the writes repeat, so after the first ignored write each write site meets a populated cache
        const string Code = """
            var log = [];
            for (var i = 0; i < 50; i++) { undefined; NaN; Infinity; }
            function readAll() { var r; for (let k = 0; k < 5; k++) { r = [typeof undefined, NaN !== NaN, Infinity === 1 / 0].join(); } return r; }
            function writeAll(v) { undefined = v; NaN = v; Infinity = v; undefined++; NaN--; ++Infinity; NaN += v; }
            readAll();
            var results = [];
            for (var k = 0; k < 20; k++) {
                writeAll(k);
                undefined = k; NaN = k; Infinity = k; NaN++; Infinity--;
                results = [undefined = 'x', NaN++, --Infinity];
            }
            log.push(results.join(), readAll(), typeof undefined, String(NaN), String(Infinity));
            log.join(';')
            """;

        Run(Code, preparation, blockUndefinedFold)
            .Should().Be("x,NaN,Infinity;undefined,true,true;undefined;NaN;Infinity");
    }

    [TestCaseSource(nameof(Modes))]
    public void AReadOnlyConfigurableHostGlobalIsReadFreshAfterEveryRedefinition(Preparation preparation, bool blockUndefinedFold)
    {
        // one top-level read site, one function and one nested-scope read stay warm across every phase:
        // the value redefined in place, the data property turned into an accessor and back, then deleted
        const string Code = """
            Object.defineProperty(globalThis, 'g', { value: 1, writable: false, configurable: true });
            var calls = 0;
            function rd() { return g; }
            function rdNested() { var out; for (let i = 0; i < 3; i++) { for (let j = 0; j < 2; j++) { out = g; } } return out; }
            function safe(f) { try { return String(f()); } catch (e) { return e.constructor.name; } }
            var phases = [
                function () { },
                function () { Object.defineProperty(globalThis, 'g', { value: 2 }); },
                function () { Object.defineProperty(globalThis, 'g', { get: function () { return 'getter' + (++calls); } }); },
                function () { Object.defineProperty(globalThis, 'g', { value: 3, writable: false }); },
                function () { delete globalThis.g; },
            ];
            var log = [];
            for (var p = 0; p < phases.length; p++) {
                phases[p]();
                var top;
                for (var i = 0; i < 10; i++) {
                    try { top = g; } catch (e) { top = e.constructor.name; }
                }
                log.push(top + '/' + safe(rd) + '/' + safe(rdNested) + '/' + typeof g);
            }
            log.join(';')
            """;

        Run(Code, preparation, blockUndefinedFold)
            .Should().Be("1/1/1/number;2/2/2/number;getter10/getter11/getter17/string;3/3/3/number;ReferenceError/ReferenceError/ReferenceError/undefined");
    }

    [TestCaseSource(nameof(Modes))]
    public void AWritableGlobalMadeReadOnlyRefusesWritesThroughWarmCaches(Preparation preparation, bool blockUndefinedFold)
    {
        // every writer — assignment, update and compound assignment, top level, function and nested
        // scope, sloppy and strict — runs warm while `h` is writable, then again after it is not
        const string Code = """
            var h = 0;
            var log = [];
            function sloppyAssign(v) { h = v; }
            function sloppyUpdate() { h++; }
            function sloppyNested(v) { for (let i = 0; i < 2; i++) { h = v; } }
            function strictAssign(v) { 'use strict'; h = v; }
            function strictUpdate() { 'use strict'; --h; }
            function strictNested(v) { 'use strict'; for (let i = 0; i < 2; i++) { h = v; } }
            function strictCompound() { 'use strict'; h += 1; }
            function attempt(name, f, v) { try { f(v); return name + ':ok'; } catch (e) { return name + ':' + e.constructor.name; } }
            for (var phase = 0; phase < 2; phase++) {
                if (phase === 1) {
                    Object.defineProperty(globalThis, 'h', { writable: false });
                }
                var results = [];
                for (var i = 0; i < 10; i++) {
                    sloppyAssign(i); sloppyUpdate(); sloppyNested(i);
                    h = i; h++;
                    results = [attempt('assign', strictAssign, i), attempt('update', strictUpdate), attempt('nested', strictNested, i), attempt('compound', strictCompound)];
                }
                log.push(phase + ':' + h + ':' + results.join(','));
            }
            log.push(Object.getOwnPropertyDescriptor(globalThis, 'h').writable);
            log.join(';')
            """;

        Run(Code, preparation, blockUndefinedFold)
            .Should().Be("0:10:assign:ok,update:ok,nested:ok,compound:ok;1:10:assign:TypeError,update:TypeError,nested:TypeError,compound:TypeError;false");
    }

    /// <summary>
    /// The change itself: a read of a non-writable global populates the identifier's global-binding cache.
    /// The static analysis is off so the identifier <c>undefined</c> is resolved rather than folded beforehand,
    /// which is how unprepared code reads it; that first resolution also folds it at run time, so its second
    /// read below answers from the fold, with the same value.
    /// </summary>
    [TestCase("undefined", "undefined")]
    [TestCase("NaN", "NaN")]
    [TestCase("Infinity", "Infinity")]
    [TestCase("readOnly", "host")]
    public void AReadOfAReadOnlyGlobalPopulatesTheGlobalBindingCache(string name, string expected)
    {
        using var engine = new Engine();
        engine.Execute("Object.defineProperty(globalThis, 'readOnly', { value: 'host', writable: false, configurable: true });");

        var identifier = ReadIdentifier(name);

        identifier.GetValue(new EvaluationContext(engine)).ToString().Should().Be(expected);

        var cachedGlobalEnv = identifier._cachedGlobalEnv;
        cachedGlobalEnv.Should().BeSameAs(engine.Realm.GlobalEnv, "a read of a plain global data property is cached whether or not it is writable");

        // the warm read answers from the cached descriptor with the same value
        identifier.GetValue(new EvaluationContext(engine)).ToString().Should().Be(expected);
    }

    /// <summary>
    /// The control for the test above, passing before and after the change: a writable global was always
    /// cached, so this shows the hook observes the cache at all.
    /// </summary>
    [Test]
    public void AReadOfAWritableGlobalPopulatesTheGlobalBindingCache()
    {
        using var engine = new Engine();
        engine.Execute("var writable = 'var';");

        var identifier = ReadIdentifier("writable");

        identifier.GetValue(new EvaluationContext(engine)).AsString().Should().Be("var");

        var cachedGlobalEnv = identifier._cachedGlobalEnv;
        cachedGlobalEnv.Should().BeSameAs(engine.Realm.GlobalEnv, "a read of a writable global data property is cached");
    }

    private static JintIdentifierExpression ReadIdentifier(string name)
    {
        var prepared = Engine.PrepareScript(name, options: new ScriptPreparationOptions { StaticAnalysis = false });
        var statement = prepared.Program!.Body[0].Should().BeAssignableTo<ExpressionStatement>().Which;
        var identifier = statement.Expression.Should().BeOfType<Identifier>().Which;
        return new JintIdentifierExpression(identifier);
    }
}
