using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Jint.DevTools.ProtocolGenerator;

/// <summary>
/// Turns the vendored protocol plus the manifest into the files under
/// <c>Jint.DevTools/Protocol/Generated/</c>.
/// </summary>
/// <remarks>
/// The output is a dictionary rather than a directory write so that
/// <c>Jint.Tests.DevTools/Protocol/GeneratedProtocolIsCurrentTests.cs</c> can run the emitter in memory and
/// compare it with what is checked in. Every line it produces uses <c>\n</c>, and the currency test
/// normalizes both sides, so the verdict does not depend on how the repository was checked out.
/// </remarks>
public sealed class ProtocolEmitter
{
    private const string DtoNamespace = "Jint.DevTools.Protocol";
    private const string DomainNamespace = "Jint.DevTools.Domains";
    private const string SessionNamespace = "Jint.DevTools.Session";
    private const string Json = "global::System.Text.Json";
    private const string JsonSerialization = "global::System.Text.Json.Serialization";
    private const string Context = "global::Jint.DevTools.Protocol.ProtocolJsonContext";
    private const string ValueTask = "global::System.Threading.Tasks.ValueTask";

    // The protocol's map shape. A type it names but declares no properties for is a JSON object whose keys
    // are data rather than a record -- Network.Headers is the one every generated domain uses -- so it
    // resolves to a dictionary and no empty record is emitted for it. An *inline* "type": "object" member is
    // a different thing and stays a JsonElement: Debugger.paused's data and an execution context's auxData
    // carry values of mixed shapes, and a dictionary of strings would silently drop them.
    private const string MapType = "global::System.Collections.Generic.Dictionary<string, string>";

    private readonly Protocol _protocol;
    private readonly GenerationManifest _manifest;
    private readonly ProtocolPin _pin;
    private readonly HashSet<string> _generated;
    private readonly Dictionary<string, GenerationManifest.GeneratedDomain> _selection;
    private readonly List<SerializableType> _serializable = [];

    // Which of a generated domain's types get a declaration. A domain the manifest names whole gets all of
    // them; one whose entry names only some commands and events gets the closure of what those reach, which
    // is what MarkGenerated walks. The set is what Resolve is held to, so a reference the walk did not reach
    // is a generation failure rather than a reference to a type that was never emitted.
    private readonly Dictionary<string, HashSet<string>> _emitted = new(StringComparer.Ordinal);

    // An array of a generated record needs a declared entry of its own. Without one the serialization
    // generator names the transitively discovered type after the element's SHORT name -- "CallFrameArray"
    // for both Runtime.CallFrame[] and Debugger.CallFrame[] -- and refuses to generate for the second
    // (SYSLIB1031). TypeInfoPropertyName only reaches types the context declares, so the emitter declares
    // every array it uses.
    private readonly SortedDictionary<string, string> _serializableArrays = new(StringComparer.Ordinal);

    private ProtocolEmitter(Protocol protocol, GenerationManifest manifest, ProtocolPin pin)
    {
        _protocol = protocol;
        _manifest = manifest;
        _pin = pin;
        _generated = new HashSet<string>(manifest.GeneratedDomainNames, StringComparer.Ordinal);
        _selection = manifest.GeneratedDomains.ToDictionary(domain => domain.Name, StringComparer.Ordinal);
    }

    /// <summary>
    /// Emits every generated file, keyed by its name inside the output directory.
    /// </summary>
    public static IReadOnlyDictionary<string, string> Emit(string protocolDirectory, string manifestPath)
    {
        var protocol = Protocol.Read(protocolDirectory);
        var manifest = GenerationManifest.Read(manifestPath);
        manifest.Validate(protocol);

        var emitter = new ProtocolEmitter(protocol, manifest, ProtocolPin.Read(Path.Combine(protocolDirectory, "pin.json")));
        return emitter.EmitAll();
    }

    private Dictionary<string, string> EmitAll()
    {
        var files = new Dictionary<string, string>(StringComparer.Ordinal);

        MarkGenerated();

        foreach (var entry in _manifest.GeneratedDomains)
        {
            files[entry.Name + ".g.cs"] = EmitDomain(_protocol.Domain(entry.Name));
        }

        files["ProtocolJsonContext.g.cs"] = EmitJsonContext();
        files["ProtocolManifest.g.cs"] = EmitManifest();
        return files;
    }

    // ---------------------------------------------------------------------------------------------
    // What of a domain is generated.
    // ---------------------------------------------------------------------------------------------

    /// <summary>The commands of <paramref name="domain"/> the manifest generates, in protocol order.</summary>
    private List<ProtocolCommand> CommandsOf(ProtocolDomain domain)
    {
        var selection = _selection[domain.Name];
        return domain.Commands.Where(command => selection.GeneratesCommand(command.Name)).ToList();
    }

