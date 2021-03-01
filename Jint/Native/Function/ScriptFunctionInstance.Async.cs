using Jint.Runtime;
using System.Threading.Tasks;

namespace Jint.Native.Function
{
    public sealed partial class ScriptFunctionInstance : FunctionInstance, IConstructor
    {
        public async override Task<JsValue> CallAsync(JsValue thisArgument, JsValue[] arguments)
        {
            if (_isClassConstructor)
            {
                ExceptionHelper.ThrowTypeError(_engine, $"Class constructor {_functionDefinition.Name} cannot be invoked without 'new'");
            }

            var calleeContext = PrepareForOrdinaryCall(Undefined);

            OrdinaryCallBindThis(calleeContext, thisArgument);

            // actual call

            var strict = _thisMode == FunctionThisMode.Strict || _engine._isStrict;
            using (new StrictModeScope(strict, true))
            {
                try
                {
                    var result = await OrdinaryCallEvaluateBodyAsync(arguments, calleeContext);

                    if (result.Type == CompletionType.Throw)
                    {
                        ExceptionHelper.ThrowJavaScriptException(_engine, result.Value, result);
                    }

                    if (result.Type == CompletionType.Return)
                    {
                        return result.Value;
                    }
                }
                finally
                {
                    _engine.LeaveExecutionContext();
                }

                return Undefined;
            }
        }
    }
}