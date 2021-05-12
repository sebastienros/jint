using Jint.Native;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Jint.Runtime.Interop
{
    internal class MethodDescriptor
    {
        internal MethodDescriptor(MethodBase method)
        {
            Method = method;
            Parameters = method.GetParameters();
            IsExtensionMethod = method.IsDefined(typeof(ExtensionAttribute), true);

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
        public bool IsExtensionMethod { get; }

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
                // if its a generic method, put it on the end
                if (d1.Method.IsGenericMethod && !d2.Method.IsGenericMethod)
                {
                    return 1;
                }

                if (d2.Method.IsGenericMethod && !d1.Method.IsGenericMethod)
                {
                    return -1;
                }

                // put params versions to end, they can be tricky to match and can cause trouble / extra overhead
                if (d1.HasParams && !d2.HasParams)
                {
                    return 1;
                }

                if (d2.HasParams && !d1.HasParams)
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

        public JsValue Call(Engine _engine, object instance, JsValue[] arguments)
        {
            var parameters = new object[arguments.Length];
            var methodParameters = Parameters;
            try
            {
                for (var i = 0; i < arguments.Length; i++)
                {
                    var parameterType = methodParameters[i].ParameterType;

                    if (typeof(JsValue).IsAssignableFrom(parameterType))
                    {
                        parameters[i] = arguments[i];
                    }
                    else
                    {
                        parameters[i] = _engine.ClrTypeConverter.Convert(
                            arguments[i].ToObject(),
                            parameterType,
                            System.Globalization.CultureInfo.InvariantCulture);
                    }
                }

                if (Method is MethodInfo m)
                {
                    var retVal = m.Invoke(instance, parameters);
                    return JsValue.FromObject(_engine, retVal);
                }
                else if (Method is ConstructorInfo c)
                {
                    var retVal = c.Invoke(parameters);
                    return JsValue.FromObject(_engine, retVal);
                }
                else
                {
                    throw new Exception("Method is unknown type");
                }
            }
            catch (TargetInvocationException exception)
            {
                ExceptionHelper.ThrowMeaningfulException(_engine, exception);
                return null;
            }
        }
    }
}