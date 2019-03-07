using System.Runtime.CompilerServices;
using Esprima;
using Jint.Native;

namespace Jint.Runtime
{
    public enum CompletionType
    {
        Normal,
        Break,
        Continue,
        Return,
        Throw
    }

    /// <summary>
    /// http://www.ecma-international.org/ecma-262/5.1/#sec-8.9
    /// </summary>
    public readonly struct Completion
    {
        public Completion(CompletionType type, JsValue value, string identifier, Location location)
        {
            Type = type;
            Value = value;
            Identifier = identifier;
            Location = location;
        }

        public readonly CompletionType Type;
        public readonly JsValue Value;
        public readonly string Identifier;
        public readonly Location Location;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public JsValue GetValueOrDefault()
        {
            return Value ?? Undefined.Instance;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsAbrupt()
        {
            return Type != CompletionType.Normal;
        }
    }
}