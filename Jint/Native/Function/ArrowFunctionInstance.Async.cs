using Jint.Runtime;
using Jint.Runtime.Environments;
using System.Threading.Tasks;

namespace Jint.Native.Function
{
    public sealed partial class ArrowFunctionInstance : FunctionInstance
    {
        public async override Task<JsValue> CallAsync(JsValue thisObject, JsValue[] arguments)
        {
            var strict = Strict || _engine._isStrict;
            using (new StrictModeScope(strict, true))
            {
                var localEnv = LexicalEnvironment.NewFunctionEnvironment(_engine, this, Undefined);
                _engine.EnterExecutionContext(localEnv, localEnv);

                try
                {
                    _engine.FunctionDeclarationInstantiation(
                        functionInstance: this,
                        arguments,
                        localEnv);

                    var result = await _function.ExecuteAsync();

                    var value = result.GetValueOrDefault().Clone();

                    if (result.Type == CompletionType.Throw)
                    {
                        ExceptionHelper.ThrowJavaScriptException(_engine, value, result);
                    }

                    if (result.Type == CompletionType.Return)
                    {
                        return value;
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