    /// <summary>The events of <paramref name="domain"/> the manifest generates, in protocol order.</summary>
    private List<ProtocolEventDefinition> EventsOf(ProtocolDomain domain)
    {
        var selection = _selection[domain.Name];
        return domain.Events.Where(@event => selection.GeneratesEvent(@event.Name)).ToList();
    }

    /// <summary>
    /// Works out which types each generated domain declares: everything, for a domain the manifest names
    /// whole, and otherwise the closure of what its generated commands and events reach.
    /// </summary>
    /// <remarks>
    /// A closure rather than a list in the manifest, because the alternative is a list somebody has to keep
    /// in step with a protocol description they did not write. It runs before anything is emitted, and it
    /// walks references exactly the way <see cref="Resolve"/> follows them, so the two cannot disagree about
    /// what a reference reaches.
    /// </remarks>
    private void MarkGenerated()
    {
        foreach (var entry in _manifest.GeneratedDomains)
        {
            _emitted[entry.Name] = new HashSet<string>(StringComparer.Ordinal);
        }

        foreach (var entry in _manifest.GeneratedDomains)
        {
            var domain = _protocol.Domain(entry.Name);

            if (entry.IsWhole)
            {
                foreach (var type in domain.Types)
                {
                    MarkType(domain.Name, type.Id);
                }
            }

            foreach (var command in CommandsOf(domain))
            {
                MarkAll(command.Parameters, domain.Name);
                MarkAll(command.Returns, domain.Name);
            }

            foreach (var @event in EventsOf(domain))
            {
                MarkAll(@event.Parameters, domain.Name);
            }
        }
    }

    private void MarkAll(IReadOnlyList<ProtocolMember> members, string owningDomain)
    {
        foreach (var member in members)
        {
            MarkValue(member.Value, owningDomain);
        }
    }

    private void MarkValue(ProtocolValue value, string owningDomain)
    {
        if (value.Reference is { } reference)
        {
            var separator = reference.IndexOf('.', StringComparison.Ordinal);
            MarkType(
                separator < 0 ? owningDomain : reference.Substring(0, separator),
                separator < 0 ? reference : reference.Substring(separator + 1));
            return;
        }

        if (value.Items is { } items)
        {
            MarkValue(items, owningDomain);
        }
    }

    private void MarkType(string domainName, string typeName)
    {
        if (!_protocol.HasDomain(domainName))
        {
            // Resolve reports it, with the member that named it.
            return;
        }

        var target = _protocol.Domain(domainName).Types.FirstOrDefault(type => string.Equals(type.Id, typeName, StringComparison.Ordinal));
        if (target is null || target.IsMap)
        {
            // A map resolves to a dictionary and declares nothing; a name the protocol does not have is
            // Resolve's complaint to make.
            return;
        }

        if (!_emitted.TryGetValue(domainName, out var types))
        {
            // A reference into a domain the manifest does not generate. Resolve accepts it only when it is a
            // type alias -- Network.RequestId is a string -- so follow the alias and stop.
            if (!target.IsObject)
            {
                MarkValue(target.Value, domainName);
            }

            return;
        }

        if (!types.Add(typeName))
        {
            return;
        }

        if (target.IsObject)
        {
            MarkAll(target.Properties, domainName);
            return;
        }

        MarkValue(target.Value, domainName);
    }

    // ---------------------------------------------------------------------------------------------
    // One domain: its data transfer objects, its dispatch base, and its event factories.
    // ---------------------------------------------------------------------------------------------

    private string EmitDomain(ProtocolDomain domain)
    {
        var declared = new HashSet<string>(StringComparer.Ordinal);
        var builder = new StringBuilder();
        Header(builder, domain);

        // The data transfer objects go into a builder of their own first: a domain whose entry names only
        // commands that take and return nothing declares no type at all, and an empty namespace block would
        // be all that was left of it.
        var objects = new StringBuilder();
        EmitDataTransferObjects(objects, domain, declared);

        if (objects.Length > 0)
        {
            builder.Append("namespace ").Append(DtoNamespace).Append('.').Append(domain.Name).Append("\n{\n");
            builder.Append(objects);
            builder.Append("}\n\n");
        }

        builder.Append("namespace ").Append(DomainNamespace).Append("\n{\n");
        EmitDomainBase(builder, domain);
        EmitEventFactories(builder, domain, EventsOf(domain));
        builder.Append("}\n");

        return builder.ToString();
    }

