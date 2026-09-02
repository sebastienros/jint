using System.Reflection;
using System.Text;
using static Jint.Browser.BindingGenerator.Inventory;

namespace Jint.Browser.BindingGenerator;

/// <summary>
/// Turns the two pinned assemblies into the model the emitter writes: which interfaces exist, which members
/// each one owns, and how every one of them crosses the boundary.
/// </summary>
internal sealed class ModelBuilder
{
    private readonly Assembly[] _assemblies;
    private readonly Overrides _overrides;
    private readonly BindingModel _model = new();

    private readonly Dictionary<string, InterfaceModel> _byClrName = new(StringComparer.Ordinal);
    private readonly HashSet<string> _excluded = new(StringComparer.Ordinal);
    private readonly HashSet<string> _closureExcluded = new(StringComparer.Ordinal);
    private readonly HashSet<string> _mixins = new(StringComparer.Ordinal);
    private readonly HashSet<string> _stringEnums = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<MethodInfo>> _extensions = new(StringComparer.Ordinal);
    private Conversions _conversions = null!;

    internal ModelBuilder(Assembly[] assemblies, Overrides overrides)
    {
        _assemblies = assemblies;
        _overrides = overrides;
    }

    internal BindingModel Build()
    {
        foreach (var assembly in _assemblies)
        {
            _model.AttributeCounts[assembly.GetName().Name!] = CountAttributes(assembly);
        }

        var interfaces = AllInterfaces(_assemblies);
        var named = interfaces.Where(t => Has(t, DomName)).ToList();

        // EventTarget is Jint's, not AngleSharp's: the engine already has the interface, the brand and the
        // listener list, and a second EventTarget would be a second brand. Every node chain roots at the
        // engine's EventTarget.prototype instead, which is what makes `document instanceof EventTarget` hold.
        _excluded.Add("AngleSharp.Dom.IEventTarget");

        foreach (var entry in _overrides.ExcludedInterfaces)
        {
            _excluded.Add(entry.Interface);
        }

        ClassifyMixins(named);
        ClassifyStringEnums();
        CollectExtensionMembers();

        var primaries = named
            .Where(t => !_mixins.Contains(t.FullName!) && !_excluded.Contains(t.FullName!))
            .OrderBy(t => t.FullName, StringComparer.Ordinal)
            .ToList();

        foreach (var type in primaries)
        {
            var domName = FirstDomName(type)!;
            _byClrName[type.FullName!] = new InterfaceModel
            {
                DomName = domName,
                ClrType = type,
                FieldName = CSharpNames.Identifier(domName),
                Group = GroupOf(type),
                HasInterfaceObject = !Has(type, DomNoInterfaceObject),
                ManualShape = _overrides.Manual.FirstOrDefault(m => m.Interface == type.FullName)?.Shape,

                // Keyed on the DOM name, the way `skip` and `hooks` are, because an addition is a decision
                // about the interface script sees rather than about the CLR type it is projected from.
                // The extend form of `additions`, keyed on the DOM name the way `skip` and `hooks` are: an
                // addition is a decision about the interface script sees rather than about the CLR type it is
                // projected from. The member form is appended in BuildMembers instead.
                ShapeAdditions = _overrides.Additions.FirstOrDefault(a => a.IsExtend && a.Interface == domName)?.Extend,
            };

            if (_overrides.Manual.Any(m => m.Interface == type.FullName))
            {
                // A manual interface contributes no members to any closure: its hand-written shape owns them,
                // and every interface below it inherits them through the prototype chain, which is where a
                // browser has them too.
                _closureExcluded.Add(type.FullName!);
            }
        }

        _conversions = new Conversions(_model, LookupInterface, t => _stringEnums.Contains(t.FullName!));

        foreach (var model in _byClrName.Values)
        {
            model.Parent = FindParent(model.ClrType);
            model.RootsAtEventTarget = model.ClrType.GetInterfaces().Any(i => i.FullName == "AngleSharp.Dom.IEventTarget");
            model.Kind = KindOf(model.ClrType);
        }

        foreach (var model in _byClrName.Values.OrderBy(m => m.DomName, StringComparer.Ordinal))
        {
            BuildMembers(model);
            BuildAccessor(model);
        }

        BuildConstants();

        _model.Interfaces.AddRange(TopologicalOrder());
        VerifyOverridesMatchTheAssemblies();
        return _model;
    }

    private List<InterfaceModel> TopologicalOrder()
    {
        var emitted = new HashSet<string>(StringComparer.Ordinal);
        var ordered = new List<InterfaceModel>();

        void Emit(InterfaceModel model)
        {
            if (!emitted.Add(model.DomName))
            {
                return;
            }

            if (model.Parent is { } parent)
            {
                Emit(parent);
            }

            ordered.Add(model);
        }

        foreach (var model in _byClrName.Values.OrderBy(m => m.DomName, StringComparer.Ordinal))
        {
            Emit(model);
        }

        return ordered;
    }

    // ---------------------------------------------------------------------------------------------------
    // Classification
    // ---------------------------------------------------------------------------------------------------

