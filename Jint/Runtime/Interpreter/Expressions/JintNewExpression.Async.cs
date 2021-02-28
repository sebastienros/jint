using System;
using System.Threading.Tasks;
using Jint.Native;

namespace Jint.Runtime.Interpreter.Expressions
{
    internal sealed partial class JintNewExpression : JintExpression
    {
        protected async override Task<object> EvaluateInternalAsync()
        {
            JsValue[] arguments;
            if (_jintArguments.Length == 0)
            {
                arguments = Array.Empty<JsValue>();
            }
            else if (_hasSpreads)
            {
                arguments = BuildArgumentsWithSpreads(_jintArguments);
            }
            else
            {
                arguments = _engine._jsValueArrayPool.RentArray(_jintArguments.Length);
                BuildArguments(_jintArguments, arguments);
            }

            // todo: optimize by defining a common abstract class or interface
            var jsValue = await _calleeExpression.GetValueAsync();
            if (!(jsValue is IConstructor callee))
            {
                return ExceptionHelper.ThrowTypeError<object>(_engine, "The object can't be used as constructor.");
            }

            // construct the new instance using the Function's constructor method
            var instance = callee.Construct(arguments, jsValue);

            _engine._jsValueArrayPool.ReturnArray(arguments);

            return instance;
        }
    }
}