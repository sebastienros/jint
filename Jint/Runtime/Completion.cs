#nullable enable

using System.Runtime.CompilerServices;
using Esprima;
using Jint.Native;
using Jint.Runtime.Interpreter.Expressions;

namespace Jint.Runtime
{
    public enum CompletionType
    {
        Normal = 0,
        Return = 1,
        Throw = 2,
        Break,
        Continue
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-completion-record-specification-type
    /// </summary>
    public readonly struct Completion
    {
        internal Completion(CompletionType type, JsValue value, string? target, in Location location)
        {
            Type = type;
            Value = value;
            Target = target;
            Location = location;
        }

        public Completion(CompletionType type, JsValue value, in Location location)
            : this(type, value, null, location)
        {
        }

        public Completion(CompletionType type, string target, in Location location)
            : this(type, null!, target, location)
        {
        }

        internal Completion(in ExpressionResult result)
        {
            Type = (CompletionType) result.Type;
            // this cast protects us from getting from type
            Value = (JsValue) result.Value;
            Target = null;
            Location = result.Location;
        }

        public readonly CompletionType Type;
        public readonly JsValue Value;
        public readonly string? Target;
        public readonly Location Location;

        public static Completion Normal(JsValue value, in Location location)
            => new Completion(CompletionType.Normal, value, location);

        public static Completion Empty()
            => new Completion(CompletionType.Normal, null!, default);

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

        /// <summary>
        /// https://tc39.es/ecma262/#sec-updateempty
        /// </summary>
        internal Completion UpdateEmpty(JsValue value)
        {
            if (Value is not null)
            {
                return this;
            }

            return new Completion(Type, value, Target, Location);
        }
    }
}