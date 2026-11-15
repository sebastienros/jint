#nullable enable

using AwesomeAssertions.Execution;
using Jint.Runtime;

namespace Jint.Tests.Runtime;

/// <summary>
/// A module whose only <c>await</c> sits in a class's heritage, in a computed key of one of its elements or in one of
/// the class's own decorators is an asynchronous module.
/// <see href="https://tc39.es/ecma262/#sec-parsemodule">ParseModule</see> sets [[HasTLA]] to <c>body Contains await</c>,
/// and <see href="https://tc39.es/ecma262/#sec-static-semantics-contains">Contains</see> for a ClassTail looks into the
/// ClassHeritage and, through ComputedPropertyContains, into every computed ClassElementName, but into no method body,
/// field initializer or static block. A class's own decorators sit outside its ClassTail.
/// <para>
/// Jint skipped classes whole, so such a module was evaluated synchronously and its await settled by a blocking
/// unwrap (sebastienros/jint#4395, and sebastienros/jint#4399 for a decorator). Each log is the module's own entries
/// interleaved with a ladder of microtasks <c>t1</c>, <c>t2</c>, … the module starts; every expected log is Node 26's,
/// except the decorator rows, which Node cannot run: theirs is the heritage row's, an await of a function that is not
/// a thenable at the same point of the module.
/// </para>
/// </summary>
public class ModuleClassAwaitTests
{
    private const string Setup = """
        globalThis.log = [];
        globalThis.ladder = function (n) {
          let p = Promise.resolve();
          for (let i = 1; i <= n; i++) {
            const k = i;
            p = p.then(() => { log.push('t' + k); });
          }
          return p;
        };
        globalThis.Base = class Base { m() { return 'base'; } };
        """;

    /// <summary>
    /// A module body standing in for <c>m</c>, imported after <c>setup</c> by a root that logs <c>root</c>.
    /// </summary>
    private static readonly Dictionary<string, string> Bodies = new()
    {
        // The reference: a module whose only await is an await expression, at the end of its body.
        ["AnAwaitExpression"] =
            "log.push('m body'); ladder(8); log.push('m end'); await undefined;",
        ["AHeritage"] =
            "log.push('m body'); ladder(8); class C extends (log.push('m heritage'), await Object) {} log.push('m end ' + (Object.getPrototypeOf(C) === Object));",
        ["AHeritageOfAPromise"] =
            "log.push('m body'); ladder(8); class C extends (await Promise.resolve(Object)) {} log.push('m end ' + (Object.getPrototypeOf(C) === Object));",
        ["AClassExpressionHeritage"] =
            "log.push('m body'); ladder(8); const C = class extends (await Object) {}; log.push('m end ' + C.name);",
        ["AComputedMethodKey"] =
            "log.push('m body'); ladder(8); class C { [(log.push('m key'), await 'k')]() { return 1; } } log.push('m end ' + new C().k());",
        ["AStaticComputedFieldKey"] =
            "log.push('m body'); ladder(8); class C { static [await 'k'] = 2; } log.push('m end ' + C.k);",
        ["ANestedClassHeritage"] =
            "log.push('m body'); ladder(8); class C extends class extends (await Object) {} {} log.push('m end');",
        ["ANestedComputedKey"] =
            "log.push('m body'); ladder(8); class C { [class { static [await 'k'] = 'n'; }.k]() {} } log.push('m end ' + Object.getOwnPropertyNames(C.prototype));",
        // A class's decorators sit outside its ClassTail, so Contains reaches them as any other child (#4399).
        ["AClassDecorator"] =
            "log.push('m body'); ladder(8); @(log.push('m decorator'), await (c => c)) class C {} log.push('m end ' + typeof C);",
        ["AClassExpressionDecorator"] =
            "log.push('m body'); ladder(8); const C = @(await (c => c)) class {}; log.push('m end ' + C.name);",
        // None of these is an await of the module's: each belongs to a function of its own.
        ["AnAwaitInAMethod"] =
            "log.push('m body'); ladder(8); class C { async m() { await 0; } } log.push('m end');",
        ["AnAwaitInAFieldArrow"] =
            "log.push('m body'); ladder(8); class C { f = async () => await 0; static g = async () => await 0; } log.push('m end');",
        ["AnAwaitInAStaticBlockArrow"] =
            "log.push('m body'); ladder(8); class C { static { (async () => { await 0; })(); } } log.push('m end');",
        ["AnAwaitInAHeritageArrow"] =
            "log.push('m body'); ladder(8); class C extends (async () => { await 0; }, Object) {} log.push('m end');",
        ["AnAwaitInAKeyArrow"] =
            "log.push('m body'); ladder(8); class C { [(async () => { await 0; }, 'k')]() {} } log.push('m end');",
    };

