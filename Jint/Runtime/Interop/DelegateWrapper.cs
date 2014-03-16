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

        public override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
            var parameterInfos = _d.Method.GetParameters();

            // convert parameter to expected types
            var parameters = new object[parameterInfos.Length];
            for (var i = 0; i < arguments.Length; i++)
            {
                var parameterType = parameterInfos[i].ParameterType;

                if (parameterType == typeof (JsValue))
                {
                    parameters[i] = arguments[i];
                }
                else
                {
                    parameters[i] = Engine.Options.GetTypeConverter().Convert(
                        arguments[i].ToObject(),
                        parameterType,
                        CultureInfo.InvariantCulture);
                }
            }

            // assign null to parameters not provided
            for (var i = arguments.Length; i < parameterInfos.Length; i++)
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

            return JsValue.FromObject(Engine, _d.DynamicInvoke(parameters));
        }
    }
}