    private void EmitDataTransferObjects(StringBuilder builder, ProtocolDomain domain, HashSet<string> declared)
    {
        var first = true;
        var emitted = _emitted[domain.Name];

        foreach (var type in domain.Types)
        {
            if (!emitted.Contains(type.Id))
            {
                // Not reached by anything this domain generates. A domain the manifest names whole marks all
                // of its types, so this only skips something in a domain whose entry names its members.
                continue;
            }

            if (type.Value.Enumeration is { } enumeration)
            {
                Separate(builder, ref first);
                EmitEnumerationConstants(
                    builder,
                    domain,
                    declared,
                    type.Id + "Values",
                    domain.Name + "." + type.Id,
                    enumeration,
                    Naming.Citation(domain, "type-" + type.Id));
                continue;
            }

            if (!type.IsObject)
            {
                continue;
            }

            if (type.IsMap)
            {
                // A map, not a record: it resolves to MapType wherever it is referenced.
                continue;
            }

            Separate(builder, ref first);
            EmitRecord(
                builder,
                domain,
                declared,
                type.Id,
                Naming.Summary(type.Description, string.Create(CultureInfo.InvariantCulture, $"The <c>{domain.Name}.{type.Id}</c> protocol type.")),
                Naming.Citation(domain, "type-" + type.Id),
                type.Experimental,
                type.Deprecated,
                type.Properties);
        }

        foreach (var command in CommandsOf(domain))
        {
            var citation = Naming.Citation(domain, "method-" + command.Name);

            if (command.Parameters.Count > 0)
            {
                Separate(builder, ref first);
                EmitRecord(
                    builder,
                    domain,
                    declared,
                    RequestName(command),
                    string.Create(CultureInfo.InvariantCulture, $"The parameters of the <c>{domain.Name}.{command.Name}</c> command."),
                    citation,
                    command.Experimental,
                    command.Deprecated,
                    command.Parameters);
            }

            if (command.Returns.Count > 0)
            {
                Separate(builder, ref first);
                EmitRecord(
                    builder,
                    domain,
                    declared,
                    ResponseName(command),
                    string.Create(CultureInfo.InvariantCulture, $"The result of the <c>{domain.Name}.{command.Name}</c> command."),
                    citation,
                    command.Experimental,
                    command.Deprecated,
                    command.Returns);
            }
        }

        foreach (var @event in EventsOf(domain))
        {
            if (@event.Parameters.Count == 0)
            {
                continue;
            }

            Separate(builder, ref first);
            EmitRecord(
                builder,
                domain,
                declared,
                EventName(@event),
                string.Create(CultureInfo.InvariantCulture, $"The parameters of the <c>{domain.Name}.{@event.Name}</c> event."),
                Naming.Citation(domain, "event-" + @event.Name),
                @event.Experimental,
                @event.Deprecated,
                @event.Parameters);
        }
    }

    private void EmitRecord(
        StringBuilder builder,
        ProtocolDomain domain,
        HashSet<string> declared,
        string name,
        string summary,
        string citation,
        bool experimental,
        bool deprecated,
        IReadOnlyList<ProtocolMember> members)
    {
        Declare(declared, domain, name);

        Documentation(builder, "    ", summary, citation, experimental, deprecated);
        builder.Append("    internal sealed record ").Append(name).Append("\n    {\n");

        var pending = new List<(string Name, string Subject, IReadOnlyList<string> Values)>();
        var used = new HashSet<string>(StringComparer.Ordinal);
        var first = true;

        foreach (var member in members)
        {
            if (!first)
            {
                builder.Append('\n');
            }

            first = false;

            var memberName = Naming.Pascal(member.Name);
            if (string.Equals(memberName, name, StringComparison.Ordinal))
            {
                // A member may not be named after the type that declares it.
                memberName += "Value";
            }

            if (!used.Add(memberName))
            {
                throw new ProtocolGeneratorException($"'{domain.Name}.{name}' declares two members that both become '{memberName}'.");
            }

            string? enumerationClass = null;
            if (member.Value.Enumeration is { } enumeration && member.Value.Reference is null)
            {
                enumerationClass = name + memberName + "Values";
                pending.Add((enumerationClass, $"{domain.Name}.{name}.{member.Name}", enumeration));
            }

            var type = Resolve(member.Value, domain.Name, string.Create(CultureInfo.InvariantCulture, $"{domain.Name}.{name}.{member.Name}"));
            var declaredType = member.Optional ? type + "?" : type;

            MemberDocumentation(builder, member, enumerationClass);
            builder.Append("        [").Append(JsonSerialization).Append(".JsonPropertyName(\"").Append(member.Name).Append("\")]\n");
            if (member.Optional)
            {
                builder.Append("        [").Append(JsonSerialization).Append(".JsonIgnore(Condition = ").Append(JsonSerialization).Append(".JsonIgnoreCondition.WhenWritingNull)]\n");
            }

            builder.Append("        public ");
            if (!member.Optional)
            {
                builder.Append("required ");
            }

            builder.Append(declaredType).Append(' ').Append(memberName).Append(" { get; init; }\n");
        }

        builder.Append("    }\n");

        foreach (var (enumerationName, subject, values) in pending)
        {
            builder.Append('\n');
            EmitEnumerationConstants(builder, domain, declared, enumerationName, subject, values, citation);
        }

        _serializable.Add(new SerializableType($"global::{DtoNamespace}.{domain.Name}.{name}", domain.Name + name));
    }

