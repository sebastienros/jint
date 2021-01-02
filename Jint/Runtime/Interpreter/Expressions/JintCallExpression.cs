using Esprima.Ast;
using Jint.Native;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime.Environments;
using Jint.Runtime.References;

namespace Jint.Runtime.Interpreter.Expressions
{
    internal sealed class JintCallExpression : JintExpression
    {
        private CachedArgumentsHolder _cachedArguments;
        private bool _cached;

        private JintExpression _calleeExpression;
        private bool _hasSpreads;

        public JintCallExpression(Engine engine, CallExpression expression) : base(engine, expression)
        {
            _initialized = false;
        }

        protected override void Initialize()
        {
            var expression = (CallExpression) _expression;
            _calleeExpression = Build(_engine, expression.Callee);
            var cachedArgumentsHolder = new CachedArgumentsHolder
            {
                JintArguments = new JintExpression[expression.Arguments.Count]
            };

            static bool CanSpread(Node e)
            {
                return e?.Type == Nodes.SpreadElement
                    || e is AssignmentExpression ae && ae.Right?.Type == Nodes.SpreadElement;
            }

            bool cacheable = true;
            for (var i = 0; i < expression.Arguments.Count; i++)
            {
                var expressionArgument = expression.Arguments[i];
                cachedArgumentsHolder.JintArguments[i] = Build(_engine, expressionArgument);
                cacheable &= expressionArgument.Type == Nodes.Literal;
                _hasSpreads |= CanSpread(expressionArgument);
                if (expressionArgument is ArrayExpression ae)
                {
                    for (var elementIndex = 0; elementIndex < ae.Elements.Count; elementIndex++)
                    {
                        _hasSpreads |= CanSpread(ae.Elements[elementIndex]);
                    }
                }
            }

            if (cacheable)
            {
                _cached = true;
                var arguments = System.Array.Empty<JsValue>();
                if (cachedArgumentsHolder.JintArguments.Length > 0)
                {
                    arguments = new JsValue[cachedArgumentsHolder.JintArguments.Length];
                    BuildArguments(cachedArgumentsHolder.JintArguments, arguments);
                }

                cachedArgumentsHolder.CachedArguments = arguments;
            }

            _cachedArguments = cachedArgumentsHolder;
        }

        protected override object EvaluateInternal()
        {
            return _calleeExpression is JintSuperExpression 
                ? SuperCall()
                : Call();
        }

        private object SuperCall()
        {
            var thisEnvironment = (FunctionEnvironmentRecord) _engine.GetThisEnvironment();
            var newTarget = GetNewTarget(thisEnvironment);
            var func = GetSuperConstructor(thisEnvironment);
            if (!func.IsConstructor)
            {
                ExceptionHelper.ThrowTypeError(_engine, "Not a constructor");
            }

            var argList = ArgumentListEvaluation();
            var result = ((IConstructor) func).Construct(argList, newTarget);
            var thisER = (FunctionEnvironmentRecord) _engine.GetThisEnvironment();
            return thisER.BindThisValue(result);
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-getsuperconstructor
        /// </summary>
        private ObjectInstance GetSuperConstructor(FunctionEnvironmentRecord thisEnvironment)
        {
            var envRec = thisEnvironment;
            var activeFunction = envRec._functionObject;
            var superConstructor = activeFunction.GetPrototypeOf();
            return superConstructor;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-getnewtarget
        /// </summary>
        private JsValue GetNewTarget(FunctionEnvironmentRecord thisEnvironment)
        {
            return thisEnvironment.NewTarget;
        }

        private object Call()
        {
            var callee = _calleeExpression.Evaluate();
            var expression = (CallExpression) _expression;

            // todo: implement as in http://www.ecma-international.org/ecma-262/5.1/#sec-11.2.4

            var arguments = ArgumentListEvaluation();

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
                    var env = (EnvironmentRecord) baseValue;
                    thisObject = env.ImplicitThisValue();
                }

                // is it a direct call to eval ? http://www.ecma-international.org/ecma-262/5.1/#sec-15.1.2.1.1
                if (r.GetReferencedName() == CommonProperties.Eval && callable is EvalFunctionInstance instance)
                {
                    var value = instance.PerformEval(arguments, true);
                    _engine._referencePool.Return(r);
                    return value;
                }
            }

            var result = _engine.Call(callable, thisObject, arguments, _calleeExpression);

            if (!_cached && arguments.Length > 0)
            {
                _engine._jsValueArrayPool.ReturnArray(arguments);
            }

            _engine._referencePool.Return(r);
            return result;
        }

        private JsValue[] ArgumentListEvaluation()
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
                        arguments = BuildArgumentsWithSpreads(cachedArguments.JintArguments);
                    }
                    else
                    {
                        arguments = _engine._jsValueArrayPool.RentArray(cachedArguments.JintArguments.Length);
                        BuildArguments(cachedArguments.JintArguments, arguments);
                    }
                }
            }

            return arguments;
        }

        private class CachedArgumentsHolder
        {
            internal JintExpression[] JintArguments;
            internal JsValue[] CachedArguments;
        }
    }
}