#nullable enable

using System.Collections;
using Jint.Native;
using Jint.Runtime;
using Jint.Runtime.Interop;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// <c>ScriptPreparationOptions.CollectReferencedGlobals</c> makes preparation report the identifier names a program
/// references but does not declare, so a host can build only the ambient API a script actually asks for. These tests
/// pin the contract documented on <see cref="ReferencedGlobals"/> — which positions count as references, how
/// shadowing resolves per site, and the sharing guarantees — from outside the Jint assembly, which is what an
/// embedder can actually reach.
/// </summary>
public class ReferencedGlobalsTests
{
    private static ReferencedGlobals Collect(string code, bool strict = false)
    {
        var prepared = Engine.PrepareScript(code, strict: strict, options: new ScriptPreparationOptions
        {
            CollectReferencedGlobals = true,
        });

        prepared.ReferencedGlobals.Should().NotBeNull();
        return prepared.ReferencedGlobals!;
    }

    private static ReferencedGlobals CollectModule(string code)
    {
        var prepared = Engine.PrepareModule(code, options: new ModulePreparationOptions
        {
            CollectReferencedGlobals = true,
        });

        prepared.ReferencedGlobals.Should().NotBeNull();
        return prepared.ReferencedGlobals!;
    }

    // ---------------------------------------------------------------- opting in and out

    [Fact]
    public void NotCollectedByDefault()
    {
        Engine.PrepareScript("uuid();").ReferencedGlobals.Should().BeNull();
        Engine.PrepareModule("uuid();").ReferencedGlobals.Should().BeNull();

        Engine.PrepareScript("uuid();", options: new ScriptPreparationOptions())
            .ReferencedGlobals.Should().BeNull();
        Engine.PrepareModule("uuid();", options: new ModulePreparationOptions())
            .ReferencedGlobals.Should().BeNull();
    }

    [Fact]
    public void NullIsDistinguishableFromEmpty()
    {
        Engine.PrepareScript("var x = 1;").ReferencedGlobals.Should().BeNull();

        var collected = Collect("var x = 1;");
        collected.Should().BeEmpty();
        collected.Count.Should().Be(0);
    }

    [Fact]
    public void DefaultPreparedInstanceHasNoReferencedGlobals()
    {
        default(Prepared<Script>).ReferencedGlobals.Should().BeNull();
    }

    // ---------------------------------------------------------------- free reads, writes, typeof

    [Fact]
    public void FreeReadIsReported()
    {
        Collect("uuid();").Should().BeEquivalentTo(["uuid"]);
    }

    [Fact]
    public void HostShapedExpressionReportsBothRoots()
    {
        Collect("variables.Foo + libX.y").Should().BeEquivalentTo(["libX", "variables"]);
    }

    [Fact]
    public void SloppyWriteToUndeclaredNameIsReported()
    {
        Collect("undeclared = 1;").Should().BeEquivalentTo(["undeclared"]);
    }

    [Fact]
    public void CompoundAssignmentAndUpdateAreReported()
    {
        Collect("a += 1; b++; --c;").Should().BeEquivalentTo(["a", "b", "c"]);
    }

    [Fact]
    public void TypeofGuardIsReported()
    {
        Collect("typeof x === 'undefined'").Should().BeEquivalentTo(["x"]);
    }

    [Fact]
    public void DeleteOperandIsReported()
    {
        Collect("delete x;").Should().BeEquivalentTo(["x"]);
    }

    [Fact]
    public void StrictModeScriptsAreAnalysedToo()
    {
        Collect("'use strict'; typeof x; y.z;", strict: true).Should().BeEquivalentTo(["x", "y"]);
        Collect("function f(p) { return p + q; } f(r);", strict: true).Should().BeEquivalentTo(["q", "r"]);
    }

    // ---------------------------------------------------------------- per-site binding resolution

    [Fact]
    public void ParameterElsewhereDoesNotHideFreeTopLevelUse()
    {
        // the precision case: a "declared anywhere" set difference would wrongly drop `x`
        Collect("function f(x) { return x; } x;").Should().BeEquivalentTo(["x"]);
    }

    [Fact]
    public void FunctionDeclarationNameIsBoundAndNotReported()
    {
        Collect("function f() { return 1; } f();").Should().BeEmpty();
    }

    [Fact]
    public void ForwardFunctionReferenceIsNotReported()
    {
        Collect("f(); function f() {}").Should().BeEmpty();
    }

    [Fact]
    public void VarHoistsOutOfBlocks()
    {
        Collect("{ var v = 1; } v;").Should().BeEmpty();
    }

    [Fact]
    public void BlockLetShadowsInsideButNotOutside()
    {
        Collect("{ let z; z; } z;").Should().BeEquivalentTo(["z"]);
    }

