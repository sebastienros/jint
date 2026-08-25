#if PUBLIC_API_BASELINES
#nullable enable

using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml.Linq;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// Why a declaration that is part of the assembly's public surface is nevertheless not something a person
/// writes a <c>&lt;summary&gt;</c> for.
/// </summary>
internal enum ApiExclusion
{
    /// <summary>The declaration is contract a caller reads, and needs a summary of its own.</summary>
    None,

    /// <summary>
    /// A member the compiler synthesized, not one anybody wrote: a record's <c>Equals</c>,
    /// <c>GetHashCode</c>, <c>ToString</c>, <c>op_Equality</c>, <c>op_Inequality</c>, <c>PrintMembers</c>,
    /// <c>&lt;Clone&gt;$</c>, <c>EqualityContract</c>, copy constructor and positional <c>Deconstruct</c>.
    /// </summary>
    CompilerGenerated,

    /// <summary>
    /// <c>Invoke</c>, <c>BeginInvoke</c>, <c>EndInvoke</c> or the constructor of a delegate type. The
    /// <c>delegate</c> declaration itself carries the summary and the <c>&lt;param&gt;</c> tags.
    /// </summary>
    DelegateMember,

    /// <summary>
    /// An <c>override</c>. The declaration it overrides is in the surface and is held to the rule; an IDE
    /// falls back to that one's documentation, so a summary here would be a second copy of it.
    /// </summary>
    Override,

    /// <summary>
    /// An explicit interface implementation. It is unreachable without a cast to the interface, whose member
    /// is in the surface and carries the documentation.
    /// </summary>
    /// <remarks>
    /// Nothing reaches this today: the compiler emits such a member as <c>private</c>, so the visibility
    /// filter has already dropped it. Naming the case keeps the decision visible if that filter ever widens.
    /// </remarks>
    ExplicitInterfaceImplementation,
}

/// <summary>
/// One declaration of Jint's public API surface, named the way a documentation comment names it.
/// </summary>
/// <param name="Id">
/// The ISO documentation comment identifier — <c>M:Jint.Engine.Evaluate(System.String,System.String)</c> —
/// which is the key the compiler writes into <c>Jint.xml</c>.
/// </param>
/// <param name="Namespace">The namespace of the declaring type, for reporting the debt by area.</param>
/// <param name="Exclusion">Why no summary is demanded of it, or <see cref="ApiExclusion.None"/>.</param>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct ApiDeclaration(string Id, string Namespace, ApiExclusion Exclusion)
{
    /// <summary>Whether a person is expected to have written a <c>&lt;summary&gt;</c> for it.</summary>
    public bool NeedsSummary => Exclusion == ApiExclusion.None;

    /// <summary>
    /// Whether the approved API baseline renders a line for it. The baseline is the whole public surface
    /// minus what no one declared, which is a wider set than <see cref="NeedsSummary"/>: an
    /// <c>override</c> and an explicit interface implementation are both written down there.
    /// </summary>
    public bool AppearsInBaseline
        => Exclusion is not ApiExclusion.CompilerGenerated and not ApiExclusion.DelegateMember;
}

/// <summary>
/// Enumerates the public API surface of a Jint assembly straight out of its metadata, as documentation
/// comment identifiers.
/// </summary>
/// <remarks>
/// <para>
/// This reads the assembly rather than parsing <c>Verify/PublicApiTest_&lt;tfm&gt;.verified.txt</c>, because
/// the baseline is C#-like prose and the XML documentation file is keyed by documentation comment id: joining
/// them means producing an id either way, and metadata is the only side that can produce one exactly. The
/// baseline is still used — <see cref="PublicApiDocumentationTest"/> holds the two to the same declaration
/// count, so this enumerator cannot quietly stop seeing part of the approved surface.
/// </para>
/// <para>
/// Only <em>declared</em> members are enumerated (<see cref="BindingFlags.DeclaredOnly"/>). An inherited
/// member's documentation lives on the declaration it was inherited from, which is in this surface already.
/// </para>
/// </remarks>
internal static class PublicApiSurface
{
    private const string CompilerGeneratedAttribute = "System.Runtime.CompilerServices.CompilerGeneratedAttribute";

