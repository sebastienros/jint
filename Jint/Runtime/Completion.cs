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
        internal static readonly Completion Empty = new Completion(CompletionType.Normal, null, null);
        internal static readonly Completion EmptyUndefined = new Completion(CompletionType.Normal, Undefined.Instance, null);

        public Completion(CompletionType type, JsValue value, string identifier, Location location = null)
        {
            Type = type;
            Value = value;
            Identifier = identifier;
            Location = location;
        }

        public readonly CompletionType Type;

        public readonly JsValue Value;

        public readonly string Identifier;

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

        public readonly Location Location;
    }
}