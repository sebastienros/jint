using Esprima.Ast;
using Jint.Native;
using Jint.Native.Function;
using Jint.Runtime.Environments;
using Jint.Runtime.References;
using System.Threading.Tasks;

namespace Jint.Runtime.Interpreter.Expressions
{
    internal sealed partial class JintCallExpression : JintExpression
    {
        protected async override Task<object> EvaluateInternalAsync()
        {
            return _calleeExpression is JintSuperExpression
                ? await SuperCallAsync()
                : await CallAsync();
        }

        private Task<object> SuperCallAsync()
        {
            return Task.FromResult(SuperCall());
        }

        private async Task<object> CallAsync()
        {
            var callee = _calleeExpression.Evaluate();
            var expression = (CallExpression)_expression;

            // todo: implement as in http://www.ecma-international.org/ecma-262/5.1/#sec-11.2.4

            var arguments = await ArgumentListEvaluationAsync();

            var func = _engine.GetValue(callee, false);
            var r = callee as Reference;

            if (func._type == InternalTypes.Undefined)
            {
                ExceptionHelper.ThrowTypeError(_engine, r == null ? "" : $"Object has no method '{r.GetReferencedName()}'");
            }

            if (!func.IsObject())
            {
                if (!_engine._referenceResolver.TryGetCallable(_engine, callee, out func))
                {
                    ExceptionHelper.ThrowTypeError(_engine,
                        r == null ? "" : $"Property '{r.GetReferencedName()}' of object is not a function");
                }
            }

            if (!(func is ICallable callable))
            {
                var message = $"{r?.GetReferencedName() ?? ""} is not a function";
                return ExceptionHelper.ThrowTypeError<object>(_engine, message);
            }

            var thisObject = Undefined.Instance;
            if (r != null)
            {
                var baseValue = r.GetBase();
                if ((baseValue._type & InternalTypes.ObjectEnvironmentRecord) == 0)
                {
                    thisObject = r.GetThisValue();
                }
                else
                {
                    var env = (EnvironmentRecord)baseValue;
                    thisObject = env.ImplicitThisValue();
                }

                // is it a direct call to eval ? http://www.ecma-international.org/ecma-262/5.1/#sec-15.1.2.1.1
                if (r.GetReferencedName() == CommonProperties.Eval && callable is EvalFunctionInstance instance)
                {
                    var value = await instance.PerformEvalAsync(arguments, true);
                    _engine._referencePool.Return(r);
                    return value;
                }
            }

            var result = await _engine.CallAsync(callable, thisObject, arguments, _calleeExpression);

            if (!_cached && arguments.Length > 0)
            {
                _engine._jsValueArrayPool.ReturnArray(arguments);
            }

            _engine._referencePool.Return(r);
            return result;
        }

        private async Task<JsValue[]> ArgumentListEvaluationAsync()
        {
            var cachedArguments = _cachedArguments;
            var arguments = System.Array.Empty<JsValue>();
            if (_cached)
            {
                arguments = cachedArguments.CachedArguments;
            }
            else
            {
                if (cachedArguments.JintArguments.Length > 0)
                {
                    if (_hasSpreads)
                    {
                        arguments = await BuildArgumentsWithSpreadsAsync(cachedArguments.JintArguments);
                    }
                    else
                    {
                        arguments = _engine._jsValueArrayPool.RentArray(cachedArguments.JintArguments.Length);
                        await BuildArgumentsAsync(cachedArguments.JintArguments, arguments);
                    }
                }
            }

            return arguments;
        }
    }
}