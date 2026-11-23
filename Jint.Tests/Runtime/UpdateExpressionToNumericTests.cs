#nullable enable

namespace Jint.Tests.Runtime;

/// <summary>
/// Pins how an update expression converts its operand. Every form converts the old value exactly once, with ToNumeric,
/// before PutValue, computes the new value from that numeric with Number::add or BigInt::add, and the postfix form
/// returns the old <em>numeric</em> value rather than the operand
/// (https://tc39.es/ecma262/#sec-postfix-increment-operator-runtime-semantics-evaluation,
/// https://tc39.es/ecma262/#sec-postfix-decrement-operator-runtime-semantics-evaluation,
/// https://tc39.es/ecma262/#sec-prefix-increment-operator-runtime-semantics-evaluation,
/// https://tc39.es/ecma262/#sec-prefix-decrement-operator-runtime-semantics-evaluation,
/// https://tc39.es/ecma262/#sec-tonumeric).
/// </summary>
/// <remarks>
/// <para>Each script runs the four operators three times in a loop, so a global identifier takes the slow
/// environment path on its first pass and the cached-global lane on the next two; a function, generator or async
/// local takes the environment path, and a member target the reference path. The operand is recreated on every
/// iteration, and the conversion hooks it carries log every call.</para>
/// <para>The expected strings were taken from Node 26 running the same scripts in script scope
/// (<c>vm.runInThisContext</c>): every scope and target gives the same answer, so the expectation depends only on
/// the operand.</para>
/// </remarks>
public class UpdateExpressionToNumericTests
{
    public enum Scope
    {
        TopLevel,
        Function,
        Generator,
        AsyncFunction,
        With,
    }

    public enum Target
    {
        Identifier,
        Member,
        Computed,
    }

    /// <summary>
    /// The operand expression, which may log to <c>calls</c>, and what Node reports for
    /// <c>x++</c>, <c>++x</c>, <c>x--</c> and <c>--x</c> on it, in that order: the logged calls, then the type and
    /// value of the expression, then the type and value of the target afterwards.
    /// </summary>
    private static readonly Dictionary<string, (string Source, string Expected)> Operands = new()
    {
        ["valueOf"] = (
            "{ valueOf: function () { calls.push('valueOf'); return 5; } }",
            "valueOf|number:5|number:6;valueOf|number:6|number:6;valueOf|number:5|number:4;valueOf|number:4|number:4"),
        ["valueOfDouble"] = (
            "{ valueOf: function () { calls.push('valueOf'); return 1.5; } }",
            "valueOf|number:1.5|number:2.5;valueOf|number:2.5|number:2.5;valueOf|number:1.5|number:0.5;valueOf|number:0.5|number:0.5"),
        ["valueOfBigInt"] = (
            "{ valueOf: function () { calls.push('valueOf'); return 5n; } }",
            "valueOf|bigint:5|bigint:6;valueOf|bigint:6|bigint:6;valueOf|bigint:5|bigint:4;valueOf|bigint:4|bigint:4"),
        ["toPrimitive"] = (
            "{ [Symbol.toPrimitive]: function (hint) { calls.push('toPrimitive:' + hint); return 5; } }",
            "toPrimitive:number|number:5|number:6;toPrimitive:number|number:6|number:6;toPrimitive:number|number:5|number:4;toPrimitive:number|number:4|number:4"),
        ["toPrimitiveBigInt"] = (
            "{ [Symbol.toPrimitive]: function (hint) { calls.push('toPrimitive:' + hint); return 5n; } }",
            "toPrimitive:number|bigint:5|bigint:6;toPrimitive:number|bigint:6|bigint:6;toPrimitive:number|bigint:5|bigint:4;toPrimitive:number|bigint:4|bigint:4"),
        ["toString"] = (
            "{ valueOf: null, toString: function () { calls.push('toString'); return '5'; } }",
            "toString|number:5|number:6;toString|number:6|number:6;toString|number:5|number:4;toString|number:4|number:4"),
        ["valueOfObjectThenToString"] = (
            "{ valueOf: function () { calls.push('valueOf'); return {}; }, toString: function () { calls.push('toString'); return '5'; } }",
            "valueOf,toString|number:5|number:6;valueOf,toString|number:6|number:6;valueOf,toString|number:5|number:4;valueOf,toString|number:4|number:4"),
        ["valueOfSymbol"] = (
            "{ valueOf: function () { calls.push('valueOf'); return Symbol(); } }",
            "valueOf|throw:TypeError;valueOf|throw:TypeError;valueOf|throw:TypeError;valueOf|throw:TypeError"),
        ["integer"] = (
            "5",
            "|number:5|number:6;|number:6|number:6;|number:5|number:4;|number:4|number:4"),
        ["double"] = (
            "1.5",
            "|number:1.5|number:2.5;|number:2.5|number:2.5;|number:1.5|number:0.5;|number:0.5|number:0.5"),
        ["bigint"] = (
            "5n",
            "|bigint:5|bigint:6;|bigint:6|bigint:6;|bigint:5|bigint:4;|bigint:4|bigint:4"),
        ["string"] = (
            "'5'",
            "|number:5|number:6;|number:6|number:6;|number:5|number:4;|number:4|number:4"),
        ["nonNumericString"] = (
            "'a'",
            "|number:NaN|number:NaN;|number:NaN|number:NaN;|number:NaN|number:NaN;|number:NaN|number:NaN"),
        ["true"] = (
            "true",
            "|number:1|number:2;|number:2|number:2;|number:1|number:0;|number:0|number:0"),
        ["false"] = (
            "false",
            "|number:0|number:1;|number:1|number:1;|number:0|number:-1;|number:-1|number:-1"),
        ["null"] = (
            "null",
            "|number:0|number:1;|number:1|number:1;|number:0|number:-1;|number:-1|number:-1"),
        ["undefined"] = (
            "undefined",
            "|number:NaN|number:NaN;|number:NaN|number:NaN;|number:NaN|number:NaN;|number:NaN|number:NaN"),
        ["symbol"] = (
            "Symbol()",
            "|throw:TypeError;|throw:TypeError;|throw:TypeError;|throw:TypeError"),
    };