    private const BindingFlags Declared =
        BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    public static List<ApiDeclaration> Enumerate(Assembly assembly)
    {
        var declarations = new List<ApiDeclaration>();

        foreach (var type in assembly.GetTypes())
        {
            if (!IsPartOfTheContract(type))
            {
                continue;
            }

            var space = type.Namespace ?? string.Empty;
            declarations.Add(new ApiDeclaration(DocumentationCommentId.ForType(type), space, ApiExclusion.None));

            var isDelegate = type.BaseType?.FullName == "System.MulticastDelegate";

            foreach (var field in type.GetFields(Declared))
            {
                // The only special-named field a contract type has is an enum's value__, which is storage
                // rather than a member anyone names.
                if (!IsVisible(field.Attributes & FieldAttributes.FieldAccessMask) || field.IsSpecialName)
                {
                    continue;
                }

                declarations.Add(new ApiDeclaration(
                    DocumentationCommentId.ForField(field),
                    space,
                    HasCompilerGenerated(field) ? ApiExclusion.CompilerGenerated : ApiExclusion.None));
            }

            foreach (var property in type.GetProperties(Declared))
            {
                var accessors = property.GetAccessors(nonPublic: true);
                if (!Array.Exists(accessors, a => IsVisible(a.Attributes & MethodAttributes.MemberAccessMask)))
                {
                    continue;
                }

                declarations.Add(new ApiDeclaration(
                    DocumentationCommentId.ForProperty(property),
                    space,
                    HasCompilerGenerated(property) ? ApiExclusion.CompilerGenerated
                        : Array.TrueForAll(accessors, IsOverride) ? ApiExclusion.Override
                        : ApiExclusion.None));
            }

            foreach (var @event in type.GetEvents(Declared))
            {
                var add = @event.GetAddMethod(nonPublic: true);
                if (add is null || !IsVisible(add.Attributes & MethodAttributes.MemberAccessMask))
                {
                    continue;
                }

                declarations.Add(new ApiDeclaration(
                    DocumentationCommentId.ForEvent(@event),
                    space,
                    HasCompilerGenerated(@event) ? ApiExclusion.CompilerGenerated
                        : IsOverride(add) ? ApiExclusion.Override
                        : ApiExclusion.None));
            }

            foreach (var method in Methods(type))
            {
                if (!IsVisible(method.Attributes & MethodAttributes.MemberAccessMask) || IsAccessor(method))
                {
                    continue;
                }

                declarations.Add(new ApiDeclaration(
                    DocumentationCommentId.ForMethod(method),
                    space,
                    ExclusionOf(method, isDelegate)));
            }
        }

        declarations.Sort(static (a, b) => string.CompareOrdinal(a.Id, b.Id));
        return declarations;
    }

    private static IEnumerable<MethodBase> Methods(Type type)
    {
        foreach (var method in type.GetMethods(Declared))
        {
            yield return method;
        }

        foreach (var constructor in type.GetConstructors(Declared))
        {
            yield return constructor;
        }
    }

    private static ApiExclusion ExclusionOf(MethodBase method, bool isDelegate)
    {
        if (isDelegate)
        {
            return ApiExclusion.DelegateMember;
        }

        if (HasCompilerGenerated(method))
        {
            return ApiExclusion.CompilerGenerated;
        }

        // A constructor's metadata name is ".ctor", so the dot has to be ruled out before it is read as the
        // dotted interface name an explicit implementation carries.
        if (method is not ConstructorInfo && method.Name.Contains('.', StringComparison.Ordinal))
        {
            return ApiExclusion.ExplicitInterfaceImplementation;
        }

        return IsOverride(method) ? ApiExclusion.Override : ApiExclusion.None;
    }

