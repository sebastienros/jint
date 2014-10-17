using System;
using System.Globalization;
using System.Linq;
using Jint.Native;
using Jint.Native.Function;

namespace Jint.Runtime.Interop
{
    /// <summary>
    /// Represents a FunctionInstance wrapper around a CLR method. This is used by user to pass
    /// custom methods to the engine.
    /// </summary>
    public sealed class DelegateWrapper : FunctionInstance
    {
        private readonly Delegate _d;

        public DelegateWrapper(Engine engine, Delegate d) : base(engine, null, null, false)
        {
            _d = d;
        }

        public override JsValue Call(JsValue thisObject, JsValue[] jsArguments)
        {
            var parameterInfos = _d.Method.GetParameters();

            bool delegateContainsParamsArgument = parameterInfos.Any(p => Attribute.IsDefined(p, typeof(ParamArrayAttribute)));
            int delegateArgumentsCount = parameterInfos.Length;
            int delegateNonParamsArgumentsCount = delegateContainsParamsArgument ? delegateArgumentsCount - 1 : delegateArgumentsCount;

            var parameters = new object[delegateArgumentsCount];


            // convert non params parameter to expected types
            for (var i = 0; i < delegateNonParamsArgumentsCount; i++)
            {
                var parameterType = parameterInfos[i].ParameterType;

                if (parameterType == typeof (JsValue))
                {
                    parameters[i] = jsArguments[i];
                }
                else
                {
                    parameters[i] = Engine.ClrTypeConverter.Convert(
                        jsArguments[i].ToObject(),
                        parameterType,
                        CultureInfo.InvariantCulture);
                }
            }

            // assign null to parameters not provided
            for (var i = jsArguments.Length; i < delegateNonParamsArgumentsCount; i++)
            {
                if (parameterInfos[i].ParameterType.IsValueType)
                {
                    parameters[i] = Activator.CreateInstance(parameterInfos[i].ParameterType);
                }
                else
                {
                    parameters[i] = null;
                }
            }

            // assign params to array and converts each objet to expected type
            if(delegateContainsParamsArgument)
            {
                object[] paramsParameter = new object[jsArguments.Length - delegateNonParamsArgumentsCount];
                var paramsParameterType = parameterInfos[delegateArgumentsCount -1].ParameterType.GetElementType();

                for (var i = delegateNonParamsArgumentsCount; i < jsArguments.Length; i++)
                {
                    if (paramsParameterType == typeof(JsValue))
                    {
                        paramsParameter[i - delegateNonParamsArgumentsCount] = jsArguments[i];
                    }
                    else
                    {
                        paramsParameter[i - delegateNonParamsArgumentsCount] = Engine.ClrTypeConverter.Convert(
                            jsArguments[i].ToObject(),
                            paramsParameterType,
                            CultureInfo.InvariantCulture);
                    }                    
                }
                parameters[delegateNonParamsArgumentsCount] = paramsParameter;
            }

            return JsValue.FromObject(Engine, _d.DynamicInvoke(parameters));
        }
    }
}
