#nullable enable

using System.Reflection;
using Jint.Native;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// <c>ShadowRealm.SetValue</c> is the mirror of <c>Engine.SetValue</c>: the same overload set, the same
/// trimming annotations with the same messages, and the same nullability, differing only in which global
/// object is written and what is returned for chaining.
/// </summary>
/// <remarks>
/// It was not always. The <see cref="Delegate"/> overload carried
/// <c>[RequiresUnreferencedCode("User supplied delegate")]</c> — a warning about a registration that
/// reflects over nothing a trimmer can remove — while the reflective <c>object</c> overload beside it,
/// which is the actual trimming hazard, carried no annotation at all. A trimming host projecting a host
/// object into a shadow realm therefore got no diagnostic and a member that read as <c>undefined</c> at
/// run time. Three overloads an embedder can reach on the engine were missing outright, and the two
/// surfaces disagreed about whether <c>null</c> was allowed.
/// </remarks>
public class ShadowRealmSetValueTests
{
    public sealed class Company
    {
        public Company(string name) => Name = name;

        public string Name { get; }

        public string Shout() => Name.ToUpperInvariant();
    }

    /// <summary>
    /// The mirror itself, asserted rather than described: same overloads, same type parameters, same
    /// <c>[DynamicallyAccessedMembers]</c> and <c>[RequiresUnreferencedCode]</c>, same messages.
    /// </summary>
    [Fact]
    public void TheOverloadSetAndItsTrimmingAnnotationsMirrorTheEngines()
    {
        var shadowRealm = DescribeSetValueOverloads(typeof(Native.ShadowRealm.ShadowRealm));
        var engine = DescribeSetValueOverloads(typeof(Engine));

        shadowRealm.Should().Equal(engine);

        // and the mirror is of something, rather than of two empty sets
        engine.Should().HaveCount(10);

#if NET8_0_OR_GREATER
        // Only where the attributes are real BCL types: PolySharp's downlevel stand-ins reach neither the
        // emitted metadata nor the public API baselines, which is why net472 and the two netstandard
        // baselines show none of this.
        engine.Should().ContainSingle(x => x.Contains("[RequiresUnreferencedCode", StringComparison.Ordinal));
        // the open paren matters: the [RequiresUnreferencedCode] message names the attribute too
        engine.Count(x => x.Contains("[DynamicallyAccessedMembers(", StringComparison.Ordinal)).Should().Be(3);
#endif
    }

    [Fact]
    public void ATypedHostObjectIsProjectedIntoTheShadowRealmAndNowhereElse()
    {
        var engine = new Engine();
        var shadowRealm = engine.Intrinsics.ShadowRealm.Construct();

        shadowRealm.SetValue("company", new Company("acme")).Should().BeSameAs(shadowRealm);

        shadowRealm.Evaluate("company.Name").Should().Be("acme");
        shadowRealm.Evaluate("company.Shout()").Should().Be("ACME");
        engine.Evaluate("typeof company").Should().Be("undefined");
    }

    [Fact]
    public void AnUntypedHostObjectStillProjects()
    {
        var engine = new Engine();
        var shadowRealm = engine.Intrinsics.ShadowRealm.Construct();

        object company = new Company("acme");
        shadowRealm.SetValue("company", company);

        shadowRealm.Evaluate("company.Name").Should().Be("acme");
    }

    /// <summary>
    /// The array overload exists so the annotation lands on the element type; what an embedder can observe
    /// is that the elements' own members are the ones script reaches.
    /// </summary>
    [Fact]
    public void AnArrayIsProjectedByItsElementType()
    {
        var engine = new Engine();
        var shadowRealm = engine.Intrinsics.ShadowRealm.Construct();

        shadowRealm.SetValue("companies", new[] { new Company("acme"), new Company("initech") });

        shadowRealm.Evaluate("companies.length").Should().Be(2);
        shadowRealm.Evaluate("companies[1].Name").Should().Be("initech");
    }

    [Fact]
    public void ATypeIsProjectedAsAConstructorScriptCanName()
    {
        var engine = new Engine();
        var shadowRealm = engine.Intrinsics.ShadowRealm.Construct();

        shadowRealm.SetValue("Company", typeof(Company));

        shadowRealm.Evaluate("new Company('acme').Name").Should().Be("acme");
        engine.Evaluate("typeof Company").Should().Be("undefined");
    }

    /// <summary>
    /// <c>ShadowRealm</c> took <c>string</c> and <c>object</c> where the engine took <c>string?</c> and
    /// <c>object?</c>, so the two disagreed about a registration a host can plausibly make.
    /// </summary>
    [Fact]
    public void ANullValueRegistersAsNullOnBothSurfaces()
    {
        var engine = new Engine();
        var shadowRealm = engine.Intrinsics.ShadowRealm.Construct();

        engine.SetValue("s", (string?) null);
        engine.SetValue("o", (object?) null);
        shadowRealm.SetValue("s", (string?) null);
        shadowRealm.SetValue("o", (object?) null);

        engine.Evaluate("s === null && o === null").Should().BeTrue();
        shadowRealm.Evaluate("s === null && o === null").Should().BeTrue();
    }

