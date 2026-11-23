#nullable enable

namespace Jint.Tests.Runtime;

/// <summary>
/// Pins the write lanes of the global-binding inline cache against user code that runs after the reference is
/// resolved and before the value is stored. An assignment resolves its target, then evaluates the right-hand side
/// (https://tc39.es/ecma262/#sec-assignment-operators-runtime-semantics-evaluation); an update converts the old value
/// with ToNumeric, which calls <c>valueOf</c> (https://tc39.es/ecma262/#sec-postfix-increment-operator-runtime-semantics-evaluation,
/// https://tc39.es/ecma262/#sec-prefix-increment-operator-runtime-semantics-evaluation); a <c>var</c> declaration
/// evaluates its initializer (https://tc39.es/ecma262/#sec-variable-statement-runtime-semantics-evaluation). Only then
/// does PutValue (https://tc39.es/ecma262/#sec-putvalue) reach the global environment's SetMutableBinding
/// (https://tc39.es/ecma262/#sec-global-environment-records-setmutablebinding-n-v-s,
/// https://tc39.es/ecma262/#sec-object-environment-records-setmutablebinding-n-v-s), which sees the property as it
/// is at that point.
/// </summary>
/// <remarks>
/// <para>Each scenario warms the cache for three iterations and changes the global during the fourth, from inside the
/// user code: it makes the global read-only, deletes it, or turns it into an accessor. A store through the descriptor
/// cached before that code ran is one the specification never makes, and shows up as a wrong value, a missing
/// exception or a setter that is not called.</para>
/// <para>The expected strings were taken from Node 26, running each script in script scope
/// (<c>vm.runInThisContext</c>). Every scenario gives the same answer in all four scopes, so the expectation depends
/// only on strictness. The generator and async scopes suspend inside the right-hand side, so the store happens after a
/// resume. The compound-assignment rows are controls: <c>+=</c> stores through PutValue and never through the
/// cached descriptor, and they pass without the fix.</para>
/// </remarks>
public class GlobalBindingWriteAfterUserCodeTests
{
    public enum Scope
    {
        TopLevel,
        Function,
        Generator,
        AsyncFunction,
    }

    public static TheoryData<bool, Scope> StrictnessAndScopes { get; } = new()
    {
        { true, Scope.TopLevel },
        { true, Scope.Function },
        { true, Scope.Generator },
        { true, Scope.AsyncFunction },
        { false, Scope.TopLevel },
        { false, Scope.Function },
        { false, Scope.Generator },
        { false, Scope.AsyncFunction },
    };

    public static TheoryData<bool, Scope, bool> StrictnessScopesAndPrefix { get; } = new()
    {
        { true, Scope.TopLevel, false },
        { true, Scope.Function, false },
        { true, Scope.Generator, false },
        { true, Scope.AsyncFunction, false },
        { false, Scope.TopLevel, false },
        { false, Scope.Function, false },
        { false, Scope.Generator, false },
        { false, Scope.AsyncFunction, false },
        { true, Scope.TopLevel, true },
        { true, Scope.Function, true },
        { true, Scope.Generator, true },
        { true, Scope.AsyncFunction, true },
        { false, Scope.TopLevel, true },
        { false, Scope.Function, true },
        { false, Scope.Generator, true },
        { false, Scope.AsyncFunction, true },
    };

    private const string ReadOnlyAtThree = """
        var x = 0;
        function v(i) { if (i === 3) Object.defineProperty(globalThis, 'x', { writable: false }); return i; }

        """;

    private const string DeletedAtThree = """
        globalThis.z = 0;
        function v(i) { if (i === 3) delete globalThis.z; return i; }

        """;

    private const string AccessorAtThree = """
        globalThis.w = 0;
        var stored = 'none';
        function v(i) { if (i === 3) Object.defineProperty(globalThis, 'w', { get: function () { return 'get:' + stored; }, set: function (value) { stored = value; out.push('set:' + value); }, configurable: true }); return i; }

        """;