    [Fact]
    public void CatchParameterBindsOnlyInsideTheClause()
    {
        Collect("try { } catch (e) { e; } e;").Should().BeEquivalentTo(["e"]);
    }

    [Fact]
    public void ForLoopBindingBindsOnlyInsideTheLoop()
    {
        Collect("for (let i = 0; i < n; i++) { i; } i;").Should().BeEquivalentTo(["i", "n"]);
    }

    [Fact]
    public void ForOfAndForInBindingsAreDeclarations()
    {
        Collect("for (const item of list) { item; } for (var k in obj) { k; }")
            .Should().BeEquivalentTo(["list", "obj"]);
    }

    [Fact]
    public void ClassDeclarationNameIsBound()
    {
        Collect("class C extends B { m() { return C; } } new C();").Should().BeEquivalentTo(["B"]);
    }

    [Fact]
    public void ClassExpressionSelfNameIsBoundInsideOnly()
    {
        Collect("var k = class D { m() { return D; } }; D;").Should().BeEquivalentTo(["D"]);
    }

    [Fact]
    public void FunctionExpressionSelfNameIsBoundInsideOnly()
    {
        Collect("var f = function foo() { return foo(); }; foo;").Should().BeEquivalentTo(["foo"]);
    }

    [Fact]
    public void StaticBlockBindingsAreScopedToTheBlock()
    {
        Collect("class F { static { let sb; sb; } } sb;").Should().BeEquivalentTo(["sb"]);
    }

    [Fact]
    public void SwitchCaseBindingDoesNotShadowTheDiscriminant()
    {
        // the switch scope covers the cases, not the discriminant
        Collect("switch (y) { case 1: let y; break; }").Should().BeEquivalentTo(["y"]);
    }

    [Fact]
    public void NestedFunctionsResolveAgainstTheirOwnChain()
    {
        const string Code = """
            function outer(a) {
                function inner(b) {
                    return a + b + c;
                }
                return inner(d);
            }
            """;

        Collect(Code).Should().BeEquivalentTo(["c", "d"]);
    }

    [Fact]
    public void ArrowParametersBindInsideTheArrow()
    {
        // arrow parameters are parsed before the arrow's scope is entered; they must still bind
        Collect("(a, b) => a + b + c;").Should().BeEquivalentTo(["c"]);
        Collect("x => y => x + y + z;").Should().BeEquivalentTo(["z"]);
    }

    [Fact]
    public void ParameterDefaultsAreReferencePositions()
    {
        Collect("function g(a = x, { b: bb = y } = {}) { return a + bb; }")
            .Should().BeEquivalentTo(["x", "y"]);
    }

    // ---------------------------------------------------------------- positions

    [Fact]
    public void NonComputedMemberPropertyNameIsNotAReference()
    {
        Collect("a.b.c;").Should().BeEquivalentTo(["a"]);
    }

    [Fact]
    public void ComputedMemberKeyIsAReference()
    {
        Collect("a[b];").Should().BeEquivalentTo(["a", "b"]);
    }

    [Fact]
    public void ObjectLiteralKeysAreNotReferencesButValuesAndComputedKeysAre()
    {
        Collect("var o = { a, b: c, [d]: e };").Should().BeEquivalentTo(["a", "c", "d", "e"]);
    }

    [Fact]
    public void AccessorKeysAreNotReferences()
    {
        Collect("var o = { get g() { return 1; }, set s(v) { v; } };").Should().BeEmpty();
    }

    [Fact]
    public void ClassMemberKeysFollowTheSameRule()
    {
        Collect("class E { m() {} [f]() {} static [g] = h; p = i; }")
            .Should().BeEquivalentTo(["f", "g", "h", "i"]);
    }

    [Fact]
    public void DestructuringDeclarationTargetsAreDeclarations()
    {
        Collect("var { p, [q]: r, s = t } = o;").Should().BeEquivalentTo(["o", "q", "t"]);
    }

    [Fact]
    public void DestructuringAssignmentTargetsAreReferences()
    {
        Collect("({ p, [q]: r } = o); [s] = arr;").Should().BeEquivalentTo(["arr", "o", "p", "q", "r", "s"]);
    }

    [Fact]
    public void ArrayPatternDeclarationTargetsAreDeclarations()
    {
        Collect("var [head, ...tail] = list; head; tail;").Should().BeEquivalentTo(["list"]);
    }

    [Fact]
    public void LabelsAreNotReferences()
    {
        Collect("outer: for (;;) { break outer; } inner: while (c) { continue inner; }")
            .Should().BeEquivalentTo(["c"]);
    }

    [Fact]
    public void TemplateLiteralTagsAndInterpolationsAreReferences()
    {
        Collect("tag`x${y}z`; `${a}${b.c}`;").Should().BeEquivalentTo(["a", "b", "tag", "y"]);
    }

