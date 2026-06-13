using Jint.Runtime;
using Jint.Runtime.Interop;
using Jint.Tests.Runtime.Domain;
using Jint.Tests.Runtime.ExtensionMethods;

namespace Jint.Tests.Runtime;

public partial class InteropTests
{
    private static Engine DetailedErrorsEngine() =>
        new Engine(options => options.Interop.ExposeDetailedResolutionErrors = true);

    [Fact]
    public void FailedMethodResolutionReportsTargetTypeArgumentsAndCandidates()
    {
        var engine = DetailedErrorsEngine();
        engine.SetValue("speaker", new Speaker());

        var ex = Assert.Throws<JavaScriptException>(() => engine.Evaluate("speaker.Say('Hello', 'World')"));

        Assert.Equal(
            "No public methods with the specified arguments were found. Target: Jint.Tests.Runtime.Speaker.Say; provided arguments: (string, string); candidate signatures: Say(String message)",
            ex.Message);
    }

    [Fact]
    public void FailedMethodResolutionDescribesArgumentKinds()
    {
        var engine = DetailedErrorsEngine();
        engine.SetValue("speaker", new Speaker());
        engine.SetValue("other", new Speaker());

        var ex = Assert.Throws<JavaScriptException>(() => engine.Evaluate(
            "speaker.Say(null, undefined, [1, 2, 3], x => x, {}, new Date(), other)"));

        Assert.Contains("(null, undefined, Array, function, object, Date, Speaker)", ex.Message);
    }

    [Fact]
    public void FailedMethodResolutionCapsReportedCandidates()
    {
        var engine = DetailedErrorsEngine();
        engine.SetValue("picker", new OverloadPicker());

        var ex = Assert.Throws<JavaScriptException>(() => engine.Evaluate("picker.Pick()"));

        Assert.StartsWith("No public methods with the specified arguments were found.", ex.Message);
        Assert.Contains("Pick(Int32 a)", ex.Message);
        Assert.Contains(" and 2 more", ex.Message);
    }

    [Fact]
    public void FailedExtensionMethodResolutionReportsDeclaringTypeAndReceiver()
    {
        var options = new Options();
        options.Interop.ExposeDetailedResolutionErrors = true;
        options.AddExtensionMethods(typeof(PersonExtensions));

        var engine = new Engine(options);
        engine.SetValue("person", new Person());

        var ex = Assert.Throws<JavaScriptException>(() => engine.Evaluate("person.MultiplyAge()"));

        Assert.StartsWith("No public methods with the specified arguments were found.", ex.Message);
        Assert.Contains("PersonExtensions.MultiplyAge(this Person person, Int32 factor)", ex.Message);
    }

    [Fact]
    public void FailedConstructorResolutionReportsArgumentsAndCandidates()
    {
        var engine = DetailedErrorsEngine();
        engine.SetValue("CtorFails", TypeReference.CreateTypeReference<CtorFails>(engine));

        var ex = Assert.Throws<JavaScriptException>(() => engine.Evaluate("new CtorFails(1, 2)"));

        Assert.StartsWith("Could not resolve a constructor for type Jint.Tests.Runtime.CtorFails for given arguments", ex.Message);
        Assert.Contains("provided arguments: (number, number)", ex.Message);
        Assert.Contains("candidate signatures: CtorFails(String name)", ex.Message);
    }

    [Fact]
    public void DefaultMethodResolutionErrorIsTerseAndHidesClrDetail()
    {
        var engine = new Engine();
        engine.SetValue("speaker", new Speaker());

        var ex = Assert.Throws<JavaScriptException>(() => engine.Evaluate("speaker.Say('Hello', 'World')"));

        // the script-visible message must not leak the CLR type, member, argument types or signatures
        Assert.Equal("No public methods with the specified arguments were found.", ex.Message);
    }

    [Fact]
    public void DefaultConstructorResolutionErrorIsTerse()
    {
        var engine = new Engine();
        engine.SetValue("CtorFails", TypeReference.CreateTypeReference<CtorFails>(engine));

        var ex = Assert.Throws<JavaScriptException>(() => engine.Evaluate("new CtorFails(1, 2)"));

        // historical terse text (names the type, as it always has) but none of the added detail
        Assert.Equal("Could not resolve a constructor for type Jint.Tests.Runtime.CtorFails for given arguments", ex.Message);
        Assert.DoesNotContain("provided arguments", ex.Message);
        Assert.DoesNotContain("candidate signatures", ex.Message);
    }

    [Fact]
    public void ClrTypeIsAvailableHostSideUnderSafeDefault()
    {
        // no options set: detailed messages are off, but the host can still recover the CLR type
        var engine = new Engine();
        engine.SetValue("speaker", new Speaker());

        var ex = Assert.Throws<JavaScriptException>(() => engine.Evaluate("speaker.Say('Hello', 'World')"));

        Assert.True(JintException.TryGetClrType(ex, out var type));
        Assert.Equal(typeof(Speaker), type);

        Assert.True(JintException.TryGetClrMemberName(ex, out var member));
        Assert.Equal("Say", member);
    }

    [Fact]
    public void ClrTypeIsAvailableForConstructorFailures()
    {
        var engine = new Engine();
        engine.SetValue("CtorFails", TypeReference.CreateTypeReference<CtorFails>(engine));

        var ex = Assert.Throws<JavaScriptException>(() => engine.Evaluate("new CtorFails(1, 2)"));

        Assert.True(JintException.TryGetClrType(ex, out var type));
        Assert.Equal(typeof(CtorFails), type);

        // constructors carry no member name
        Assert.False(JintException.TryGetClrMemberName(ex, out _));
    }
}

public class Speaker
{
    public string Say(string message) => $"Speaker says: {message}";
}

public class OverloadPicker
{
    public void Pick(int a) { }
    public void Pick(int a, int b) { }
    public void Pick(int a, int b, int c) { }
    public void Pick(int a, int b, int c, int d) { }
    public void Pick(int a, int b, int c, int d, int e) { }
    public void Pick(int a, int b, int c, int d, int e, int f) { }
    public void Pick(int a, int b, int c, int d, int e, int f, int g) { }
}

public class CtorFails
{
    public CtorFails(string name)
    {
    }
}