    private static void EmitEnumerationConstants(
        StringBuilder builder,
        ProtocolDomain domain,
        HashSet<string> declared,
        string name,
        string subject,
        IReadOnlyList<string> values,
        string citation)
    {
        Declare(declared, domain, name);

        Documentation(
            builder,
            "    ",
            string.Create(CultureInfo.InvariantCulture, $"The strings the protocol admits for <c>{subject}</c>."),
            citation,
            experimental: false,
            deprecated: false);

        builder.Append("    internal static class ").Append(name).Append("\n    {\n");

        var used = new HashSet<string>(StringComparer.Ordinal);
        var first = true;

        foreach (var value in values)
        {
            if (!first)
            {
                builder.Append('\n');
            }

            first = false;

            var constant = Naming.Constant(value);
            if (!used.Add(constant))
            {
                throw new ProtocolGeneratorException($"'{domain.Name}.{name}' has two admissible values that both become '{constant}'.");
            }

            builder.Append("        /// <summary>The protocol's <c>").Append(Naming.Escape(value)).Append("</c> value.</summary>\n");
            builder.Append("        internal const string ").Append(constant).Append(" = \"").Append(value).Append("\";\n");
        }

        builder.Append("    }\n");
    }

    private void EmitDomainBase(StringBuilder builder, ProtocolDomain domain)
    {
        Documentation(
            builder,
            "    ",
            string.Create(CultureInfo.InvariantCulture, $"Dispatches the <c>{domain.Name}</c> domain's commands."),
            Naming.Citation(domain),
            domain.Experimental,
            domain.Deprecated);

        builder.Append("    internal abstract class ").Append(domain.Name).Append("DomainBase : global::").Append(DomainNamespace).Append(".DevToolsDomain\n    {\n");
        builder.Append("        /// <inheritdoc/>\n");
        builder.Append("        internal sealed override string Name => \"").Append(domain.Name).Append("\";\n");

        var generated = CommandsOf(domain);

        foreach (var command in generated)
        {
            var parameters = command.Parameters.Count > 0
                ? $"global::{DtoNamespace}.{domain.Name}.{RequestName(command)}"
                : $"global::{DtoNamespace}.EmptyParameters";
            var result = command.Returns.Count > 0
                ? $"global::{DtoNamespace}.{domain.Name}.{ResponseName(command)}"
                : $"global::{DtoNamespace}.EmptyResult";

            builder.Append('\n');
            CommandDocumentation(builder, domain, command);
            builder
                .Append("        protected virtual ").Append(ValueTask).Append('<').Append(result).Append("> ")
                .Append(Naming.Pascal(command.Name)).Append("Async(").Append(parameters).Append(" parameters, global::")
                .Append(SessionNamespace).Append(".CommandContext context)\n")
                .Append("            => global::Jint.DevTools.Throw.MethodNotFound<").Append(ValueTask).Append('<').Append(result)
                .Append(">>(\"").Append(domain.Name).Append('.').Append(command.Name).Append("\");\n");
        }

        var answered = generated
            .Where(command => _manifest.ImplementedMethods.Contains(domain.Name + "." + command.Name, StringComparer.Ordinal))
            .ToList();

        builder.Append('\n');
        builder.Append("        /// <inheritdoc/>\n");

        if (answered.Count == 0)
        {
            builder
                .Append("        internal sealed override ").Append(ValueTask).Append("<string> DispatchAsync(string method, ").Append(Json)
                .Append(".JsonElement? parameters, global::").Append(SessionNamespace).Append(".CommandContext context)\n")
                .Append("            => global::Jint.DevTools.Throw.MethodNotFound<").Append(ValueTask).Append("<string>>(\"").Append(domain.Name).Append(".\" + method);\n")
                .Append("    }\n");
            return;
        }

        builder
            .Append("        internal sealed override async ").Append(ValueTask).Append("<string> DispatchAsync(string method, ").Append(Json)
            .Append(".JsonElement? parameters, global::").Append(SessionNamespace).Append(".CommandContext context)\n")
            .Append("        {\n")
            .Append("            switch (method)\n            {\n");

        foreach (var command in answered)
        {
            var parametersProperty = command.Parameters.Count > 0 ? domain.Name + RequestName(command) : "EmptyParameters";
            var resultProperty = command.Returns.Count > 0 ? domain.Name + ResponseName(command) : "EmptyResult";

            builder
                .Append("                case \"").Append(command.Name).Append("\":\n")
                .Append("                {\n")
                .Append("                    var result = await ").Append(Naming.Pascal(command.Name)).Append("Async(global::")
                .Append(DtoNamespace).Append(".ProtocolPayload.Read(parameters, ").Append(Context).Append(".Default.").Append(parametersProperty)
                .Append("), context).ConfigureAwait(false);\n")
                .Append("                    return ").Append(Json).Append(".JsonSerializer.Serialize(result, ").Append(Context).Append(".Default.")
                .Append(resultProperty).Append(");\n")
                .Append("                }\n\n");
        }

        builder
            .Append("                // A command manifest.json does not list is method-not-found BEFORE its parameters\n")
            .Append("                // are looked at, which is the order Chrome answers in: a command a backend does not\n")
            .Append("                // implement is not in its dispatch table at all, so its payload is never read.\n")
            .Append("                default:\n")
            .Append("                    return global::Jint.DevTools.Throw.MethodNotFound<string>(\"").Append(domain.Name).Append(".\" + method);\n")
            .Append("            }\n")
            .Append("        }\n")
            .Append("    }\n");
    }