    /// <summary>
    /// Builds the script the Node expectations were taken from: <paramref name="body"/> runs in a five-iteration loop
    /// whose exceptions are logged and end it, and <c>{pause}</c> in it becomes a suspension point in the generator
    /// and async scopes.
    /// </summary>
    private static string Run(string prelude, string body, string epilogue, bool strict, Scope scope)
    {
        var pause = scope switch
        {
            Scope.Generator => "yield i, ",
            Scope.AsyncFunction => "await i, ",
            _ => "",
        };

        var loop = "for (var i = 0; i < 5; i++) { try { " + body.Replace("{pause}", pause) + " } catch (e) { out.push('throw@' + i + ':' + e.constructor.name); break; } }";
        var finish = epilogue + " done = out.join(';');";
        var code = (strict ? "'use strict';\n" : "") + "var out = [], done;\n" + prelude + scope switch
        {
            Scope.TopLevel => loop + "\n" + finish + "\n",
            Scope.Function => "(function () { " + loop + " })();\n" + finish + "\n",
            Scope.Generator => "function* g() { " + loop + " }\nfor (var it = g(), s = it.next(); !s.done; s = it.next()) {}\n" + finish + "\n",
            _ => "async function a() { " + loop + " }\na().then(function () { " + finish + " });\n",
        };

        using var engine = new Engine();
        engine.Execute(code);
        return engine.Evaluate("done").AsString();
    }

    [Theory]
    [MemberData(nameof(StrictnessAndScopes))]
    public void AnAssignmentWhoseRightHandSideMakesTheGlobalReadOnlyDoesNotStore(bool strict, Scope scope)
    {
        Run(ReadOnlyAtThree, "x = ({pause}v(i));", "out.push('x=' + x);", strict, scope)
            .Should().Be(strict ? "throw@3:TypeError;x=2" : "x=2");
    }

    [Theory]
    [MemberData(nameof(StrictnessAndScopes))]
    public void AnAssignmentWhoseRightHandSideDeletesTheGlobalCompletesThroughTheBinding(bool strict, Scope scope)
    {
        // strict: the binding no longer exists, a ReferenceError; sloppy: the property is created again
        Run(
                DeletedAtThree,
                "z = ({pause}v(i)); if (i === 3) out.push('after3=' + ('z' in globalThis ? z : 'none'));",
                "out.push('z=' + ('z' in globalThis ? z : 'none'));",
                strict,
                scope)
            .Should().Be(strict ? "throw@3:ReferenceError;z=none" : "after3=3;z=4");
    }