    /// <summary>
    /// Which interfaces are WebIDL <c>includes</c> mixins rather than interfaces in their own right.
    /// </summary>
    /// <remarks>
    /// <para>
    /// AngleSharp records two shapes and neither is labelled as a mixin. The first is a <c>[DomName]</c>
    /// interface carrying <c>[DomNoInterfaceObject]</c>, having no <c>[DomName]</c> base of its own and being
    /// extended by at least one other <c>[DomName]</c> interface: <c>ParentNode</c>, <c>ChildNode</c>,
    /// <c>GlobalEventHandlers</c>, <c>NavigatorID</c>. The three conditions together are what separate a mixin
    /// from an interface that merely has no constructor property — <c>CSSGroupingRule</c> carries the same
    /// attribute and <em>does</em> have a base, so it stays a real link in the chain, and
    /// <c>CaretPosition</c> carries it with neither a base nor an includer, so it stays a standalone
    /// interface that simply cannot be named.
    /// </para>
    /// <para>
    /// The second shape has no attribute at all: a plain CLR interface with no <c>[DomName]</c> of its own but
    /// with <c>[DomName]</c> members — <c>IValidation</c>, <c>ILoadableElement</c>, <c>IMediaController</c>.
    /// Those are mixins too, and their members reach every including interface through the closure below
    /// without needing to be listed here.
    /// </para>
    /// </remarks>
    private void ClassifyMixins(List<Type> named)
    {
        var namedSet = named.Select(t => t.FullName!).ToHashSet(StringComparer.Ordinal);

        var extendedByADomInterface = new HashSet<string>(StringComparer.Ordinal);
        foreach (var type in named)
        {
            foreach (var baseInterface in type.GetInterfaces())
            {
                extendedByADomInterface.Add(baseInterface.FullName!);
            }
        }

        foreach (var type in named)
        {
            if (!Has(type, DomNoInterfaceObject))
            {
                continue;
            }

            var hasNamedBase = type.GetInterfaces().Any(i => namedSet.Contains(i.FullName!) && i.FullName != "AngleSharp.Dom.IEventTarget");
            if (!hasNamedBase && extendedByADomInterface.Contains(type.FullName!))
            {
                _mixins.Add(type.FullName!);
            }
        }
    }

    /// <summary>
    /// Which CLR enums cross as WebIDL string enumerations and which as numeric constants.
    /// </summary>
    /// <remarks>
    /// AngleSharp marks a few with <c>[DomLiterals]</c> and leaves the rest to be told apart by their field
    /// names: a <c>[DomName]</c> that is <c>SCREAMING_SNAKE_CASE</c> is a WebIDL <em>constant</em> name and
    /// the enum is numeric (<c>Node.ELEMENT_NODE</c>, <c>CSSRule.STYLE_RULE</c>), while a lower-case one is a
    /// string enumeration value (<c>"open"</c>, <c>"beforebegin"</c>). The heuristic is deliberate rather than
    /// exhaustive, and <c>overrides.json</c>'s <c>stringEnums</c> is where a wrong answer is corrected — which
    /// is also where the enums AngleSharp forgot to mark are named.
    /// </remarks>
    private void ClassifyStringEnums()
    {
        var forced = _overrides.StringEnums.Select(e => e.Enum).ToHashSet(StringComparer.Ordinal);

        foreach (var assembly in _assemblies)
        {
            foreach (var type in assembly.GetTypes().Where(t => t.IsEnum))
            {
                var fields = type.GetFields(BindingFlags.Public | BindingFlags.Static);
                var values = new List<(string, string)>();
                var anyLowercase = false;

                foreach (var field in fields)
                {
                    var literal = FirstDomName(field);
                    if (literal is null)
                    {
                        continue;
                    }

                    values.Add((field.Name, literal));
                    anyLowercase |= literal.Any(char.IsLower);
                }

                if (values.Count == 0)
                {
                    continue;
                }

                if (!Has(type, DomLiterals) && !anyLowercase && !forced.Contains(type.FullName!))
                {
                    continue;
                }

                _stringEnums.Add(type.FullName!);
                _model.StringEnums[type.FullName!] = new EnumModel
                {
                    ClrFullName = CSharpNames.Render(type),
                    HelperName = type.Name,
                    Values = values,
                };
            }
        }
    }

