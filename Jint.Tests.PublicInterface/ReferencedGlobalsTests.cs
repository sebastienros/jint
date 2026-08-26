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

    [Test]
    public void NotCollectedByDefault()
    {
        Engine.PrepareScript("uuid();").ReferencedGlobals.Should().BeNull();
        Engine.PrepareModule("uuid();").ReferencedGlobals.Should().BeNull();

        Engine.PrepareScript("uuid();", options: new ScriptPreparationOptions())
            .ReferencedGlobals.Should().BeNull();
        Engine.PrepareModule("uuid();", options: new ModulePreparationOptions())
            .ReferencedGlobals.Should().BeNull();
    }

    [Test]
    public void NullIsDistinguishableFromEmpty()
    {
        Engine.PrepareScript("var x = 1;").ReferencedGlobals.Should().BeNull();

        var collected = Collect("var x = 1;");
        collected.Should().BeEmpty();
        collected.Count.Should().Be(0);
    }

    [Test]
    public void DefaultPreparedInstanceHasNoReferencedGlobals()
    {
        default(Prepared<Script>).ReferencedGlobals.Should().BeNull();
    }

    // ---------------------------------------------------------------- free reads, writes, typeof

    [Test]
    public void FreeReadIsReported()
    {
        Collect("uuid();").Should().BeEquivalentTo(["uuid"]);
    }

    [Test]
    public void HostShapedExpressionReportsBothRoots()
    {
        Collect("variables.Foo + libX.y").Should().BeEquivalentTo(["libX", "variables"]);
    }

    [Test]
    public void SloppyWriteToUndeclaredNameIsReported()
    {
        Collect("undeclared = 1;").Should().BeEquivalentTo(["undeclared"]);
    }

    [Test]
    public void CompoundAssignmentAndUpdateAreReported()
    {
        Collect("a += 1; b++; --c;").Should().BeEquivalentTo(["a", "b", "c"]);
    }

    [Test]
    public void TypeofGuardIsReported()
    {
        Collect("typeof x === 'undefined'").Should().BeEquivalentTo(["x"]);
    }

    [Test]
    public void DeleteOperandIsReported()
    {
        Collect("delete x;").Should().BeEquivalentTo(["x"]);
    }

    [Test]
    public void StrictModeScriptsAreAnalysedToo()
    {
        Collect("'use strict'; typeof x; y.z;", strict: true).Should().BeEquivalentTo(["x", "y"]);
        Collect("function f(p) { return p + q; } f(r);", strict: true).Should().BeEquivalentTo(["q", "r"]);
    }

    // ---------------------------------------------------------------- per-site binding resolution

    [Test]
    public void ParameterElsewhereDoesNotHideFreeTopLevelUse()
    {
        // the precision case: a "declared anywhere" set difference would wrongly drop `x`
        Collect("function f(x) { return x; } x;").Should().BeEquivalentTo(["x"]);
    }

    [Test]
    public void FunctionDeclarationNameIsBoundAndNotReported()
    {
        Collect("function f() { return 1; } f();").Should().BeEmpty();
    }

    [Test]
    public void ForwardFunctionReferenceIsNotReported()
    {
        Collect("f(); function f() {}").Should().BeEmpty();
    }

    [Test]
    public void VarHoistsOutOfBlocks()
    {
        Collect("{ var v = 1; } v;").Should().BeEmpty();
    }

    [Test]
    public void BlockLetShadowsInsideButNotOutside()
    {
        Collect("{ let z; z; } z;").Should().BeEquivalentTo(["z"]);
    }

    [Test]
    public void CatchParameterBindsOnlyInsideTheClause()
    {
        Collect("try { } catch (e) { e; } e;").Should().BeEquivalentTo(["e"]);
    }

    [Test]
    public void ForLoopBindingBindsOnlyInsideTheLoop()
    {
        Collect("for (let i = 0; i < n; i++) { i; } i;").Should().BeEquivalentTo(["i", "n"]);
    }

    [Test]
    public void ForOfAndForInBindingsAreDeclarations()
    {
        Collect("for (const item of list) { item; } for (var k in obj) { k; }")
            .Should().BeEquivalentTo(["list", "obj"]);
    }

    [Test]
    public void ClassDeclarationNameIsBound()
    {
        Collect("class C extends B { m() { return C; } } new C();").Should().BeEquivalentTo(["B"]);
    }

    [Test]
    public void ClassExpressionSelfNameIsBoundInsideOnly()
    {
        Collect("var k = class D { m() { return D; } }; D;").Should().BeEquivalentTo(["D"]);
    }

    [Test]
    public void FunctionExpressionSelfNameIsBoundInsideOnly()
    {
        Collect("var f = function foo() { return foo(); }; foo;").Should().BeEquivalentTo(["foo"]);
    }

    [Test]
    public void StaticBlockBindingsAreScopedToTheBlock()
    {
        Collect("class F { static { let sb; sb; } } sb;").Should().BeEquivalentTo(["sb"]);
    }

    [Test]
    public void SwitchCaseBindingDoesNotShadowTheDiscriminant()
    {
        // the switch scope covers the cases, not the discriminant
        Collect("switch (y) { case 1: let y; break; }").Should().BeEquivalentTo(["y"]);
    }

    [Test]
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

    [Test]
    public void ArrowParametersBindInsideTheArrow()
    {
        // arrow parameters are parsed before the arrow's scope is entered; they must still bind
        Collect("(a, b) => a + b + c;").Should().BeEquivalentTo(["c"]);
        Collect("x => y => x + y + z;").Should().BeEquivalentTo(["z"]);
    }

    [Test]
    public void ParameterDefaultsAreReferencePositions()
    {
        Collect("function g(a = x, { b: bb = y } = {}) { return a + bb; }")
            .Should().BeEquivalentTo(["x", "y"]);
    }

    // ---------------------------------------------------------------- positions

    [Test]
    public void NonComputedMemberPropertyNameIsNotAReference()
    {
        Collect("a.b.c;").Should().BeEquivalentTo(["a"]);
    }

    [Test]
    public void ComputedMemberKeyIsAReference()
    {
        Collect("a[b];").Should().BeEquivalentTo(["a", "b"]);
    }

    [Test]
    public void ObjectLiteralKeysAreNotReferencesButValuesAndComputedKeysAre()
    {
        Collect("var o = { a, b: c, [d]: e };").Should().BeEquivalentTo(["a", "c", "d", "e"]);
    }

    [Test]
    public void AccessorKeysAreNotReferences()
    {
        Collect("var o = { get g() { return 1; }, set s(v) { v; } };").Should().BeEmpty();
    }

    [Test]
    public void ClassMemberKeysFollowTheSameRule()
    {
        Collect("class E { m() {} [f]() {} static [g] = h; p = i; }")
            .Should().BeEquivalentTo(["f", "g", "h", "i"]);
    }

    [Test]
    public void DestructuringDeclarationTargetsAreDeclarations()
    {
        Collect("var { p, [q]: r, s = t } = o;").Should().BeEquivalentTo(["o", "q", "t"]);
    }

    [Test]
    public void DestructuringAssignmentTargetsAreReferences()
    {
        Collect("({ p, [q]: r } = o); [s] = arr;").Should().BeEquivalentTo(["arr", "o", "p", "q", "r", "s"]);
    }

    [Test]
    public void ArrayPatternDeclarationTargetsAreDeclarations()
    {
        Collect("var [head, ...tail] = list; head; tail;").Should().BeEquivalentTo(["list"]);
    }

    [Test]
    public void LabelsAreNotReferences()
    {
        Collect("outer: for (;;) { break outer; } inner: while (c) { continue inner; }")
            .Should().BeEquivalentTo(["c"]);
    }

    [Test]
    public void TemplateLiteralTagsAndInterpolationsAreReferences()
    {
        Collect("tag`x${y}z`; `${a}${b.c}`;").Should().BeEquivalentTo(["a", "b", "tag", "y"]);
    }

    [Test]
    public void CalleesArgumentsAndSpreadAreReferences()
    {
        Collect("fn(a, ...rest); new Ctor(b);").Should().BeEquivalentTo(["a", "b", "Ctor", "fn", "rest"]);
    }

    [Test]
    public void MetaPropertiesAreNotReferences()
    {
        Collect("function f() { return new.target; }").Should().BeEmpty();
    }

    [Test]
    public void ThisAndSuperAndPrivateNamesNeverAppear()
    {
        Collect("class C extends B { #p = 1; m() { return super.m() + this.#p; } }")
            .Should().BeEquivalentTo(["B"]);
    }

    // ---------------------------------------------------------------- arguments

    [Test]
    public void ArgumentsIsBoundInsideANonArrowFunction()
    {
        Collect("function f() { return arguments; }").Should().BeEmpty();
        Collect("var f = function () { return arguments; };").Should().BeEmpty();
        Collect("var o = { m() { return arguments; } };").Should().BeEmpty();
    }

    [Test]
    public void ArgumentsIsFreeAtTopLevelAndInsideTopLevelArrows()
    {
        Collect("arguments;").Should().BeEquivalentTo(["arguments"]);
        Collect("(() => arguments);").Should().BeEquivalentTo(["arguments"]);
    }

    [Test]
    public void ArgumentsInsideAnArrowNestedInAFunctionIsBound()
    {
        Collect("function f() { return () => arguments; }").Should().BeEmpty();
    }

    // ---------------------------------------------------------------- with

    [Test]
    public void WithStatementOverApproximates()
    {
        // `x` may resolve on `o` at run time, but it is lexically free and is reported
        Collect("with (o) { x; }").Should().BeEquivalentTo(["o", "x"]);
    }

    // ---------------------------------------------------------------- eval

    [Test]
    public void DirectEvalIsFlagged()
    {
        var collected = Collect("eval('1 + 1');");
        collected.HasDirectEvalCall.Should().BeTrue();
        collected.Should().BeEquivalentTo(["eval"]);
    }

    [Test]
    public void OptionalDirectEvalCallIsFlagged()
    {
        Collect("eval?.('1 + 1');").HasDirectEvalCall.Should().BeTrue();
    }

    [Test]
    public void IndirectEvalIsNotFlaggedButAppearsInTheSet()
    {
        var collected = Collect("(0, eval)('1 + 1');");
        collected.HasDirectEvalCall.Should().BeFalse();
        collected.Should().BeEquivalentTo(["eval"]);
    }

    [Test]
    public void FunctionConstructorIsNotFlaggedButAppearsInTheSet()
    {
        var collected = Collect("new Function('return 1;')();");
        collected.HasDirectEvalCall.Should().BeFalse();
        collected.Should().BeEquivalentTo(["Function"]);
    }

    [Test]
    public void ProgramWithoutEvalIsNotFlagged()
    {
        Collect("evaluate(); o.eval();").HasDirectEvalCall.Should().BeFalse();
    }

    // ---------------------------------------------------------------- Annex B

    [Test]
    public void AnnexBBlockFunctionFollowsTheParserScopeModel()
    {
        // sloppy-mode Annex B also creates a global `var fb`, so this over-approximates, which is the safe direction
        Collect("{ function fb() {} } fb();").Should().BeEquivalentTo(["fb"]);
        Collect("'use strict'; { function fb() {} } fb();", strict: true).Should().BeEquivalentTo(["fb"]);
    }

    // ---------------------------------------------------------------- modules

    [Test]
    public void ImportBindingsAreDeclarationsAndImportedNamesAreNotReferences()
    {
        CollectModule("import d, { a as b } from 'm'; d; b; g;").Should().BeEquivalentTo(["g"]);
    }

    [Test]
    public void NamespaceImportIsADeclaration()
    {
        CollectModule("import * as ns from 'm'; ns.thing;").Should().BeEmpty();
    }

    [Test]
    public void ExportedAliasesAreNotReferencesButLocalsAre()
    {
        // `export { b as c }` requires `b` to be declared, so the local always resolves; what matters is that the
        // alias `c` is never mistaken for a reference
        CollectModule("const b = compute(); export { b as c };").Should().BeEquivalentTo(["compute"]);
        CollectModule("export * as ns from 'n';").Should().BeEmpty();
        CollectModule("export { a as b } from 'm';").Should().BeEmpty();
    }

    [Test]
    public void ExportedDeclarationsBindTheirNames()
    {
        CollectModule("export const value = compute(); value;").Should().BeEquivalentTo(["compute"]);
        CollectModule("export default function local() { return local; } local;").Should().BeEmpty();
    }

    [Test]
    public void DynamicImportArgumentIsAReference()
    {
        CollectModule("import(specifier);").Should().BeEquivalentTo(["specifier"]);
    }

    [Test]
    public void ImportMetaIsNotAReference()
    {
        CollectModule("var u = import.meta.url;").Should().BeEmpty();
    }

    // ---------------------------------------------------------------- collection semantics

    [Test]
    public void NamesAreOrdinalSortedAndDeduplicated()
    {
        var collected = Collect("b; a; B; a; A; b;");

        collected.Should().Equal("A", "B", "a", "b");
        collected.Count.Should().Be(4);
    }

    [Test]
    public void ContainsIsOrdinal()
    {
        var collected = Collect("uuid();");

        collected.Contains("uuid").Should().BeTrue();
        collected.Contains("UUID").Should().BeFalse();
        collected.Contains("nope").Should().BeFalse();
        Invoking(() => collected.Contains(null!)).Should().Throw<ArgumentNullException>();
    }

    [Test]
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

    [Test]
    public void SameSourceProducesTheSameSet()
    {
        const string Code = "variables.Foo + libX.y + (function (p) { return p + q; })(r);";

        Collect(Code).Should().Equal(Collect(Code));
    }

    // ---------------------------------------------------------------- sharing

    [Test]
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
    [Test]
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

    [Test]
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
