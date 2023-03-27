using System.Globalization;
using System.Reflection;
using Jint.Extensions;
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
        private static readonly JsString _name = new JsString("delegate");
        private readonly Delegate _d;
        private readonly bool _delegateContainsParamsArgument;

        public DelegateWrapper(
            Engine engine, Delegate d)
            : base(engine, engine.Realm, _name, FunctionThisMode.Global)
        {
            _d = d;
            _prototype = engine.Realm.Intrinsics.Function.PrototypeObject;

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

        protected internal override JsValue Call(JsValue thisObject, JsValue[] jsArguments)
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

            var clrTypeConverter = Engine.ClrTypeConverter;
            var valueCoercionType = Engine.Options.Interop.ValueCoercion;
            var parameters = new object?[delegateArgumentsCount];

            // convert non params parameter to expected types
            for (var i = 0; i < jsArgumentsWithoutParamsCount; i++)
            {
                var parameterType = parameterInfos[i].ParameterType;
                var value = jsArguments[i];
                object? converted;

                if (parameterType == typeof(JsValue))
                {
                    converted = value;
                }
                else if (!ReflectionExtensions.TryConvertViaTypeCoercion(parameterType, valueCoercionType, value, out converted))
                {
                    converted = clrTypeConverter.Convert(
                        value.ToObject(),
                        parameterType,
                        CultureInfo.InvariantCulture);
                }

                parameters[i] = converted;
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

            // assign params to array and converts each object to expected type
            if (_delegateContainsParamsArgument)
            {
                int paramsArgumentIndex = delegateArgumentsCount - 1;
                int paramsCount = Math.Max(0, jsArgumentsCount - delegateNonParamsArgumentsCount);

                var paramsParameterType = parameterInfos[paramsArgumentIndex].ParameterType.GetElementType();
                var paramsParameter = Array.CreateInstance(paramsParameterType!, paramsCount);

                for (var i = paramsArgumentIndex; i < jsArgumentsCount; i++)
                {
                    var paramsIndex = i - paramsArgumentIndex;
                    var value = jsArguments[i];
                    object? converted;

                    if (paramsParameterType == typeof(JsValue))
                    {
                        converted = value;
                    }
                    else if (!ReflectionExtensions.TryConvertViaTypeCoercion(paramsParameterType, valueCoercionType, value, out converted))
                    {
                        converted = Engine.ClrTypeConverter.Convert(
                            value.ToObject(),
                            paramsParameterType!,
                            CultureInfo.InvariantCulture);
                    }

                    paramsParameter.SetValue(converted, paramsIndex);
                }

                parameters[paramsArgumentIndex] = paramsParameter;
            }

            try
            {
                return FromObject(Engine, _d.DynamicInvoke(parameters));
            }
            catch (TargetInvocationException exception)
            {
                ExceptionHelper.ThrowMeaningfulException(_engine, exception);
                throw;
            }
        }
    }
}
