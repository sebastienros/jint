using Jint.Native;
using Jint.Runtime;
using Jint.Tests.Runtime.Domain;

namespace Jint.Tests.Runtime.ExtensionMethods;

/// <summary>
/// A callback registered with <c>Options.Configure</c> runs from <c>Options.Apply</c>, which is also what
/// attaches extension methods to the prototypes - so a callback that registers a container type has to reach
/// both that attach and <c>TypeResolver</c>. https://github.com/sebastienros/jint/issues/3568
/// </summary>
public class ConfigureCallbackRegistrationTests
{
    [Test]
    public void ExtensionMethodsRegisteredFromConfigureCallbackReachThePrototypes()
    {
        var engine = new Engine(options =>
        {
            options.Configure(_ => options.AddExtensionMethods(typeof(CustomStringExtensions)));
        });

        engine.Evaluate("'Hello World!'.Backwards()").AsString().Should().Be("!dlroW olleH");
    }

    [Test]
    public void ExtensionMethodsRegisteredFromConfigureCallbackReachTheResolver()
    {
        var person = new Person { Name = "Mickey Mouse", Age = 35 };

        var engine = new Engine(options =>
        {
            options.Configure(_ => options.AddExtensionMethods(typeof(PersonExtensions)));
        });
        engine.SetValue("person", person);

        engine.Evaluate("person.MultiplyAge(2)").AsInteger().Should().Be(70);
    }

    [Test]
    public void ExtensionMethodsRegisteredBeforeAndFromAConfigureCallbackAreBothHonoured()
    {
        var person = new Person { Name = "Mickey Mouse", Age = 35 };

        var engine = new Engine(options =>
        {
            options.AddExtensionMethods(typeof(PersonExtensions));
            options.Configure(_ => options.AddExtensionMethods(typeof(CustomStringExtensions)));
        });
        engine.SetValue("person", person);

        engine.Evaluate("person.MultiplyAge(2)").AsInteger().Should().Be(70);
        engine.Evaluate("'Hello World!'.Backwards()").AsString().Should().Be("!dlroW olleH");
    }

    [Test]
    public void ExtensionMethodsRegisteredFromAConfigureCallbackAreReportedBySecurityConfiguration()
    {
        var engine = new Engine(options =>
        {
            options.Configure(_ => options.AddExtensionMethods(typeof(CustomStringExtensions)));
        });

        engine.Diagnostics.ValidateSecurityConfiguration().Diagnostics
            .Select(d => d.Code)
            .Should().Contain(SecurityDiagnosticCodes.ClrExtensionMethodsConfigured);
    }

    [Test]
    public void AnUntrustedEngineSeesNoneOfWhatItsConfigureCallbackRegistered()
    {
        var engine = new Engine(options =>
        {
            options.Configure(e => e.Options.AddExtensionMethods(typeof(PersonExtensions)));
            options.ForUntrustedCode(new UntrustedCodeLimits
            {
                TimeoutInterval = TimeSpan.FromSeconds(5),
                MaxStatements = 100_000,
                MemoryLimit = 16_000_000,
                MaxRecursionDepth = 64,
                MaxArraySize = 10_000,
                RegexTimeout = TimeSpan.FromMilliseconds(100),
                PromiseTimeout = TimeSpan.FromMilliseconds(100),
                MaxOperationDuration = TimeSpan.FromSeconds(10),
            });
        });

        // The profile clears the registry on its way through Apply, and the refresh sits after it - so what
        // the callback registered reaches neither the prototypes nor the resolver.
        engine.Diagnostics.ValidateSecurityConfiguration().Diagnostics
            .Select(d => d.Code)
            .Should().NotContain(SecurityDiagnosticCodes.ClrExtensionMethodsConfigured);
    }

    [Test]
    public void RegisteringFromAConfigureCallbackIsRefusedOnceTheOptionsAreFrozen()
    {
        var options = new Options();
        options.Configure(_ => options.AddExtensionMethods(typeof(CustomStringExtensions)));

        var first = new Engine(options);
        first.Evaluate("'Hello World!'.Backwards()").AsString().Should().Be("!dlroW olleH");

        // The first engine froze the options it was handed, so the callback's own registration is what the
        // second engine refuses - the freeze is the guard, and it names the registry that was written to.
        var second = () => new Engine(options);
        second.Should().Throw<InvalidOperationException>()
            .WithMessage("*Options.Interop.ExtensionMethodTypes*");
    }
}
