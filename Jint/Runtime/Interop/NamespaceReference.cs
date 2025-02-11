using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime.Descriptors;

#pragma warning disable IL3050

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
    private readonly string? _path;

    public NamespaceReference(Engine engine, string? path) : base(engine)
    {
        _path = path;
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
                ExceptionHelper.ThrowTypeError(_engine.Realm, "Invalid generic type parameter on " + _path + ", if this is not a generic type / method, are you missing a lookup assembly?");
                return default;
            }

            genericTypes[i] = tr.ReferenceType;
        }

        var typeReference = GetPath(_path + "`" + arguments.Length.ToString(CultureInfo.InvariantCulture)).As<TypeReference>();

        if (typeReference is null)
        {
            return Undefined;
        }

        try
        {
            var genericType = typeReference.ReferenceType.MakeGenericType(genericTypes);

            return TypeReference.CreateTypeReference(Engine, genericType);
        }
        catch (Exception e)
        {
            ExceptionHelper.ThrowInvalidOperationException($"Invalid generic type parameter on {_path}, if this is not a generic type / method, are you missing a lookup assembly?", e);
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
            if (type == null)
            {
                return new NamespaceReference(_engine, path);
            }

            return TypeReference.CreateTypeReference(_engine, type);
        }

        // in CoreCLR, for example, classes that used to be in
        // mscorlib were moved away, and only stubs remained, because
        // of that, we do the search on the lookup assemblies first,
        // and only then in mscorlib. Probelm usage: System.IO.File.CreateText

        // search in loaded assemblies
        var lookupAssemblies = new[] { Assembly.GetCallingAssembly(), Assembly.GetExecutingAssembly() };

        foreach (var assembly in lookupAssemblies)
        {
            type = assembly.GetType(path);
            if (type != null)
            {
                _engine.TypeCache.Add(path, type);
                return TypeReference.CreateTypeReference(_engine, type);
            }
        }

        // search in lookup assemblies
        var comparedPath = path.Replace('+', '.');
        foreach (var assembly in _engine.Options.Interop.AllowedAssemblies)
        {
            type = assembly.GetType(path);
            if (type != null)
            {
                _engine.TypeCache.Add(path, type);
                return TypeReference.CreateTypeReference(_engine, type);
            }

            var lastPeriodPos = path.LastIndexOf('.');
            if (lastPeriodPos != -1)
            {
                var trimPath = path.Substring(0, lastPeriodPos);
                type = GetType(assembly, trimPath);
            }
            if (type != null)
            {
                foreach (Type nType in GetAllNestedTypes(type))
                {
                    if (nType.FullName != null && nType.FullName.Replace('+', '.').Equals(comparedPath, StringComparison.Ordinal))
                    {
                        _engine.TypeCache.Add(comparedPath, nType);
                        return TypeReference.CreateTypeReference(_engine, nType);
                    }
                }
            }
        }

        // search for type in mscorlib
        type = System.Type.GetType(path);
        if (type != null)
        {
            _engine.TypeCache.Add(path, type);
            return TypeReference.CreateTypeReference(_engine, type);
        }

        // the new path doesn't represent a known class, thus return a new namespace instance

        _engine.TypeCache.Add(path, null);
        return new NamespaceReference(_engine, path);
    }

    /// <summary>   Gets a type. </summary>
    ///<remarks>Nested type separators are converted to '.' instead of '+' </remarks>
    /// <param name="assembly"> The assembly. </param>
    /// <param name="typeName"> Name of the type. </param>
    ///
    /// <returns>   The type. </returns>
    [RequiresUnreferencedCode("Assembly type loading")]
    private static Type? GetType(Assembly assembly, string typeName)
    {
        var compared = typeName.Replace('+', '.');
        foreach (Type t in assembly.GetTypes())
        {
            if (string.Equals(t.FullName?.Replace('+', '.'), compared, StringComparison.Ordinal))
            {
                return t;
            }
        }

        return null;
    }

    private static Type[] GetAllNestedTypes(Type type)
    {
        var types = new List<Type>();
        AddNestedTypesRecursively(types, type);
        return types.ToArray();
    }

    private static void AddNestedTypesRecursively(
        List<Type> types,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicNestedTypes)] Type type)
    {
        foreach (var nestedType in type.GetNestedTypes(BindingFlags.Public))
        {
            types.Add(nestedType);
            AddNestedTypesRecursively(types, nestedType);
        }
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
