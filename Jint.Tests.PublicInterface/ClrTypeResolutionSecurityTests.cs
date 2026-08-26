using System.Reflection;
using Jint.Runtime.Interop;

namespace Jint.Tests.PublicInterface;

public class ClrTypeResolutionSecurityTests
{
    [Test]
    public void NarrowAssemblyListDeniesUnlistedRuntimeCapabilities()
    {
        var engine = new Engine(options => options.AllowClr(typeof(AllowedClrHost).Assembly));

        engine.Evaluate("System.IO.File").Should().NotBeOfType<TypeReference>();
        engine.Evaluate("System.Environment").Should().NotBeOfType<TypeReference>();
        engine.Evaluate("System.Diagnostics.Process").Should().NotBeOfType<TypeReference>();
        engine.Evaluate("System.Reflection.Assembly").Should().NotBeOfType<TypeReference>();
    }

    [Test]
    public void EmptyComputedAssemblyListFailsClosed()
    {
        Assembly[] assemblies = [];
        var engine = new Engine(options => options.AllowClr(assemblies));

        engine.Evaluate("System.Object").Should().NotBeOfType<TypeReference>();
    }

    [Test]
    public void NarrowAssemblyListRetainsExplicitlyAllowedTypes()
    {
        var engine = new Engine(options => options.AllowClr(typeof(AllowedClrHost).Assembly));

        engine.Evaluate(
                "importNamespace('Jint.Tests.PublicInterface').AllowedClrHost.Value")
            .Should()
            .Be(42);
    }

    [Test]
    public void MemberFilterAppliesToNamespaceAndNestedTypeResolution()
    {
        var resolver = new TypeResolver
        {
            MemberFilter = member => member switch
            {
                Type type => type == typeof(AllowedClrHost) || type == typeof(AllowedClrHost.VisibleNested),
                _ => member.Name == nameof(AllowedClrHost.Value)
            }
        };
        var engine = new Engine(options =>
        {
            options.AllowClr(typeof(AllowedClrHost).Assembly);
            options.Interop.TypeResolver = resolver;
        });

        engine.Evaluate("importNamespace('Jint.Tests.PublicInterface').AllowedClrHost")
            .Should()
            .BeOfType<TypeReference>();
        engine.Evaluate("importNamespace('Jint.Tests.PublicInterface').DeniedClrHost")
            .Should()
            .NotBeOfType<TypeReference>();
        engine.Evaluate("importNamespace('Jint.Tests.PublicInterface').AllowedClrHost.VisibleNested")
            .Should()
            .BeOfType<TypeReference>();
        engine.Evaluate("importNamespace('Jint.Tests.PublicInterface').AllowedClrHost.HiddenNested")
            .Should()
            .NotBeOfType<TypeReference>();
        engine.Evaluate("importNamespace('Jint.Tests.PublicInterface').AllowedClrHost.PrivateNested")
            .Should()
            .NotBeOfType<TypeReference>();
        engine.Evaluate("importNamespace('Jint.Tests.PublicInterface').AllowedClrHost.InternalNested")
            .Should()
            .NotBeOfType<TypeReference>();
        engine.Evaluate("importNamespace('Jint.Tests.PublicInterface').InternalClrHost")
            .Should()
            .NotBeOfType<TypeReference>();
        engine.Evaluate("importNamespace('Jint.Tests.PublicInterface')['AllowedClrHost+PrivateNested']")
            .Should()
            .NotBeOfType<TypeReference>();
        engine.Evaluate("importNamespace('Jint.Tests.PublicInterface')['AllowedClrHost+PrivateNested+PublicChild']")
            .Should()
            .NotBeOfType<TypeReference>();
    }

    [Test]
    public void ExplicitTypeReferencesRemainCapabilitiesOutsideAssemblyList()
    {
        var engine = new Engine(options => options.AllowClr(typeof(AllowedClrHost).Assembly));
        engine.SetValue("StringType", TypeReference.CreateTypeReference<string>(engine));

        engine.Evaluate("StringType.IsNullOrEmpty('')").Should().BeTrue();
        engine.Evaluate("System.String").Should().NotBeOfType<TypeReference>();
    }