    [Fact]
    public void CalleesArgumentsAndSpreadAreReferences()
    {
        Collect("fn(a, ...rest); new Ctor(b);").Should().BeEquivalentTo(["a", "b", "Ctor", "fn", "rest"]);
    }

    [Fact]
    public void MetaPropertiesAreNotReferences()
    {
        Collect("function f() { return new.target; }").Should().BeEmpty();
    }

    [Fact]
    public void ThisAndSuperAndPrivateNamesNeverAppear()
    {
        Collect("class C extends B { #p = 1; m() { return super.m() + this.#p; } }")
            .Should().BeEquivalentTo(["B"]);
    }

    // ---------------------------------------------------------------- arguments

    [Fact]
    public void ArgumentsIsBoundInsideANonArrowFunction()
    {
        Collect("function f() { return arguments; }").Should().BeEmpty();
        Collect("var f = function () { return arguments; };").Should().BeEmpty();
        Collect("var o = { m() { return arguments; } };").Should().BeEmpty();
    }

    [Fact]
    public void ArgumentsIsFreeAtTopLevelAndInsideTopLevelArrows()
    {
        Collect("arguments;").Should().BeEquivalentTo(["arguments"]);
        Collect("(() => arguments);").Should().BeEquivalentTo(["arguments"]);
    }

    [Fact]
    public void ArgumentsInsideAnArrowNestedInAFunctionIsBound()
    {
        Collect("function f() { return () => arguments; }").Should().BeEmpty();
    }

    // ---------------------------------------------------------------- with

    [Fact]
    public void WithStatementOverApproximates()
    {
        // `x` may resolve on `o` at run time, but it is lexically free and is reported
        Collect("with (o) { x; }").Should().BeEquivalentTo(["o", "x"]);
    }

    // ---------------------------------------------------------------- eval

    [Fact]
    public void DirectEvalIsFlagged()
    {
        var collected = Collect("eval('1 + 1');");
        collected.HasDirectEvalCall.Should().BeTrue();
        collected.Should().BeEquivalentTo(["eval"]);
    }

    [Fact]
    public void OptionalDirectEvalCallIsFlagged()
    {
        Collect("eval?.('1 + 1');").HasDirectEvalCall.Should().BeTrue();
    }

    [Fact]
    public void IndirectEvalIsNotFlaggedButAppearsInTheSet()
    {
        var collected = Collect("(0, eval)('1 + 1');");
        collected.HasDirectEvalCall.Should().BeFalse();
        collected.Should().BeEquivalentTo(["eval"]);
    }

    [Fact]
    public void FunctionConstructorIsNotFlaggedButAppearsInTheSet()
    {
        var collected = Collect("new Function('return 1;')();");
        collected.HasDirectEvalCall.Should().BeFalse();
        collected.Should().BeEquivalentTo(["Function"]);
    }

    [Fact]
    public void ProgramWithoutEvalIsNotFlagged()
    {
        Collect("evaluate(); o.eval();").HasDirectEvalCall.Should().BeFalse();
    }

    // ---------------------------------------------------------------- Annex B

    [Fact]
    public void AnnexBBlockFunctionFollowsTheParserScopeModel()
    {
        // sloppy-mode Annex B also creates a global `var fb`, so this over-approximates, which is the safe direction
        Collect("{ function fb() {} } fb();").Should().BeEquivalentTo(["fb"]);
        Collect("'use strict'; { function fb() {} } fb();", strict: true).Should().BeEquivalentTo(["fb"]);
    }

    // ---------------------------------------------------------------- modules

    [Fact]
    public void ImportBindingsAreDeclarationsAndImportedNamesAreNotReferences()
    {
        CollectModule("import d, { a as b } from 'm'; d; b; g;").Should().BeEquivalentTo(["g"]);
    }

    [Fact]
    public void NamespaceImportIsADeclaration()
    {
        CollectModule("import * as ns from 'm'; ns.thing;").Should().BeEmpty();
    }

    [Fact]
    public void ExportedAliasesAreNotReferencesButLocalsAre()
    {
        // `export { b as c }` requires `b` to be declared, so the local always resolves; what matters is that the
        // alias `c` is never mistaken for a reference
        CollectModule("const b = compute(); export { b as c };").Should().BeEquivalentTo(["compute"]);
        CollectModule("export * as ns from 'n';").Should().BeEmpty();
        CollectModule("export { a as b } from 'm';").Should().BeEmpty();
    }

    [Fact]
    public void ExportedDeclarationsBindTheirNames()
    {
        CollectModule("export const value = compute(); value;").Should().BeEquivalentTo(["compute"]);
        CollectModule("export default function local() { return local; } local;").Should().BeEmpty();
    }

    [Fact]
    public void DynamicImportArgumentIsAReference()
    {
        CollectModule("import(specifier);").Should().BeEquivalentTo(["specifier"]);
    }