    private static void EmitEventFactories(StringBuilder builder, ProtocolDomain domain, List<ProtocolEventDefinition> events)
    {
        if (events.Count == 0)
        {
            return;
        }

        builder.Append('\n');
        Documentation(
            builder,
            "    ",
            string.Create(CultureInfo.InvariantCulture, $"Builds the <c>{domain.Name}</c> domain's events."),
            Naming.Citation(domain),
            experimental: false,
            deprecated: false);

        builder.Append("    internal static class ").Append(domain.Name).Append("Events\n    {\n");

        var first = true;
        foreach (var @event in events)
        {
            if (!first)
            {
                builder.Append('\n');
            }

            first = false;

            Documentation(
                builder,
                "        ",
                string.Create(CultureInfo.InvariantCulture, $"Builds the <c>{domain.Name}.{@event.Name}</c> event."),
                Naming.Citation(domain, "event-" + @event.Name),
                @event.Experimental,
                @event.Deprecated);

            builder
                .Append("        internal static global::").Append(DtoNamespace).Append(".ProtocolEvent ").Append(Naming.Pascal(@event.Name)).Append('(');

            if (@event.Parameters.Count == 0)
            {
                builder
                    .Append(")\n            => new(\"").Append(domain.Name).Append('.').Append(@event.Name).Append("\", \"{}\");\n");
                continue;
            }

            builder
                .Append("global::").Append(DtoNamespace).Append('.').Append(domain.Name).Append('.').Append(EventName(@event)).Append(" parameters)\n")
                .Append("            => new(\"").Append(domain.Name).Append('.').Append(@event.Name).Append("\", ").Append(Json)
                .Append(".JsonSerializer.Serialize(parameters, ").Append(Context).Append(".Default.").Append(domain.Name).Append(EventName(@event)).Append("));\n");
        }

        builder.Append("    }\n");
    }

    // ---------------------------------------------------------------------------------------------
    // The serialization context and the manifest.
    // ---------------------------------------------------------------------------------------------

    private string EmitJsonContext()
    {
        var builder = new StringBuilder();
        Header(builder);

        builder.Append("namespace ").Append(DtoNamespace).Append("\n{\n");
        Documentation(
            builder,
            "    ",
            "Serializes and deserializes every protocol payload without reflection.",
            citation: null,
            experimental: false,
            deprecated: false);

        builder
            .Append("    [").Append(JsonSerialization).Append(".JsonSourceGenerationOptions(\n")
            .Append("        PropertyNamingPolicy = ").Append(JsonSerialization).Append(".JsonKnownNamingPolicy.CamelCase,\n")
            .Append("        DefaultIgnoreCondition = ").Append(JsonSerialization).Append(".JsonIgnoreCondition.WhenWritingNull)]\n");

        builder
            .Append("    [").Append(JsonSerialization).Append(".JsonSerializable(typeof(global::").Append(DtoNamespace).Append(".EmptyParameters), TypeInfoPropertyName = \"EmptyParameters\")]\n")
            .Append("    [").Append(JsonSerialization).Append(".JsonSerializable(typeof(global::").Append(DtoNamespace).Append(".EmptyResult), TypeInfoPropertyName = \"EmptyResult\")]\n");

        foreach (var type in _serializable)
        {
            builder
                .Append("    [").Append(JsonSerialization).Append(".JsonSerializable(typeof(").Append(type.ClrName)
                .Append("), TypeInfoPropertyName = \"").Append(type.ContextProperty).Append("\")]\n");
        }

        foreach (var (property, clrName) in _serializableArrays)
        {
            builder
                .Append("    [").Append(JsonSerialization).Append(".JsonSerializable(typeof(").Append(clrName)
                .Append("), TypeInfoPropertyName = \"").Append(property).Append("\")]\n");
        }

        builder
            .Append("    internal sealed partial class ProtocolJsonContext : ").Append(JsonSerialization).Append(".JsonSerializerContext\n")
            .Append("    {\n    }\n")
            .Append("}\n");

        return builder.ToString();
    }