    [Test]
    public void ExplicitTypeReferenceRoundTripsOutsideAssemblyList()
    {
        var engine = new Engine(options =>
        {
            options.AllowClr();
            options.Interop.AllowGetType = true;
        });
        engine.SetValue("Explicit", TypeReference.CreateTypeReference<DeniedClrHost>(engine));

        engine.Evaluate("clrHelper.objectToType(clrHelper.typeToObject(Explicit))")
            .Should()
            .BeOfType<TypeReference>()
            .Which.ReferenceType.Should().Be(typeof(DeniedClrHost));
    }

    [Test]
    public void ExplicitTypeCapabilityDoesNotTransferToAnUnrelatedWrapper()
    {
        var engine = new Engine(options =>
        {
            options.AllowClr();
            options.Interop.AllowGetType = true;
        });
        engine.SetValue("Explicit", TypeReference.CreateTypeReference<DeniedClrHost>(engine));
        engine.SetValue("types", new ClrTypeHolder());
        engine.Execute("explicitObject = clrHelper.typeToObject(Explicit)");

        Invoking(() => engine.Evaluate("clrHelper.objectToType(types.HostDenied)"))
            .Should()
            .ThrowExactly<InvalidOperationException>()
            .WithMessage("*not allowed*");
    }

    [Test]
    public void ParameterlessAllowClrIsCoreAssemblyCompatibilityMode()
    {
        var engine = new Engine(options => options.AllowClr());

        engine.Evaluate("System.Object").Should().BeOfType<TypeReference>();
        engine.Evaluate("importNamespace('Jint.Tests.PublicInterface').AllowedClrHost")
            .Should()
            .NotBeOfType<TypeReference>();
    }

    [Test]
    public void GenericDefinitionUsesAllowedAssemblyAndExplicitTypeArgument()
    {
        var engine = new Engine(options => options.AllowClr(typeof(AllowedClrHost).Assembly));
        engine.SetValue("StringType", TypeReference.CreateTypeReference<string>(engine));

        engine.Evaluate(
                """
                const PublicInterface = importNamespace('Jint.Tests.PublicInterface');
                const StringBox = PublicInterface.AllowedClrBox(StringType);
                new StringBox('safe').Value;
                """)
            .Should()
            .Be("safe");
    }

    [Test]
    public void AllowGetTypeDoesNotExposeStaticTypeLookup()
    {
        var engine = new Engine(options =>
        {
            options.AllowClr();
            options.Interop.AllowGetType = true;
        });
        engine.SetValue("value", new object());

        engine.Evaluate("typeof value.GetType").Should().Be("function");
        engine.Evaluate("typeof System.Type.GetType").Should().Be("undefined");
    }

    [Test]
    public void ClrHelperCannotWidenOutsideAssemblyPolicy()
    {
        var engine = new Engine(options =>
        {
            options.AllowClr(typeof(AllowedClrHost).Assembly);
            options.Interop.AllowGetType = true;
        });
        engine.SetValue("types", new ClrTypeHolder());

        engine.Evaluate("clrHelper.objectToType(types.Allowed)")
            .Should()
            .BeOfType<TypeReference>()
            .Which.ReferenceType.Should().Be(typeof(AllowedClrHost));

        Invoking(() => engine.Evaluate("clrHelper.objectToType(types.Denied)"))
            .Should()
            .ThrowExactly<InvalidOperationException>()
            .WithMessage("*not allowed*");
    }

    [Test]
    public void ClrHelperUnwrapCannotRevealUnlistedConcreteType()
    {
        var engine = new Engine(options =>
        {
            options.AllowClr();
            options.Interop.AllowGetType = true;
        });
        var projected = ObjectWrapper.Create(engine, new ConcreteClrView(), typeof(IClrView));
        engine.SetValue("projected", projected);

        Invoking(() => engine.Evaluate("clrHelper.unwrap(projected)"))
            .Should()
            .ThrowExactly<InvalidOperationException>()
            .WithMessage("*not allowed*");
    }

    [Test]
    public void ReflectionNamespaceRequiresExplicitOptIn()
    {
        var restricted = new Engine(options => options.AllowClr(typeof(object).Assembly));
        restricted.Evaluate("System.Reflection.Assembly").Should().NotBeOfType<TypeReference>();

        var permissive = new Engine(options =>
        {
            options.AllowClr(typeof(object).Assembly);
            options.Interop.AllowSystemReflection = true;
        });
        permissive.Evaluate("System.Reflection.Assembly").Should().BeOfType<TypeReference>();
    }

