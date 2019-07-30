using System;
using System.Globalization;
using System.Reflection;
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
        private readonly bool _delegateContainsParamsArgument;

        public DelegateWrapper(Engine engine, Delegate d)
            : base(engine, "delegate", null, null, false)
        {
            _d = d;
            Prototype = engine.Function.PrototypeObject;

            var parameterInfos = _d.Method.GetParameters();

            _delegateContainsParamsArgument = false;
            foreach (var p in parameterInfos)
            {
                if (Attribute.IsDefined(p, typeof(ParamArrayAttribute)))
                {
                    _delegateContainsParamsArgument = true;
                    break;
                }
            }
        }

        public override JsValue Call(JsValue thisObject, JsValue[] jsArguments)
        {
            var parameterInfos = _d.Method.GetParameters();

#if NETFRAMEWORK
            if (parameterInfos.Length > 0 && parameterInfos[0].ParameterType == typeof(System.Runtime.CompilerServices.Closure))
            {
                var reducedLength = parameterInfos.Length - 1;
                var reducedParameterInfos = new ParameterInfo[reducedLength];
                Array.Copy(parameterInfos, 1, reducedParameterInfos, 0, reducedLength);
                parameterInfos = reducedParameterInfos;
            }
#endif

            int delegateArgumentsCount = parameterInfos.Length;
            int delegateNonParamsArgumentsCount = _delegateContainsParamsArgument ? delegateArgumentsCount - 1 : delegateArgumentsCount;

            int jsArgumentsCount = jsArguments.Length;
            int jsArgumentsWithoutParamsCount = Math.Min(jsArgumentsCount, delegateNonParamsArgumentsCount);

            var parameters = new object[delegateArgumentsCount];

            // convert non params parameter to expected types
            for (var i = 0; i < jsArgumentsWithoutParamsCount; i++)
            {
                var parameterType = parameterInfos[i].ParameterType;

                if (parameterType == typeof(JsValue))
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
            for (var i = jsArgumentsWithoutParamsCount; i < delegateNonParamsArgumentsCount; i++)
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
            if (_delegateContainsParamsArgument)
            {
                int paramsArgumentIndex = delegateArgumentsCount - 1;
                int paramsCount = Math.Max(0, jsArgumentsCount - delegateNonParamsArgumentsCount);

                object[] paramsParameter = new object[paramsCount];
                var paramsParameterType = parameterInfos[paramsArgumentIndex].ParameterType.GetElementType();

                for (var i = paramsArgumentIndex; i < jsArgumentsCount; i++)
                {
                    var paramsIndex = i - paramsArgumentIndex;

                    if (paramsParameterType == typeof(JsValue))
                    {
                        paramsParameter[paramsIndex] = jsArguments[i];
                    }
                    else
                    {
                        paramsParameter[paramsIndex] = Engine.ClrTypeConverter.Convert(
                            jsArguments[i].ToObject(),
                            paramsParameterType,
                            CultureInfo.InvariantCulture);
                    }
                }
                parameters[paramsArgumentIndex] = paramsParameter;
            }
            try
            {
                return JsValue.FromObject(Engine, _d.DynamicInvoke(parameters));
            }
            catch (TargetInvocationException exception)
            {
                ExceptionHelper.ThrowMeaningfulException(_engine, exception);
                throw;
            }
        }
    }
}
