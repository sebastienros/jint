#nullable enable

using System;
using System.Runtime.CompilerServices;
using Jint.Native;

namespace Jint.Runtime
{
    public readonly struct Arguments
    {
        public static readonly Arguments Empty = new();

        private readonly object? _first= null;
        private readonly JsValue? _second = null;
        private readonly JsValue? _third = null;
        private readonly JsValue? _fourth = null;

        public readonly int Length = 0;

        public Arguments()
        {
        }

        public Arguments(JsValue arg1)
        {
            _first = arg1;
            Length = 1;
        }

        public Arguments(JsValue arg1, JsValue arg2)
        {
            _first = arg1;
            _second = arg2;
            Length = 2;
        }

        public Arguments(JsValue arg1, JsValue arg2, JsValue arg3)
        {
            _first = arg1;
            _second = arg2;
            _third = arg3;
            Length = 3;
        }

        public Arguments(JsValue arg1, JsValue arg2, JsValue arg3, JsValue arg4)
        {
            _first = arg1;
            _second = arg2;
            _third = arg3;
            _fourth = arg4;
            Length = 4;
        }

        private Arguments(JsValue arg1, JsValue arg2, JsValue arg3, JsValue arg4, int length)
        {
            _first = arg1;
            _second = arg2;
            _third = arg3;
            _fourth = arg4;
            Length = length;
        }

        public Arguments(JsValue[] argsArray, int length)
        {
            _first = length > 0 ? argsArray : JsValue.Undefined;
            Length = length;
        }

        /// <summary>
        /// Returns the arguments at the provided position or Undefined if not present
        /// </summary>
        public JsValue this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return At((uint) index); }
        }

        /// <summary>
        /// Returns the arguments at the provided position or Undefined if not present
        /// </summary>
        public JsValue this[uint index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return At(index); }
        }

        /// <summary>
        /// Returns the arguments at the provided position or Undefined if not present
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public JsValue At(uint index, JsValue undefinedValue)
        {
            if (Length == 0)
            {
                return undefinedValue;
            }
            
            if (_first is JsValue jsValue)
            {
                return index switch
                {
                    0 => jsValue,
                    1 => _second ?? undefinedValue,
                    2 => _third ?? undefinedValue,
                    3 => _fourth ?? undefinedValue,
                    _ => undefinedValue
                };
            }

            var args = (JsValue[]) _first!;
            return index < (uint) args.Length 
                ? args[index] 
                : undefinedValue;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public JsValue At(uint index)
        {
            return At(index, Undefined.Instance);
        }

        internal Arguments Skip(int count)
        {
            var newLength = Length - count;
            if (newLength <= 0)
            {
                return Empty;
            }

            var newArray = new JsValue[newLength];
            for (var i = 0; i < newArray.Length; ++i)
            {
                newArray[i] = this[count + i];
            }
            
            return new Arguments(newArray, newArray.Length);
        }

        internal Arguments SkipFirst()
        {
            if (Length == 0)
            {
                return Empty;
            }
            
            if (_first is JsValue)
            {
                return new Arguments(_second!, _third!, _fourth!, JsValue.Undefined, Length - 1);
            }

            return Skip(1);
        }

        public void CopyTo(Span<JsValue> target)
        {
            for (var i = 0; i < target.Length; ++i)
            {
                target[i] = this[i];
            }
        }

        internal Arguments WithFirstParameter(JsValue newFirst)
        {
            if (_first is JsValue currentFirst && Length < 4)
            {
                return new Arguments(newFirst, currentFirst, _second!, _third!, Length + 1);
            }
            
            // create new array
            var jsArgumentsTemp = new JsValue[1 + Length];
            jsArgumentsTemp[0] = newFirst;
            for (var i = 0; i < Length; ++i)
            {
                jsArgumentsTemp[i + 1] = this[i];
            }
            return new Arguments(jsArgumentsTemp, jsArgumentsTemp.Length);
        }
    }
}
