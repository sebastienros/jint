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
            var callee = await _calleeExpression.EvaluateAsync();
            var expression = (CallExpression) _expression;

            if (_isDebugMode)
            {
                _engine.DebugHandler.AddToDebugCallStack(expression);
            }

            // todo: implement as in http://www.ecma-international.org/ecma-262/5.1/#sec-11.2.4

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
                        arguments = BuildArgumentsWithSpreads(cachedArguments.JintArguments);
                    }
                    else
                    {
                        arguments = _engine._jsValueArrayPool.RentArray(cachedArguments.JintArguments.Length);
                        await BuildArgumentsAsync(cachedArguments.JintArguments, arguments);
                    }
                }
            }


            var func = _engine.GetValue(callee, false);
            var r = callee as Reference;

            if (_maxRecursionDepth >= 0)
            {
                var stackItem = new CallStackElement(expression, func, r?.GetReferencedName()?.ToString() ?? "anonymous function");

                var recursionDepth = _engine.CallStack.Push(stackItem);

                if (recursionDepth > _maxRecursionDepth)
                {
                    _engine.CallStack.Pop();
                    ExceptionHelper.ThrowRecursionDepthOverflowException(_engine.CallStack, stackItem.ToString());
                }
            }

            if (func._type == InternalTypes.Undefined)
            {
                ExceptionHelper.ThrowTypeError(_engine, r == null ? "" : $"Object has no method '{r.GetReferencedName()}'");
            }

            if (!func.IsObject())
            {
                if (_engine._referenceResolver == null || !_engine._referenceResolver.TryGetCallable(_engine, callee, out func))
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
                    thisObject = baseValue;
                }
                else
                {
                    var env = (EnvironmentRecord) baseValue;
                    thisObject = env.ImplicitThisValue();
                }

                // is it a direct call to eval ? http://www.ecma-international.org/ecma-262/5.1/#sec-15.1.2.1.1
                if (r.GetReferencedName() == CommonProperties.Eval && callable is EvalFunctionInstance instance)
                {
                    var value = instance.Call(thisObject, arguments, true);
                    _engine._referencePool.Return(r);
                    return value;
                }
            }

            var result = await callable.CallAsync(thisObject, arguments);

            if (_isDebugMode)
            {
                _engine.DebugHandler.PopDebugCallStack();
            }

            if (_maxRecursionDepth >= 0)
            {
                _engine.CallStack.Pop();
            }

            if (!_cached && arguments.Length > 0)
            {
                _engine._jsValueArrayPool.ReturnArray(arguments);
            }

            _engine._referencePool.Return(r);
            return result;
        }
    }
}