    /// <summary>
    /// The delegate overload is the one whose property attributes differ, on both surfaces: non-enumerable,
    /// which is what ECMA-262 §17 gives a built-in global function, while every other overload installs the
    /// ordinary enumerable data property.
    /// </summary>
    [Fact]
    public void ADelegateIsInstalledNonEnumerableAndEverythingElseIsNot()
    {
        var engine = new Engine();
        var shadowRealm = engine.Intrinsics.ShadowRealm.Construct();

        shadowRealm.SetValue("fn", (Delegate) new Func<int>(() => 42));
        shadowRealm.SetValue("num", 42);

        shadowRealm.Evaluate("fn()").Should().Be(42);
        shadowRealm.Evaluate("Object.keys(globalThis).indexOf('fn')").Should().Be(-1);
        shadowRealm.Evaluate("Object.keys(globalThis).indexOf('num') >= 0").Should().BeTrue();
    }

    /// <summary>
    /// The primitive overloads still bind to themselves rather than to the newly added generic one - which
    /// is why each of them casts its <c>JsNumber</c> / <c>JsBoolean</c> to <c>JsValue</c> before handing it
    /// on, exactly as the engine's do.
    /// </summary>
    [Fact]
    public void ThePrimitiveOverloadsKeepTheirTypes()
    {
        var engine = new Engine();
        var shadowRealm = engine.Intrinsics.ShadowRealm.Construct();

        shadowRealm
            .SetValue("s", "text")
            .SetValue("d", 1.5)
            .SetValue("i", 7)
            .SetValue("b", true)
            .SetValue("v", JsValue.Undefined);

        shadowRealm.Evaluate("[typeof s, typeof d, typeof i, typeof b, typeof v].join(',')")
            .Should().Be("string,number,number,boolean,undefined");
        shadowRealm.Evaluate("s + '|' + d + '|' + i + '|' + b").Should().Be("text|1.5|7|true");
    }

    private static List<string> DescribeSetValueOverloads(Type type)
    {
        return type
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => string.Equals(m.Name, "SetValue", StringComparison.Ordinal))
            .Select(Describe)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();
    }

    private static string Describe(MethodInfo method)
    {
        var typeParameters = method.IsGenericMethodDefinition
            ? "<" + string.Join(", ", method.GetGenericArguments().Select(a => a.Name + DescribeAnnotations(a.GetCustomAttributes(inherit: false)))) + ">"
            : "";

        var parameters = string.Join(", ", method.GetParameters().Select(DescribeParameter));

        return "SetValue" + typeParameters + "(" + parameters + ")" + DescribeAnnotations(method.GetCustomAttributes(inherit: false));
    }

    private static string DescribeParameter(ParameterInfo parameter)
    {
        return parameter.ParameterType.Name
               + DescribeNullability(parameter)
               + DescribeAnnotations(parameter.GetCustomAttributes(inherit: false));
    }

    private static string DescribeNullability(ParameterInfo parameter)
    {
#if NET8_0_OR_GREATER
        return new NullabilityInfoContext().Create(parameter).WriteState == NullabilityState.Nullable ? "?" : "";
#else
        _ = parameter;
        return "";
#endif
    }

    /// <summary>
    /// Read by name rather than by type: on the downlevel targets both Jint and this test project get their
    /// own internal PolySharp copy of these attributes, so <c>GetCustomAttribute&lt;T&gt;</c> would look for
    /// the wrong one and quietly find nothing on either side of the comparison.
    /// </summary>
    private static string DescribeAnnotations(object[] attributes)
    {
        var described = attributes
            .Select(DescribeAnnotation)
            .Where(x => x is not null)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        return described.Count == 0 ? "" : " " + string.Join(" ", described);
    }

    private static string? DescribeAnnotation(object attribute)
    {
        var name = attribute.GetType().FullName;
        if (string.Equals(name, "System.Diagnostics.CodeAnalysis.RequiresUnreferencedCodeAttribute", StringComparison.Ordinal))
        {
            return "[RequiresUnreferencedCode(" + Read(attribute, "Message") + ")]";
        }

        if (string.Equals(name, "System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembersAttribute", StringComparison.Ordinal))
        {
            return "[DynamicallyAccessedMembers(" + Read(attribute, "MemberTypes") + ")]";
        }

        return null;
    }

    private static string Read(object attribute, string propertyName)
    {
        var value = attribute.GetType().GetProperty(propertyName)?.GetValue(attribute);
        return Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? "";
    }
}
