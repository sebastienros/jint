using System;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using Jint.Extensions;
using Jint.Native;
using Jint.Native.Function;

namespace Jint.Runtime.Interop
{
    internal sealed class MethodInfoFunctionInstance : FunctionInstance
    {
        private static readonly JsString _name = new JsString("Function");
        private readonly MethodDescriptor[] _methods;
        private readonly ClrFunctionInstance _fallbackClrFunctionInstance;

        public MethodInfoFunctionInstance(Engine engine, MethodDescriptor[] methods)
            : base(engine, engine.Realm, _name)
        {
            _methods = methods;
            _prototype = engine.Realm.Intrinsics.Function.PrototypeObject;
        }

        public MethodInfoFunctionInstance(Engine engine, MethodDescriptor[] methods, ClrFunctionInstance fallbackClrFunctionInstance)
            : this(engine, methods)
        {
            _fallbackClrFunctionInstance = fallbackClrFunctionInstance;
        }

        private bool HandleGenericParameter(object argObj, Type parameterType, object[] parameters, int parameterIndex, Type[] genericArgTypes)
        {
            var result = TypeConverter.IsAssignableToGenericType(argObj?.GetType(), parameterType);
            if (result.Score < 0)
            {
                return false;
            }

            if (parameterType.IsGenericParameter)
            {
                var genericParamPosition = parameterType.GenericParameterPosition;
                if (genericParamPosition >= 0)
                {
                    genericArgTypes[genericParamPosition] = argObj.GetType();
                }
            }
            else if (parameterType.IsGenericType)
            {
                // TPC: maybe we can pull the generic parameters from the arguments?
                var genericArgs = parameterType.GetGenericArguments();
                for (int j = 0; j < genericArgs.Length; ++j)
                {
                    var genericArg = genericArgs[j];
                    if (genericArg.IsGenericParameter)
                    {
                        var genericParamPosition = genericArg.GenericParameterPosition;
                        if (genericParamPosition >= 0)
                        {
                            var givenTypeGenericArgs = result.MatchingGivenType.GetGenericArguments();
                            genericArgTypes[genericParamPosition] = givenTypeGenericArgs[j]; // is this string in my test?
                        }
                    }
                }
            }
            else
            {
                return false;
            }
            return true;
        }

