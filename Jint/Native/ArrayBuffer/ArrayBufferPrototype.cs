using Jint.Collections;
using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.ArrayBuffer
{
    /// <summary>
    /// https://tc39.es/ecma262/#sec-properties-of-the-arraybuffer-prototype-object
    /// </summary>
    public sealed class ArrayBufferPrototype : ObjectInstance
    {
        private readonly Realm _realm;
        private readonly ArrayBufferConstructor _constructor;

        internal ArrayBufferPrototype(
            Engine engine,
            Realm realm,
            ArrayBufferConstructor constructor,
            ObjectPrototype objectPrototype) : base(engine, 0)
        {
            _prototype = objectPrototype;
            _realm = realm;
            _constructor = constructor;
        }

        protected override void Initialize()
        {
            const PropertyFlag lengthFlags = PropertyFlag.Configurable;
            var properties = new PropertyDictionary(3, checkExistingKeys: false)
            {
                ["byteLength"] = new GetSetPropertyDescriptor(new ClrFunctionInstance(_engine, "get byteLength", ByteLength, 0, lengthFlags), Undefined, PropertyFlag.Configurable),
                ["constructor"] = new PropertyDescriptor(_constructor, PropertyFlag.NonEnumerable),
                ["slice"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "slice", Slice, 2, lengthFlags), PropertyFlag.Configurable | PropertyFlag.Writable)
            };
            SetProperties(properties);

            var symbols = new SymbolDictionary(1)
            {
                [GlobalSymbolRegistry.ToStringTag] = new PropertyDescriptor("ArrayBuffer", PropertyFlag.Configurable)
            };
            SetSymbols(symbols);
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-get-arraybuffer.prototype.bytelength
        /// </summary>
        private JsValue ByteLength(JsValue thisObj, JsValue[] arguments)
        {
            var o = thisObj as ArrayBufferInstance;
            if (o is null)
            {
                ExceptionHelper.ThrowTypeError(_realm, "Method ArrayBuffer.prototype.byteLength called on incompatible receiver " + thisObj);
            }

            if (o.IsSharedArrayBuffer)
            {
                ExceptionHelper.ThrowTypeError(_realm);
            }

            if (o.IsDetachedBuffer)
            {
                return JsNumber.PositiveZero;
            }

            return JsNumber.Create(o.ArrayBufferByteLength);
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-arraybuffer.prototype.slice
        /// </summary>
        private JsValue Slice(JsValue thisObj, JsValue[] arguments)
        {
            var o = thisObj as ArrayBufferInstance;
            if (o is null)
            {
                ExceptionHelper.ThrowTypeError(_realm, "Method ArrayBuffer.prototype.slice called on incompatible receiver " + thisObj);
            }

            if (o.IsSharedArrayBuffer)
            {
                ExceptionHelper.ThrowTypeError(_realm);
            }

            o.AssertNotDetached();

            var start = arguments.At(0);
            var end = arguments.At(1);

            var len = o.ArrayBufferByteLength;
            var relativeStart = TypeConverter.ToIntegerOrInfinity(start);
            var first = relativeStart switch
            {
                double.NegativeInfinity => 0,
                < 0 => (int) System.Math.Max(len + relativeStart, 0),
                _ => (int) System.Math.Min(relativeStart, len)
            };

            double relativeEnd;
            if (end.IsUndefined())
            {
                relativeEnd = len;
            }
            else
            {
                relativeEnd = TypeConverter.ToIntegerOrInfinity(end);
            }

            var final = relativeEnd switch
            {
                double.NegativeInfinity => 0,
                < 0 => (int) System.Math.Max(len + relativeEnd, 0),
                _ => (int) System.Math.Min(relativeEnd, len)
            };

            var newLen = System.Math.Max(final - first, 0);
            var ctor = SpeciesConstructor(o, _realm.Intrinsics.ArrayBuffer);
            var bufferInstance = Construct(ctor, new JsValue[] { JsNumber.Create(newLen) }) as ArrayBufferInstance;

            if (bufferInstance is null)
            {
                ExceptionHelper.ThrowTypeError(_realm);
            }
            if (bufferInstance.IsSharedArrayBuffer)
            {
                ExceptionHelper.ThrowTypeError(_realm);
            }
            if (bufferInstance.IsDetachedBuffer)
            {
                ExceptionHelper.ThrowTypeError(_realm);
            }

            if (ReferenceEquals(bufferInstance, o))
            {
                ExceptionHelper.ThrowTypeError(_realm);
            }

            if (bufferInstance.ArrayBufferByteLength < newLen)
            {
                ExceptionHelper.ThrowTypeError(_realm);
            }

            // NOTE: Side-effects of the above steps may have detached O.

            if (bufferInstance.IsDetachedBuffer)
            {
                ExceptionHelper.ThrowTypeError(_realm);
            }

            var fromBuf = o.ArrayBufferData;
            var toBuf = bufferInstance.ArrayBufferData;
            System.Array.Copy(fromBuf, first, toBuf, 0, newLen);
            return bufferInstance;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-copydatablockbytes
        /// </summary>
        /// <remarks>
        /// Here only to support algorithm of shared view buffer.
        /// </remarks>
        private void CopyDataBlockBytes(byte[] toBlock, int toIndex, byte[] fromBlock, int fromIndex, int count)
        {
            var fromSize = Length;
            var toSize = Length;

            bool isSharedDataBlock = false;

            while (count > 0)
            {
                if (isSharedDataBlock)
                {
                    /*
                    i. Let execution be the [[CandidateExecution]] field of the surrounding agent's Agent Record.
                        ii. Let eventList be the [[EventList]] field of the element in execution.[[EventsRecords]] whose [[AgentSignifier]] is AgentSignifier().
                        iii. Let bytes be a List whose sole element is a nondeterministically chosen byte value.
                        iv. NOTE: In implementations, bytes is the result of a non-atomic read instruction on the underlying hardware. The nondeterminism is a semantic prescription of the memory model to describe observable behaviour of hardware with weak consistency.
                        v. Let readEvent be ReadSharedMemory { [[Order]]: Unordered, [[NoTear]]: true, [[Block]]: fromBlock, [[ByteIndex]]: fromIndex, [[ElementSize]]: 1 }.
                    vi. Append readEvent to eventList.
                        vii. Append Chosen Value Record { [[Event]]: readEvent, [[ChosenValue]]: bytes } to execution.[[ChosenValues]].
                    viii. If toBlock is a Shared Data Block, then
                    1. Append WriteSharedMemory { [[Order]]: Unordered, [[NoTear]]: true, [[Block]]: toBlock, [[ByteIndex]]: toIndex, [[ElementSize]]: 1, [[Payload]]: bytes } to eventList.
                        ix. Else,
                    1. Set toBlock[toIndex] to bytes[0].
                    */
                    ExceptionHelper.ThrowNotImplementedException();
                }
                else
                {
                    toBlock[toIndex] = fromBlock[fromIndex];
                }

                toIndex++;
                fromIndex++;
                count--;
            }
        }
    }
}