    private string EmitManifest()
    {
        var builder = new StringBuilder();
        Header(builder);

        builder.Append("namespace ").Append(DtoNamespace).Append("\n{\n");
        Documentation(
            builder,
            "    ",
            "What Jint.DevTools generates from, and what it answers.",
            citation: null,
            experimental: false,
            deprecated: false);

        builder.Append("    internal static class ProtocolManifest\n    {\n");
        builder.Append("        /// <summary>The protocol version the pinned description declares.</summary>\n");
        builder.Append("        internal const string ProtocolVersion = \"").Append(_protocol.Version).Append("\";\n\n");
        builder.Append("        /// <summary>The commit of ChromeDevTools/devtools-protocol the description was vendored from.</summary>\n");
        builder.Append("        internal const string PinnedCommit = \"").Append(_pin.Commit).Append("\";\n\n");
        builder.Append("        /// <summary>The npm version of <c>devtools-protocol</c> that commit publishes.</summary>\n");
        builder.Append("        internal const string PinnedNpmVersion = \"").Append(_pin.NpmVersion).Append("\";\n\n");

        StringTable(builder, "GeneratedDomains", "The domains this assembly has data transfer objects and a dispatch base for.", _manifest.GeneratedDomainNames);
        builder.Append('\n');
        StringTable(builder, "ImplementedMethods", "The commands this assembly answers; every other one is method-not-found.", _manifest.ImplementedMethods);
        builder.Append('\n');
        StringTable(builder, "ImplementedEvents", "The events this assembly emits.", _manifest.ImplementedEvents);
        builder.Append('\n');

        builder.Append("        /// <summary>What <c>Schema.getDomains</c> answers.</summary>\n");
        builder.Append("        internal static global::System.Collections.Generic.IReadOnlyList<global::").Append(DtoNamespace).Append(".Schema.Domain> ReportedDomains { get; } =\n        [\n");
        foreach (var reported in _manifest.ReportedDomains)
        {
            builder
                .Append("            new global::").Append(DtoNamespace).Append(".Schema.Domain { Name = \"").Append(reported.Name)
                .Append("\", Version = \"").Append(reported.Version).Append("\" },\n");
        }

        builder.Append("        ];\n    }\n}\n");
        return builder.ToString();
    }

    private static void StringTable(StringBuilder builder, string name, string summary, IReadOnlyList<string> values)
    {
        builder.Append("        /// <summary>").Append(summary).Append("</summary>\n");
        builder.Append("        internal static global::System.Collections.Generic.IReadOnlyList<string> ").Append(name).Append(" { get; } =\n        [\n");
        foreach (var value in values)
        {
            builder.Append("            \"").Append(value).Append("\",\n");
        }

        builder.Append("        ];\n");
    }

    // ---------------------------------------------------------------------------------------------
    // Type resolution.
    // ---------------------------------------------------------------------------------------------

