using System;
using System.Threading.Tasks;
using Jint.Native;

namespace Jint.Runtime.Interpreter.Expressions
{
    internal sealed partial class JintNewExpression : JintExpression
    {
        protected async override Task<object> EvaluateInternalAsync()
        {
            // todo: optimize by defining a common abstract class or interface
            var jsValue = _calleeExpression.GetValue();

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
                await BuildArgumentsAsync(_jintArguments, arguments);
            }

            if (!jsValue.IsConstructor)
            {
                ExceptionHelper.ThrowTypeError(_engine, _calleeExpression.SourceText + " is not a constructor");
            }

            // construct the new instance using the Function's constructor method
            var instance = _engine.Construct((IConstructor)jsValue, arguments, jsValue, _calleeExpression);

            _engine._jsValueArrayPool.ReturnArray(arguments);

            return instance;
        }
    }
}