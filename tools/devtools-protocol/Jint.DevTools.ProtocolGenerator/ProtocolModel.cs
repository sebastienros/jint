using System.Text.Json;

namespace Jint.DevTools.ProtocolGenerator;

/// <summary>
/// The vendored protocol description, read out of <c>js_protocol.json</c> and <c>browser_protocol.json</c>.
/// </summary>
public sealed class Protocol
{
    private readonly Dictionary<string, ProtocolDomain> _domains = new(StringComparer.Ordinal);

    private Protocol(string version, IReadOnlyList<ProtocolDomain> domains)
    {
        Version = version;
        Domains = domains;
        foreach (var domain in domains)
        {
            _domains[domain.Name] = domain;
        }
    }

    /// <summary>The protocol version both files declare, as <c>major.minor</c>.</summary>
    public string Version { get; }

    /// <summary>Every domain both files describe, in the order they appear.</summary>
    public IReadOnlyList<ProtocolDomain> Domains { get; }

    public ProtocolDomain Domain(string name)
    {
        return _domains.TryGetValue(name, out var domain)
            ? domain
            : throw new ProtocolGeneratorException($"The vendored protocol has no domain '{name}'.");
    }

    public bool HasDomain(string name) => _domains.ContainsKey(name);

    /// <summary>
    /// Reads the two protocol files out of <paramref name="directory"/> and merges their domain lists.
    /// </summary>
    public static Protocol Read(string directory)
    {
        var files = new[] { "js_protocol.json", "browser_protocol.json" };
        var domains = new List<ProtocolDomain>();
        string? version = null;

        foreach (var file in files)
        {
            var path = Path.Combine(directory, file);
            if (!File.Exists(path))
            {
                throw new ProtocolGeneratorException($"'{path}' does not exist. The vendored protocol is what the generator reads; see tools/devtools-protocol/README.md.");
            }

            using var document = JsonDocument.Parse(File.ReadAllBytes(path));
            var root = document.RootElement;

            var major = root.GetProperty("version").GetProperty("major").GetString();
            var minor = root.GetProperty("version").GetProperty("minor").GetString();
            var declared = major + "." + minor;

            if (version is not null && !string.Equals(version, declared, StringComparison.Ordinal))
            {
                throw new ProtocolGeneratorException($"'{file}' declares protocol version {declared} where the other file declares {version}. The two are vendored from one commit and must agree.");
            }

            version = declared;

            foreach (var element in root.GetProperty("domains").EnumerateArray())
            {
                domains.Add(ReadDomain(element));
            }
        }

        return new Protocol(version!, domains);
    }

    private static ProtocolDomain ReadDomain(JsonElement element)
    {
        return new ProtocolDomain
        {
            Name = element.GetProperty("domain").GetString()!,
            Description = Text(element, "description"),
            Experimental = Flag(element, "experimental"),
            Deprecated = Flag(element, "deprecated"),
            Types = ReadList(element, "types", ReadType),
            Commands = ReadList(element, "commands", ReadCommand),
            Events = ReadList(element, "events", ReadEvent),
        };
    }

    private static ProtocolType ReadType(JsonElement element)
    {
        return new ProtocolType
        {
            Id = element.GetProperty("id").GetString()!,
            Description = Text(element, "description"),
            Experimental = Flag(element, "experimental"),
            Deprecated = Flag(element, "deprecated"),
            Value = ReadValue(element),
            Properties = ReadList(element, "properties", ReadMember),
        };
    }

    private static ProtocolCommand ReadCommand(JsonElement element)
    {
        return new ProtocolCommand
        {
            Name = element.GetProperty("name").GetString()!,
            Description = Text(element, "description"),
            Experimental = Flag(element, "experimental"),
            Deprecated = Flag(element, "deprecated"),
            Redirect = Text(element, "redirect"),
            Parameters = ReadList(element, "parameters", ReadMember),
            Returns = ReadList(element, "returns", ReadMember),
        };
    }

    private static ProtocolEventDefinition ReadEvent(JsonElement element)
    {
        return new ProtocolEventDefinition
        {
            Name = element.GetProperty("name").GetString()!,
            Description = Text(element, "description"),
            Experimental = Flag(element, "experimental"),
            Deprecated = Flag(element, "deprecated"),
            Parameters = ReadList(element, "parameters", ReadMember),
        };
    }

    private static ProtocolMember ReadMember(JsonElement element)
    {
        return new ProtocolMember
        {
            Name = element.GetProperty("name").GetString()!,
            Description = Text(element, "description"),
            Optional = Flag(element, "optional"),
            Experimental = Flag(element, "experimental"),
            Deprecated = Flag(element, "deprecated"),
            Value = ReadValue(element),
        };
    }

