#nullable enable

namespace Jint.Tests.Runtime;

/// <summary>
/// A non-computed, non-shorthand <c>__proto__: value</c> in an object literal sets the new object's
/// <c>[[Prototype]]</c> instead of defining a property, whether its name is written as an identifier or as a string
/// literal (https://tc39.es/ecma262/#sec-runtime-semantics-propertydefinitionevaluation, the isProtoSetter step).
/// Jint recognised only the identifier spelling, so <c>{ "__proto__": p }</c> built an object with an own enumerable
/// <c>"__proto__"</c> property (sebastienros/jint#4579). Every expected string is Node's answer to the same script.
/// </summary>
[Parallelizable(ParallelScope.All)]
public class ObjectLiteralProtoSetterTests
{
    private const string Prelude = "var proto = { inh: 1 }; function report(o) { return [Object.getPrototypeOf(o) === proto, o.inh, Object.keys(o).join('|')].join(); }";

    public static TestCases<string, string> Cases => new()
    {
        { "report({ __proto__: proto })", "true,1," },
        { "report({ '__proto__': proto })", "true,1," },
        { "report({ \"__proto__\": proto, a: 2 })", "true,1,a" },
        { "report({ a: 2, '__proto__': proto })", "true,1,a" },
        { "Object.getPrototypeOf({ '__proto__': null }) === null", "true" },
        { "Object.getPrototypeOf({ '__proto__': 5 }) === Object.prototype", "true" },
        { "Object.keys({ ['__proto__']: proto }).join()", "__proto__" },
        { "var __proto__ = proto; Object.keys({ __proto__ }).join()", "__proto__" },
        { "Object.keys({ '__proto__'() {} }).join()", "__proto__" },
        { "Object.keys({ __proto__() {} }).join()", "__proto__" },
    };

    [TestCaseSource(nameof(Cases))]
    public void SetsThePrototype(string script, string expected)
    {
        new Engine().Execute(Prelude).Evaluate(script).ToString().Should().Be(expected);
    }

    [Test]
    public void ARepeatedLiteralKeepsSettingThePrototype()
    {
        var engine = new Engine();
        engine.Execute(Prelude + "function make() { return { 'a': 1, '__proto__': proto }; }");
        for (var i = 0; i < 3; i++)
        {
            engine.Evaluate("report(make())").ToString().Should().Be("true,1,a");
        }
    }
}