    private string Resolve(ProtocolValue value, string owningDomain, string where)
    {
        if (value.Reference is { } reference)
        {
            var separator = reference.IndexOf('.', StringComparison.Ordinal);
            var domainName = separator < 0 ? owningDomain : reference.Substring(0, separator);
            var typeName = separator < 0 ? reference : reference.Substring(separator + 1);

            var target = _protocol.Domain(domainName).Types.FirstOrDefault(type => string.Equals(type.Id, typeName, StringComparison.Ordinal))
                ?? throw new ProtocolGeneratorException($"'{where}' refers to '{domainName}.{typeName}', which the vendored protocol does not declare.");

            if (!target.IsObject)
            {
                // A type alias: the protocol names it, but it is a string, a number or an array of
                // something, so the C# side is that shape and the name is documentation.
                return Resolve(target.Value, domainName, where);
            }

            if (target.IsMap)
            {
                // Declared as an object and given no properties: the protocol's own way of writing a map of
                // strings, which is what Network.Headers is. It needs no domain to be generated, because
                // nothing of the domain is referenced -- only the shape.
                return MapType;
            }

            if (!_generated.Contains(domainName))
            {
                throw new ProtocolGeneratorException(
                    $"'{where}' refers to '{domainName}.{typeName}', an object type of a domain manifest.json does not generate. " +
                    "Add the domain to generatedDomains, or the reference has nothing to resolve to.");
            }

            if (!_emitted[domainName].Contains(typeName))
            {
                throw new ProtocolGeneratorException(
                    $"'{where}' refers to '{domainName}.{typeName}', which nothing the manifest generates reaches. " +
                    "MarkGenerated and Resolve disagree about what a reference reaches, which is a bug in the emitter.");
            }

            return $"global::{DtoNamespace}.{domainName}.{typeName}";
        }

        return value.Kind switch
        {
            "string" => "string",
            "integer" => "int",
            "number" => "double",
            "boolean" => "bool",

            // The protocol carries binary as base64 in JSON, so a string is what crosses the wire.
            "binary" => "string",

            // "any", and "object" with no declared properties, are both an arbitrary JSON value.
            "any" or "object" => $"{Json}.JsonElement",

            "array" => ArrayOf(Resolve(
                value.Items ?? throw new ProtocolGeneratorException($"'{where}' is an array with no items."),
                owningDomain,
                where)),

            _ => throw new ProtocolGeneratorException($"'{where}' has protocol type '{value.Kind}', which the emitter does not know how to write."),
        };
    }

    /// <summary>
    /// The array of an element type, registering it with the serialization context when the element is a
    /// generated record.
    /// </summary>
    private string ArrayOf(string element)
    {
        const string Prefix = "global::" + DtoNamespace + ".";

        if (element.StartsWith(Prefix, StringComparison.Ordinal))
        {
            var qualified = element.Substring(Prefix.Length).Replace(".", "", StringComparison.Ordinal) + "Array";
            _serializableArrays[qualified] = element + "[]";
        }

        return element + "[]";
    }

    // ---------------------------------------------------------------------------------------------
    // Shared writing.
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// The header of a domain's file, which names the description file <i>that domain</i> was read from.
    /// </summary>
    /// <remarks>
    /// <c>Jint.g.cs</c> is why this takes a domain at all: the <c>Jint</c> domain is described by
    /// <c>jint_protocol.json</c>, which is this repository's own, and its header used to cite the Chrome
    /// commit its twenty-one neighbours come from - provenance that named the wrong document, which is item
    /// three of <see href="https://github.com/sebastienros/jint/issues/3683">#3683</see>.
    /// </remarks>
    private void Header(StringBuilder builder, ProtocolDomain domain)
    {
        var source = domain.Vendored
            ? ProtocolPath(domain.SourceFile)
            : ProtocolPath(domain.SourceFile) + " - this repository's own domain, not Chrome's";

        Header(builder, source, domain.Vendored, domain.Name + " entries, sha256:" + _manifest.DigestOf(domain.Name));
    }

    /// <summary>
    /// The header of a file generated from all three descriptions and the whole manifest.
    /// </summary>
    private void Header(StringBuilder builder)
    {
        Header(
            builder,
            ProtocolPath(string.Join(", ", Protocol.Files)),
            vendored: true,
            "whole file, sha256:" + _manifest.Digest);
    }

    /// <summary>
    /// Writes the provenance every generated file carries: the description it was read from, the pin that
    /// description is vendored at, and the part of the manifest that shaped it.
    /// </summary>
    /// <remarks>
    /// The three lines are what make a file that is checked in and out of date <i>diagnosable</i> rather than
    /// merely wrong. <c>GeneratedProtocolIsCurrentTests</c> catches a stale file in a build; a reader with a
    /// diff in front of them has only what the file says about itself.
    /// </remarks>
    private void Header(StringBuilder builder, string source, bool vendored, string manifestScope)
    {
        builder
            .Append("// <auto-generated/>\n")
            .Append("//------------------------------------------------------------------------------\n")
            .Append("// Emitted by tools/devtools-protocol/Jint.DevTools.ProtocolGenerator.\n")
            .Append("//\n")
            .Append("//     source:   ").Append(source).Append('\n')
            .Append("//     protocol: version ").Append(_protocol.Version);

        if (vendored)
        {
            builder
                .Append(", ChromeDevTools/devtools-protocol@").Append(_pin.Commit)
                .Append(" (devtools-protocol@").Append(_pin.NpmVersion).Append(')');
        }

        builder
            .Append('\n')
            .Append("//     manifest: ").Append(ProtocolPath("manifest.json")).Append(", ").Append(manifestScope).Append('\n')
            .Append("//\n")
            .Append("// Do not edit. Regenerate instead, and read the diff: it is the upstream change stated in the\n")
            .Append("// vocabulary this repository compiles. tools/devtools-protocol/README.md has the command, and\n")
            .Append("// Jint.Tests.DevTools/Protocol/GeneratedProtocolIsCurrentTests.cs fails when this file drifts.\n")
            .Append("//------------------------------------------------------------------------------\n")
            .Append("#nullable enable\n\n");
    }

