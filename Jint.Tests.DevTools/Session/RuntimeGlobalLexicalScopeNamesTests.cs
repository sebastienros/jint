using Jint.DevTools;

namespace Jint.Tests.DevTools.Session;

/// <summary>
/// <c>Runtime.globalLexicalScopeNames</c>: the global <c>let</c>, <c>const</c> and <c>class</c> declarations,
/// which is what the front end's console completion list is built from.
/// </summary>
/// <remarks>
/// They live in the global environment record rather than on the global object, so nothing a client can
/// enumerate through <c>Runtime.getProperties</c> over <c>globalThis</c> would have found them.
/// </remarks>
public class RuntimeGlobalLexicalScopeNamesTests
{
    [Test]
    public async Task TheLetConstAndClassDeclarationsAreNamedInDeclarationOrder()
    {
        await using var session = await AttachedSession.CreateAsync(
            engine => engine.Execute("let a; const b = 1; class C {}"));

        var result = await session.ResultAsync("Runtime.globalLexicalScopeNames");

        var names = result.GetProperty("names").EnumerateArray().Select(n => n.GetString()).ToArray();
        names.Should().Equal("a", "b", "C");
    }

    [Test]
    public async Task AnEngineThatDeclaredNoneAnswersAnEmptyList()
    {
        await using var session = await AttachedSession.CreateAsync();

        var result = await session.ResultAsync("Runtime.globalLexicalScopeNames");

        result.GetProperty("names").GetArrayLength().Should().Be(0);
    }

    [Test]
    public async Task VarsAndFunctionDeclarationsAreGlobalPropertiesAndAreNotNamedHere()
    {
        await using var session = await AttachedSession.CreateAsync(
            engine => engine.Execute("var v = 1; function f() {} let l = 2;"));

        var result = await session.ResultAsync("Runtime.globalLexicalScopeNames");

        var names = result.GetProperty("names").EnumerateArray().Select(n => n.GetString()).ToArray();
        names.Should().Equal("l");

        var seesVar = await session.EvaluateAsync("Object.prototype.hasOwnProperty.call(globalThis, 'v')");
        seesVar.GetProperty("value").GetBoolean().Should().BeTrue();
    }

    [Test]
    public async Task AnExecutionContextThatIsNotTheEnginesIsRefused()
    {
        await using var session = await AttachedSession.CreateAsync();

        var error = await session.ErrorAsync(
            "Runtime.globalLexicalScopeNames",
            """{"executionContextId":77}""");

        error.GetProperty("code").GetInt32().Should().Be(-32000);
    }
}