    public static TestCases<string, string> Forms => new()
    {
        { "AnAwaitExpression", "m body, m end, t1, t2, root, t3, t4, t5, t6, t7, t8" },
        { "AHeritage", "m body, m heritage, t1, m end true, t2, root, t3, t4, t5, t6, t7, t8" },
        { "AHeritageOfAPromise", "m body, t1, m end true, t2, root, t3, t4, t5, t6, t7, t8" },
        { "AClassExpressionHeritage", "m body, t1, m end C, t2, root, t3, t4, t5, t6, t7, t8" },
        { "AComputedMethodKey", "m body, m key, t1, m end 1, t2, root, t3, t4, t5, t6, t7, t8" },
        { "AStaticComputedFieldKey", "m body, t1, m end 2, t2, root, t3, t4, t5, t6, t7, t8" },
        { "ANestedClassHeritage", "m body, t1, m end, t2, root, t3, t4, t5, t6, t7, t8" },
        { "ANestedComputedKey", "m body, t1, m end constructor,n, t2, root, t3, t4, t5, t6, t7, t8" },
        { "AClassDecorator", "m body, m decorator, t1, m end function, t2, root, t3, t4, t5, t6, t7, t8" },
        { "AClassExpressionDecorator", "m body, t1, m end C, t2, root, t3, t4, t5, t6, t7, t8" },
        { "AnAwaitInAMethod", "m body, m end, root, t1, t2, t3, t4, t5, t6, t7, t8" },
        { "AnAwaitInAFieldArrow", "m body, m end, root, t1, t2, t3, t4, t5, t6, t7, t8" },
        { "AnAwaitInAStaticBlockArrow", "m body, m end, root, t1, t2, t3, t4, t5, t6, t7, t8" },
        { "AnAwaitInAHeritageArrow", "m body, m end, root, t1, t2, t3, t4, t5, t6, t7, t8" },
        { "AnAwaitInAKeyArrow", "m body, m end, root, t1, t2, t3, t4, t5, t6, t7, t8" },
    };

    [TestCaseSource(nameof(Forms)), CancelAfter(10000)]
    public void AnImporterRunsOnceTheModuleHasSettled(string form, string expected)
    {
        var engine = NewEngine();
        engine.Modules.Add("m", Bodies[form]);
        engine.Modules.Add("root", "import 'setup'; import 'm'; log.push('root');");

        engine.Modules.Import("root");

        Log(engine).Should().Be(expected, form);
    }

