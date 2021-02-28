using Jint.Native.Array;
using System.Threading.Tasks;

namespace Jint.Runtime.Interpreter.Expressions
{
    internal sealed partial class JintArrayExpression : JintExpression
    {
        protected async override Task<object> EvaluateInternalAsync()
        {
            var a = _engine.Array.ConstructFast(_hasSpreads ? 0 : (uint)_expressions.Length);
            var expressions = _expressions;

            uint arrayIndexCounter = 0;
            for (uint i = 0; i < (uint)expressions.Length; i++)
            {
                var expr = expressions[i];
                if (expr == null)
                {
                    arrayIndexCounter++;
                    continue;
                }

                if (_hasSpreads && expr is JintSpreadExpression jse)
                {
                    jse.GetValueAndCheckIterator(out var objectInstance, out var iterator);
                    // optimize for array
                    if (objectInstance is ArrayInstance ai)
                    {
                        var length = ai.GetLength();
                        var newLength = arrayIndexCounter + length;
                        a.EnsureCapacity(newLength);
                        a.CopyValues(ai, sourceStartIndex: 0, targetStartIndex: arrayIndexCounter, length);
                        arrayIndexCounter += length;
                        a.SetLength(newLength);
                    }
                    else
                    {
                        var protocol = new ArraySpreadProtocol(_engine, a, iterator, arrayIndexCounter);
                        protocol.Execute();
                        arrayIndexCounter += protocol._addedCount;
                    }
                }
                else
                {
                    var value = await expr.GetValueAsync();
                    a.SetIndexValue(arrayIndexCounter++, value, updateLength: false);
                }
            }

            if (_hasSpreads)
            {
                a.SetLength(arrayIndexCounter);
            }

            return a;
        }
    }
}