    private static ProtocolValue ReadValue(JsonElement element)
    {
        IReadOnlyList<string>? enumeration = null;
        if (element.TryGetProperty("enum", out var enumElement))
        {
            var values = new List<string>();
            foreach (var value in enumElement.EnumerateArray())
            {
                values.Add(value.GetString()!);
            }

            enumeration = values;
        }

        ProtocolValue? items = null;
        if (element.TryGetProperty("items", out var itemsElement))
        {
            items = ReadValue(itemsElement);
        }

        return new ProtocolValue
        {
            Reference = Text(element, "$ref"),
            Kind = Text(element, "type"),
            Enumeration = enumeration,
            Items = items,
        };
    }

    private static List<T> ReadList<T>(JsonElement element, string name, Func<JsonElement, T> read)
    {
        if (!element.TryGetProperty(name, out var list))
        {
            return [];
        }

        var items = new List<T>();
        foreach (var item in list.EnumerateArray())
        {
            items.Add(read(item));
        }

        return items;
    }

    private static string? Text(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var value) ? value.GetString() : null;
    }

    private static bool Flag(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var value) && value.GetBoolean();
    }
}

/// <summary>One domain of the protocol.</summary>
public sealed class ProtocolDomain
{
    public required string Name { get; init; }
    public string? Description { get; init; }
    public bool Experimental { get; init; }
    public bool Deprecated { get; init; }
    public required IReadOnlyList<ProtocolType> Types { get; init; }
    public required IReadOnlyList<ProtocolCommand> Commands { get; init; }
    public required IReadOnlyList<ProtocolEventDefinition> Events { get; init; }
}

/// <summary>A named type a domain declares.</summary>
public sealed class ProtocolType
{
    public required string Id { get; init; }
    public string? Description { get; init; }
    public bool Experimental { get; init; }
    public bool Deprecated { get; init; }
    public required ProtocolValue Value { get; init; }
    public required IReadOnlyList<ProtocolMember> Properties { get; init; }

    /// <summary>Whether this is an object type, which is the only kind that becomes a record.</summary>
    public bool IsObject => string.Equals(Value.Kind, "object", StringComparison.Ordinal);
}

/// <summary>A command a domain declares.</summary>
public sealed class ProtocolCommand
{
    public required string Name { get; init; }
    public string? Description { get; init; }
    public bool Experimental { get; init; }
    public bool Deprecated { get; init; }

    /// <summary>The domain that actually handles this command, when the protocol redirects it.</summary>
    public string? Redirect { get; init; }

    public required IReadOnlyList<ProtocolMember> Parameters { get; init; }
    public required IReadOnlyList<ProtocolMember> Returns { get; init; }
}

/// <summary>An event a domain declares.</summary>
public sealed class ProtocolEventDefinition
{
    public required string Name { get; init; }
    public string? Description { get; init; }
    public bool Experimental { get; init; }
    public bool Deprecated { get; init; }
    public required IReadOnlyList<ProtocolMember> Parameters { get; init; }
}

/// <summary>A property of a type, a parameter of a command or event, or one of a command's returns.</summary>
public sealed class ProtocolMember
{
    public required string Name { get; init; }
    public string? Description { get; init; }
    public bool Optional { get; init; }
    public bool Experimental { get; init; }
    public bool Deprecated { get; init; }
    public required ProtocolValue Value { get; init; }
}

/// <summary>What a member or a type says about the shape of its value.</summary>
public sealed class ProtocolValue
{
    /// <summary>The <c>$ref</c>, either <c>Type</c> or <c>Domain.Type</c>, when there is one.</summary>
    public string? Reference { get; init; }

    /// <summary>The protocol's own type name: <c>string</c>, <c>integer</c>, <c>object</c>, …</summary>
    public string? Kind { get; init; }

    /// <summary>The admissible strings, when the value is an enumeration.</summary>
    public IReadOnlyList<string>? Enumeration { get; init; }

    /// <summary>The element shape, when the value is an array.</summary>
    public ProtocolValue? Items { get; init; }
}

/// <summary>
/// A generation failure that names what in the pinned protocol or the manifest the emitter could not
/// turn into code.
/// </summary>
public sealed class ProtocolGeneratorException : Exception
{
    public ProtocolGeneratorException(string message) : base(message)
    {
    }

    public ProtocolGeneratorException(string message, Exception innerException) : base(message, innerException)
    {
    }

    public ProtocolGeneratorException()
    {
    }
}