        public override JsValue Call(JsValue thisObject, JsValue[] jsArguments)
        {
            JsValue[] ArgumentProvider(MethodDescriptor method)
            {
                if (method.IsExtensionMethod)
                {
                    var jsArgumentsTemp = new JsValue[1 + jsArguments.Length];
                    jsArgumentsTemp[0] = thisObject;
                    Array.Copy(jsArguments, 0, jsArgumentsTemp, 1, jsArguments.Length);
                    return method.HasParams
                        ? ProcessParamsArrays(jsArgumentsTemp, method)
                        : jsArgumentsTemp;
                }

                return method.HasParams
                    ? ProcessParamsArrays(jsArguments, method)
                    : jsArguments;
            }

            var converter = Engine.ClrTypeConverter;
            var thisObj = thisObject.ToObject();
            var thisObjType = thisObj?.GetType();
            object[] parameters = null;
            foreach (var (method, arguments, _) in TypeConverter.FindBestMatch(_engine, _methods, ArgumentProvider))
            {
                var methodParameters = method.Parameters;

                if (parameters == null || parameters.Length != methodParameters.Length)
                {
                    parameters = new object[methodParameters.Length];
                }

                var argumentsMatch = true;
                Type[] genericArgTypes = null;
                Type[] originalGenericArgTypes = null;
                if (method.Method.IsGenericMethod)
                {
                    var methodGenericArgs = method.Method.GetGenericArguments();
                    originalGenericArgTypes = methodGenericArgs;
                    genericArgTypes = new Type[methodGenericArgs.Length];
                }

                for (var i = 0; i < parameters.Length; i++)
                {
                    var methodParameter = methodParameters[i];
                    var parameterType = methodParameter.ParameterType;
                    var argument = arguments.Length > i ? arguments[i] : null;

                    if (typeof(JsValue).IsAssignableFrom(parameterType))
                    {
                        parameters[i] = argument;
                    }
                    else if (argument is null)
                    {
                        // optional
                        parameters[i] = System.Type.Missing;
                    }
                    else if (HandleGenericParameter(argument.ToObject(), parameterType, parameters, i, genericArgTypes)) // don't think we need the condition preface of (argument == null) because of earlier condition
                    {
                        parameters[i] = argument.ToObject();
                    }
                    else if (parameterType == typeof(JsValue[]) && argument.IsArray())
                    {
                        // Handle specific case of F(params JsValue[])
                        var arrayInstance = argument.AsArray();
                        var len = TypeConverter.ToInt32(arrayInstance.Get(CommonProperties.Length, this));
                        var result = new JsValue[len];
                        for (uint k = 0; k < len; k++)
                        {
                            result[k] = arrayInstance.TryGetValue(k, out var value) ? value : Undefined;
                        }

                        parameters[i] = result;
                    }
                    else
                    {
                        if (!ReflectionExtensions.TryConvertViaTypeCoercion(parameterType, _engine.Options.Interop.ValueCoercion, argument, out parameters[i])
                            && !converter.TryConvert(argument.ToObject(), parameterType, CultureInfo.InvariantCulture, out parameters[i]))
                        {
                            argumentsMatch = false;
                            break;
                        }

                        if (parameters[i] is LambdaExpression lambdaExpression)
                        {
                            parameters[i] = lambdaExpression.Compile();
                        }
                    }
                }

                if (!argumentsMatch)
                {
                    continue;
                }

                // todo: cache method info
                try
                {
                    if ((method.Method.IsGenericMethodDefinition) && (method.Method is MethodInfo methodInfo))
                    {
                        var genericMethodInfo = methodInfo.MakeGenericMethod(genericArgTypes);
                        var result = genericMethodInfo.Invoke(thisObj, parameters);
                        return FromObject(Engine, result);
                    }
                    else
                    {
                        return FromObject(Engine, method.Method.Invoke(thisObj, parameters));
                    }
                }
                catch (TargetInvocationException exception)
                {
                    ExceptionHelper.ThrowMeaningfulException(_engine, exception);
                }
            }

            if (_fallbackClrFunctionInstance is not null)
            {
                return _fallbackClrFunctionInstance.Call(thisObject, jsArguments);
            }

            ExceptionHelper.ThrowTypeError(_engine.Realm, "No public methods with the specified arguments were found.");
            return null;
        }

        /// <summary>
        /// Reduces a flat list of parameters to a params array, if needed
        /// </summary>
        private JsValue[] ProcessParamsArrays(JsValue[] jsArguments, MethodDescriptor methodInfo)
        {
            var parameters = methodInfo.Parameters;

            var nonParamsArgumentsCount = parameters.Length - 1;
            if (jsArguments.Length < nonParamsArgumentsCount)
            {
                return jsArguments;
            }

            var argsToTransform = jsArguments.Skip(nonParamsArgumentsCount);

            if (argsToTransform.Length == 1 && argsToTransform[0].IsArray())
            {
                return jsArguments;
            }

            var jsArray = Engine.Realm.Intrinsics.Array.Construct(Arguments.Empty);
            Engine.Realm.Intrinsics.Array.PrototypeObject.Push(jsArray, argsToTransform);

            var newArgumentsCollection = new JsValue[nonParamsArgumentsCount + 1];
            for (var j = 0; j < nonParamsArgumentsCount; ++j)
            {
                newArgumentsCollection[j] = jsArguments[j];
            }

            newArgumentsCollection[nonParamsArgumentsCount] = jsArray;
            return newArgumentsCollection;
        }
    }
}
