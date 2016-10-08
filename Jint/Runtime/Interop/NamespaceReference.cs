using System;
using System.Collections.Generic;
using System.Globalization;
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

                genericTypes[i] = arguments.At(i).As<TypeReference>().Type;
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
#if NETSTANDARD1_3
            var lookupAssemblies = new[] { typeof(NamespaceReference).GetTypeInfo().Assembly };
#else
            var lookupAssemblies = new[] { Assembly.GetCallingAssembly(), Assembly.GetExecutingAssembly() };
#endif

            foreach (var assembly in lookupAssemblies)
            {
                type = assembly.GetType(path);
                if (type != null)
                {
                    Engine.TypeCache.Add(path, type);
                    return TypeReference.CreateTypeReference(Engine, type);
                }
            }

            // search in lookup assemblies
            foreach (var assembly in Engine.Options._LookupAssemblies)
            {
                type = assembly.GetType(path);
                if (type != null)
                {
                    Engine.TypeCache.Add(path, type);
                    return TypeReference.CreateTypeReference(Engine, type);
                }

                var lastPeriodPos = path.LastIndexOf(".", StringComparison.Ordinal);
                var trimPath = path.Substring(0, lastPeriodPos);
                type = GetType(assembly, trimPath);
                if (type != null)
                  foreach (Type nType in GetAllNestedTypes(type))
                  {
                    if (nType.FullName.Replace("+", ".").Equals(path.Replace("+", ".")))
                    {
                      Engine.TypeCache.Add(path.Replace("+", "."), nType);
                      return TypeReference.CreateTypeReference(Engine, nType);
                    }
                  }
            }

            // the new path doesn't represent a known class, thus return a new namespace instance

            Engine.TypeCache.Add(path, null);
            return new NamespaceReference(Engine, path);
        }

        /// <summary>   Gets a type. </summary>
        ///<remarks>Nested type separators are converted to '.' instead of '+' </remarks>
        /// <param name="assembly"> The assembly. </param>
        /// <param name="typeName"> Name of the type. </param>
        ///
        /// <returns>   The type. </returns>

        private static Type GetType(Assembly assembly, string typeName)
        {
            Type[] types = assembly.GetTypes();
            foreach (Type t in types)
            {
                if (t.FullName.Replace("+", ".") == typeName.Replace("+", "."))
                {
                    return t;
                }
            }
            return null;
        }

        private static IEnumerable<Type> GetAllNestedTypes(Type type)
        {
          var types = new List<Type>();
          AddNestedTypesRecursively(types, type);
          return types.ToArray();
        }

        private static void AddNestedTypesRecursively(List<Type> types, Type type)
        {
          Type[] nestedTypes = type.GetNestedTypes(BindingFlags.Public);
          foreach (Type nestedType in nestedTypes)
          {
            types.Add(nestedType);
            AddNestedTypesRecursively(types, nestedType);
          }
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
