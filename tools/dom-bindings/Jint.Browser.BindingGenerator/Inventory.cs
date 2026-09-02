using System.Reflection;

namespace Jint.Browser.BindingGenerator;

/// <summary>
/// Reads the pinned AngleSharp assemblies and answers the questions the model is built from: which types
/// carry which <c>AngleSharp.Attributes</c> attribute, and what each one says.
/// </summary>
/// <remarks>
/// Everything here goes through <see cref="CustomAttributeData"/> rather than through
/// <see cref="Attribute"/> instances, because the assemblies are loaded into a
/// <see cref="System.Reflection.MetadataLoadContext"/> — nothing in them is executable, and nothing in them
/// shares a <see cref="Type"/> identity with anything this process compiled against.
/// </remarks>
internal static class Inventory
{
    internal const string AttributeNamespace = "AngleSharp.Attributes";

    internal const string DomName = "DomNameAttribute";
    internal const string DomAccessor = "DomAccessorAttribute";
    internal const string DomConstructor = "DomConstructorAttribute";
    internal const string DomNoInterfaceObject = "DomNoInterfaceObjectAttribute";
    internal const string DomPutForwards = "DomPutForwardsAttribute";
    internal const string DomLenientThis = "DomLenientThisAttribute";
    internal const string DomHistorical = "DomHistoricalAttribute";
    internal const string DomInitDict = "DomInitDictAttribute";
    internal const string DomLiterals = "DomLiteralsAttribute";
    internal const string DomExposed = "DomExposedAttribute";
    internal const string DomInstance = "DomInstanceAttribute";
    internal const string DomDescription = "DomDescriptionAttribute";

    /// <summary>AngleSharp's <c>Accessors</c> flags.</summary>
    [Flags]
    internal enum Accessors
    {
        None = 0,
        Getter = 1,
        Setter = 2,
        Deleter = 4,
        Adder = 8,
        Remover = 16,
        Method = 32,
    }

    internal static IEnumerable<CustomAttributeData> DomAttributes(MemberInfo member)
        => member.GetCustomAttributesData().Where(a => a.AttributeType.Namespace == AttributeNamespace);

    internal static bool Has(MemberInfo member, string attribute)
        => DomAttributes(member).Any(a => a.AttributeType.Name == attribute);

    /// <summary>Every <c>[DomName]</c> on the member. The attribute allows multiples, and several members use it.</summary>
    internal static List<string> DomNames(MemberInfo member)
        => DomAttributes(member)
            .Where(a => a.AttributeType.Name == DomName)
            .Select(a => (string) a.ConstructorArguments[0].Value!)
            .ToList();

    internal static string? FirstDomName(MemberInfo member)
        => DomAttributes(member).FirstOrDefault(a => a.AttributeType.Name == DomName)?.ConstructorArguments[0].Value as string;

    internal static Accessors AccessorsOf(MemberInfo member)
    {
        var attribute = DomAttributes(member).FirstOrDefault(a => a.AttributeType.Name == DomAccessor);
        return attribute is null ? Accessors.None : (Accessors) Convert.ToInt32(attribute.ConstructorArguments[0].Value, System.Globalization.CultureInfo.InvariantCulture);
    }

    internal static string? PutForwards(MemberInfo member)
        => DomAttributes(member).FirstOrDefault(a => a.AttributeType.Name == DomPutForwards)?.ConstructorArguments[0].Value as string;

    /// <summary>The <c>[DomInitDict]</c> offset, or <c>-1</c>.</summary>
    internal static int InitDictOffset(MemberInfo member)
    {
        var attribute = DomAttributes(member).FirstOrDefault(a => a.AttributeType.Name == DomInitDict);
        return attribute is null ? -1 : Convert.ToInt32(attribute.ConstructorArguments[0].Value, System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// The members of one type that are candidates for projection: <c>[DomName]</c>-bearing properties and
    /// non-accessor methods. CLR events are deliberately absent — every one of them is an <c>on*</c> event
    /// handler content attribute, and Jint's own event bus owns those (campaign item R2).
    /// </summary>
    internal static IEnumerable<MemberInfo> CandidateMembers(Type type)
    {
        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        {
            if (Has(property, DomName) || AccessorsOf(property) != Accessors.None)
            {
                yield return property;
            }
        }

        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        {
            if (!method.IsSpecialName && (Has(method, DomName) || AccessorsOf(method) != Accessors.None))
            {
                yield return method;
            }
        }
    }

    /// <summary>Every interface in the two assemblies, whether or not it carries <c>[DomName]</c>.</summary>
    internal static List<Type> AllInterfaces(IEnumerable<Assembly> assemblies)
        => assemblies.SelectMany(a => a.GetTypes()).Where(t => t.IsInterface).ToList();

    /// <summary>
    /// Counts every <c>AngleSharp.Attributes</c> attribute use per assembly — the inventory the pull request
    /// reports, and the number that moves when the pin does.
    /// </summary>
    internal static Dictionary<string, int> CountAttributes(Assembly assembly)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);

        void Bump(MemberInfo member)
        {
            foreach (var attribute in DomAttributes(member))
            {
                counts[attribute.AttributeType.Name] = counts.GetValueOrDefault(attribute.AttributeType.Name) + 1;
            }
        }

        foreach (var type in assembly.GetTypes())
        {
            Bump(type);
            foreach (var member in type.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                Bump(member);
            }
        }

        return counts;
    }
}