    [Test]
    public void DeniedMemberErrorsAreSanitizedAcrossColdAndWarmCacheReads()
    {
        var resolver = new TypeResolver { MemberFilter = static member => member.Name != nameof(AllowedClrInstance.Secret) };
        var engine = new Engine(options =>
        {
            options.AllowClr(typeof(AllowedClrInstance).Assembly);
            options.Interop.TypeResolver = resolver;
            options.Interop.ThrowOnUnresolvedMember = true;
        });
        engine.SetValue("host", new AllowedClrInstance());

        for (var i = 0; i < 2; i++)
        {
            Invoking(() => engine.Evaluate("host.Secret"))
                .Should()
                .ThrowExactly<MissingMemberException>()
                .WithMessage("Cannot access the requested CLR member.")
                .Which.Message.Should().NotContain(nameof(AllowedClrInstance)).And.NotContain(nameof(AllowedClrInstance.Secret));
        }
    }

    [Test]
    public void DetailedDeniedMemberErrorsRetainCompatibilityMessage()
    {
        var resolver = new TypeResolver { MemberFilter = static member => member.Name != nameof(AllowedClrInstance.Secret) };
        var engine = new Engine(options =>
        {
            options.AllowClr(typeof(AllowedClrInstance).Assembly);
            options.Interop.TypeResolver = resolver;
            options.Interop.ThrowOnUnresolvedMember = true;
            options.Interop.ExposeDetailedResolutionErrors = true;
        });
        engine.SetValue("host", new AllowedClrInstance());

        Invoking(() => engine.Evaluate("host.Secret"))
            .Should()
            .ThrowExactly<MissingMemberException>()
            .WithMessage($"*{nameof(AllowedClrInstance.Secret)}*{nameof(AllowedClrInstance)}*");
    }

    [Test]
    public void GenericTypeBindingErrorsFollowResolutionDisclosurePolicy()
    {
        static Engine CreateEngine(bool exposeDetails)
        {
            var engine = new Engine(options =>
            {
                options.AllowClr(typeof(ConstrainedClrBox<>).Assembly);
                options.Interop.ExposeDetailedResolutionErrors = exposeDetails;
            });
            engine.SetValue("StringType", TypeReference.CreateTypeReference<string>(engine));
            return engine;
        }

        var sanitized = Invoking(() => CreateEngine(false).Evaluate(
                "importNamespace('Jint.Tests.PublicInterface').ConstrainedClrBox(StringType)"))
            .Should()
            .ThrowExactly<InvalidOperationException>()
            .Which;
        sanitized.Message.Should().Be("Could not construct the requested generic CLR type.");
        sanitized.InnerException.Should().BeNull();

        var detailed = Invoking(() => CreateEngine(true).Evaluate(
                "importNamespace('Jint.Tests.PublicInterface').ConstrainedClrBox(StringType)"))
            .Should()
            .ThrowExactly<InvalidOperationException>()
            .Which;
        detailed.Message.Should().Contain(nameof(ConstrainedClrBox<int>));
        detailed.InnerException.Should().NotBeNull();
    }
}

public static class AllowedClrHost
{
    public static int Value => 42;

    public sealed class VisibleNested;

    public sealed class HiddenNested;

    private sealed class PrivateNested
    {
        public sealed class PublicChild;
    }

    internal sealed class InternalNested;
}

public sealed class DeniedClrHost;

internal sealed class InternalClrHost;

public sealed class AllowedClrBox<T>(T value)
{
    public T Value { get; } = value;
}

public sealed class ConstrainedClrBox<T> where T : struct;

public sealed class AllowedClrInstance
{
    public string Secret => "secret";
}

public sealed class ClrTypeHolder
{
    public Type Allowed => typeof(AllowedClrHost);

    public Type Denied => typeof(Environment);

    public Type HostDenied => typeof(DeniedClrHost);
}

public interface IClrView
{
    string Name { get; }
}

public sealed class ConcreteClrView : IClrView
{
    public string Name => nameof(ConcreteClrView);
}
