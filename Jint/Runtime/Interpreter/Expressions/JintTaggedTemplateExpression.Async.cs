using Jint.Native;
using System.Threading.Tasks;

namespace Jint.Runtime.Interpreter.Expressions
{
    internal sealed partial class JintTaggedTemplateExpression : JintExpression
    {
        protected async override Task<object> EvaluateInternalAsync()
        {
            var tagger = _engine.GetValue(await _tagIdentifier.GetValueAsync()) as ICallable
                ?? ExceptionHelper.ThrowTypeError<ICallable>(_engine, "Argument must be callable");

            var expressions = _quasi._expressions;

            var args = _engine._jsValueArrayPool.RentArray((expressions.Length + 1));

            var template = GetTemplateObject();
            args[0] = template;

            for (int i = 0; i < expressions.Length; ++i)
            {
                args[i + 1] = await expressions[i].GetValueAsync();
            }

            var result = await tagger.CallAsync(JsValue.Undefined, args);
            _engine._jsValueArrayPool.ReturnArray(args);

            return result;
        }
    }
}