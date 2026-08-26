using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime.Descriptors;

namespace Jint.Runtime.Interop;

/// <summary>
/// Any instance on this class represents a reference to a CLR namespace.
/// Accessing its properties will look for a class of the full name, or instantiate
/// a new <see cref="NamespaceReference"/> as it assumes that the property is a deeper
/// level of the current namespace
/// </summary>
[RequiresUnreferencedCode("Dynamic loading")]
public class NamespaceReference : ObjectInstance, ICallable
{
    private static readonly ConditionalWeakTable<Assembly, Dictionary<string, Type>> _typesByAssembly = new();

    private readonly string? _path;
    private readonly ClrTypeResolutionPolicy _policy;

    public NamespaceReference(Engine engine, string? path)
        : this(engine, path, new ClrTypeResolutionPolicy(engine.Options.Interop))
    {
    }

    internal NamespaceReference(Engine engine, string? path, ClrTypeResolutionPolicy policy) : base(engine)
    {
        // Member access resolves namespace/type segments, not ordinary property lookup, so the
        // prototype-method inline cache must skip this receiver. See InternalTypes.ExoticGet.
        // Callable: `importNamespace('System.Collections.Generic').List(...)` calls through
        // ICallable to bind a generic type, so call sites must see this as callable.
        _type |= InternalTypes.ExoticGet | InternalTypes.Callable;
        _path = path;
        _policy = policy;
    }

    public override bool DefineOwnProperty(JsValue property, PropertyDescriptor desc)
    {
        return false;
    }

    public override bool Delete(JsValue property)
    {
        return false;
    }

    JsValue ICallable.Call(JsValue thisObject, params JsCallArguments arguments)
    {
        // direct calls on a NamespaceReference constructor object is creating a generic type
        var genericTypes = new Type[arguments.Length];
        for (int i = 0; i < arguments.Length; i++)
        {
            var genericTypeReference = arguments[i];
            if (genericTypeReference.IsUndefined()
                || !genericTypeReference.IsObject()
                || genericTypeReference.AsObject() is not TypeReference tr)
            {
                var message = _policy.ExposeDetailedResolutionErrors
                    ? "Invalid generic type parameter on " + _path + ", if this is not a generic type / method, are you missing a lookup assembly?"
                    : "Invalid generic CLR type parameter.";
                Throw.TypeError(_engine.Realm, message);
                return default;
            }

            genericTypes[i] = tr.ReferenceType;
        }

        var typeReference = GetPath(_path + "`" + arguments.Length.ToString(CultureInfo.InvariantCulture)) as TypeReference;

        if (typeReference is null)
        {
            return Undefined;
        }

        try
        {
            var genericType = typeReference.ReferenceType.MakeGenericType(genericTypes);

            return TypeReference.CreateTypeReference(Engine, genericType);
        }
        catch (Exception e) when (!Throw.MustPropagateHostException(e))
        {
            if (_policy.ExposeDetailedResolutionErrors)
            {
                Throw.InvalidOperationException($"Invalid generic type parameter on {_path}, if this is not a generic type / method, are you missing a lookup assembly?", e);
            }

            Throw.InvalidOperationException("Could not construct the requested generic CLR type.");
            return null;
        }
    }

    public override JsValue Get(JsValue property, JsValue receiver)
    {
        var newPath = string.IsNullOrEmpty(_path)
            ? property.ToString()
            : $"{_path}.{property}";

        return GetPath(newPath);
    }

    [RequiresUnreferencedCode("Dynamic loading")]
    public JsValue GetPath(string path)
    {
        if (_engine.TypeCache.TryGetValue(path, out var type))
        {
            if (type == null || !_policy.Allows(type))
            {
                return new NamespaceReference(_engine, path, _policy);
            }

            return TypeReference.CreateTypeReference(_engine, type);
        }

        // Search only the host's closed allow-list. Explicit TypeReference values are separate capabilities.
        var comparedPath = path.Replace('+', '.');
        foreach (var assembly in _policy.AllowedAssemblies)
        {
            type = assembly.GetType(path);
            if (type is null)
            {
                type = GetTypeByNormalizedName(assembly, comparedPath);
            }

            if (type is not null)
            {
                _engine.TypeCache.Add(path, type);
                return _policy.Allows(type)
                    ? TypeReference.CreateTypeReference(_engine, type)
                    : new NamespaceReference(_engine, path, _policy);
            }
        }

        // the new path doesn't represent a known class, thus return a new namespace instance

        _engine.TypeCache.Add(path, null);
        return new NamespaceReference(_engine, path, _policy);
    }

    [RequiresUnreferencedCode("Assembly type loading")]
    private static Type? GetTypeByNormalizedName(Assembly assembly, string typeName)
    {
        var types = _typesByAssembly.GetValue(assembly, static a =>
        {
            var result = new Dictionary<string, Type>(StringComparer.Ordinal);
            foreach (var type in a.GetTypes())
            {
                if (!ClrTypeResolutionPolicy.IsPubliclyAccessibleType(type))
                {
                    continue;
                }

                if (type.FullName?.Replace('+', '.') is { } name && !result.ContainsKey(name))
                {
                    result.Add(name, type);
                }
            }
            return result;
        });
        return types.TryGetValue(typeName, out var type) ? type : null;
    }

    public override PropertyDescriptor GetOwnProperty(JsValue property)
    {
        return PropertyDescriptor.Undefined;
    }

    public override string ToString()
    {
        return "[CLR namespace: " + _path + "]";
    }
}

internal sealed class ClrTypeResolutionPolicy
{
    private readonly bool _allowSystemReflection;
    private readonly TypeResolver _typeResolver;

    internal ClrTypeResolutionPolicy(Options.InteropOptions options)
    {
        AllowedAssemblies = new HashSet<Assembly>(options.AllowedAssemblies);
        _allowSystemReflection = options.AllowSystemReflection;
        ExposeDetailedResolutionErrors = options.ExposeDetailedResolutionErrors;
        _typeResolver = options.TypeResolver;
    }

    internal HashSet<Assembly> AllowedAssemblies { get; }

    internal bool ExposeDetailedResolutionErrors { get; }

    internal bool Allows(Type type)
    {
        return AllowedAssemblies.Contains(type.Assembly)
               && IsPubliclyAccessibleType(type)
               && (_allowSystemReflection
                   || type.Namespace?.StartsWith("System.Reflection", StringComparison.Ordinal) != true)
               && _typeResolver.FilterType(type);
    }

    internal static bool IsPubliclyAccessibleType(Type type)
    {
        if (!type.IsNested)
        {
            return type.IsPublic;
        }

        while (type.IsNested)
        {
            if (!type.IsNestedPublic)
            {
                return false;
            }

            type = type.DeclaringType!;
        }

        return type.IsPublic;
    }
}
