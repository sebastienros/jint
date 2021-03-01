using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Jint.Native.Array;
using Jint.Native.Date;
using Jint.Native.Iterator;
using Jint.Native.Number;
using Jint.Native.Object;
using Jint.Native.RegExp;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native
{
    [DebuggerTypeProxy(typeof(JsValueDebugView))]
    public abstract partial class JsValue : IEquatable<JsValue>
    {
        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal async Task<IIterator> TryGetIteratorAsync(Engine engine)
        {
            var objectInstance = TypeConverter.ToObject(engine, this);

            if (!objectInstance.TryGetValue(GlobalSymbolRegistry.Iterator, out var value)
                || !(value is ICallable callable))
            {
                return null;
            }

            var obj = await callable.CallAsync(this, Arguments.Empty) as ObjectInstance
                ?? ExceptionHelper.ThrowTypeError<ObjectInstance>(engine, "Result of the Symbol.iterator method is not an object");

            if (obj is IIterator i) return i;
            return new IteratorInstance.ObjectIterator(obj);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task<JsValue> GetAsync(JsValue property)
        {
            return GetAsync(property, this);
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-8.12.3
        /// </summary>
        public virtual Task<JsValue> GetAsync(JsValue property, JsValue receiver)
        {
            return Task.FromResult(Undefined);
        }
    }
}