    /// <summary>
    /// AngleSharp puts some interface members on <b>extension methods</b> instead of on the interface: every
    /// one of <c>CSSStyleDeclaration</c>'s two hundred-odd CSS property attributes lives on
    /// <c>StyleDeclarationExtensions</c>, and <c>element.style</c> itself lives on
    /// <c>ElementCssInlineStyleExtensions</c>. A static method carrying <c>[DomName]</c> and
    /// <c>[DomAccessor]</c> whose first parameter is a <c>[DomName]</c> interface is a member of that
    /// interface, and this is what collects them.
    /// </summary>
    private void CollectExtensionMembers()
    {
        foreach (var assembly in _assemblies)
        {
            foreach (var type in assembly.GetTypes().Where(t => t.IsAbstract && t.IsSealed))
            {
                foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly))
                {
                    if (!Has(method, DomName) || AccessorsOf(method) == Accessors.None)
                    {
                        continue;
                    }

                    var parameters = method.GetParameters();
                    if (parameters.Length == 0 || !parameters[0].ParameterType.IsInterface)
                    {
                        continue;
                    }

                    var owner = parameters[0].ParameterType.FullName!;
                    if (!_extensions.TryGetValue(owner, out var list))
                    {
                        list = [];
                        _extensions[owner] = list;
                    }

                    list.Add(method);
                }
            }
        }
    }

    private InterfaceModel? FindParent(Type type)
    {
        InterfaceModel? best = null;
        Type? bestType = null;

        foreach (var candidate in type.GetInterfaces())
        {
            if (!_byClrName.TryGetValue(DefinitionName(candidate) ?? "", out var model))
            {
                continue;
            }

            if (bestType is null || bestType.IsAssignableFrom(candidate))
            {
                best = model;
                bestType = candidate;
            }
        }

        return best;
    }

    private InterfaceModel? LookupInterface(Type type)
        => DefinitionName(type) is { } name && _byClrName.TryGetValue(name, out var model) ? model : null;

    private static WrapperKind KindOf(Type type)
    {
        if (type.GetInterfaces().Any(i => i.FullName == "AngleSharp.Dom.INode") || type.FullName == "AngleSharp.Dom.INode")
        {
            return WrapperKind.Node;
        }

        if (type.FullName == "AngleSharp.Dom.IHtmlCollection`1"
            || type.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition().FullName == "AngleSharp.Dom.IHtmlCollection`1"))
        {
            return WrapperKind.HtmlCollection;
        }

        if (FindIndexedGetter(type) is not null)
        {
            return WrapperKind.Collection;
        }

        if (FindNamedGetter(type) is not null)
        {
            return WrapperKind.NamedMap;
        }

        return WrapperKind.Object;
    }

    /// <summary>
    /// The lookup flags every member walk uses. <see cref="BindingFlags.NonPublic"/> is load-bearing:
    /// AngleSharp re-declares <c>IReadOnlyList&lt;T&gt;.Item</c> explicitly on <c>ITokenList</c>,
    /// <c>IStringList</c> and <c>IHtmlCollection&lt;T&gt;</c> so that it can carry <c>[DomName("item")]</c>,
    /// and an explicit re-declaration is not public in metadata. Looking only at public members loses the
    /// indexed getter of three collections — silently, since the wrapper then falls back to a plain object.
    /// </summary>
    private const BindingFlags MemberFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;

    private static PropertyInfo? FindIndexedGetter(Type type)
    {
        foreach (var candidate in Closure(type))
        {
            foreach (var property in candidate.GetProperties(MemberFlags))
            {
                var index = property.GetIndexParameters();
                if (index.Length == 1
                    && index[0].ParameterType.FullName == "System.Int32"
                    && (AccessorsOf(property) & Accessors.Getter) != 0)
                {
                    return property;
                }
            }
        }

        return null;
    }

    private static PropertyInfo? FindNamedGetter(Type type)
    {
        foreach (var candidate in Closure(type))
        {
            foreach (var property in candidate.GetProperties(MemberFlags))
            {
                var index = property.GetIndexParameters();
                if (index.Length == 1
                    && index[0].ParameterType.FullName == "System.String"
                    && (AccessorsOf(property) & Accessors.Getter) != 0)
                {
                    return property;
                }
            }
        }

        return null;
    }

    private static IEnumerable<Type> Closure(Type type)
    {
        yield return type;
        foreach (var candidate in type.GetInterfaces())
        {
            yield return candidate;
        }
    }

    private static string GroupOf(Type type) => type.Namespace switch
    {
        "AngleSharp.Html.Dom" or "AngleSharp.Html.Dom.Events" => "Html",
        "AngleSharp.Css.Dom" => "Css",
        "AngleSharp.Svg.Dom" => "Svg",
        "AngleSharp.Media.Dom" => "Media",
        "AngleSharp.Io.Dom" => "Io",
        "AngleSharp.Browser.Dom" => "Browser",
        _ => "Dom",
    };

    // ---------------------------------------------------------------------------------------------------
    // Members
    // ---------------------------------------------------------------------------------------------------

    private void BuildMembers(InterfaceModel model)
    {
        if (model.ManualShape is not null)
        {
            return;
        }

        var inherited = model.Parent is null ? [] : DeclaredKeys(model.Parent.ClrType);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var (declaring, member) in ClosureMembers(model.ClrType))
        {
            if (inherited.Contains(Key(declaring, member)))
            {
                continue;
            }

            foreach (var domName in DomNamesOf(member))
            {
                if (!seen.Add(domName))
                {
                    _model.Diagnostics.Add(
                        model.DomName + "." + domName + " is declared more than once in the interface closure; the first declaration wins (" + declaring.Name + "." + member.Name + " lost).");
                    continue;
                }

                BuildMember(model, declaring, member, domName);
            }
        }

        // The curated additions last, and against what was actually emitted rather than against what was
        // seen: half of them name a member AngleSharp does declare and the generator skipped, which is
        // exactly the case an addition exists for. A collision with an *emitted* member is a real one — the
        // pinned assemblies grew a member the table is now shadowing — and is reported.
        foreach (var addition in _overrides.Additions)
        {
            // An extend entry declares its members in C#, so there is nothing to append here; the emitter
            // hands it the builder instead.
            if (addition.IsExtend || addition.Interface != model.DomName)
            {
                continue;
            }

            if (model.Members.Any(m => m.DomName == addition.Member))
            {
                _model.Diagnostics.Add(
                    "overrides.json adds " + model.DomName + "." + addition.Member + " (" + addition.Reason
                    + "), but the pinned assemblies already project it; the addition is ignored.");
                continue;
            }

            // The skip this replaces is no longer a consequence, so it leaves the report: a reader looking
            // for members that do not cross should not find one that does.
            _model.Skipped.RemoveAll(s => s.Interface == model.DomName && s.Member == addition.Member);

            model.Members.Add(new MemberModel
            {
                DomName = addition.Member,
                Kind = addition.Kind == "attribute" ? MemberKind.Attribute : MemberKind.Operation,
                Body = string.Join("\n", addition.Body),
                SetterBody = addition.Setter is { Count: > 0 } setter ? string.Join("\n", setter) : null,
                Length = addition.Length,
                Origin = "overrides.json",
            });
        }

        model.Members.Sort((a, b) => string.CompareOrdinal(a.DomName, b.DomName));
    }

    private HashSet<string> DeclaredKeys(Type type)
        => ClosureMembers(type).Select(entry => Key(entry.Declaring, entry.Member)).ToHashSet(StringComparer.Ordinal);

    private static string Key(Type declaring, MemberInfo member)
        => DefinitionName(declaring) + "::" + member.Name + "::" + string.Join(",", (member as MethodInfo)?.GetParameters().Select(p => p.ParameterType.Name) ?? []);

    /// <summary>
    /// A type's identity for lookup and for member subtraction: a closed generic construction answers with
    /// its definition, because <c>IHtmlCollection&lt;IHtmlOptionElement&gt;</c> and
    /// <c>IHtmlCollection&lt;IElement&gt;</c> are one WebIDL interface however many CLR types they are.
    /// </summary>
    private static string? DefinitionName(Type type)
        => type.IsGenericType && !type.IsGenericTypeDefinition
            ? type.GetGenericTypeDefinition().FullName
            : type.FullName;

    /// <summary>
    /// Every member reachable through an interface: its own, every base interface's, every mixin's, and the
    /// extension methods attached to any of them.
    /// </summary>
    private List<(Type Declaring, MemberInfo Member)> ClosureMembers(Type type)
    {
        var result = new List<(Type, MemberInfo)>();

        foreach (var candidate in Closure(type))
        {
            var name = DefinitionName(candidate);
            if (name is null || _excluded.Contains(name) || _closureExcluded.Contains(name))
            {
                continue;
            }

            foreach (var member in CandidateMembers(candidate))
            {
                if (Has(member, DomName))
                {
                    result.Add((candidate, member));
                }
            }

            if (_extensions.TryGetValue(name, out var extensions))
            {
                foreach (var extension in extensions)
                {
                    // The setter half of an extension-defined attribute is found from the getter, not listed
                    // beside it: AngleSharp spells `style.color` as a GetColor/SetColor pair sharing one
                    // [DomName], and two entries would be two members with the same name.
                    if ((AccessorsOf(extension) & (Accessors.Getter | Accessors.Method)) != Accessors.None)
                    {
                        result.Add((candidate, extension));
                    }
                }
            }
        }

        return result;
    }

    private static List<string> DomNamesOf(MemberInfo member) => DomNames(member);

    private void BuildMember(InterfaceModel model, Type declaring, MemberInfo member, string domName)
    {
        var qualified = model.DomName + "." + domName;

        if (_overrides.Skip.FirstOrDefault(s => s.Interface == model.DomName && s.Member == domName && s.Half is null) is { } skip)
        {
            _model.Skipped.Add(new SkipRecord(model.DomName, domName, skip.Reason));
            return;
        }

        // `length` on a collection is an OWN property of the wrapper, put there by ArrayLikeObject, so a
        // prototype accessor of the same name could only ever be shadowed. The deviation from a browser —
        // which has it on the interface prototype — is documented on DomCollectionBase.
        if (domName == "length" && model.Kind is WrapperKind.Collection or WrapperKind.HtmlCollection)
        {
            _model.Skipped.Add(new SkipRecord(model.DomName, domName, "is an own property of the collection wrapper (ArrayLikeObject), a documented deviation from putting it on the prototype"));
            return;
        }

        if (member is MethodInfo method)
        {
            // An extension method carrying Accessors.Getter is an IDL attribute, not an operation: it is how
            // AngleSharp spells `element.style` and every one of CSSStyleDeclaration's CSS properties.
            if (method.IsStatic && (AccessorsOf(method) & Accessors.Getter) != Accessors.None && (AccessorsOf(method) & Accessors.Method) == Accessors.None)
            {
                BuildExtensionAttribute(model, declaring, method, domName, qualified);
                return;
            }

            BuildOperation(model, declaring, method, domName, qualified);
            return;
        }

        if (member is PropertyInfo property)
        {
            if (property.GetIndexParameters().Length > 0)
            {
                if (model.Kind is WrapperKind.Node)
                {
                    // An indexed or named getter needs a property projection, and a node wrapper is a
                    // JsEventTarget rather than an ArrayLikeObject, so it has none. `form[0]` and
                    // `form.username` are the two that lose by it; `form.elements[0]` and
                    // `form.elements.namedItem('username')` are the same values through the collection.
                    _model.Skipped.Add(new SkipRecord(model.DomName, domName, "is an indexed or named getter on a node, and a node wrapper carries no property projection; the collection member beside it carries the same values"));
                    return;
                }

                BuildIndexerAsOperation(model, declaring, property, domName, qualified);
                return;
            }

            if ((AccessorsOf(property) & Accessors.Method) != 0)
            {
                BuildPropertyAsOperation(model, declaring, property, domName, qualified);
                return;
            }

            BuildAttribute(model, declaring, property, domName, qualified);
        }
    }

    /// <summary>The line every generated member starts with: the brand check, and the realm behind it.</summary>
    private static string Bind(InterfaceModel model, string qualified)
        => "var self = global::Jint.Browser.Dom.DomBindings.Bind<" + CSharpNames.Render(model.ClrType) + ">(thisObj, " + CSharpNames.Literal(qualified) + ");\n";

    private void BuildIndexerAsOperation(InterfaceModel model, Type declaring, PropertyInfo property, string domName, string qualified)
    {
        // `nodeList.item(0)` and `collection.namedItem('x')` are operations in IDL and indexers in AngleSharp.
        var index = property.GetIndexParameters()[0];
        if (!_conversions.TryParameter(index, 0, qualified, null, out var argument, out var argumentReason))
        {
            _model.Skipped.Add(new SkipRecord(model.DomName, domName, argumentReason));
            return;
        }

        var access = CSharpNames.IsExplicitlyNamed(property)
            ? "((" + CSharpNames.Render(declaring.GetInterfaces().First(i => i.IsGenericType && i.GetGenericTypeDefinition().FullName == "System.Collections.Generic.IReadOnlyList`1")) + ") self.Target)[" + argument + "]"
            : "self.Target[" + argument + "]";

        if (!_conversions.TryReturn(property.PropertyType, access, "self.Realm", IsNullableString(model, domName), out var body, out var reason))
        {
            _model.Skipped.Add(new SkipRecord(model.DomName, domName, reason));
            return;
        }

        model.Members.Add(new MemberModel
        {
            DomName = domName,
            Kind = MemberKind.Operation,
            Length = 1,
            Body = Bind(model, qualified) + "return " + body + ";",
            Origin = declaring.Name + "." + property.Name,
        });
    }

    private void BuildOperation(InterfaceModel model, Type declaring, MethodInfo method, string domName, string qualified)
    {
        if (_overrides.Hooks.FirstOrDefault(h => h.Interface == model.DomName && h.Member == domName && h.Half == "operation") is { } hook)
        {
            model.Members.Add(new MemberModel
            {
                DomName = domName,
                Kind = MemberKind.Operation,
                Length = method.GetParameters().Length,
                Body = Bind(model, qualified) + "self.Realm.Hooks." + hook.Hook + "(self.Realm, self.Target, args); return global::Jint.Native.JsValue.Undefined;",
                Origin = declaring.Name + "." + method.Name + " (hook)",
            });
            return;
        }

        if (!TryArguments(model, method, qualified, out var arguments, out var reason))
        {
            _model.Skipped.Add(new SkipRecord(model.DomName, domName, reason));
            return;
        }

        var call = method.IsStatic
            ? ExtensionCall(method, "self.Target", [.. arguments])
            : "self.Target." + method.Name + "(" + string.Join(", ", arguments) + ")";

        if (!_conversions.TryReturn(method.ReturnType, call, "self.Realm", IsNullableString(model, domName), out var body, out var returnReason))
        {
            _model.Skipped.Add(new SkipRecord(model.DomName, domName, returnReason));
            return;
        }

        var length = method.GetParameters().Count(p => !p.IsOptional && !IsParams(p));
        model.Members.Add(new MemberModel
        {
            DomName = domName,
            Kind = MemberKind.Operation,
            Length = length,
            Body = Bind(model, qualified) + (method.ReturnType.FullName == "System.Void"
                ? body + "; return global::Jint.Native.JsValue.Undefined;"
                : "return " + body + ";"),
            Origin = declaring.Name + "." + method.Name,
        });
    }

    private void BuildPropertyAsOperation(InterfaceModel model, Type declaring, PropertyInfo property, string domName, string qualified)
    {
        // [DomAccessor(Accessors.Method)] on a property means WebIDL declares it as an operation:
        // Node.hasChildNodes() is the whole of it in the pinned assemblies.
        if (!_conversions.TryReturn(property.PropertyType, "self.Target." + property.Name, "self.Realm", IsNullableString(model, domName), out var body, out var reason))
        {
            _model.Skipped.Add(new SkipRecord(model.DomName, domName, reason));
            return;
        }

        model.Members.Add(new MemberModel
        {
            DomName = domName,
            Kind = MemberKind.Operation,
            Length = 0,
            Body = Bind(model, qualified) + "return " + body + ";",
            Origin = declaring.Name + "." + property.Name,
        });
    }

    /// <summary>
    /// An IDL attribute AngleSharp spells as a pair of extension methods rather than as a property:
    /// <c>element.style</c>, <c>element.innerText</c>, and every one of <c>CSSStyleDeclaration</c>'s CSS
    /// properties.
    /// </summary>
    private void BuildExtensionAttribute(InterfaceModel model, Type declaring, MethodInfo getter, string domName, string qualified)
    {
        var read = ExtensionCall(getter, "self.Target");
        if (!_conversions.TryReturn(getter.ReturnType, read, "self.Realm", IsNullableString(model, domName), out var body, out var reason))
        {
            _model.Skipped.Add(new SkipRecord(model.DomName, domName, reason));
            return;
        }

        string? setter = null;
        if (FindExtensionAccessor(declaring, domName, Accessors.Setter) is { } extensionSetter
            && !_overrides.Skip.Any(s => s.Interface == model.DomName && s.Member == domName && s.Half == "setter")
            && _conversions.TryParameter(extensionSetter.GetParameters()[1], 0, qualified, null, out var value, out _))
        {
            setter = ExtensionCall(extensionSetter, "self.Target", value) + "; return global::Jint.Native.JsValue.Undefined;";
        }

        var bind = Bind(model, qualified);
        model.Members.Add(new MemberModel
        {
            DomName = domName,
            Kind = MemberKind.Attribute,
            Body = bind + "return " + body + ";",
            SetterBody = setter is null ? null : bind + setter,
            Origin = getter.DeclaringType!.Name + "." + getter.Name,
        });
    }

    private void BuildAttribute(InterfaceModel model, Type declaring, PropertyInfo property, string domName, string qualified)
    {
        var lenient = Has(property, DomLenientThis);
        var extensionGetter = FindExtensionAccessor(declaring, domName, Accessors.Getter);
        var extensionSetter = FindExtensionAccessor(declaring, domName, Accessors.Setter);

        string read;
        Type readType;
        if (extensionGetter is not null)
        {
            read = CSharpNames.Render(extensionGetter.DeclaringType!) + "." + extensionGetter.Name + "(self.Target)";
            readType = extensionGetter.ReturnType;
        }
        else
        {
            if (!property.CanRead)
            {
                _model.Skipped.Add(new SkipRecord(model.DomName, domName, "is write-only in AngleSharp, which WebIDL has no shape for"));
                return;
            }

            read = "self.Target." + property.Name;
            readType = property.PropertyType;
        }

        if (!_conversions.TryReturn(readType, read, "self.Realm", IsNullableString(model, domName), out var getter, out var reason))
        {
            _model.Skipped.Add(new SkipRecord(model.DomName, domName, reason));
            return;
        }

        var setter = BuildSetter(model, property, extensionSetter, domName, qualified);
        var receiver = CSharpNames.Render(model.ClrType);

        // [LegacyLenientThis]: a wrong receiver answers undefined from the getter and is ignored by the
        // setter, rather than throwing. https://webidl.spec.whatwg.org/#LegacyLenientThis
        var bind = lenient
            ? "if (!global::Jint.Browser.Dom.DomBindings.TryBind<" + receiver + ">(thisObj, out var self))\n{\n    return global::Jint.Native.JsValue.Undefined;\n}\n\n"
            : Bind(model, qualified);

        model.Members.Add(new MemberModel
        {
            DomName = domName,
            Kind = MemberKind.Attribute,
            Body = bind + "return " + getter + ";",
            SetterBody = setter is null ? null : bind + setter,
            Origin = declaring.Name + "." + property.Name,
        });
    }

    private string? BuildSetter(InterfaceModel model, PropertyInfo property, MethodInfo? extensionSetter, string domName, string qualified)
    {
        if (_overrides.Skip.Any(s => s.Interface == model.DomName && s.Member == domName && s.Half == "setter"))
        {
            return null;
        }

        if (_overrides.Hooks.FirstOrDefault(h => h.Interface == model.DomName && h.Member == domName && h.Half == "setter") is { } hook)
        {
            return "self.Realm.Hooks." + hook.Hook + "(self.Realm, self.Target, global::Jint.Browser.Dom.DomConvert.RequiredText(args, 0, " + CSharpNames.Literal(qualified) + ")); return global::Jint.Native.JsValue.Undefined;";
        }

        if (extensionSetter is not null)
        {
            var parameter = extensionSetter.GetParameters()[1];
            if (!_conversions.TryParameter(parameter, 0, qualified, null, out var value, out _))
            {
                return null;
            }

            return ExtensionCall(extensionSetter, "self.Target", value) + "; return global::Jint.Native.JsValue.Undefined;";
        }

        if (PutForwards(property) is { } forwarded)
        {
            var target = property.PropertyType;
            var forwardedProperty = Closure(target)
                .SelectMany(t => t.GetProperties(MemberFlags))
                .FirstOrDefault(p => DomNames(p).Contains(forwarded) && p.CanWrite);

            if (forwardedProperty is null || forwardedProperty.PropertyType.FullName != "System.String")
            {
                _model.Diagnostics.Add(model.DomName + "." + domName + " declares [DomPutForwards(\"" + forwarded + "\")] but no writable string member of that name was found on " + target.Name + "; the attribute stays read-only.");
                return null;
            }

            return "var forwardTarget = " + "self.Target." + property.Name + "; if (forwardTarget is not null) { forwardTarget." + forwardedProperty.Name
                   + " = global::Jint.Browser.Dom.DomConvert.RequiredText(args, 0, " + CSharpNames.Literal(qualified) + "); } return global::Jint.Native.JsValue.Undefined;";
        }

        if (!property.CanWrite)
        {
            return null;
        }

        var setterParameter = property.SetMethod!.GetParameters()[0];
        if (!_conversions.TryParameter(setterParameter, 0, qualified, null, out var assigned, out var reason))
        {
            _model.Diagnostics.Add(model.DomName + "." + domName + " is read-only in the binding because its setter " + reason + ".");
            return null;
        }

        return "self.Target." + property.Name + " = " + assigned + "; return global::Jint.Native.JsValue.Undefined;";
    }

    /// <summary>
    /// An extension-method call, written in extension form. AngleSharp and AngleSharp.Css both declare an
    /// <c>AngleSharp.Dom.ElementExtensions</c>, so naming the class by its full name is CS0433 whichever one
    /// is meant; extension-method lookup resolves it by which of the two declares the method, and the
    /// namespace goes out as a <c>using</c> directive.
    /// </summary>
    private string ExtensionCall(MethodInfo method, string receiver, params string[] arguments)
    {
        _model.ExtensionNamespaces.Add(method.DeclaringType!.Namespace!);
        return receiver + "." + method.Name + "(" + string.Join(", ", arguments) + ")";
    }

    private MethodInfo? FindExtensionAccessor(Type declaring, string domName, Accessors accessor)
    {
        if (declaring.FullName is not { } name || !_extensions.TryGetValue(name, out var extensions))
        {
            return null;
        }

        return extensions.FirstOrDefault(m => DomNames(m).Contains(domName) && (AccessorsOf(m) & accessor) != 0 && (AccessorsOf(m) & Accessors.Method) == 0);
    }

    private bool TryArguments(InterfaceModel model, MethodInfo method, string qualified, out List<string> arguments, out string reason)
    {
        arguments = [];
        reason = "";

        var parameters = method.GetParameters();
        var start = method.IsStatic ? 1 : 0;
        var dictionaryOffset = InitDictOffset(method);

        for (var i = start; i < parameters.Length; i++)
        {
            var index = i - start;
            var parameter = parameters[i];
            var dictionaryMember = dictionaryOffset >= 0 && index >= dictionaryOffset ? CamelCase(parameter.Name!) : null;

            if (!_conversions.TryParameter(parameter, dictionaryMember is null ? index : dictionaryOffset, qualified, dictionaryMember, out var code, out reason))
            {
                return false;
            }

            arguments.Add(code);
        }

        return true;
    }

    private bool IsNullableString(InterfaceModel model, string domName)
        => _overrides.NullableStrings.Any(n => n.Interface == model.DomName && n.Member == domName);

    private static bool IsParams(ParameterInfo parameter)
        => parameter.GetCustomAttributesData().Any(a => a.AttributeType.FullName == "System.ParamArrayAttribute");

    private static string CamelCase(string name) => name.Length == 0 ? name : char.ToLowerInvariant(name[0]) + name[1..];

    // ---------------------------------------------------------------------------------------------------
    // Collection accessors
    // ---------------------------------------------------------------------------------------------------

    private void BuildAccessor(InterfaceModel model)
    {
        if (model.Kind is not (WrapperKind.Collection or WrapperKind.NamedMap))
        {
            return;
        }

        var builder = new StringBuilder();
        var className = "DomAccessor" + model.FieldName;
        var target = CSharpNames.Render(model.ClrType);

        builder.Append("/// <summary>How <c>").Append(model.DomName).Append("</c> answers indexed and named property lookups.</summary>\n");
        builder.Append("internal sealed class ").Append(className).Append(" : DomCollectionAccessor\n{\n");
        builder.Append("    internal static readonly ").Append(className).Append(" Instance = new();\n\n");

        if (model.Kind == WrapperKind.Collection)
        {
            var length = FindLength(model.ClrType);
            var indexed = FindIndexedGetter(model.ClrType)!;
            var readOnlyList = model.ClrType.GetInterfaces()
                .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition().FullName == "System.Collections.Generic.IReadOnlyList`1");

            var access = readOnlyList is not null && CSharpNames.IsExplicitlyNamed(indexed)
                ? "((" + CSharpNames.Render(readOnlyList) + ") collection)[(int) index]"
                : "collection[(int) index]";

            var elementType = readOnlyList is not null && CSharpNames.IsExplicitlyNamed(indexed)
                ? readOnlyList.GetGenericArguments()[0]
                : indexed.PropertyType;

            if (length is null)
            {
                _model.Diagnostics.Add(model.DomName + " has an indexed getter but no length; it is wrapped as a plain object instead.");
                model.Kind = WrapperKind.Object;
                return;
            }

            if (!_conversions.TryReturn(elementType, access, "realm", nullableString: false, out var element, out var reason))
            {
                _model.Diagnostics.Add(model.DomName + " has an indexed getter whose element " + reason + "; it is wrapped as a plain object instead.");
                model.Kind = WrapperKind.Object;
                return;
            }

            builder.Append("    internal override uint Length(object target) => (uint) ((").Append(target).Append(") target).").Append(length.Name).Append(";\n\n");
            builder.Append("    internal override bool TryGetIndex(DomRealm realm, object target, uint index, out global::Jint.Native.JsValue value)\n    {\n");
            builder.Append("        var collection = (").Append(target).Append(") target;\n");
            builder.Append("        if (index >= (uint) collection.").Append(length.Name).Append(")\n        {\n");
            builder.Append("            value = global::Jint.Native.JsValue.Undefined;\n            return false;\n        }\n\n");
            builder.Append("        value = ").Append(element).Append(";\n        return true;\n    }\n");
        }

        AppendNamedHalf(model, builder, target);

        builder.Append("}\n");
        model.AccessorClass = builder.ToString();
        model.AccessorReference = className + ".Instance";
    }

    private void AppendNamedHalf(InterfaceModel model, StringBuilder builder, string target)
    {
        var named = FindNamedGetter(model.ClrType);
        if (named is null)
        {
            return;
        }

        var accessors = AccessorsOf(named);
        var deleter = Closure(model.ClrType)
            .SelectMany(t => t.GetMethods(MemberFlags))
            .FirstOrDefault(m => (AccessorsOf(m) & Accessors.Deleter) != 0);

        var isMap = model.ClrType.GetInterfaces().Any(i =>
            i.IsGenericType
            && i.GetGenericTypeDefinition().FullName == "System.Collections.Generic.IEnumerable`1"
            && i.GetGenericArguments()[0].IsGenericType
            && i.GetGenericArguments()[0].GetGenericTypeDefinition().FullName == "System.Collections.Generic.KeyValuePair`2");

        builder.Append("\n    internal override bool HasNamedGetter => true;\n\n");

        if (isMap)
        {
            var pair = model.ClrType.GetInterfaces().First(i =>
                i.IsGenericType
                && i.GetGenericTypeDefinition().FullName == "System.Collections.Generic.IEnumerable`1"
                && i.GetGenericArguments()[0].IsGenericType);

            builder.Append("    internal override global::System.Collections.Generic.IReadOnlyList<string> SupportedNames(object target)\n    {\n");
            builder.Append("        var names = new global::System.Collections.Generic.List<string>();\n");
            builder.Append("        foreach (var entry in (").Append(CSharpNames.Render(pair)).Append(") target)\n        {\n");
            builder.Append("            // A null value is filtered out because the projection's three hooks have to agree at the\n");
            builder.Append("            // same instant, and TryGetNamed reads null as an authoritative miss. AngleSharp's\n");
            builder.Append("            // StringMap.Remove leaves the attribute in place with a null value rather than removing it\n");
            builder.Append("            // (reported upstream), so without this a deleted dataset key would still enumerate while\n");
            builder.Append("            // reading as undefined — the exact incoherence host-contract verification catches.\n");
            builder.Append("            if (entry.Value is not null)\n            {\n                names.Add(entry.Key);\n            }\n        }\n\n        return names;\n    }\n\n");
        }
        else
        {
            // WebIDL's [LegacyUnenumerableNamedProperties]: the names exist for `in` and `hasOwnProperty` but
            // do not enumerate. Every named getter here but DOMStringMap's carries it — NamedNodeMap and
            // HTMLCollection both do in their own specifications — so the names are listed and marked
            // non-enumerable rather than hidden.
            var length = FindLength(model.ClrType);
            var itemName = ItemNameProperty(model.ClrType);
            if (length is null || itemName is null)
            {
                builder.Append("    internal override global::System.Collections.Generic.IReadOnlyList<string> SupportedNames(object target) => [];\n\n");
            }
            else
            {
                builder.Append("    internal override global::System.Collections.Generic.IReadOnlyList<string> SupportedNames(object target)\n    {\n");
                builder.Append("        var collection = (").Append(target).Append(") target;\n");
                builder.Append("        var names = new global::System.Collections.Generic.List<string>(collection.").Append(length.Name).Append(");\n");
                builder.Append("        for (var i = 0; i < collection.").Append(length.Name).Append("; i++)\n        {\n");
                builder.Append("            names.Add(collection[i]!.").Append(itemName.Name).Append(");\n        }\n\n        return names;\n    }\n\n");
            }

            builder.Append("    internal override bool AreNamesEnumerable => false;\n\n");
        }

        builder.Append("    internal override bool TryGetNamed(DomRealm realm, object target, string name, out global::Jint.Native.JsValue value)\n    {\n");
        builder.Append("        var item = ((").Append(target).Append(") target)[name];\n");
        builder.Append("        if (item is null)\n        {\n            value = global::Jint.Native.JsValue.Undefined;\n            return false;\n        }\n\n");

        var namedElement = named.PropertyType;
        if (!_conversions.TryReturn(namedElement, "item", "realm", nullableString: false, out var namedValue, out var namedReason))
        {
            _model.Diagnostics.Add(model.DomName + "'s named getter " + namedReason + "; it is omitted.");
            builder.Length = builder.ToString().LastIndexOf("\n    internal override bool HasNamedGetter", StringComparison.Ordinal);
            return;
        }

        builder.Append("        value = ").Append(namedValue).Append(";\n        return true;\n    }\n");

        if ((accessors & Accessors.Setter) != 0 && named.CanWrite && namedElement.FullName == "System.String")
        {
            builder.Append("\n    internal override bool IsNameWritable => true;\n\n");
            builder.Append("    internal override bool TrySetNamed(DomRealm realm, object target, string name, global::Jint.Native.JsValue value)\n    {\n");
            builder.Append("        ((").Append(target).Append(") target)[name] = global::Jint.Runtime.TypeConverter.ToString(value);\n        return true;\n    }\n");
        }

        if (deleter is not null)
        {
            builder.Append("\n    internal override bool TryDeleteNamed(object target, string name)\n    {\n");
            builder.Append("        ((").Append(target).Append(") target).").Append(deleter.Name).Append("(name);\n        return true;\n    }\n");
        }
    }

    private static PropertyInfo? FindLength(Type type)
        => Closure(type)
            .SelectMany(t => t.GetProperties(MemberFlags))
            .FirstOrDefault(p => p.GetIndexParameters().Length == 0
                                 && p.PropertyType.FullName == "System.Int32"
                                 && (p.Name == "Length" || p.Name == "Count"));

    private static PropertyInfo? ItemNameProperty(Type type)
    {
        var indexed = FindIndexedGetter(type);
        if (indexed is null)
        {
            return null;
        }

        return Closure(indexed.PropertyType)
            .SelectMany(t => t.GetProperties(MemberFlags))
            .FirstOrDefault(p => p.PropertyType.FullName == "System.String" && DomNames(p).Contains("name"));
    }

    // ---------------------------------------------------------------------------------------------------
    // Constants
    // ---------------------------------------------------------------------------------------------------

    /// <summary>
    /// WebIDL constants — <c>Node.ELEMENT_NODE</c>, <c>CSSRule.STYLE_RULE</c> — come from the numeric enums
    /// AngleSharp <em>returns</em>. Returned rather than merely mentioned, because an enum in an argument
    /// position belongs to whatever interface the specification hangs it off (<c>NodeFilter.SHOW_ELEMENT</c>
    /// is not <c>Document</c>'s), and <c>overrides.json</c> is where the two that do not follow the rule are
    /// added and removed by hand.
    /// </summary>
    private void BuildConstants()
    {
        foreach (var model in _byClrName.Values)
        {
            var enums = new List<Type>();

            foreach (var (declaring, member) in ClosureMembers(model.ClrType))
            {
                if (model.Parent is not null && DeclaredKeys(model.Parent.ClrType).Contains(Key(declaring, member)))
                {
                    continue;
                }

                var returned = member switch
                {
                    PropertyInfo property => property.PropertyType,
                    MethodInfo method => method.ReturnType,
                    _ => null,
                };

                if (returned is { IsEnum: true } && !_stringEnums.Contains(returned.FullName!) && !enums.Contains(returned))
                {
                    enums.Add(returned);
                }
            }

            foreach (var entry in _overrides.Constants.Add.Where(a => a.Interface == model.DomName))
            {
                var type = _assemblies.SelectMany(a => a.GetTypes()).FirstOrDefault(t => t.FullName == entry.Enum);
                if (type is null)
                {
                    _model.Diagnostics.Add("overrides.json adds constants from '" + entry.Enum + "' to " + model.DomName + ", but that enum is not in the pinned assemblies.");
                    continue;
                }

                if (!enums.Contains(type))
                {
                    enums.Add(type);
                }
            }

            foreach (var entry in _overrides.Constants.Skip.Where(s => s.Interface == model.DomName))
            {
                enums.RemoveAll(t => t.FullName == entry.Enum);
            }

            foreach (var type in enums)
            {
                foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Static))
                {
                    if (FirstDomName(field) is not { } name)
                    {
                        continue;
                    }

                    var value = Convert.ToInt64(field.GetRawConstantValue(), System.Globalization.CultureInfo.InvariantCulture);
                    if (model.Constants.Any(c => c.Name == name))
                    {
                        continue;
                    }

                    model.Constants.Add(new ConstantModel(name, value, type.Name + "." + field.Name));
                }
            }

            model.Constants.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
        }
    }

    // ---------------------------------------------------------------------------------------------------

    private void VerifyOverridesMatchTheAssemblies()
    {
        var known = new HashSet<string>(StringComparer.Ordinal);
        foreach (var model in _byClrName.Values)
        {
            foreach (var (_, member) in ClosureMembers(model.ClrType))
            {
                foreach (var domName in DomNames(member))
                {
                    known.Add(model.DomName + "." + domName);
                }
            }
        }

        void Check(string kind, string iface, string member, string reason)
        {
            if (!known.Contains(iface + "." + member))
            {
                _model.Diagnostics.Add("overrides.json's " + kind + " entry '" + iface + "." + member + "' (" + reason + ") names no member of the pinned assemblies.");
            }
        }

        foreach (var entry in _overrides.Skip)
        {
            Check("skip", entry.Interface, entry.Member, entry.Reason);
        }

        foreach (var entry in _overrides.Hooks)
        {
            Check("hook", entry.Interface, entry.Member, entry.Reason);
        }

        foreach (var entry in _overrides.NullableStrings)
        {
            Check("nullableStrings", entry.Interface, entry.Member, entry.Reason);
        }

        // The additions are checked the other way round: the interface has to exist, and the member has to
        // be one AngleSharp does not have — a member it grew is reported by BuildMembers as a collision.
        foreach (var entry in _overrides.Additions)
        {
            var named = entry.IsExtend ? entry.Extend : entry.Member;

            if (entry.IsExtend == !string.IsNullOrEmpty(entry.Member))
            {
                _model.Diagnostics.Add(
                    "overrides.json's addition on '" + entry.Interface + "' (" + entry.Reason
                    + ") sets " + (entry.IsExtend ? "both 'member' and 'extend'" : "neither 'member' nor 'extend'")
                    + "; an entry declares one member or hands the builder to one method, never both and never neither.");
                continue;
            }

            var target = _byClrName.Values.FirstOrDefault(m => m.DomName == entry.Interface);

            if (target is null)
            {
                _model.Diagnostics.Add(
                    "overrides.json's addition '" + entry.Interface + "." + named + "' (" + entry.Reason
                    + ") names an interface the pinned assemblies do not project.");
                continue;
            }

            // An extend entry needs a builder to add to, and a manual interface's shape is hand-written whole.
            if (entry.IsExtend && target.ManualShape is not null)
            {
                _model.Diagnostics.Add(
                    "overrides.json's addition hands the builder of '" + entry.Interface + "' to '" + named
                    + "' (" + entry.Reason + "), but that interface's shape is hand-written by 'manual', so the generator emits no builder for it.");
            }
        }

        foreach (var entry in _overrides.ExcludedInterfaces)
        {
            if (!_assemblies.SelectMany(a => a.GetTypes()).Any(t => t.FullName == entry.Interface))
            {
                _model.Diagnostics.Add("overrides.json excludes '" + entry.Interface + "' (" + entry.Reason + "), which is not in the pinned assemblies.");
            }
        }

        foreach (var entry in _overrides.StringEnums)
        {
            if (!_assemblies.SelectMany(a => a.GetTypes()).Any(t => t.FullName == entry.Enum))
            {
                _model.Diagnostics.Add("overrides.json names string enum '" + entry.Enum + "' (" + entry.Reason + "), which is not in the pinned assemblies.");
            }
        }
    }
}