    [Theory]
    [MemberData(nameof(StrictnessAndScopes))]
    public void AnAssignmentWhoseRightHandSideMakesTheGlobalAnAccessorCallsItsSetter(bool strict, Scope scope)
    {
        Run(AccessorAtThree, "w = ({pause}v(i));", "out.push('w=' + w);", strict, scope)
            .Should().Be("set:3;set:4;w=get:4");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AVarDeclarationWhoseInitializerMakesTheGlobalReadOnlyDoesNotStore(bool strict)
    {
        // a declaration's initializer reaches the same cached lane as an assignment's right-hand side; only the
        // read-only case applies, because a var-declared global is not configurable and cannot be deleted or redefined
        Run(ReadOnlyAtThree, "var x = ({pause}v(i));", "out.push('x=' + x);", strict, Scope.TopLevel)
            .Should().Be(strict ? "throw@3:TypeError;x=2" : "x=2");
    }

    [Theory]
    [MemberData(nameof(StrictnessScopesAndPrefix))]
    public void AnUpdateWhoseValueOfMakesTheGlobalReadOnlyDoesNotStore(bool strict, Scope scope, bool prefix)
    {
        const string Prelude = """
            var y = 0, r;
            var obj = { valueOf: function () { Object.defineProperty(globalThis, 'y', { writable: false }); return 100; } };

            """;

        string expected;
        if (strict)
        {
            expected = "throw@3:TypeError;y=[object Object];r=" + (prefix ? "3" : "2");
        }
        else
        {
            expected = "y=[object Object];r=" + (prefix ? "101" : "100");
        }

        Run(Prelude, "if (i === 3) y = ({pause}obj); r = " + (prefix ? "++y" : "y++") + ";", "out.push('y=' + String(y), 'r=' + r);", strict, scope)
            .Should().Be(expected);
    }

    [Theory]
    [MemberData(nameof(StrictnessScopesAndPrefix))]
    public void AnUpdateWhoseValueOfDeletesTheGlobalCompletesThroughTheBinding(bool strict, Scope scope, bool prefix)
    {
        // valueOf deletes only once, so the scenario measures the store and nothing else
        const string Prelude = """
            globalThis.u = 0;
            var r, deleted = false;
            var obj = { valueOf: function () { if (!deleted) { deleted = true; delete globalThis.u; } return 100; } };

            """;

        string expected;
        if (strict)
        {
            expected = "throw@3:ReferenceError;u=none;r=" + (prefix ? "3" : "2");
        }
        else
        {
            expected = "after3=101;u=102;r=" + (prefix ? "102" : "101");
        }

        Run(
                Prelude,
                "if (i === 3) u = ({pause}obj); r = " + (prefix ? "++u" : "u++") + "; if (i === 3) out.push('after3=' + ('u' in globalThis ? String(u) : 'none'));",
                "out.push('u=' + ('u' in globalThis ? String(u) : 'none'), 'r=' + r);",
                strict,
                scope)
            .Should().Be(expected);
    }

    [Theory]
    [MemberData(nameof(StrictnessScopesAndPrefix))]
    public void AnUpdateWhoseValueOfMakesTheGlobalAnAccessorCallsItsSetter(bool strict, Scope scope, bool prefix)
    {
        const string Prelude = """
            globalThis.t = 0;
            var r, stored = 'none';
            var obj = { valueOf: function () { Object.defineProperty(globalThis, 't', { get: function () { return stored; }, set: function (value) { stored = value; out.push('set:' + value); }, configurable: true }); return 100; } };

            """;

        Run(Prelude, "if (i === 3) t = ({pause}obj); r = " + (prefix ? "++t" : "t++") + ";", "out.push('t=' + t, 'r=' + r);", strict, scope)
            .Should().Be("set:101;set:102;t=102;r=" + (prefix ? "102" : "101"));
    }

    [Theory]
    [MemberData(nameof(StrictnessAndScopes))]
    public void ACompoundAssignmentWhoseRightHandSideMakesTheGlobalReadOnlyDoesNotStore(bool strict, Scope scope)
    {
        Run(ReadOnlyAtThree, "x += ({pause}v(i));", "out.push('x=' + x);", strict, scope)
            .Should().Be(strict ? "throw@3:TypeError;x=3" : "x=3");
    }

    [Theory]
    [MemberData(nameof(StrictnessAndScopes))]
    public void ACompoundAssignmentWhoseRightHandSideDeletesTheGlobalCompletesThroughTheBinding(bool strict, Scope scope)
    {
        Run(
                DeletedAtThree,
                "z += ({pause}v(i)); if (i === 3) out.push('after3=' + ('z' in globalThis ? z : 'none'));",
                "out.push('z=' + ('z' in globalThis ? z : 'none'));",
                strict,
                scope)
            .Should().Be(strict ? "throw@3:ReferenceError;z=none" : "after3=6;z=10");
    }

    [Theory]
    [MemberData(nameof(StrictnessAndScopes))]
    public void ACompoundAssignmentWhoseRightHandSideMakesTheGlobalAnAccessorCallsItsSetter(bool strict, Scope scope)
    {
        Run(AccessorAtThree, "w += ({pause}v(i));", "out.push('w=' + w);", strict, scope)
            .Should().Be("set:6;set:get:64;w=get:get:64");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AnAssignmentReenteredFromItsOwnRightHandSideStoresIntoTheGlobalAsItIsAfterwards(bool strict)
    {
        // The inner call deletes the global, creates it again and assigns it through the same node, which warms that
        // node's cache again for the new property. The outer assignment must still not store into the descriptor it
        // validated before its right-hand side ran: that one belongs to the deleted property.
        const string Code = """
            var out = [], done;
            globalThis.q = 0;
            function f(value) { q = value === 'recurse' ? inner() : value; }
            function inner() { delete globalThis.q; globalThis.q = 'fresh'; f('again'); return 'outer'; }
            for (var i = 0; i < 5; i++) f(i);
            f('recurse');
            out.push('q=' + q); done = out.join(';');
            """;

        using var engine = new Engine();
        engine.Execute((strict ? "'use strict';\n" : "") + Code);
        engine.Evaluate("done").AsString().Should().Be("q=outer");
    }
}
