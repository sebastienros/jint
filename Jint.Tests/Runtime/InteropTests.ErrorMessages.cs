using System.Reflection;
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

        var ex = Invoking(() => engine.Evaluate("speaker.Say('Hello', 'World')")).Should().ThrowExactly<JavaScriptException>().Which;

        ex.Message.Should().Be("No public methods with the specified arguments were found. Target: Jint.Tests.Runtime.Speaker.Say; provided arguments: (string, string); candidate signatures: Say(String message)");
    }

    [Fact]
    public void FailedMethodResolutionDoesNotDuplicateImplicitlyImplementedInterfaceMethods()
    {
        var engine = DetailedErrorsEngine();
        engine.SetValue("holder", new GreeterHolder(new Greeter()));

        // Greeter.Greet is collected both from the class and from IGreeter, it must be reported once
        var ex = Invoking(() => engine.Evaluate("holder.Greeter.Greet()")).Should().ThrowExactly<JavaScriptException>().Which;

        ex.Message.Should().Be("No public methods with the specified arguments were found. Target: Jint.Tests.Runtime.Greeter.Greet; provided arguments: (); candidate signatures: Greet(String name)");
    }

    [Fact]
    public void FailedMethodResolutionDescribesArgumentKinds()
    {
        var engine = DetailedErrorsEngine();
        engine.SetValue("speaker", new Speaker());
        engine.SetValue("other", new Speaker());

        var ex = Invoking(() => engine.Evaluate(
            "speaker.Say(null, undefined, [1, 2, 3], x => x, {}, new Date(), other)")).Should().ThrowExactly<JavaScriptException>().Which;

        ex.Message.Should().Contain("(null, undefined, Array, function, object, Date, Speaker)");
    }

    [Fact]
    public void FailedMethodResolutionCapsReportedCandidates()
    {
        var engine = DetailedErrorsEngine();
        engine.SetValue("picker", new OverloadPicker());

        var ex = Invoking(() => engine.Evaluate("picker.Pick()")).Should().ThrowExactly<JavaScriptException>().Which;

        ex.Message.Should().StartWith("No public methods with the specified arguments were found.");
        ex.Message.Should().Contain("Pick(Int32 a)");
        ex.Message.Should().Contain(" and 2 more");
    }

    [Fact]
    public void FailedExtensionMethodResolutionReportsDeclaringTypeAndReceiver()
    {
        var options = new Options();
        options.Interop.ExposeDetailedResolutionErrors = true;
        options.AddExtensionMethods(typeof(PersonExtensions));

        var engine = new Engine(options);
        engine.SetValue("person", new Person());

        var ex = Invoking(() => engine.Evaluate("person.MultiplyAge()")).Should().ThrowExactly<JavaScriptException>().Which;

        ex.Message.Should().StartWith("No public methods with the specified arguments were found.");
        ex.Message.Should().Contain("PersonExtensions.MultiplyAge(this Person person, Int32 factor)");
    }

    [Fact]
    public void FailedConstructorResolutionReportsArgumentsAndCandidates()
    {
        var engine = DetailedErrorsEngine();
        engine.SetValue("CtorFails", TypeReference.CreateTypeReference<CtorFails>(engine));

        var ex = Invoking(() => engine.Evaluate("new CtorFails(1, 2)")).Should().ThrowExactly<JavaScriptException>().Which;

        ex.Message.Should().StartWith("Could not resolve a constructor for type Jint.Tests.Runtime.CtorFails for given arguments");
        ex.Message.Should().Contain("provided arguments: (number, number)");
        ex.Message.Should().Contain("candidate signatures: CtorFails(String name)");
    }

    [Fact]
    public void DefaultMethodResolutionErrorIsTerseAndHidesClrDetail()
    {
        var engine = new Engine();
        engine.SetValue("speaker", new Speaker());

        var ex = Invoking(() => engine.Evaluate("speaker.Say('Hello', 'World')")).Should().ThrowExactly<JavaScriptException>().Which;

        // the script-visible message must not leak the CLR type, member, argument types or signatures
        ex.Message.Should().Be("No public methods with the specified arguments were found.");
    }

    [Fact]
    public void DefaultConstructorResolutionErrorIsTerse()
    {
        var engine = new Engine();
        engine.SetValue("CtorFails", TypeReference.CreateTypeReference<CtorFails>(engine));

        var ex = Invoking(() => engine.Evaluate("new CtorFails(1, 2)")).Should().ThrowExactly<JavaScriptException>().Which;

        // historical terse text (names the type, as it always has) but none of the added detail
        ex.Message.Should().Be("Could not resolve a constructor for type Jint.Tests.Runtime.CtorFails for given arguments");
        ex.Message.Should().NotContain("provided arguments");
        ex.Message.Should().NotContain("candidate signatures");
    }

    [Fact]
    public void ClrTypeIsAvailableHostSideUnderSafeDefault()
    {
        // no options set: detailed messages are off, but the host can still recover the CLR type
        var engine = new Engine();
        engine.SetValue("speaker", new Speaker());

        var ex = Invoking(() => engine.Evaluate("speaker.Say('Hello', 'World')")).Should().ThrowExactly<JavaScriptException>().Which;

        JintException.TryGetClrType(ex, out var type).Should().BeTrue();
        type.Should().Be(typeof(Speaker));

        JintException.TryGetClrMemberName(ex, out var member).Should().BeTrue();
        member.Should().Be("Say");
    }

    [Fact]
    public void ClrTypeIsAvailableForConstructorFailures()
    {
        var engine = new Engine();
        engine.SetValue("CtorFails", TypeReference.CreateTypeReference<CtorFails>(engine));

        var ex = Invoking(() => engine.Evaluate("new CtorFails(1, 2)")).Should().ThrowExactly<JavaScriptException>().Which;

        JintException.TryGetClrType(ex, out var type).Should().BeTrue();
        type.Should().Be(typeof(CtorFails));

        // constructors carry no member name
        JintException.TryGetClrMemberName(ex, out _).Should().BeFalse();
    }

    [Fact]
    public void ResolutionErrorDecoratorCanRewriteScriptVisibleMessage()
    {
        var engine = new Engine(options =>
        {
            options.Interop.ExposeDetailedResolutionErrors = true;
            options.DecorateClrResolutionErrors(static (_, error, info) =>
                error.Set("message", $"{info.Type.Name}.{info.MemberName}: no matching overload"));
        });
        engine.SetValue("speaker", new Speaker());

        // a script-side try/catch sees the rewritten message, not the detailed one with the fully-qualified type
        var scriptVisible = engine.Evaluate("try { speaker.Say('Hello', 'World'); } catch (e) { e.message }");
        scriptVisible.AsString().Should().Be("Speaker.Say: no matching overload");

        // and the host-side exception message matches
        var ex = Invoking(() => engine.Evaluate("speaker.Say('Hello', 'World')")).Should().ThrowExactly<JavaScriptException>().Which;
        ex.Message.Should().Be("Speaker.Say: no matching overload");
    }

    [Fact]
    public void ResolutionErrorDecoratorCanAddErrorCodeProperty()
    {
        var engine = new Engine(options => options.DecorateClrResolutionErrors(
            static (_, error, _) => error.Set("errorCode", "USR-001")));
        engine.SetValue("speaker", new Speaker());

        var errorCode = engine.Evaluate("try { speaker.Say('Hello', 'World'); } catch (e) { e.errorCode }");
        errorCode.AsString().Should().Be("USR-001");

        // without a decorator the property does not exist
        var undecorated = new Engine();
        undecorated.SetValue("speaker", new Speaker());
        var missing = undecorated.Evaluate("try { speaker.Say('Hello', 'World'); } catch (e) { e.errorCode === undefined }");
        missing.AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void ResolutionErrorDecoratorReceivesStructuredInfoForMethods()
    {
        // detailed messages stay off: the decorator still receives the full structured information
        ClrResolutionErrorInfo capturedInfo = null;
        var engine = new Engine(options => options.Interop.ClrResolutionErrorDecorator = (_, _, info) => capturedInfo = info);
        engine.SetValue("speaker", new Speaker());

        var ex = Invoking(() => engine.Evaluate("speaker.Say('Hello', 'World')")).Should().ThrowExactly<JavaScriptException>().Which;

        ex.Message.Should().Be("No public methods with the specified arguments were found.");

        capturedInfo.Should().NotBeNull();
        capturedInfo.Type.Should().Be(typeof(Speaker));
        capturedInfo.MemberName.Should().Be("Say");
        capturedInfo.IsConstructor.Should().BeFalse();
        capturedInfo.Arguments.Should().HaveCount(2);
        capturedInfo.Arguments[0].AsString().Should().Be("Hello");
        capturedInfo.Arguments[1].AsString().Should().Be("World");
        var candidate = capturedInfo.Candidates.Should().ContainSingle().Which;
        candidate.Name.Should().Be("Say");
        capturedInfo.CandidateSignatures.Should().ContainSingle().Which.Should().Be("Say(String message)");
        capturedInfo.DetailedMessage.Should().Be("No public methods with the specified arguments were found. Target: Jint.Tests.Runtime.Speaker.Say; provided arguments: (string, string); candidate signatures: Say(String message)");
    }

    [Fact]
    public void ResolutionErrorDecoratorReceivesStructuredInfoForConstructors()
    {
        ClrResolutionErrorInfo capturedInfo = null;
        var engine = new Engine(options => options.DecorateClrResolutionErrors((_, _, info) => capturedInfo = info));
        engine.SetValue("CtorFails", TypeReference.CreateTypeReference<CtorFails>(engine));

        Invoking(() => engine.Evaluate("new CtorFails(1, 2)")).Should().ThrowExactly<JavaScriptException>();

        capturedInfo.Should().NotBeNull();
        capturedInfo.Type.Should().Be(typeof(CtorFails));
        capturedInfo.MemberName.Should().BeNull();
        capturedInfo.IsConstructor.Should().BeTrue();
        capturedInfo.Arguments.Should().HaveCount(2);
        var candidate = capturedInfo.Candidates.Should().ContainSingle().Which;
        candidate.Should().BeAssignableTo<ConstructorInfo>();
        capturedInfo.CandidateSignatures.Should().ContainSingle().Which.Should().Be("CtorFails(String name)");
    }

    [Fact]
    public void ResolutionErrorDecoratorIsNotCalledOnSuccessfulResolution()
    {
        var called = false;
        var engine = new Engine(options => options.DecorateClrResolutionErrors((_, _, _) => called = true));
        engine.SetValue("speaker", new Speaker());

        var result = engine.Evaluate("speaker.Say('Hello')");

        result.AsString().Should().Be("Speaker says: Hello");
        called.Should().BeFalse();
    }

    [Fact]
    public void ResolutionErrorDecoratorPreservesHostSideClrInfo()
    {
        var engine = new Engine(options => options.DecorateClrResolutionErrors(
            static (_, error, _) => error.Set("message", "rewritten")));
        engine.SetValue("speaker", new Speaker());

        var ex = Invoking(() => engine.Evaluate("speaker.Say('Hello', 'World')")).Should().ThrowExactly<JavaScriptException>().Which;

        ex.Message.Should().Be("rewritten");
        JintException.TryGetClrType(ex, out var type).Should().BeTrue();
        type.Should().Be(typeof(Speaker));
        JintException.TryGetClrMemberName(ex, out var member).Should().BeTrue();
        member.Should().Be("Say");
    }

    [Fact]
    public void ResolutionErrorDecoratorArgumentsSurviveBeyondCallback()
    {
        ClrResolutionErrorInfo capturedInfo = null;
        var engine = new Engine(options => options.DecorateClrResolutionErrors((_, _, info) => capturedInfo = info));
        engine.SetValue("speaker", new Speaker());

        Invoking(() => engine.Evaluate("speaker.Say('Hello', 'World')")).Should().ThrowExactly<JavaScriptException>();
        // churn the engine's pooled/cached argument arrays; the captured copy must be unaffected
        engine.Evaluate("for (var i = 0; i < 100; i++) { Math.max(i, i + 1, i + 2); }");

        capturedInfo.Arguments[0].AsString().Should().Be("Hello");
        capturedInfo.Arguments[1].AsString().Should().Be("World");
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

public interface IGreeter
{
    string Greet(string name);
}

public class Greeter : IGreeter
{
    public string Greet(string name) => $"Hello, {name}";
}

public class GreeterHolder
{
    private readonly IGreeter _greeter;

    public GreeterHolder(IGreeter greeter)
    {
        _greeter = greeter;
    }

    // object-typed on purpose: the wrapper resolves members against the runtime type
    public object Greeter => _greeter;
}
