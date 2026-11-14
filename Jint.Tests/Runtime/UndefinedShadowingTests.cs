#nullable enable

using Environment = Jint.Runtime.Environments.Environment;

namespace Jint.Tests.Runtime;

/// <summary>
/// <c>undefined</c> is an ordinary identifier: the global property is non-writable and non-configurable
/// (https://tc39.es/ecma262/#sec-undefined), but a parameter, a declaration, a catch parameter, a function or class
/// self-name, a <c>with</c> object or a sloppy <c>eval</c> can bind the name closer to the read, and the read then
/// answers that binding. Jint folded every read of the name to the global value, so
/// <c>function (undefined) { return undefined; }</c> returned <c>undefined</c> whatever it was passed
/// (sebastienros/jint#4579). Only the static analysis folds now, and only for a program in which nothing can shadow
/// the name. Every expected value is Node's answer to the same script.
/// </summary>
[Parallelizable(ParallelScope.All)]
public class UndefinedShadowingTests
{
    public enum Preparation
    {
        Source,
        Prepared,
        ParseOnly,
    }

    private static readonly Preparation[] Preparations = [Preparation.Source, Preparation.Prepared, Preparation.ParseOnly];

    public static TestCases<string, string> Cases => new()
    {
        { "(function () { var undefined = 7; return [typeof undefined, undefined].join(); })()", "number,7" },
        { "(function (undefined) { return undefined; })('s')", "s" },
        { "(function () { let undefined = 1; return undefined; })()", "1" },
        { "(function () { const undefined = 2; return undefined; })()", "2" },
        { "(function () { var r = String(undefined); var undefined = 3; return r + ',' + undefined; })()", "undefined,3" },
        { "(function () { try { throw 5; } catch (undefined) { return undefined; } })()", "5" },
        { "((undefined) => undefined)(4)", "4" },
        { "(function () { var { a: undefined } = { a: 6 }; return undefined; })()", "6" },
        { "(function () { function undefined() {} return typeof undefined + ':' + (undefined === void 0); })()", "function:false" },
        { "typeof (function undefined() { return undefined; })()", "function" },
        { "(function () { class undefined {} return undefined === void 0; })()", "false" },
        { "(function () { with ({ undefined: 9 }) { return undefined; } })()", "9" },
        { "(function () { eval('var undefined = 8'); return undefined; })()", "8" },
        { "(function (undefined) { return eval('undefined'); })(2)", "2" },
        { "new Function('undefined', 'return undefined')(3)", "3" },
        { "(function (undefined) { return [undefined == undefined, 1 === undefined, null == undefined, undefined === 1, undefined !== 1].join(); })(1)", "true,true,false,true,false" },
        { "(function (undefined) { var x; return [x === undefined, x == undefined].join(); })(0)", "false,false" },
        { "(function () { return [undefined === void 0, typeof undefined, String(undefined)].join(); })()", "true,undefined,undefined" },
    };

    [TestCaseSource(nameof(Cases))]
    public void ReadsTheClosestBinding(string script, string expected)
    {
        foreach (var preparation in Preparations)
        {
            Run(preparation, script).Should().Be(expected, preparation.ToString());
        }
    }

    [Test]
    public void AStrictProgramReadsAParameterNamedUndefined()
    {
        foreach (var preparation in Preparations)
        {
            Run(preparation, "'use strict'; (function (undefined) { return undefined; })('s')").Should().Be("s", preparation.ToString());
        }
    }

    [Test]
    public void AModuleReadsItsOwnBindingNamedUndefined()
    {
        var engine = new Engine();
        engine.Modules.Add("source", "let undefined = 5; export const r = undefined;");
        engine.Modules.Import("source").Get("r").AsNumber().Should().Be(5);

        var prepared = Engine.PrepareModule("var undefined = 6; export const r = undefined;");
        engine.Modules.Add("prepared", builder => builder.AddModule(prepared));
        engine.Modules.Import("prepared").Get("r").AsNumber().Should().Be(6);
    }

    /// <summary>
    /// The fold is the reason the analysis exists for this name, so a program that cannot shadow it must keep it.
    /// </summary>
    [Test]
    public void AProgramThatCannotShadowUndefinedStillFoldsIt()
    {
        Assert.That(FoldsUndefined("function f(a) { return a === undefined ? undefined : a; }"), Is.True);
        Assert.That(FoldsUndefined("function f(a) { return undefined; } function g(undefined) { return 1; }"), Is.False);
        Assert.That(FoldsUndefined("function f() { return undefined; } function g() { eval('1'); }"), Is.False);
    }

    private static bool FoldsUndefined(string script)
    {
        var program = Engine.PrepareScript(script).Program!;
        var names = program.DescendantNodes()
            .OfType<Identifier>()
            .Where(static identifier => identifier.Name == "undefined")
            .Select(static identifier => (Environment.BindingName) identifier.UserData!)
            .Distinct()
            .ToList();

        names.Should().ContainSingle();
        return names[0].CalculatedValue is not null;
    }

    private static string Run(Preparation preparation, string script)
    {
        var engine = new Engine();
        var result = preparation switch
        {
            Preparation.Source => engine.Evaluate(script),
            Preparation.Prepared => engine.Evaluate(Engine.PrepareScript(script)),
            _ => engine.Evaluate(Engine.PrepareScript(script, options: new ScriptPreparationOptions { StaticAnalysis = false })),
        };

        return result.ToString();
    }
}
