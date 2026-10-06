using System.Text.Json;
using System.Text.Encodings.Web;

namespace Jint.Browser.BindingGenerator;

/// <summary>The checked-in interface and member contract consumed by the binding emitter.</summary>
internal sealed class BindingContract
{
    private static readonly JsonSerializerOptions SerializationOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public int Version { get; set; } = 1;
    public List<ContractInterface> Interfaces { get; set; } = [];
    public List<ContractEnum> StringEnums { get; set; } = [];
    public List<string> HtmlCollectionElements { get; set; } = [];
    public string HtmlCollectionOpenType { get; set; } = "global::Jint.Browser.Dom.Collections.DomHtmlCollection";
    public string HtmlCollectionDefaultElement { get; set; } = "global::Jint.HtmlParser.Element";
    public List<string> ExtensionNamespaces { get; set; } = [];
    public List<SkipRecord> Skipped { get; set; } = [];
    public List<ReflectedModel> Reflected { get; set; } = [];
    public List<string> Diagnostics { get; set; } = [];

    public static BindingContract Load(string path)
    {
        var contract = JsonSerializer.Deserialize<BindingContract>(File.ReadAllText(path))
            ?? throw new InvalidDataException("The DOM binding contract is empty.");
        if (contract.Version != 1)
        {
            throw new InvalidDataException($"Unsupported DOM binding contract version {contract.Version}.");
        }

        return contract;
    }

    public void Save(string path)
    {
        File.WriteAllText(path, JsonSerializer.Serialize(this, SerializationOptions) + "\n");
    }

    public static BindingContract FromModel(BindingModel model)
    {
        var result = new BindingContract();
        foreach (var source in model.Interfaces)
        {
            var target = new ContractInterface
            {
                DomName = source.DomName,
                ClrTypeName = source.ClrTypeName,
                ReceiverType = source.ReceiverType,
                TypeMapCandidate = source.TypeMapCandidate,
                Parent = source.Parent?.DomName,
                RootsAtEventTarget = source.RootsAtEventTarget,
                HasInterfaceObject = source.HasInterfaceObject,
                Kind = source.Kind,
                FieldName = source.FieldName,
                Group = source.Group,
                ManualShape = source.ManualShape,
                ShapeAdditions = source.ShapeAdditions,
                AccessorClass = source.AccessorClass,
                AccessorReference = source.AccessorReference,
            };
            target.Members.AddRange(source.Members.Select(m => new ContractMember
            {
                DomName = m.DomName,
                Kind = m.Kind,
                Body = m.Body,
                SetterBody = m.SetterBody,
                Length = m.Length,
                Origin = m.Origin,
            }));
            target.Constants.AddRange(source.Constants);
            target.Unscopables.AddRange(source.Unscopables);
            result.Interfaces.Add(target);
        }

        foreach (var source in model.StringEnums.Values.OrderBy(e => e.ClrFullName, StringComparer.Ordinal))
        {
            result.StringEnums.Add(new ContractEnum
            {
                ClrFullName = source.ClrFullName,
                HelperName = source.HelperName,
                Values = source.Values.Select(v => new ContractEnumValue(v.FieldName, v.Literal)).ToList(),
            });
        }

        result.HtmlCollectionElements.AddRange(model.HtmlCollectionElements);
        result.HtmlCollectionOpenType = model.HtmlCollectionOpenType;
        result.HtmlCollectionDefaultElement = model.HtmlCollectionDefaultElement;
        result.ExtensionNamespaces.AddRange(model.ExtensionNamespaces);
        result.Skipped.AddRange(model.Skipped);
        result.Reflected.AddRange(model.Reflected);
        result.Diagnostics.AddRange(model.Diagnostics);
        return result;
    }