    private static string ProtocolPath(string file) => "tools/devtools-protocol/" + file;

    private static void Documentation(StringBuilder builder, string indent, string summary, string? citation, bool experimental, bool deprecated)
    {
        builder.Append(indent).Append("/// <summary>").Append(summary).Append("</summary>\n");
        builder.Append(indent).Append("/// <remarks>");

        if (experimental)
        {
            builder.Append("Experimental in the protocol. ");
        }

        if (deprecated)
        {
            builder.Append("Deprecated in the protocol. ");
        }

        builder.Append("Generated from the pinned protocol; do not edit.");

        if (citation is not null)
        {
            builder.Append(" See <see href=\"").Append(citation).Append("\"/>.");
        }

        builder.Append("</remarks>\n");
    }

    private static void MemberDocumentation(StringBuilder builder, ProtocolMember member, string? enumerationClass)
    {
        var summary = Naming.Summary(
            member.Description,
            string.Create(CultureInfo.InvariantCulture, $"The protocol's <c>{member.Name}</c> member."));

        builder.Append("        /// <summary>").Append(summary).Append("</summary>\n");

        if (!member.Experimental && !member.Deprecated && enumerationClass is null)
        {
            return;
        }

        builder.Append("        /// <remarks>");
        if (member.Experimental)
        {
            builder.Append("Experimental in the protocol. ");
        }

        if (member.Deprecated)
        {
            builder.Append("Deprecated in the protocol. ");
        }

        if (enumerationClass is not null)
        {
            builder.Append("The protocol admits the constants of <see cref=\"").Append(enumerationClass).Append("\"/>.");
        }

        builder.Append("</remarks>\n");
    }

    private static void CommandDocumentation(StringBuilder builder, ProtocolDomain domain, ProtocolCommand command)
    {
        var summary = Naming.Summary(
            command.Description,
            string.Create(CultureInfo.InvariantCulture, $"Answers the <c>{domain.Name}.{command.Name}</c> command."));

        builder.Append("        /// <summary>").Append(summary).Append("</summary>\n");
        builder.Append("        /// <remarks>");

        if (command.Experimental)
        {
            builder.Append("Experimental in the protocol. ");
        }

        if (command.Deprecated)
        {
            builder.Append("Deprecated in the protocol. ");
        }

        if (command.Redirect is { } redirect)
        {
            builder.Append("The protocol redirects this command to the <c>").Append(redirect).Append("</c> domain. ");
        }

        builder
            .Append("Reached only while manifest.json lists it. See <see href=\"")
            .Append(Naming.Citation(domain, "method-" + command.Name)).Append("\"/>.</remarks>\n");
    }

    private static void Separate(StringBuilder builder, ref bool first)
    {
        if (!first)
        {
            builder.Append('\n');
        }

        first = false;
    }

    private static void Declare(HashSet<string> declared, ProtocolDomain domain, string name)
    {
        if (!declared.Add(name))
        {
            throw new ProtocolGeneratorException($"The '{domain.Name}' domain would declare '{name}' twice. Two protocol members collide once their C# names are formed.");
        }
    }

    private static string RequestName(ProtocolCommand command) => Naming.Pascal(command.Name) + "Request";

    private static string ResponseName(ProtocolCommand command) => Naming.Pascal(command.Name) + "Response";

    private static string EventName(ProtocolEventDefinition @event) => Naming.Pascal(@event.Name) + "Event";

    private readonly record struct SerializableType(string ClrName, string ContextProperty);
}

/// <summary>
/// <c>tools/devtools-protocol/pin.json</c>: which commit of the protocol description is vendored.
/// </summary>
public sealed record ProtocolPin(string Commit, string NpmVersion)
{
    public static ProtocolPin Read(string path)
    {
        if (!File.Exists(path))
        {
            throw new ProtocolGeneratorException($"'{path}' does not exist, so the emitted code could not say which protocol commit it came from.");
        }

        using var document = JsonDocument.Parse(File.ReadAllBytes(path));
        return new ProtocolPin(
            document.RootElement.GetProperty("commit").GetString()!,
            document.RootElement.GetProperty("npmVersion").GetString()!);
    }
}