    /// <summary>
    /// Whether a type and every type it is nested in are reachable from outside the assembly. A
    /// <c>protected</c> nested type counts: a host deriving from the outer type reaches it.
    /// </summary>
    private static bool IsPartOfTheContract(Type type)
    {
        for (var current = type; current is not null; current = current.DeclaringType)
        {
            if (current.IsNested)
            {
                if (!current.IsNestedPublic && !current.IsNestedFamily && !current.IsNestedFamORAssem)
                {
                    return false;
                }
            }
            else if (!current.IsPublic)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Whether the method reuses a base type's virtual slot, which is what an <c>override</c> is in metadata.
    /// A <c>virtual</c> or <c>abstract</c> declaration takes a new slot and is therefore not one.
    /// </summary>
    private static bool IsOverride(MethodBase method)
        => (method.Attributes & MethodAttributes.Virtual) != 0
        && (method.Attributes & MethodAttributes.NewSlot) == 0;

    private static bool IsAccessor(MethodBase method)
        => method.IsSpecialName
        && (method.Name.StartsWith("get_", StringComparison.Ordinal)
            || method.Name.StartsWith("set_", StringComparison.Ordinal)
            || method.Name.StartsWith("add_", StringComparison.Ordinal)
            || method.Name.StartsWith("remove_", StringComparison.Ordinal));

    // MetadataLoadContext never runs a constructor, so an attribute is read off CustomAttributeData rather
    // than instantiated.
    private static bool HasCompilerGenerated(MemberInfo member)
    {
        foreach (var attribute in member.GetCustomAttributesData())
        {
            if (attribute.AttributeType.FullName == CompilerGeneratedAttribute)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsVisible(FieldAttributes access)
        => access is FieldAttributes.Public or FieldAttributes.Family or FieldAttributes.FamORAssem;

    private static bool IsVisible(MethodAttributes access)
        => access is MethodAttributes.Public or MethodAttributes.Family or MethodAttributes.FamORAssem;
}

/// <summary>
/// Renders the ISO documentation comment identifier of a metadata declaration — the string the C# compiler
/// writes into the <c>name</c> attribute of a <c>&lt;member&gt;</c> element.
/// </summary>
/// <remarks>
/// Annex E of the C# specification defines the grammar. The parts that matter here are that a declaring type
/// keeps its arity as <c>`n</c> while a referenced one spells its arguments as <c>{…}</c>, that a type
/// parameter is <c>`i</c> for the type's own and <c>``i</c> for a method's, and that a conversion operator
/// carries its return type after a <c>~</c> because that is the only thing distinguishing two of them.
/// </remarks>
internal static class DocumentationCommentId
{
    public static string ForType(Type type)
    {
        var builder = new StringBuilder("T:");
        AppendDeclaringName(builder, type);
        return builder.ToString();
    }

    public static string ForField(FieldInfo field)
        => Member('F', field.DeclaringType!, field.Name);

    public static string ForEvent(EventInfo @event)
        => Member('E', @event.DeclaringType!, @event.Name);

    public static string ForProperty(PropertyInfo property)
    {
        var builder = Prefix('P', property.DeclaringType!, property.Name);
        AppendParameters(builder, property.GetIndexParameters());
        return builder.ToString();
    }

    public static string ForMethod(MethodBase method)
    {
        var builder = Prefix(
            'M',
            method.DeclaringType!,
            method is ConstructorInfo ? (method.IsStatic ? "#cctor" : "#ctor") : method.Name);

        if (method.IsGenericMethodDefinition)
        {
            builder.Append("``").Append(method.GetGenericArguments().Length);
        }

        AppendParameters(builder, method.GetParameters());

        if (method is MethodInfo conversion
            && method.IsSpecialName
            && method.Name is "op_Implicit" or "op_Explicit")
        {
            builder.Append('~');
            AppendTypeReference(builder, conversion.ReturnType);
        }

        return builder.ToString();
    }

    private static string Member(char kind, Type declaringType, string name)
        => Prefix(kind, declaringType, name).ToString();

    private static StringBuilder Prefix(char kind, Type declaringType, string name)
    {
        var builder = new StringBuilder(2 + name.Length);
        builder.Append(kind).Append(':');
        AppendDeclaringName(builder, declaringType);
        builder.Append('.');

        // An explicit interface implementation carries the interface in its metadata name, and the dots and
        // angle brackets of that name are not the ones the grammar uses to separate an identifier.
        foreach (var c in name)
        {
            builder.Append(c switch
            {
                '.' => '#',
                '<' => '{',
                '>' => '}',
                _ => c,
            });
        }

        return builder;
    }

    private static void AppendParameters(StringBuilder builder, ParameterInfo[] parameters)
    {
        if (parameters.Length == 0)
        {
            return;
        }

        builder.Append('(');
        for (var i = 0; i < parameters.Length; i++)
        {
            if (i > 0)
            {
                builder.Append(',');
            }

            AppendTypeReference(builder, parameters[i].ParameterType);
        }

        builder.Append(')');
    }

    /// <summary>Declaration form: nesting flattened onto '.', generic arity spelled <c>`n</c>.</summary>
    private static void AppendDeclaringName(StringBuilder builder, Type type)
    {
        var chain = NestingChain(type);
        AppendNamespace(builder, chain[0]);

        for (var i = 0; i < chain.Count; i++)
        {
            if (i > 0)
            {
                builder.Append('.');
            }

            var (name, arity) = SplitArity(chain[i].Name);
            builder.Append(name);
            if (arity > 0)
            {
                builder.Append('`').Append(arity);
            }
        }
    }

    /// <summary>Reference form: generic arguments spelled <c>{…}</c>, arrays and by-refs decorated.</summary>
    private static void AppendTypeReference(StringBuilder builder, Type type)
    {
        if (type.IsByRef)
        {
            AppendTypeReference(builder, type.GetElementType()!);
            builder.Append('@');
            return;
        }

        if (type.IsPointer)
        {
            AppendTypeReference(builder, type.GetElementType()!);
            builder.Append('*');
            return;
        }

        if (type.IsArray)
        {
            AppendTypeReference(builder, type.GetElementType()!);
            var rank = type.GetArrayRank();
            if (rank == 1 && type.Name.EndsWith("[]", StringComparison.Ordinal))
            {
                builder.Append("[]");
            }
            else
            {
                builder.Append('[');
                for (var i = 0; i < rank; i++)
                {
                    builder.Append(i > 0 ? ",0:" : "0:");
                }

                builder.Append(']');
            }

            return;
        }

        if (type.IsGenericParameter)
        {
            builder.Append(type.DeclaringMethod is null ? "`" : "``").Append(type.GenericParameterPosition);
            return;
        }

        var chain = NestingChain(type);
        AppendNamespace(builder, chain[0]);

        // GetGenericArguments is cumulative over the nesting chain, while the arity in each metadata name
        // counts only the parameters that level introduced, so the arguments are handed out level by level.
        var arguments = type.IsGenericType ? type.GetGenericArguments() : Type.EmptyTypes;
        var consumed = 0;

        for (var i = 0; i < chain.Count; i++)
        {
            if (i > 0)
            {
                builder.Append('.');
            }

            var (name, arity) = SplitArity(chain[i].Name);
            builder.Append(name);
            if (arity == 0)
            {
                continue;
            }

            builder.Append('{');
            for (var a = 0; a < arity; a++)
            {
                if (a > 0)
                {
                    builder.Append(',');
                }

                AppendTypeReference(builder, arguments[consumed + a]);
            }

            builder.Append('}');
            consumed += arity;
        }
    }

    private static void AppendNamespace(StringBuilder builder, Type outermost)
    {
        if (!string.IsNullOrEmpty(outermost.Namespace))
        {
            builder.Append(outermost.Namespace).Append('.');
        }
    }

    private static List<Type> NestingChain(Type type)
    {
        var chain = new List<Type>();
        for (var current = type; current is not null; current = current.IsNested ? current.DeclaringType : null)
        {
            chain.Add(current);
        }

        chain.Reverse();
        return chain;
    }

    private static (string Name, int Arity) SplitArity(string metadataName)
    {
        var tick = metadataName.IndexOf('`');
        return tick < 0
            ? (metadataName, 0)
            : (metadataName.Substring(0, tick), int.Parse(metadataName.Substring(tick + 1), CultureInfo.InvariantCulture));
    }
}

/// <summary>
/// Reads the <c>Jint.xml</c> the compiler produces beside each shipped assembly, which is the only place the
/// documentation comments exist as data rather than as source text.
/// </summary>
internal static class XmlDocumentationFile
{
    /// <summary>
    /// The documentation comment ids that carry something an IDE can show: a <c>&lt;summary&gt;</c> with
    /// content, or an <c>&lt;inheritdoc&gt;</c>, which resolves to the summary of the declaration it inherits
    /// from — and that declaration is held to the rule in its own right.
    /// </summary>
    public static HashSet<string> DeclarationsWithASummary(string path)
    {
        var documented = new HashSet<string>(StringComparer.Ordinal);

        foreach (var member in Members(path))
        {
            var name = member.Attribute("name")?.Value;
            if (name is null)
            {
                continue;
            }

            if (member.Element("inheritdoc") is not null)
            {
                documented.Add(name);
                continue;
            }

            foreach (var summary in member.Elements("summary"))
            {
                if (!string.IsNullOrWhiteSpace(summary.Value) || summary.HasElements)
                {
                    documented.Add(name);
                    break;
                }
            }
        }

        return documented;
    }

    /// <summary>
    /// The ids whose documentation nests a <c>&lt;para&gt;</c> inside another one. That is well-formed XML,
    /// so nothing in the build notices, and it is invalid in every renderer downstream — a paragraph cannot
    /// contain a paragraph.
    /// </summary>
    public static List<string> DeclarationsNestingAParagraph(string path)
    {
        var nesting = new List<string>();

        foreach (var member in Members(path))
        {
            var name = member.Attribute("name")?.Value;
            if (name is null)
            {
                continue;
            }

            foreach (var paragraph in member.Descendants("para"))
            {
                if (paragraph.Descendants("para").Any())
                {
                    nesting.Add(name);
                    break;
                }
            }
        }

        return nesting;
    }

    private static IEnumerable<XElement> Members(string path)
        => XDocument.Load(path).Descendants("member");
}
#endif
