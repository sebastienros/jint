using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Environments;
using System.Threading.Tasks;

namespace Jint.Native.Function
{
    public abstract partial class FunctionInstance : ObjectInstance, ICallable
    {
        public abstract Task<JsValue> CallAsync(JsValue thisObject, JsValue[] arguments);

        protected async Task<Completion> OrdinaryCallEvaluateBodyAsync(
            JsValue[] arguments,
            ExecutionContext calleeContext)
        {
            var argumentsInstance = _engine.FunctionDeclarationInstantiation(
                functionInstance: this,
                arguments,
                calleeContext.LexicalEnvironment);

            var result = await _functionDefinition.ExecuteAsync();
            var value = result.GetValueOrDefault().Clone();

            argumentsInstance?.FunctionWasCalled();

            return new Completion(result.Type, value, result.Identifier, result.Location);
        }
    }
}