    public BindingModel ToModel()
    {
        var model = new BindingModel();
        var byName = new Dictionary<string, InterfaceModel>(StringComparer.Ordinal);
        var seenFields = new HashSet<string>(StringComparer.Ordinal);
        foreach (var source in Interfaces)
        {
            if (byName.ContainsKey(source.DomName))
            {
                throw new InvalidDataException($"Duplicate DOM interface {source.DomName}.");
            }

            if (!seenFields.Add(source.FieldName))
            {
                throw new InvalidDataException($"Duplicate generated interface field {source.FieldName}.");
            }

            // Registry initializers use the parent's static field. Enforce the order here so a malformed
            // contract cannot silently install a null parent or make the type-map depth walk cyclic.
            if (source.Parent is not null && !byName.ContainsKey(source.Parent))
            {
                throw new InvalidDataException($"{source.DomName} must follow its known parent {source.Parent}.");
            }

            var memberNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (var member in source.Members)
            {
                if (!memberNames.Add(member.DomName))
                {
                    throw new InvalidDataException($"Duplicate DOM member {source.DomName}.{member.DomName}.");
                }
            }

            var target = new InterfaceModel
            {
                DomName = source.DomName,
                ClrTypeName = source.ClrTypeName,
                ReceiverType = source.ReceiverType,
                TypeMapCandidate = source.TypeMapCandidate,
                RootsAtEventTarget = source.RootsAtEventTarget,
                HasInterfaceObject = source.HasInterfaceObject,
                Kind = source.Kind,
                FieldName = source.FieldName,
                Group = source.Group,
                ManualShape = source.ManualShape,
                ShapeAdditions = source.ShapeAdditions,
                AccessorClass = source.AccessorClass,
                AccessorReference = source.AccessorReference,
            };
            target.Members.AddRange(source.Members.Select(m => new MemberModel
            {
                DomName = m.DomName,
                Kind = m.Kind,
                Body = m.Body,
                SetterBody = m.SetterBody,
                Length = m.Length,
                Origin = m.Origin,
            }));
            target.Constants.AddRange(source.Constants);
            target.Unscopables.AddRange(source.Unscopables);
            model.Interfaces.Add(target);
            byName.Add(target.DomName, target);
        }

        foreach (var source in Interfaces)
        {
            if (source.Parent is not null)
            {
                byName[source.DomName].Parent = byName[source.Parent];
            }
        }

        foreach (var source in StringEnums)
        {
            model.StringEnums.Add(source.ClrFullName, new EnumModel
            {
                ClrFullName = source.ClrFullName,
                HelperName = source.HelperName,
                Values = source.Values.Select(v => (v.FieldName, v.Literal)).ToList(),
            });
        }

        model.HtmlCollectionElements.UnionWith(HtmlCollectionElements);
        model.HtmlCollectionOpenType = HtmlCollectionOpenType;
        model.HtmlCollectionDefaultElement = HtmlCollectionDefaultElement;
        model.ExtensionNamespaces.UnionWith(ExtensionNamespaces);
        model.Skipped.AddRange(Skipped);
        model.Reflected.AddRange(Reflected);
        model.Diagnostics.AddRange(Diagnostics);
        return model;
    }
}

internal sealed class ContractInterface
{
    public required string DomName { get; set; }
    public required string ClrTypeName { get; set; }
    public required string ReceiverType { get; set; }
    public bool TypeMapCandidate { get; set; } = true;
    public string? Parent { get; set; }
    public bool RootsAtEventTarget { get; set; }
    public bool HasInterfaceObject { get; set; }
    public WrapperKind Kind { get; set; }
    public required string FieldName { get; set; }
    public required string Group { get; set; }
    public string? ManualShape { get; set; }
    public string? ShapeAdditions { get; set; }
    public string? AccessorClass { get; set; }
    public string? AccessorReference { get; set; }
    public List<ContractMember> Members { get; set; } = [];
    public List<ConstantModel> Constants { get; set; } = [];
    public List<string> Unscopables { get; set; } = [];
}

internal sealed class ContractMember
{
    public required string DomName { get; set; }
    public MemberKind Kind { get; set; }
    public required string Body { get; set; }
    public string? SetterBody { get; set; }
    public int Length { get; set; }
    public required string Origin { get; set; }
}

internal sealed class ContractEnum
{
    public required string ClrFullName { get; set; }
    public required string HelperName { get; set; }
    public List<ContractEnumValue> Values { get; set; } = [];
}

internal sealed record ContractEnumValue(string FieldName, string Literal);