    [Fact]
    public void ImportMetaIsNotAReference()
    {
        CollectModule("var u = import.meta.url;").Should().BeEmpty();
    }

    // ---------------------------------------------------------------- collection semantics

    [Fact]
    public void NamesAreOrdinalSortedAndDeduplicated()
    {
        var collected = Collect("b; a; B; a; A; b;");

        collected.Should().Equal("A", "B", "a", "b");
        collected.Count.Should().Be(4);
    }

    [Fact]
    public void ContainsIsOrdinal()
    {
        var collected = Collect("uuid();");

        collected.Contains("uuid").Should().BeTrue();
        collected.Contains("UUID").Should().BeFalse();
        collected.Contains("nope").Should().BeFalse();
        Invoking(() => collected.Contains(null!)).Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void NonGenericEnumerationWorks()
    {
        var collected = Collect("b; a;");

        var names = new List<string>();
        foreach (var name in (IEnumerable) collected)
        {
            names.Add((string) name);
        }

        names.Should().Equal("a", "b");
    }

    [Fact]
    public void SameSourceProducesTheSameSet()
    {
        const string Code = "variables.Foo + libX.y + (function (p) { return p + q; })(r);";

        Collect(Code).Should().Equal(Collect(Code));
    }

    // ---------------------------------------------------------------- sharing

    [Fact]
    public void OnePreparedScriptCanDriveManyEngines()
    {
        var prepared = Engine.PrepareScript("host.value + extra;", options: new ScriptPreparationOptions
        {
            CollectReferencedGlobals = true,
        });

        var referenced = prepared.ReferencedGlobals!;
        referenced.Should().BeEquivalentTo(["extra", "host"]);

        Parallel.For(0, 20, _ =>
        {
            // the collection must be readable from every engine's thread while the script runs
            referenced.Contains("host").Should().BeTrue();
            referenced.Count.Should().Be(2);

            var engine = new Engine();
            engine.SetValue("host", JsObject.Create(engine, new JsObjectLayout("value"), [JsNumber.Create(40)]));
            engine.SetValue("extra", 2);
            engine.Evaluate(prepared).AsNumber().Should().Be(42);
        });
    }

    // ---------------------------------------------------------------- the host workflow

    /// <summary>
    /// The motivating end-to-end pattern: a host with a large ambient API prepares the script once, intersects the
    /// referenced names with its registry, and builds only the intersection.
    /// </summary>
    [Fact]
    public void HostRegistersOnlyTheIntersectionOfItsRegistryAndTheReferencedNames()
    {
        var built = new List<string>();

        var registry = new Dictionary<string, Func<Engine, JsValue>>(StringComparer.Ordinal)
        {
            ["variables"] = e =>
            {
                built.Add("variables");
                return JsObject.Create(e, new JsObjectLayout("Amount"), [JsNumber.Create(40)]);
            },
            ["libX"] = e =>
            {
                built.Add("libX");
                return JsObject.Create(e, new JsObjectLayout("factor"), [JsNumber.Create(2)]);
            },
            ["expensiveWorkflowContext"] = e =>
            {
                built.Add("expensiveWorkflowContext");
                return new ClrFunction(e, "expensiveWorkflowContext", (_, _) => JsValue.Undefined);
            },
            ["anotherUnusedApi"] = e =>
            {
                built.Add("anotherUnusedApi");
                return JsValue.Undefined;
            },
        };

        var prepared = Engine.PrepareScript("variables.Amount + libX.factor;", options: new ScriptPreparationOptions
        {
            CollectReferencedGlobals = true,
        });

        var referenced = prepared.ReferencedGlobals!;
        referenced.HasDirectEvalCall.Should().BeFalse("otherwise the host would have to install everything");

        var engine = new Engine();
        foreach (var entry in registry)
        {
            if (referenced.Contains(entry.Key))
            {
                engine.SetValue(entry.Key, entry.Value(engine));
            }
        }

        engine.Evaluate(prepared).AsNumber().Should().Be(42);
        built.Should().BeEquivalentTo(["libX", "variables"]);
    }

    [Fact]
    public void DirectEvalTellsAHostToInstallEverything()
    {
        var prepared = Engine.PrepareScript("eval('unlisted');", options: new ScriptPreparationOptions
        {
            CollectReferencedGlobals = true,
        });

        var referenced = prepared.ReferencedGlobals!;
        referenced.Should().BeEquivalentTo(["eval"]);
        referenced.Contains("unlisted").Should().BeFalse();

        // the flag is the host's signal that the set is not the whole story
        referenced.HasDirectEvalCall.Should().BeTrue();

        var engine = new Engine();
        engine.SetValue("unlisted", 42);
        engine.Evaluate(prepared).AsNumber().Should().Be(42);
    }
}