    public static TheoryData<Scope, Target, string, bool> Cases()
    {
        var data = new TheoryData<Scope, Target, string, bool>();
        foreach (var operatorOverloading in new[] { false, true })
        {
            foreach (var scope in (Scope[]) Enum.GetValues(typeof(Scope)))
            {
                foreach (var target in (Target[]) Enum.GetValues(typeof(Target)))
                {
                    foreach (var operand in Operands.Keys)
                    {
                        data.Add(scope, target, operand, operatorOverloading);
                    }
                }
            }
        }

        return data;
    }

    /// <summary>
    /// Builds the script the Node expectations were taken from. <c>{pause}</c> becomes a suspension point in the
    /// generator and async scopes, so the update runs after a resume.
    /// </summary>
    internal static string BuildScript(Scope scope, Target target, string operandSource)
    {
        var pause = scope switch
        {
            Scope.Generator => "yield i, ",
            Scope.AsyncFunction => "await i, ",
            _ => "",
        };

        var reference = target switch
        {
            Target.Identifier => "x",
            Target.Member => "o.p",
            _ => "o[k]",
        };

        var steps = new List<string>();
        foreach (var update in new[] { reference + "++", "++" + reference, reference + "--", "--" + reference })
        {
            steps.Add(
                "calls = []; " + reference + " = (" + operandSource + "); "
                + "try { r = (" + pause + update + "); line.push(calls.join(',') + '|' + typeof r + ':' + String(r) + '|' + typeof " + reference + " + ':' + String(" + reference + ")); } "
                + "catch (e) { line.push(calls.join(',') + '|throw:' + e.constructor.name); }");
        }

        var loop = "for (var i = 0; i < 3; i++) { var line = []; " + string.Join(" ", steps) + " out.push(line.join(';')); }";
        var locals = "var x, r, calls, o = {}, k = 'p'; ";
        const string Finish = "done = out.join('\\n');";

        return "var out = [], done;\n" + scope switch
        {
            Scope.TopLevel => locals + loop + "\n" + Finish + "\n",
            Scope.Function => "(function () { " + locals + loop + " })();\n" + Finish + "\n",
            Scope.Generator => "function* g() { " + locals + loop + " }\nfor (var it = g(), s = it.next(); !s.done; s = it.next()) {}\n" + Finish + "\n",
            Scope.AsyncFunction => "async function a() { " + locals + loop + " }\na().then(function () { " + Finish + " });\n",
            _ => "(function () { var r, calls, o = {}, k = 'p'; var scope = { x: 0 }; with (scope) { " + loop + " } })();\n" + Finish + "\n",
        };
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void ConvertsTheOperandOnceWithToNumeric(Scope scope, Target target, string operand, bool operatorOverloading)
    {
        var (source, expected) = Operands[operand];
        var script = BuildScript(scope, target, source);

        using var engine = CreateEngine(operatorOverloading);
        engine.Execute(script);

        engine.GetValue("done").AsString().Should().Be(string.Join("\n", Enumerable.Repeat(expected, 3)), script);
    }

    private static Engine CreateEngine(bool operatorOverloading) =>
        operatorOverloading ? new Engine(options => options.Interop.AllowOperatorOverloading = true) : new Engine();

    /// <summary>
    /// The member form reads the property once, converts it once, and only then stores
    /// (https://tc39.es/ecma262/#sec-postfix-increment-operator-runtime-semantics-evaluation). With operator
    /// overloading allowed the CLR operator lookup reuses the value already read rather than reading it again.
    /// </summary>
    [Theory]
    [InlineData("o.p++", "get,valueOf,set:6|5", false)]
    [InlineData("++o.p", "get,valueOf,set:6|6", false)]
    [InlineData("o.p--", "get,valueOf,set:4|5", false)]
    [InlineData("--o.p", "get,valueOf,set:4|4", false)]
    [InlineData("o[k]++", "get,valueOf,set:6|5", false)]
    [InlineData("o.p++", "get,valueOf,set:6|5", true)]
    [InlineData("--o.p", "get,valueOf,set:4|4", true)]
    [InlineData("o[k]++", "get,valueOf,set:6|5", true)]
    public void MemberUpdateReadsConvertsAndStoresInOrder(string update, string expected, bool operatorOverloading)
    {
        using var engine = CreateEngine(operatorOverloading);
        var result = engine.Evaluate($$"""
            var events = [], k = 'p';
            var o = {
                get p() { events.push('get'); return { valueOf: function () { events.push('valueOf'); return 5; } }; },
                set p(v) { events.push('set:' + v); }
            };
            var r = {{update}};
            events.join(',') + '|' + r;
            """);

        result.AsString().Should().Be(expected);
    }

    /// <summary>
    /// The operand expression is evaluated once: with operator overloading allowed the CLR operator lookup used to
    /// evaluate it a second time, calling the key expression again.
    /// </summary>
    [Theory]
    [InlineData("o[key()]++", false)]
    [InlineData("--o[key()]", false)]
    [InlineData("o[key()]++", true)]
    [InlineData("--o[key()]", true)]
    public void ComputedTargetIsEvaluatedOnce(string update, bool operatorOverloading)
    {
        using var engine = CreateEngine(operatorOverloading);
        var result = engine.Evaluate($$"""
            var keys = 0;
            function key() { keys++; return 'p'; }
            var o = { p: 5 };
            var r = {{update}};
            keys + '|' + r + '|' + o.p;
            """);

        result.AsString().Should().Be(update.StartsWith("--", StringComparison.Ordinal) ? "1|4|4" : "1|5|6");
    }

    /// <summary>
    /// A global accessor binding: the getter runs once and the conversion once, before the setter.
    /// </summary>
    [Theory]
    [InlineData("g++", "get,valueOf,set:6|5", false)]
    [InlineData("--g", "get,valueOf,set:4|4", false)]
    [InlineData("g++", "get,valueOf,set:6|5", true)]
    [InlineData("--g", "get,valueOf,set:4|4", true)]
    public void GlobalAccessorUpdateReadsConvertsAndStoresInOrder(string update, string expected, bool operatorOverloading)
    {
        using var engine = CreateEngine(operatorOverloading);
        var result = engine.Evaluate($$"""
            var events = [], r;
            Object.defineProperty(globalThis, 'g', {
                get: function () { events.push('get'); return { valueOf: function () { events.push('valueOf'); return 5; } }; },
                set: function (v) { events.push('set:' + v); },
                configurable: true
            });
            for (var i = 0; i < 3; i++) { events = []; r = {{update}}; }
            events.join(',') + '|' + r;
            """);

        result.AsString().Should().Be(expected);
    }
}