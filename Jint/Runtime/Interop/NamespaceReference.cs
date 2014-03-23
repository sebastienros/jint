using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime.Descriptors;

namespace Jint.Runtime.Interop
{
    /// <summary>
    /// Any instance on this class represents a reference to a CLR namespace.
    /// Accessing its properties will look for a class of the full name, or instantiate
    /// a new <see cref="NamespaceReference"/> as it assumes that the property is a deeper
    /// level of the current namespace
    /// </summary>
    public class NamespaceReference : ObjectInstance, ICallable
    {
        private readonly string _path;

        public NamespaceReference(Engine engine, string path) : base(engine)
        {
            _path = path;
        }

        public override bool DefineOwnProperty(string propertyName, PropertyDescriptor desc, bool throwOnError)
        {
            if (throwOnError)
            {
                throw new JavaScriptException(Engine.TypeError, "Can't define a property of a NamespaceReference");
            }

            return false;
        }

        public override bool Delete(string propertyName, bool throwOnError)
        {
            if (throwOnError)
            {
                throw new JavaScriptException(Engine.TypeError, "Can't delete a property of a NamespaceReference");
            }

            return false;
        }

        public JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
            // direct calls on a NamespaceReference constructor object is creating a generic type 
            var genericTypes = new Type[arguments.Length];
            for (int i = 0; i < arguments.Length; i++)
            {
                var genericTypeReference = arguments.At(i);
                if (genericTypeReference == Undefined.Instance || !genericTypeReference.IsObject() || genericTypeReference.AsObject().Class != "TypeReference")
                {
                    throw new JavaScriptException(Engine.TypeError, "Invalid generic type parameter");
                }

                genericTypes[i] = arguments.At(0).As<TypeReference>().Type;
            }

            var typeReference = GetPath(_path + "`" + arguments.Length.ToString(CultureInfo.InvariantCulture)).As<TypeReference>();

            if (typeReference == null)
            {
                return Undefined.Instance;
            }

            var genericType = typeReference.Type.MakeGenericType(genericTypes);

            return TypeReference.CreateTypeReference(Engine, genericType);
        }

        public override JsValue Get(string propertyName)
        {
            var newPath = _path + "." + propertyName;

            return GetPath(newPath);
        }

        public JsValue GetPath(string path)
        {
            Type type;

            if (Engine.TypeCache.TryGetValue(path, out type))
            {
                if (type == null)
                {
                    return new NamespaceReference(Engine, path);
                }

                return TypeReference.CreateTypeReference(Engine, type);
            }

            // search for type in mscorlib
            type = Type.GetType(path);
            if (type != null)
            {
                Engine.TypeCache.Add(path, type);
                return TypeReference.CreateTypeReference(Engine, type);
            }

            // search in loaded assemblies
            foreach (var assembly in new[] { Assembly.GetCallingAssembly(), Assembly.GetExecutingAssembly() }.Distinct())
            {
                type = assembly.GetType(path);
                if (type != null)
                {
                    Engine.TypeCache.Add(path, type);
                    return TypeReference.CreateTypeReference(Engine, type);
                }
            }

            // search in lookup asemblies
            foreach (var assembly in Engine.Options.GetLookupAssemblies())
            {
                type = assembly.GetType(path);
                if (type != null)
                {
                    Engine.TypeCache.Add(path, type);
                    return TypeReference.CreateTypeReference(Engine, type);
                }
            }

            // the new path doesn't represent a known class, thus return a new namespace instance

            Engine.TypeCache.Add(path, null);
            return new NamespaceReference(Engine, path);
        }

        public override PropertyDescriptor GetOwnProperty(string propertyName)
        {
            return PropertyDescriptor.Undefined;
        }

        public override string ToString()
        {
            return "[Namespace: " + _path + "]";
        }
    }
}