    /// <summary>
    /// The answer [[HasTLA]] is computed from: Contains for a ClassTail
    /// (https://tc39.es/ecma262/#sec-static-semantics-contains) searches the heritage and each computed key, nested
    /// classes included, and returns false for everything a function of its own evaluates.
    /// <para>
    /// The decorators proposal puts a class's own decorator list beside its ClassTail, a child of the ClassDeclaration
    /// or ClassExpression that the default rule of Contains searches, so an await there counts. Its rule for
    /// ComputedPropertyContains of a decorated element returns that of the MethodDefinition or FieldDefinition alone
    /// (tc39/ecma262#2417), so an await in an element's decorator does not (sebastienros/jint#4399).
    /// </para>
    /// </summary>
    [TestCase("class C extends (await x) {}", true)]
    [TestCase("class C extends f(await x) {}", true)]
    [TestCase("(class extends (await x) {});", true)]
    [TestCase("export default class extends (await x) {}", true)]
    [TestCase("export class C extends (await x) {}", true)]
    [TestCase("class C { [await k]() {} }", true)]
    [TestCase("class C { static [await k]() {} }", true)]
    [TestCase("class C { get [await k]() {} }", true)]
    [TestCase("class C { *[await k]() {} }", true)]
    [TestCase("class C { async [await k]() {} }", true)]
    [TestCase("class C { [await k] = 1; }", true)]
    [TestCase("class C { static [await k]; }", true)]
    [TestCase("class C { accessor [await k] = 1; }", true)]
    [TestCase("class C extends class extends (await x) {} {}", true)]
    [TestCase("class C { [class { [await k]() {} }.name]() {} }", true)]
    [TestCase("class C extends (class { static [await k] = 1; }) {}", true)]
    [TestCase("@(await d) class C {}", true)]
    [TestCase("@a @(await d) class C {}", true)]
    [TestCase("(@(await d) class {});", true)]
    [TestCase("export default @(await d) class {}", true)]
    [TestCase("class C extends (@(await d) class {}) {}", true)]
    [TestCase("class C { [(@(await d) class {}).name]() {} }", true)]
    [TestCase("class C {}", false)]
    [TestCase("class C extends B { [k]() {} static [k] = 1; }", false)]
    [TestCase("class C { async m() { await 0; } }", false)]
    [TestCase("class C { async [k]() { await 0; } }", false)]
    [TestCase("class C { f = async () => await 0; }", false)]
    [TestCase("class C { static f = async () => await 0; }", false)]
    [TestCase("class C { static { (async () => { await 0; })(); } }", false)]
    [TestCase("class C extends (async () => await 0, B) {}", false)]
    [TestCase("class C { [(async () => await 0, k)]() {} }", false)]
    [TestCase("class C extends (async function () { class D extends (await x) {} }, B) {}", false)]
    [TestCase("@(async () => await 0) class C {}", false)]
    [TestCase("class C { @(await d) m() {} }", false)]
    [TestCase("class C { @(await d) static m() {} }", false)]
    [TestCase("class C { @(await d) x = 1; }", false)]
    [TestCase("class C { @(await d) accessor x = 1; }", false)]
    public void AnAwaitInAClassHeritageOrComputedKeyIsATopLevelAwait(string source, bool expected)
    {
        var module = Engine.PrepareModule(source, "m");

        HoistingScope.HasTopLevelAwait(module.Program!).Should().Be(expected, source);
    }

    /// <summary>
    /// An exported class whose heritage awaits is bound once its evaluation resumes: the default export takes the
    /// name <c>default</c>, a closure created before the class reads the same binding the importer is handed, and
    /// both reach their importer initialized.
    /// </summary>
    [Test, CancelAfter(10000)]
    public void AnExportedClassWhoseHeritageAwaitsIsBoundOnceItResumes()
    {
        var engine = NewEngine();
        engine.Modules.Add("m", """
            const read = () => C;
            export default class extends (await Base) { static x = 'dx'; }
            export class C extends (await Base) { static y = 'cy'; }
            log.push('m read ' + (read() === C));
            """);
        engine.Modules.Add("root", """
            import 'setup';
            import D, { C } from 'm';
            log.push('root ' + D.name + ' ' + D.x + ' ' + (Object.getPrototypeOf(D) === Base) + ' ' + C.name + ' ' + C.y + ' ' + new C().m());
            """);

        engine.Modules.Import("root");

        Log(engine).Should().Be("m read true, root default dx true C cy base");
    }

    /// <summary>
    /// An await in the heritage or in a computed key that rejects is the module's evaluation error, raised once:
    /// nothing after the class runs, and neither does the importer.
    /// </summary>
    [TestCase("class C extends (await Promise.reject(new Error('no'))) {}", "no")]
    [TestCase("class C { [await Promise.reject(new Error('nokey'))]() {} }", "nokey")]
    [CancelAfter(10000)]
    public void ARejectedAwaitInAClassRejectsTheModule(string declaration, string expectedMessage)
    {
        var engine = NewEngine();
        engine.Modules.Add("m", "log.push('m body'); " + declaration + " log.push('m not here');");
        engine.Modules.Add("root", "import 'setup'; import 'm'; log.push('root not here');");

        string outcome;
        try
        {
            engine.Modules.Import("root");
            outcome = "resolved";
        }
        catch (JavaScriptException e)
        {
            outcome = "rejected " + e.Error.Get("message");
        }

        using var _ = new AssertionScope();
        outcome.Should().Be("rejected " + expectedMessage);
        Log(engine).Should().Be("m body");
    }

    private static Engine NewEngine()
    {
        var engine = new Engine(options => options.ObserveCancellation(TestContext.CurrentContext.CancellationToken));
        engine.Modules.Add("setup", Setup);
        return engine;
    }

    private static string Log(Engine engine)
    {
        // The ladder may still have steps queued once the import has settled; a script run drains them.
        engine.Evaluate("0");
        return engine.Evaluate("log.join(', ')").AsString();
    }
}
