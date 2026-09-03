#nullable enable

using System.Linq;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// The names of the global lexical bindings — the <c>let</c>, <c>const</c> and <c>class</c> declarations a
/// top-level script made, which the specification keeps in the global environment record rather than on the
/// global object.
/// </summary>
/// <remarks>
/// A host reading <c>globalThis</c> sees the <c>var</c>s and the function declarations and none of these, so
/// a completion list built from the global object alone is missing exactly the half a modern script declares.
/// </remarks>
public class HostGlobalLexicalNamesTests
{
    [Test]
    public void TheLetConstAndClassDeclarationsOfATopLevelScriptAreNamed()
    {
        var engine = new Engine();
        engine.Execute("let a; const b = 1; class C {}");

        Assert.That(engine.Advanced.GetGlobalLexicalNames(), Is.EqualTo(new[] { "a", "b", "C" }));
    }

    [Test]
    public void NamesComeBackInDeclarationOrder()
    {
        var engine = new Engine();
        engine.Execute("const zulu = 1; let alpha = 2; class Mike {}");

        Assert.That(engine.Advanced.GetGlobalLexicalNames(), Is.EqualTo(new[] { "zulu", "alpha", "Mike" }));
    }

    [Test]
    public void AFreshEngineHasNone()
    {
        Assert.That(new Engine().Advanced.GetGlobalLexicalNames(), Is.Empty);
    }

    [Test]
    public void VarsAndFunctionDeclarationsAreGlobalObjectPropertiesAndNotNamedHere()
    {
        var engine = new Engine();
        engine.Execute("var v = 1; function f() {} let l = 2;");

        var names = engine.Advanced.GetGlobalLexicalNames();

        Assert.That(names, Is.EqualTo(new[] { "l" }));
        Assert.That(engine.Evaluate("Object.prototype.hasOwnProperty.call(globalThis, 'v')").AsBoolean(), Is.True);
        Assert.That(engine.Evaluate("Object.prototype.hasOwnProperty.call(globalThis, 'f')").AsBoolean(), Is.True);
        Assert.That(engine.Evaluate("Object.prototype.hasOwnProperty.call(globalThis, 'l')").AsBoolean(), Is.False);
    }

    [Test]
    public void ASetValueIsAGlobalPropertyAndNotALexicalBinding()
    {
        var engine = new Engine();
        engine.SetValue("host", 1);

        Assert.That(engine.Advanced.GetGlobalLexicalNames(), Is.Empty);
    }

    [Test]
    public void ABindingDeclaredAcrossTwoScriptsIsNamedOnce()
    {
        var engine = new Engine();
        engine.Execute("let first = 1;");
        engine.Execute("const second = 2;");

        Assert.That(engine.Advanced.GetGlobalLexicalNames(), Is.EqualTo(new[] { "first", "second" }));
    }

    [Test]
    public void ALexicalDeclarationInsideAFunctionOrABlockIsNotGlobal()
    {
        var engine = new Engine();
        engine.Execute("{ let scoped = 1; } function f() { let inner = 2; return inner; } f(); let top = 3;");

        Assert.That(engine.Advanced.GetGlobalLexicalNames(), Is.EqualTo(new[] { "top" }));
    }

    [Test]
    public void TheAnswerIsAReadOnlySnapshotThatALaterDeclarationDoesNotChange()
    {
        var engine = new Engine();
        engine.Execute("let one = 1;");

        var before = engine.Advanced.GetGlobalLexicalNames();
        engine.Execute("let two = 2;");

        Assert.That(before.ToArray(), Is.EqualTo(new[] { "one" }));
        Assert.That(engine.Advanced.GetGlobalLexicalNames(), Is.EqualTo(new[] { "one", "two" }));
    }
}
