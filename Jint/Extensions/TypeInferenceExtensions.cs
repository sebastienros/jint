using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Jint.Extensions
{
    public static class TypeInferenceExtensions
    {
        /// <summary>
        /// Determine if the given type implements the given generic interface or derives from the given generic type,
        /// and if so return the concrete types implemented.
        /// </summary>
        /// <param name="type"> The type to examine. </param>
        /// <param name="interfaceOrBaseType"> The generic type to be queried for. </param>
        /// <returns> 
        /// The generic types constructed from <paramref name="interfaceOrBaseType"/> and implemented by <paramref name="type"/>.
        /// </returns>
        public static IEnumerable<Type> GetGenericTypeImplementations(this Type type, Type interfaceOrBaseType)
        {
            if (!type.IsGenericTypeDefinition)
            {
                return (interfaceOrBaseType.IsInterface ? type.GetInterfaces() : type.GetBaseTypes())
                        .Union(new[] { type })
                        .Where(t => t.IsGenericType && t.GetGenericTypeDefinition() == interfaceOrBaseType);
            }

            return Enumerable.Empty<Type>();
        }

        /// <summary>
        /// Gets the base types that the given type inherits from
        /// </summary>
        /// <param name="type">The <see cref="Type"/> to get the base types from.</param>
        /// <returns>A collection of base types that the given type inherits from.</returns>
        public static IEnumerable<Type> GetBaseTypes(this Type type)
        {
            type = type.BaseType;

            while (type != null)
            {
                yield return type;

                type = type.BaseType;
            }
        }
    }
}
