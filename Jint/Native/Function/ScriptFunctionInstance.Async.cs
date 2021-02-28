using Jint.Runtime;
using Jint.Runtime.Environments;
using System.Threading.Tasks;

namespace Jint.Native.Function
{
    public sealed partial class ScriptFunctionInstance : FunctionInstance, IConstructor
    {
        public async override Task<JsValue> CallAsync(JsValue thisArgument, JsValue[] arguments)
        {
            // ** PrepareForOrdinaryCall **
            // var callerContext = _engine.ExecutionContext;
            // Let calleeRealm be F.[[Realm]].
            // Set the Realm of calleeContext to calleeRealm.
            // Set the ScriptOrModule of calleeContext to F.[[ScriptOrModule]].
            var localEnv = LexicalEnvironment.NewFunctionEnvironment(_engine, this, Undefined);
            // If callerContext is not already suspended, suspend callerContext.
            // Push calleeContext onto the execution context stack; calleeContext is now the running execution context.
            // NOTE: Any exception objects produced after this point are associated with calleeRealm.
            // Return calleeContext.

            _engine.EnterExecutionContext(localEnv, localEnv);

            // ** OrdinaryCallBindThis **

            JsValue thisValue;
            if (_thisMode == FunctionThisMode.Strict)
            {
                thisValue = thisArgument;
            }
            else
            {
                if (thisArgument.IsNullOrUndefined())
                {
                    var globalEnv = _engine.GlobalEnvironment;
                    var globalEnvRec = (GlobalEnvironmentRecord)globalEnv._record;
                    thisValue = globalEnvRec.GlobalThisValue;
                }
                else
                {
                    thisValue = TypeConverter.ToObject(_engine, thisArgument);
                }
            }

            var envRec = (FunctionEnvironmentRecord)localEnv._record;
            envRec.BindThisValue(thisValue);

            // actual call

            var strict = _thisMode == FunctionThisMode.Strict || _engine._isStrict;
            using (new StrictModeScope(strict, true))
            {
                try
                {
                    var argumentsInstance = _engine.FunctionDeclarationInstantiation(
                        functionInstance: this,
                        arguments,
                        localEnv);

                    var result = await _function.ExecuteAsync();
                    var value = result.GetValueOrDefault().Clone();
                    argumentsInstance?.FunctionWasCalled();

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