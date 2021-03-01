using System.Runtime.CompilerServices;
using Esprima.Ast;
using Jint.Native;
using Jint.Native.Array;
using Jint.Native.Iterator;
using Jint.Native.Number;
using Jint.Native.Symbol;
using System.Threading.Tasks;

namespace Jint.Runtime.Interpreter.Expressions
{
    internal abstract partial class JintExpression
    {
        public async virtual Task<JsValue> GetValueAsync()
        {
            return await _engine.GetValueAsync(await EvaluateAsync(), true);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task<object> EvaluateAsync()
        {
            _engine._lastSyntaxNode = _expression;
            if (!_initialized)
            {
                Initialize();
                _initialized = true;
            }
            return EvaluateInternalAsync();
        }

        protected abstract Task<object> EvaluateInternalAsync();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected async static Task BuildArgumentsAsync(JintExpression[] jintExpressions, JsValue[] targetArray)
        {
            for (var i = 0; i < jintExpressions.Length; i++)
            {
                targetArray[i] = (await jintExpressions[i].GetValueAsync()).Clone();
            }
        }

        protected async Task<JsValue[]> BuildArgumentsWithSpreadsAsync(JintExpression[] jintExpressions)
        {
            var args = new System.Collections.Generic.List<JsValue>(jintExpressions.Length);
            for (var i = 0; i < jintExpressions.Length; i++)
            {
                var jintExpression = jintExpressions[i];
                if (jintExpression is JintSpreadExpression jse)
                {
                    var iterationState = await jse.GetValueAndCheckIteratorAsync();
                    var objectInstance = iterationState.instance;
                    var iterator = iterationState.iterator;

                    // optimize for array unless someone has touched the iterator
                    if (objectInstance is ArrayInstance ai
                        && ReferenceEquals(ai.Get(GlobalSymbolRegistry.Iterator), _engine.Array.PrototypeObject._originalIteratorFunction))
                    {
                        var length = ai.GetLength();
                        for (uint j = 0; j < length; ++j)
                        {
                            if (ai.TryGetValue(j, out var value))
                            {
                                args.Add(value);
                            }
                        }
                    }
                    else
                    {
                        var protocol = new ArraySpreadProtocol(_engine, args, iterator);
                        protocol.Execute();
                    }
                }
                else
                {
                    args.Add((await jintExpression.GetValueAsync()).Clone());
                }
            }

            return args.ToArray();
        }
    }
}