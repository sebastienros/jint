using System;
using System.Collections.Generic;
using System.Reflection;

namespace Jint.Runtime.Interop
{
    internal class MethodDescriptor
    {
        private MethodDescriptor(MethodBase method)
        {
            Method = method;
            Parameters = method.GetParameters();
            foreach (var parameter in Parameters)
            {
                if (Attribute.IsDefined(parameter, typeof(ParamArrayAttribute)))
                {
                    HasParams = true;
                    break;
                }

                if (parameter.HasDefaultValue)
                {
                    ParameterDefaultValuesCount++;
                }
            }
        }

        public MethodBase Method { get; }
        public ParameterInfo[] Parameters { get; }
        public bool HasParams { get; }
        public int ParameterDefaultValuesCount { get; }

        public static MethodDescriptor[] Build<T>(List<T> source) where T : MethodBase
        {
            var descriptors = new MethodDescriptor[source.Count];
            for (var i = 0; i < source.Count; i++)
            {
                descriptors[i] = new MethodDescriptor(source[i]);
            }

            return Prioritize(descriptors);
        }

        public static MethodDescriptor[] Build<T>(T[] source) where T : MethodBase
        {
            var descriptors = new MethodDescriptor[source.Length];
            for (var i = 0; i < source.Length; i++)
            {
                descriptors[i] = new MethodDescriptor(source[i]);
            }

            return Prioritize(descriptors);
        }

        private static MethodDescriptor[] Prioritize(MethodDescriptor[] descriptors)
        {
            static int CreateComparison(MethodDescriptor d1, MethodDescriptor d2)
            {
                // put params versions to end, they can be tricky to match and can cause trouble / extra overhead
                if (d1.HasParams)
                {
                    return 1;
                }

                if (d2.HasParams)
                {
                    return -1;
                }

                // then favor less parameters
                if (d1.Parameters.Length > d2.Parameters.Length)
                {
                    return 1;
                }

                if (d2.Parameters.Length > d1.Parameters.Length)
                {
                    return -1;
                }

                return 0;
            }

            Array.Sort(descriptors, CreateComparison);

            return descriptors;
        }
    }
}